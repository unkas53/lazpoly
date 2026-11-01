using System.Collections;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.UI;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Builds the board from data with meshes made in code (<see cref="MeshFactory"/>). When the
    /// Blender models arrive they replace these shapes; the layout and the API stay the same.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const float HalfSize = 6f;
        private const float TileHeight = 0.2f;

        private static readonly Color StartColor = new Color(0.40f, 0.73f, 0.42f);
        private static readonly Color TaxColor = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color RestColor = new Color(0.93f, 0.91f, 0.85f);
        private static readonly Color CardColor = new Color(0.98f, 0.80f, 0.42f);
        private static readonly Color PotColor = new Color(0.55f, 0.82f, 0.75f);
        private static readonly Color TravelColor = new Color(0.62f, 0.74f, 0.95f);
        private static readonly Color LookoutColor = new Color(0.80f, 0.70f, 0.95f);
        private static readonly Color TableColor = new Color(0.36f, 0.24f, 0.16f);
        private static readonly Color SeaColor = new Color(0.12f, 0.40f, 0.54f);
        private static readonly Color WaveColor = new Color(0.55f, 0.78f, 0.86f);
        private static readonly Color SandColor = new Color(0.90f, 0.82f, 0.62f);
        private static readonly Color GrassColor = new Color(0.42f, 0.62f, 0.36f);
        private static readonly Color TreeColor = new Color(0.22f, 0.46f, 0.28f);
        private static readonly Color BoatColor = new Color(0.95f, 0.93f, 0.86f);
        private static readonly Color PropertyBodyColor = new Color(0.96f, 0.94f, 0.88f);
        private static readonly Color TitleColor = new Color(0.95f, 0.86f, 0.55f);
        private static readonly Color PlaqueColor = new Color(0.17f, 0.15f, 0.14f);
        private static readonly Color LabelColor = new Color(0.97f, 0.94f, 0.87f);

        /// <summary>Where the group band's centre sits, outward from the square's centre, in tile widths.</summary>
        public const float BandOffset = 0.35f;
        public const float BandDepth = 0.28f;

        /// <summary>Where the name plaque's centre sits, outward from the square's centre, in tile widths.</summary>
        public const float PlaqueOffset = 0.04f;
        public const float PlaqueDepth = 0.32f;

        private GameObject[] _ownerMarkers;
        private GameObject[] _pennants;
        private Vector3[] _flagScale;
        private int[] _levels;
        private Color?[] _ownerColors;
        private GameObject[][] _buildings;
        private Vector3[][] _buildingRest;
        private GameObject[] _ownerFrames;
        private GameObject[] _tiles;
        private Color[] _tileColors;
        private readonly Dictionary<Collider, int> _squareOfCollider = new Dictionary<Collider, int>();
        private readonly HashSet<Transform> _animating = new HashSet<Transform>();
        private HashSet<int> _highlighted;
        private float _tile;
        private TextMesh _pot;

        public BoardLayout Layout { get; private set; }

        /// <summary>Top surface height of the tiles, where pawns stand.</summary>
        public float SurfaceY => TileHeight;

        /// <summary>Half the width of the table the board sits on.</summary>
        public float HalfExtent { get; private set; }

        public static BoardView Create(Transform parent, BoardData data)
        {
            var root = new GameObject("Board");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<BoardView>();
            view.Build(data);
            return view;
        }

        public Vector3 SquarePosition(int index) => transform.TransformPoint(Layout.SquareCenter(index)) + Vector3.up * SurfaceY;

        /// <summary>Where pawns stand on a square: its inner half, so the name and price on the outer half stay readable.</summary>
        public Vector3 PawnSpot(int index)
        {
            Vector3 center = Layout.SquareCenter(index);
            return transform.TransformPoint(center - Outward(center) * (_tile * 0.18f)) + Vector3.up * SurfaceY;
        }

        /// <summary>The square under a screen ray, if any.</summary>
        public bool TryGetSquare(Ray ray, out int squareIndex)
        {
            squareIndex = -1;
            return Physics.Raycast(ray, out RaycastHit hit, 500f) && _squareOfCollider.TryGetValue(hit.collider, out squareIndex);
        }

        /// <summary>Lights up these squares and dims the rest; null or empty shows the board normally.</summary>
        public void Highlight(ICollection<int> squares)
        {
            _highlighted = squares == null || squares.Count == 0 ? null : new HashSet<int>(squares);
            for (int i = 0; i < _tiles.Length; i++)
            {
                Color color = _tileColors[i];
                if (_highlighted != null) color = _highlighted.Contains(i) ? Color.Lerp(color, Color.white, 0.25f) : Color.Lerp(color, Color.black, 0.45f);
                Paint(_tiles[i], color);
            }
        }

        /// <summary>Shows what the pot holds in the middle of the board (boards without a pot show nothing).</summary>
        public void SetPot(int amount)
        {
            if (_pot != null) _pot.text = F($"Çay kumbarası\n{amount}");
        }

        /// <summary>The pot's label rocks when money drops in and swells when it is paid out.</summary>
        public void NudgePot(bool paidIn)
        {
            if (_pot != null && _animating.Add(_pot.transform)) StartCoroutine(Nudge(_pot.transform, paidIn));
        }

        private IEnumerator Nudge(Transform label, bool rock)
        {
            const float seconds = 0.45f;
            Quaternion rest = label.localRotation;
            Vector3 size = label.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (label == null) yield break;
                float fade = 1f - t;
                if (rock) label.localRotation = rest * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 4f) * 7f * fade);
                else label.localScale = size * (1f + 0.25f * Ease.Bump(t));
                yield return null;
            }

            if (label == null) yield break;
            label.localRotation = rest;
            label.localScale = size;
            _animating.Remove(label);
        }

        /// <summary>A short swell of a tile, for the start square when a pawn passes it.</summary>
        public void PulseTile(int squareIndex)
        {
            GameObject tile = _tiles[squareIndex];
            if (tile != null && _animating.Add(tile.transform)) StartCoroutine(Swell(tile.transform));
        }

        private IEnumerator Swell(Transform tile)
        {
            const float seconds = 0.5f;
            Vector3 size = tile.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (tile == null) yield break;
                tile.localScale = new Vector3(size.x * (1f + 0.06f * Ease.Bump(t)), size.y * (1f + 0.5f * Ease.Bump(t)), size.z * (1f + 0.06f * Ease.Bump(t)));
                yield return null;
            }

            if (tile == null) yield break;
            tile.localScale = size;
            _animating.Remove(tile);
        }

        /// <summary>
        /// Shows who owns a square: a flag in their colour where the buildings will stand, until the
        /// first building takes its place; the buildings themselves wear the owner's colour.
        /// Pass null to show it as unowned.
        /// </summary>
        public void SetOwner(int squareIndex, Color? ownerColor)
        {
            GameObject marker = _ownerMarkers[squareIndex];
            if (marker == null) return;

            _ownerColors[squareIndex] = ownerColor;
            RefreshOwnerMarker(squareIndex);
        }

        /// <summary>
        /// Takes a square back from its owner. With <paramref name="animate"/>, the flag shrinks into
        /// the tile first (a sale); without, the marker simply goes.
        /// </summary>
        public void ClearOwner(int squareIndex, bool animate)
        {
            GameObject flag = _ownerMarkers[squareIndex];
            if (flag == null) return;
            if (animate && flag.activeInHierarchy && _animating.Add(flag.transform))
            {
                StartCoroutine(Lower(flag.transform, _flagScale[squareIndex], () => SetOwner(squareIndex, null)));
                return;
            }

            SetOwner(squareIndex, null);
        }

        private IEnumerator Lower(Transform flag, Vector3 size, System.Action then)
        {
            const float seconds = 0.35f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (flag == null) yield break;
                flag.localScale = new Vector3(size.x, size.y * Mathf.Max(0.02f, 1f - Ease.InCubic(t)), size.z);
                yield return null;
            }

            if (flag == null) yield break;
            _animating.Remove(flag);
            then?.Invoke();
            flag.localScale = size;
        }

        /// <summary>Raises the owner's flag on a square that was just bought: it grows up from the tile and settles.</summary>
        public void RaiseFlag(int squareIndex)
        {
            GameObject flag = _ownerMarkers[squareIndex];
            if (flag != null && flag.activeInHierarchy) StartCoroutine(Raise(flag.transform, _flagScale[squareIndex]));
        }

        private static IEnumerator Raise(Transform flag, Vector3 size)
        {
            const float seconds = 0.6f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (flag == null) yield break;
                flag.localScale = new Vector3(size.x, size.y * Mathf.Max(0.02f, Ease.OutBack(t)), size.z);
                yield return null;
            }

            if (flag != null) flag.localScale = size;
        }

        /// <summary>
        /// Shows 1-4 small houses, or one hotel at the top level. With <paramref name="animate"/>, pieces
        /// that appear drop onto the tile and bounce, pieces that go shrink away, and the flag folds
        /// down when the first building takes its place.
        /// </summary>
        public void SetBuildingLevel(int squareIndex, int level, bool animate = false)
        {
            GameObject[] pieces = _buildings[squareIndex];
            if (pieces == null) return;

            bool hotel = level >= SquareDefinition.MaxBuildingLevel;
            _levels[squareIndex] = level;
            PaintBuildings(squareIndex);
            for (int i = 0; i < pieces.Length; i++)
            {
                bool shown = i == pieces.Length - 1 ? hotel : !hotel && i < level;
                Transform piece = pieces[i].transform;
                bool appears = shown && !pieces[i].activeSelf;
                bool goes = !shown && pieces[i].activeSelf;
                if (goes && animate && _animating.Add(piece))
                {
                    StartCoroutine(Shrink(piece, squareIndex, i));
                    continue;
                }

                pieces[i].SetActive(shown);
                piece.localPosition = _buildingRest[squareIndex][i];
                if (appears && animate) StartCoroutine(Drop(piece, _buildingRest[squareIndex][i]));
            }

            // The flag gives way to the first building and returns when the last one is sold.
            GameObject flag = _ownerMarkers[squareIndex];
            if (flag == null) return;
            bool flagWasUp = flag.activeSelf;
            RefreshOwnerMarker(squareIndex);
            if (animate && flagWasUp && !flag.activeSelf && _animating.Add(flag.transform))
            {
                flag.SetActive(true);
                StartCoroutine(Lower(flag.transform, _flagScale[squareIndex], () => RefreshOwnerMarker(squareIndex)));
            }
        }

        /// <summary>Whether piece <paramref name="index"/> of a square belongs on the table at its current level.</summary>
        private bool PieceShown(int squareIndex, int index)
        {
            int level = _levels[squareIndex];
            bool hotel = level >= SquareDefinition.MaxBuildingLevel;
            return index == _buildings[squareIndex].Length - 1 ? hotel : !hotel && index < level;
        }

        private IEnumerator Shrink(Transform piece, int squareIndex, int index)
        {
            const float seconds = 0.3f;
            Vector3 size = piece.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (piece == null) yield break;
                piece.localScale = size * Mathf.Max(0.01f, 1f - Ease.InCubic(t));
                yield return null;
            }

            if (piece == null) yield break;
            piece.localScale = size;
            piece.localPosition = _buildingRest[squareIndex][index];
            _animating.Remove(piece);
            piece.gameObject.SetActive(PieceShown(squareIndex, index));
        }

        /// <summary>Walls in the owner's colour, roofs a darker shade of it; the builder's own colours while unowned.</summary>
        private void PaintBuildings(int squareIndex)
        {
            GameObject[] pieces = _buildings[squareIndex];
            Color? owner = _ownerColors[squareIndex];
            foreach (GameObject piece in pieces)
            {
                foreach (Renderer part in piece.GetComponentsInChildren<Renderer>(true))
                {
                    bool roof = part.name == "Roof" || part.name == "Chimney";
                    Color color = owner.HasValue
                        ? (roof ? Color.Lerp(owner.Value, Color.black, 0.4f) : owner.Value)
                        : (roof ? BuildingParts.Tile : part.name == "Ground floor" ? BuildingParts.Whitewash : BuildingParts.Timber);
                    ModelParts.Paint(part.gameObject, color, 0.2f);
                }
            }
        }

        /// <summary>A short swell of a square's owner frame, when it is bought or changes hands.</summary>
        public void PulseOwner(int squareIndex)
        {
            GameObject frame = _ownerFrames[squareIndex];
            if (frame != null && frame.activeInHierarchy) StartCoroutine(Pulse(frame.transform));
        }

        private static IEnumerator Drop(Transform piece, Vector3 rest)
        {
            const float seconds = 0.45f;
            const float height = 0.9f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (piece == null) yield break;
                piece.localPosition = rest + Vector3.up * (height * (1f - Ease.OutBounce(t)));
                yield return null;
            }

            if (piece != null) piece.localPosition = rest;
        }

        private static IEnumerator Pulse(Transform frame)
        {
            const float seconds = 0.4f;
            Vector3 size = Vector3.one;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                float swell = 1f + 0.12f * Ease.Bump(t);
                frame.localScale = new Vector3(size.x * swell, size.y * (1f + 0.6f * Ease.Bump(t)), size.z * swell);
                yield return null;
            }

            frame.localScale = size;
        }

        private void RefreshOwnerMarker(int squareIndex)
        {
            GameObject marker = _ownerMarkers[squareIndex];
            Color? color = _ownerColors[squareIndex];
            GameObject frame = _ownerFrames[squareIndex];
            marker.SetActive(color.HasValue && _levels[squareIndex] == 0);
            frame.SetActive(color.HasValue);
            if (_buildings[squareIndex] != null) PaintBuildings(squareIndex);
            if (!color.HasValue) return;
            Paint(_pennants[squareIndex], color.Value);
            Paint(frame, color.Value);
        }

        private void Build(BoardData data)
        {
            BoardDefinition board = data.Board;
            Layout = new BoardLayout(board.Count, HalfSize);
            float tile = Layout.Spacing * 0.92f;

            float width = 2f * HalfSize + 2f * tile;
            HalfExtent = width / 2f;
            GameObject table = ModelParts.Create("Table", transform, MeshFactory.RoundedBox(new Vector3(width, 0.3f, width), 0.15f), TableColor, 0.35f);
            table.transform.localPosition = new Vector3(0f, -0.15f, 0f);

            float inner = 2f * HalfSize - tile * 1.05f;
            // The middle of the board is the sea, with the town on an island and the title across the far side.
            GameObject sea = ModelParts.Create("Sea", transform, MeshFactory.RoundedBox(new Vector3(inner, 0.04f, inner), 0.02f), SeaColor, 0.75f);
            sea.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            CreateTown(inner);
            CreateTitle(new Vector3(0f, 0.045f, inner * 0.28f), inner);
            if (board.HasPot)
            {
                _pot = CreateText("Pot", "", new Vector3(0f, 0.045f, inner * 0.06f), 96, inner * 0.0035f, TitleColor);
                SetPot(0);
            }

            _tile = tile;
            _ownerMarkers = new GameObject[board.Count];
            _pennants = new GameObject[board.Count];
            _flagScale = new Vector3[board.Count];
            _levels = new int[board.Count];
            _ownerColors = new Color?[board.Count];
            _buildings = new GameObject[board.Count][];
            _buildingRest = new Vector3[board.Count][];
            _ownerFrames = new GameObject[board.Count];
            _tiles = new GameObject[board.Count];
            _tileColors = new Color[board.Count];
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition square = board[i];
                Vector3 center = Layout.SquareCenter(i);

                bool property = square.Type == SquareType.Property;
                GameObject tileObject = ModelParts.Create($"Square {i:00} {square.Name}", transform,
                    MeshFactory.RoundedBox(new Vector3(tile, TileHeight, tile), 0.05f, 4), property ? PropertyBodyColor : TileColor(square, data));
                tileObject.transform.localPosition = center + Vector3.up * (TileHeight / 2f);
                _tiles[i] = tileObject;
                _tileColors[i] = property ? PropertyBodyColor : TileColor(square, data);
                _squareOfCollider[tileObject.AddComponent<BoxCollider>()] = i;

                if (property)
                {
                    // The group colour runs as a raised band along the tile's outer edge.
                    Vector3 outward = Outward(center);
                    GameObject band = ModelParts.Create("Group band", transform,
                        MeshFactory.RoundedBox(new Vector3(tile * 0.96f, 0.07f, tile * BandDepth), 0.03f, 3), TileColor(square, data));
                    band.transform.localPosition = center + outward * (tile * BandOffset) + Vector3.up * (TileHeight + 0.025f);
                    band.transform.localRotation = Quaternion.LookRotation(outward);
                }
                else
                {
                    CreateIcon(square.Type, center, tile);
                }

                string caption = square.Type == SquareType.Property ? F($"{L(square.Name)}\n{square.Price}")
                    : square.Type == SquareType.Tax ? F($"{L(square.Name)}\n-{square.TaxAmount}")
                    : square.Type == SquareType.Travel ? F($"{L(square.Name)}\nbilet {square.Fee}")
                    : square.Name;
                // Name and price sit on a dark plaque towards the outer edge; pawns stand on the inner half (PawnSpot).
                Vector3 plaqueCenter = center + Outward(center) * (tile * PlaqueOffset);
                GameObject plaque = ModelParts.Create("Plaque", transform,
                    MeshFactory.RoundedBox(new Vector3(tile * 0.86f, 0.03f, tile * PlaqueDepth), 0.012f, 2), PlaqueColor, 0.15f);
                plaque.transform.localPosition = plaqueCenter + Vector3.up * (TileHeight + 0.012f);
                plaque.transform.localRotation = Quaternion.LookRotation(Outward(center));
                CreateLabel(caption, plaqueCenter + Vector3.up * (TileHeight + 0.03f), tile);

                if (square.Type == SquareType.Property)
                {
                    // Owner flag: planted where the buildings will stand (the tile's inner half), pennant flying along the edge.
                    Vector3 outward = Outward(center);
                    GameObject flag = BuildingParts.Flag("Owner flag", transform);
                    flag.transform.localPosition = BuildingRowCenter(center);
                    flag.transform.localRotation = Quaternion.LookRotation(-outward); // Its +X, the pennant, flies along the row.
                    _flagScale[i] = new Vector3(tile * 0.5f, tile * 0.62f, tile * 0.5f);
                    flag.transform.localScale = _flagScale[i];
                    flag.SetActive(false);
                    _ownerMarkers[i] = flag;
                    _pennants[i] = flag.transform.Find(BuildingParts.PennantName).gameObject;
                    _pennants[i].AddComponent<FlagWave>().Rest(_pennants[i].transform.localRotation, i * 0.9f);

                    // Owner frame: a rim in the owner's colour just around the tile, seen from any angle.
                    GameObject frame = ModelParts.Create("Owner frame", transform,
                        MeshFactory.RoundedBox(new Vector3(tile * 1.07f, TileHeight * 0.9f, tile * 1.07f), 0.06f, 4), Color.white, 0.4f);
                    frame.transform.localPosition = center + Vector3.up * (TileHeight * 0.45f);
                    frame.SetActive(false);
                    _ownerFrames[i] = frame;
                    if (square.IsDevelopable)
                    {
                        _buildings[i] = CreateBuildings(center);
                        _buildingRest[i] = System.Array.ConvertAll(_buildings[i], piece => piece.transform.localPosition);
                    }
                }
            }
        }

        /// <summary>A small island with the town's houses, a lighthouse and trees, ripples around it, and a boat out at sea.</summary>
        private void CreateTown(float inner)
        {
            var town = new GameObject("Town");
            town.transform.SetParent(transform, false);
            Transform root = town.transform;
            var center = new Vector3(-inner * 0.26f, 0.04f, inner * 0.05f);
            float radius = inner * 0.16f;

            for (int i = 0; i < 3; i++)
            {
                GameObject ripple = ModelParts.Create("Ripple", root, MeshFactory.Ring(0.97f, 0.01f), WaveColor, 0.6f);
                ripple.transform.localPosition = center + Vector3.up * 0.001f;
                ripple.transform.localScale = Vector3.one * (radius * (1.25f + i * 0.22f));
            }

            GameObject sand = ModelParts.Create("Island", root, MeshFactory.Disc(0.06f, 40), SandColor, 0.1f);
            sand.transform.localPosition = center;
            sand.transform.localScale = new Vector3(radius, 1f, radius * 0.85f);
            GameObject grass = ModelParts.Create("Grass", root, MeshFactory.Disc(0.05f, 40), GrassColor, 0.05f);
            grass.transform.localPosition = center + Vector3.up * 0.05f;
            grass.transform.localScale = new Vector3(radius * 0.72f, 1f, radius * 0.6f);

            Vector3 ground = center + Vector3.up * 0.1f;
            float house = radius * 0.22f;
            Vector2[] houseSpots = { new Vector2(-0.35f, -0.1f), new Vector2(-0.05f, 0.25f), new Vector2(0.25f, -0.2f), new Vector2(0.05f, -0.42f), new Vector2(-0.42f, 0.3f) };
            for (int i = 0; i < houseSpots.Length; i++)
            {
                GameObject piece = BuildingParts.House($"Town house {i + 1}", root);
                piece.transform.localPosition = ground + new Vector3(houseSpots[i].x * radius, 0f, houseSpots[i].y * radius);
                piece.transform.localRotation = Quaternion.Euler(0f, 20f + i * 37f, 0f);
                piece.transform.localScale = Vector3.one * house;
            }

            GameObject mansion = BuildingParts.Mansion("Town mansion", root);
            mansion.transform.localPosition = ground + new Vector3(0.3f * radius, 0f, 0.3f * radius);
            mansion.transform.localRotation = Quaternion.Euler(0f, -30f, 0f);
            mansion.transform.localScale = Vector3.one * (house * 1.3f);
            ChimneySmoke.Attach(mansion, BuildingParts.ChimneyTop);

            GameObject lighthouse = ModelParts.Create("Lighthouse", root, PawnShapes.Lighthouse(), Color.white, 0.3f);
            lighthouse.transform.localPosition = ground + new Vector3(0.62f * radius, 0f, -0.5f * radius);
            lighthouse.transform.localScale = Vector3.one * (radius * 0.9f);

            Vector2[] treeSpots = { new Vector2(-0.6f, 0.0f), new Vector2(-0.2f, 0.55f), new Vector2(0.45f, 0.6f), new Vector2(-0.25f, -0.5f) };
            foreach (Vector2 spot in treeSpots)
            {
                Vector3 at = ground + new Vector3(spot.x * radius, 0f, spot.y * radius);
                GameObject trunk = ModelParts.Create("Trunk", root, MeshFactory.Cylinder(8), BuildingParts.DarkTimber, 0.1f);
                trunk.transform.localPosition = at;
                trunk.transform.localScale = new Vector3(radius * 0.025f, radius * 0.12f, radius * 0.025f);
                GameObject crown = ModelParts.Create("Crown", root, MeshFactory.Cone(10), TreeColor, 0.1f);
                crown.transform.localPosition = at + Vector3.up * (radius * 0.1f);
                crown.transform.localScale = new Vector3(radius * 0.11f, radius * 0.3f, radius * 0.11f);
            }

            GameObject boat = ModelParts.Create("Boat", root, PawnShapes.Boat(), BoatColor, 0.3f);
            boat.transform.localPosition = new Vector3(inner * 0.27f, 0.04f, -inner * 0.08f);
            boat.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            boat.transform.localScale = Vector3.one * (inner * 0.12f);
        }

        /// <summary>Direction from a square on the edge towards the outside of the board.</summary>
        private static Vector3 Outward(Vector3 center) =>
            Mathf.Abs(center.z) >= Mathf.Abs(center.x)
                ? new Vector3(0f, 0f, Mathf.Sign(center.z))
                : new Vector3(Mathf.Sign(center.x), 0f, 0f);

        /// <summary>Middle of the building row on a square: its inner half, on the tile's top.</summary>
        private Vector3 BuildingRowCenter(Vector3 center) => center - Outward(center) * (_tile * 0.36f) + Vector3.up * TileHeight;

        /// <summary>Distance between neighbouring houses in a row, in tile widths; four fit with clear gaps.</summary>
        public const float HouseStep = 0.215f;

        /// <summary>A house's width (and depth and height), in tile widths.</summary>
        public const float HouseSize = 0.13f;

        // Four houses in a row along the tile's inner edge plus one hotel, all hidden until built.
        private GameObject[] CreateBuildings(Vector3 center)
        {
            const int houses = SquareDefinition.MaxBuildingLevel - 1;
            var pieces = new GameObject[houses + 1];
            Vector3 inward = -Outward(center);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 rowCenter = BuildingRowCenter(center);

            for (int i = 0; i < houses; i++)
            {
                float offset = (i - (houses - 1) / 2f) * _tile * HouseStep;
                pieces[i] = CreatePiece(BuildingParts.House("House", transform), rowCenter + side * offset, Vector3.one * (_tile * HouseSize), inward);
            }

            GameObject mansion = BuildingParts.Mansion("Mansion", transform);
            ChimneySmoke.Attach(mansion, BuildingParts.ChimneyTop);
            pieces[houses] = CreatePiece(mansion, rowCenter, new Vector3(_tile * 0.46f, _tile * 0.30f, _tile * 0.26f), inward);
            return pieces;
        }

        // Houses and the mansion have their base at y = 0 and their long side along the tile's edge.
        private GameObject CreatePiece(GameObject piece, Vector3 baseCenter, Vector3 size, Vector3 inward)
        {
            piece.transform.localPosition = baseCenter;
            piece.transform.localRotation = Quaternion.LookRotation(inward);
            piece.transform.localScale = size;
            piece.SetActive(false);
            return piece;
        }

        private void CreateTitle(Vector3 localPosition, float width)
        {
            var title = new GameObject("Title");
            title.transform.SetParent(transform, false);
            title.transform.localPosition = localPosition;
            title.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = title.AddComponent<TextMesh>();
            mesh.text = "LAZPOLY";
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 192;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = width * 0.006f;
            mesh.color = TitleColor;
            mesh.font = UiTheme.BoldFont;
            title.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        private TextMesh CreateText(string name, string text, Vector3 localPosition, int fontSize, float characterSize, Color color)
        {
            var label = new GameObject(name);
            label.transform.SetParent(transform, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = fontSize;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = characterSize;
            mesh.color = color;
            mesh.font = UiTheme.BoldFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }

        /// <summary>The sign of a special square, standing on the tile's outer edge where properties have their band.</summary>
        private void CreateIcon(SquareType type, Vector3 center, float tile)
        {
            Mesh mesh = SquareIcons.For(type);
            if (mesh == null) return;
            Vector3 outward = Outward(center);
            GameObject icon = ModelParts.Create($"Sign {type}", transform, mesh, SquareIcons.ColorFor(type), 0.3f);
            icon.transform.localPosition = center + outward * (tile * BandOffset) + Vector3.up * TileHeight;
            // The sign's front (+Z) faces the player looking at the square from outside the board.
            icon.transform.localRotation = Quaternion.LookRotation(outward);
            icon.transform.localScale = Vector3.one * (tile * 0.22f);
        }

        private void CreateLabel(string text, Vector3 localPosition, float tile)
        {
            var label = new GameObject("Label");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            // A large font size drawn small keeps the letters sharp when the camera is far away.
            mesh.fontSize = 128;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = tile * 0.0115f;
            mesh.color = LabelColor;
            mesh.font = UiTheme.BoldFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        private static Color TileColor(SquareDefinition square, BoardData data)
        {
            switch (square.Type)
            {
                case SquareType.Start: return StartColor;
                case SquareType.Tax: return TaxColor;
                case SquareType.Property: return data.GroupColor(square.GroupId);
                case SquareType.Card: return CardColor;
                case SquareType.Pot: return PotColor;
                case SquareType.Travel: return TravelColor;
                case SquareType.Lookout: return LookoutColor;
                default: return RestColor;
            }
        }

        private static void Paint(GameObject target, Color color) => ModelParts.Paint(target, color, 0.5f);
    }
}
