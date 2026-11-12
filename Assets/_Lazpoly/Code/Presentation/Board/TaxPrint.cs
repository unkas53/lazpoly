using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The picture printed on the tax square (the owner's wish, 2026-11-12: a design that suits the table): a cream bill
    /// torn off in teeth along its bottom, two written lines on it, and a little stack of gold coins at its foot. The big
    /// "%" is the board's own print, laid on the bill at the picture's origin. Built like <see cref="ChancePrint"/>: each
    /// colour is a picture drawn with the board's print shader on one flat sheet, so its edges hold still under TAA. In a
    /// unit space: the page is <see cref="Area"/> (x across, y up the page), the sheets a few thousandths above y = 0.
    /// </summary>
    public static class TaxPrint
    {
        /// <summary>The page the pictures cover, around the "%"'s middle (the origin).</summary>
        public static readonly Rect Area = new Rect(-0.36f, -0.60f, 0.96f, 0.97f);

        /// <summary>The bill: half its width, its top and the line its torn bottom wanders about, <see cref="Tooth"/> either way in <see cref="Teeth"/> teeth.</summary>
        public const float BillHalf = 0.29f;
        public const float BillTop = 0.30f;
        public const float BillBottom = -0.50f;
        public const float Tooth = 0.04f;
        public const int Teeth = 4;

        /// <summary>The round of the bill's top corners, and its deep outline.</summary>
        public const float BillRound = 0.05f;
        public const float Outline = 0.035f;

        /// <summary>The bill's room for the "%" (inside its outline, above the written lines), in page units.</summary>
        public static readonly Vector2 MarkRoom = new Vector2(0.42f, 0.40f);

        /// <summary>The two written lines under the "%": how thick, how high on the page, and from where to where (the second stops short).</summary>
        public const float LineThickness = 0.03f;
        public const float FirstLine = -0.25f;
        public const float SecondLine = -0.33f;
        public const float LineFrom = -0.19f;
        public const float FirstLineTo = 0.19f;
        public const float SecondLineTo = 0.03f;

        /// <summary>The coins, seen from the side, one on another at the bill's lower right: each one's size, the deep edge round it, and where their middles are.</summary>
        public static readonly Vector2 CoinSize = new Vector2(0.30f, 0.06f);
        public const float CoinEdge = 0.022f;
        public static readonly Vector2[] Coins = { new Vector2(0.37f, -0.50f), new Vector2(0.345f, -0.42f), new Vector2(0.375f, -0.34f) };

        /// <summary>Like the Şans cards': no bigger on the screen, and the three pictures are made at once when the board is built.</summary>
        public const int PictureWidth = 192;
        public const int Samples = 3;

        private const float Layer = 0.003f;

        private static Texture2D _paper;
        private static Texture2D _deep;
        private static Texture2D _gold;

        /// <summary>
        /// The picture as one object: the bill's paper, the deep colour and the gold as three children, the page's up its
        /// local +z, the "%"'s middle at its origin, <see cref="Area"/> times <paramref name="scale"/> across.
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

        // The layers draw under the board's print (queue offsets below the names'), so the "%" printed with the names lies on the bill.
        private static void Print(string name, Transform parent, Texture2D picture, int layer, Color color, float height)
        {
            Material material = PrintMaterial.ForPicture(picture, layer);
            if (material == null) return; // Without the print shader the "%" alone marks the square.
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.AddComponent<MeshFilter>().sharedMesh = FloorPrint.PictureSheet($"print:tax-{name.ToLowerInvariant()}", Area, color, height);
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.receiveShadows = false;
            FloorPrint.NoShadow(part);
        }

        /// <summary>The bill's cream.</summary>
        public static Texture2D PaperPicture() => _paper != null ? _paper : _paper = FloorPrint.Picture("print-tax-paper", Area, PictureWidth, IsPaper, Samples);

        /// <summary>The bill's outline, its two written lines, and the edge round each coin.</summary>
        public static Texture2D DeepPicture() => _deep != null ? _deep : _deep = FloorPrint.Picture("print-tax-deep", Area, PictureWidth, IsDeep, Samples);

        /// <summary>The coins.</summary>
        public static Texture2D GoldPicture() => _gold != null ? _gold : _gold = FloorPrint.Picture("print-tax-gold", Area, PictureWidth, IsGold, Samples);

        public static bool IsPaper(float x, float z) => OnBill(x, z, 0f);

        public static bool IsDeep(float x, float z)
        {
            if (OnCoin(x, z, 0f)) return false;
            if (OnCoin(x, z, CoinEdge)) return true;
            if (!OnBill(x, z, 0f)) return false;
            return !OnBill(x, z, Outline) || Line(x, z, FirstLineTo, FirstLine) || Line(x, z, SecondLineTo, SecondLine);
        }

        public static bool IsGold(float x, float z) => OnCoin(x, z, 0f);

        /// <summary>The height of the bill's torn bottom at <paramref name="x"/>: a saw of <see cref="Teeth"/> teeth, its points down.</summary>
        public static float TornEdge(float x)
        {
            float along = Mathf.Repeat((x + BillHalf) / (2f * BillHalf / Teeth), 1f);
            return BillBottom + Tooth * (Mathf.Abs(along * 2f - 1f) * 2f - 1f);
        }

        // A tooth's side rises this much for each unit across, which makes an outline measured straight up that much taller than its thickness.
        private const float ToothSlope = 2f * Tooth / (BillHalf / Teeth);
        private static readonly float ToothRise = Mathf.Sqrt(1f + ToothSlope * ToothSlope);

        /// <summary>Whether the point lies on the bill, <paramref name="inset"/> in from its edge.</summary>
        public static bool OnBill(float x, float z, float inset)
        {
            float across = Mathf.Abs(x);
            if (across > BillHalf - inset || z > BillTop - inset) return false;
            if (z < TornEdge(x) + inset * ToothRise) return false;
            float round = Mathf.Max(BillRound - inset, 0.005f);
            float qx = across - (BillHalf - inset - round), qz = z - (BillTop - inset - round);
            return qx <= 0f || qz <= 0f || qx * qx + qz * qz <= round * round;
        }

        /// <summary>Whether the point lies on one of the coins, each grown by <paramref name="grow"/> all round.</summary>
        public static bool OnCoin(float x, float z, float grow)
        {
            // Most of the page is nowhere near them.
            if (x < CoinsLeft - grow || z > CoinsTop + grow) return false;
            foreach (Vector2 coin in Coins)
            {
                if (Bar(x, z, coin.x, (CoinSize.x - CoinSize.y) / 2f, coin.y, CoinSize.y / 2f + grow)) return true;
            }

            return false;
        }

        private static readonly float CoinsLeft = Mathf.Min(Coins[0].x, Mathf.Min(Coins[1].x, Coins[2].x)) - CoinSize.x / 2f;
        private static readonly float CoinsTop = Mathf.Max(Coins[0].y, Mathf.Max(Coins[1].y, Coins[2].y)) + CoinSize.y / 2f;

        // A written line: from LineFrom to an end, its ends round.
        private static bool Line(float x, float z, float to, float height) =>
            Bar(x, z, (LineFrom + to) / 2f, (to - LineFrom - LineThickness) / 2f, height, LineThickness / 2f);

        // A bar with round ends lying across the page: its middle, how far its straight part reaches either way, its height on the page, and its ends' radius.
        private static bool Bar(float x, float z, float middle, float reach, float height, float radius)
        {
            float dx = Mathf.Max(Mathf.Abs(x - middle) - reach, 0f), dz = z - height;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
