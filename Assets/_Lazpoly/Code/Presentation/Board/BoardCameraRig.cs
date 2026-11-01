using UnityEngine;
using UnityEngine.EventSystems;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>The two ready-made views of the table.</summary>
    public enum CameraView
    {
        /// <summary>Sitting at the table: the near edge large, the far edge small.</summary>
        Table = 0,
        /// <summary>Looking almost straight down, every square the same size.</summary>
        Overhead = 1,
    }

    /// <summary>
    /// Lets the player look around the board: the mouse wheel zooms, dragging with the right button
    /// turns the view around the board, and <see cref="ResetView"/> (or the Home key) returns to the
    /// framed view that <see cref="BoardCameraFraming"/> computes. A pinned square pulls the view a
    /// little towards itself (<see cref="Peek"/>); the view never leaves the board. The pointer over the interface is left alone.
    /// </summary>
    public sealed class BoardCameraRig : MonoBehaviour
    {
        public const float MinZoom = 0.45f;
        public const float MaxZoom = 1.15f;

        /// <summary>How far towards a pinned square the view slides: a fraction of the distance.</summary>
        public const float PeekAmount = 0.3f;
        private const float WheelStep = 0.1f;
        private const float DegreesPerMouseUnit = 5f;
        private const float Smoothing = 12f;

        private Vector3 _boardCenter;
        private Vector3 _aim;
        private Vector3 _offset;
        private float _pitch;
        private float _boardHalfExtent = float.PositiveInfinity;
        private bool _hasBase;
        private float _yaw;
        private float _zoom = 1f;
        private Vector3? _peek;

        public float Yaw => _yaw;
        public float Zoom => _zoom;

        /// <summary>Camera tilt below the horizon for each ready-made view.</summary>
        public static float PitchOf(CameraView view) => view == CameraView.Overhead ? 76f : 55f;

        /// <summary>The framed view: the camera position for yaw 0 and zoom 1, looking down at <paramref name="pitch"/> degrees.</summary>
        /// <param name="boardHalfExtent">Half the board's width; the view is never pulled past its edge.</param>
        public void SetBase(Vector3 boardCenter, Vector3 cameraPosition, float pitch, float boardHalfExtent = float.PositiveInfinity)
        {
            _boardCenter = boardCenter;
            _pitch = pitch;
            _boardHalfExtent = boardHalfExtent;
            Vector3 forward = Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
            // Where the view direction meets the board's plane.
            float t = (boardCenter.y - cameraPosition.y) / forward.y;
            _aim = cameraPosition + forward * t;
            _offset = cameraPosition - _aim;
            bool first = !_hasBase;
            _hasBase = true;
            if (first) Apply(snap: true);
        }

        public void ResetView()
        {
            _yaw = 0f;
            _zoom = 1f;
        }

        /// <summary>Turns and zooms the view to given values (the screenshot presets use it).</summary>
        public void SetView(float yaw, float zoom)
        {
            _yaw = yaw;
            _zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
        }

        /// <summary>Slides the view part of the way towards a point on the board (a pinned square); null lets it go.</summary>
        public void Peek(Vector3? worldPoint) => _peek = worldPoint;

        /// <summary>A point held inside the board's square, so the view cannot be pulled off the table.</summary>
        public static Vector3 ClampToBoard(Vector3 point, Vector3 boardCenter, float halfExtent)
        {
            if (float.IsInfinity(halfExtent)) return point;
            return new Vector3(
                boardCenter.x + Mathf.Clamp(point.x - boardCenter.x, -halfExtent, halfExtent), point.y,
                boardCenter.z + Mathf.Clamp(point.z - boardCenter.z, -halfExtent, halfExtent));
        }

        /// <summary>
        /// Where the view aims once the peek is applied: the framed aim slid towards the point. The aim
        /// lives in the un-turned frame that <see cref="Place"/> turns by the yaw, so the world point is turned back first.
        /// </summary>
        public static Vector3 PeekAim(Vector3 aim, Vector3? peek, Vector3 boardCenter, float halfExtent, float yaw)
        {
            if (!peek.HasValue) return aim;
            Vector3 target = ClampToBoard(peek.Value, boardCenter, halfExtent);
            Vector3 unturned = boardCenter + Quaternion.Euler(0f, -yaw, 0f) * (target - boardCenter);
            return Vector3.Lerp(aim, new Vector3(unturned.x, aim.y, unturned.z), PeekAmount);
        }

        /// <summary>Camera position for a view turned by <paramref name="yaw"/> degrees and scaled by <paramref name="zoom"/>.</summary>
        public static Vector3 Place(Vector3 boardCenter, Vector3 aim, Vector3 offset, float yaw, float zoom)
        {
            Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
            return boardCenter + turn * (aim - boardCenter) + turn * (offset * zoom);
        }

        private void LateUpdate()
        {
            if (!_hasBase) return;
            ReadInput();
            Apply(snap: false);
        }

        private void ReadInput()
        {
            if (Input.GetKeyDown(KeyCode.Home)) ResetView();

            bool overInterface = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!overInterface)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f) _zoom = Mathf.Clamp(_zoom * (1f - wheel * WheelStep), MinZoom, MaxZoom);
            }

            if (Input.GetMouseButton(1) && !overInterface) _yaw += Input.GetAxisRaw("Mouse X") * DegreesPerMouseUnit;
        }

        private void Apply(bool snap)
        {
            Vector3 aim = PeekAim(_aim, _peek, _boardCenter, _boardHalfExtent, _yaw);
            Vector3 position = Place(_boardCenter, aim, _offset, _yaw, _zoom);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            float blend = snap ? 1f : 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
        }
    }
}
