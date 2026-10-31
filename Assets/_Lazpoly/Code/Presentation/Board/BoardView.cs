using System.Collections;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.UI;
using UnityEngine;

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

        private GameObject[] _ownerMarkers;
        private Color?[] _ownerColors;
        private bool[] _mortgaged;
        private GameObject[][] _buildings;
        private Vector3[][] _buildingRest;
        private GameObject[] _ownerFrames;
        private GameObject[] _tiles;
        private Color[] _tileColors;
        private readonly Dictionary<Collider, int> _squareOfCollider = new Dictionary<Collider, int>();
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
            if (_pot != null) _pot.text = $"Çay kumbarası\n{amount}";
        }

        /// <summary>Shows who owns a square; pass null to show it as unowned.</summary>
        public void SetOwner(int squareIndex, Color? ownerColor)
        {
            GameObject marker = _ownerMarkers[squareIndex];
            if (marker == null) return;

            _ownerColors[squareIndex] = ownerColor;
            RefreshOwnerMarker(squareIndex);
        }

        /// <summary>A mortgaged property shows its owner marker dimmed.</summary>
        public void SetMortgaged(int squareIndex, bool mortgaged)
        {
            if (_ownerMarkers[squareIndex] == null) return;
            _mortgaged[squareIndex] = mortgaged;
            RefreshOwnerMarker(squareIndex);
        }

        /// <summary>
        /// Shows 1-4 small houses, or one hotel at the top level. With <paramref name="animate"/>, pieces
        /// that appear drop onto the tile and bounce; pieces that go are removed at once.
        /// </summary>
        public void SetBuildingLevel(int squareIndex, int level, bool animate = false)
        {
            GameObject[] pieces = _buildings[squareIndex];
            if (pieces == null) return;

            bool hotel = level >= SquareDefinition.MaxBuildingLevel;
            for (int i = 0; i < pieces.Length; i++)
            {
                bool shown = i == pieces.Length - 1 ? hotel : !hotel && i < level;
                bool appears = shown && !pieces[i].activeSelf;
                pieces[i].SetActive(shown);
                pieces[i].transform.localPosition = _buildingRest[squareIndex][i];
                if (appears && animate) StartCoroutine(Drop(pieces[i].transform, _buildingRest[squareIndex][i]));
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
            marker.SetActive(color.HasValue);
            frame.SetActive(color.HasValue);
            if (!color.HasValue) return;
            Color shown = _mortgaged[squareIndex] ? Color.Lerp(color.Value, Color.black, 0.65f) : color.Value;
            Paint(marker, shown);
            Paint(frame, shown);
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
            _ownerColors = new Color?[board.Count];
            _mortgaged = new bool[board.Count];
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
                    // The group colour runs as a band along the tile's outer edge.
                    Vector3 outward = Outward(center);
                    GameObject band = ModelParts.Create("Group band", transform,
                        MeshFactory.RoundedBox(new Vector3(tile * 0.96f, 0.04f, tile * 0.24f), 0.015f, 2), TileColor(square, data));
                    band.transform.localPosition = center + outward * (tile * 0.35f) + Vector3.up * (TileHeight + 0.01f);
                    band.transform.localRotation = Quaternion.LookRotation(outward);
                }

                string caption = square.Type == SquareType.Property ? $"{square.Name}\n{square.Price}"
                    : square.Type == SquareType.Tax ? $"{square.Name}\n-{square.TaxAmount}"
                    : square.Type == SquareType.Travel ? $"{square.Name}\nbilet {square.Fee}"
                    : square.Name;
                // Name and price sit towards the outer edge; pawns stand on the inner half (PawnSpot).
                CreateLabel(caption, center + Outward(center) * (tile * 0.07f) + Vector3.up * (TileHeight + 0.01f), tile);

                if (square.Type == SquareType.Property)
                {
                    // Owner token: a coloured disc on the band, beside the price.
                    GameObject marker = ModelParts.Create("Owner", transform, MeshFactory.Lathe("owner", OwnerProfile), Color.white, 0.5f);
                    marker.transform.localPosition = center + Outward(center) * (tile * 0.35f) + Vector3.up * (TileHeight + 0.03f) +
                                                     Vector3.Cross(Vector3.up, Outward(center)) * (tile * 0.33f);
                    marker.transform.localScale = Vector3.one * tile * 0.5f;
                    marker.SetActive(false);
                    _ownerMarkers[i] = marker;

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

        /// <summary>Owner token outline (radius, height): a low rounded disc.</summary>
        private static readonly Vector2[] OwnerProfile =
        {
            new Vector2(0f, 0f), new Vector2(0.18f, 0f), new Vector2(0.2f, 0.03f), new Vector2(0.18f, 0.06f), new Vector2(0f, 0.065f),
        };

        /// <summary>Direction from a square on the edge towards the outside of the board.</summary>
        private static Vector3 Outward(Vector3 center) =>
            Mathf.Abs(center.z) >= Mathf.Abs(center.x)
                ? new Vector3(0f, 0f, Mathf.Sign(center.z))
                : new Vector3(Mathf.Sign(center.x), 0f, 0f);

        // Four houses in a row along the tile's inner edge plus one hotel, all hidden until built.
        private GameObject[] CreateBuildings(Vector3 center)
        {
            const int houses = SquareDefinition.MaxBuildingLevel - 1;
            var pieces = new GameObject[houses + 1];
            Vector3 inward = -Outward(center);
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            Vector3 rowCenter = center + inward * (_tile * 0.36f) + Vector3.up * TileHeight;

            for (int i = 0; i < houses; i++)
            {
                float offset = (i - (houses - 1) / 2f) * _tile * 0.2f;
                pieces[i] = CreatePiece(BuildingParts.House("House", transform), rowCenter + side * offset, new Vector3(_tile * 0.15f, _tile * 0.16f, _tile * 0.15f), inward);
            }

            pieces[houses] = CreatePiece(BuildingParts.Mansion("Mansion", transform), rowCenter, new Vector3(_tile * 0.42f, _tile * 0.26f, _tile * 0.24f), inward);
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
            mesh.color = new Color(0.12f, 0.10f, 0.08f);
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
