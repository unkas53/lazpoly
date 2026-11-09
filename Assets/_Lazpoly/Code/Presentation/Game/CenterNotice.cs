using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// A card in the middle of the screen that stays up for <see cref="Seconds"/> and fades out over the last
    /// <see cref="FadeOutSeconds"/> (a trade's result, a player who cannot pay; the owner's wish, Faz 23). The table
    /// waits the same time, so play goes on only once the card is gone. A card can stay up for another time (the trade
    /// agreement card, 4 s). It never takes the mouse, and the roll button waits until it has closed.
    /// The card is drawn as a playing card (the owner's wish, 2026-11-09): a cream face with a soft shadow, the card's
    /// picture on top in its colour family (<see cref="CardArt"/>), a small family line, the title in the family's colour
    /// and the text under it in large dark letters. It grows with its text, so nothing is cut and no space is left over.
    /// </summary>
    public sealed class CenterNotice
    {
        public const float Seconds = 5f;
        public const float FadeInSeconds = 0.15f;
        public const float FadeOutSeconds = 0.5f;

        /// <summary>The card's width in interface units (a 1080p screen's pixels), its inner margin and the gap between its lines.</summary>
        public const float Width = 560f;
        public const int Padding = 20;
        public const float Gap = 8f;

        /// <summary>The picture's height: the band is 8:3, as Tools/Art/make_card_art.js draws it.</summary>
        public const float PictureHeight = (Width - 2f * Padding) * 3f / 8f;

        /// <summary>The letter sizes: the title, the text, the family line.</summary>
        public const int TitleSize = 34;
        public const int BodySize = 24;
        public const int FamilySize = 15;

        /// <summary>The dark ink of the text on the cream face; it reads the same in every colour setting.</summary>
        public static readonly Color Ink = new Color(0.17f, 0.15f, 0.14f);

        /// <summary>How far above the screen's middle the card's middle sits; the roll button hides while a card is up.</summary>
        public const float Lift = 20f;

        private readonly CanvasGroup _group;
        private readonly RectTransform _rect;

        /// <summary>How long this card stays up.</summary>
        public float Lasts { get; }

        public Text Title { get; }
        public Text Body { get; }

        /// <summary>The small line over the title ("SALDIRI KARTI · Ayşe"); hidden while empty.</summary>
        public Text Family { get; }

        /// <summary>The card's picture; hidden while it has none.</summary>
        public Image Picture { get; }

        public RectTransform Rect => _rect;

        public CenterNotice(Transform canvas, string name, float seconds = Seconds)
        {
            Lasts = seconds;
            _rect = UiRoot.Rect(canvas, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _rect.pivot = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = new Vector2(0f, Lift);
            _rect.sizeDelta = new Vector2(Width, 0f);
            _group = _rect.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;

            var column = _rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(Padding, Padding, Padding, Padding + 6);
            column.spacing = Gap;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            _rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // The shadow and the face stretch with the card behind its lines; the layout does not place them.
            Backdrop("Shadow", CardArt.Shadow, new Vector2(-46f, -58f), new Vector2(46f, 34f), new Color(1f, 1f, 1f, 0.9f));
            Image face = Backdrop("Face", CardArt.Face, Vector2.zero, Vector2.zero, Color.white);
            if (face.sprite == null)
            {
                face.sprite = UiSprites.Rounded;
                face.type = Image.Type.Sliced;
                face.color = new Color(1f, 0.99f, 0.95f);
            }

            RectTransform picture = UiRoot.Rect(_rect, "Picture", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            Picture = picture.gameObject.AddComponent<Image>();
            Picture.preserveAspect = true;
            Picture.raycastTarget = false;
            var size = picture.gameObject.AddComponent<LayoutElement>();
            size.preferredHeight = PictureHeight;
            size.minHeight = PictureHeight;
            picture.gameObject.SetActive(false);

            Family = Line("Family", FamilySize, UiTheme.BoldFont, CardArt.ToneColor(CardTone.Grey));
            Family.gameObject.SetActive(false);
            Title = Line("Title", TitleSize, UiTheme.BlackFont, CardArt.ToneColor(CardTone.Defence));
            Body = Line("Body", BodySize, UiTheme.Font, Ink);
            Body.lineSpacing = 1.08f;
        }

        private Image Backdrop(string name, string picture, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            RectTransform rect = UiRoot.Rect(_rect, name, Vector2.zero, Vector2.one, offsetMin, offsetMax);
            rect.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            image.sprite = CardArt.Get(picture);
            if (image.sprite != null) image.type = Image.Type.Sliced;
            return image;
        }

        private Text Line(string name, int size, Font font, Color color)
        {
            RectTransform rect = UiRoot.Rect(_rect, name, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = false;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>
        /// Dresses the card: its picture (none hides the band), its colour family (the title's colour) and the small line over
        /// the title (null hides it). The title's colour may be set again after.
        /// </summary>
        public void Paint(string picture, CardTone tone, string family = null)
        {
            Sprite sprite = CardArt.Get(picture);
            Picture.sprite = sprite;
            if (Picture.gameObject.activeSelf != (sprite != null)) Picture.gameObject.SetActive(sprite != null);
            Color colour = CardArt.ToneColor(tone);
            Title.color = colour;
            Family.color = colour;
            Family.text = family ?? "";
            bool showFamily = !string.IsNullOrEmpty(family);
            if (Family.gameObject.activeSelf != showFamily) Family.gameObject.SetActive(showFamily);
        }

        /// <summary>True while a card shown <paramref name="age"/> seconds ago is still on screen.</summary>
        public static bool Showing(float age) => Showing(age, Seconds);

        /// <summary>True while a card that lasts <paramref name="seconds"/> and was shown <paramref name="age"/> seconds ago is still on screen.</summary>
        public static bool Showing(float age, float seconds) => age >= 0f && age < seconds;

        /// <summary>How opaque the card is <paramref name="age"/> seconds after it went up: a quick fade in, fully there, then the last half second fading out.</summary>
        public static float Alpha(float age) => Alpha(age, Seconds);

        /// <summary>The same for a card that lasts <paramref name="seconds"/>.</summary>
        public static float Alpha(float age, float seconds) =>
            Showing(age, seconds) ? Mathf.Clamp01(age / FadeInSeconds) * Mathf.Clamp01((seconds - age) / FadeOutSeconds) : 0f;

        /// <summary>Whether the card is drawn at all now.</summary>
        public bool Shown => _group.alpha > 0f;

        public void Hide() => _group.alpha = 0f;

        public void Show(float age)
        {
            _group.alpha = Alpha(age, Lasts);
            _rect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Ease.OutBack(Mathf.Clamp01(age / 0.3f)));
        }
    }
}
