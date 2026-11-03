using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The paper loading screen: cream paper over the whole window, the logo, the table's name, a bar and a
    /// line under it. The opening screen (<see cref="BootSplash"/>) and every scene change (<see cref="SceneFader"/>)
    /// draw the same one, so leaving the menu for the table looks like the game starting. It never takes the
    /// pointer; the caller decides when it shows and how full the bar is.
    /// </summary>
    public sealed class LoadingScreen
    {
        public const float BarWidth = 360f;
        public const float BarHeight = 10f;

        private readonly RectTransform _fill;
        private readonly Text _caption;

        private LoadingScreen(RectTransform fill, Text caption)
        {
            _fill = fill;
            _caption = caption;
        }

        /// <summary>Builds the screen under <paramref name="root"/>, stretched over it; without the logo picture only the paper, the name and the bar show.</summary>
        public static LoadingScreen Build(RectTransform root, float logoSize, string caption)
        {
            UiPalette paper = UiPalette.Paper;
            Image ground = UiRoot.Background(UiRoot.Stretch(root, "Paper"), paper.Panel, rounded: false);
            ground.raycastTarget = false;

            RectTransform column = UiRoot.Rect(root, "Column", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            column.sizeDelta = new Vector2(720f, logoSize + 220f);
            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var ui = new UiBuilder(column);
            if (MenuArt.HasLogo)
            {
                Image logo = ui.Swatch(Color.white, logoSize);
                logo.sprite = MenuArt.Logo;
                logo.type = Image.Type.Simple;
                logo.preserveAspect = true;
                logo.raycastTarget = false;
            }

            ui.Eyebrow(Texts.Lang.L("Karadeniz Masası"), paper.Muted, TextAnchor.MiddleCenter);

            Image track = ui.Swatch(paper.Field, BarWidth);
            track.sprite = UiSprites.Pill;
            track.raycastTarget = false;
            UiBuilder.Fix(track, BarWidth, BarHeight);
            RectTransform fill = UiRoot.Rect(track.rectTransform, "Fill", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
            Image bar = UiRoot.Background(fill, paper.PrimaryFill);
            bar.sprite = UiSprites.Pill;
            bar.raycastTarget = false;
            fill.pivot = new Vector2(0f, 0.5f);
            fill.sizeDelta = Vector2.zero;

            Text line = ui.Label(caption, UiTextStyle.Small, paper.Muted, TextAnchor.MiddleCenter);
            line.raycastTarget = false;
            return new LoadingScreen(fill, line);
        }

        /// <summary>Fills the bar to <paramref name="progress"/> (0..1).</summary>
        public void SetProgress(float progress) => _fill.sizeDelta = new Vector2(BarWidth * Mathf.Clamp01(progress), 0f);

        public void SetCaption(string caption) => _caption.text = caption;
    }
}
