using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The scene behind the main menu: the real board floating in the sky, seen from the game's own
    /// isometric corner with a few pawns standing about, and a camera that breathes in and out very
    /// slowly. Purely decorative; nothing here reacts to input.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        /// <summary>How much larger and smaller than the framed view the breathing goes.</summary>
        public const float Breath = 0.04f;

        /// <summary>Seconds for one breath in and out.</summary>
        public const float BreathSeconds = 14f;

        /// <summary>The menu's windows cover the middle, so the board is framed a little wider than in the game.</summary>
        private const float Loosen = 1.08f;
        private const int PawnCount = 4;

        private static readonly int[] PawnSquares = { 3, 11, 18, 27 };

        private Camera _camera;
        private BoardView _board;
        private SettingsService _settings;

        public static MenuBackdrop Create(Transform parent, BoardData data)
        {
            var root = new GameObject("Menu Backdrop");
            root.transform.SetParent(parent, false);
            var backdrop = root.AddComponent<MenuBackdrop>();
            backdrop.Build(data);
            return backdrop;
        }

        /// <summary>The view's size at a moment of the breath: the framed size swelling and shrinking by <see cref="Breath"/>.</summary>
        public static float BreathingSize(float framedSize, float seconds) =>
            framedSize * (1f + Breath * Mathf.Sin(seconds / BreathSeconds * 2f * Mathf.PI));

        private void Build(BoardData data)
        {
            _board = BoardView.Create(transform, data);
            GraphicsQuality quality = RoomDressing.CurrentQuality();
            RoomDressing.LightScene(quality);

            int squares = _board.Layout.SquareCount;
            var crowd = new PawnCrowd(PawnCount);
            for (int seat = 0; seat < PawnCount; seat++)
            {
                PawnView.Create(transform, _board, seat, PlayerPalette.ForSeat(seat), crowd, seat).PlaceOn(PawnSquares[seat] % squares);
            }

            _camera = Camera.main;
            if (_camera == null) _camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = 100f;
            RoomDressing.DressCamera(_camera, quality);
            Place();

            GameServices.Registry?.TryGet(out _settings);
            if (_settings != null) _settings.Changed += OnSettingsChanged;
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
        }

        private static void OnSettingsChanged(GameSettings settings) => RoomDressing.ApplyQuality(settings.Quality);

        private void Update() => Place();

        private void Place()
        {
            if (_camera == null) return;
            Vector3 aim = BoardCameraFraming.Aim(_board.transform.position, BoardView.PlateThickness);
            float framed = BoardCameraFraming.OrthographicSize(_board.HalfExtent, BoardView.PlateThickness, BoardCameraRig.Pitch,
                Screen.width, Screen.height, 0f) * Loosen;
            _camera.orthographicSize = BreathingSize(framed, Time.unscaledTime);
            _camera.transform.position = BoardCameraRig.Place(aim, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, BoardCameraRig.Distance);
            _camera.transform.rotation = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
        }
    }
}
