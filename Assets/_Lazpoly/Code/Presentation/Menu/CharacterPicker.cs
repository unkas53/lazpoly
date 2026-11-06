using System;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The lobby's character choice, drawn with <see cref="UiBuilder"/>: the characters' faces side by side, the
    /// picked one on the dark selected skin, the ones another player holds dimmed and locked, the rest a click away.
    /// Also the round face that stands for a character on a seat.
    /// </summary>
    public static class CharacterPicker
    {
        /// <summary>A face's size in the strip.</summary>
        public const float FaceSize = 58f;

        /// <summary>A tile's width: wide enough for the longest name ("Necdet Bey") in the small bold letters.</summary>
        public const float TileWidth = 100f;

        /// <summary>How see-through a character another player holds is drawn.</summary>
        public const float TakenAlpha = 0.35f;

        /// <summary>Where a character stands for one seat.</summary>
        public enum Mark { Free, Picked, Taken }

        /// <summary>
        /// The characters in a row of tiles: a click on a free one picks it for the seat.
        /// </summary>
        /// <param name="picked">The seat's character now.</param>
        /// <param name="takenBy">Who holds a character, by id; null or empty when nobody else does.</param>
        public static void Render(UiBuilder ui, string picked, Func<string, string> takenBy, Action<string> pick, float size = FaceSize)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            UiBuilder row = ui.Row(6f, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            var holders = new string[CharacterChoice.All.Count];
            bool anyTaken = false;
            for (int i = 0; i < holders.Length; i++)
            {
                string id = CharacterChoice.All[i].Id;
                holders[i] = id == picked ? null : takenBy?.Invoke(id);
                anyTaken |= holders[i] != null;
            }

            for (int i = 0; i < holders.Length; i++)
            {
                CharacterOption option = CharacterChoice.All[i];
                string holder = holders[i];
                Mark mark = MarkOf(option.Id, picked, holder);
                UiBuilder tile = row.Tile(mark == Mark.Free ? () => pick?.Invoke(option.Id) : (Action)null,
                    mark == Mark.Picked ? UiTheme.Fill(UiButtonStyle.Selected) : (Color?)null, 4);
                tile.Container.name = $"Character {option.Id}";
                tile.Container.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                Button button = tile.Container.GetComponent<Button>();
                if (button != null) button.interactable = mark != Mark.Taken;
                Face(Centred(tile), option, size, mark == Mark.Taken ? TakenAlpha : 1f);
                Text name = tile.Label(option.Name, UiTextStyle.Small, mark == Mark.Picked ? Color.white : UiTheme.Text, TextAnchor.MiddleCenter);
                name.font = UiTheme.BoldFont;
                name.fontSize = NameSize;
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiBuilder.Fix(tile.Container.gameObject, width: Mathf.Max(TileWidth, size + 22f));
                // While someone holds a character, every tile keeps a line for the holder's name, so the row stays level.
                if (!anyTaken) continue;
                Text line = tile.Label(mark == Mark.Taken ? Short(string.IsNullOrEmpty(holder) ? L("Alındı") : holder) : " ", UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
                line.fontSize = NameSize;
                line.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
        }

        /// <summary>The characters' names under their faces, a little under the small text so "Necdet Bey" fits.</summary>
        public const int NameSize = 15;

        /// <summary>The longest holder's name a tile shows in full.</summary>
        public const int HolderLetters = 10;

        /// <summary>A holder's name cut to fit under a tile: "Oyuncu 2" stays, a long Steam name ends in "…".</summary>
        public static string Short(string name) =>
            name == null || name.Length <= HolderLetters ? name : name.Substring(0, HolderLetters - 1).TrimEnd() + "…";

        /// <summary>How a character shows for a seat that holds <paramref name="picked"/> while <paramref name="holder"/> (if anyone) holds it.</summary>
        public static Mark MarkOf(string id, string picked, string holder) =>
            id == picked ? Mark.Picked : holder != null ? Mark.Taken : Mark.Free;

        /// <summary>A character's face in a round window on a disc of <paramref name="ring"/>; the name's initial when its picture is missing.</summary>
        public static Image Face(UiBuilder ui, CharacterOption option, float size, float alpha = 1f, Color? ring = null)
        {
            if (ui == null) throw new ArgumentNullException(nameof(ui));
            Color disc = ring ?? (option != null ? Color.Lerp(option.Color, Color.white, 0.55f) : UiTheme.RestingFill);
            disc.a *= alpha;
            Sprite picture = option != null ? KitArt.Get(option.Face) : null;
            Image window = ui.Swatch(disc, size, round: true, mark: picture == null ? LobbyPanel.Initial(option?.Name) : null);
            window.gameObject.name = "Face";
            if (picture == null) return window;

            window.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            RectTransform inside = UiRoot.Stretch(window.transform, "Picture");
            var image = inside.gameObject.AddComponent<Image>();
            image.sprite = picture;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(1f, 1f, 1f, alpha);
            return image;
        }

        /// <summary>A row inside <paramref name="ui"/> that keeps what is put in it at its own size, in the middle.</summary>
        public static UiBuilder Centred(UiBuilder ui)
        {
            UiBuilder row = ui.Row(0f, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            return row;
        }
    }
}
