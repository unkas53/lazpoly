using System;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>What the home screen's buttons do; the menu controller fills it in.</summary>
    public sealed class MenuHomeActions
    {
        public Action Play;
        public Action Host;
        public Action Join;
        public Action Single;
        public Action Local;
        public Action Characters;
        public Action HowToPlay;
        public Action Settings;
        public Action ToggleSound;
    }

    /// <summary>
    /// The main menu's home screen in the owner's kit (see <see cref="MenuLayout"/>): the profile card, the logo and
    /// tagline, the play buttons, the bottom row and the line of the board's dearest towns, laid on a 1920x1080
    /// design frame. It shows what the controller tells it; the line under the buttons (<see cref="InfoArea"/>) is
    /// the controller's to fill, since what it offers depends on the save and the last room.
    /// </summary>
    public sealed class MenuHome
    {
        private readonly Text _initial;
        private readonly Image _photo;
        private readonly Text _name;
        private readonly Text _caption;
        private readonly Text _status;
        private readonly Image _soundIcon;
        private bool? _muted;

        public MenuHome(RectTransform frame, MenuHomeActions actions, string towns)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            // Profile card: a navy disc with the name's first letter (or the Steam profile picture inside its brass rim), the name and the character.
            MenuKit.Picture(At(frame, "Profile Card", MenuLayout.Profile), MenuKit.Chip, Color.white);
            MenuKit.Picture(At(frame, "Profile Disc", MenuLayout.ProfileDisc), MenuKit.Circle, Color.white);
            _initial = MenuKit.Words(At(frame, "Profile Initial", MenuLayout.ProfileDisc), "", 25, MenuKit.Cream, bold: true, TextAnchor.MiddleCenter);
            _photo = Photo(At(frame, "Profile Photo", MenuLayout.ProfilePhoto));
            _name = MenuKit.Words(At(frame, "Player Name", MenuLayout.ProfileName), "", 21, MenuKit.Ink, bold: true);
            _caption = MenuKit.Words(At(frame, "Character Caption", MenuLayout.ProfileCaption), "", 14, MenuKit.Muted);

            Button sound = MenuKit.IconButtonAt(frame, "Sound", MenuLayout.Sound, "Icon_Sound", actions.ToggleSound);
            _soundIcon = sound.transform.Find("Icon").GetComponent<Image>();
            MenuKit.IconButtonAt(frame, "Settings Top", MenuLayout.SettingsTop, "Icon_Gear", actions.Settings);

            MenuKit.Picture(At(frame, "Logo", MenuLayout.Logo), MenuKit.Logo, Color.white, keepAspect: true);
            MenuKit.Words(At(frame, "Tagline", MenuLayout.Tagline), L("Dünya senin oyun tahtan."), 32, MenuKit.Ink, bold: true);
            MenuKit.Words(At(frame, "Subtitle", MenuLayout.Subtitle), L("Şehirleri keşfet. Hamleni yap."), 20, MenuKit.Muted);

            MenuKit.ButtonAt(frame, "Play", MenuLayout.Play, MenuKit.Skin.Primary, "Icon_Dice", L("HEMEN OYNA"), 27, actions.Play, "Icon_Arrow");
            MenuKit.ButtonAt(frame, "Host", MenuLayout.Host, MenuKit.Skin.Secondary, "Icon_Plus", L("ODA KUR"), 21, actions.Host);
            MenuKit.ButtonAt(frame, "Join", MenuLayout.Join, MenuKit.Skin.Secondary, "Icon_Join", L("ODAYA KATIL"), 21, actions.Join);
            MenuKit.ButtonAt(frame, "Single", MenuLayout.Single, MenuKit.Skin.Secondary, "Icon_Dice", L("TEK OYUNCULU"), 21, actions.Single);
            MenuKit.ButtonAt(frame, "Local", MenuLayout.Local, MenuKit.Skin.Secondary, "Icon_People", L("AYNI BİLGİSAYARDA"), 21, actions.Local);

            InfoArea = At(frame, "Info", MenuLayout.Info);

            MenuKit.ButtonAt(frame, "Characters", MenuLayout.Characters, MenuKit.Skin.Secondary, "Icon_Case", L("Karakterler"), 21, actions.Characters);
            MenuKit.ButtonAt(frame, "How To Play", MenuLayout.HowToPlay, MenuKit.Skin.Secondary, "Icon_Book", L("Nasıl oynanır"), 21, actions.HowToPlay);
            MenuKit.ButtonAt(frame, "Settings Bottom", MenuLayout.SettingsBottom, MenuKit.Skin.Secondary, "Icon_Gear", L("Ayarlar"), 21, actions.Settings);

            MenuKit.Picture(At(frame, "World Icon", MenuLayout.WorldIcon), "Icon_Globe", MenuKit.Ink);
            MenuKit.Words(At(frame, "World Title", MenuLayout.WorldTitle), L("İSTANBUL'DAN DÜNYAYA"), 21, MenuKit.Ink, bold: true);
            MenuKit.Words(At(frame, "World Towns", MenuLayout.WorldTowns), towns ?? "", 17, MenuKit.Muted);
            _status = MenuKit.Words(At(frame, "Status", MenuLayout.Status), "", 14, MenuKit.Muted);
        }

        /// <summary>The space under the play buttons, a column the controller renders into.</summary>
        public RectTransform InfoArea { get; }

        /// <summary>The name and character on the profile card.</summary>
        public void SetProfile(string playerName, string character)
        {
            string name = string.IsNullOrWhiteSpace(playerName) ? L("Oyuncu") : playerName.Trim();
            Set(_name, name);
            Set(_initial, LobbyPanel.Initial(name));
            Set(_caption, F($"Karakter: {character}"));
        }

        /// <summary>The person's profile picture in the disc, in place of the letter; null goes back to the letter.</summary>
        public void SetPhoto(Sprite photo)
        {
            if (_photo.sprite == photo && _photo.gameObject.activeSelf == (photo != null)) return;
            _photo.sprite = photo;
            _photo.gameObject.SetActive(photo != null);
            _initial.gameObject.SetActive(photo == null);
        }

        /// <summary>Whether the profile card shows a picture rather than the letter.</summary>
        public bool ShowsPhoto => _photo.gameObject.activeSelf;

        /// <summary>A round window for the picture: a circle mask (not drawn) with the picture filling it; hidden until there is one.</summary>
        private static Image Photo(RectTransform rect)
        {
            var mask = rect.gameObject.AddComponent<Image>();
            mask.sprite = UiSprites.Circle;
            mask.raycastTarget = false;
            rect.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform inner = UiRoot.Stretch(rect, "Picture");
            var photo = inner.gameObject.AddComponent<Image>();
            photo.raycastTarget = false;
            photo.preserveAspect = false;
            rect.gameObject.SetActive(false);
            return photo;
        }

        /// <summary>The sound button shows a struck speaker while the game is silent.</summary>
        public void SetMuted(bool muted)
        {
            if (_muted == muted) return;
            _muted = muted;
            Sprite icon = MenuKit.Get(muted ? "Icon_Mute" : "Icon_Sound");
            if (icon != null) _soundIcon.sprite = icon;
        }

        /// <summary>Steam's state and the version, under the towns.</summary>
        public void SetStatus(string status) => Set(_status, status ?? "");

        private static void Set(Text text, string value)
        {
            if (text.text != value) text.text = value;
        }

        private static RectTransform At(RectTransform frame, string name, Rect design) =>
            MenuKit.Place(frame, name, design.x, design.y, design.width, design.height);
    }
}
