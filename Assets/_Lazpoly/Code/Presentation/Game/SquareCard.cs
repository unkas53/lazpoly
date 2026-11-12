using System;
using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The card a click on a square opens in the middle of the screen: the dark band the property card has
    /// (the name, its kind over it, the group's colour on a ribbon), a picture of the square as it stands
    /// (its little island with the houses or hotel on it in the owner's colour), the
    /// facts beside it (owner, price, building cost, sale value, what is built), the whole rent
    /// table with the rent that applies now marked, the squares of the same colour with who holds
    /// each (a click on one opens its card), and the notes <see cref="SquareInfo"/> writes. Special
    /// squares get their own facts and explanation instead of the table.
    /// </summary>
    public sealed class SquareCard
    {
        /// <summary>The picture beside the facts: upright, like the property pictures themselves.</summary>
        public const float PictureSize = 135f;
        public const float PictureHeight = 180f;
        public const float PictureRadius = 12f;
        public const float RentRowHeight = 26f;

        private readonly GameSceneController _controller;
        private readonly BuildingPreview _preview;

        public SquareCard(GameSceneController controller, BuildingPreview preview)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _preview = preview;
        }

        /// <param name="close">Runs for the close mark, the button at the foot and a click on the dimmed board.</param>
        /// <param name="open">Opens the card of another square of the same colour.</param>
        public void Render(UiBuilder ui, int squareIndex, Action close, Action<int> open)
        {
            IGameSession session = _controller.Session;
            BoardDefinition board = session.Board;
            GameState state = session.State;
            SquareDefinition square = board[squareIndex];
            bool property = square.Type == SquareType.Property;
            SquareInfo info = SquareInfo.Describe(board, session.Rules, state, squareIndex, _controller.GroupNameOf(squareIndex));
            Color band = property ? _controller.GroupColorOf(squareIndex) : UiTheme.Accent;

            PropertyCard.Band(ui, info.Title, info.Kind, band, property ? _controller.GroupMarkOf(squareIndex) : null, close);

            UiBuilder top = ui.Row(14f, expand: false);
            RenderPicture(top, square, squareIndex, property, band);
            UiBuilder facts = top.Column(4f);
            if (property) RenderOwner(facts, state, squareIndex);
            foreach (SquareInfoRow row in info.Rows)
            {
                if (!row.Rent) RenderFact(facts, row.Label, row.Value, row.Current);
            }

            if (property && square.IsDevelopable)
            {
                int level = state.GetBuildingLevel(squareIndex);
                RenderFact(facts, L("Bina"), EventDescriber.LevelText(level), level > 0);
            }

            var rents = new List<SquareInfoRow>();
            foreach (SquareInfoRow row in info.Rows)
            {
                if (row.Rent) rents.Add(row);
            }

            if (rents.Count > 0) RenderRentTable(ui, rents);
            if (property) RenderGroup(ui, board, state, square.GroupId, squareIndex, open);
            foreach (string note in info.Notes) ui.Label(note, UiTextStyle.Small, UiTheme.Muted);
            ui.Actions(null, null, L("Kapat"), close);
        }

        /// <summary>
        /// The square as it stands: its little island with what is built on it in its owner's colour, a factory city with
        /// its works; special squares show a sign instead.
        /// </summary>
        private void RenderPicture(UiBuilder row, SquareDefinition square, int squareIndex, bool property, Color band)
        {
            RectTransform box = UiRoot.Rect(row.Container, "Picture", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(box.gameObject, PictureSize, PictureHeight);
            Texture picture = null;
            if (property)
            {
                GameState state = _controller.Session.State;
                int owner = state.GetOwner(squareIndex);
                Color? colour = owner != GameState.NoOwner ? _controller.ColorOf(owner) : (Color?)null;
                if (square.IsResort)
                {
                    // A factory city shows its works at the stage its owner's factory cities reach; for sale, the first stage unpainted.
                    int held = colour.HasValue ? GameRules.ResortsOwned(_controller.Session.Board, state, owner) : 1;
                    picture = _preview?.FactoryPicture(held, BoardView.FactoryStone, colour);
                }
                else
                {
                    picture = _preview?.Picture(state.GetBuildingLevel(squareIndex), Color.Lerp(band, BoardView.PaperColor, BoardView.Pastel), colour);
                }
            }

            if (picture != null)
            {
                UiPictures.Show(box, picture, new Vector2(PictureSize, PictureHeight), PictureRadius);
                return;
            }

            UiRoot.Background(box, UiTheme.Field).raycastTarget = false;
            RectTransform icon = UiIcons.Create(box, IconFor(square.Type), PictureSize * 0.45f, property ? band : UiTheme.Accent);
            icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
            icon.anchoredPosition = Vector2.zero;
        }

        /// <summary>The sign on a special square's picture.</summary>
        public static UiIcon IconFor(SquareType type)
        {
            switch (type)
            {
                case SquareType.Start: return UiIcon.Flag;
                case SquareType.Tax: return UiIcon.List;
                case SquareType.Card: return UiIcon.Book;
                case SquareType.Pot: return UiIcon.Bookmark;
                case SquareType.Jail: return UiIcon.Door;
                case SquareType.Travel: return UiIcon.Next;
                case SquareType.Cup: return UiIcon.Flag;
                case SquareType.Lookout: return UiIcon.Dice;
                case SquareType.Property: return UiIcon.Home;
                default: return UiIcon.Pause;
            }
        }

        /// <summary>"Sahibi" with the owner's colour and name, or "sahipsiz".</summary>
        private void RenderOwner(UiBuilder facts, GameState state, int squareIndex)
        {
            int owner = state.GetOwner(squareIndex);
            UiBuilder line = facts.Row(6f, expand: false);
            UiBuilder.Fix(line.Label(L("Sahibi"), UiTextStyle.Small, UiTheme.Muted), -1f).flexibleWidth = 1f;
            if (owner == GameState.NoOwner)
            {
                line.Label(L("sahipsiz"), UiTextStyle.Body, UiTheme.Text, TextAnchor.MiddleRight);
                return;
            }

            line.Swatch(_controller.ColorOf(owner), 16f, round: true);
            Text name = line.Label(EventDescriber.NameOf(state, owner), UiTextStyle.Body, _controller.ColorOf(owner), TextAnchor.MiddleRight);
            name.font = UiTheme.BoldFont;
        }

        /// <summary>A label at the left, its value bold at the right; the value that applies now in the accent colour.</summary>
        private static void RenderFact(UiBuilder facts, string label, string value, bool current)
        {
            UiBuilder line = facts.Row();
            line.Label(label, UiTextStyle.Small, UiTheme.Muted);
            Text text = line.Label(value, UiTextStyle.Body, current ? UiTheme.Accent : UiTheme.Text, TextAnchor.MiddleRight);
            text.font = UiTheme.BoldFont;
        }

        /// <summary>The rent at every level in one table; the row that applies now sits on a tinted strip in the accent colour.</summary>
        private static void RenderRentTable(UiBuilder ui, List<SquareInfoRow> rents)
        {
            UiBuilder table = ui.Card(UiTheme.Field, 8);
            UiBuilder head = table.Row();
            head.Label(L("Kira"), UiTextStyle.Small, UiTheme.Muted).font = UiTheme.BoldFont;
            foreach (SquareInfoRow row in rents)
            {
                UiBuilder line = table.Row();
                UiBuilder.Fix(line.Container.gameObject, height: RentRowHeight);
                line.Container.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(8, 8, 0, 0);
                if (row.Current) UiRoot.Background(line.Container, Color.Lerp(UiTheme.Field, UiTheme.Accent, 0.3f)).raycastTarget = false;
                Color color = row.Current ? UiTheme.Accent : UiTheme.Text;
                Text label = line.Label(row.Label, UiTextStyle.Small, color);
                Text value = line.Label(row.Value, UiTextStyle.Small, color, TextAnchor.MiddleRight);
                if (row.Current)
                {
                    label.font = UiTheme.BoldFont;
                    value.font = UiTheme.BoldFont;
                }
            }
        }

        /// <summary>The squares of the same colour side by side, each with its holder; this square's own chip is tinted, the others open their card.</summary>
        private void RenderGroup(UiBuilder ui, BoardDefinition board, GameState state, int groupId, int squareIndex, Action<int> open)
        {
            var squares = new List<int>(GameRules.GroupSquares(board, groupId));
            if (squares.Count < 2) return;
            ui.Label(F($"Bu renk: {squares.Count} kare"), UiTextStyle.Small, UiTheme.Muted).font = UiTheme.BoldFont;
            UiBuilder row = ui.Row(6f);
            foreach (int other in squares)
            {
                bool self = other == squareIndex;
                int pick = other;
                UiBuilder chip = row.Tile(self || open == null ? null : (Action)(() => open(pick)), self ? Color.Lerp(UiTheme.Card, UiTheme.Accent, 0.22f) : UiTheme.Card, 8);
                Text name = chip.Label(L(board[other].Name), UiTextStyle.Small, UiTheme.Text, TextAnchor.MiddleCenter);
                if (self) name.font = UiTheme.BoldFont;
                int owner = state.GetOwner(other);
                UiBuilder who = chip.Row(6f, expand: false);
                who.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                who.Swatch(owner == GameState.NoOwner ? UiTheme.RestingFill : _controller.ColorOf(owner), 12f, round: true);
                who.Label(owner == GameState.NoOwner ? L("sahipsiz") : EventDescriber.NameOf(state, owner), UiTextStyle.Small, UiTheme.Muted);
            }
        }
    }
}
