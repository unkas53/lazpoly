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
        /// <summary>
        /// An edge square's width along the edge: the width the 24-square board had. A board with more squares grows
        /// instead of narrowing its squares (the owner's wish, Faz 22), and the camera frames whatever size it comes to.
        /// </summary>
        public const float SquareWidth = 5.7f * 2f / (5f + DepthRatio);

        /// <summary>Distance from the board's centre to the line the square centres lie on, for squares <see cref="SquareWidth"/> wide.</summary>
        public static float HalfSizeFor(int squareCount)
        {
            if (squareCount % 4 != 0) return SquareWidth * squareCount / 8f;
            return SquareWidth * (squareCount / 4 - 1 + DepthRatio) / 2f;
        }

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
        private static readonly Color JailColor = new Color(0.74f, 0.72f, 0.70f);
        private static readonly Color LookoutColor = new Color(0.80f, 0.70f, 0.95f);
        private static readonly Color PlateColor = new Color(0.70f, 0.63f, 0.52f);
        private static readonly Color RoadColor = new Color(0.72f, 0.68f, 0.60f);
        private static readonly Color GrassColor = new Color(0.47f, 0.70f, 0.36f);
        /// <summary>The paper every tile is printed on; a group's colour is mixed into it by <see cref="Pastel"/>.</summary>
        public static readonly Color PaperColor = new Color(0.95f, 0.92f, 0.85f);

        /// <summary>How far a tile's own colour is washed towards the paper: the pastel of the printed board.</summary>
        public const float Pastel = 0.6f;
        private static readonly Color TitleColor = new Color(0.16f, 0.32f, 0.20f);
        private static readonly Color ScoreColor = new Color(0.14f, 0.18f, 0.15f);
        private static readonly Color PotTextColor = new Color(0.20f, 0.26f, 0.20f);
        /// <summary>The one dark ink every name on the board is printed in.</summary>
        public static readonly Color InkColor = new Color(0.17f, 0.15f, 0.14f);
        private static readonly Color PriceColor = new Color(0.21f, 0.19f, 0.17f);

        // Where things sit across a square, in tile depths from its centre towards the edge that lies lower on the
        // screen (+0.5; the outer edge on the rows near the camera, the inner edge on the far rows): the price at the
        // bottom with the pawns walking over it, the name on its group-coloured strip above, and the buildings on the
        // top part. The camera looks down at 35 degrees, so a piece's height rises up the screen, away from the writing.
        /// <summary>The building row's centre, towards the screen's top edge of the square (negative: up the screen).</summary>
        public const float BuildingOffset = -0.27f;

        /// <summary>
        /// A holiday resort's tile: sand with a strip of sea along its top edge and white surf where the sea meets the
        /// sand, so it reads as a beach and not a town, all printed flat into the tile inside its frame
        /// (<see cref="TileSkin.Resort"/>). The sea's middle and depth in tile depths (it starts just past the frame, which
        /// clips it), the surf's scallop radius in tile widths and how many scallops run along the edge, and where the
        /// owner's beach stands (a little further down the tile than a town's buildings, clear of the sea).
        /// </summary>
        public const float SeaOffset = -0.40f;
        public const float SeaDepth = 0.08f;
        public const float SurfRadius = 0.05f;
        public const int SurfScallops = 7;
        public const float ResortOffset = -0.19f;

        /// <summary>
        /// A town's tile: from its top edge down to the bottom of the name's line the group's colour with a pattern,
        /// washed only as far as the dark ink needs to read on it (<see cref="BandColor"/>); below it a light paved
        /// strip for the rent and the pawns. Both are printed into the tile's own face (<see cref="TileSkin.Town"/>),
        /// flat, with the frame line running round over the colour.
        /// </summary>
        public static readonly Color PavingColor = new Color(0.91f, 0.90f, 0.91f);
        public const float BandPastel = 0.2f;
        public const float BandContrast = 4.5f;

        /// <summary>The sand of a resort's tile, and the sea and surf printed on it.</summary>
        public static readonly Color SandColor = new Color(0.97f, 0.86f, 0.58f);
        public static readonly Color SeaColor = new Color(0.30f, 0.70f, 0.84f);
        public static readonly Color SurfColor = new Color(0.98f, 0.98f, 0.96f);

        /// <summary>How far a resort's sand is washed towards the paper: less than a town's, so the beach stands out.</summary>
        public const float SandPastel = 0.25f;

        /// <summary>Where pawns stand: on the price line.</summary>
        public const float PawnOffset = 0.31f;

        /// <summary>The name's line, and the room kept for it between the buildings and the price (it once held the group's strip).</summary>
        public const float NameOffset = 0.07f;
        public const float StripDepth = 0.17f;

        /// <summary>The price line, at the bottom of the square.</summary>
        public const float PriceOffset = 0.31f;

        /// <summary>
        /// On a corner, which has no price: the name's line and where pawns stand, from the square's centre towards
        /// its outer edge (the name) and inner edge (the pawns); a corner that faces away swaps them (<see cref="SceneInset"/>).
        /// </summary>
        public const float CornerNameOffset = 0.19f;
        public const float CornerPawnOffset = 0.22f;

        /// <summary>A corner's small second line (the bus station's ticket), beyond its name, with its size and room.</summary>
        public const float CornerPriceOffset = 0.335f;
        public const float CornerPriceSize = 0.014f;
        public const float CornerPriceDepth = 0.12f;

        /// <summary>How high the board's print (names, prices) lies over a tile's top; anything flat on a tile stays under it.</summary>
        public const float PrintHeight = 0.012f;

        /// <summary>On a corner, pawns also step this far along the edge away from the corner's scene.</summary>
        public const float CornerPawnShift = 0.25f;

        /// <summary>
        /// How much of the tile's width the name may use, and how tall its line and the price's line may be, in tile depths.
        /// The fill leaves a clear margin at both ends, so "Rio de Janeiro" does not run into the tile's edge. A second line does
        /// not help: the name's room is low and wide, so two lines would each come out smaller than the shrunk single line.
        /// </summary>
        public const float NameFill = 0.86f;
        public const float NameDepth = 0.13f;
        public const float PriceDepth = 0.24f;

        /// <summary>How much of the tile's width the name leaves free when the group's letter sits at the strip's end.</summary>
        public const float MarkedNameFill = 0.66f;

        /// <summary>A corner's name line, which has the room a price line would take elsewhere.</summary>
        public const float CornerNameDepth = 0.16f;

        /// <summary>
        /// The jail's name is printed larger, in capitals, with nothing under it (the owner's wish, Faz 23): its size and room
        /// grow by this much, and its line sits between a corner's name line and its second line.
        /// </summary>
        public const float JailNameScale = 1.6f;
        public const float JailNameOffset = 0.26f;

        /// <summary>
        /// A Şans square (a card square along an edge, Faz 24) prints its name larger and in capitals, like the jail, with a
        /// big question mark printed flat on the tile's top part in <see cref="ChanceMarkColor"/>, in place of a standing sign.
        /// </summary>
        public const float ChanceNameScale = 1.6f;
        public const float ChanceMarkSize = 0.12f;
        public const float ChanceMarkDepth = 0.44f;
        public static readonly Color ChanceMarkColor = new Color(0.36f, 0.27f, 0.62f);

        /// <summary>Character sizes of the name and the price before they shrink to fit, in tile widths.</summary>
        public const float NameSize = 0.02f;
        public const float PriceSize = 0.034f;

        /// <summary>
        /// A corner's scene: how far along the edge and how far across the corner it stands, and its size, in corner sides.
        /// On a corner that faces away from the camera the scene and the pawns take the outer part and the name the inner
        /// edge, so their height rises over the sky instead of the name; on the near corners it is the other way round.
        /// </summary>
        public const float SceneOffset = 0.29f;
        public const float SceneInset = 0.22f;
        public const float SceneSize = 0.40f;

        /// <summary>
        /// The bus station diorama (the owner's model, twice the drawn stop's size at the owner's wish): it fills the
        /// corner's back part, from one side nearly to the other, and the pawns that come to it stand in front of the
        /// bus (<see cref="StationPawnOffset"/>, back from the centre: a small negative step comes forward), clear of the
        /// name. <see cref="StationDepth"/> is the model's depth for its width (Tools/Art/make_corner_models.py). The
        /// cartoon bus stop is deeper than the first diorama, so it is a little narrower to leave the pawns and the name room.
        /// </summary>
        public const float StationSize = 0.68f;
        public const float StationOffset = 0.09f;
        public const float StationInset = 0.20f;
        public const float StationDepth = 0.87f;
        public const float StationPawnOffset = -0.10f;

        // The corner that holds the bus station diorama; -1 when the drawn stop stands there.
        private int _stationCorner = -1;

        private OwnerMarker[] _ownerMarkers;
        private Vector3[] _flagScale;
        private int[] _levels;
        private Color?[] _ownerColors;
        private GameObject[][] _buildings;

        // Corner scenes that are textured models (the bus station): turned to show their front to the camera.
        private readonly List<KeyValuePair<Transform, Vector3>> _frontScenes = new List<KeyValuePair<Transform, Vector3>>();
        private Vector3[][] _buildingRest;
        private GameObject[] _tiles;
        private Color[] _tileColors;
        private bool[] _printed;
        private TextMesh[] _names;
        private TextMesh[] _chanceMarks;
        private TextMesh[] _prices;
        private int?[] _rents;
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

        /// <summary>Where pawns stand on a square: on its price line at the bottom, below the name and the buildings; on a corner, beside the scene.</summary>
        public Vector3 PawnSpot(int index)
        {
            Vector3 center = Layout.SquareCenter(index);
            Vector3 outward = Outward(center);
            Vector3 spot = center + ScreenDown(outward) * (_depth * PawnOffset);
            if (Layout.IsCorner(index))
            {
                bool far = FacesAway(outward);
                float back = index == _stationCorner ? StationPawnOffset : CornerPawnOffset;
                spot = center + outward * (_depth * back * (far ? 1f : -1f)) - SceneSide(index, far) * (_depth * CornerPawnShift);
            }

            return transform.TransformPoint(spot) + Vector3.up * SurfaceY;
        }

        /// <summary>Whether a square's outer edge faces away from the game's camera: its pieces' height falls over its outer part.</summary>
        public static bool FacesAway(Vector3 outward) => Vector3.Dot(outward, ScreenUp(BoardCameraRig.BaseYaw)) > 0f;

        /// <summary>Across a square whose outer edge faces <paramref name="outward"/>, the way that runs down the game's screen: out on the near rows, in on the far ones.</summary>
        public static Vector3 ScreenDown(Vector3 outward) => FacesAway(outward) ? -outward : outward;

        /// <summary>Which way along the edge a corner's scene stands: towards the board's corner when the corner faces away, else away from it.</summary>
        private Vector3 SceneSide(int index, bool far) => Layout.Cornerward(index) * (far ? 1f : -1f);

        /// <summary>The square under a screen ray, if any.</summary>
        public bool TryGetSquare(Ray ray, out int squareIndex)
        {
            squareIndex = -1;
            return Physics.Raycast(ray, out RaycastHit hit, 500f) && _squareOfCollider.TryGetValue(hit.collider, out squareIndex);
        }

        /// <summary>How far the squares outside a choice darken towards black (<see cref="Highlight"/> with dimOthers).</summary>
        public const float DimAmount = 0.55f;

        /// <summary>
        /// How far a glowing square turns to its player's colour at the top of a pulse, and how many pulses a
        /// second. The tiles are a pale paper print, so a flash towards white hardly showed; the player's
        /// own colour, about half way, reads at the game's speed and keeps the dark name and price legible (at 85% they vanished).
        /// </summary>
        public const float GlowAmount = 0.55f;
        public const float GlowPulsesPerSecond = 2.2f;

        private bool _dimOthers;
        private int _glowSquare = -1;
        private Color _glowColor;

        /// <summary>The square that glows now (<see cref="Glow"/>); -1 when none does.</summary>
        public int GlowingSquare => _glowSquare;

        /// <summary>
        /// Lights up these squares; null or empty shows the board normally. With <paramref name="dimOthers"/>
        /// the rest of the board goes dark, for a choice where only the lit squares may be picked (a ride).
        /// Otherwise the rest keeps its colour: a hovered group darkening the board reads as a mistake.
        /// </summary>
        public void Highlight(ICollection<int> squares, bool dimOthers = false)
        {
            _highlighted = squares == null || squares.Count == 0 ? null : new HashSet<int>(squares);
            _dimOthers = dimOthers && _highlighted != null;
            for (int i = 0; i < _tiles.Length; i++) PaintSquare(i);
        }

        /// <summary>
        /// Makes a square swell bright and fade again, over and over, tinted towards <paramref name="color"/>
        /// (where a pawn is about to stop). One square glows at a time; <see cref="StopGlow"/> ends it.
        /// </summary>
        public void Glow(int square, Color color)
        {
            if (_glowSquare >= 0 && _glowSquare != square) PaintSquare(_glowSquare);
            _glowSquare = square >= 0 && square < _tiles.Length ? square : -1;
            _glowColor = Color.Lerp(color, Color.white, 0.15f);
        }

        public void StopGlow()
        {
            if (_glowSquare >= 0) PaintSquare(_glowSquare);
            _glowSquare = -1;
        }

        /// <summary>Paints a square's tile with the current highlight, brightened by <paramref name="glow"/> towards the glow's colour.</summary>
        private void PaintSquare(int square, float glow = 0f)
        {
            PaintTile(_tiles[square], Color.Lerp(Shown(square, _tileColors[square]), _glowColor, glow));
        }

        /// <summary>A colour of a square with the current highlight: lit when chosen, dark when a choice leaves it out.</summary>
        private Color Shown(int square, Color color)
        {
            if (_highlighted == null) return color;
            if (_highlighted.Contains(square))
            {
                float lift = _dimOthers ? 0.35f : 0.25f;
                // A town's colours are in its texture and it is drawn white, so it is lit by drawing it brighter than white.
                if (_printed[square]) return new Color(1f + lift, 1f + lift, 1f + lift, 1f);
                return Color.Lerp(color, Color.white, lift);
            }
            return _dimOthers ? Color.Lerp(color, Color.black, DimAmount) : color;
        }

        private void Update()
        {
            if (_glowSquare < 0) return;
            // A soft beat: up to full brightness and back to the tile's own colour, never below it.
            float beat = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * GlowPulsesPerSecond * 2f * Mathf.PI);
            PaintSquare(_glowSquare, GlowAmount * beat);
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
                if (_chanceMarks[i] != null) _chanceMarks[i].transform.localRotation = rotation;
            }

            foreach (KeyValuePair<Transform, Vector3> mark in _marks) mark.Key.localRotation = LabelRotation(mark.Value, yaw);
            TurnBuildings(yaw);
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
            if (_rents[squareIndex].HasValue) ShowPrice(squareIndex);
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

        /// <summary>The building level the board shows on a square now.</summary>
        public int LevelOf(int squareIndex) => _levels[squareIndex];

        /// <summary>
        /// Shows the one building of a level: a house of one to four storeys, or the hotel at the top level.
        /// With <paramref name="animate"/>, the new building drops onto the tile and bounces while the old one
        /// shrinks away, and the plot folds down when the first building takes its place.
        /// </summary>
        public void SetBuildingLevel(int squareIndex, int level, bool animate = false)
        {
            GameObject[] pieces = _buildings[squareIndex];
            if (pieces == null) return;

            _levels[squareIndex] = level;
            PaintBuildings(squareIndex);
            for (int i = 0; i < pieces.Length; i++)
            {
                bool shown = Shows(i, level);
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

            // The plot (or flag) gives way to the first building and returns when the last one is sold.
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
        private bool PieceShown(int squareIndex, int index) => Shows(index, _levels[squareIndex]);

        /// <summary>A square has one building per level (index 0 is the one-storey house, the last the hotel); only the current level's stands.</summary>
        public static bool Shows(int index, int level) => index == Mathf.Min(level, SquareDefinition.MaxBuildingLevel) - 1;

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

        /// <summary>
        /// A building model's walls take the owner's colour, a little lightened, while its doors, frames,
        /// glass and roof keep their own (<see cref="BuildingModels.PaintOwner"/>); the drawn stand-ins have
        /// walls in it and roofs a darker shade. Unowned, each keeps its own colours.
        /// </summary>
        private void PaintBuildings(int squareIndex)
        {
            GameObject[] pieces = _buildings[squareIndex];
            Color? owner = _ownerColors[squareIndex];
            bool models = BuildingModels.IsAvailable;
            foreach (GameObject piece in pieces)
            {
                if (models)
                {
                    BuildingModels.PaintOwner(piece, owner);
                    continue;
                }

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

        private void RefreshOwnerMarker(int squareIndex)
        {
            OwnerMarker marker = _ownerMarkers[squareIndex];
            Color? color = _ownerColors[squareIndex];
            marker.gameObject.SetActive(color.HasValue && _levels[squareIndex] == 0);
            if (_buildings[squareIndex] != null) PaintBuildings(squareIndex);
            if (!color.HasValue) return;
            marker.Paint(color.Value); // The plot and the buildings carry the owner's colour; the tile's edge does not (Faz 21).
        }

        private void Build(BoardData data)
        {
            BoardDefinition board = data.Board;
            Layout = new BoardLayout(board.Count, HalfSizeFor(board.Count), DepthRatio);
            _width = Layout.Width - Gap;
            _depth = Layout.Depth - Gap;

            float width = 2f * Layout.OuterHalf + 2f * PlateMargin;
            HalfExtent = width / 2f;
            // The plate floats in the sky; its edge hangs below the squares.
            GameObject plate = ModelParts.Create("Plate", transform, MeshFactory.RoundedBox(new Vector3(width, PlateThickness, width), 0.2f), PlateColor, 0.25f);
            plate.transform.localPosition = new Vector3(0f, -PlateThickness / 2f, 0f);

            _inner = 2f * Layout.HalfSize - Layout.Depth * 1.05f;
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
            _tiles = new GameObject[board.Count];
            _tileColors = new Color[board.Count];
            _printed = new bool[board.Count];
            _names = new TextMesh[board.Count];
            _chanceMarks = new TextMesh[board.Count];
            _prices = new TextMesh[board.Count];
            _rents = new int?[board.Count];
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

                // The printed tile: its colour washed to a pastel over the paper grain; the group shows in that tint; every name is in the one dark ink.
                // A town is two parts, like the owner's reference: the top, with the buildings and the name, in the group's
                // colour with a pattern, and a light paved strip below where the rent and the pawns are, both printed into
                // the tile (TownSkin); a resort is sand with its sea; the other squares one colour.
                bool town = square.Type == SquareType.Property && !square.IsResort;
                bool printed = town || square.IsResort;
                Color body = printed ? Color.white : Color.Lerp(TileColor(square, data), PaperColor, Pastel);
                GameObject tileObject = ModelParts.Create($"Square {i:00} {square.Name}", transform,
                    MeshFactory.RoundedBox(new Vector3(size.x, TileHeight, size.y), 0.05f, 4), body, 0.05f);
                ModelParts.Skin(tileObject, town ? TownSkin(outward, size, BandColor(TileColor(square, data)))
                    : square.IsResort ? ResortSkin(outward, size) : paper);
                _printed[i] = printed;
                tileObject.transform.localPosition = center + Vector3.up * (TileHeight / 2f);
                tileObject.transform.localRotation = facing;
                _tiles[i] = tileObject;
                _tileColors[i] = body;
                _squareOfCollider[tileObject.AddComponent<BoxCollider>()] = i;

                bool property = square.Type == SquareType.Property;
                bool corner = Layout.IsCorner(i);
                Vector3 down = ScreenDown(outward);
                float top = TileHeight + PrintHeight;
                if (property)
                {
                    // The name is printed straight on the tile (the owner had the raised group strip under it taken
                    // away, Faz 21). With colour-blind friendly colours on, the group's letter sits at the name line's
                    // left end and the name keeps clear of it.
                    string mark = data.GroupMark(square.GroupId);
                    if (mark != null)
                    {
                        Vector3 along = Vector3.Cross(Vector3.up, outward);
                        if (Vector3.Dot(along, ScreenRight(BoardCameraRig.BaseYaw)) > 0f) along = -along;
                        Vector3 line = center + down * (_depth * NameOffset) + Vector3.up * top;
                        CreateBandMark(mark, line + along * (size.x * 0.4f), outward, InkColor);
                    }
                }
                else if (corner && CornerParts.Has(square.Type))
                {
                    CreateScene(square.Type, i, center, outward, size.y);
                }
                else if (IsChance(i))
                {
                    _chanceMarks[i] = CreateLabel("Chance mark", "?", BuildingRowCenter(center) + Vector3.up * PrintHeight, outward,
                        _width * ChanceMarkSize, new Vector2(size.x * NameFill, _depth * ChanceMarkDepth), ChanceMarkColor);
                }
                else
                {
                    CreateIcon(square.Type, center, outward);
                }

                // Price at the bottom, name above it (on its strip on a town), buildings or the sign on the top part.
                // A corner has no price: its name lies on the outer part, or the inner edge when the corner faces away,
                // clear of its scene and pawns.
                Vector3 namePlace = corner
                    ? center + outward * ((FacesAway(outward) ? -1f : 1f) * _depth * (square.Type == SquareType.Jail ? JailNameOffset : CornerNameOffset))
                    : center + down * (_depth * NameOffset);
                _names[i] = CreateLabel("Name", PrintedName(square, corner), namePlace + Vector3.up * top, outward,
                    NameCharacterSize(i), NameRoom(i, size), InkColor);
                string subtitle = Subtitle(square);
                if (subtitle != null || property)
                {
                    Vector3 pricePlace = corner
                        ? center + outward * ((FacesAway(outward) ? -1f : 1f) * _depth * CornerPriceOffset)
                        : center + down * (_depth * PriceOffset);
                    _prices[i] = CreateLabel("Price", subtitle ?? string.Empty, pricePlace + Vector3.up * (TileHeight + PrintHeight), outward,
                        PriceCharacterSize(i), PriceRoom(i, size), PriceColor);
                }

                if (property)
                {
                    // Owner marker: the owner's plot (or the flag) on the tile's top part, where the buildings will stand.
                    // On a resort it is the owner's beach, which grows with the resorts they hold (SetResortStage).
                    OwnerMarker marker = square.IsResort ? OwnerMarker.CreateResort("Owner marker", transform) : OwnerMarker.Create("Owner marker", transform, i * 0.9f);
                    marker.Place(outward, square.IsResort ? ResortSpot(center) : BuildingRowCenter(center), _yaw);
                    _flagScale[i] = marker.RestScale(_width);
                    marker.transform.localScale = _flagScale[i];
                    marker.gameObject.SetActive(false);
                    _ownerMarkers[i] = marker;
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
            scene.transform.localScale = Vector3.one * (side * SceneSize);
            if (!CornerParts.ShowsFront(scene))
            {
                scene.transform.localRotation = Quaternion.LookRotation(outward);
                return;
            }

            // The diorama: twice the drawn stop, over the corner's back part; its front (the bus) turns to the camera
            // like a building's, so it never shows its back.
            _stationCorner = index;
            scene.transform.localPosition = center + SceneSide(index, far) * (side * StationOffset) + outward * (side * StationInset * (far ? 1f : -1f)) + Vector3.up * TileHeight;
            scene.transform.localScale = Vector3.one * (side * StationSize);
            scene.transform.localRotation = Quaternion.LookRotation(BuildingFront(outward, _yaw));
            _frontScenes.Add(new KeyValuePair<Transform, Vector3>(scene.transform, outward));
        }

        /// <summary>The lawn in the middle: a gravel ring where the squares end and plain grass (the owner had the trees, bunting and paths taken away).</summary>
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
        private Vector3 BuildingRowCenter(Vector3 center) => center + ScreenDown(Outward(center)) * (_depth * BuildingOffset) + Vector3.up * TileHeight;

        /// <summary>Where a resort's beach stands: on the sand between the sea and the name.</summary>
        private Vector3 ResortSpot(Vector3 center) => center + ScreenDown(Outward(center)) * (_depth * ResortOffset) + Vector3.up * TileHeight;

        /// <summary>
        /// The group's colour for a town's name band: washed towards the paper from <see cref="BandPastel"/> on, only as far as
        /// the dark ink needs to reach <see cref="BandContrast"/> on it, so the light groups stay bright and the dark ones still read.
        /// </summary>
        public static Color BandColor(Color group)
        {
            for (float wash = BandPastel; wash < 1f; wash += 0.05f)
            {
                Color band = Color.Lerp(group, PaperColor, wash);
                if (Contrast(band, InkColor) >= BandContrast) return band;
            }

            return PaperColor;
        }

        /// <summary>The contrast ratio of two colours (WCAG), 1 to 21.</summary>
        public static float Contrast(Color a, Color b)
        {
            float la = RelativeLuminance(a), lb = RelativeLuminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        private static float RelativeLuminance(Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

        private static float Linear(float channel) => channel <= 0.04045f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);

        /// <summary>
        /// A town tile's skin: the group's colour from the tile's top edge on the screen down to the bottom of the name's
        /// line, across its whole width, and the light paving below. The tile's local Z runs outward and its texture's v
        /// with it, so the colour lies at the low or the high end of v as the screen's down runs with or against outward.
        /// </summary>
        private Texture2D TownSkin(Vector3 outward, Vector2 size, Color color)
        {
            float bottom = _depth * (NameOffset + StripDepth / 2f) / size.y + 0.5f;
            bool withOutward = Vector3.Dot(ScreenDown(outward), outward) > 0f;
            return withOutward
                ? TileSkin.Town(color, PavingColor, 0f, bottom, size.y / size.x)
                : TileSkin.Town(color, PavingColor, 1f - bottom, 1f, size.y / size.x);
        }

        /// <summary>
        /// A resort tile's skin: the sand, the sea along the tile's top edge on the screen and the surf below it, inside
        /// the frame (<see cref="TileSkin.Resort"/>); the sea lies at the low or the high end of v as for a town.
        /// </summary>
        private Texture2D ResortSkin(Vector3 outward, Vector2 size)
        {
            float seaEnd = 0.5f + _depth * (SeaOffset + SeaDepth / 2f) / size.y;
            bool withOutward = Vector3.Dot(ScreenDown(outward), outward) > 0f;
            return TileSkin.Resort(Color.Lerp(SandColor, PaperColor, SandPastel), SeaColor, SurfColor, seaEnd,
                _width * SurfRadius / size.x, SurfScallops, !withOutward, size.y / size.x);
        }

        /// <summary>
        /// Shows on a resort the beach for an owner who holds <paramref name="held"/> resorts; it pops up from the sand
        /// when it grows or shrinks while in view and <paramref name="animate"/> is set. Nothing happens on a square that is not a resort.
        /// </summary>
        public void SetResortStage(int squareIndex, int held, bool animate = false)
        {
            OwnerMarker marker = _ownerMarkers[squareIndex];
            if (marker == null || !marker.IsResort) return;
            int before = marker.Stage;
            marker.ShowStage(held);
            if (animate && marker.Stage != before && marker.gameObject.activeInHierarchy) StartCoroutine(Raise(marker.transform, _flagScale[squareIndex]));
        }

        /// <summary>A square's price line: big at the bottom of a town or tax square, small under a corner's name.</summary>
        private float PriceCharacterSize(int index) => _width * (Layout.IsCorner(index) ? CornerPriceSize : PriceSize);

        private Vector2 PriceRoom(int index, Vector2 size) =>
            new Vector2(size.x * NameFill, _depth * (Layout.IsCorner(index) ? CornerPriceDepth : PriceDepth));

        /// <summary>The room a square's name may fill: the tile's width less a margin (and the group's letter, when shown), and its line's depth.</summary>
        /// <summary>How much larger than other names a square's name is printed: the jail's, larger.</summary>
        private float NameScale(int index) =>
            _data.Board[index].Type == SquareType.Jail ? JailNameScale : IsChance(index) ? ChanceNameScale : 1f;

        /// <summary>A Şans square: a card square along an edge (the card corner is Sokak Haberi, with its kiosk).</summary>
        private bool IsChance(int index) => _data.Board[index].Type == SquareType.Card && !Layout.IsCorner(index);

        /// <summary>The flat "?" printed on a Şans square, or null on any other square.</summary>
        public TextMesh ChanceMark(int index) => _chanceMarks != null ? _chanceMarks[index] : null;

        private float NameCharacterSize(int index) => _width * NameSize * NameScale(index);

        private Vector2 NameRoom(int index, Vector2 size)
        {
            if (Layout.IsCorner(index)) return new Vector2(size.x * NameFill, _depth * CornerNameDepth * NameScale(index));
            SquareDefinition square = _data.Board[index];
            bool marked = square.Type == SquareType.Property && _data.GroupMark(square.GroupId) != null;
            return new Vector2(size.x * (marked ? MarkedNameFill : NameFill), _depth * NameDepth * NameScale(index));
        }

        /// <summary>
        /// A building model's size: one model unit is this many tile widths, so its footprint
        /// (<see cref="BuildingModels.Width"/> along the edge) takes about three fifths of the tile's width and the hotel
        /// stands about a tile width tall. Faz 21: the owner asked for half as big again (0.44 to 0.66).
        /// </summary>
        public const float BuildingScale = 0.66f;

        /// <summary>The drawn house that stands in when the models are missing, in tile widths (grown with the models).</summary>
        public const float FallbackHouseSize = 0.39f;

        // One building per level on the tile's inner part, all hidden until built: the models (their door
        // towards the camera, see BuildingFront), or drawn houses and the mansion when the models are not in the build.
        private GameObject[] CreateBuildings(Vector3 center)
        {
            var pieces = new GameObject[SquareDefinition.MaxBuildingLevel];
            Vector3 outward = Outward(center);
            Vector3 spot = BuildingRowCenter(center);
            for (int i = 0; i < pieces.Length; i++)
            {
                int level = i + 1;
                GameObject model = BuildingModels.Create(level, Level(level), transform);
                if (model != null)
                {
                    pieces[i] = CreatePiece(model, spot, Vector3.one * (_width * BuildingScale), BuildingFront(outward, _yaw));
                }
                else if (level < SquareDefinition.MaxBuildingLevel)
                {
                    pieces[i] = CreatePiece(BuildingParts.House(Level(level), transform), spot, Vector3.one * (_width * FallbackHouseSize), -outward);
                }
                else
                {
                    GameObject mansion = BuildingParts.Mansion(Level(level), transform);
                    ChimneySmoke.Attach(mansion, BuildingParts.ChimneyTop);
                    pieces[i] = CreatePiece(mansion, spot, new Vector3(_width * 0.69f, _width * 0.45f, _width * 0.39f), -outward);
                }
            }

            return pieces;
        }

        private static string Level(int level) => level >= SquareDefinition.MaxBuildingLevel ? "Hotel" : $"House {level}";

        // Buildings have their base at y = 0 and their long side along the tile's edge; front is +Z.
        private GameObject CreatePiece(GameObject piece, Vector3 baseCenter, Vector3 size, Vector3 front)
        {
            piece.transform.localPosition = baseCenter;
            piece.transform.localRotation = Quaternion.LookRotation(front);
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

        /// <summary>The sign of a special square, standing on the tile's top part where towns have their buildings.</summary>
        private void CreateIcon(SquareType type, Vector3 center, Vector3 outward)
        {
            Mesh mesh = SquareIcons.For(type);
            if (mesh == null) return;
            GameObject icon = ModelParts.Create($"Sign {type}", transform, mesh, SquareIcons.ColorFor(type), 0.3f);
            icon.transform.localPosition = BuildingRowCenter(center);
            // The sign's front (+Z) faces the camera, like the buildings'.
            icon.transform.localRotation = Quaternion.LookRotation(ScreenDown(outward));
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

        /// <summary>
        /// Which way a building model's front (its door, the plot's sign) turns on a square whose outer edge
        /// faces <paramref name="outward"/>, for a camera turned by <paramref name="yaw"/>: out over the edge
        /// on the sides near the camera, in towards the lawn on the far sides, so the camera never sees a back.
        /// </summary>
        public static Vector3 BuildingFront(Vector3 outward, float yaw) => Vector3.Dot(outward, ScreenUp(yaw)) > 0f ? -outward : outward;

        /// <summary>Turns the building models and plots to show their fronts to a camera at <paramref name="yaw"/>.</summary>
        private void TurnBuildings(float yaw)
        {
            foreach (KeyValuePair<Transform, Vector3> scene in _frontScenes)
            {
                if (scene.Key != null) scene.Key.localRotation = Quaternion.LookRotation(BuildingFront(scene.Value, yaw));
            }

            if (_buildings == null || !BuildingModels.IsAvailable) return;
            for (int i = 0; i < _buildings.Length; i++)
            {
                Vector3 outward = Outward(Layout.SquareCenter(i));
                if (_buildings[i] != null)
                {
                    Quaternion facing = Quaternion.LookRotation(BuildingFront(outward, yaw));
                    foreach (GameObject piece in _buildings[i]) piece.transform.localRotation = facing;
                }

                if (_ownerMarkers != null && _ownerMarkers[i] != null) _ownerMarkers[i].Turn(outward, yaw);
            }
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

        /// <summary>The line under a square's name: the tax it takes or the fare; null when the square has none (a town for sale shows no figure).</summary>
        public static string Subtitle(SquareDefinition square)
        {
            switch (square.Type)
            {
                case SquareType.Property: return null; // For sale a town shows no figure; owned, its rent (the owner's wish, Faz 22).
                case SquareType.Tax: return F($"-{square.TaxAmount}");
                case SquareType.Travel: return F($"bilet {square.Fee}");
                default: return null;
            }
        }

        /// <summary>
        /// What a town's price line says: nothing while it is for sale, or, once owned, the rent a visitor pays
        /// (<paramref name="rent"/>), in the print's ink. Null clears it again.
        /// </summary>
        public void SetRent(int squareIndex, int? rent)
        {
            if (_rents == null || _rents[squareIndex] == rent) return;
            _rents[squareIndex] = rent;
            ShowPrice(squareIndex);
        }

        /// <summary>A square's name line and its price line (null where it has none).</summary>
        public TextMesh NameLabel(int squareIndex) => _names[squareIndex];
        public TextMesh PriceLabel(int squareIndex) => _prices[squareIndex];

        /// <summary>The rent a town's price line shows now; null while it shows the price.</summary>
        public int? RentShown(int squareIndex) => _rents?[squareIndex];

        private void ShowPrice(int index)
        {
            TextMesh price = _prices[index];
            if (price == null) return;
            int? rent = _rents[index];
            price.text = rent.HasValue ? F($"{rent.Value}") : Subtitle(_data.Board[index]) ?? string.Empty;
            price.color = PriceColor; // The rent stays in the print's ink, never the owner's colour (the owner's wish, Faz 21).
            Vector2 size = Layout.SquareSize(index) - Vector2.one * Gap;
            FitLabel(price, PriceCharacterSize(index), PriceRoom(index, size), Outward(Layout.SquareCenter(index)));
        }

        /// <summary>
        /// A square's name as printed on the board: a town's, the jail's and a Şans square's (a card square off the corners) in
        /// capitals ("İSTANBUL", "HAPİS", "ŞANS", Turkish capitals in Turkish), the others as written.
        /// </summary>
        public static string PrintedName(SquareDefinition square, bool corner) =>
            square.Type == SquareType.Property || square.Type == SquareType.Jail || (square.Type == SquareType.Card && !corner)
                ? Upper(L(square.Name)) : L(square.Name);

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
                    _names[i].text = PrintedName(square, Layout.IsCorner(i));
                    FitLabel(_names[i], NameCharacterSize(i), NameRoom(i, size), outward);
                }

                if (_prices[i] != null)
                {
                    ShowPrice(i);
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

        /// <summary>The group's letter beside the town's name, in the name's ink.</summary>
        private void CreateBandMark(string mark, Vector3 localPosition, Vector3 outward, Color ink)
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
            mesh.color = ink;
            mesh.font = UiTheme.BlackFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = PrintMaterial.For(mesh.font);
        }

        private static Color TileColor(SquareDefinition square, BoardData data)
        {
            switch (square.Type)
            {
                case SquareType.Start: return StartColor;
                case SquareType.Tax: return TaxColor;
                case SquareType.Property: return square.IsResort ? SandColor : data.GroupColor(square.GroupId);
                case SquareType.Card: return CardColor;
                case SquareType.Pot: return PotColor;
                case SquareType.Jail: return JailColor;
                case SquareType.Travel: return TravelColor;
                case SquareType.Lookout: return LookoutColor;
                default: return RestColor;
            }
        }

        /// <summary>Tiles are matte paper: the highlight must not polish them.</summary>
        private static void PaintTile(GameObject tile, Color color) => ModelParts.Paint(tile, color, 0.05f);
    }
}
