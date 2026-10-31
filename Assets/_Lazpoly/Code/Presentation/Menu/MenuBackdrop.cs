using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The scene behind the main menu: the real board on its desk, a few pawns standing about,
    /// and a camera that drifts slowly around it. Purely decorative; nothing here reacts to input.
    /// </summary>
    public sealed class MenuBackdrop : MonoBehaviour
    {
        /// <summary>Degrees the camera travels around the board per second.</summary>
        public const float YawSpeed = 3f;
        private const float Pitch = 42f;
        private const int PawnCount = 4;

        private static readonly int[] PawnSquares = { 3, 11, 18, 27 };

        private Camera _camera;
        private Vector3 _center;
        private float _distance;
        private float _yaw = 200f;
        private SettingsService _settings;

        public static MenuBackdrop Create(Transform parent, BoardData data)
        {
            var root = new GameObject("Menu Backdrop");
            root.transform.SetParent(parent, false);
            var backdrop = root.AddComponent<MenuBackdrop>();
            backdrop.Build(data);
            return backdrop;
        }

        private void Build(BoardData data)
        {
            BoardView board = BoardView.Create(transform, data);
            RoomDressing.CreateRoom(transform, board.HalfExtent);
            GraphicsQuality quality = RoomDressing.CurrentQuality();
            RoomDressing.LightScene(quality);

            int squares = board.Layout.SquareCount;
            for (int seat = 0; seat < PawnCount; seat++)
            {
                PawnView.Create(transform, board, seat, PawnCount, PlayerPalette.ForSeat(seat)).PlaceOn(PawnSquares[seat] % squares);
            }

            _center = board.transform.position;
            _distance = board.HalfExtent * 2.4f;

            _camera = Camera.main;
            if (_camera == null) _camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            _camera.fieldOfView = 45f;
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

        private void Update()
        {
            _yaw = (_yaw + YawSpeed * Time.deltaTime) % 360f;
            Place();
        }

        private void Place()
        {
            if (_camera == null) return;
            Quaternion look = Quaternion.Euler(Pitch, _yaw, 0f);
            _camera.transform.position = _center - look * Vector3.forward * _distance;
            _camera.transform.rotation = look;
        }
    }
}
