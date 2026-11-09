using System;
using System.Collections.Generic;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Game
{
    /// <summary>How a player's corner card looks: resting, on turn (gold edge, the pill and the clock line) or out of the game.</summary>
    public enum PlayerCardState { Normal, Active, Bankrupt }

    /// <summary>One small house on a card: a colour group the player owns a town in, and the town a click opens.</summary>
    public readonly struct PlayerCardPip
    {
        public PlayerCardPip(Color color, int square)
        {
            Color = color;
            Square = square;
        }

        public Color Color { get; }
        public int Square { get; }
    }

    /// <summary>What a player's corner card shows; <see cref="PlayerCardView"/> draws it.</summary>
    public sealed class PlayerCardModel
    {
        public string Name = "";

        /// <summary>The seat number on the tab under the face, "01" to "08".</summary>
        public string Number = "01";

        public Color Color = Color.white;
        public PlayerCardState State;

        /// <summary>The face in the frame: a Steam profile picture or a character portrait; null shows <see cref="Initial"/> on the player's colour.</summary>
        public Sprite Face;

        public string Initial = "?";

        /// <summary>The words on the dark pill of the card on turn ("SIRA SENDE"); a bankrupt card says İFLAS by itself.</summary>
        public string Pill = "";

        /// <summary>A short line in the pill's place on a resting card: who plays the seat or how the connection is.</summary>
        public string Status;

        /// <summary>Online, a dot before <see cref="Status"/>: green for a reachable seat, brick for a lost one.</summary>
        public Color? StatusDot;

        public IReadOnlyList<PlayerCardPip> Pips = Array.Empty<PlayerCardPip>();

        /// <summary>How many strategic cards the player holds (Faz 30): a small card back each under the face; hands are open to all.</summary>
        public int Cards;
    }

    /// <summary>
    /// A player's card in a corner of the table, in the owner's player card kit (320 by 112 design units, scaled to
    /// the corner's width): the face in a frame on the dark left panel with the seat number under it, the name, the
    /// money after the Lazpoly coin, a house per colour group owned, and on turn a "SIRA SENDE" pill and a clock line.
    /// A bankrupt player's card goes grey and says İFLAS. The money label and the background are handed back so the
    /// table can count the money and flash the card without a rebuild.
    /// </summary>
    public sealed class PlayerCardView
    {
        public const float DesignWidth = 320f;
        public const float DesignHeight = 112f;

        /// <summary>At most this many houses fit on the card's strip.</summary>
        public const int MaxPips = 8;

        public const string NormalCard = "Card_Normal";
        public const string ActiveCard = "Card_Active";
        public const string BankruptCard = "Card_Bankrupt";

        /// <summary>The player card kit's pictures, all shipped under Resources/Lazpoly/Art/Menu.</summary>
        public static readonly string[] Pictures =
        {
            NormalCard, ActiveCard, BankruptCard, "Avatar_Disc", "Avatar_Frame", "Player_Stripe", "Currency_L", "Lazpoly_Mark",
            "Property_Pip", "Turn_Pill", "Timer_Line", "Number_Tab",
        };

        /// <summary>A bankrupt player's parts: a cool grey instead of their colour.</summary>
        public static readonly Color Muted = new Color32(141, 158, 163, 255);

        private static readonly Color Ink = new Color32(23, 61, 80, 255);
        private static readonly Color NameInk = new Color32(56, 85, 99, 255);
        private static readonly Color BankruptInk = new Color32(114, 133, 140, 255);
        private static readonly Color PillInk = new Color32(241, 216, 153, 255);
        private static readonly Color Seal = new Color32(173, 166, 126, 255);
        private static readonly Color TrackColor = new Color32(229, 217, 190, 255);
        private static readonly Color TimeColor = new Color32(188, 141, 52, 255);

        private readonly float _scale;
        private Image _timerTrack;
        private Image _timerFill;

        private PlayerCardView(RectTransform root, float scale)
        {
            Root = root;
            _scale = scale;
        }

        public RectTransform Root { get; }

        /// <summary>The card's skin; the table tints it for a moment when the money moves.</summary>
        public Image Background { get; private set; }

        /// <summary>The money, which the table counts up and down; null on a bankrupt card.</summary>
        public Text Balance { get; private set; }

        /// <summary>The face in the frame: the profile picture or portrait, null when the initial stands in.</summary>
        public Image Face { get; private set; }

        /// <summary>The houses on the strip, one per group, in the order given.</summary>
        public IReadOnlyList<Image> Pips => _pips;

        private readonly List<Image> _pips = new List<Image>();

        /// <summary>
        /// Draws <paramref name="model"/> as the last child of <paramref name="parent"/> (a column), <paramref name="width"/>
        /// wide. A click on a house calls <paramref name="squareClicked"/> with its town.
        /// </summary>
        public static PlayerCardView Create(RectTransform parent, float width, PlayerCardModel model, Action<int> squareClicked = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            float scale = width / DesignWidth;
            RectTransform root = UiRoot.Rect(parent, $"Player Card {model.Name}", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(root, width, DesignHeight * scale);
            var view = new PlayerCardView(root, scale);
            view.Build(model, squareClicked);
            return view;
        }

        /// <summary>The skin for a state.</summary>
        public static string CardPicture(PlayerCardState state) =>
            state == PlayerCardState.Bankrupt ? BankruptCard : state == PlayerCardState.Active ? ActiveCard : NormalCard;

        /// <summary>
        /// A house per colour group the player owns a town in, in board order, each opening the group's first town
        /// they own; at most <see cref="MaxPips"/>.
        /// </summary>
        public static List<PlayerCardPip> GroupsOf(GameState state, int playerId, Func<int, int> groupOf, Func<int, Color> colorOf)
        {
            var pips = new List<PlayerCardPip>();
            var seen = new HashSet<int>();
            for (int square = 0; square < state.SquareCount && pips.Count < MaxPips; square++)
            {
                if (state.GetOwner(square) != playerId || !seen.Add(groupOf(square))) continue;
                pips.Add(new PlayerCardPip(colorOf(square), square));
            }

            return pips;
        }

        /// <summary>The seat number as the tab shows it: two digits.</summary>
        public static string NumberOf(int seat) => (seat + 1).ToString("D2");

        /// <summary>The clock line on the card on turn: the share of the time left, or null to hide it (no clock, or nobody here waited on).</summary>
        public void SetTime(float? left)
        {
            if (_timerTrack == null) return;
            bool show = left.HasValue;
            if (_timerTrack.gameObject.activeSelf != show) _timerTrack.gameObject.SetActive(show);
            if (_timerFill.gameObject.activeSelf != show) _timerFill.gameObject.SetActive(show);
            if (show) _timerFill.fillAmount = Mathf.Clamp01(left.Value);
        }

        private void Build(PlayerCardModel model, Action<int> squareClicked)
        {
            bool bankrupt = model.State == PlayerCardState.Bankrupt;
            bool active = model.State == PlayerCardState.Active;
            Color own = bankrupt ? Muted : model.Color;

            Background = Picture("Background", 0f, 0f, DesignWidth, DesignHeight, CardPicture(model.State), Color.white);
            Background.raycastTarget = true; // The card stops clicks from reaching the board under it.
            if (Background.sprite == null) Background.color = UiTheme.Card;

            Picture("Player Ribbon", 1f, 20f, 4f, 60f, "Player_Stripe", own);

            // The face: a disc in the player's colour clips the picture; the frame goes over both.
            Image disc = Picture("Avatar", 12f, 14f, 56f, 56f, "Avatar_Disc", own);
            if (disc.sprite == null) disc.sprite = UiSprites.Circle;
            disc.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (model.Face != null)
            {
                Face = Picture("Portrait", 0f, 0f, 56f, 56f, null, new Color(1f, 1f, 1f, bankrupt ? 0.55f : 1f), disc.transform);
                Face.sprite = model.Face;
                Face.preserveAspect = false;
            }
            else
            {
                Words("Initials", 0f, 0f, 56f, 56f, model.Initial, 22, Color.white, UiTheme.BoldFont, TextAnchor.MiddleCenter, disc.transform);
            }

            Picture("Avatar Frame", 12f, 14f, 56f, 56f, "Avatar_Frame", Color.white);
            Picture("Number Tab", 30f, 66f, 20f, 12f, "Number_Tab", own);
            Words("Number", 30f, 66f, 20f, 12f, model.Number, 8, Color.white, UiTheme.BoldFont, TextAnchor.MiddleCenter);
            BuildCards(bankrupt ? 0 : model.Cards); // Over the face's frame.

            Shrinking(Words("Name", 94f, 12f, 136f, 22f, model.Name, 14, bankrupt ? Muted : NameInk, UiTheme.BoldFont), 10);

            if (bankrupt)
            {
                Words("Bankrupt", 94f, 34f, 200f, 35f, Lang.Upper(Lang.L("iflas")), 27, BankruptInk, UiTheme.BlackFont);
            }
            else
            {
                Picture("Currency", 93f, 41f, 20.4f, 20.4f, "Currency_L", Color.white).preserveAspect = true;
                Balance = Shrinking(Words("Balance", 121f, 34f, 183f, 35f, "", 27, Ink, UiTheme.BlackFont), 16);
                BuildPips(model.Pips, squareClicked);
            }

            Picture("Lazpoly Seal", 284f, 83f, 13.2f, 13.2f, "Lazpoly_Mark", Seal).preserveAspect = true;

            if (active)
            {
                Image pill = Picture("Turn Pill", 235f, 13f, 73f, 22f, "Turn_Pill", Ink);
                if (pill.sprite == null) pill.sprite = UiSprites.Pill;
                Shrinking(Words("Label", 4f, 0f, 65f, 22f, model.Pill, 9, PillInk, UiTheme.BoldFont, TextAnchor.MiddleCenter, pill.transform), 6);
                _timerTrack = Picture("Turn Time Track", 89f, 104f, 210f, 2.4f, "Timer_Line", TrackColor);
                _timerFill = Picture("Turn Time", 89f, 104f, 210f, 2.4f, "Timer_Line", TimeColor);
                if (_timerFill.sprite == null) _timerTrack.sprite = _timerFill.sprite = UiSprites.Rounded;
                _timerFill.type = Image.Type.Filled;
                _timerFill.fillMethod = Image.FillMethod.Horizontal;
                _timerFill.fillOrigin = 0;
                SetTime(null);
            }
            else if (!bankrupt && !string.IsNullOrEmpty(model.Status))
            {
                float left = 200f;
                if (model.StatusDot.HasValue)
                {
                    Image dot = Picture("Connection", left, 20f, 8f, 8f, null, model.StatusDot.Value);
                    dot.sprite = UiSprites.Circle;
                    left += 12f;
                }

                Shrinking(Words("Status", left, 12f, 308f - left, 22f, model.Status, 10, UiTheme.Muted, UiTheme.Font, TextAnchor.MiddleRight), 7);
            }
        }

        /// <summary>The colour of the small card backs under the face: the Şans squares' violet with a cream edge.</summary>
        public static readonly Color CardBackColor = new Color(0.42f, 0.32f, 0.7f);
        public static readonly Color CardEdgeColor = new Color(0.97f, 0.93f, 0.82f);

        /// <summary>The badge never shows more than this (the hand's limit).</summary>
        public const int MaxCards = 4;

        /// <summary>The number on the hand's badge, 0 when there is none; for tests.</summary>
        public int CardBacks { get; private set; }

        /// <summary>
        /// A small violet card on the face's lower right corner with the number of cards held, only while the player holds
        /// any. A row of card backs under the seat number covered the kit's LAZPOLY print, so the hand sits on the face.
        /// </summary>
        private void BuildCards(int count)
        {
            CardBacks = Mathf.Clamp(count, 0, MaxCards);
            if (CardBacks == 0) return;
            Image edge = Picture("Hand Badge", 55f, 49f, 16f, 22f, null, CardEdgeColor);
            edge.sprite = UiSprites.Rounded;
            Image back = Picture("Back", 1.5f, 1.5f, 13f, 19f, null, CardBackColor, edge.transform);
            back.sprite = UiSprites.Rounded;
            Words("Hand Count", 0f, 0f, 16f, 22f, CardBacks.ToString(), 12, Color.white, UiTheme.BoldFont, TextAnchor.MiddleCenter, edge.transform);
        }

        /// <summary>A house per group on the strip; each has a wider invisible pad around it so it is easy to click.</summary>
        private void BuildPips(IReadOnlyList<PlayerCardPip> pips, Action<int> squareClicked)
        {
            int count = Mathf.Min(pips?.Count ?? 0, MaxPips);
            for (int i = 0; i < count; i++)
            {
                PlayerCardPip pip = pips[i];
                Image pad = Picture($"Property Group {i}", 93f + i * 20f, 79f, 20f, 19.6f, null, Color.clear);
                pad.raycastTarget = squareClicked != null;
                if (squareClicked != null) pad.gameObject.AddComponent<SquareClickTarget>().Bind(pip.Square, squareClicked);
                Image house = Picture("House", 4.8f, 5f, 10.4f, 9.6f, "Property_Pip", pip.Color, pad.transform);
                if (house.sprite == null) house.sprite = UiSprites.Rounded;
                _pips.Add(house);
            }
        }

        /// <summary>A rectangle at a design position from the top left of <paramref name="parent"/>, in design units (parts inside a part too).</summary>
        private RectTransform Place(string name, float x, float y, float w, float h, Transform parent)
        {
            float s = _scale;
            RectTransform rect = UiRoot.Rect(parent, name, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * s, -y * s);
            rect.sizeDelta = new Vector2(w * s, h * s);
            return rect;
        }

        /// <summary>A kit picture at a design position; null <paramref name="picture"/> leaves a plain (or clear) block.</summary>
        private Image Picture(string name, float x, float y, float w, float h, string picture, Color color, Transform parent = null)
        {
            parent = parent != null ? parent : Root;
            RectTransform rect = Place(name, x, y, w, h, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = picture != null ? KitArt.Get(picture) : null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Words(string name, float x, float y, float w, float h, string value, int size, Color color, Font font,
            TextAnchor alignment = TextAnchor.MiddleLeft, Transform parent = null)
        {
            parent = parent != null ? parent : Root;
            RectTransform rect = Place(name, x, y, w, h, parent);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value ?? "";
            text.fontSize = Mathf.Max(6, Mathf.RoundToInt(size * _scale));
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = false;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        /// <summary>Lets a line shrink (down to <paramref name="min"/> design points) rather than spill when the words are long.</summary>
        private Text Shrinking(Text text, int min)
        {
            text.resizeTextForBestFit = true;
            text.resizeTextMaxSize = text.fontSize;
            text.resizeTextMinSize = Mathf.Max(6, Mathf.Min(text.fontSize, Mathf.RoundToInt(min * _scale)));
            return text;
        }
    }
}
