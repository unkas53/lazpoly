using UnityEngine;
using UnityEngine.EventSystems;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// Lets the player look around the board: the mouse wheel zooms, dragging with the right button
    /// turns the view around the board, and <see cref="ResetView"/> (or the Home key) returns to the
    /// framed view that <see cref="BoardCameraFraming"/> computes. The pointer over the interface is left alone.
    /// </summary>
    public sealed class BoardCameraRig : MonoBehaviour
    {
        public const float MinZoom = 0.45f;
        public const float MaxZoom = 1.15f;
        private const float WheelStep = 0.1f;
        private const float DegreesPerMouseUnit = 5f;
        private const float Smoothing = 12f;
        private const float GlanceAmount = 0.28f;

        private Vector3 _boardCenter;
        private Vector3 _aim;
        private Vector3 _offset;
        private float _pitch;
        private bool _hasBase;
        private float _yaw;
        private float _zoom = 1f;
        private Vector3 _glancePoint;
        private float _glanceUntil;
        private float _glanceSeconds;

        public float Yaw => _yaw;
        public float Zoom => _zoom;

        /// <summary>The framed view: the camera position for yaw 0 and zoom 1, looking down at <paramref name="pitch"/> degrees.</summary>
        public void SetBase(Vector3 boardCenter, Vector3 cameraPosition, float pitch)
        {
            _boardCenter = boardCenter;
            _pitch = pitch;
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

        /// <summary>
        /// Pans a little towards <paramref name="worldPoint"/> and back over <paramref name="seconds"/>,
        /// a glance at the pawn whose turn begins. The player's own zoom and turn are kept.
        /// </summary>
        public void Glance(Vector3 worldPoint, float seconds = 1.4f)
        {
            _glancePoint = worldPoint;
            _glanceSeconds = Mathf.Max(seconds, 0.1f);
            _glanceUntil = Time.unscaledTime + _glanceSeconds;
        }

        private Vector3 GlanceOffset()
        {
            float left = _glanceUntil - Time.unscaledTime;
            if (left <= 0f) return Vector3.zero;
            float t = 1f - left / _glanceSeconds;
            Vector3 towards = _glancePoint - _aim;
            towards.y = 0f;
            return towards * (GlanceAmount * Mathf.Sin(t * Mathf.PI));
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
            Vector3 position = Place(_boardCenter, _aim + GlanceOffset(), _offset, _yaw, _zoom);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            float blend = snap ? 1f : 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
        }
    }
}
