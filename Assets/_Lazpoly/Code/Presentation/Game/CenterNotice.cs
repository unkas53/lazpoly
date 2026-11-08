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
    /// agreement card, 4 s). It never takes the mouse.
    /// </summary>
    public sealed class CenterNotice
    {
        public const float Seconds = 5f;
        public const float FadeInSeconds = 0.15f;
        public const float FadeOutSeconds = 0.5f;

        private readonly CanvasGroup _group;
        private readonly RectTransform _rect;

        /// <summary>How long this card stays up.</summary>
        public float Lasts { get; }

        public Text Title { get; }
        public Text Body { get; }

        public CenterNotice(Transform canvas, string name, float seconds = Seconds)
        {
            Lasts = seconds;
            _rect = UiRoot.Rect(canvas, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300f, -70f), new Vector2(300f, 170f));
            UiRoot.Panel(_rect).raycastTarget = false;
            _group = _rect.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;
            _group.alpha = 0f;
            UiBuilder.AddColumnLayout(_rect.gameObject, 18, 8f);
            var builder = new UiBuilder(_rect);
            Title = builder.Label("", UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleCenter);
            Body = builder.Label("", UiTextStyle.Body, UiTheme.Text, TextAnchor.MiddleCenter);
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

        public void Hide() => _group.alpha = 0f;

        public void Show(float age)
        {
            _group.alpha = Alpha(age, Lasts);
            _rect.localScale = Vector3.one * Mathf.Lerp(0.8f, 1f, Ease.OutBack(Mathf.Clamp01(age / 0.3f)));
        }
    }
}
