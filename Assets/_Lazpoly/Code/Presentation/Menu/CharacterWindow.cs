using System;
using System.Collections.Generic;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The kit's "Karakterini seç" window: the four portraits on cream cards, the picked one on the highlighted card
    /// with "SEÇİLİ" under its name, and the pick written at the bottom. A click picks; the cross or Esc closes.
    /// </summary>
    public sealed class CharacterWindow
    {
        private readonly GameObject _layer;
        private readonly Action<CharacterOption> _picked;
        private readonly List<Image> _cards = new List<Image>();
        private readonly List<Text> _marks = new List<Text>();
        private readonly Text _selected;

        /// <param name="frame">A 1920x1080 design frame on a dimmed layer that covers the screen (the controller scales it).</param>
        public CharacterWindow(RectTransform frame, GameObject layer, Action<CharacterOption> picked)
        {
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            _layer = layer != null ? layer : throw new ArgumentNullException(nameof(layer));
            _picked = picked;

            Rect panel = MenuLayout.CharacterPanel;
            RectTransform window = MenuKit.Place(frame, "Characters Panel", panel.x, panel.y, panel.width, panel.height);
            MenuKit.Picture(window, MenuKit.Panel, Color.white).raycastTarget = true;

            MenuKit.Words(MenuKit.Place(window, "Title", 42f, 31f, 900f, 55f), L("Karakterini seç"), 34, MenuKit.Ink, bold: true);
            MenuKit.IconButtonAt(window, "Close", new Rect(panel.width - 102f, 22f, 70f, 76f), "Icon_Close", Hide);
            MenuKit.Words(MenuKit.Place(window, "Hint", 42f, 93f, 930f, 28f), L("Masadaki yerini kendi tarzınla al."), 18, MenuKit.Muted);

            for (int i = 0; i < CharacterChoice.All.Count; i++)
            {
                CharacterOption option = CharacterChoice.All[i];
                float x = 40f + i * (MenuLayout.CharacterCardWidth + MenuLayout.CharacterCardGap);
                RectTransform card = MenuKit.Place(window, $"Character {option.Name}", x, 150f, MenuLayout.CharacterCardWidth, MenuLayout.CharacterCardHeight);
                Image skin = MenuKit.Picture(card, "Secondary_Normal", Color.white);
                skin.raycastTarget = true;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = skin;
                button.onClick.AddListener(() => Pick(option));
                _cards.Add(skin);

                MenuKit.Picture(MenuKit.Place(card, "Portrait", 24f, 18f, 182f, 290f), option.Portrait, Color.white, keepAspect: true);
                MenuKit.Words(MenuKit.Place(card, "Name", 10f, 322f, 210f, 35f), option.Name, 24, MenuKit.Ink, bold: true, TextAnchor.MiddleCenter);
                _marks.Add(MenuKit.Words(MenuKit.Place(card, "Mark", 10f, 366f, 210f, 28f), L("SEÇ"), 15, option.Color, bold: true, TextAnchor.MiddleCenter));
            }

            _selected = MenuKit.Words(MenuKit.Place(window, "Selected", 42f, 623f, 850f, 35f), "", 19, MenuKit.Ink, bold: true);
            _layer.SetActive(false);
        }

        public bool IsOpen => _layer.activeSelf;

        public void Show(CharacterOption selected)
        {
            Refresh(selected);
            _layer.SetActive(true);
        }

        public void Hide() => _layer.SetActive(false);

        /// <summary>Marks the picked character's card.</summary>
        public void Refresh(CharacterOption selected)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool picked = CharacterChoice.All[i] == selected;
                Sprite skin = MenuKit.Get(picked ? "Secondary_Hover" : "Secondary_Normal");
                if (skin != null) _cards[i].sprite = skin;
                _marks[i].text = picked ? L("SEÇİLİ") : L("SEÇ");
            }

            _selected.text = selected != null ? F($"Seçili karakter: {selected.Name}") : "";
        }

        private void Pick(CharacterOption option)
        {
            AudioPlayer.Play(SoundCue.UiClick);
            _picked?.Invoke(option);
            Refresh(option);
        }
    }
}
