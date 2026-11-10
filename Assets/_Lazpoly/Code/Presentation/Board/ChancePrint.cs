using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The picture printed on a Şans square (the owner's wish, 2026-11-11: a better shape than the plain question mark):
    /// two playing cards fanned out, the one behind showing its deep back, the one in front a cream face with a gold pip
    /// in two corners, and two gold sparkles beside them. The big "?" is the board's own print, laid on the front card,
    /// whose middle is the picture's origin. Built like <see cref="FloorPrint"/>'s document: each colour is a picture
    /// drawn with the board's print shader on one flat sheet, so its edges hold still under TAA. In a unit space: the
    /// page is <see cref="Area"/> (x across, y up the page), the sheets a few thousandths above y = 0.
    /// </summary>
    public static class ChancePrint
    {
        /// <summary>The page the pictures cover, around the front card's middle (the origin).</summary>
        public static readonly Rect Area = new Rect(-0.6f, -0.46f, 1.1f, 0.98f);

        /// <summary>A card's size and the round of its corners.</summary>
        public static readonly Vector2 CardSize = new Vector2(0.5f, 0.72f);
        public const float CardRound = 0.07f;

        /// <summary>The card behind: where its middle is and how far it leans (degrees, positive turns its top to the right).</summary>
        public static readonly Vector2 BackAt = new Vector2(-0.24f, 0.08f);
        public const float BackLean = -14f;

        /// <summary>The front card stands at the origin, leaning a little the other way.</summary>
        public const float FrontLean = 9f;

        /// <summary>The cream rim round the back's deep colour, and the front card's deep outline.</summary>
        public const float Rim = 0.045f;
        public const float Outline = 0.035f;

        /// <summary>The front card's room for the "?" (inside its outline), in page units.</summary>
        public static readonly Vector2 MarkRoom = new Vector2(0.34f, 0.56f);

        public const int PictureWidth = 256;

        private const float Layer = 0.003f;

        private static Texture2D _paper;
        private static Texture2D _deep;
        private static Texture2D _gold;

        /// <summary>
        /// The picture as one object: the cards' paper, their deep colour and the gold as three children, the page's up its
        /// local +z, the front card's middle at its origin, <see cref="Area"/> times <paramref name="scale"/> across.
        /// </summary>
        public static GameObject Create(string name, Transform parent, float scale, Color paper, Color deep, Color gold)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localScale = Vector3.one * scale;
            Print("Paper", root.transform, PaperPicture(), -3, paper, 0.4f * Layer);
            Print("Deep", root.transform, DeepPicture(), -2, deep, 0.7f * Layer);
            Print("Gold", root.transform, GoldPicture(), -1, gold, Layer);
            return root;
        }

        // The layers draw under the board's print (queue offsets below the names'), so the "?" printed with the names lies on the front card.
        private static void Print(string name, Transform parent, Texture2D picture, int layer, Color color, float height)
        {
            Material material = PrintMaterial.ForPicture(picture, layer);
            if (material == null) return; // Without the print shader the "?" alone marks the square.
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = FloorPrint.PictureSheet($"print:chance-{name.ToLowerInvariant()}", Area, color, height);
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = false;
            FloorPrint.NoShadow(part);
        }

        /// <summary>Both cards' cream, the one in front over the one behind.</summary>
        public static Texture2D PaperPicture() => _paper != null ? _paper : _paper = FloorPrint.Picture("print-chance-paper", Area, PictureWidth, IsPaper);

        /// <summary>The back card's deep colour inside its rim, where the front card leaves it showing, and the front card's outline.</summary>
        public static Texture2D DeepPicture() => _deep != null ? _deep : _deep = FloorPrint.Picture("print-chance-deep", Area, PictureWidth, IsDeep);

        /// <summary>The front card's two pips and the two sparkles beside the cards.</summary>
        public static Texture2D GoldPicture() => _gold != null ? _gold : _gold = FloorPrint.Picture("print-chance-gold", Area, PictureWidth, IsGold);

        public static bool IsPaper(float x, float z) => InFront(x, z, 0f) || InBack(x, z, 0f);

        public static bool IsDeep(float x, float z) =>
            (InBack(x, z, Rim) && !InFront(x, z, 0f)) || (InFront(x, z, 0f) && !InFront(x, z, Outline));

        public static bool IsGold(float x, float z)
        {
            // The pips: small diamonds in the front card's top left and bottom right, like a playing card's corners.
            Vector2 card = ToCard(x, z, Vector2.zero, FrontLean);
            if (Mathf.Abs(card.x + 0.175f) + Mathf.Abs(card.y - 0.28f) <= 0.035f) return true;
            if (Mathf.Abs(card.x - 0.175f) + Mathf.Abs(card.y + 0.28f) <= 0.035f) return true;
            if (InFront(x, z, 0f) || InBack(x, z, 0f)) return false;
            return Sparkle(x, z, new Vector2(0.39f, 0.4f), 0.1f) || Sparkle(x, z, new Vector2(-0.5f, -0.35f), 0.08f);
        }

        /// <summary>Whether the point lies on the front card, <paramref name="inset"/> in from its edge.</summary>
        public static bool InFront(float x, float z, float inset) => InCard(ToCard(x, z, Vector2.zero, FrontLean), inset);

        /// <summary>Whether the point lies on the card behind, <paramref name="inset"/> in from its edge.</summary>
        public static bool InBack(float x, float z, float inset) => InCard(ToCard(x, z, BackAt, BackLean), inset);

        // The point in a card's own frame (x across it, y up it): its turn undone as in Quaternion.Euler(0, lean, 0).
        private static Vector2 ToCard(float x, float z, Vector2 at, float lean)
        {
            float dx = x - at.x, dz = z - at.y;
            float radians = lean * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new Vector2(dx * cos - dz * sin, dx * sin + dz * cos);
        }

        // A rounded rectangle the card's size, shrunk by inset all round.
        private static bool InCard(Vector2 p, float inset)
        {
            float round = Mathf.Max(CardRound - inset, 0.005f);
            float qx = Mathf.Abs(p.x) - (CardSize.x / 2f - inset - round);
            float qy = Mathf.Abs(p.y) - (CardSize.y / 2f - inset - round);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) <= round;
        }

        // A four-pointed star: the curve |x|^0.5 + |y|^0.5 = r^0.5, its points r from the middle.
        private static bool Sparkle(float x, float z, Vector2 at, float radius) =>
            Mathf.Sqrt(Mathf.Abs(x - at.x)) + Mathf.Sqrt(Mathf.Abs(z - at.y)) <= Mathf.Sqrt(radius);
    }
}
