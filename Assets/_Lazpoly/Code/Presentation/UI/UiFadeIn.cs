using System;
using Lazpoly.Presentation.Effects;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Brings a window in each time its layer is switched on: the layer fades in while the window grows into place,
    /// a touch past its size and back, like something set down (the owner's wish, 2026-11-21: animated menus).
    /// Rebuilding the window's content does not switch the layer, so redraws never flicker. Uses unscaled time, so
    /// it works while paused. Opening and closing are told to <see cref="Shown"/> and <see cref="Hidden"/>.
    /// It also keeps the window on the screen: one taller than the layer (a long card on a wide, low
    /// screen at the large interface size) is scaled down to fit, see <see cref="UiLayout.FitScale"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiFadeIn : MonoBehaviour
    {
        public const float Seconds = 0.22f;

        /// <summary>The layer's dimming is there before the window has settled: it fades over this share of the time.</summary>
        public const float FadeShare = 0.6f;

        /// <summary>How small the window starts.</summary>
        public const float StartScale = 0.9f;

        private CanvasGroup _group;
        private Transform _window;
        private float _startedAt;
        private bool _done;
        private bool _armed;
        private bool _announced;
        private float _fit = 1f;
        private bool _fits = true;

        /// <summary>Raised whenever a window opens; the audio player plays its sound on it.</summary>
        public static event Action Shown;

        /// <summary>Raised whenever a window that was announced by <see cref="Shown"/> closes; the audio player plays its sound on it.</summary>
        public static event Action Hidden;

        /// <summary>How see-through the layer is, and how large the window, <paramref name="t"/> of the way in (0 to 1).</summary>
        public static float AlphaAt(float t) => Mathf.Clamp01(t / FadeShare);

        public static float ScaleAt(float t) => Mathf.LerpUnclamped(StartScale, 1f, Ease.OutBack(t));

        /// <summary>
        /// Fades <paramref name="layer"/> in and lifts <paramref name="window"/> slightly into place. A window that
        /// stands on a frame already scaled to the screen (the menu's sheets) is not fitted again: <paramref name="fit"/> false.
        /// </summary>
        public static void Attach(GameObject layer, Transform window, bool fit = true)
        {
            var fade = layer.AddComponent<UiFadeIn>();
            fade._window = window;
            fade._fits = fit;
            // The first switch-on happens while the screen is being built; only later openings sound.
            fade._armed = true;
        }

        private void OnEnable()
        {
            _group = GetComponent<CanvasGroup>();
            _startedAt = Time.unscaledTime;
            _done = false;
            _fit = Fit();
            Apply(0f);
            if (!_armed) return;
            _announced = true;
            Shown?.Invoke();
        }

        private void OnDisable()
        {
            // Only a window the player saw open is heard closing, and never a scene being torn down.
            if (!_announced) return;
            _announced = false;
            if (gameObject.scene.isLoaded) Hidden?.Invoke();
        }

        private void Update()
        {
            float fit = Fit();
            if (_done && Mathf.Approximately(fit, _fit)) return;
            _fit = fit;
            float t = Mathf.Clamp01((Time.unscaledTime - _startedAt) / Seconds);
            Apply(t);
            _done = t >= 1f;
        }

        private void Apply(float t)
        {
            if (_group != null) _group.alpha = AlphaAt(t);
            if (_window != null) _window.localScale = Vector3.one * (ScaleAt(t) * _fit);
        }

        /// <summary>The scale that keeps the window inside its layer, from their heights as they are laid out now.</summary>
        private float Fit()
        {
            if (!_fits) return 1f;
            var window = _window as RectTransform;
            var screen = transform as RectTransform;
            return window != null && screen != null ? UiLayout.FitScale(window.rect.height, screen.rect.height) : 1f;
        }
    }
}
