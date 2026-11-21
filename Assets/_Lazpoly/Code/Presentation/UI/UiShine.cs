using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// A sheen that crosses a picture now and then: a slanted band of light sliding from its left to its right,
    /// seen only where the picture is (a window in the picture's own shape holds it), then nothing for a while. On
    /// the logo and on the button a screen is about, it says "this is the thing" without a word. Unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiShine : MonoBehaviour
    {
        /// <summary>How far the band leans from upright, in degrees, and how wide it is as a share of what it crosses.</summary>
        public const float Lean = 20f;
        public const float BandShare = 0.28f;

        private RectTransform _window;
        private RectTransform _band;
        private float _every;
        private float _seconds;
        private float _offset;

        /// <summary>
        /// A sheen over <paramref name="host"/>, in the shape of <paramref name="shape"/> (drawn like
        /// <paramref name="like"/> when given: sliced, with the same corners), crossing in <paramref name="seconds"/>
        /// once every <paramref name="every"/> seconds, the first time <paramref name="offset"/> seconds late.
        /// <paramref name="inset"/> keeps it off the host's edges (left, bottom, right, top).
        /// </summary>
        public static UiShine Attach(RectTransform host, Sprite shape, float every, float seconds, float strength, float offset = 0f,
            Vector4 inset = default, Image like = null, float cornerScale = 1f)
        {
            RectTransform window = UiRoot.Rect(host, "Shine", Vector2.zero, Vector2.one, new Vector2(inset.x, inset.y), new Vector2(-inset.z, -inset.w));
            var mask = window.gameObject.AddComponent<Image>();
            mask.sprite = shape;
            mask.raycastTarget = false;
            if (like != null) mask.preserveAspect = like.preserveAspect;
            if (shape != null && shape.border != Vector4.zero)
            {
                mask.type = Image.Type.Sliced;
                mask.pixelsPerUnitMultiplier = cornerScale;
            }

            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform band = UiRoot.Rect(window, "Band", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            band.localRotation = Quaternion.Euler(0f, 0f, -Lean);
            var light = band.gameObject.AddComponent<Image>();
            light.sprite = UiFx.Band;
            light.color = new Color(1f, 1f, 1f, strength);
            light.raycastTarget = false;

            var shine = window.gameObject.AddComponent<UiShine>();
            shine._window = window;
            shine._band = band;
            shine._every = Mathf.Max(0.1f, every);
            shine._seconds = Mathf.Clamp(seconds, 0.05f, shine._every);
            shine._offset = offset;
            band.gameObject.SetActive(false);
            return shine;
        }

        /// <summary>
        /// How far across the band is at a time: 0 to 1 while it crosses (the first <paramref name="seconds"/> of every
        /// <paramref name="every"/>), -1 while it rests.
        /// </summary>
        public static float CrossingAt(float time, float every, float seconds)
        {
            float t = Mathf.Repeat(time, every);
            return t <= seconds ? t / seconds : -1f;
        }

        private void Update()
        {
            float crossing = CrossingAt(Time.unscaledTime - _offset, _every, _seconds);
            bool shows = crossing >= 0f && Time.unscaledTime >= _offset;
            if (_band.gameObject.activeSelf != shows) _band.gameObject.SetActive(shows);
            if (!shows) return;

            Vector2 size = _window.rect.size;
            float width = Mathf.Max(24f, size.x * BandShare);
            // Leaning, the band's ends reach sideways by its height's share of the lean; it starts and ends clear of the picture.
            float reach = width / 2f + size.y * Mathf.Tan(Lean * Mathf.Deg2Rad);
            _band.sizeDelta = new Vector2(width, size.y * 1.6f + 40f);
            _band.anchoredPosition = new Vector2(Mathf.Lerp(-reach, size.x + reach, crossing), 0f);
        }
    }
}
