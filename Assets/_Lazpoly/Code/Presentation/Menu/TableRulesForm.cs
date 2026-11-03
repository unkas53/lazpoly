using System;
using Lazpoly.Core.Rules;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Menu
{
    /// <summary>
    /// The house rules of the next game as a form: starting money, length, auctions, what the bank
    /// does with a bankrupt's towns and the online turn clock. The main menu shows it in a window
    /// and the lobby shows it in its settings column, where only the owner can press the choices.
    /// </summary>
    public static class TableRulesForm
    {
        /// <param name="table">The rules to show.</param>
        /// <param name="canEdit">False shows the choices without letting anyone press them (a guest reading the owner's rules).</param>
        /// <param name="boardMoney">The board's own starting money, for the "normal" choice.</param>
        /// <param name="changed">Receives the new rules when a choice is pressed.</param>
        /// <param name="explain">Whether to write the line under each choice that says what it means.</param>
        public static void Render(UiBuilder ui, TableRules table, bool canEdit, int boardMoney, Action<TableRules> changed, bool explain = true)
        {
            ui.Eyebrow(L("Başlangıç parası"), UiTheme.Muted);
            UiBuilder money = ui.Row();
            foreach (int choice in TableSetup.StartingMoneyChoices)
            {
                int value = choice;
                Choice(money, TableSetup.StartingMoneyText(value, boardMoney), table.StartingMoney == value, canEdit, () => changed(table.WithStartingMoney(value)));
            }

            ui.Eyebrow(L("Oyun süresi"), UiTheme.Muted);
            UiBuilder rounds = ui.Row();
            foreach (int choice in TableSetup.RoundLimitChoices)
            {
                int value = choice;
                Choice(rounds, TableSetup.RoundLimitText(value), table.RoundLimit == value, canEdit, () => changed(table.WithRoundLimit(value)));
            }

            if (explain)
            {
                ui.Label(table.RoundLimit > 0 ? L("Sınır dolunca parası, mülkleri ve binaları en değerli olan kazanır.") : L("Tek bir oyuncu kalana kadar sürer."),
                    UiTextStyle.Small, UiTheme.Muted);
            }

            ui.Eyebrow(L("Hamle süresi (çevrim içi)"), UiTheme.Muted);
            UiBuilder turn = ui.Row();
            foreach (int choice in TableSetup.TurnSecondsChoices)
            {
                int value = choice;
                Choice(turn, TableSetup.TurnSecondsText(value), table.TurnSeconds == value, canEdit, () => changed(table.WithTurnSeconds(value)));
            }

            if (explain)
            {
                ui.Label(table.TurnSeconds == 0
                        ? L("Saat yok: herkes istediği kadar düşünür. Yalnızca çevrim içi oyunlarda geçerli.")
                        : L("Süre dolunca ev sahibi oyuncu adına en temkinli hamleyi yapar; takas cevapları için en çok 30 sn. Son 10 saniyede ekran kenarı kızarır. Yalnızca çevrim içi oyunlarda geçerli."),
                    UiTextStyle.Small, UiTheme.Muted);
            }
        }

        /// <summary>One choice of a row: selected, or plain; a choice nobody may press keeps its look but does nothing.</summary>
        private static void Choice(UiBuilder row, string label, bool selected, bool enabled, Action pick) =>
            row.Button(label, pick, enabled, selected ? UiButtonStyle.Selected : UiButtonStyle.Secondary, UiTheme.SmallButtonHeight);
    }
}
