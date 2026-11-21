using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The scene behind the main menu (the owner's wish, 2026-11-21: a menu that suits the game's own air): the real
    /// table floating in its sky, a game under way on it (towns held, houses and hotels up, four pieces standing
    /// about, the safes full), seen from the game's own corner and framed in the part of the screen the menu's rail
    /// leaves free. The clouds, the far islands and the plane move as they do at the table; the table itself rises and
    /// sinks a little and the view breathes very slowly. Purely for show: nothing here answers the pointer.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        /// <summary>The players of the game that is shown.</summary>
        public const int Seats = 4;

        /// <summary>The safes on their islands stand outside the plate, and the menu's small buttons take the corners: the table is framed looser than in the game.</summary>
        public const float Loosen = 1.2f;

        /// <summary>How much larger and smaller than the framed view the breathing goes, and the seconds of one breath.</summary>
        public const float Breath = 0.02f;
        public const float BreathSeconds = 18f;

        /// <summary>How far the table rises and sinks on the screen (world units), and the seconds of one rise and fall.</summary>
        public const float Bob = 0.1f;
        public const float BobSeconds = 8f;

        /// <summary>Where the four pieces stand.</summary>
        private static readonly int[] PawnSquares = { 2, 10, 19, 27 };

        /// <summary>The money in the four safes, as shares of what a game starts with.</summary>
        private static readonly float[] MoneyShare = { 1.2f, 0.7f, 1.6f, 0.5f };

        private Camera _camera;
        private BoardView _board;

        public static MenuBackdrop Create(Transform parent, BoardData data)
        {
            var root = new GameObject("Menu Backdrop");
            root.transform.SetParent(parent, false);
            var backdrop = root.AddComponent<MenuBackdrop>();
            backdrop.Build(data);
            return backdrop;
        }

        /// <summary>The share of the screen's width the rail takes; the controller keeps it up to date with the canvas.</summary>
        public float RailShare { get; set; } = MenuLayout.RailEdge(MenuLayout.Design.y / 2f) / MenuLayout.Design.x;

        /// <summary>Where across the screen (0 the left, 1 the right) the middle of the part the rail leaves free is.</summary>
        public static float FreeMiddle(float railShare) => (Mathf.Clamp01(railShare) + 1f) / 2f;

        /// <summary>The view's size that shows the whole table in the part of the screen the rail leaves free.</summary>
        public static float FramedSize(float halfExtent, float screenWidth, float screenHeight, float railShare) =>
            BoardCameraFraming.OrthographicSize(halfExtent, BoardView.PlateThickness, BoardCameraRig.Pitch,
                Mathf.Max(1f, screenWidth * (1f - Mathf.Clamp(railShare, 0f, 0.8f))), screenHeight, 0f) * Loosen;

        /// <summary>How far to the left of the table's middle the camera looks (world units), so the table shows at <paramref name="middle"/> across the screen.</summary>
        public static float Aside(float middle, float orthographicSize, float aspect) => (middle - 0.5f) * 2f * orthographicSize * aspect;

        /// <summary>The view's size at a moment of the breath.</summary>
        public static float BreathingSize(float framedSize, float seconds) =>
            framedSize * (1f + Breath * Mathf.Sin(seconds / BreathSeconds * 2f * Mathf.PI));

        /// <summary>How far above its place the table shows at a moment.</summary>
        public static float BobAt(float seconds) => Bob * Mathf.Sin(seconds / BobSeconds * 2f * Mathf.PI);

        /// <summary>
        /// The game that is shown: which seat holds a town and what stands on it, by the town's place among the
        /// board's towns. Most are held, a few are still for sale (seat -1); bare plots, houses and hotels all show.
        /// </summary>
        public static (int Seat, int Level) Holding(int town)
        {
            if (town % 7 == 5) return (-1, 0);
            return (town % Seats, (town * 3 + 1) % (SquareDefinition.MaxBuildingLevel + 1));
        }

        private void Build(BoardData data)
        {
            _board = BoardView.Create(transform, data);
            GraphicsQuality quality = RoomDressing.CurrentQuality();

            var colors = new List<Color>();
            for (int seat = 0; seat < Seats; seat++) colors.Add(PlayerPalette.ForSeat(seat));
            _board.CreateMoneyPiles(colors);
            for (int seat = 0; seat < Seats; seat++) _board.ShowSeatMoney(seat, Mathf.RoundToInt(data.Rules.StartingMoney * MoneyShare[seat]));
            Dress(data.Board, colors);

            int squares = _board.Layout.SquareCount;
            var crowd = new PawnCrowd(Seats);
            for (int seat = 0; seat < Seats; seat++)
            {
                PawnView.Create(transform, _board, seat, colors[seat], crowd, seat + 1, CharacterChoice.ForSeat(seat).Id).PlaceOn(PawnSquares[seat] % squares);
            }

            _camera = Camera.main;
            if (_camera == null) _camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;
            _camera.cullingMask = ~0;
            RoomDressing.DressCamera(_camera, quality);
            RoomDressing.LightScene(quality);
            RoomDressing.ApplyQuality(quality);
            Place();
        }

        /// <summary>Hands out the towns and the factories, and puts up what stands on them.</summary>
        private void Dress(BoardDefinition board, IReadOnlyList<Color> colors)
        {
            int town = 0, works = 0;
            for (int i = 0; i < board.Count; i++)
            {
                SquareDefinition square = board[i];
                if (square.Type != SquareType.Property) continue;
                if (square.IsResort)
                {
                    // The four factories: two with the second seat, one with the third, one for sale.
                    int holder = works < 2 ? 1 : works == 2 ? 2 : -1;
                    works++;
                    if (holder < 0) continue;
                    _board.SetOwner(i, colors[holder]);
                    _board.SetResortStage(i, holder == 1 ? 2 : 1);
                    continue;
                }

                if (!square.IsDevelopable) continue;
                (int seat, int level) = Holding(town++);
                if (seat < 0) continue;
                _board.SetOwner(i, colors[seat]);
                _board.SetBuildingLevel(i, level);
            }
        }

        private void LateUpdate() => Place();

        private void Place()
        {
            if (_camera == null || _board == null || Screen.width <= 0 || Screen.height <= 0) return;
            float time = Time.unscaledTime;
            float framed = FramedSize(_board.HalfExtent, Screen.width, Screen.height, RailShare);
            float size = BreathingSize(framed, time);
            Quaternion look = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
            // The camera looks to the left of the table and a little above or below it, so the table shows right of the rail, rising and sinking.
            Vector3 aim = BoardCameraFraming.Aim(_board.transform.position, BoardView.PlateThickness)
                - look * Vector3.right * Aside(FreeMiddle(RailShare), size, (float)Screen.width / Screen.height)
                - look * Vector3.up * BobAt(time);
            _camera.orthographicSize = size;
            _camera.transform.position = BoardCameraRig.Place(aim, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, BoardCameraRig.Distance);
            _camera.transform.rotation = look;
        }
    }
}
