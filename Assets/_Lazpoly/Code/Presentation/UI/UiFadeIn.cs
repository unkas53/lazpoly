using System;
using Lazpoly.Presentation.Effects;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Fades a window layer in each time it is switched on. Rebuilding the window's content does not
    /// switch the layer, so redraws never flicker. Uses unscaled time, so it works while paused.
    /// It also keeps the window on the screen: one taller than the layer (a long card on a wide, low
    /// screen at the large interface size) is scaled down to fit, see <see cref="UiLayout.FitScale"/>.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UiFadeIn : MonoBehaviour
    {
        public const float Seconds = 0.14f;

        private CanvasGroup _group;
        private Transform _window;
        private float _startedAt;
        private bool _done;
        private bool _armed;
        private float _fit = 1f;

        /// <summary>Raised whenever a window opens; the audio player plays its sound on it.</summary>
        public static event Action Shown;

        /// <summary>Fades <paramref name="layer"/> in and lifts <paramref name="window"/> slightly into place.</summary>
        public static void Attach(GameObject layer, Transform window)
        {
            var fade = layer.AddComponent<UiFadeIn>();
            fade._window = window;
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
            if (_armed) Shown?.Invoke();
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
            if (_group != null) _group.alpha = t;
            if (_window != null) _window.localScale = Vector3.one * (Mathf.Lerp(0.96f, 1f, Ease.OutCubic(t)) * _fit);
        }

        /// <summary>The scale that keeps the window inside its layer, from their heights as they are laid out now.</summary>
        private float Fit()
        {
            var window = _window as RectTransform;
            var screen = transform as RectTransform;
            return window != null && screen != null ? UiLayout.FitScale(window.rect.height, screen.rect.height) : 1f;
        }
    }
}
