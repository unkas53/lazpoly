using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// A short line a computer player says, in a paper bubble over its pawn: it pops in, follows the
    /// pawn, faces the camera and goes again after <see cref="Seconds"/>. The tail is in the player's colour.
    /// </summary>
    public sealed class SpeechBubble : MonoBehaviour
    {
        public const float Seconds = 2.6f;

        /// <summary>How far above the followed point the bubble's tail ends.</summary>
        public const float Lift = 0.8f;

        /// <summary>Paper round the text, in world units each side.</summary>
        public const float Padding = 0.1f;

        public const float TailHeight = 0.14f;
        private const float PopSeconds = 0.22f;
        private const float FadeSeconds = 0.2f;
        private const float Thickness = 0.1f;
        private static readonly Color Paper = new Color(0.98f, 0.97f, 0.93f);
        private static readonly Color Ink = new Color(0.17f, 0.15f, 0.14f);

        private Transform _follow;
        private float _age;
        private float _end = Seconds;

        /// <summary>The line in the bubble.</summary>
        public string Text { get; private set; }

        /// <summary>The paper's width and height, text and padding included.</summary>
        public Vector2 PaperSize { get; private set; }

        public static SpeechBubble Say(Transform parent, Transform follow, string text, Color accent)
        {
            var root = new GameObject($"Bubble {text}");
            root.transform.SetParent(parent, false);
            var bubble = root.AddComponent<SpeechBubble>();
            bubble._follow = follow;
            bubble.Text = text;

            // The text first, upright and unturned, so its size can be read for the paper behind it.
            var label = new GameObject("Line");
            label.transform.SetParent(root.transform, false);
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 96;
            mesh.characterSize = 0.04f;
            mesh.color = Ink;
            mesh.font = UI.UiTheme.BoldFont;
            label.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            Vector3 size = label.GetComponent<MeshRenderer>().bounds.size;
            bubble.PaperSize = new Vector2(Mathf.Max(size.x, 0.2f) + 2f * Padding, Mathf.Max(size.y, 0.2f) + 2f * Padding);
            // The line sits a little in front of the paper, on the side the camera sees.
            label.transform.localPosition = new Vector3(0f, 0f, -Thickness / 2f - 0.01f);

            GameObject paper = ModelParts.Create("Paper", root.transform, MeshFactory.RoundedBox(new Vector3(bubble.PaperSize.x, bubble.PaperSize.y, Thickness), 0.05f, 3), Paper, 0.1f);
            GameObject tail = ModelParts.Create("Tail", root.transform, MeshFactory.Cone(8), accent, 0.1f);
            tail.transform.localPosition = new Vector3(0f, -bubble.PaperSize.y / 2f + 0.02f, 0f);
            tail.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            tail.transform.localScale = new Vector3(0.06f, TailHeight, 0.06f);
            paper.transform.localPosition = Vector3.zero;
            // A bubble floats over the table; a hard shadow of it on the lawn would read as a stain.
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            bubble.Place();
            bubble.transform.localScale = Vector3.zero;
            return bubble;
        }

        /// <summary>Lets the bubble go now (a newer line replaces it): it shrinks away over the fade time.</summary>
        public void Dismiss() => _end = Mathf.Min(_end, _age + FadeSeconds);

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _end)
            {
                Destroy(gameObject);
                return;
            }

            float grow = Ease.OutBack(Mathf.Clamp01(_age / PopSeconds));
            float shrink = Mathf.Clamp01((_end - _age) / FadeSeconds);
            transform.localScale = Vector3.one * (grow * shrink);
            Place();
        }

        /// <summary>Over the followed point with its tail's tip at <see cref="Lift"/>, turned the way the camera looks.</summary>
        private void Place()
        {
            if (_follow != null) transform.position = _follow.position + Vector3.up * (Lift + PaperSize.y / 2f + TailHeight);
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
        }
    }
}
