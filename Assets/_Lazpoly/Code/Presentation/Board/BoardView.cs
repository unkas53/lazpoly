using System.Collections;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.UI;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Builds the board from data with meshes made in code (<see cref="MeshFactory"/>): a plate floating
    /// in the sky with the squares around its edge and a lawn in the middle that carries the dice score,
    /// the pot and the title. When the Blender models arrive they replace these shapes; the layout and the API stay the same.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        /// <summary>Distance from the board's centre to the line the square centres lie on.</summary>
        private const float HalfSize = 5.7f;

        /// <summary>How much deeper than wide an edge square is; the corners are squares of that depth.</summary>
        public const float DepthRatio = 1.4f;

        /// <summary>The gap between neighbouring tiles.</summary>
        public const float Gap = 0.12f;
        private const float TileHeight = 0.2f;

        /// <summary>How far the plate hangs below the squares; its edge shows in the isometric view.</summary>
        public const float PlateThickness = 0.6f;

        /// <summary>The plate's rim outside the squares.</summary>
        public const float PlateMargin = 0.12f;

        /// <summary>Top of the lawn in the middle of the board, where the dice land.</summary>
        public const float LawnHeight = 0.08f;

        /// <summary>Where the dice score stands on the lawn, towards the top of the screen from the middle, in lawn widths.</summary>
        public const float ScoreOffset = 0.17f;

        /// <summary>Where the title lies, further up the screen, in lawn widths.</summary>
        public const float TitleOffset = 0.40f;

        /// <summary>The logo's side in lawn widths: its lettering sits above the dice score and its corners stay on the lawn.</summary>
        public const float TitleSize = 0.30f;

        /// <summary>Where the tea money's jar stands, as fractions of the lawn's inner width from the centre: a little below the pot's line and to its right, clear of the dice.</summary>
        public const float PotPileOffset = -0.04f;
        public const float PotPileSide = 0.12f;

        private static readonly Color StartColor = new Color(0.40f, 0.73f, 0.42f);
        private static readonly Color TaxColor = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color RestColor = new Color(0.93f, 0.91f, 0.85f);
        private static readonly Color CardColor = new Color(0.98f, 0.80f, 0.42f);
        private static readonly Color PotColor = new Color(0.55f, 0.82f, 0.75f);
        private static readonly Color TravelColor = new Color(0.62f, 0.74f, 0.95f);
        private static readonly Color LookoutColor = new Color(0.80f, 0.70f, 0.95f);
        private static readonly Color PlateColor = new Color(0.70f, 0.63f, 0.52f);
        private static readonly Color RoadColor = new Color(0.72f, 0.68f, 0.60f);
        private static readonly Color GrassColor = new Color(0.47f, 0.70f, 0.36f);
        private static readonly Color LightGrassColor = new Color(0.55f, 0.76f, 0.42f);
        private static readonly Color PathColor = new Color(0.86f, 0.80f, 0.66f);
        /// <summary>The paper every tile is printed on; a group's colour is mixed into it by <see cref="Pastel"/>.</summary>
        public static readonly Color PaperColor = new Color(0.95f, 0.92f, 0.85f);

        /// <summary>How far a tile's own colour is washed towards the paper: the pastel of the printed board.</summary>
        public const float Pastel = 0.6f;
        private static readonly Color TitleColor = new Color(0.16f, 0.32f, 0.20f);
        private static readonly Color ScoreColor = new Color(0.14f, 0.18f, 0.15f);
        private static readonly Color PotTextColor = new Color(0.20f, 0.26f, 0.20f);
        private static readonly Color InkColor = new Color(0.17f, 0.15f, 0.14f);
        private static readonly Color PriceColor = new Color(0.21f, 0.19f, 0.17f);

        // Where things sit across a square, from its inner edge (-0.5) to its outer edge (+0.5), in tile depths:
        // buildings and the tent along the inner edge, pawns inside them, then the name, the price and the group band.
        // The camera looks down at 35 degrees, so a piece's height falls up the screen over the ground behind it:
        // on the rows that face away from the camera the pawns' room is kept short of the name so they never cover it.
        /// <summary>The building row's centre, inward from the square's centre.</summary>
        public const float BuildingOffset = 0.40f;

        /// <summary>Where pawns stand, inward from the square's centre.</summary>
        public const float PawnOffset = 0.22f;

        /// <summary>On a corner, pawns also step this far along the edge away from the corner's scene.</summary>
        public const float CornerPawnShift = 0.25f;

        /// <summary>The name's line, outward from the square's centre.</summary>
        public const float NameOffset = 0.19f;

        /// <summary>The price line, outward from the square's centre.</summary>
        public const float PriceOffset = 0.335f;

        /// <summary>The group band's centre, outward from the square's centre, and its depth.</summary>
        public const float BandOffset = 0.445f;
        public const float BandDepth = 0.09f;

        /// <summary>How much of the tile's width the name may use, and how tall its line and the price's line may be, in tile depths.</summary>
        public const float NameFill = 0.92f;
        public const float NameDepth = 0.16f;
        public const float PriceDepth = 0.12f;

        /// <summary>Character sizes of the name and the price before they shrink to fit, in tile widths.</summary>
        public const float NameSize = 0.022f;
        public const float PriceSize = 0.014f;

        /// <summary>
        /// A corner's scene: how far along the edge and how far across the corner it stands, and its size, in corner sides.
        /// On a corner that faces away from the camera the scene and the pawns take the outer part and the name the inner
        /// edge, so their height rises over the sky instead of the name; on the near corners it is the other way round.
        /// </summary>
        public const float SceneOffset = 0.29f;
        public const float SceneInset = 0.22f;
        public const float SceneSize = 0.40f;

        private OwnerMarker[] _ownerMarkers;
        private Vector3[] _flagScale;
        private int[] _levels;
        private Color?[] _ownerColors;
        private GameObject[][] _buildings;
        private Vector3[][] _buildingRest;
        private GameObject[] _ownerFrames;
        private GameObject[] _tiles;
        private Color[] _tileColors;
        private TextMesh[] _names;
        private TextMesh[] _prices;
        private readonly List<KeyValuePair<Transform, Vector3>> _marks = new List<KeyValuePair<Transform, Vector3>>();
        private BoardData _data;
        private readonly Dictionary<Collider, int> _squareOfCollider = new Dictionary<Collider, int>();
        private readonly HashSet<Transform> _animating = new HashSet<Transform>();
        private HashSet<int> _highlighted;
        private float _width;
        private float _depth;
        private float _inner;
        private float _yaw = BoardCameraRig.BaseYaw;
        private Transform _scoreboard;
        private Transform _title;
        private TextMesh _score;
        private TextMesh _pot;
        private PotPile _potPile;

        public BoardLayout Layout { get; private set; }

        /// <summary>Top surface height of the tiles, where pawns stand.</summary>
        public float SurfaceY => TileHeight;

        /// <summary>Half the width of the plate the board sits on.</summary>
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

        /// <summary>Where pawns stand on a square: its inner part, so the name and price on the outer part stay readable; on a corner, beside the scene.</summary>
        public Vector3 PawnSpot(int index)
        {
            Vector3 center = Layout.SquareCenter(index);
            Vector3 outward = Outward(center);
            Vector3 spot = center - outward * (_depth * PawnOffset);
            if (Layout.IsCorner(index))
            {
                bool far = FacesAway(outward);
                spot = center + outward * (_depth * PawnOffset * (far ? 1f : -1f)) - SceneSide(index, far) * (_depth * CornerPawnShift);
            }

            return transform.TransformPoint(spot) + Vector3.up * SurfaceY;
        }

        /// <summary>Whether a square's outer edge faces away from the game's camera: its pieces' height falls over its outer part.</summary>
        public static bool FacesAway(Vector3 outward) => Vector3.Dot(outward, ScreenUp(BoardCameraRig.BaseYaw)) > 0f;

        /// <summary>Which way along the edge a corner's scene stands: towards the board's corner when the corner faces away, else away from it.</summary>
        private Vector3 SceneSide(int index, bool far) => Layout.Cornerward(index) * (far ? 1f : -1f);

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
                // Only the group lights up; the rest of the board keeps its colour (fading it reads as a mistake).
                if (_highlighted != null && _highlighted.Contains(i)) color = Color.Lerp(color, Color.white, 0.25f);
                PaintTile(_tiles[i], color);
            }
        }

        /// <summary>Shows what the pot holds under the dice score (boards without a pot show nothing).</summary>
        public void SetPot(int amount)
        {
            if (_pot != null) _pot.text = F($"Çay kumbarası {amount}");
            _potPile?.Show(amount);
        }

        /// <summary>Shows the last roll large on the lawn; two dashes before the first roll of the game.</summary>
        public void ShowRoll(int die1, int die2)
        {
            if (_score != null) _score.text = ScoreText(die1, die2);
        }

        /// <summary>What the lawn's score says for a roll: "6  4", or "–  –" when nothing has been rolled.</summary>
        public static string ScoreText(int die1, int die2) => die1 == 0 ? "–  –" : $"{die1}  {die2}";

        /// <summary>
        /// Turns everything written on the board so it reads from a camera at <paramref name="yaw"/>: the
        /// plaques and group letters flip to run left to right, and the score and the title lie square to the screen.
        /// </summary>
        public void FaceCamera(float yaw)
        {
            _yaw = yaw;
            for (int i = 0; _names != null && i < _names.Length; i++)
            {
                Quaternion rotation = LabelRotation(Outward(Layout.SquareCenter(i)), yaw);
                if (_names[i] != null) _names[i].transform.localRotation = rotation;
                if (_prices[i] != null) _prices[i].transform.localRotation = rotation;
            }

            foreach (KeyValuePair<Transform, Vector3> mark in _marks) mark.Key.localRotation = LabelRotation(mark.Value, yaw);
            Vector3 up = ScreenUp(yaw);
            if (_scoreboard != null)
            {
                _scoreboard.localPosition = new Vector3(0f, LawnHeight + 0.01f, 0f) + up * (_inner * ScoreOffset);
                _scoreboard.localRotation = FlatFacing(yaw);
            }

            if (_title != null)
            {
                _title.localPosition = new Vector3(0f, LawnHeight + 0.01f, 0f) + up * (_inner * TitleOffset);
                _title.localRotation = FlatFacing(yaw);
            }

            if (_potPile != null) _potPile.Root.localPosition = new Vector3(0f, LawnHeight, 0f) + up * (_inner * PotPileOffset) + ScreenRight(yaw) * (_inner * PotPileSide);
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
            OwnerMarker marker = _ownerMarkers[squareIndex];
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
            OwnerMarker flag = _ownerMarkers[squareIndex];
            if (flag == null) return;
            if (animate && flag.gameObject.activeInHierarchy && _animating.Add(flag.transform))
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
            OwnerMarker flag = _ownerMarkers[squareIndex];
            if (flag != null && flag.gameObject.activeInHierarchy) StartCoroutine(Raise(flag.transform, _flagScale[squareIndex]));
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
        /// <summary>The building level the board shows on a square now.</summary>
        public int LevelOf(int squareIndex) => _levels[squareIndex];

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

            // The tent (or flag) gives way to the first building and returns when the last one is sold.
            OwnerMarker flag = _ownerMarkers[squareIndex];
            if (flag == null) return;
            bool flagWasUp = flag.gameObject.activeSelf;
            RefreshOwnerMarker(squareIndex);
            if (animate && flagWasUp && !flag.gameObject.activeSelf && _animating.Add(flag.transform))
            {
                flag.gameObject.SetActive(true);
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
            OwnerMarker marker = _ownerMarkers[squareIndex];
            Color? color = _ownerColors[squareIndex];
            GameObject frame = _ownerFrames[squareIndex];
            marker.gameObject.SetActive(color.HasValue && _levels[squareIndex] == 0);
            frame.SetActive(color.HasValue);
            if (_buildings[squareIndex] != null) PaintBuildings(squareIndex);
            if (!color.HasValue) return;
            marker.Paint(color.Value);
            Paint(frame, color.Value);
        }

        private void Build(BoardData data)
        {
            BoardDefinition board = data.Board;
            Layout = new BoardLayout(board.Count, HalfSize, DepthRatio);
            _width = Layout.Width - Gap;
            _depth = Layout.Depth - Gap;

            float width = 2f * Layout.OuterHalf + 2f * PlateMargin;
            HalfExtent = width / 2f;
            // The plate floats in the sky; its edge hangs below the squares.
            GameObject plate = ModelParts.Create("Plate", transform, MeshFactory.RoundedBox(new Vector3(width, PlateThickness, width), 0.2f), PlateColor, 0.25f);
            plate.transform.localPosition = new Vector3(0f, -PlateThickness / 2f, 0f);

            _inner = 2f * HalfSize - Layout.Depth * 1.05f;
            // The middle of the board is a lawn that carries the dice score, the pot and the title.
            CreateLawn(_inner);
            CreateTitle(_inner);
            CreateScoreboard(_inner, board.HasPot);
            FaceCamera(_yaw);
            _ownerMarkers = new OwnerMarker[board.Count];
            _flagScale = new Vector3[board.Count];
            _levels = new int[board.Count];
            _ownerColors = new Color?[board.Count];
            _buildings = new GameObject[board.Count][];
            _buildingRest = new Vector3[board.Count][];
            _ownerFrames = new GameObject[board.Count];
            _tiles = new GameObject[board.Count];
            _tileColors = new Color[board.Count];
            _names = new TextMesh[board.Count];
            _prices = new TextMesh[board.Count];
            _data = data;
            Texture2D paper = TileSkin.Paper();
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition square = board[i];
                Vector3 center = Layout.SquareCenter(i);
                Vector3 outward = Outward(center);
                Quaternion facing = Quaternion.LookRotation(outward);
                // A tile's size: along the edge, and from its inner edge to its outer edge (its local Z, turned outward).
                Vector2 size = Layout.SquareSize(i) - Vector2.one * Gap;

                // The printed tile: its colour washed to a pastel over the paper grain, the group's full colour kept for the band.
                Color body = Color.Lerp(TileColor(square, data), PaperColor, Pastel);
                GameObject tileObject = ModelParts.Create($"Square {i:00} {square.Name}", transform,
                    MeshFactory.RoundedBox(new Vector3(size.x, TileHeight, size.y), 0.05f, 4), body, 0.05f);
                ModelParts.Skin(tileObject, paper);
                tileObject.transform.localPosition = center + Vector3.up * (TileHeight / 2f);
                tileObject.transform.localRotation = facing;
                _tiles[i] = tileObject;
                _tileColors[i] = body;
                _squareOfCollider[tileObject.AddComponent<BoxCollider>()] = i;

                bool property = square.Type == SquareType.Property;
                if (property)
                {
                    // The group colour runs as a raised band along the tile's outer edge.
                    GameObject band = ModelParts.Create("Group band", transform,
                        MeshFactory.RoundedBox(new Vector3(size.x * 0.96f, 0.07f, _depth * BandDepth), 0.03f, 3), TileColor(square, data));
                    band.transform.localPosition = center + outward * (_depth * BandOffset) + Vector3.up * (TileHeight + 0.025f);
                    band.transform.localRotation = facing;
                    // With colour-blind friendly colours on, the group's letter sits on the band as well.
                    string mark = data.GroupMark(square.GroupId);
                    if (mark != null) CreateBandMark(mark, band.transform.localPosition + Vector3.up * 0.04f, outward, TileColor(square, data));
                }
                else if (Layout.IsCorner(i) && CornerParts.Has(square.Type))
                {
                    CreateScene(square.Type, i, center, outward, size.y);
                }
                else
                {
                    CreateIcon(square.Type, center, outward);
                }

                // The name and, under it, the price lie on the tile's outer part; pawns stand on the inner part (PawnSpot).
                // A corner facing away from the camera keeps its name on the inner edge, clear of its scene and pawns.
                float top = TileHeight + 0.012f;
                float textSide = Layout.IsCorner(i) && FacesAway(outward) ? -1f : 1f;
                _names[i] = CreateLabel("Name", L(square.Name), center + outward * (textSide * _depth * NameOffset) + Vector3.up * top, outward,
                    _width * NameSize, new Vector2(size.x * NameFill, _depth * NameDepth), InkColor);
                string subtitle = Subtitle(square);
                if (subtitle != null)
                {
                    _prices[i] = CreateLabel("Price", subtitle, center + outward * (textSide * _depth * PriceOffset) + Vector3.up * top, outward,
                        _width * PriceSize, new Vector2(size.x * NameFill, _depth * PriceDepth), PriceColor);
                }

                if (property)
                {
                    // Owner marker: the owner's tent (or the flag) pitched on the tile's inner part, where the buildings will stand.
                    OwnerMarker marker = OwnerMarker.Create("Owner marker", transform, i * 0.9f);
                    marker.Place(center, outward, size, TileHeight, BuildingRowCenter(center));
                    _flagScale[i] = marker.RestScale(_width);
                    marker.transform.localScale = _flagScale[i];
                    marker.gameObject.SetActive(false);
                    _ownerMarkers[i] = marker;

                    // Owner frame: a rim in the owner's colour just around the tile, seen from any angle.
                    GameObject frame = ModelParts.Create("Owner frame", transform,
                        MeshFactory.RoundedBox(new Vector3(size.x + Gap * 0.9f, TileHeight * 0.9f, size.y + Gap * 0.9f), 0.06f, 4), Color.white, 0.4f);
                    frame.transform.localPosition = center + Vector3.up * (TileHeight * 0.45f);
                    frame.transform.localRotation = facing;
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

        /// <summary>A corner's scene stands on one side of the corner with the pawns on the other, both on the part away from the name.</summary>
        private void CreateScene(SquareType type, int index, Vector3 center, Vector3 outward, float side)
        {
            GameObject scene = CornerParts.For(type, $"Scene {type}", transform);
            if (scene == null) return;
            bool far = FacesAway(outward);
            scene.transform.localPosition = center + SceneSide(index, far) * (side * SceneOffset) + outward * (side * SceneInset * (far ? 1f : -1f)) + Vector3.up * TileHeight;
            scene.transform.localRotation = Quaternion.LookRotation(outward);
            scene.transform.localScale = Vector3.one * (side * SceneSize);
        }

        /// <summary>The lawn in the middle: a gravel ring where the squares end, grass with lighter patches, a path round the park, and <see cref="LawnDressing"/> on it.</summary>
        private void CreateLawn(float inner)
        {
            var lawn = new GameObject("Lawn");
            lawn.transform.SetParent(transform, false);
            Transform root = lawn.transform;

            GameObject road = ModelParts.Create("Gravel", root, MeshFactory.RoundedBox(new Vector3(inner, 0.04f, inner), 0.02f, 2), RoadColor, 0.1f);
            road.transform.localPosition = new Vector3(0f, 0.02f, 0f);

            float grass = inner * 0.9f;
            GameObject green = ModelParts.Create("Grass", root, MeshFactory.RoundedBox(new Vector3(grass, LawnHeight, grass), 0.2f, 3), GrassColor, 0.05f);
            green.transform.localPosition = new Vector3(0f, LawnHeight / 2f, 0f);

            // A path runs round the park, a little way in from the gravel.
            float path = inner * 0.44f;
            for (int side = 0; side < 4; side++)
            {
                GameObject piece = ModelParts.Create("Path", root, MeshFactory.RoundedBox(new Vector3(path + inner * 0.03f, 0.01f, inner * 0.03f), 0.005f, 1), PathColor, 0.1f);
                piece.transform.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                piece.transform.localPosition = piece.transform.localRotation * new Vector3(0f, LawnHeight + 0.004f, path / 2f);
            }

            Vector2[] patches = { new Vector2(-0.34f, 0.22f), new Vector2(0.3f, 0.3f), new Vector2(0.36f, -0.2f), new Vector2(-0.2f, -0.36f), new Vector2(0.05f, 0.4f) };
            float[] sizes = { 0.09f, 0.06f, 0.08f, 0.07f, 0.05f };
            for (int i = 0; i < patches.Length; i++)
            {
                GameObject patch = ModelParts.Create("Light grass", root, MeshFactory.Disc(0.004f, 28), LightGrassColor, 0.05f);
                patch.transform.localPosition = new Vector3(patches[i].x * inner, LawnHeight + 0.002f, patches[i].y * inner);
                patch.transform.localScale = new Vector3(inner * sizes[i], 1f, inner * sizes[i] * 0.8f);
            }

            LawnDressing.Dress(root, inner, LawnHeight);
        }

        /// <summary>The dice score large on the lawn with the pot's line under it; <see cref="FaceCamera"/> places and turns it.</summary>
        private void CreateScoreboard(float inner, bool hasPot)
        {
            var board = new GameObject("Scoreboard");
            board.transform.SetParent(transform, false);
            _scoreboard = board.transform;
            _score = CreateText("Score", ScoreText(0, 0), _scoreboard, Vector3.zero, 192, inner * 0.008f, ScoreColor);
            if (!hasPot) return;
            _pot = CreateText("Pot", "", _scoreboard, new Vector3(0f, -inner * 0.1f, 0f), 128, inner * 0.0036f, PotTextColor);
            _potPile = new PotPile(transform);
            SetPot(0);
        }

        /// <summary>Direction from a square on the edge towards the outside of the board.</summary>
        private static Vector3 Outward(Vector3 center) => BoardLayout.Outward(center);

        /// <summary>Middle of the building row on a square: along its inner edge, on the tile's top.</summary>
        private Vector3 BuildingRowCenter(Vector3 center) => center - Outward(center) * (_depth * BuildingOffset) + Vector3.up * TileHeight;

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
                float offset = (i - (houses - 1) / 2f) * _width * HouseStep;
                pieces[i] = CreatePiece(BuildingParts.House("House", transform), rowCenter + side * offset, Vector3.one * (_width * HouseSize), inward);
            }

            GameObject mansion = BuildingParts.Mansion("Mansion", transform);
            ChimneySmoke.Attach(mansion, BuildingParts.ChimneyTop);
            pieces[houses] = CreatePiece(mansion, rowCenter, new Vector3(_width * 0.46f, _width * 0.30f, _width * 0.26f), inward);
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

        /// <summary>
        /// The logo lying on the far part of the lawn (<see cref="FaceCamera"/> places and turns it); the name
        /// in letters when the picture or its shader is not in the build.
        /// </summary>
        private void CreateTitle(float inner)
        {
            var title = new GameObject("Title");
            title.transform.SetParent(transform, false);
            _title = title.transform;
            if (MenuArt.HasLogo && PicturePlate.IsAvailable)
            {
                PicturePlate.Create("Logo", _title, MenuArt.Logo.texture, Vector2.one * (inner * TitleSize));
                return;
            }

            CreateText("Lettering", "LAZPOLY", _title, Vector3.zero, 192, inner * 0.005f, TitleColor);
        }

        /// <summary>Text lying in its parent's plane, readable from the parent's -Z side (<see cref="FlatFacing"/> turns the parent to the camera).</summary>
        private static TextMesh CreateText(string name, string text, Transform parent, Vector3 localPosition, int fontSize, float characterSize, Color color)
        {
            var label = new GameObject(name);
            label.transform.SetParent(parent, false);
            label.transform.localPosition = localPosition;

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = fontSize;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = characterSize;
            mesh.color = color;
            mesh.font = UiTheme.BoldFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = PrintMaterial.For(mesh.font);
            return mesh;
        }

        /// <summary>The sign of a special square, standing on the tile's outer edge where properties have their band.</summary>
        private void CreateIcon(SquareType type, Vector3 center, Vector3 outward)
        {
            Mesh mesh = SquareIcons.For(type);
            if (mesh == null) return;
            GameObject icon = ModelParts.Create($"Sign {type}", transform, mesh, SquareIcons.ColorFor(type), 0.3f);
            icon.transform.localPosition = center + outward * (_depth * BandOffset) + Vector3.up * TileHeight;
            // The sign's front (+Z) faces the player looking at the square from outside the board.
            icon.transform.localRotation = Quaternion.LookRotation(outward);
            icon.transform.localScale = Vector3.one * (_width * 0.22f);
        }

        /// <summary>
        /// How a square's label lies: flat on the tile with its line running along the tile's outer edge
        /// (so it fits its plaque), and with the tops of its letters pointing whichever way makes the
        /// line read left to right for a camera turned by <paramref name="yaw"/>. From the game's own corner,
        /// the near and far rows read with their tops away from the camera and the side columns run along the edge.
        /// </summary>
        public static Quaternion LabelRotation(Vector3 outward, float yaw)
        {
            Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
            // Text lying on the board: its face looks up, the tops of its letters point along or against the outward direction.
            Vector3 tops = outward.normalized;
            Vector3 line = Vector3.Cross(tops, Vector3.down);
            float along = Vector3.Dot(line, right);
            if (along < -1e-4f || (Mathf.Abs(along) <= 1e-4f && Vector3.Dot(tops, ScreenUp(yaw)) < 0f)) tops = -tops;
            return Quaternion.LookRotation(Vector3.down, tops);
        }

        /// <summary>The way on the board that is straight up the screen for a camera turned by <paramref name="yaw"/>.</summary>
        public static Vector3 ScreenUp(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        /// <summary>The way on the board that is straight right across the screen for a camera turned by <paramref name="yaw"/>.</summary>
        public static Vector3 ScreenRight(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.right;

        /// <summary>Lying flat on the board, square to the screen of a camera turned by <paramref name="yaw"/>: a child text reads upright.</summary>
        public static Quaternion FlatFacing(float yaw) => Quaternion.LookRotation(Vector3.down, ScreenUp(yaw));

        /// <summary>The character size that keeps a label of <paramref name="width"/> within <paramref name="maxWidth"/>.</summary>
        public static float FitCharacterSize(float characterSize, float width, float maxWidth) =>
            width > maxWidth && width > 0f ? characterSize * maxWidth / width : characterSize;

        /// <summary>The character size that keeps a label inside its plaque both along the line and across it (two lines of name and price).</summary>
        public static float FitCharacterSize(float characterSize, Vector2 size, Vector2 maxSize) =>
            Mathf.Min(FitCharacterSize(characterSize, size.x, maxSize.x), FitCharacterSize(characterSize, size.y, maxSize.y));

        /// <summary>The line under a square's name: its price, the tax it takes or the fare; null when the square has none.</summary>
        public static string Subtitle(SquareDefinition square)
        {
            switch (square.Type)
            {
                case SquareType.Property: return F($"{square.Price}");
                case SquareType.Tax: return F($"-{square.TaxAmount}");
                case SquareType.Travel: return F($"bilet {square.Fee}");
                default: return null;
            }
        }

        /// <summary>Rewrites every name and price in the current language, so a language change needs no new scene.</summary>
        public void Relabel()
        {
            if (_names == null) return;
            for (int i = 0; i < _names.Length; i++)
            {
                SquareDefinition square = _data.Board[i];
                Vector2 size = Layout.SquareSize(i) - Vector2.one * Gap;
                Vector3 outward = Outward(Layout.SquareCenter(i));
                if (_names[i] != null)
                {
                    _names[i].text = L(square.Name);
                    FitLabel(_names[i], _width * NameSize, new Vector2(size.x * NameFill, _depth * NameDepth), outward);
                }

                if (_prices[i] != null)
                {
                    _prices[i].text = Subtitle(square);
                    FitLabel(_prices[i], _width * PriceSize, new Vector2(size.x * NameFill, _depth * PriceDepth), outward);
                }
            }
        }

        private TextMesh CreateLabel(string name, string text, Vector3 localPosition, Vector3 outward, float characterSize, Vector2 maxSize, Color color)
        {
            var label = new GameObject(name);
            label.transform.SetParent(transform, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = LabelRotation(outward, _yaw);

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            // A large font size drawn small keeps the letters sharp when the camera is far away.
            mesh.fontSize = 128;
            mesh.fontStyle = FontStyle.Normal;
            mesh.color = color;
            // The heaviest cut: thin strokes fade when the board is small on screen or the far row is read at a slant.
            mesh.font = UiTheme.BlackFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = PrintMaterial.For(mesh.font);
            FitLabel(mesh, characterSize, maxSize, outward);
            return mesh;
        }

        /// <summary>Long names ("Rio de Janeiro") shrink to the tile's width and tall lines to their room instead of running off.</summary>
        private static void FitLabel(TextMesh mesh, float characterSize, Vector2 maxSize, Vector3 outward)
        {
            mesh.characterSize = characterSize;
            Vector3 size = mesh.GetComponent<MeshRenderer>().bounds.size;
            bool side = Mathf.Abs(outward.x) > Mathf.Abs(outward.z);
            var extent = new Vector2(side ? size.z : size.x, side ? size.x : size.z);
            mesh.characterSize = FitCharacterSize(characterSize, extent, maxSize);
        }

        /// <summary>The group's letter on its band, in a colour that reads on the band.</summary>
        private void CreateBandMark(string mark, Vector3 localPosition, Vector3 outward, Color band)
        {
            var label = new GameObject("Group mark");
            label.transform.SetParent(transform, false);
            label.transform.localPosition = localPosition;
            label.transform.localRotation = LabelRotation(outward, _yaw);
            _marks.Add(new KeyValuePair<Transform, Vector3>(label.transform, outward));

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = mark;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 128;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = _width * 0.012f;
            mesh.color = ColorVision.TextOn(band);
            mesh.font = UiTheme.BlackFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = PrintMaterial.For(mesh.font);
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

        /// <summary>Tiles are matte paper: the highlight must not polish them.</summary>
        private static void PaintTile(GameObject tile, Color color) => ModelParts.Paint(tile, color, 0.05f);
    }
}
