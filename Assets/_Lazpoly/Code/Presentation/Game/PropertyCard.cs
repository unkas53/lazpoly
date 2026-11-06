using System;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The card that opens on a city: a title band in the group's colour, a row of tiles with a
    /// picture of each level (bare land, one to four houses, the hotel) and a check on the ones the
    /// choice includes, the rent the chosen level earns, and one big button with the whole price.
    /// When buying, the land is picked and the player may add the houses the first visit allows;
    /// on an owned city the tiles above its level are offered. A holiday resort has one tile with its beach. A level the rules keep for later or
    /// the money does not reach is greyed, with the reason stamped across its picture. The host
    /// still validates every command the card sends.
    /// </summary>
    public sealed class PropertyCard
    {
        public const float TileWidth = 128f;
        public const float PictureSize = 112f;
        public const float CheckSize = 24f;
        public const float ConfirmHeight = 64f;
        public const int ConfirmFontSize = 28;
        public const float DismissWidth = 170f;
        public const float StampTilt = 22f;

        private readonly GameSceneController _controller;
        private readonly BuildingPreview _preview;
        private int _visit;
        private int _picked;

        public PropertyCard(GameSceneController controller, BuildingPreview preview)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _preview = preview;
        }

        /// <summary>The level the card has selected.</summary>
        public int Picked => _picked;

        /// <summary>Forgets the selection, so the next card opens on its default pick.</summary>
        public void Reset() => _visit = 0;

        /// <summary>
        /// Draws the card for the current player's square. <paramref name="dismiss"/> runs for
        /// "move on" / "not now" and the close mark; <paramref name="done"/> after the commands are sent.
        /// </summary>
        public void Render(UiBuilder ui, bool buying, Action dismiss, Action done)
        {
            IGameSession session = _controller.Session;
            GameState state = session.State;
            PlayerState player = state.CurrentPlayer;
            int squareIndex = player.Position;
            SquareDefinition square = session.Board[squareIndex];
            PropertyChoice choice = PropertyChoice.For(session.Board, session.Rules, state, player, squareIndex, buying);

            int visit = HashCode.Combine(state.TurnNumber, squareIndex, buying);
            if (visit != _visit)
            {
                _visit = visit;
                _picked = choice.DefaultPick;
            }

            _picked = choice.Clamp(_picked);
            int picked = _picked;
            int cost = choice.CostOf(picked);
            Color groupColor = _controller.GroupColorOf(squareIndex);

            Band(ui, L(square.Name), groupColor, _controller.GroupMarkOf(squareIndex), dismiss);
            ui.Label((buying ? L("Satılık şehir") : L("Senin şehrin")) + " · " + _controller.GroupNameOf(squareIndex), UiTextStyle.Small, UiTheme.Muted,
                TextAnchor.MiddleCenter);
            Color paper = choice.Resort ? Color.Lerp(BoardView.SandColor, BoardView.PaperColor, BoardView.SandPastel) : Color.Lerp(groupColor, BoardView.PaperColor, BoardView.Pastel);
            RenderTiles(ui, choice, paper, _controller.ColorOf(player.Id));

            Text rent = ui.Label(F($"Kira: {choice.RentAt(picked)}"), UiTextStyle.Body, UiTheme.Accent, TextAnchor.MiddleCenter);
            rent.font = UiTheme.BoldFont;
            if (choice.AffordsLand)
                ui.Label(F($"Paran: {choice.Money}, kalan: {choice.Money - cost}"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
            else
                ui.Label(F($"Paran: {choice.Money}"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
            if (buying && !choice.AffordsLand)
                ui.Label(L("Paran yetmiyor: bina ya da mülk satışıyla para bulabilirsin."), UiTextStyle.Small, UiTheme.Warning, TextAnchor.MiddleCenter);

            UiBuilder actions = ui.Row();
            UiBuilder.Fix(actions.Button(buying ? L("Devam Et") : L("Şimdi Değil"), dismiss, height: ConfirmHeight), width: DismissWidth);
            int playerId = player.Id;
            int builds = picked - choice.Level;
            bool enabled = buying ? choice.AffordsLand && _controller.IsAllowed(new BuyPropertyCommand(playerId)) : builds > 0;
            Button confirm = actions.Button(buying ? F($"Satın Al ({cost})") : F($"Kur ({cost})"), () =>
            {
                if (buying) _controller.Submit(new BuyPropertyCommand(playerId));
                for (int i = 0; i < builds; i++) _controller.Submit(new BuildCommand(playerId, squareIndex));
                done?.Invoke();
            }, enabled, UiButtonStyle.Primary, ConfirmHeight);
            Text label = confirm.GetComponentInChildren<Text>();
            label.fontSize = ConfirmFontSize;
            UiBuilder.ShrinkToFit(label);
        }

        /// <summary>The title on a band of the group's colour, its letter mark at the left and a close mark at the right; the square card opens the same way.</summary>
        internal static void Band(UiBuilder ui, string title, Color color, string mark, Action close)
        {
            UiBuilder band = ui.Card(color, 6);
            UiBuilder head = band.Row(10f, expand: false);
            if (mark != null) head.Swatch(color, 36f, mark: mark);
            else UiBuilder.Fix(UiRoot.Rect(head.Container, "Spacer", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero).gameObject, 36f, 36f);
            Text label = head.Label(title, UiTextStyle.Heading, ColorVision.TextOn(color), TextAnchor.MiddleCenter);
            UiBuilder.Fix(label, -1f).flexibleWidth = 1f;
            UiBuilder.Fix(head.Button("×", close, height: 36f), width: 36f);
        }

        /// <summary>One tile per level, side by side and centred; only the land for a square that takes no buildings.</summary>
        private void RenderTiles(UiBuilder ui, PropertyChoice choice, Color paper, Color owner)
        {
            UiBuilder row = ui.Row(8f, expand: false);
            row.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            int levels = choice.Developable ? BuildingPreview.Levels : 1;
            for (int level = 0; level < levels; level++) RenderTile(row, choice, level, paper, owner);
        }

        private void RenderTile(UiBuilder row, PropertyChoice choice, int level, Color paper, Color owner)
        {
            bool pickable = choice.CanPick(level);
            string locked = choice.Locked(level);
            bool included = level <= _picked;
            int pick = level;
            UiBuilder tile = row.Tile(pickable ? (Action)(() => _picked = pick) : null, included ? Color.Lerp(UiTheme.Card, UiTheme.Accent, 0.22f) : UiTheme.Card, 8);
            UiBuilder.Fix(tile.Container.gameObject, width: TileWidth);
            tile.Container.GetComponent<Button>().interactable = locked == null;

            RectTransform box = UiRoot.Rect(tile.Container, "Picture", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(box.gameObject, PictureSize, PictureSize);
            // A resort shows the beach its stage would have, in the player's colour; a town the level's building.
            Texture picture = choice.Resort ? _preview?.BeachPicture(choice.ResortStage, paper, owner) : _preview?.Picture(level, paper);
            if (picture != null)
            {
                var image = box.gameObject.AddComponent<RawImage>();
                image.texture = picture;
                image.raycastTarget = false;
            }
            else
            {
                UiRoot.Background(box, UiTheme.Field).raycastTarget = false;
                RectTransform icon = UiIcons.Create(box, level == 0 ? UiIcon.Flag : UiIcon.Home, PictureSize * 0.45f, UiTheme.Muted);
                icon.anchorMin = icon.anchorMax = new Vector2(0.5f, 0.5f);
                icon.anchoredPosition = Vector2.zero;
            }

            if (locked != null)
            {
                UiRoot.Background(UiRoot.Stretch(box, "Veil"), UiTheme.Veil, rounded: false).raycastTarget = false;
                RectTransform stamp = UiRoot.Rect(box, "Stamp", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                stamp.sizeDelta = new Vector2(PictureSize * 1.3f, 26f);
                stamp.localRotation = Quaternion.Euler(0f, 0f, StampTilt);
                UiRoot.Background(stamp, UiTheme.Warning, rounded: false).raycastTarget = false;
                var text = UiRoot.Stretch(stamp, "Text").gameObject.AddComponent<Text>();
                UiTheme.Apply(text, UiTextStyle.Small);
                text.text = locked;
                text.font = UiTheme.BoldFont;
                text.fontSize = 13;
                text.color = UiTheme.Label(UiButtonStyle.Danger);
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            RectTransform check = UiRoot.Rect(box, "Check", new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
            check.pivot = new Vector2(1f, 0f);
            check.anchoredPosition = new Vector2(-6f, 6f);
            check.sizeDelta = new Vector2(CheckSize, CheckSize);
            UiRoot.Background(check, UiTheme.Field).raycastTarget = false;
            if (included)
                UiRoot.Background(UiRoot.Rect(check, "Mark", Vector2.zero, Vector2.one, new Vector2(5f, 5f), new Vector2(-5f, -5f)), UiTheme.Accent).raycastTarget = false;

            Text name = tile.Label(choice.Resort ? L("Tatil yeri") : LevelName(level), UiTextStyle.Small, locked != null ? UiTheme.Muted : UiTheme.Text, TextAnchor.MiddleCenter);
            name.font = UiTheme.BoldFont;
            tile.Label(F($"Kira {choice.RentAt(level)}"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);
        }

        /// <summary>What a tile is called: the land, "house 1" to "house 4", the hotel.</summary>
        public static string LevelName(int level)
        {
            if (level <= 0) return L("Arsa");
            return level >= SquareDefinition.MaxBuildingLevel ? L("Otel") : F($"Ev {level}");
        }
    }
}
