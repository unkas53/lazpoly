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

        /// <summary>How far above the followed point the bubble's tail ends: a little over the character's head (<see cref="Lazpoly.Presentation.Pawns.PawnView.CharacterHeight"/>).</summary>
        public const float Lift = Pawns.PawnView.CharacterHeight + 0.25f;

        /// <summary>Paper round the text, in world units each side.</summary>
        public const float Padding = 0.08f;

        /// <summary>The line's character size: small enough that a bubble over a far figure leaves most of the names behind it readable.</summary>
        public const float TextSize = 0.034f;

        public const float TailHeight = 0.14f;
        private const float PopSeconds = 0.22f;
        private const float FadeSeconds = 0.2f;
        private const float Thickness = 0.1f;
        // The interface kit's cream paper and petrol ink, so a figure's words read like the windows.
        private static readonly Color Paper = new Color(1f, 0.9921569f, 0.9607843f);
        private static readonly Color Ink = new Color(0.086f, 0.239f, 0.314f);

        private Transform _follow;
        private float _age;
        private float _end = Seconds;

        /// <summary>The line in the bubble.</summary>
        public string Text { get; private set; }

        /// <summary>The paper's width and height, text and padding included.</summary>
        public Vector2 PaperSize { get; private set; }

        /// <summary>
        /// Screen pixels at the top that the bubble keeps clear of (the turn banner while it shows): a bubble
        /// over a far square drops down in front of its figure instead of hiding behind the band.
        /// </summary>
        public float TopClearPixels { get; set; }

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
            mesh.characterSize = TextSize;
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
            if (camera == null) return;
            transform.rotation = camera.transform.rotation;
            transform.position += DropBelow(camera, transform.position, PaperSize.y / 2f, TopClearPixels);
        }

        /// <summary>
        /// How far to move a bubble centred at <paramref name="center"/> so its top edge (half its height up
        /// the screen) stays <paramref name="topClear"/> pixels under the top of the camera's view; nothing
        /// when it is already lower, when nothing is kept clear, or when it is behind the camera.
        /// </summary>
        public static Vector3 DropBelow(Camera camera, Vector3 center, float halfHeight, float topClear)
        {
            if (topClear <= 0f) return Vector3.zero;
            Vector3 top = center + camera.transform.up * halfHeight;
            Vector3 screen = camera.WorldToScreenPoint(top);
            float ceiling = camera.pixelHeight - topClear;
            if (screen.z <= 0f || screen.y <= ceiling) return Vector3.zero;
            return camera.ScreenToWorldPoint(new Vector3(screen.x, ceiling, screen.z)) - top;
        }
    }
}
