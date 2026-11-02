using UnityEngine;
using UnityEngine.EventSystems;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The fixed isometric view of the board: an orthographic camera looking down at the board from one
    /// corner, so the board is a diamond on the screen. The mouse wheel zooms, <see cref="ResetView"/>
    /// (or the Home key) returns to the framed view that <see cref="BoardCameraFraming"/> computes,
    /// and a pinned square pulls the view a little towards itself (<see cref="Peek"/>); the view never
    /// leaves the board. The screenshot presets may turn it to another corner. The pointer over the interface is left alone.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class BoardCameraRig : MonoBehaviour
    {
        /// <summary>Camera tilt below the horizon.</summary>
        public const float Pitch = 35f;

        /// <summary>Which way the camera looks: from the start square's corner, so the start sits at the bottom of the screen.</summary>
        public const float BaseYaw = -45f;

        /// <summary>How far the camera sits from the point it looks at; depth makes no difference to the picture, only to what the shadows reach.</summary>
        public const float Distance = 25f;

        public const float MinZoom = 0.45f;
        public const float MaxZoom = 1.15f;

        /// <summary>How far towards a pinned square the view slides: a fraction of the distance.</summary>
        public const float PeekAmount = 0.3f;
        private const float WheelStep = 0.1f;
        private const float Smoothing = 12f;

        private Camera _camera;
        private Vector3 _boardCenter;
        private Vector3 _aim;
        private float _baseSize = 10f;
        private float _boardHalfExtent = float.PositiveInfinity;
        private bool _hasBase;
        private float _yaw = BaseYaw;
        private float _zoom = 1f;
        private Vector3? _peek;

        public float Yaw => _yaw;
        public float Zoom => _zoom;

        /// <summary>The framed view: what the camera looks at and how large the view is at zoom 1.</summary>
        /// <param name="boardHalfExtent">Half the board's width; the view is never pulled past its edge.</param>
        public void SetBase(Vector3 boardCenter, Vector3 aim, float orthographicSize, float boardHalfExtent = float.PositiveInfinity)
        {
            _boardCenter = boardCenter;
            _aim = aim;
            _baseSize = orthographicSize;
            _boardHalfExtent = boardHalfExtent;
            bool first = !_hasBase;
            _hasBase = true;
            if (first) Apply(snap: true);
        }

        public void ResetView()
        {
            _yaw = BaseYaw;
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

        /// <summary>A point held inside the board's square, so the view cannot be pulled off the plate.</summary>
        public static Vector3 ClampToBoard(Vector3 point, Vector3 boardCenter, float halfExtent)
        {
            if (float.IsInfinity(halfExtent)) return point;
            return new Vector3(
                boardCenter.x + Mathf.Clamp(point.x - boardCenter.x, -halfExtent, halfExtent), point.y,
                boardCenter.z + Mathf.Clamp(point.z - boardCenter.z, -halfExtent, halfExtent));
        }

        /// <summary>Where the view aims once the peek is applied: the framed aim slid towards the point, at the aim's own height.</summary>
        public static Vector3 PeekAim(Vector3 aim, Vector3? peek, Vector3 boardCenter, float halfExtent)
        {
            if (!peek.HasValue) return aim;
            Vector3 target = ClampToBoard(peek.Value, boardCenter, halfExtent);
            return Vector3.Lerp(aim, new Vector3(target.x, aim.y, target.z), PeekAmount);
        }

        /// <summary>Camera position that looks at <paramref name="aim"/> from <paramref name="distance"/> away, tilted and turned as given.</summary>
        public static Vector3 Place(Vector3 aim, float pitch, float yaw, float distance) =>
            aim - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;

        /// <summary>The orthographic size at a zoom: zooming in shows less of the board.</summary>
        public static float SizeAt(float baseSize, float zoom) => baseSize * Mathf.Clamp(zoom, MinZoom, MaxZoom);

        private void Awake() => _camera = GetComponent<Camera>();

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
            if (overInterface) return;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) _zoom = Mathf.Clamp(_zoom * (1f - wheel * WheelStep), MinZoom, MaxZoom);
        }

        private void Apply(bool snap)
        {
            Vector3 aim = PeekAim(_aim, _peek, _boardCenter, _boardHalfExtent);
            Vector3 position = Place(aim, Pitch, _yaw, Distance);
            Quaternion rotation = Quaternion.Euler(Pitch, _yaw, 0f);
            float size = SizeAt(_baseSize, _zoom);
            float blend = snap ? 1f : 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, position, blend);
            transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
            if (_camera == null) _camera = GetComponent<Camera>();
            if (_camera != null) _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, size, blend);
        }
    }
}
