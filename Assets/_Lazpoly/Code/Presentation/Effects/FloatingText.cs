using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>A short line ("+200", "-60") that rises from a point on the table, faces the camera and fades out.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        private const float Seconds = 1.3f;
        private const float Rise = 1.1f;

        private TextMesh _text;
        private Color _color;
        private Vector3 _start;
        private float _age;

        public static FloatingText Spawn(Transform parent, Vector3 position, string text, Color color)
        {
            var root = new GameObject($"Floating {text}");
            root.transform.SetParent(parent, false);
            root.transform.position = position;

            var mesh = root.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.LowerCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 96;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = 0.045f;
            mesh.color = color;
            mesh.font = UI.UiTheme.BoldFont;
            root.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;

            var floating = root.AddComponent<FloatingText>();
            floating._text = mesh;
            floating._color = color;
            floating._start = position;
            floating.Face();
            return floating;
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
            Color color = _color;
            color.a = 1f - Mathf.Clamp01((t - 0.5f) * 2f);
            _text.color = color;
            Face();
        }

        private void Face()
        {
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
