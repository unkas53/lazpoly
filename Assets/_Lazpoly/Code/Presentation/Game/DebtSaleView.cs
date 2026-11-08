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
    /// The sale screen of a player who cannot pay: a card in the middle of the screen with what is owed and
    /// what is in hand, and over each of their towns on the board a small tag with a tick box and what the
    /// bank pays for it. The player ticks towns (on the tags, or by clicking the towns) and sells them with
    /// "Sat", which also pays once the money covers the debt; "Otomatik Satış" sells for them
    /// (<see cref="DebtSettlement"/>), and "İflas" gives up after a second press.
    /// </summary>
    public sealed class DebtSaleView
    {
        /// <summary>The card's width and a tag's size, in reference pixels.</summary>
        public const float CardWidth = 470f;
        public const float TagWidth = 118f;
        public const float TagHeight = 44f;

        /// <summary>The side of the square that, turned on its corner, makes the tag's point; and how far that point reaches below the tag.</summary>
        public const float PointerSize = 24f;
        public static readonly float PointerReach = PointerSize * 0.7071f;

        /// <summary>The tick box's outline, so it reads as something to click on the white tag.</summary>
        public const float BoxOutline = 2f;

        /// <summary>How far above a town's tile surface its tag points, in world units.</summary>
        public const float TagLift = 0.02f;

        /// <summary>The red of the card's heading.</summary>
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
            UiBuilder.AddColumnLayout(_card.gameObject, 0, UiTheme.Spacing);
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

            UiBuilder card = ui.Card(UiTheme.Card, 16);
            card.Chip(L("Satmak için bir şehir seçin"), HeadingColor, Color.white, 40f);
            AmountRow(card, L("Para lazım"), debt.Amount);
            AmountRow(card, L("Kalan para"), player.Money);
            string creditor = EventDescriber.CreditorText(state, debt.CreditorId);
            card.Label(F($"{EventDescriber.ReasonText(debt.Reason)} → {creditor}"), UiTextStyle.Small, UiTheme.Muted, TextAnchor.MiddleCenter);

            IReadOnlyList<GameCommand> plan = DebtSettlement.Plan(board, _controller.Session.Rules, state, player.Id);
            if (plan.Count == 1 && plan[0] is DeclareBankruptcyCommand giveUp)
            {
                // Only a game saved before insolvency ended games at once can stand here.
                card.Button(L("İflas Et"), () => _controller.Submit(giveUp), style: UiButtonStyle.Danger);
                return;
            }

            var pay = new PayDebtCommand(player.Id);
            bool canPay = _controller.IsAllowed(pay);
            int gain = _sale.Proceeds(board, _controller.Session.Rules, state, player.Id);
            if (canPay)
            {
                card.Label(L("Paran borcu karşılıyor."), UiTextStyle.Body, UiTheme.Gain, TextAnchor.MiddleCenter);
            }
            else if (_sale.Picked.Count == 0)
            {
                card.Label(L("Ödemek için yeterli paran yok. Satacağın şehirleri haritada işaretle."), UiTextStyle.Body, alignment: TextAnchor.MiddleCenter);
            }
            else
            {
                bool covers = player.Money + gain >= debt.Amount;
                card.Label(F($"Seçilenler: +{gain}, kalan para {player.Money + gain} olur."), UiTextStyle.Body,
                    covers ? UiTheme.Gain : UiTheme.Warning, TextAnchor.MiddleCenter);
            }

            UiBuilder row = card.Row();
            row.Button(_confirmBankruptcy ? L("Emin misin? İflas et") : L("İflas"), () =>
            {
                if (_confirmBankruptcy) _controller.Submit(new DeclareBankruptcyCommand(player.Id));
                else _confirmBankruptcy = true;
                _cardView.Invalidate();
            }, style: UiButtonStyle.Danger);
            row.Button(L("Otomatik Satış"), () => SubmitAll(plan), enabled: plan.Count > 0 && !canPay);

            if (canPay)
            {
                card.Button(F($"Borcu Öde ({debt.Amount})"), () => _controller.Submit(pay), style: UiButtonStyle.Primary);
                return;
            }

            IReadOnlyList<GameCommand> sale = _sale.Commands(board, _controller.Session.Rules, state, player.Id);
            card.Button(L("Sat"), () =>
            {
                SubmitAll(sale);
                _sale.Clear();
            }, enabled: sale.Count > 0, style: UiButtonStyle.Primary);
        }

        private static void AmountRow(UiBuilder card, string label, int amount)
        {
            UiBuilder row = card.Row();
            row.Label(label, UiTextStyle.Body, alignment: TextAnchor.MiddleRight);
            Text value = row.Label(Lang.M(amount), UiTextStyle.Heading, UiTheme.Money, TextAnchor.MiddleLeft);
            value.font = UiTheme.BlackFont;
        }

        private void SubmitAll(IReadOnlyList<GameCommand> commands)
        {
            foreach (GameCommand command in commands) _controller.Submit(command);
        }

        /// <summary>Makes a tag for each town again when the towns, the ticks or the state change.</summary>
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
                bool picked = _sale.IsPicked(square);
                RectTransform tag = UiRoot.Rect(_tagLayer, $"Sale tag {square}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                tag.pivot = new Vector2(0.5f, 0f);
                tag.sizeDelta = new Vector2(TagWidth, TagHeight);
                Color fill = picked ? PickedFill : UiTheme.Card;
                UiRoot.Background(tag, fill, shadow: true);
                AddPointer(tag, fill);
                UiBuilder.AddColumnLayout(tag.gameObject, 6, 0f);
                int proceeds = GameRules.PropertySaleProceeds(board, _controller.Session.Rules, state, square);
                int town = square;
                Toggle toggle = new UiBuilder(tag).Toggle(F($"+{proceeds}"), picked, on => _sale.Set(town, on));
                StyleBox(toggle);
                _tags[square] = tag;
            }
        }

        /// <summary>The fill of a ticked town's tag: a pale green, so the towns going up for sale stand out.</summary>
        public static readonly Color PickedFill = new Color(0.86f, 0.96f, 0.86f);

        /// <summary>A square turned on its corner under the tag's middle, its tip on the town, so each tag plainly belongs to one town.</summary>
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

        /// <summary>A white box with a dark outline and a green tick, instead of the faint form box, so it reads as clickable on the tag.</summary>
        private static void StyleBox(Toggle toggle)
        {
            if (toggle == null) return;
            if (toggle.targetGraphic is Image box)
            {
                box.color = Color.white;
                var outline = box.gameObject.AddComponent<Outline>();
                outline.effectColor = UiTheme.Text;
                outline.effectDistance = new Vector2(BoxOutline, -BoxOutline);
                outline.useGraphicAlpha = false;
            }

            if (toggle.graphic != null) toggle.graphic.color = UiTheme.Gain;
        }

        /// <summary>Keeps each tag over its town as the camera moves, its point on the town.</summary>
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
