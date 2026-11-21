using Lazpoly.Presentation.Effects;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Brings a part of a screen in when the screen is first seen: it starts a little aside and see-through and
    /// eases to its place, each part a moment after the one before (<see cref="Step"/>), so a screen assembles
    /// itself instead of appearing at once. It waits while a loading screen still covers the scene
    /// (<see cref="BootSplash.Covers"/>, <see cref="SceneFader.Covers"/>). Uses unscaled time. Outside play mode
    /// (the editor's tests) the part simply stands in its place.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiEntrance : MonoBehaviour
    {
        /// <summary>How long one part takes to arrive, and how long after the part before it sets off.</summary>
        public const float Seconds = 0.42f;
        public const float Step = 0.05f;

        private RectTransform _rect;
        private CanvasGroup _group;
        private Vector2 _rest;
        private Vector2 _from;
        private float _delay;
        private float _startedAt = -1f;

        /// <summary>Whether the part has arrived (for tests).</summary>
        public bool Arrived { get; private set; }

        /// <summary>
        /// <paramref name="rect"/> comes in from <paramref name="from"/> pixels beside its place, as the
        /// <paramref name="order"/>th part of its screen (0 first).
        /// </summary>
        public static UiEntrance Attach(RectTransform rect, int order, Vector2 from)
        {
            var entrance = rect.gameObject.AddComponent<UiEntrance>();
            entrance._rect = rect;
            entrance._rest = rect.anchoredPosition;
            entrance._from = from;
            entrance._delay = Delay(order);
            if (!rect.TryGetComponent(out entrance._group)) entrance._group = rect.gameObject.AddComponent<CanvasGroup>();
            if (Application.isPlaying) entrance.Apply(0f);
            else entrance.Arrived = true;
            return entrance;
        }

        /// <summary>When the part of a given order sets off, in seconds after the first.</summary>
        public static float Delay(int order) => Mathf.Max(0, order) * Step;

        /// <summary>How far a part is on its way (0 to 1) <paramref name="age"/> seconds after its screen was first seen.</summary>
        public static float Progress(float age, float delay) => Mathf.Clamp01((age - delay) / Seconds);

        /// <summary>How much of the way aside is left, and how solid the part is, at a progress.</summary>
        public static float Aside(float progress) => 1f - Ease.OutCubic(progress);

        public static float Solid(float progress) => Mathf.Clamp01(progress * 2f);

        private void Update()
        {
            if (Arrived) return;
            if (_startedAt < 0f)
            {
                if (BootSplash.Covers || SceneFader.Covers) return;
                _startedAt = Time.unscaledTime;
            }

            float progress = Progress(Time.unscaledTime - _startedAt, _delay);
            Apply(progress);
            if (progress >= 1f) Arrived = true;
        }

        private void Apply(float progress)
        {
            _rect.anchoredPosition = _rest + _from * Aside(progress);
            _group.alpha = Solid(progress);
        }
    }

    /// <summary>Lets a picture hover: it rises and sinks a few pixels about its place, slowly, for as long as it shows (the logo). Unscaled time.</summary>
    [DisallowMultipleComponent]
    public sealed class UiFloat : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector2 _rest;
        private float _height;
        private float _seconds;

        public static UiFloat Attach(RectTransform rect, float height, float seconds)
        {
            var hover = rect.gameObject.AddComponent<UiFloat>();
            hover._rect = rect;
            hover._rest = rect.anchoredPosition;
            hover._height = height;
            hover._seconds = Mathf.Max(0.1f, seconds);
            return hover;
        }

        /// <summary>How far above its place the picture is at a time.</summary>
        public static float RiseAt(float time, float height, float seconds) => height * Mathf.Sin(time / seconds * 2f * Mathf.PI);

        private void Update() => _rect.anchoredPosition = _rest + new Vector2(0f, RiseAt(Time.unscaledTime, _height, _seconds));
    }

    /// <summary>Turns a picture about its middle at a steady pace (the sunburst behind the logo). Unscaled time.</summary>
    [DisallowMultipleComponent]
    public sealed class UiSpin : MonoBehaviour
    {
        private float _degreesPerSecond;

        public static UiSpin Attach(RectTransform rect, float degreesPerSecond)
        {
            var spin = rect.gameObject.AddComponent<UiSpin>();
            spin._degreesPerSecond = degreesPerSecond;
            return spin;
        }

        /// <summary>The turn at a time, in degrees, kept within one round.</summary>
        public static float TurnAt(float time, float degreesPerSecond) => Mathf.Repeat(time * degreesPerSecond, 360f);

        private void Update() => transform.localRotation = Quaternion.Euler(0f, 0f, TurnAt(Time.unscaledTime, _degreesPerSecond));
    }
}
