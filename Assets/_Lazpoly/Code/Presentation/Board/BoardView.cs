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
    /// and the pot (no logo since 2026-11-10). When the Blender models arrive they replace these shapes; the layout and the API stay the same.
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

        /// <summary>The gap between neighbouring tiles: none since Faz 25 (the owner wanted the squares side by side; their rounded edges still mark the seam).</summary>
        public const float Gap = 0f;
        private const float TileHeight = 0.2f;

        /// <summary>
        /// How round a tile's edges are. Only just: the squares lie side by side (Faz 25), and rounder edges (0.05) left a
        /// groove between neighbours that read as a gap; the owner wanted them to meet with none.
        /// </summary>
        public const float TileEdgeRadius = 0.005f;

        /// <summary>
        /// How far the plate hangs below the squares; its edge shows in the isometric view. Half as thick since 2026-11-11
        /// (the owner's wish; it was 0.6).
        /// </summary>
        public const float PlateThickness = 0.3f;

        /// <summary>
        /// The plate's rim outside the squares: none since 2026-11-11 (the owner's wish: the plate ends flush with the
        /// squares' outer edge, nothing sticking out).
        /// </summary>
        public const float PlateMargin = 0f;

        /// <summary>Top of the lawn in the middle of the board, where the dice land.</summary>
        public const float LawnHeight = 0.08f;

        /// <summary>How much of the inner square the lawn covers; the road shows round it.</summary>
        public const float LawnShare = 0.9f;

        /// <summary>Where the dice score stands on the lawn, towards the top of the screen from the middle, in lawn widths.</summary>
        public const float ScoreOffset = 0.17f;

        /// <summary>
        /// Each player's money lies on a shelf just outside the board's edge that faces their corner card (seats 1-4 take a
        /// corner each, 5-8 share them), its money safe along the edge: the safe's middle this far out from the
        /// plate's edge (past the gap and the shelf's margin), and when two share an edge, this far either side of its middle
        /// along it.
        /// </summary>
        public static float MoneyPileOut(int seat, float yaw) => ShelfGapAt(seat, yaw) + ShelfMargin + MoneyPile.SafeSize.z / 2f;
        public const float MoneyPileSpread = 2.4f;

        /// <summary>
        /// The shelf each money safe stands on, so the money lies on the table and not in the air: a little floating island
        /// since 2026-11-11 (the owner's wish; <see cref="IslandParts"/>, it was the plate's stone), its meadow top a little
        /// lower than the plate's top, this far out from the plate's edge (the owner wanted a small gap between them; it first
        /// reached in under the plate), a little bigger than the money safe all round (the owner's wish, 2026-11-11:
        /// <see cref="ShelfMargin"/>). On the far edges the gap is wider since 2026-11-11 (the owner's wish:
        /// the safes at the top a little further from the table; it was 0.5).
        /// </summary>
        public const float ShelfDrop = 0.03f;
        public const float ShelfGap = 0.9f;

        /// <summary>
        /// On the two edges nearest the camera the plate's own side face, seen from above, hangs down over the gap: at
        /// 0.5 no sky showed there, at 1.59 a strip three times the far edges' did and the 8-player
        /// trays nearly touched the bottom corner cards. This gap shows a strip a little wider than the far edges' one.
        /// </summary>
        public const float NearShelfGap = 1.15f;

        /// <summary>How far a seat's shelf stands off the plate: further on the edges nearest the camera (<see cref="NearShelfGap"/>).</summary>
        public static float ShelfGapAt(int seat, float yaw) =>
            Vector3.Dot(MoneyEdgeOutward(seat, yaw), ScreenUp(yaw)) > 0f ? ShelfGap : NearShelfGap;
        public const float ShelfMargin = 0.12f;

        /// <summary>Where the tea money's jar stands, as fractions of the lawn's inner width from the centre: a little below the pot's line and to its right, clear of the dice.</summary>
        public const float PotPileOffset = -0.04f;
        public const float PotPileSide = 0.12f;

        // The squares that are no town, each before its wash towards the paper (Pastel). Since 2026-11-10 (the owner's
        // wish: tones close to each other, pleasing, not every colour of the rainbow) they are one family of warm
        // creams, a good deal darker than the light shows them: the table's light brightens a plain tile by about 1.5
        // (linear), and brighter ones burn out to lemon and white on the screen.
        public static readonly Color StartColor = new Color(0.45f, 0.56f, 0.38f);
        public static readonly Color TaxColor = new Color(0.60f, 0.58f, 0.55f);
        private static readonly Color RestColor = new Color(0.93f, 0.91f, 0.85f);
        /// <summary>
        /// A Şans square's ground (the owner's wish, 2026-11-11: a colour that suits the table better): a soft jade, the
        /// lawn's and the sea's quiet green among the warm squares, printed as it is rather than washed to the paper, so
        /// the cards on it (<see cref="ChancePrint"/>) stand out. Their back, outline and "?" are a deep shade of it, the pips and sparkles gold.
        /// </summary>
        public static readonly Color ChanceColor = new Color(0.46f, 0.60f, 0.55f);
        public static readonly Color ChanceDeep = new Color(0.13f, 0.33f, 0.31f);
        public static readonly Color ChanceGold = new Color(0.90f, 0.69f, 0.26f);
        private static readonly Color PotColor = new Color(0.55f, 0.82f, 0.75f);
        public static readonly Color TravelColor = new Color(0.66f, 0.67f, 0.65f);
        /// <summary>The Champions Cup corner is printed on the bus station's ground (the owner's wish, 2026-11-11).</summary>
        public static readonly Color CupColor = TravelColor;
        public static readonly Color JailColor = new Color(0.74f, 0.72f, 0.70f);
        private static readonly Color LookoutColor = new Color(0.80f, 0.70f, 0.95f);
        /// <summary>The paper every tile is printed on; a group's colour is mixed into it by <see cref="Pastel"/>.</summary>
        public static readonly Color PaperColor = new Color(0.95f, 0.92f, 0.85f);

        /// <summary>How far a tile's own colour is washed towards the paper: the pastel of the printed board.</summary>
        public const float Pastel = 0.6f;
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
        /// Where the owner's factory stands on a factory city (the squares that were the holiday resorts; the owner's wish,
        /// 2026-11-11), in tile depths towards the screen's bottom: a little further down the tile than a town's buildings.
        /// </summary>
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

        /// <summary>
        /// A factory city's tile (the owner's wish, 2026-11-11): dark grey paving with larger stones than a town's
        /// (<see cref="TileSkin.Factory"/>), its name and rent printed in a light ink that reads on it. A step lighter
        /// than at first (the owner's wish, 2026-11-11: "biraz daha aç").
        /// </summary>
        public static readonly Color FactoryStone = new Color(0.38f, 0.39f, 0.41f);
        public static readonly Color FactoryInk = new Color(0.95f, 0.93f, 0.88f);

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

        /// <summary>
        /// The jail corner as the owner laid it out (Faz 28), in shares of the tile's side measured on the screen: the cell
        /// towards the tile's left tip, the "HAPİS" lettering towards its lower right edge (no wider than
        /// <see cref="JailNameFill"/>), the pawns towards its top tip.
        /// </summary>
        public const float JailSceneShift = 0.3f;
        public const float JailNameShift = 0.26f;
        public const float JailNameFill = 0.6f;
        public const float JailPawnShift = 0.22f;

        /// <summary>From a tile's middle towards its left tip on the screen (the diamond's left corner), for the table's camera.</summary>
        public static Vector3 ScreenLeftTip => -DiagonalLine(BoardCameraRig.BaseYaw);

        /// <summary>From a tile's middle towards the middle of its lower right edge on the screen.</summary>
        public static Vector3 ScreenLowerRight => (DiagonalLine(BoardCameraRig.BaseYaw) - DiagonalTops(BoardCameraRig.BaseYaw)).normalized;

        /// <summary>
        /// The cup's long name runs across its corner, along the tile's diagonal that lies left to right on the screen (the
        /// owner's wish, Faz 24; <see cref="DiagonalRotation"/>), on two lines ("ŞAMPİYONLAR" over "KUPASI") so the letters can
        /// be large: it starts as large as the jail's and shrinks to its room, which is this long and deep in corner sides, its
        /// middle this far below the corner's middle on the screen.
        /// </summary>
        public const float CupNameScale = 1.6f;
        public const float CupNameLength = 1.15f;
        public const float CupNameDepth = 0.30f;
        public const float CupNameShift = 0.12f;

        /// <summary>
        /// The owner's stadium on the cup's corner (in the cup model's place, the owner's wish): its width in corner sides, its
        /// depth for its width (Tools/Art/make_corner_models.py), and how far up the screen from the corner's middle it stands,
        /// so its front edge stays just behind the two-line name and its back low enough to clear the turn banner.
        /// </summary>
        public const float StadiumSize = 0.46f;
        public const float StadiumDepth = 0.98f;
        public const float StadiumShift = 0.26f;

        /// <summary>
        /// The cup's scene is a little smaller than the other corners' and stands nearer the corner's middle (less inset
        /// towards the outer edge), so on the far corner its top stays clear of the banner over the board.
        /// </summary>
        public const float CupSceneSize = 0.30f;
        public const float CupSceneInset = 0.04f;


        /// <summary>
        /// A Şans square (a card square along an edge, Faz 24) prints its name larger and in capitals, like the jail, low on
        /// the tile (where a price would sit), with two fanned cards printed flat over the rest of the tile (<see cref="ChancePrint"/>,
        /// since 2026-11-11) and a big question mark on the front one, in place of a standing sign. Offsets and depth are in
        /// tile depths, towards the screen's bottom.
        /// </summary>
        public const float ChanceNameScale = 1.6f;

        /// <summary>
        /// A tax square's name, in capitals and larger too ("VERGİ", the owner's wish, Faz 24). A tax taken as a share prints
        /// no figure: its name sits low like Şans, under a document printed flat on the tile (<see cref="FloorPrint.Document"/>);
        /// a fixed tax keeps its amount on the price line.
        /// </summary>
        public const float TaxNameScale = 1.6f;
        public const float DocumentDepth = 0.46f;
        public const float DocumentOffset = -0.12f;

        /// <summary>The start arrow's length across the corner (shorter when it runs towards the name) and how far from the middle it lies, away from the name, in corner sides.</summary>
        public const float StartArrowLength = 0.72f;
        public const float StartArrowShortLength = 0.5f;
        public const float StartArrowOffset = 0.17f;
        public const float ChanceNameOffset = 0.3f;
        public const float ChanceMarkOffset = -0.15f;
        public const float ChanceMarkSize = 0.3f;
        public const float ChanceMarkDepth = 0.62f;

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
        private Transform _station;
        private BusView _bus;
        private RoadPath _lane;

        /// <summary>The road's top, where the bus drives round the lawn.</summary>
        public const float RoadHeight = 0.04f;

        /// <summary>The square of the bus station diorama; -1 on a board without it.</summary>
        public int StationSquare => _stationCorner;

        /// <summary>The station's bus, which takes a player along the road (the owner's wish, 2026-11-10); null when the diorama or the bus is not in the build.</summary>
        public BusView Bus => _bus;

        /// <summary>The middle of the road's asphalt round the lawn, in the board's frame: the bus drives along it, over the painted line.</summary>
        public RoadPath Lane => _lane;

        /// <summary>The bus's way from its stop to the road and along it to the point nearest <paramref name="square"/>.</summary>
        public BusRoute BusRouteTo(int square) =>
            BusRoute.Out(_bus.ParkedSpot, _bus.ParkedHeading, BusGate, _lane, Layout.SquareCenter(square), BusGround);

        /// <summary>The bus's way home from the point of the road nearest <paramref name="square"/>, on round the lawn to its stop.</summary>
        public BusRoute BusRouteHome(int square) =>
            BusRoute.Home(_bus.ParkedSpot, _bus.ParkedHeading, BusGate, _lane, Layout.SquareCenter(square), BusGround);

        /// <summary>The station square's inner corner: the one place the bus gets down to the road without crossing another square.</summary>
        public Vector3 BusGate
        {
            get
            {
                Vector3 corner = Layout.SquareCenter(_stationCorner);
                float edge = Layout.HalfSize - Layout.Depth / 2f;
                return new Vector3(Mathf.Sign(corner.x) * edge, 0f, Mathf.Sign(corner.z) * edge);
            }
        }

        private BusRoute.Ground BusGround =>
            new BusRoute.Ground(Layout.HalfSize - Layout.Depth / 2f, TileHeight, RoadHeight, _bus.StopRise, _bus.BaseCentre, _bus.BaseHalf);

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
        private Transform[] _chanceCards;
        private Transform[] _documents;
        private Transform _startArrow;

        // The small cup beside the buildings of the town that holds it (SetCup), its size, and that town; -1 while none does.
        private Transform _cupTrophy;
        private Transform _cupScene;
        private Vector3 _cupScale;
        private int _cupSquare = -1;
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
            if (Layout.IsCorner(index) && _data.Board[index].Type == SquareType.Jail)
            {
                // The jail's pawns wait towards the tile's top tip, clear of the cell on the left and the lettering lower right (Faz 28).
                spot = center + DiagonalTops(BoardCameraRig.BaseYaw) * (Layout.SquareSize(index).y * JailPawnShift);
            }
            else if (Layout.IsCorner(index))
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

        /// <summary>
        /// Turns everything written on the board so it reads from a camera at <paramref name="yaw"/>: the
        /// plaques and group letters flip to run left to right, and the score lies square to the screen.
        /// </summary>
        public void FaceCamera(float yaw)
        {
            _yaw = yaw;
            for (int i = 0; _names != null && i < _names.Length; i++)
            {
                Quaternion rotation = LabelRotation(Outward(Layout.SquareCenter(i)), yaw);
                if (_names[i] != null) _names[i].transform.localRotation = NameRotation(i, rotation, yaw);
                if (_prices[i] != null) _prices[i].transform.localRotation = rotation;
                if (_chanceMarks[i] != null) _chanceMarks[i].transform.localRotation = rotation;
                if (_documents[i] != null) _documents[i].localRotation = PrintRotation(Outward(Layout.SquareCenter(i)), yaw);
                if (_chanceCards[i] != null) _chanceCards[i].localRotation = PrintRotation(Outward(Layout.SquareCenter(i)), yaw);
            }

            foreach (KeyValuePair<Transform, Vector3> mark in _marks) mark.Key.localRotation = LabelRotation(mark.Value, yaw);
            TurnBuildings(yaw);
            Vector3 up = ScreenUp(yaw);
            if (_scoreboard != null)
            {
                _scoreboard.localPosition = new Vector3(0f, LawnHeight + 0.01f, 0f) + up * (_inner * ScoreOffset);
                _scoreboard.localRotation = FlatFacing(yaw);
            }

            if (_potPile != null) _potPile.Root.localPosition = new Vector3(0f, LawnHeight, 0f) + up * (_inner * PotPileOffset) + ScreenRight(yaw) * (_inner * PotPileSide);
            PlaceMoneyPiles(yaw);
        }

        private readonly List<MoneyPile> _moneyPiles = new List<MoneyPile>();
        private readonly List<Transform> _shelves = new List<Transform>();
        private readonly List<MoneyHatch> _hatches = new List<MoneyHatch>();

        /// <summary>
        /// Stands a money safe on a shelf at the table's edge for each seat, in the seat's colour, in seat order, and puts
        /// the seat's money hatch on the lawn on that edge's side.
        /// </summary>
        public void CreateMoneyPiles(IReadOnlyList<Color> seatColors)
        {
            foreach (MoneyPile pile in _moneyPiles) Discard(pile.Root.gameObject);
            foreach (Transform shelf in _shelves) Discard(shelf.gameObject);
            foreach (MoneyHatch hatch in _hatches) Discard(hatch.gameObject);
            _moneyPiles.Clear();
            _shelves.Clear();
            _hatches.Clear();
            for (int seat = 0; seat < seatColors.Count; seat++)
            {
                _moneyPiles.Add(new MoneyPile(transform, $"Money of seat {seat + 1}", seatColors[seat]));
                // A little floating island (the owner's wish, 2026-11-11), its meadow top the shelf's size.
                Vector3 size = ShelfSize;
                Transform island = IslandParts.Create(transform, $"Money shelf of seat {seat + 1}", new Vector2(size.x, size.z), seat).transform;
                IslandDrift.Attach(island.Find(IslandParts.StonesName), seat);
                _shelves.Add(island);
                _hatches.Add(MoneyHatch.Create(transform, $"Money hatch of seat {seat + 1}"));
            }

            PlaceMoneyPiles(_yaw);
        }

        private static void Discard(GameObject thing)
        {
            if (Application.isPlaying) Destroy(thing);
            else DestroyImmediate(thing);
        }

        /// <summary>A shelf's size: the money safe's foot and <see cref="ShelfMargin"/> round it, and how deep its island hangs.</summary>
        public static Vector3 ShelfSize => new Vector3(
            MoneyPile.SafeSize.x + 2f * ShelfMargin,
            IslandParts.Depth,
            MoneyPile.SafeSize.z + 2f * ShelfMargin);

        /// <summary>The seats' shelves at the table's edge, in seat order.</summary>
        public IReadOnlyList<Transform> MoneyShelves => _shelves;

        /// <summary>The seats' money at the table's edge, in seat order.</summary>
        public IReadOnlyList<MoneyPile> MoneyPiles => _moneyPiles;

        /// <summary>Shows a seat's money in its safe.</summary>
        public void ShowSeatMoney(int seat, int money)
        {
            if (seat >= 0 && seat < _moneyPiles.Count) _moneyPiles[seat].Show(money);
        }

        /// <summary>
        /// Where a seat's money lies, in the board's frame: outside the middle of the board's edge that faces the seat's
        /// corner card (0 top left, 1 top right, 2 bottom left, 3 bottom right; seat 5 shares seat 1's edge and so on), on
        /// its shelf a little below the plate's top.
        /// </summary>
        public static Vector3 MoneyPileSpot(int seat, int seats, float halfExtent, float yaw) =>
            MoneyEdgeOutward(seat, yaw) * (halfExtent + MoneyPileOut(seat, yaw)) + MoneyEdgeAlong(seat, seats, yaw) - Vector3.up * ShelfDrop;

        /// <summary>The way out of the board across the edge a seat's money lies at (one of the board's axes).</summary>
        public static Vector3 MoneyEdgeOutward(int seat, float yaw)
        {
            int corner = seat % 4;
            Vector3 up = ScreenUp(yaw), right = ScreenRight(yaw);
            return ((corner < 2 ? up : -up) + (corner % 2 == 0 ? -right : right)).normalized;
        }

        /// <summary>How far along its edge a seat's money lies from the edge's middle: aside only when two seats share the edge.</summary>
        private static Vector3 MoneyEdgeAlong(int seat, int seats, float yaw)
        {
            int corner = seat % 4;
            bool shared = seats > corner + 4;
            float offset = shared ? (seat < 4 ? -MoneyPileSpread : MoneyPileSpread) : 0f;
            return Vector3.Cross(Vector3.up, MoneyEdgeOutward(seat, yaw)) * offset;
        }

        /// <summary>
        /// How a seat's money lies: its row along the board's edge, turned whichever way of the two lets the money's print read
        /// to the camera (away from it for the far edges, towards the board for the near ones).
        /// </summary>
        public static Quaternion MoneyPileTurn(int seat, float yaw)
        {
            Vector3 outward = MoneyEdgeOutward(seat, yaw);
            return Quaternion.LookRotation(Vector3.Dot(outward, ScreenUp(yaw)) > 0f ? outward : -outward);
        }

        private void PlaceMoneyPiles(float yaw)
        {
            for (int seat = 0; seat < _moneyPiles.Count; seat++)
            {
                Vector3 shelf = ShelfSize;
                Transform root = _moneyPiles[seat].Root;
                root.localPosition = MoneyPileSpot(seat, _moneyPiles.Count, HalfExtent, yaw);
                root.localRotation = MoneyPileTurn(seat, yaw);
                Vector3 outward = MoneyEdgeOutward(seat, yaw);
                if (seat >= _shelves.Count) continue;
                _shelves[seat].localPosition = outward * (HalfExtent + ShelfGapAt(seat, yaw) + shelf.z / 2f) + MoneyEdgeAlong(seat, _moneyPiles.Count, yaw)
                    - Vector3.up * ShelfDrop;
                _shelves[seat].localRotation = Quaternion.LookRotation(outward);
                if (seat >= _hatches.Count) continue;
                _hatches[seat].transform.localPosition = HatchSpot(seat, _moneyPiles.Count, _inner * LawnShare / 2f, yaw) + Vector3.up * LawnHeight;
                _hatches[seat].transform.localRotation = MoneyPileTurn(seat, yaw);
            }
        }

        /// <summary>The seats' money hatches on the lawn, in seat order.</summary>
        public IReadOnlyList<MoneyHatch> MoneyHatches => _hatches;

        /// <summary>A seat's money safe, or null.</summary>
        public MoneyPile MoneyPileOf(int seat) => seat >= 0 && seat < _moneyPiles.Count ? _moneyPiles[seat] : null;

        /// <summary>A seat's money hatch, or null.</summary>
        public MoneyHatch HatchOf(int seat) => seat >= 0 && seat < _hatches.Count ? _hatches[seat] : null;

        /// <summary>
        /// How far a hatch's middle lies in from the lawn's edge, and how far either side of the edge's middle when two seats
        /// share the edge: the top ones stay clear of the clock sign, the bottom ones of the dice and the pot. Far enough in
        /// that the whole frame stays on the flat grass wherever the lawn's wandering edge comes in (<see cref="LawnShape.Inward"/>).
        /// </summary>
        public const float HatchInset = 0.80f;
        public const float HatchSpread = 1.25f;

        /// <summary>
        /// Where a seat's money hatch lies on the lawn, in the board's frame at the lawn's foot (the owner's wish: in the green
        /// middle, on the player's side): near the middle of the lawn's edge that faces the seat's case, beside it the way its
        /// case lies when two seats share the edge.
        /// </summary>
        public static Vector3 HatchSpot(int seat, int seats, float lawnHalf, float yaw) =>
            MoneyEdgeOutward(seat, yaw) * (lawnHalf - HatchInset) + MoneyEdgeAlong(seat, seats, yaw) * (HatchSpread / MoneyPileSpread);

        /// <summary>A town's price line swells once, as its rent changes after a trade agreement's fireworks (Faz 27).</summary>
        public void PopPrice(int squareIndex)
        {
            TextMesh price = _prices != null && squareIndex >= 0 && squareIndex < _prices.Length ? _prices[squareIndex] : null;
            if (price != null && _animating.Add(price.transform)) StartCoroutine(Nudge(price.transform, false));
        }

        /// <summary>How far up the screen from the lawn's middle the game clock stands, as a share of the lawn's inner width: near the top corner of the green (Faz 28).</summary>
        public const float ClockOffset = 0.52f;

        /// <summary>
        /// Where the roll's total shows over the lawn, in world space (Faz 28): where the lawn's dice figures used to lie,
        /// just above the settled dice, so it never covers them.
        /// </summary>
        public Vector3 RollTotalSpot => transform.TransformPoint(new Vector3(0f, LawnHeight, 0f) + ScreenUp(_yaw) * (_inner * ScoreOffset));

        /// <summary>The camera turn the board's writing last faced (<see cref="FaceCamera"/>).</summary>
        public float Yaw => _yaw;

        /// <summary>The lawn's inner width, which the pieces on it are sized by.</summary>
        public float Inner => _inner;

        /// <summary>Where the game clock stands on the lawn, in the board's frame, for a camera turned by <paramref name="yaw"/>.</summary>
        public Vector3 ClockSpot(float yaw) => new Vector3(0f, LawnHeight, 0f) + ScreenUp(yaw) * (_inner * ClockOffset);

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
            // Sharp-edged stone (the owner's wish, 2026-11-11), its sides shaded in the picture (PlateSkin) and matt.
            GameObject plate = ModelParts.Create("Plate", transform, MeshFactory.Slab(new Vector3(width, PlateThickness, width), PlateSkin.Repeat), Color.white, 0.1f);
            ModelParts.Skin(plate, PlateSkin.Side());
            plate.transform.localPosition = new Vector3(0f, -PlateThickness / 2f, 0f);

            _inner = 2f * Layout.HalfSize - Layout.Depth * 1.05f;
            // The middle of the board is a lawn that carries the dice score and the pot; no logo (the owner's wish, 2026-11-10).
            CreateLawn(_inner);
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
            _chanceCards = new Transform[board.Count];
            _documents = new Transform[board.Count];
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
                Color body = printed ? Color.white : TileBody(square, data);
                GameObject tileObject = ModelParts.Create($"Square {i:00} {square.Name}", transform,
                    MeshFactory.RoundedBox(new Vector3(size.x, TileHeight, size.y), TileEdgeRadius, 2), body, 0.05f);
                ModelParts.Skin(tileObject, town ? TownSkin(outward, size, BandColor(TileColor(square, data)))
                    : square.IsResort ? TileSkin.Factory(FactoryStone, size.y / size.x) : paper);
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
                        CreateBandMark(mark, line + along * (size.x * 0.4f), outward, InkOf(square));
                    }
                }
                else if (corner && square.Type == SquareType.Start)
                {
                    _startArrow = CreateStartArrow(i, center, outward, size.y);
                }
                else if (corner && CornerParts.Has(square.Type))
                {
                    CreateScene(square.Type, i, center, outward, size.y);
                }
                else if (DocumentUnderName(i))
                {
                    float fit = Mathf.Min(size.x * NameFill / FloorPrint.DocumentSize.x, _depth * DocumentDepth / FloorPrint.DocumentSize.y);
                    GameObject document = FloorPrint.Document("Tax document", transform, fit, InkColor);
                    document.transform.localPosition = center + down * (_depth * DocumentOffset) + Vector3.up * (TileHeight + 0.001f);
                    document.transform.localRotation = PrintRotation(outward, _yaw);
                    _documents[i] = document.transform;
                }
                else if (IsChance(i))
                {
                    // Two fanned cards printed flat, and the big "?" on the front one.
                    float fit = Mathf.Min(size.x * NameFill / ChancePrint.Area.width, _depth * ChanceMarkDepth / ChancePrint.Area.height);
                    Vector3 mark = center + down * (_depth * ChanceMarkOffset);
                    GameObject cards = ChancePrint.Create("Chance cards", transform, fit, FloorPrint.PrintedPaper, ChanceDeep, ChanceGold);
                    cards.transform.localPosition = mark + Vector3.up * (TileHeight + 0.001f);
                    cards.transform.localRotation = PrintRotation(outward, _yaw);
                    _chanceCards[i] = cards.transform;
                    _chanceMarks[i] = CreateLabel("Chance mark", "?", mark + Vector3.up * top, outward,
                        _width * ChanceMarkSize, ChancePrint.MarkRoom * fit, ChanceDeep);
                }
                else
                {
                    CreateIcon(square.Type, center, outward);
                }

                // Price at the bottom, name above it (on its strip on a town), buildings or the sign on the top part.
                // A corner has no price: its name lies on the outer part, or the inner edge when the corner faces away,
                // clear of its scene and pawns.
                Vector3 namePlace = corner
                    ? DiagonalName(i) ? center - DiagonalTops(BoardCameraRig.BaseYaw) * (_depth * CupNameShift)
                    : square.Type == SquareType.Jail ? center + ScreenLowerRight * (size.y * JailNameShift)
                    : center + outward * ((FacesAway(outward) ? -1f : 1f) * _depth * CornerNameOffset)
                    : center + down * (_depth * (IsChance(i) || DocumentUnderName(i) ? ChanceNameOffset : NameOffset));
                _names[i] = CreateLabel("Name", PrintedName(square, corner), namePlace + Vector3.up * top, outward,
                    NameCharacterSize(i), NameRoom(i, size), InkOf(square));
                _names[i].transform.localRotation = NameRotation(i, _names[i].transform.localRotation, _yaw);
                string subtitle = Subtitle(square);
                if (subtitle != null || property)
                {
                    Vector3 pricePlace = corner
                        ? center + outward * ((FacesAway(outward) ? -1f : 1f) * _depth * CornerPriceOffset)
                        : center + down * (_depth * PriceOffset);
                    _prices[i] = CreateLabel("Price", subtitle ?? string.Empty, pricePlace + Vector3.up * (TileHeight + PrintHeight), outward,
                        PriceCharacterSize(i), PriceRoom(i, size), PriceInkOf(square));
                }

                if (property)
                {
                    // Owner marker: the owner's plot (or the flag) on the tile's top part, where the buildings will stand.
                    // On a factory city it is the owner's factory, which grows with the factory cities they hold (SetResortStage).
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

        /// <summary>
        /// The start square's arrow (the owner had its pier and lighthouse taken away, Faz 24): printed flat in red over the
        /// half of the corner away from its name, pointing the way the pawns go, towards the next square. It is shorter when
        /// it runs towards the name, so it never reaches it.
        /// </summary>
        private Transform CreateStartArrow(int index, Vector3 center, Vector3 outward, float side)
        {
            Vector3 towardsName = outward * (FacesAway(outward) ? -1f : 1f);
            Vector3 way = Layout.SquareCenter(index + 1) - Layout.SquareCenter(index);
            way.y = 0f;
            way.Normalize();
            bool alongName = Mathf.Abs(Vector3.Dot(way, towardsName)) > 0.5f;
            float scale = side * (alongName ? StartArrowShortLength : StartArrowLength) / FloorPrint.ArrowSize.y;
            GameObject arrow = FloorPrint.Arrow("Start arrow", transform, scale, FloorPrint.ArrowRed);
            arrow.transform.localPosition = center - towardsName * (side * StartArrowOffset) + Vector3.up * (TileHeight + 0.001f);
            arrow.transform.localRotation = Quaternion.LookRotation(way, Vector3.up);
            return arrow.transform;
        }

        /// <summary>The start square's arrow, pointing the way the pawns go; null on a board without corners.</summary>
        public Transform StartArrow => _startArrow;

        /// <summary>How big the cup beside a town's buildings is, and how far from the tile's middle it stands along the edge and towards the camera, in tile widths and depths.</summary>
        public const float CupSize = 0.28f;
        public const float CupSide = 0.37f;
        public const float CupForward = 0.08f;

        /// <summary>The small cup on the town that holds it; hidden while no town does.</summary>
        public Transform CupTrophy => _cupTrophy;

        /// <summary>The town the board shows the cup on; -1 while none.</summary>
        public int CupShownOn => _cupSquare;

        /// <summary>
        /// Shows the champions' cup on a town (the owner's wish, Faz 24): a small gold cup beside its buildings, on the screen's
        /// right of them, its handles across the screen. Only one town holds it; it leaves the town before. A square that is no
        /// town (or -1) hides it. With <paramref name="animate"/> it rises up on its new town.
        /// </summary>
        public void SetCup(int squareIndex, bool animate = false)
        {
            if (_data == null) return;
            bool town = squareIndex >= 0 && squareIndex < _data.Board.Count && _data.Board[squareIndex].Type == SquareType.Property && !_data.Board[squareIndex].IsResort;
            if (!town)
            {
                _cupSquare = -1;
                if (_cupTrophy != null) _cupTrophy.gameObject.SetActive(false);
                return;
            }

            if (squareIndex == _cupSquare) return;
            if (_cupTrophy == null)
            {
                _cupTrophy = (CornerParts.ChampionsCup("Cup on town", transform) ?? CornerParts.Trophy("Cup on town", transform)).transform;
                _cupScale = Vector3.one * (_width * CupSize);
            }

            _cupSquare = squareIndex;
            _cupTrophy.localScale = _cupScale;
            _cupTrophy.gameObject.SetActive(true);
            PlaceCup();
            if (animate) StartCoroutine(Raise(_cupTrophy, _cupScale));
        }

        /// <summary>Puts the cup beside its town's buildings for the camera's turn now: on the screen's right of them, a little towards the camera.</summary>
        private void PlaceCup()
        {
            if (_cupTrophy == null || _cupSquare < 0) return;
            Vector3 center = Layout.SquareCenter(_cupSquare);
            Vector3 outward = Outward(center);
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            if (Vector3.Dot(along, ScreenRight(_yaw)) < 0f) along = -along;
            float width = Layout.SquareSize(_cupSquare).x - Gap;
            _cupTrophy.localPosition = BuildingRowCenter(center) + along * (width * CupSide) + ScreenDown(outward) * (_depth * CupForward);
            _cupTrophy.localRotation = FacingCamera(_yaw);
        }

        /// <summary>How big the card marks beside a town's buildings are, and how far to the screen's left of them they stand (Faz 30).</summary>
        public const float CardMarkSize = 0.22f;
        public const float CardMarkSide = 0.37f;

        /// <summary>The protection card's shield, blue as the kit's water; the rent strike's picket sign, red.</summary>
        public static readonly Color ShieldColor = new Color(0.22f, 0.5f, 0.9f);
        public static readonly Color ShieldRimColor = new Color(0.98f, 0.86f, 0.35f);
        public static readonly Color StrikeColor = new Color(0.86f, 0.22f, 0.2f);
        public static readonly Color StrikePoleColor = new Color(0.55f, 0.38f, 0.22f);

        private readonly Dictionary<int, Transform> _shieldMarks = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _strikeMarks = new Dictionary<int, Transform>();

        /// <summary>Whether the board shows the protection card's shield on a town.</summary>
        public bool ShowsShield(int squareIndex) => _shieldMarks.TryGetValue(squareIndex, out Transform mark) && mark != null && mark.gameObject.activeSelf;

        /// <summary>Whether the board shows the rent strike's sign on a town.</summary>
        public bool ShowsStrike(int squareIndex) => _strikeMarks.TryGetValue(squareIndex, out Transform mark) && mark != null && mark.gameObject.activeSelf;

        /// <summary>
        /// A protection card's shield on a town (Faz 30): a blue shield with a gold rim beside its buildings, on the screen's
        /// left of them (the cup stands on the right). With <paramref name="animate"/> it rises, or, when it goes, shrinks away.
        /// </summary>
        public void SetShield(int squareIndex, bool raised, bool animate = false) =>
            SetCardMark(_shieldMarks, squareIndex, raised, animate, CreateShieldMark);

        /// <summary>A rent strike on a town (Faz 30): a red picket sign beside its buildings, in front of a shield if both stand.</summary>
        public void SetStrike(int squareIndex, bool struck, bool animate = false) =>
            SetCardMark(_strikeMarks, squareIndex, struck, animate, CreateStrikeMark);

        private void SetCardMark(Dictionary<int, Transform> marks, int squareIndex, bool shown, bool animate, System.Func<Transform> create)
        {
            if (_data == null || squareIndex < 0 || squareIndex >= _data.Board.Count) return;
            marks.TryGetValue(squareIndex, out Transform mark);
            if (!shown)
            {
                if (mark == null || !mark.gameObject.activeSelf) return;
                if (animate) StartCoroutine(Shrink(mark));
                else mark.gameObject.SetActive(false);
                return;
            }

            bool created = mark == null;
            if (created)
            {
                mark = create();
                marks[squareIndex] = mark;
            }

            // A mark already standing stays as it is; a new one is sized and placed first (it is made active).
            if (!created && mark.gameObject.activeSelf && !animate) return;
            mark.gameObject.SetActive(true);
            mark.localScale = Vector3.one * (_width * CardMarkSize);
            PlaceCardMarks();
            if (animate) StartCoroutine(Raise(mark, mark.localScale));
        }

        private Transform CreateShieldMark()
        {
            var root = new GameObject("Shield mark").transform;
            root.SetParent(transform, false);
            GameObject rim = ModelParts.Create("Rim", root, MeshFactory.Shield(0.12f), ShieldRimColor, 0.5f);
            rim.transform.localScale = new Vector3(1.12f, 1.1f, 1f);
            rim.transform.localPosition = new Vector3(0f, -0.06f, 0f);
            GameObject face = ModelParts.Create("Face", root, MeshFactory.Shield(0.2f), ShieldColor, 0.5f);
            face.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            return root;
        }

        private Transform CreateStrikeMark()
        {
            var root = new GameObject("Strike mark").transform;
            root.SetParent(transform, false);
            GameObject pole = ModelParts.Create("Pole", root, MeshFactory.Cylinder(12), StrikePoleColor);
            pole.transform.localScale = new Vector3(0.06f, 1.1f, 0.06f);
            GameObject board = ModelParts.Create("Sign", root, MeshFactory.RoundedBox(new Vector3(1f, 0.62f, 0.08f), 0.03f), StrikeColor, 0.35f);
            board.transform.localPosition = new Vector3(0f, 1.05f, -0.05f);
            GameObject stripe = ModelParts.Create("Stripe", root, MeshFactory.RoundedBox(new Vector3(0.8f, 0.12f, 0.1f), 0.02f), PaperColor, 0.35f);
            stripe.transform.localPosition = new Vector3(0f, 1.05f, -0.06f);
            return root;
        }

        /// <summary>Puts the card marks beside their towns' buildings for the camera's turn now, on the screen's left of them.</summary>
        private void PlaceCardMarks()
        {
            PlaceCardMarks(_shieldMarks, 0f);
            PlaceCardMarks(_strikeMarks, CupForward * 2.5f);
        }

        private void PlaceCardMarks(Dictionary<int, Transform> marks, float forward)
        {
            foreach (KeyValuePair<int, Transform> pair in marks)
            {
                if (pair.Value == null || !pair.Value.gameObject.activeSelf) continue;
                Vector3 center = Layout.SquareCenter(pair.Key);
                Vector3 outward = Outward(center);
                Vector3 along = Vector3.Cross(Vector3.up, outward);
                if (Vector3.Dot(along, ScreenRight(_yaw)) > 0f) along = -along;
                float width = Layout.SquareSize(pair.Key).x - Gap;
                pair.Value.localPosition = BuildingRowCenter(center) + along * (width * CardMarkSide) + ScreenDown(outward) * (_depth * (CupForward + forward));
                pair.Value.localRotation = FacingCamera(_yaw);
            }
        }

        private static IEnumerator Shrink(Transform mark)
        {
            const float seconds = 0.45f;
            Vector3 size = mark.localScale;
            for (float t = 0f; t < 1f; t += Time.deltaTime / seconds)
            {
                if (mark == null) yield break;
                mark.localScale = size * (1f - Ease.InCubic(t));
                yield return null;
            }

            if (mark == null) yield break;
            mark.gameObject.SetActive(false);
            mark.localScale = size;
        }

        /// <summary>A model's turn that shows its front (+Z) straight to a camera at <paramref name="yaw"/>.</summary>
        public static Quaternion FacingCamera(float yaw) => Quaternion.LookRotation(-ScreenUp(yaw));

        /// <summary>The scene on the cup's corner (the owner's stadium); null on a board without one.</summary>
        public Transform CupScene => _cupScene;

        /// <summary>A corner's scene stands on one side of the corner with the pawns on the other, both on the part away from the name.</summary>
        private void CreateScene(SquareType type, int index, Vector3 center, Vector3 outward, float side)
        {
            GameObject scene = CornerParts.For(type, $"Scene {type}", transform);
            if (scene == null) return;
            bool far = FacesAway(outward);
            bool cup = type == SquareType.Cup;
            scene.transform.localPosition = center + SceneSide(index, far) * (side * SceneOffset)
                + outward * (side * (cup ? CupSceneInset : SceneInset) * (far ? 1f : -1f)) + Vector3.up * TileHeight;
            scene.transform.localScale = Vector3.one * (side * (cup ? CupSceneSize : SceneSize));
            if (type == SquareType.Jail)
            {
                // The cell stands towards the tile's left tip (the owner's wish, Faz 28).
                scene.transform.localPosition = center + ScreenLeftTip * (side * JailSceneShift) + Vector3.up * TileHeight;
            }

            if (!CornerParts.ShowsFront(scene))
            {
                scene.transform.localRotation = Quaternion.LookRotation(outward);
                return;
            }

            if (cup)
            {
                // The owner's stadium: up the screen behind the name, its stand turned straight to the camera whichever way the table is turned.
                scene.transform.localPosition = center + DiagonalTops(BoardCameraRig.BaseYaw) * (side * StadiumShift) + Vector3.up * TileHeight;
                scene.transform.localScale = Vector3.one * (side * StadiumSize);
                _cupScene = scene.transform;
                _cupScene.localRotation = FacingCamera(_yaw);
                return;
            }

            // The diorama: twice the drawn stop, over the corner's back part; its front (the bus) turns to the camera
            // like a building's, so it never shows its back.
            _stationCorner = index;
            scene.transform.localPosition = center + SceneSide(index, far) * (side * StationOffset) + outward * (side * StationInset * (far ? 1f : -1f)) + Vector3.up * TileHeight;
            scene.transform.localScale = Vector3.one * (side * StationSize);
            scene.transform.localRotation = Quaternion.LookRotation(BuildingFront(outward, _yaw));
            _frontScenes.Add(new KeyValuePair<Transform, Vector3>(scene.transform, outward));
            // The stop's bus is a model of its own, parked where the diorama showed it, so it can drive off with a player.
            _station = scene.transform;
            _bus = BusView.Create(transform, _station);
        }

        /// <summary>The lawn in the middle: a gravel ring where the squares end and plain grass (the owner had the trees, bunting and paths taken away).</summary>
        private void CreateLawn(float inner)
        {
            var lawn = new GameObject("Lawn");
            lawn.transform.SetParent(transform, false);
            Transform root = lawn.transform;

            // Sandy ground with an asphalt lane and its dashed middle line round the lawn (Faz 25, the owner's wish; since
            // 2026-11-10 the lane's edges are mixed with sand and the line bends round the corners with it).
            float grass = inner * LawnShare;
            GameObject road = ModelParts.Create("Road", root, MeshFactory.RoundedBox(new Vector3(inner, RoadHeight, inner), RoadHeight / 2f, 2), Color.white, 0.15f);
            float strip = (1f - LawnShare) / 2f;
            ModelParts.Skin(road, RoadSkin.Create(strip));
            road.transform.localPosition = new Vector3(0f, RoadHeight / 2f, 0f);
            _lane = new RoadPath(RoadSkin.Reach(strip, RoadSkin.LaneMiddle) * inner / 2f, RoadSkin.Bend(strip, RoadSkin.LaneMiddle) * inner / 2f);

            // A natural, matt meadow whose edge wanders instead of running dead straight (the owner's wish, 2026-11-10; it
            // was mowed diamonds inside a cream border on a straight-edged box).
            GameObject green = ModelParts.Create("Grass", root, LawnShape.Build(grass / 2f, LawnHeight), Color.white, 0f);
            ModelParts.Skin(green, LawnSkin.Texture());
            // The mesh stands on the board's floor and its top is the lawn's height, where the hatches, the clock and the pot stand.
            green.transform.localPosition = Vector3.zero;
        }

        /// <summary>
        /// The pot's line on the lawn, which <see cref="FaceCamera"/> places and turns; a board without a pot prints nothing there.
        /// The lawn no longer prints the dice (the owner's wish, Faz 28): the roll's total shows on the screen instead.
        /// </summary>
        private void CreateScoreboard(float inner, bool hasPot)
        {
            if (!hasPot) return;
            var board = new GameObject("Scoreboard");
            board.transform.SetParent(transform, false);
            _scoreboard = board.transform;
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

        /// <summary>The ink of a square's name: light on a factory city's dark paving, the board's dark ink elsewhere.</summary>
        public static Color InkOf(SquareDefinition square) => square.IsResort ? FactoryInk : InkColor;

        private static Color PriceInkOf(SquareDefinition square) => square.IsResort ? FactoryInk : PriceColor;

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
            _data.Board[index].Type == SquareType.Jail ? JailNameScale
            : IsChance(index) ? ChanceNameScale
            : _data.Board[index].Type == SquareType.Tax ? TaxNameScale
            : _data.Board[index].Type == SquareType.Cup ? CupNameScale : 1f;

        /// <summary>The cup's corner: its name runs across the corner, along the diagonal (<see cref="DiagonalRotation"/>).</summary>
        private bool DiagonalName(int index) => _data.Board[index].Type == SquareType.Cup && Layout.IsCorner(index);

        /// <summary>Of a square tile's two diagonals, the one that lies most nearly left to right on the screen for a camera at <paramref name="yaw"/>, pointing right.</summary>
        public static Vector3 DiagonalLine(float yaw)
        {
            Vector3 right = ScreenRight(yaw);
            Vector3 first = new Vector3(1f, 0f, 1f).normalized;
            Vector3 second = new Vector3(1f, 0f, -1f).normalized;
            Vector3 line = Mathf.Abs(Vector3.Dot(first, right)) >= Mathf.Abs(Vector3.Dot(second, right)) ? first : second;
            return Vector3.Dot(line, right) < 0f ? -line : line;
        }

        /// <summary>Which way the tops of the letters of a diagonal name point: across its line, up the screen.</summary>
        public static Vector3 DiagonalTops(float yaw)
        {
            Vector3 line = DiagonalLine(yaw);
            return new Vector3(-line.z, 0f, line.x);
        }

        /// <summary>
        /// How a square's name lies, from <paramref name="lying"/> (its <see cref="LabelRotation"/>): the cup's across its
        /// corner, the jail's turned a quarter to the left on the screen (the owner's wish, Faz 28), so it reads from lower
        /// left to upper right along the tile's lower right edge; the others as they lie.
        /// </summary>
        private Quaternion NameRotation(int index, Quaternion lying, float yaw) =>
            DiagonalName(index) ? DiagonalRotation(yaw) : JailName(index) ? TurnedLeft(lying) : lying;

        /// <summary>The jail corner's name, which is turned (<see cref="TurnedLeft"/>).</summary>
        private bool JailName(int index) => Layout.IsCorner(index) && _data.Board[index].Type == SquareType.Jail;

        /// <summary>A label lying flat on the board turned a quarter to the left as the camera sees it (counter-clockwise from above).</summary>
        public static Quaternion TurnedLeft(Quaternion lying) => Quaternion.AngleAxis(-90f, Vector3.up) * lying;

        /// <summary>Text lying on the board along <see cref="DiagonalLine"/>, reading left to right.</summary>
        public static Quaternion DiagonalRotation(float yaw) => Quaternion.LookRotation(Vector3.down, DiagonalTops(yaw));

        /// <summary>A Şans square: a card square along an edge (a card corner has its kiosk instead).</summary>
        private bool IsChance(int index) => _data.Board[index].Type == SquareType.Card && !Layout.IsCorner(index);

        /// <summary>A tax taken as a share of property value: it shows no figure but a document, its name low on the tile.</summary>
        private bool DocumentUnderName(int index) => _data.Board[index].Type == SquareType.Tax && Subtitle(_data.Board[index]) == null;

        /// <summary>The document printed on a tax square, or null on any other square.</summary>
        public Transform Document(int index) => _documents != null ? _documents[index] : null;

        /// <summary>A flat print's turn: its +z (the picture's up) along the tops of the letters of a label at the same place.</summary>
        private static Quaternion PrintRotation(Vector3 outward, float yaw) => Quaternion.LookRotation(LabelRotation(outward, yaw) * Vector3.up, Vector3.up);

        /// <summary>The flat "?" printed on a Şans square, or null on any other square.</summary>
        public TextMesh ChanceMark(int index) => _chanceMarks != null ? _chanceMarks[index] : null;

        /// <summary>The cards printed on a Şans square (<see cref="ChancePrint"/>); null on any other square.</summary>
        public Transform ChanceCards(int index) => _chanceCards != null ? _chanceCards[index] : null;

        private float NameCharacterSize(int index) => _width * NameSize * NameScale(index);

        private Vector2 NameRoom(int index, Vector2 size)
        {
            if (DiagonalName(index)) return new Vector2(_depth * CupNameLength, _depth * CupNameDepth);
            if (Layout.IsCorner(index) && _data.Board[index].Type == SquareType.Jail) return new Vector2(size.x * JailNameFill, _depth * CornerNameDepth * NameScale(index));
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
            PlaceCup();
            PlaceCardMarks();
            if (_cupScene != null) _cupScene.localRotation = FacingCamera(yaw);
            foreach (KeyValuePair<Transform, Vector3> scene in _frontScenes)
            {
                if (scene.Key != null) scene.Key.localRotation = Quaternion.LookRotation(BuildingFront(scene.Value, yaw));
            }

            // The stop turned: the waiting bus turns with it (one on the road finds the new place when it gets home).
            if (_bus != null && !_bus.Away) _bus.Park();

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

        /// <summary>The line under a square's name: the tax it takes; null when the square has none (a town for sale shows no figure, the bus station no fare).</summary>
        public static string Subtitle(SquareDefinition square)
        {
            switch (square.Type)
            {
                case SquareType.Property: return null; // For sale a town shows no figure; owned, its rent (the owner's wish, Faz 22).
                case SquareType.Tax: return square.TaxPercent > 0 ? null : F($"-{square.TaxAmount}"); // A share prints a document instead (Faz 24).
                case SquareType.Travel: return null; // The fare is no longer printed under the bus station (the owner's wish, Faz 28).
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
            price.color = PriceInkOf(_data.Board[index]); // The rent stays in the print's ink, never the owner's colour (the owner's wish, Faz 21).
            Vector2 size = Layout.SquareSize(index) - Vector2.one * Gap;
            FitLabel(price, PriceCharacterSize(index), PriceRoom(index, size), Outward(Layout.SquareCenter(index)));
        }

        /// <summary>
        /// A square's name as printed on the board: a town's, the jail's, a tax's, the cup's, a Şans square's (a card square
        /// off the corners), the start's and the bus station's in capitals ("İSTANBUL", "HAPİS", "VERGİ", "ŞANS", "ŞAMPİYONLAR
        /// KUPASI", "BAŞLANGIÇ", "OTOGAR", Turkish capitals in Turkish; the last two the owner's wish, Faz 28), the others as written.
        /// </summary>
        /// The cup's corner breaks its name onto two lines (<see cref="TwoLines"/>).
        public static string PrintedName(SquareDefinition square, bool corner)
        {
            if (square.Type == SquareType.Cup) return corner ? TwoLines(Upper(L(square.Name))) : Upper(L(square.Name));
            return square.Type == SquareType.Property || square.Type == SquareType.Jail || square.Type == SquareType.Tax || (square.Type == SquareType.Card && !corner)
                || square.Type == SquareType.Start || square.Type == SquareType.Travel
                ? Upper(L(square.Name)) : L(square.Name);
        }

        /// <summary>A name broken onto two lines at its first space; a one-word name stays on one.</summary>
        public static string TwoLines(string name)
        {
            int space = name.IndexOf(' ');
            return space < 0 ? name : name.Substring(0, space) + "\n" + name.Substring(space + 1);
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
                    _names[i].text = PrintedName(square, Layout.IsCorner(i));
                    // A diagonal name is measured lying square to its edge, then turned back across the corner.
                    // A diagonal or turned name is measured lying square to its edge, then turned back.
                    _names[i].transform.localRotation = LabelRotation(outward, _yaw);
                    FitLabel(_names[i], NameCharacterSize(i), NameRoom(i, size), outward);
                    _names[i].transform.localRotation = NameRotation(i, LabelRotation(outward, _yaw), _yaw);
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

        /// <summary>A plain tile's colour on the paper: its own colour washed to a pastel, but a Şans square's jade as it is.</summary>
        public static Color TileBody(SquareDefinition square, BoardData data) =>
            square.Type == SquareType.Card ? ChanceColor : Color.Lerp(TileColor(square, data), PaperColor, Pastel);

        private static Color TileColor(SquareDefinition square, BoardData data)
        {
            switch (square.Type)
            {
                case SquareType.Start: return StartColor;
                case SquareType.Tax: return TaxColor;
                case SquareType.Property: return square.IsResort ? FactoryStone : data.GroupColor(square.GroupId);
                case SquareType.Card: return ChanceColor;
                case SquareType.Cup: return CupColor;
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
