using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Rules;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>The "Nasıl Oynanır" window, shared by the main menu and the game screen.</summary>
    public static class HowToPlayPanel
    {
        public const float Width = 640f;
        private const float TextHeight = 460f;

        public static void Render(UiBuilder ui, BoardDefinition board, RulesConfig rules, Action close)
        {
            ui.Label(L("Nasıl Oynanır"), UiTextStyle.Heading);
            UiBuilder list = ui.Scroll(TextHeight);
            foreach (KeyValuePair<string, string> section in HowToPlay.Sections(board, rules))
            {
                list.Label(section.Key, UiTextStyle.Body, UiTheme.Accent);
                list.Label(section.Value, UiTextStyle.Small);
                list.Space(4f);
            }

            UiBuilder row = ui.Row();
            row.Button(L("İpuçlarını Yeniden Göster"), () => new TipMemory(new PlayerPrefsStorage()).Reset(), height: UiTheme.SmallButtonHeight);
            row.Button(L("Kapat"), close, style: UiButtonStyle.Primary, height: UiTheme.SmallButtonHeight);
        }
    }
}
