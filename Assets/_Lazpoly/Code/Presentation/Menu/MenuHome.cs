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
    /// The main menu's home screen (see <see cref="MenuLayout"/>; the owner's wish, 2026-11-21: a main menu that
    /// suits the game, with animation). On the left design frame the navy rail (<see cref="MenuRail"/>) with the
    /// profile, the logo hovering in its light, the tagline, the gold button that starts a game at once and the navy
    /// ones for the other ways in; on the right design frame the small ivory buttons at the top and the line of the
    /// board's dearest towns at the bottom. The parts come in one after another when the screen is first seen
    /// (<see cref="UiEntrance"/>). It shows what the controller tells it; the line under the buttons
    /// (<see cref="InfoArea"/>) is the controller's to fill, since what it offers depends on the save and the last room.
    /// </summary>
    public sealed class MenuHome
    {
        /// <summary>Where the rail's lights stand: the logo's middle.</summary>
        public static Vector2 Light => MenuLayout.Logo.center;

        /// <summary>The glass of the profile card and of the offer under the buttons: a lighter navy in a thin gold line.</summary>
        public static readonly Color Glass = new Color(0.118f, 0.212f, 0.459f, 1f);
        public static readonly Color GlassLine = new Color(0.949f, 0.780f, 0.361f, 0.6f);

        /// <summary>How far aside a part of the rail starts, and one of the right side.</summary>
        private static readonly Vector2 FromLeft = new Vector2(-46f, 0f);
        private static readonly Vector2 FromAbove = new Vector2(0f, 34f);
        private static readonly Vector2 FromBelow = new Vector2(0f, -24f);

        private readonly Text _initial;
        private readonly Image _photo;
        private readonly GameObject _photoWindow;
        private readonly Text _name;
        private readonly Text _caption;
        private readonly Text _status;
        private readonly Image _soundIcon;
        private readonly GameObject _infoGlass;
        private bool? _muted;

        /// <param name="left">The design frame kept at the screen's left edge: the rail and what stands on it.</param>
        /// <param name="right">The design frame kept at the screen's right edge: the top buttons and the towns' line.</param>
        public MenuHome(RectTransform left, RectTransform right, MenuHomeActions actions, string towns)
        {
            if (left == null) throw new ArgumentNullException(nameof(left));
            if (right == null) throw new ArgumentNullException(nameof(right));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            MenuRail.Build(left, Light);
            int order = 0;

            // Profile: a glass pill with a navy disc holding the name's first letter (or the Steam profile picture in its gold rim), the name and the character.
            RectTransform profile = At(left, "Profile", MenuLayout.Profile);
            Pane(profile, MenuLayout.Profile.height / 2f);
            MenuKit.Picture(In(profile, "Profile Disc", MenuLayout.Profile, MenuLayout.ProfileDisc), MenuKit.Circle, Color.white);
            _initial = MenuKit.Words(In(profile, "Profile Initial", MenuLayout.Profile, MenuLayout.ProfileDisc), "", 25, MenuKit.Cream, bold: true, TextAnchor.MiddleCenter);
            RectTransform window = In(profile, "Profile Photo", MenuLayout.Profile, MenuLayout.ProfilePhoto);
            _photo = Photo(window);
            _photoWindow = window.gameObject;
            _name = MenuKit.Words(In(profile, "Player Name", MenuLayout.Profile, MenuLayout.ProfileName), "", 21, MenuKit.Cream, bold: true);
            _caption = MenuKit.Words(In(profile, "Character Caption", MenuLayout.Profile, MenuLayout.ProfileCaption), "", 14, MenuKit.OnNight);
            UiEntrance.Attach(profile, order++, FromLeft);

            // The logo hovers in the rail's light, a sheen crossing it now and then.
            RectTransform logo = At(left, "Logo", MenuLayout.Logo);
            RectTransform mark = UiRoot.Stretch(logo, "Picture");
            Image logoImage = MenuKit.Picture(mark, MenuKit.Logo, Color.white, keepAspect: true);
            if (logoImage.sprite != null) UiShine.Attach(mark, logoImage.sprite, every: 5.5f, seconds: 1f, strength: 0.38f, offset: 1.6f, like: logoImage);
            else logoImage.enabled = false;
            UiFloat.Attach(mark, 6f, 5.5f);
            UiEntrance.Attach(logo, order++, FromLeft);

            RectTransform tagline = At(left, "Tagline", MenuLayout.Tagline);
            MenuKit.Words(tagline, L("Dünya senin oyun tahtan."), 30, MenuKit.Cream, bold: true, TextAnchor.MiddleCenter);
            UiEntrance.Attach(tagline, order++, FromLeft);
            RectTransform subtitle = At(left, "Subtitle", MenuLayout.Subtitle);
            MenuKit.Words(subtitle, L("Şehirleri keşfet. Hamleni yap."), 19, MenuKit.OnNight, alignment: TextAnchor.MiddleCenter);
            UiEntrance.Attach(subtitle, order++, FromLeft);

            // The one gold button, a sheen crossing it; under it the navy ones.
            Button play = MenuKit.ButtonAt(left, "Play", MenuLayout.Play, MenuKit.Skin.Hero, "Icon_Dice", L("HEMEN OYNA"), 30, actions.Play, "Icon_Arrow");
            UiShine.Attach((RectTransform)play.transform, UiSprites.Rounded, every: 4.4f, seconds: 0.85f, strength: 0.5f, offset: 2.4f,
                inset: PlayShineInset, cornerScale: 0.6f);
            UiEntrance.Attach((RectTransform)play.transform, order++, FromLeft);
            Rail(left, "Host", MenuLayout.Host, "Icon_Plus", L("ODA KUR"), 20, actions.Host, order++);
            Rail(left, "Join", MenuLayout.Join, "Icon_Join", L("ODAYA KATIL"), 20, actions.Join, order++);
            Rail(left, "Single", MenuLayout.Single, "Icon_Dice", L("TEK OYUNCULU"), 20, actions.Single, order++);
            Rail(left, "Local", MenuLayout.Local, "Icon_People", L("AYNI BİLGİSAYARDA"), 20, actions.Local, order++);

            // Under the buttons: an offer on a glass pane (the controller says when), or the motto on the bare rail.
            RectTransform info = At(left, "Info", MenuLayout.Info);
            _infoGlass = Pane(UiRoot.Stretch(info, "Glass"), 14f).gameObject;
            _infoGlass.SetActive(false);
            InfoArea = UiRoot.Stretch(info, "Content");
            UiEntrance.Attach(info, order++, FromLeft);

            RectTransform status = At(left, "Status", MenuLayout.Status);
            _status = MenuKit.Words(status, "", 14, MenuKit.OnNight, alignment: TextAnchor.MiddleCenter);
            UiEntrance.Attach(status, order, FromLeft);

            // The top right, on the sky: the three windows and the sound.
            int side = 3;
            Side(right, "Characters", MenuLayout.Characters, "Icon_Case", L("Karakterler"), actions.Characters, side++);
            Side(right, "How To Play", MenuLayout.HowToPlay, "Icon_Book", L("Nasıl oynanır"), actions.HowToPlay, side++);
            Side(right, "Settings", MenuLayout.Settings, "Icon_Gear", L("Ayarlar"), actions.Settings, side++);
            Button sound = MenuKit.IconButtonAt(right, "Sound", MenuLayout.Sound, "Icon_Sound", actions.ToggleSound);
            _soundIcon = sound.transform.Find("Icon").GetComponent<Image>();
            UiEntrance.Attach((RectTransform)sound.transform, side, FromAbove);

            // The bottom right: the board's dearest towns, written on the sky.
            RectTransform world = At(right, "World Title", MenuLayout.WorldTitle);
            OnSky(MenuKit.Words(world, L("İSTANBUL'DAN DÜNYAYA"), 21, MenuKit.Ink, bold: true, TextAnchor.MiddleRight));
            UiEntrance.Attach(world, order, FromBelow);
            RectTransform line = At(right, "World Towns", MenuLayout.WorldTowns);
            OnSky(MenuKit.Words(line, towns ?? "", 17, MenuKit.Ink, alignment: TextAnchor.MiddleRight));
            UiEntrance.Attach(line, order + 1, FromBelow);
        }

        /// <summary>The sheen on the gold button keeps to its key: off the rim, and above the lip and the shadow under it (left, bottom, right, top).</summary>
        public static readonly Vector4 PlayShineInset = new Vector4(5f, 18f, 5f, 5f);

        /// <summary>One of the navy buttons on the rail.</summary>
        private static void Rail(RectTransform frame, string name, Rect design, string icon, string label, int size, Action onClick, int order)
        {
            Button button = MenuKit.ButtonAt(frame, name, design, MenuKit.Skin.Primary, icon, label, size, onClick);
            UiEntrance.Attach((RectTransform)button.transform, order, FromLeft);
        }

        /// <summary>One of the ivory buttons at the top right.</summary>
        private static void Side(RectTransform frame, string name, Rect design, string icon, string label, Action onClick, int order)
        {
            Button button = MenuKit.ButtonAt(frame, name, design, MenuKit.Skin.Secondary, icon, label, 19, onClick);
            UiEntrance.Attach((RectTransform)button.transform, order, FromAbove);
        }

        /// <summary>A glass pane with corners of a radius: a thin gold line round a lighter navy.</summary>
        private static RectTransform Pane(RectTransform rect, float radius)
        {
            Round(rect, GlassLine, radius);
            RectTransform inner = UiRoot.Rect(rect, "Pane", Vector2.zero, Vector2.one, new Vector2(1.5f, 1.5f), new Vector2(-1.5f, -1.5f));
            Round(inner, Glass, Mathf.Max(2f, radius - 1.5f));
            return rect;
        }

        /// <summary>A rounded shape in a colour, its corners of a radius (the pill sprite's are 16 pixels as drawn).</summary>
        private static void Round(RectTransform rect, Color color, float radius)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Pill;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 16f / Mathf.Max(1f, radius);
            image.color = color;
            image.raycastTarget = false;
        }

        /// <summary>Words written straight on the sky keep a pale edge, so a cloud or an island passing behind never swallows them.</summary>
        private static void OnSky(Text text)
        {
            var edge = text.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(1f, 0.99f, 0.95f, 0.7f);
            edge.effectDistance = new Vector2(1.5f, -1.5f);
            edge.useGraphicAlpha = true;
        }

        /// <summary>The space under the play buttons, a column the controller renders into.</summary>
        public RectTransform InfoArea { get; }

        /// <summary>Whether the space under the buttons holds an offer (on its glass pane) or only the motto (on the bare rail).</summary>
        public void SetOffer(bool offer)
        {
            if (_infoGlass.activeSelf != offer) _infoGlass.SetActive(offer);
        }

        public bool ShowsOffer => _infoGlass.activeSelf;

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
            if (_photo.sprite == photo && _photoWindow.activeSelf == (photo != null)) return;
            _photo.sprite = photo;
            _photoWindow.SetActive(photo != null); // The round window, not only the picture in it.
            _initial.gameObject.SetActive(photo == null);
        }

        /// <summary>Whether the profile card shows a picture rather than the letter.</summary>
        public bool ShowsPhoto => _photoWindow.activeSelf;

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

        /// <summary>Steam's state and the version, at the rail's foot.</summary>
        public void SetStatus(string status) => Set(_status, status ?? "");

        private static void Set(Text text, string value)
        {
            if (text.text != value) text.text = value;
        }

        private static RectTransform At(RectTransform frame, string name, Rect design) =>
            MenuKit.Place(frame, name, design.x, design.y, design.width, design.height);

        /// <summary>A part at its design place, as a child of another part that stands at <paramref name="holder"/>.</summary>
        private static RectTransform In(RectTransform parent, string name, Rect holder, Rect design) =>
            MenuKit.Place(parent, name, design.x - holder.x, design.y - holder.y, design.width, design.height);
    }
}
