using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Motes of gold dust rising slowly through a part of the screen, each at its own pace, swaying a little and
    /// glinting: the air of the menu's rail and of the loading screen is never quite still. Where each mote is at a
    /// time is a plain function of its number (<see cref="MoteAt"/>), so nothing is random and nothing piles up.
    /// The motes draw on a canvas of their own, so moving them every frame redraws nothing else. Unscaled time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiDust : MonoBehaviour
    {
        /// <summary>How far below and above its field a mote starts and ends, in pixels.</summary>
        public const float Margin = 24f;

        public readonly struct Mote
        {
            public Mote(Vector2 position, float alpha, float size)
            {
                Position = position;
                Alpha = alpha;
                Size = size;
            }

            /// <summary>From the field's bottom left, in pixels.</summary>
            public Vector2 Position { get; }

            public float Alpha { get; }

            public float Size { get; }
        }

        private RectTransform _field;
        private RectTransform[] _motes;
        private Image[] _lights;
        private Color _color;

        /// <summary><paramref name="count"/> motes in <paramref name="color"/> rising through <paramref name="field"/>.</summary>
        public static UiDust Attach(RectTransform field, int count, Color color)
        {
            // A canvas of its own: the motes move every frame, and only they are drawn again for it.
            field.gameObject.AddComponent<Canvas>();
            var dust = field.gameObject.AddComponent<UiDust>();
            dust._field = field;
            dust._color = color;
            dust._motes = new RectTransform[count];
            dust._lights = new Image[count];
            for (int i = 0; i < count; i++)
            {
                RectTransform mote = UiRoot.Rect(field, "Mote", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                var light = mote.gameObject.AddComponent<Image>();
                light.sprite = UiFx.Dot;
                light.raycastTarget = false;
                dust._motes[i] = mote;
                dust._lights[i] = light;
            }

            dust.Show(0f);
            return dust;
        }

        /// <summary>A number from 0 to 1 that belongs to a mote and never changes: the same mote, the same pace and path.</summary>
        public static float Trait(int index, int which) => Mathf.Repeat(Mathf.Sin(index * 12.9898f + which * 78.233f) * 43758.547f, 1f);

        /// <summary>
        /// Where mote <paramref name="index"/> is in a field of <paramref name="field"/> pixels at a time, how bright and
        /// how large: it rises from under the field to over it, round and round, fading in at the bottom and out at the top.
        /// </summary>
        public static Mote MoteAt(int index, float time, Vector2 field)
        {
            float pace = Trait(index, 1), start = Trait(index, 2), across = Trait(index, 3), nature = Trait(index, 4);
            float way = field.y + 2f * Margin;
            float seconds = way / (10f + 20f * pace);
            float risen = Mathf.Repeat(time / seconds + start, 1f);
            float sway = Mathf.Sin(time * (0.25f + 0.35f * nature) + index * 2.4f) * 16f;
            float glint = 0.6f + 0.4f * Mathf.Sin(time * (0.7f + 1.3f * pace) + index * 1.7f);
            float ends = Mathf.Clamp01(risen / 0.12f) * Mathf.Clamp01((1f - risen) / 0.2f);
            return new Mote(
                new Vector2(across * field.x + sway, -Margin + risen * way),
                (0.25f + 0.6f * nature) * glint * ends,
                5f + 11f * nature * (0.4f + 0.6f * pace));
        }

        private void Update() => Show(Time.unscaledTime);

        private void Show(float time)
        {
            Vector2 field = _field.rect.size;
            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = MoteAt(i, time, field);
                _motes[i].anchoredPosition = mote.Position;
                _motes[i].sizeDelta = new Vector2(mote.Size, mote.Size);
                _lights[i].color = new Color(_color.r, _color.g, _color.b, _color.a * mote.Alpha);
            }
        }
    }
}
