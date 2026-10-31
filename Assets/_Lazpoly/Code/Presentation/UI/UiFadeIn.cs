using System;
using Lazpoly.Presentation.Effects;
using UnityEngine;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Fades a window layer in each time it is switched on. Rebuilding the window's content does not
    /// switch the layer, so redraws never flicker. Uses unscaled time, so it works while paused.
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
            Apply(0f);
            if (_armed) Shown?.Invoke();
        }

        private void Update()
        {
            if (_done) return;
            float t = Mathf.Clamp01((Time.unscaledTime - _startedAt) / Seconds);
            Apply(t);
            _done = t >= 1f;
        }

        private void Apply(float t)
        {
            if (_group != null) _group.alpha = t;
            if (_window != null) _window.localScale = Vector3.one * Mathf.Lerp(0.96f, 1f, Ease.OutCubic(t));
        }
    }
}
