using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The sum passing a money hatch, written in front of it (the owner's wish, 2026-11-12: "-50K, +100K"): the sum
    /// with a minus while a payment goes down the payer's hatch, with a plus while money comes up the receiver's. The
    /// line stands on the lawn just under the hatch on the screen, facing the camera like the rising money lines
    /// (<see cref="FloatingText"/>), with a dark edge all round so the pale inks read on the grass; it appears as the lid
    /// starts to open, stays while the hatch is open and fades once it has shut. It starts under the hatch's edge that
    /// faces the screen's middle and runs outwards from there (<see cref="Side"/>), so a long sum never reaches further
    /// towards the middle, where the roll button stands, than the hatch itself. One line per hatch: a new sum replaces
    /// the one still showing.
    /// The times are the hatch's own (<see cref="MoneyHatch.Swing"/>), so the writing and the lid never part.
    /// </summary>
    public sealed class HatchAmount : MonoBehaviour
    {
        public const string ObjectName = "Hatch amount";

        /// <summary>How far under the hatch's middle the line's middle stands on the screen: clear of the collar's near edge.</summary>
        public const float Below = 0.58f;

        /// <summary>How high over the hatch's mouth the line's middle stands, so the letters are over the grass, not in it.</summary>
        public const float Height = 0.25f;

        /// <summary>The letters' size: a little larger than the rising money lines', since this one is read from across the lawn.</summary>
        public const float CharacterSize = 0.05f;

        /// <summary>The line swells in over this long, stays this long after the lid has shut, and fades over the last of that.</summary>
        public const float PopSeconds = 0.2f;
        public const float Linger = 0.45f;
        public const float FadeSeconds = 0.3f;

        /// <summary>Money coming up: a pale leaf green; money going down: a pale coral. Light, so both read on the meadow.</summary>
        public static readonly Color Up = new Color(0.80f, 0.96f, 0.66f);
        public static readonly Color Down = new Color(1.00f, 0.68f, 0.60f);

        /// <summary>The dark edge round the letters: how far its four copies lie from the line, and how many they are.</summary>
        public const float Edge = 0.028f;
        public const int EdgeCopies = 4;

        /// <summary>A hatch this far or more to one side of the table's middle on the screen writes its sum outwards from its inner edge.</summary>
        public const float SideFrom = 0.3f;

        private static readonly Color EdgeInk = new Color(0.05f, 0.07f, 0.06f, 0.9f);

        private MoneyHatch _hatch;
        private TextMesh _line;
        private TextMesh[] _edges;
        private int _side;
        private Color _ink;
        private float _from;
        private float _until;
        private float _speed = 1f;
        private float _age;

        /// <summary>The sum this line shows: under zero for money going down the hatch.</summary>
        public int Amount { get; private set; }

        /// <summary>What is written.</summary>
        public string Written => _line != null ? _line.text : null;

        /// <summary>How solid the line is now, from 0 to 1.</summary>
        public float Alpha { get; private set; }

        /// <summary>A sum as it is written at a hatch: "+100K" coming up, "-50K" going down, in the game's short money form.</summary>
        public static string Text(int amount) => amount > 0 ? "+" + Lang.M(amount) : Lang.M(amount);

        /// <summary>The ink for a sum: <see cref="Up"/> for money coming up, <see cref="Down"/> for money going down.</summary>
        public static Color Ink(int amount) => amount > 0 ? Up : Down;

        /// <summary>
        /// Writes <paramref name="amount"/> in front of <paramref name="hatch"/> for one swing of its lid: from
        /// <paramref name="opensAt"/> seconds from now, when the lid starts to open, until it has shut after
        /// <paramref name="shutsAt"/> (normal-speed seconds played at <paramref name="speed"/>, as
        /// <see cref="MoneyHatch.Swing"/> takes them). Nothing for no hatch or no money.
        /// </summary>
        public static HatchAmount Show(MoneyHatch hatch, int amount, float opensAt, float shutsAt, float speed)
        {
            if (hatch == null || amount == 0) return null;
            foreach (HatchAmount old in hatch.GetComponentsInChildren<HatchAmount>(true)) old.Remove();

            var root = new GameObject(ObjectName);
            root.transform.SetParent(hatch.transform, false);
            string text = Text(amount);
            Color ink = Ink(amount);
            TextMesh line = Line(root.transform, text, ink);
            var edges = new TextMesh[EdgeCopies];
            for (int i = 0; i < EdgeCopies; i++)
            {
                var edge = new GameObject("Edge");
                edge.transform.SetParent(root.transform, false);
                edge.transform.localPosition = new Vector3(i % 2 == 0 ? Edge : -Edge, i < 2 ? Edge : -Edge, 0.01f);
                edges[i] = Line(edge.transform, text, EdgeInk);
            }

            var shown = root.AddComponent<HatchAmount>();
            shown._hatch = hatch;
            shown._line = line;
            shown._edges = edges;
            shown._ink = ink;
            shown._from = opensAt;
            shown._until = ShutAt(opensAt, shutsAt);
            shown._speed = Mathf.Max(speed, 0.1f);
            shown.Amount = amount;
            shown.Pose(0f);
            return shown;
        }

        /// <summary>Takes the line away at once.</summary>
        public void Remove()
        {
            if (Application.isPlaying) Destroy(gameObject);
            else DestroyImmediate(gameObject);
        }

        /// <summary>When the lid is shut again, for a swing that opens at <paramref name="opensAt"/> and starts to shut at <paramref name="shutsAt"/>.</summary>
        public static float ShutAt(float opensAt, float shutsAt) =>
            Mathf.Max(shutsAt, opensAt + MoneyHatch.SwingSeconds) + MoneyHatch.SwingSeconds;

        /// <summary>When a line whose hatch is shut at <paramref name="until"/> is gone.</summary>
        public static float GoneAt(float until) => until + Linger;

        /// <summary>
        /// How solid the line is <paramref name="t"/> seconds in: nothing before the lid starts to open at
        /// <paramref name="from"/>, whole while the hatch is open, fading over the last <see cref="FadeSeconds"/> before
        /// <see cref="GoneAt"/>.
        /// </summary>
        public static float AlphaAt(float t, float from, float until)
        {
            if (t < from) return 0f;
            return Mathf.Clamp01((GoneAt(until) - t) / FadeSeconds);
        }

        /// <summary>The line's size <paramref name="t"/> seconds in: it swells in as the lid starts to open at <paramref name="from"/>.</summary>
        public static float ScaleAt(float t, float from) =>
            Mathf.Lerp(0.6f, 1f, Ease.OutBack(Mathf.Clamp01((t - from) / PopSeconds)));

        /// <summary>
        /// Where the line stands for a hatch whose mouth is at <paramref name="mouth"/>, seen by a camera with these
        /// axes: <see cref="Below"/> under the mouth on the screen, and brought towards the camera until it is
        /// <see cref="Height"/> over the mouth (which leaves it where it is on the screen), both scaled by
        /// <paramref name="size"/>. For a hatch in the screen's middle (<paramref name="side"/> 0) that is the line's
        /// middle; for one to the right (1) or left (-1) it is the line's inner end, under the hatch's inner edge.
        /// </summary>
        public static Vector3 Spot(Vector3 mouth, Vector3 cameraUp, Vector3 cameraForward, float size = 1f, int side = 0)
        {
            float down = Mathf.Max(-cameraForward.y, 0.2f);
            float near = (Height + cameraUp.y * Below) / down;
            Vector3 right = Vector3.Cross(cameraUp, cameraForward);
            return mouth - cameraUp * (Below * size) - cameraForward * (near * size) - right * (side * MoneyHatch.OuterRadius * size);
        }

        /// <summary>
        /// Which side of the table's <paramref name="middle"/> a hatch lies on the screen: 1 to the right, -1 to the left,
        /// 0 when it is nearer than <see cref="SideFrom"/> (times <paramref name="size"/>) to the middle's column.
        /// </summary>
        public static int Side(Vector3 mouth, Vector3 middle, Vector3 cameraRight, float size = 1f)
        {
            float across = Vector3.Dot(mouth - middle, cameraRight);
            return Mathf.Abs(across) < SideFrom * size ? 0 : across > 0f ? 1 : -1;
        }

        /// <summary>How a line is anchored for its <see cref="Side"/>: by its inner end, or by its middle.</summary>
        public static TextAnchor Anchor(int side) =>
            side > 0 ? TextAnchor.MiddleLeft : side < 0 ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;

        private static TextMesh Line(Transform parent, string text, Color color)
        {
            var mesh = parent.gameObject.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 96;
            mesh.fontStyle = FontStyle.Normal;
            mesh.characterSize = CharacterSize;
            mesh.color = UiBlend.ForVertices(color);
            mesh.font = UiTheme.BoldFont;
            // The font's own material: the line shows through whatever stands before it, like the rising money lines.
            parent.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
            return mesh;
        }

        private void Update()
        {
            _age += Time.deltaTime * _speed;
            if (_hatch == null || _age >= GoneAt(_until))
            {
                Destroy(gameObject);
                return;
            }

            Pose(_age);
        }

        /// <summary>Anchors the line and its edge for the side of the screen its hatch is on.</summary>
        private void Turn(int side)
        {
            TextAnchor anchor = Anchor(side);
            _side = side;
            if (_line.anchor == anchor) return;
            _line.anchor = anchor;
            foreach (TextMesh edge in _edges) edge.anchor = anchor;
        }

        private void Pose(float t)
        {
            Alpha = AlphaAt(t, _from, _until);
            Color ink = _ink;
            ink.a = Alpha;
            _line.color = UiBlend.ForVertices(ink);
            Color dark = EdgeInk;
            dark.a *= Alpha;
            foreach (TextMesh edge in _edges) edge.color = UiBlend.ForVertices(dark);
            transform.localScale = Vector3.one * ScaleAt(t, _from);

            float size = _hatch.transform.lossyScale.x;
            Camera camera = Camera.main;
            if (camera != null)
            {
                Transform table = _hatch.transform.parent;
                Vector3 mouth = _hatch.Mouth;
                Turn(Side(mouth, table != null ? table.position : mouth, camera.transform.right, size));
                transform.position = Spot(mouth, camera.transform.up, camera.transform.forward, size, _side);
                transform.rotation = camera.transform.rotation;
            }
            else
            {
                transform.position = _hatch.Mouth + Vector3.up * (Height * size);
            }
        }
    }
}
