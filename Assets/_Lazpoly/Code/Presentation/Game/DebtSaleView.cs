using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using static Lazpoly.Presentation.Texts.Lang;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The sale screen of a player who cannot pay, in the look of the game's deeds (the owner's wish, 2026-11-12): a
    /// card in the middle of the screen under the kit's dark band, with what is owed, what is in hand, what the ticked
    /// towns bring and what is then left or still missing; and over each of their towns on the board a small plate with
    /// a tick box and what the bank pays for it. The player ticks towns (on the plates, or by clicking the towns) and
    /// sells them with "Sat", which also pays once the money covers the debt. "Otomatik Seç" only ticks the towns the
    /// automatic sale would pick (<see cref="DebtSettlement"/>); nothing is sold for the player before their time runs
    /// out. "İflas" gives up after a second press.
    /// </summary>
    public sealed class DebtSaleView
    {
        /// <summary>The card's width and a plate's size, in reference pixels.</summary>
        public const float CardWidth = 500f;
        public const float TagWidth = 140f;
        public const float TagHeight = 50f;

        /// <summary>The side of the square that, turned on its corner, makes the plate's point; and how far that point reaches below the plate.</summary>
        public const float PointerSize = 24f;
        public static readonly float PointerReach = PointerSize * 0.7071f;

        /// <summary>The tick box's side on a plate, and the letter size of the sum beside it and of the debt on the card.</summary>
        public const float BoxSize = 30f;
        public const int AmountFontSize = 23;
        public const int DebtFontSize = 30;

        /// <summary>How strong the edge line of an unticked plate and of its empty box is.</summary>
        public const float EdgeAlpha = 0.16f;
        public const float BoxEdgeAlpha = 0.5f;

        /// <summary>How far above a town's tile surface its plate points, in world units.</summary>
        public const float TagLift = 0.02f;

        /// <summary>The red of the debt: the band's ribbon and the sum owed.</summary>
        public static readonly Color HeadingColor = new Color(0.86f, 0.22f, 0.22f);

        private readonly GameSceneController _controller;
        private readonly DebtSale _sale = new DebtSale();
        private readonly RectTransform _tagLayer;
        private readonly RectTransform _card;
        private readonly UiView _cardView;
        private readonly Dictionary<int, RectTransform> _tags = new Dictionary<int, RectTransform>();
        private readonly List<int> _choices = new List<int>();
        private int _tagsKey;
        private bool _confirmBankruptcy;

        public DebtSaleView(Transform canvas, GameSceneController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            // The tags lie under the card, so the card stays readable where they meet.
            _tagLayer = UiRoot.Stretch(canvas, "Sale Tags");
            _card = UiRoot.Rect(canvas, "Sale Card", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(CardWidth, 0f);
            UiRoot.Panel(_card);
            UiBuilder.AddColumnLayout(_card.gameObject, UiTheme.Padding + 8, UiTheme.Spacing);
            _card.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cardView = new UiView(new UiBuilder(_card), RenderCard);
            _tagLayer.gameObject.SetActive(false);
            _card.gameObject.SetActive(false);
        }

        /// <summary>The towns ticked so far.</summary>
        public DebtSale Sale => _sale;

        /// <summary>Whether the card is up.</summary>
        public bool Shown => _card.gameObject.activeSelf;

        /// <summary>The tag over a town, or null when it has none.</summary>
        public RectTransform TagOf(int square) => _tags.TryGetValue(square, out RectTransform tag) ? tag : null;

        /// <summary>
        /// Shows the screen while <paramref name="active"/> (the person here sells for a debt and nothing is
        /// playing out) over <paramref name="choices"/>, their towns; hides it and forgets the ticks otherwise.
        /// </summary>
        public void Update(GameState state, IReadOnlyList<int> choices, bool active)
        {
            if (!active || state == null || choices == null)
            {
                if (Shown || _tagLayer.gameObject.activeSelf) Hide();
                return;
            }

            _choices.Clear();
            _choices.AddRange(choices);
            _sale.Keep(_choices.Contains);
            if (!Shown) _card.gameObject.SetActive(true);
            if (!_tagLayer.gameObject.activeSelf) _tagLayer.gameObject.SetActive(true);
            _cardView.Refresh(HashCode.Combine(_controller.Revision, _sale.Version, _confirmBankruptcy));
            RebuildTags(state);
            PlaceTags();
        }

        /// <summary>A click on one of the towns ticks or unticks it.</summary>
        public void Toggle(int square)
        {
            if (_choices.Contains(square)) _sale.Toggle(square);
        }

        /// <summary>
        /// "Otomatik Seç": ticks the towns the automatic sale would sell (<see cref="DebtSettlement.Plan"/>) in place of
        /// whatever was ticked, and sells nothing. False when there is no debt or nothing to suggest.
        /// </summary>
        public bool AutoPick()
        {
            GameState state = _controller.Session.State;
            DebtState debt = state?.Debt;
            if (debt == null) return false;
            IReadOnlyList<int> suggested = DebtSettlement.SquaresSold(
                DebtSettlement.Plan(_controller.Session.Board, _controller.Session.Rules, state, debt.PlayerId));
            if (suggested.Count == 0) return false;
            _sale.Pick(suggested);
            return true;
        }

        private void Hide()
        {
            _card.gameObject.SetActive(false);
            _tagLayer.gameObject.SetActive(false);
            _sale.Clear();
            _confirmBankruptcy = false;
            _tagsKey = 0;
        }

        private void RenderCard(UiBuilder ui)
        {
            GameState state = _controller.Session.State;
            DebtState debt = state?.Debt;
            if (debt == null) return;
            BoardDefinition board = _controller.Session.Board;
            PlayerState player = state.FindPlayer(debt.PlayerId);
            if (player == null) return;

            string creditor = EventDescriber.CreditorText(state, debt.CreditorId);
            PropertyCard.Band(ui, L("Şehir sat"), F($"{EventDescriber.ReasonText(debt.Reason)} → {creditor}"), HeadingColor, null, null);

            IReadOnlyList<GameCommand> plan = DebtSettlement.Plan(board, _controller.Session.Rules, state, player.Id);
            var pay = new PayDebtCommand(player.Id);
            bool canPay = _controller.IsAllowed(pay);
            int gain = _sale.Proceeds(board, _controller.Session.Rules, state, player.Id);
            int left = DebtSale.Left(player.Money, gain, debt.Amount);

            // The sums, as on a deed: the debt large, then the money in hand, what the ticked towns bring, and the rest.
            UiBuilder box = ui.Card(UiTheme.Field, 14);
            UiBuilder owed = box.Row();
            owed.Label(L("Borç"), UiTextStyle.Body, UiTheme.Text).font = UiTheme.BoldFont;
            Text sum = owed.Label(M(debt.Amount), UiTextStyle.Heading, HeadingColor, TextAnchor.MiddleRight);
            sum.font = UiTheme.BlackFont;
            sum.fontSize = DebtFontSize;
            box.Rule();
            Fact(box, L("Paran"), M(player.Money), UiTheme.Text);
            Fact(box, F($"Seçilen şehirler ({_sale.Picked.Count})"), "+" + M(gain), gain > 0 ? UiTheme.Gain : UiTheme.Muted);
            box.Rule();
            Fact(box, left >= 0 ? L("Kalan") : L("Eksik"), M(Math.Abs(left)), left >= 0 ? UiTheme.Gain : UiTheme.Warning);

            if (plan.Count == 1 && plan[0] is DeclareBankruptcyCommand giveUp)
            {
                // Only a game saved before insolvency ended games at once can stand here.
                ui.Button(L("İflas Et"), () => _controller.Submit(giveUp), style: UiButtonStyle.Danger);
                return;
            }

            if (canPay) Status(ui, L("Paran borcu karşılıyor."), UiTheme.Gain);
            else if (_sale.Picked.Count == 0) Status(ui, L("Satacağın şehirleri haritada işaretle ya da Otomatik Seç'e bas."), UiTheme.Text);
            else if (left >= 0) Status(ui, L("Seçilenler borcu karşılıyor."), UiTheme.Gain);
            else Status(ui, F($"Seçilenler borcu karşılamıyor: {-left} daha gerek."), UiTheme.Warning);

            UiBuilder row = ui.Row();
            row.Button(_confirmBankruptcy ? L("Emin misin? İflas et") : L("İflas"), () =>
            {
                if (_confirmBankruptcy) _controller.Submit(new DeclareBankruptcyCommand(player.Id));
                else _confirmBankruptcy = true;
                _cardView.Invalidate();
            }, style: UiButtonStyle.Danger);
            // Ticks what the automatic sale would sell and sells nothing: the player still looks and presses "Sat".
            row.Button(L("Otomatik Seç"), () => AutoPick(), enabled: DebtSettlement.SquaresSold(plan).Count > 0 && !canPay);

            Button confirm;
            if (canPay)
            {
                confirm = ui.Button(F($"Borcu Öde ({debt.Amount})"), () => _controller.Submit(pay), true, UiButtonStyle.Primary, PropertyCard.ConfirmHeight);
            }
            else
            {
                IReadOnlyList<GameCommand> sale = _sale.Commands(board, _controller.Session.Rules, state, player.Id);
                confirm = ui.Button(L("Sat"), () =>
                {
                    SubmitAll(sale);
                    _sale.Clear();
                }, sale.Count > 0, UiButtonStyle.Primary, PropertyCard.ConfirmHeight);
            }

            Text label = confirm.GetComponentInChildren<Text>();
            if (label == null) return;
            label.fontSize = PropertyCard.ConfirmFontSize;
            UiBuilder.ShrinkToFit(label);
        }

        /// <summary>A line of the sums: what it is at the left, its money in bold at the right.</summary>
        private static void Fact(UiBuilder box, string label, string value, Color ink)
        {
            UiBuilder line = box.Row();
            line.Label(label, UiTextStyle.Small, UiTheme.Muted);
            line.Label(value, UiTextStyle.Body, ink, TextAnchor.MiddleRight).font = UiTheme.BoldFont;
        }

        /// <summary>The line under the sums that says where the sale stands.</summary>
        private static void Status(UiBuilder ui, string text, Color ink)
        {
            Text line = ui.Label(text, UiTextStyle.Small, ink, TextAnchor.MiddleCenter);
            line.font = UiTheme.BoldFont;
        }

        private void SubmitAll(IReadOnlyList<GameCommand> commands)
        {
            foreach (GameCommand command in commands) _controller.Submit(command);
        }

        /// <summary>Makes a plate for each town again when the towns, the ticks or the state change.</summary>
        private void RebuildTags(GameState state)
        {
            int key = HashCode.Combine(_controller.Revision, _sale.Version, _choices.Count) | 1;
            foreach (int square in _choices) key = HashCode.Combine(key, square);
            if (key == _tagsKey) return;
            _tagsKey = key;

            foreach (RectTransform tag in _tags.Values)
            {
                if (tag != null) UnityEngine.Object.Destroy(tag.gameObject);
            }

            _tags.Clear();
            BoardDefinition board = _controller.Session.Board;
            foreach (int square in _choices)
            {
                int proceeds = GameRules.PropertySaleProceeds(board, _controller.Session.Rules, state, square);
                _tags[square] = BuildTag(square, _sale.IsPicked(square), proceeds);
            }
        }

        /// <summary>The fill of a ticked town's plate: a pale green, so the towns going up for sale stand out.</summary>
        public static readonly Color PickedFill = new Color(0.86f, 0.96f, 0.86f);

        /// <summary>What a plate says: the bank's price with a plus, in the game's short money form ("+120K").</summary>
        public static string TagText(int proceeds) => "+" + Lang.M(proceeds);

        /// <summary>
        /// A town's plate: a rounded card with a thin edge line and a point down to the town; at its left the tick box (an
        /// empty well, or the kit's gold tick on a dark ground once ticked), at its right the bank's price in heavy
        /// letters. A ticked plate stands on pale green, ringed in the gain colour. The whole plate is the tick box: a
        /// click anywhere on it ticks or unticks its town.
        /// </summary>
        private RectTransform BuildTag(int square, bool picked, int proceeds)
        {
            RectTransform tag = UiRoot.Rect(_tagLayer, $"Sale tag {square}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            tag.pivot = new Vector2(0.5f, 0f);
            tag.sizeDelta = new Vector2(TagWidth, TagHeight);
            Color fill = picked ? PickedFill : UiTheme.Card;
            Image ground = UiRoot.Background(tag, fill, shadow: true);
            AddPointer(tag, fill);
            Color dark = UiTheme.Text;
            Edge(tag, picked ? UiTheme.Gain : new Color(dark.r, dark.g, dark.b, EdgeAlpha));

            var row = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(9, 11, 0, 0);
            row.spacing = 8f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            RectTransform box = UiRoot.Rect(tag, "Box", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            UiBuilder.Fix(box.gameObject, BoxSize, BoxSize);
            UiRoot.Background(box, picked ? UiPalette.Ink.MastheadDeep : UiTheme.Field).raycastTarget = false;
            if (!picked) Edge(box, new Color(dark.r, dark.g, dark.b, BoxEdgeAlpha));
            RectTransform mark = UiRoot.Rect(box, "Check", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            mark.sizeDelta = new Vector2(BoxSize * 0.6f, BoxSize * 0.6f);
            Sprite tick = KitArt.Get(UiBuilder.CheckIcon);
            Image sign = UiRoot.Background(mark, UiPalette.Ink.Gold, rounded: tick == null);
            if (tick != null) sign.sprite = tick;
            sign.raycastTarget = false;

            Text amount = new UiBuilder(tag).Label(TagText(proceeds), UiTextStyle.Body, picked ? UiTheme.Gain : UiTheme.Text, TextAnchor.MiddleCenter);
            amount.font = UiTheme.BlackFont;
            amount.fontSize = AmountFontSize;
            amount.raycastTarget = false;
            UiBuilder.Fix(amount, height: TagHeight - 12f).flexibleWidth = 1f;
            UiBuilder.ShrinkToFit(amount);

            var toggle = tag.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = ground;
            toggle.graphic = sign;
            toggle.isOn = picked; // Before it listens: setting it up is not a tick.
            int town = square;
            toggle.onValueChanged.AddListener(on =>
            {
                UiBuilder.Press();
                _sale.Set(town, on);
            });
            return tag;
        }

        /// <summary>A thin line round a plate or a box, drawn over its ground; it takes no clicks and no room.</summary>
        private static void Edge(RectTransform around, Color color)
        {
            RectTransform ring = UiRoot.Stretch(around, "Edge");
            ring.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var line = ring.gameObject.AddComponent<Image>();
            line.sprite = UiSprites.Ring;
            line.type = Image.Type.Sliced;
            line.color = color;
            line.raycastTarget = false;
        }

        /// <summary>A square turned on its corner under the plate's middle, its tip on the town, so each plate plainly belongs to one town.</summary>
        private static void AddPointer(RectTransform tag, Color fill)
        {
            RectTransform pointer = UiRoot.Rect(tag, "Pointer", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
            pointer.sizeDelta = new Vector2(PointerSize, PointerSize);
            pointer.anchoredPosition = Vector2.zero;
            pointer.localRotation = Quaternion.Euler(0f, 0f, 45f);
            UiRoot.Background(pointer, fill, rounded: false, shadow: true).raycastTarget = false; // The shadow sets it off from the board.
            pointer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            pointer.SetAsFirstSibling(); // Under the tick box.
        }

        /// <summary>Keeps each plate over its town as the camera moves, its point on the town.</summary>
        private void PlaceTags()
        {
            BoardView board = _controller.Board;
            Camera camera = Camera.main;
            if (board == null || camera == null) return;
            foreach (KeyValuePair<int, RectTransform> pair in _tags)
            {
                Vector3 screen = camera.WorldToScreenPoint(board.SquarePosition(pair.Key) + Vector3.up * TagLift);
                bool front = screen.z > 0f;
                if (pair.Value.gameObject.activeSelf != front) pair.Value.gameObject.SetActive(front);
                if (front && RectTransformUtility.ScreenPointToLocalPointInRectangle(_tagLayer, screen, null, out Vector2 local))
                {
                    pair.Value.anchoredPosition = local + new Vector2(0f, PointerReach);
                }
            }
        }
    }
}
