using System;
using System.Collections.Generic;
using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The strategic cards a person here holds (Faz 30): the hand window with a way to play each card that may be played
    /// now, picking a card's town on the board (the hand closes and the towns it may hit light up), letting one card go
    /// when a fifth is drawn, and the rent shield's question when rent is due. One card a turn, before the roll.
    /// </summary>
    public sealed class CardHand
    {
        public const int NoCard = -1;

        /// <summary>The width of a card's picture in the hand window.</summary>
        public const float ThumbWidth = 128f;

        private readonly GameSceneController _controller;
        private int _aimCard = NoCard;
        private int _aimKey;

        public CardHand(GameSceneController controller)
        {
            _controller = controller;
        }

        /// <summary>Whether the hand window is open (it closes by itself when its owner has nothing to show).</summary>
        public bool Open { get; set; }

        private BoardDefinition Board => _controller.Session.Board;

        private bool PersonHere(int playerId) => _controller.Session.CanControl(playerId) && !_controller.Session.IsAiPlaying(playerId);

        /// <summary>True while the board has played everything and the game waits in <paramref name="phase"/> on a person here.</summary>
        private bool WaitsHere(GameState state, GamePhase phase) =>
            state != null && state.Phase == phase && !_controller.IsBusy && !_controller.EndReason.HasValue && PersonHere(state.CurrentPlayer.Id);

        /// <summary>
        /// Whose hand the window shows: the player on turn when a person here plays them, otherwise the first seat a person
        /// here plays. Null when no one here holds a seat (a guest watching, or computers only).
        /// </summary>
        public PlayerState Owner(GameState state)
        {
            if (state == null) return null;
            if (PersonHere(state.CurrentPlayer.Id)) return state.CurrentPlayer;
            foreach (PlayerState player in state.Players)
            {
                if (!player.IsBankrupt && PersonHere(player.Id)) return player;
            }

            return null;
        }

        /// <summary>The moment the turn's card may be played: before the roll, or on a turn in jail; one card a turn.</summary>
        private bool PlayMoment(GameState state) =>
            (WaitsHere(state, GamePhase.WaitingForRoll) || WaitsHere(state, GamePhase.AwaitingJailDecision)) && !state.CardPlayedThisTurn;

        /// <summary>Whether <paramref name="card"/> of the player on turn may be played now at some target (with either fund choice).</summary>
        private bool Playable(GameState state, int card)
        {
            if (!PlayMoment(state)) return false;
            PlayerState player = state.CurrentPlayer;
            if (GameRules.CardTargets(Board, state, player, card).Count > 0) return true;
            return Board.Cards[card].Kind == CardKind.Fund && GameRules.CardTargets(Board, state, player, card, 1).Count > 0;
        }

        /// <summary>How many cards the person on turn here could play now; 0 outside the moment for it.</summary>
        public int PlayableCount(GameState state)
        {
            if (!PlayMoment(state)) return 0;
            int count = 0;
            foreach (int card in state.CurrentPlayer.Hand)
            {
                if (Playable(state, card)) count++;
            }

            return count;
        }

        /// <summary>Starts picking the town (or square) for <paramref name="card"/> on the board; the hand window closes.</summary>
        public void Aim(GameState state, int card)
        {
            _aimCard = card;
            _aimKey = AimKey(state);
            Open = false;
        }

        public void StopAiming() => _aimCard = NoCard;

        private static int AimKey(GameState state) => HashCode.Combine(state.TurnNumber, state.CurrentPlayer.Id);

        /// <summary>The card whose target is being picked on the board, or <see cref="NoCard"/>; picking ends by itself when the moment passes.</summary>
        public int Aiming(GameState state)
        {
            if (_aimCard == NoCard) return NoCard;
            if (state == null || AimKey(state) != _aimKey || !state.CurrentPlayer.Hand.Contains(_aimCard))
            {
                _aimCard = NoCard;
                return NoCard;
            }

            // While the board still plays a move, picking waits; once the moment is gone it ends.
            if (!_controller.IsBusy && !PlayMoment(state)) _aimCard = NoCard;
            return _controller.IsBusy ? NoCard : _aimCard;
        }

        /// <summary>The squares the aimed card may be played at now.</summary>
        public List<int> AimTargets(GameState state)
        {
            int card = Aiming(state);
            return card == NoCard ? null : GameRules.CardTargets(Board, state, state.CurrentPlayer, card);
        }

        /// <summary>
        /// The hand window: each card with its name and what it does, and a way to play it when it may be played now:
        /// "Oyna", the fund's two choices, a button per rival for the audit, or "Tahtadan seç" for a card that hits a town.
        /// </summary>
        public void RenderHand(UiBuilder ui, GameState state)
        {
            PlayerState owner = Owner(state);
            if (owner == null)
            {
                Open = false;
                return;
            }

            ui.Title(UiIcon.Bookmark, F($"{owner.Name} · Kartlar ({owner.Hand.Count}/{GameRules.HandLimit})"));
            bool onTurn = owner.Id == state.CurrentPlayer.Id;
            ui.Label(!onTurn ? L("Kartlar sıranın başında, zar atmadan önce oynanır; her sırada bir kart.")
                    : state.CardPlayedThisTurn ? L("Bu sırada bir kart oynadın; sonraki kart bir sonraki sıranda.")
                    : PlayMoment(state) ? L("Zar atmadan önce bir kart oynayabilirsin; her sırada bir kart.")
                    : L("Kartlar sıranın başında, zar atmadan önce oynanır; her sırada bir kart."),
                UiTextStyle.Small, UiTheme.Muted);
            if (owner.FundPayout > 0) ui.Label(F($"Yatırım fonun: Başlangıç'tan geçince {M(owner.FundPayout)} alacaksın."), UiTextStyle.Small, UiTheme.Gain);
            if (owner.Hand.Count == 0) ui.Label(L("Elinde kart yok. Şans karelerinden çekilen bazı kartlar ele alınır."), UiTextStyle.Body);

            foreach (int card in owner.Hand)
            {
                UiBuilder block = CardBlock(ui, card);
                if (onTurn && Playable(state, card)) RenderPlay(block, state, card);
            }

            if (_controller.LastRejection != null) ui.Notice(_controller.LastRejection);
            ui.Button(L("Kapat"), () => Open = false, style: UiButtonStyle.Secondary, height: UiTheme.SmallButtonHeight);
        }

        /// <summary>A card's block: its name in bold and what it does under it.</summary>
        private UiBuilder CardBlock(UiBuilder ui, int card)
        {
            UiBuilder block = ui.Card();
            CardKind kind = Board.Cards[card].Kind;
            // The card's picture beside its name and text, as on the card in the middle (2026-11-09).
            UiBuilder row = block.Row(12f, expand: false);
            Sprite picture = CardArt.Get(CardArt.PictureFor(kind));
            if (picture != null)
            {
                RectTransform rect = UiRoot.Rect(row.Container, "Picture", Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
                var image = rect.gameObject.AddComponent<Image>();
                image.sprite = picture;
                image.preserveAspect = true;
                image.raycastTarget = false;
                UiBuilder.Fix(rect.gameObject, ThumbWidth, ThumbWidth * 3f / 8f);
            }

            UiBuilder words = row.Column();
            Text name = words.Label(CardPlay.Name(Board, card), UiTextStyle.Body, CardArt.ToneColor(CardArt.ToneFor(kind)));
            name.font = UiTheme.BoldFont;
            words.Label(CardPlay.Body(Board, card), UiTextStyle.Small, UiTheme.Muted);
            return block;
        }

        private void RenderPlay(UiBuilder block, GameState state, int card)
        {
            CardDefinition definition = Board.Cards[card];
            int id = state.CurrentPlayer.Id;
            switch (definition.Kind == CardKind.Fund ? (CardTarget?)null : definition.Aim)
            {
                case null:
                {
                    UiBuilder row = block.Row();
                    PlayButton(row, F($"{M(definition.Amount)} al"), new PlayCardCommand(id, card, -1, 0));
                    PlayButton(row, F($"{M(definition.Cost)} yatır"), new PlayCardCommand(id, card, -1, 1));
                    break;
                }

                case CardTarget.None:
                    PlayButton(block, L("Oyna"), new PlayCardCommand(id, card));
                    break;

                case CardTarget.Rival:
                {
                    UiBuilder row = null;
                    int inRow = 0;
                    foreach (int rival in GameRules.CardTargets(Board, state, state.CurrentPlayer, card))
                    {
                        if (row == null || inRow == 2)
                        {
                            row = block.Row();
                            inRow = 0;
                        }

                        int due = GameRules.AuditDue(Board, state, definition, rival);
                        PlayButton(row, $"{EventDescriber.NameOf(state, rival)} ({M(due)})", new PlayCardCommand(id, card, rival));
                        inRow++;
                    }

                    break;
                }

                default:
                    block.Button(L("Tahtadan seç"), () => Aim(state, card), true, UiButtonStyle.Primary, UiTheme.SmallButtonHeight);
                    break;
            }
        }

        private void PlayButton(UiBuilder ui, string label, PlayCardCommand play)
        {
            ui.Button(label, () =>
            {
                Open = false;
                _controller.Submit(play);
            }, _controller.IsAllowed(play), UiButtonStyle.Primary, UiTheme.SmallButtonHeight);
        }

        /// <summary>
        /// The bottom panel while a card's town is picked on the board: what the card does, the picked town with what it
        /// costs or does there, the button that plays it, and the way back to the hand.
        /// </summary>
        public void RenderAim(UiBuilder ui, GameState state, int picked, Action unpick)
        {
            int card = Aiming(state);
            if (card == NoCard) return;
            CardDefinition definition = Board.Cards[card];
            PlayerState player = state.CurrentPlayer;
            UiBuilder block = CardBlock(ui, card);
            block.Label(AimHint(definition.Aim), UiTextStyle.Small);

            var play = new PlayCardCommand(player.Id, card, picked);
            bool chosen = picked >= 0 && GameRules.CardTargets(Board, state, player, card).Contains(picked);
            if (chosen && definition.IsAttack && state.HasShield(picked))
            {
                ui.Notice(L("Bu şehirde koruma kalkanı var: saldırın durur, kalkan kalkar."));
            }

            if (chosen && _controller.IsAllowed(play))
            {
                ui.Button(PlayLabel(definition, picked), () =>
                {
                    StopAiming();
                    unpick();
                    _controller.Submit(play);
                }, style: UiButtonStyle.Primary, height: UiLayout.CardButtonHeight);
            }
            else
            {
                ui.Button(L("Önce tahtadan bir yer seç"), null, false, UiButtonStyle.Primary, UiLayout.CardButtonHeight);
            }

            ui.Button(L("Vazgeç"), () =>
            {
                StopAiming();
                unpick();
                Open = true;
            });
        }

        private static string AimHint(CardTarget aim)
        {
            switch (aim)
            {
                case CardTarget.OwnTown: return L("Tahtada parlayan şehirlerinden birine tıkla.");
                case CardTarget.FreeProperty: return L("Tahtada parlayan sahipsiz şehir ya da tatil yerlerinden birine tıkla.");
                case CardTarget.RivalTown: return L("Tahtada parlayan rakip şehirlerden birine tıkla.");
                default: return L("Tahtada parlayan karelerden gitmek istediğine tıkla.");
            }
        }

        /// <summary>The play button for a picked square: "Paris'e oyna", with the price where the card costs money.</summary>
        private string PlayLabel(CardDefinition card, int square)
        {
            string town = L(Board[square].Name);
            switch (card.Kind)
            {
                case CardKind.LandDeal: return F($"{town} al ({M(GameRules.LandDealCost(Board, card, square))})");
                case CardKind.Renewal: return F($"{town} için bina kur ({M(GameRules.RenewalCost(Board, card, square))})");
                case CardKind.Chauffeur: return F($"{town} karesine git");
                default: return F($"Oyna: {town}");
            }
        }

        /// <summary>True while a person here must let a card go: a fifth was drawn.</summary>
        public bool Discarding(GameState state) => WaitsHere(state, GamePhase.AwaitingCardDiscard);

        /// <summary>The discard window: every card with "Bırak"; the one let go goes back to the deck.</summary>
        public void RenderDiscard(UiBuilder ui, GameState state)
        {
            if (!Discarding(state)) return;
            PlayerState player = state.CurrentPlayer;
            ui.Title(UiIcon.Warning, L("Elin dolu"));
            ui.Label(F($"En çok {GameRules.HandLimit} kart tutabilirsin. Bırakacağın kartı seç; desteye geri döner."), UiTextStyle.Small, UiTheme.Muted);
            foreach (int card in player.Hand)
            {
                UiBuilder block = CardBlock(ui, card);
                var discard = new DiscardCardCommand(player.Id, card);
                block.Button(L("Bırak"), () => _controller.Submit(discard), _controller.IsAllowed(discard), UiButtonStyle.Secondary, UiTheme.SmallButtonHeight);
            }
        }

        /// <summary>True while a person here may answer rent with the rent shield card.</summary>
        public bool AskingRent(GameState state) => WaitsHere(state, GamePhase.AwaitingRentShield) && state.RentDue != null;

        /// <summary>The rent shield's question: the rent due and to whom, "Kira Kalkanı kullan" or "Kirayı öde".</summary>
        public void RenderRentShield(UiBuilder ui, GameState state)
        {
            if (!AskingRent(state)) return;
            PlayerState player = state.CurrentPlayer;
            DebtState due = state.RentDue;
            ui.Title(UiIcon.Warning, CardPlay.Name(CardKind.RentShield));
            ui.Label(F($"{L(Board[player.Position].Name)}: {EventDescriber.CreditorText(state, due.CreditorId)} oyuncusuna {M(due.Amount)} kira ödeyeceksin."),
                UiTextStyle.Body);
            ui.Label(L("Kira Kalkanı kartını oynarsan bu kirayı hiç ödemezsin; kart desteye döner."), UiTextStyle.Small, UiTheme.Muted);
            int card = GameRules.HeldCard(Board, player, CardKind.RentShield);
            var shield = new PlayCardCommand(player.Id, card);
            ui.Button(L("Kira Kalkanı kullan"), () => _controller.Submit(shield), card >= 0 && _controller.IsAllowed(shield), UiButtonStyle.Primary,
                UiLayout.CardButtonHeight);
            var pay = new PayRentCommand(player.Id);
            ui.Button(F($"Kirayı öde ({M(due.Amount)})"), () => _controller.Submit(pay), _controller.IsAllowed(pay), UiButtonStyle.Secondary,
                UiLayout.CardButtonHeight);
        }
    }
}
