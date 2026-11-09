using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.UI;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>The colour family of a card in the middle of the screen: its picture's band and its title.</summary>
    public enum CardTone
    {
        Attack,
        Defence,
        Money,
        Cost,
        Move,
        Cup,
        Jail,
        Trade,
        Grey,
    }

    /// <summary>
    /// The pictures and colours of the cards that open in the middle of the screen (the owner's wish, 2026-11-09): a
    /// picture for each kind of Şans card and for each of the table's own cards (a trade, a debt, the third double, a trade
    /// agreement, the cup), the kind's colour family and the small line over the title. The pictures are made by
    /// Tools/Art/make_card_art.js from Tabler line icons and ship as PNG bytes under Resources/Lazpoly/Art/Cards.
    /// </summary>
    public static class CardArt
    {
        public const string Folder = "Lazpoly/Art/Cards/";

        /// <summary>The card's face (cream, rounded) and its soft shadow, both stretched as 9-slices.</summary>
        public const string Face = "Card_Face";
        public const string Shadow = "Card_Shadow";

        public const string TradeAccepted = "Card_tradeAccepted";
        public const string TradeRejected = "Card_tradeRejected";
        public const string Debt = "Card_debt";
        public const string ThirdDouble = "Card_thirdDouble";
        public const string GroupDeal = "Card_groupDeal";
        public const string Cup = "Card_cup";
        public const string ShieldHeld = "Card_shieldHeld";

        /// <summary>Every picture the game asks for, so a test can check each one ships.</summary>
        public static IEnumerable<string> All
        {
            get
            {
                foreach (CardKind kind in System.Enum.GetValues(typeof(CardKind))) yield return PictureFor(kind);
                yield return TradeAccepted;
                yield return TradeRejected;
                yield return Debt;
                yield return ThirdDouble;
                yield return GroupDeal;
                yield return Cup;
                yield return ShieldHeld;
            }
        }

        /// <summary>The picture of a Şans card's kind: "Card_demolish".</summary>
        public static string PictureFor(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Collect: return "Card_collect";
                case CardKind.Pay: return "Card_pay";
                case CardKind.MoveTo: return "Card_moveTo";
                case CardKind.MoveBy: return "Card_moveBy";
                case CardKind.PayPerBuilding: return "Card_repairs";
                case CardKind.Lookout: return "Card_lookout";
                case CardKind.GoToJail: return "Card_goToJail";
                case CardKind.Lawyer: return "Card_lawyer";
                case CardKind.RentShield: return "Card_rentShield";
                case CardKind.Chauffeur: return "Card_chauffeur";
                case CardKind.CupTransfer: return "Card_cupTransfer";
                case CardKind.LandDeal: return "Card_landDeal";
                case CardKind.Renewal: return "Card_renewal";
                case CardKind.Fund: return "Card_fund";
                case CardKind.Shield: return "Card_shield";
                case CardKind.Strike: return "Card_strike";
                case CardKind.Audit: return "Card_audit";
                case CardKind.Demolish: return "Card_demolish";
                case CardKind.IllegalFloor: return "Card_illegalFloor";
                case CardKind.ForceSale: return "Card_forceSale";
                default: return "Card_collect";
            }
        }

        public static CardTone ToneFor(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Strike:
                case CardKind.Audit:
                case CardKind.Demolish:
                case CardKind.IllegalFloor:
                case CardKind.ForceSale: return CardTone.Attack;
                case CardKind.Lawyer:
                case CardKind.RentShield:
                case CardKind.Shield: return CardTone.Defence;
                case CardKind.Collect:
                case CardKind.LandDeal:
                case CardKind.Renewal:
                case CardKind.Fund: return CardTone.Money;
                case CardKind.Pay:
                case CardKind.PayPerBuilding: return CardTone.Cost;
                case CardKind.CupTransfer: return CardTone.Cup;
                case CardKind.GoToJail: return CardTone.Jail;
                default: return CardTone.Move;
            }
        }

        /// <summary>The deep shade of a colour family, as the picture's band ends in it: the card's title is written in it.</summary>
        public static Color ToneColor(CardTone tone)
        {
            switch (tone)
            {
                case CardTone.Attack: return Hex(0x9c2f2a);
                case CardTone.Defence: return Hex(0x174959);
                case CardTone.Money: return Hex(0x1f6b43);
                case CardTone.Cost: return Hex(0x96541a);
                case CardTone.Move: return Hex(0x41338a);
                case CardTone.Cup: return Hex(0x8a6309);
                case CardTone.Jail: return Hex(0x2f3847);
                case CardTone.Trade: return Hex(0x17685f);
                default: return Hex(0x4a4f59);
            }
        }

        /// <summary>The small line over a card's title: what family the card is from ("SALDIRI KARTI").</summary>
        public static string Family(CardKind kind)
        {
            if (!CardDefinition.IsKeptKind(kind) || kind == CardKind.Lookout)
            {
                return kind == CardKind.Lookout ? Upper(L("Hareket kartı")) : null;
            }

            switch (ToneFor(kind))
            {
                case CardTone.Attack: return Upper(L("Saldırı kartı"));
                case CardTone.Defence: return Upper(L("Koruma kartı"));
                case CardTone.Money: return Upper(L("Ekonomi kartı"));
                case CardTone.Cup: return Upper(L("Kupa kartı"));
                default: return Upper(L("Hareket kartı"));
            }
        }

        /// <summary>A drawn Şans card's title: a kept card's name, or what a plain card does ("PARA", "CEZA", "HAPSE GİR").</summary>
        public static string Title(CardKind kind)
        {
            switch (kind)
            {
                case CardKind.Collect: return Upper(L("Para geldi"));
                case CardKind.Pay: return Upper(L("Ceza"));
                case CardKind.MoveTo: return Upper(L("Yolculuk"));
                case CardKind.MoveBy: return Upper(L("Yürü"));
                case CardKind.PayPerBuilding: return Upper(L("Tamir"));
                case CardKind.GoToJail: return Upper(L("Hapse gir"));
                default: return Upper(CardPlay.Name(kind));
            }
        }

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);

        /// <summary>The 9-slice borders of the face and the shadow, in picture pixels; the pictures have none.</summary>
        public static Vector4 Borders(string name) =>
            name == Face ? new Vector4(64f, 64f, 64f, 64f) : name == Shadow ? new Vector4(120f, 120f, 120f, 120f) : Vector4.zero;

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> Missing = new HashSet<string>();

        /// <summary>A card picture as a sprite, or null when it is not in the build (the card then shows without it).</summary>
        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (Sprites.TryGetValue(name, out Sprite cached) && cached != null) return cached;
            if (Missing.Contains(name)) return null;

            var asset = Resources.Load<TextAsset>(Folder + name);
            Vector4 borders = Borders(name);
            Texture2D texture = asset != null ? KitArt.Decode(asset.bytes, name, mipmaps: borders == Vector4.zero) : null;
            if (texture == null)
            {
                Missing.Add(name);
                Debug.LogWarning($"[Lazpoly] Card picture '{Folder}{name}' is missing; run Tools/Art/make_card_art.js.");
                return null;
            }

            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, borders);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            Sprites[name] = sprite;
            return sprite;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sprites.Clear();
            Missing.Clear();
        }
    }
}
