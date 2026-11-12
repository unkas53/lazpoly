using System;
using Lazpoly.App.Session;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
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
    /// The card that opens on a city, laid out like a title deed: a dark band with the city's name, its group's
    /// colour on a ribbon beside it and the close mark; under it, at the left, a large picture of the city as the
    /// choice would leave it (its little island, in the player's colour), and at the right a strip of small pictures,
    /// one per level (bare land, one to four houses, the hotel) with a check on the ones the choice includes, then a
    /// box with the rent the chosen level earns, the whole price and what is left of the player's money; at the foot
    /// one big button with the whole price. When buying, the land is picked and the player may add the houses the
    /// first visit allows; on an owned city the levels above its own are offered. A level the rules keep for later
    /// or the money does not reach is greyed, with the reason under its name. A factory city shows its works at each
    /// stage instead, the one this purchase reaches marked. The host still validates every command the card sends.
    /// </summary>
    public sealed class PropertyCard
    {
        /// <summary>The large picture at the left: upright like the pictures themselves.</summary>
        public const float HeroWidth = 300f;
        public const float HeroHeight = 400f;
        public const float HeroRadius = 16f;

        /// <summary>A level's tile in the strip, and the picture in it.</summary>
        public const float TileWidth = 96f;
        public const float PictureWidth = 84f;
        public const float PictureHeight = 112f;
        public const float PictureRadius = 8f;
        public const float NoteHeight = 30f;
        public const int NoteFontSize = 13;

        /// <summary>The gaps: between the large picture and the right side, and between the strip's tiles.</summary>
        public const float BodyGap = 20f;
        public const float StripGap = 8f;

        /// <summary>How grey, and how see-through, the picture of a level that is not open yet is.</summary>
        public const float LockedGrey = 0.8f;
        public const float LockedAlpha = 0.55f;

        public const float BandHeight = 84f;
        public const int TitleFontSize = 34;
        public const float CheckSize = 26f;
        public const int RentFontSize = 30;
        public const float ConfirmHeight = 64f;
        public const int ConfirmFontSize = 28;
        public const float DismissWidth = 200f;

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
            Color owner = _controller.ColorOf(player.Id);

            Band(ui, L(square.Name), (buying ? L("Satılık şehir") : L("Senin şehrin")) + " · " + _controller.GroupNameOf(squareIndex), groupColor,
                _controller.GroupMarkOf(squareIndex), dismiss);

            UiBuilder body = ui.Row(BodyGap, expand: false);
            var sides = body.Container.GetComponent<HorizontalLayoutGroup>();
            sides.childAlignment = TextAnchor.UpperLeft;
            sides.childForceExpandHeight = true; // The right side is as tall as the picture, so its box fills what the strip leaves.

            // The large picture: the city as the choice would leave it, in the player's colour.
            Color paper = choice.Resort ? BoardView.FactoryStone : Color.Lerp(groupColor, BoardView.PaperColor, BoardView.Pastel);
            Texture large = choice.Resort ? _preview?.FactoryPicture(choice.ResortStage, paper, owner) : _preview?.Picture(picked, paper, owner);
            Hero(body, large, choice.Resort ? L("Fabrika şehri") : LevelName(picked), picked == 0 && !choice.Resort ? UiIcon.Flag : UiIcon.Home);

            UiBuilder right = body.Column(10f);
            if (choice.Resort) RenderStages(right, choice, square, paper, owner);
            else RenderLevels(right, choice, paper, owner);
            RenderSummary(right, choice, picked, cost);
            if (buying && !choice.AffordsLand) right.Notice(L("Paran yetmiyor: bina ya da mülk satışıyla para bulabilirsin."));

            UiBuilder actions = ui.Row();
            UiBuilder.Fix(actions.Button(buying ? L("Devam Et") : L("Şimdi Değil"), dismiss, height: ConfirmHeight), width: DismissWidth);
            int playerId = player.Id;
            int builds = picked - choice.Level;
            bool enabled = buying ? choice.AffordsLand && _controller.IsAllowed(new BuyPropertyCommand(playerId, Math.Max(0, builds))) : builds > 0;
            Button confirm = actions.Button(buying ? F($"Satın Al ({cost})") : F($"Kur ({cost})"), () =>
            {
                // The picked level goes up at once with the purchase, in one payment (the owner's wish): the plot never shows on its own.
                if (buying) _controller.Submit(new BuyPropertyCommand(playerId, Math.Max(0, builds)));
                else if (builds > 0) _controller.Submit(new BuildCommand(playerId, squareIndex, builds));
                done?.Invoke();
            }, enabled, UiButtonStyle.Primary, ConfirmHeight);
            Text label = confirm.GetComponentInChildren<Text>();
            label.fontSize = ConfirmFontSize;
            UiBuilder.ShrinkToFit(label);
        }

        /// <summary>
        /// The top of a card about a square: the kit's dark band with a ribbon and (when the group has one) a letter mark
        /// in the group's colour at the left, a small line in brass capitals over the name in heavy capitals, and the
        /// close mark at the right; the square card opens the same way.
        /// </summary>
        public static void Band(UiBuilder ui, string title, string subtitle, Color color, string mark, Action close)
        {
            UiBuilder band = ui.Row(14f, expand: false);
            UiRoot.Banner(band.Container, BandHeight);
            UiBuilder.Fix(band.Container.gameObject, height: BandHeight);
            var layout = band.Container.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(14, 16, 9, 11);
            layout.childAlignment = TextAnchor.MiddleLeft;

            RectTransform ribbon = UiRoot.Rect(band.Container, "Ribbon", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiRoot.Background(ribbon, color).raycastTarget = false;
            UiBuilder.Fix(ribbon.gameObject, 12f, BandHeight - 28f);
            if (mark != null) band.Swatch(color, 44f, mark: mark);

            UiBuilder words = band.Column(0f);
            if (!string.IsNullOrEmpty(subtitle))
            {
                Text over = words.Eyebrow(subtitle, UiPalette.Ink.Gold);
                over.fontSize = 15;
                over.horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            Text name = words.Label(Upper(title), UiTextStyle.Heading, UiPalette.Ink.OnMasthead);
            name.font = UiTheme.BlackFont;
            name.fontSize = TitleFontSize;
            UiBuilder.Fix(name, height: 42f);
            UiBuilder.ShrinkToFit(name); // A long name gets smaller, never a second line.

            band.CloseMark(close, UiPalette.Ink.OnMasthead);
        }

        /// <summary>
        /// The large picture, <see cref="HeroWidth"/> by <see cref="HeroHeight"/>, with a caption in a pill at its foot
        /// (over the picture's clouds); an icon on a plain ground where no picture can be drawn.
        /// </summary>
        private static void Hero(UiBuilder row, Texture picture, string caption, UiIcon stand)
        {
            // The holder may be stretched to the row's height; the picture keeps its own, hung from the top.
            RectTransform holder = UiRoot.Rect(row.Container, "Hero", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(holder.gameObject, HeroWidth, HeroHeight);
            RectTransform box = UiRoot.Rect(holder, "Picture", new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            box.pivot = new Vector2(0.5f, 1f);
            box.sizeDelta = new Vector2(0f, HeroHeight);
            if (picture != null)
            {
                UiPictures.Show(box, picture, new Vector2(HeroWidth, HeroHeight), HeroRadius);
            }
            else
            {
                UiRoot.Background(box, UiTheme.Field).raycastTarget = false;
                Centre(UiIcons.Create(box, stand, HeroWidth * 0.4f, UiTheme.Muted));
            }

            RectTransform tag = UiRoot.Rect(box, "Caption", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            tag.pivot = new Vector2(0.5f, 0f);
            tag.anchoredPosition = new Vector2(0f, 12f);
            var fit = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            fit.childControlWidth = true;
            fit.childControlHeight = true;
            fit.childForceExpandWidth = false;
            fit.childForceExpandHeight = false;
            var fitter = tag.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            new UiBuilder(tag).Chip(caption, UiPalette.Ink.MastheadDeep, UiPalette.Ink.OnMasthead);
        }

        private static void Centre(RectTransform rect)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        /// <summary>The strip of levels, one tile each, under a line that says what is asked; only the land for a square that takes no buildings.</summary>
        private void RenderLevels(UiBuilder right, PropertyChoice choice, Color paper, Color owner)
        {
            right.Eyebrow(choice.Buying ? L("Ne kurmak istersin?") : L("Kaç kat çıkalım?"), UiTheme.Muted);
            UiBuilder strip = Strip(right);
            int levels = choice.Developable ? BuildingPreview.Levels : 1;
            for (int level = 0; level < levels; level++) RenderLevel(strip, choice, level, paper, owner);
        }

        private static UiBuilder Strip(UiBuilder right)
        {
            UiBuilder strip = right.Row(StripGap, expand: false);
            strip.Container.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.UpperCenter;
            return strip;
        }

        private void RenderLevel(UiBuilder strip, PropertyChoice choice, int level, Color paper, Color owner)
        {
            bool pickable = choice.CanPick(level);
            string locked = choice.Locked(level);
            bool included = level <= _picked;
            int pick = level;
            UiBuilder tile = strip.Choice(pickable && locked == null ? (Action)(() => _picked = pick) : null, included, dimmed: locked != null);
            UiBuilder.Fix(tile.Container.gameObject, width: TileWidth);

            Small(tile, _preview?.Picture(level, paper, owner), level == 0 ? UiIcon.Flag : UiIcon.Home, included, locked != null);
            Text name = tile.Label(LevelName(level), UiTextStyle.Small, locked != null ? UiTheme.Muted : UiTheme.Text, TextAnchor.MiddleCenter);
            name.font = UiTheme.BoldFont;
            Note(tile, locked ?? F($"Kira {choice.RentAt(level)}"), locked != null);
        }

        /// <summary>
        /// A factory city: its works at each stage (one more with every factory city the player holds) with the rent that
        /// stage earns on each of them, the stage this card reaches marked. Nothing to pick: the stage follows the cities held.
        /// </summary>
        private void RenderStages(UiBuilder right, PropertyChoice choice, SquareDefinition square, Color stone, Color owner)
        {
            right.Eyebrow(L("Fabrika şehirlerin çoğaldıkça kira artar"), UiTheme.Muted);
            UiBuilder strip = Strip(right);
            for (int stage = 1; stage <= ResortParts.Stages; stage++)
            {
                bool reached = stage == choice.ResortStage;
                UiBuilder tile = strip.Choice(null, reached);
                UiBuilder.Fix(tile.Container.gameObject, width: TileWidth);
                Small(tile, _preview?.FactoryPicture(stage, stone, owner), UiIcon.Home, reached, false);
                Text name = tile.Label(F($"{stage}. kademe"), UiTextStyle.Small, UiTheme.Text, TextAnchor.MiddleCenter);
                name.font = UiTheme.BoldFont;
                Note(tile, F($"Kira {GameRules.ResortRent(square.BaseRent, stage)}"), false);
            }
        }

        /// <summary>A tile's picture with its check when the choice includes it; greyed and faded when the level is not open yet.</summary>
        private static void Small(UiBuilder tile, Texture picture, UiIcon stand, bool included, bool locked)
        {
            RectTransform box = UiRoot.Rect(tile.Container, "Picture", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(box.gameObject, PictureWidth, PictureHeight);
            if (picture != null)
            {
                RawImage image = UiPictures.Show(box, picture, new Vector2(PictureWidth, PictureHeight), PictureRadius, grey: locked ? LockedGrey : 0f);
                if (locked) image.color = new Color(1f, 1f, 1f, LockedAlpha);
            }
            else
            {
                UiRoot.Background(box, UiTheme.Field).raycastTarget = false;
                Centre(UiIcons.Create(box, stand, PictureWidth * 0.5f, UiTheme.Muted));
            }

            if (!included) return;
            RectTransform check = UiRoot.Rect(box, "Check", Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
            check.pivot = Vector2.one;
            check.anchoredPosition = new Vector2(-3f, -3f);
            check.sizeDelta = new Vector2(CheckSize, CheckSize);
            Image disc = UiRoot.Background(check, UiPalette.Ink.MastheadDeep);
            disc.sprite = UiSprites.Circle;
            disc.type = Image.Type.Simple;
            disc.raycastTarget = false;
            RectTransform mark = UiRoot.Rect(check, "Mark", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            mark.sizeDelta = new Vector2(CheckSize * 0.56f, CheckSize * 0.56f);
            Sprite tick = KitArt.Get(UiBuilder.CheckIcon);
            Image sign = UiRoot.Background(mark, UiPalette.Ink.Gold, rounded: tick == null);
            if (tick != null) sign.sprite = tick;
            sign.raycastTarget = false;
        }

        /// <summary>The line under a tile's name: its rent, or in the warning colour why it cannot be picked yet; two short lines at most.</summary>
        private static void Note(UiBuilder tile, string text, bool warning)
        {
            Text note = tile.Label(text, UiTextStyle.Small, warning ? UiTheme.Warning : UiTheme.Muted, TextAnchor.UpperCenter);
            note.fontSize = warning ? NoteFontSize : 15;
            if (warning) note.font = UiTheme.BoldFont;
            UiBuilder.Fix(note, height: NoteHeight);
            UiBuilder.ShrinkToFit(note);
        }

        /// <summary>The box under the strip: the rent the chosen level earns, large, then the price, the player's money and what is left of it.</summary>
        private static void RenderSummary(UiBuilder right, PropertyChoice choice, int picked, int cost)
        {
            UiBuilder card = right.Card(UiTheme.Field, 14);
            UiBuilder.Fix(card.Container.gameObject).flexibleHeight = 1f; // It takes the height the strip leaves beside the picture.
            card.Container.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            UiBuilder rent = card.Row();
            rent.Label(L("Kira"), UiTextStyle.Body, UiTheme.Text).font = UiTheme.BoldFont;
            Text earned = rent.Label(M(choice.RentAt(picked)), UiTextStyle.Heading, UiTheme.Accent, TextAnchor.MiddleRight);
            earned.font = UiTheme.BlackFont;
            earned.fontSize = RentFontSize;
            card.Rule();
            Fact(card, L("Bedel"), M(cost), UiTheme.Text);
            Fact(card, L("Paran"), M(choice.Money), UiTheme.Text);
            if (choice.AffordsLand) Fact(card, L("Kalan"), M(choice.Money - cost), UiTheme.Text);
        }

        private static void Fact(UiBuilder card, string label, string value, Color ink)
        {
            UiBuilder line = card.Row();
            line.Label(label, UiTextStyle.Small, UiTheme.Muted);
            line.Label(value, UiTextStyle.Body, ink, TextAnchor.MiddleRight).font = UiTheme.BoldFont;
        }

        /// <summary>What a tile is called: the land, "house 1" to "house 4", the hotel.</summary>
        public static string LevelName(int level)
        {
            if (level <= 0) return L("Arsa");
            return level >= SquareDefinition.MaxBuildingLevel ? L("Otel") : F($"Ev {level}");
        }
    }
}
