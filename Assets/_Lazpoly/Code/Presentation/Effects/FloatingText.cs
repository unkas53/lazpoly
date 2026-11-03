using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// A short line ("+200", "-60") that rises from a point on the table, faces the camera and fades out.
    /// A dark copy just behind it keeps it readable on the light lawn and tiles; lines that start at the
    /// same spot within <see cref="StackSeconds"/> of each other start one above the other instead of on top.
    /// </summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Seconds = 1.3f;
        private const float Rise = 0.8f;

        /// <summary>Height between stacked lines, in world units.</summary>
        public const float LineGap = 0.34f;

        /// <summary>How long a line keeps its spot taken for the next one.</summary>
        public const float StackSeconds = 0.7f;

        /// <summary>How close two starts count as the same spot.</summary>
        public const float SameSpot = 0.9f;

        private static readonly List<FloatingText> Active = new List<FloatingText>();
        private static readonly Color ShadowInk = new Color(0.08f, 0.10f, 0.10f, 0.7f);

        private TextMesh _text;
        private TextMesh _shadow;
        private Color _color;
        private Vector3 _start;
        private Vector3 _spot;
        private float _age;

        public static FloatingText Spawn(Transform parent, Vector3 position, string text, Color color)
        {
            Active.RemoveAll(line => line == null);
            var starts = new Vector3[Active.Count];
            var ages = new float[Active.Count];
            for (int i = 0; i < Active.Count; i++)
            {
                starts[i] = Active[i]._spot;
                ages[i] = Active[i]._age;
            }

            int stack = StackIndex(starts, ages, position);
            var root = new GameObject($"Floating {text}");
            root.transform.SetParent(parent, false);
            root.transform.position = position + Vector3.up * (stack * LineGap);

            TextMesh mesh = Line(root.transform, text, color);
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localPosition = new Vector3(0.018f, -0.018f, 0.01f);
            TextMesh back = Line(shadow.transform, text, ShadowInk);

            var floating = root.AddComponent<FloatingText>();
            floating._text = mesh;
            floating._shadow = back;
            floating._color = color;
            floating._start = root.transform.position;
            floating._spot = position + Vector3.up * (stack * LineGap);
            floating.Face();
            Active.Add(floating);
            return floating;
        }

        /// <summary>
        /// Where a new line starting at <paramref name="position"/> goes in the stack: one step up for each
        /// line that started at the same spot (or a step above it) less than <see cref="StackSeconds"/> ago.
        /// </summary>
        public static int StackIndex(IReadOnlyList<Vector3> starts, IReadOnlyList<float> ages, Vector3 position)
        {
            int index = 0;
            while (Taken(starts, ages, position + Vector3.up * (index * LineGap))) index++;
            return index;
        }

        /// <summary>Whether a fresh line already starts at this spot (same place on the table, same height).</summary>
        private static bool Taken(IReadOnlyList<Vector3> starts, IReadOnlyList<float> ages, Vector3 spot)
        {
            for (int i = 0; i < starts.Count; i++)
            {
                if (ages[i] >= StackSeconds) continue;
                if (Vector3.Distance(Flat(starts[i]), Flat(spot)) > SameSpot) continue;
                if (Mathf.Abs(starts[i].y - spot.y) < LineGap * 0.5f) return true;
            }

            return false;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        private static TextMesh Line(Transform parent, string text, Color color)
        {
            var mesh = parent.gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 96;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = 0.045f;
            mesh.color = color;
            mesh.font = UI.UiTheme.BoldFont;
            parent.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = _start + Vector3.up * (Rise * Ease.OutCubic(t));
            // A quick pop in, then fade during the last half.
            transform.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, Ease.OutBack(Mathf.Clamp01(t * 5f)));
            float alpha = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            Color color = _color;
            color.a = alpha;
            _text.color = color;
            Color shade = ShadowInk;
            shade.a *= alpha;
            _shadow.color = shade;
            Face();
        }

        private void OnDestroy() => Active.Remove(this);

        private void Face()
        {
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Active.Clear();
    }
}
