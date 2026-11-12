using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.Achievements;
using Lazpoly.App.Avatars;
using Lazpoly.App.Lobby;
using Lazpoly.App.Presence;
using Lazpoly.App.Save;
using Lazpoly.App.Session;
using Lazpoly.App.AI;
using Lazpoly.Core.Board;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Infra.Logging;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Perf;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using Lazpoly.Presentation.UI;
using UnityEngine;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Root of the Game scene: creates the session (local, online host or online client) from the
    /// menu's setup, builds the board and pawns once the state is known, and plays the session's
    /// events back as animations, one at a time.
    /// </summary>
    public sealed class GameSceneController : MonoBehaviour
    {
        private const int MaxLogLines = 40;

        /// <summary>Pause after a card is drawn so it can be read before it acts.</summary>
        public const float CardReadSeconds = 1.6f;

        /// <summary>
        /// How long a drawn card stays up in the middle. The table waits as long, so the card's money moves only once
        /// the card has closed (the owner's wish, 2026-11-12).
        /// </summary>
        public const float CardSeconds = CardReadSeconds + 1.6f;

        /// <summary>How long the table waits on a card a person here drew to keep: its buttons ("Kullan", "Sakla") come up as it has swelled in.</summary>
        public const float CardOfferSeconds = 0.35f;

        /// <summary>Pause after the dice settle, so the result is read before the pawn moves.</summary>
        public const float RollReadSeconds = 0.55f;

        /// <summary>How long the line over the table (who rolls first) stays up.</summary>
        public const float AnnouncementSeconds = 3f;

        private readonly Queue<GameEvent> _pendingEvents = new Queue<GameEvent>();
        private readonly Dictionary<int, PawnView> _pawns = new Dictionary<int, PawnView>();
        private readonly BubbleQueue _bubbleQueue = new BubbleQueue();
        private SpeechBubble _bubble;
        private float _bubbleShownAt;
        private PawnCrowd _crowd;
        private TurnMarker _turnMarker;
        private readonly Dictionary<int, Color> _playerColors = new Dictionary<int, Color>();
        private readonly List<LogEntry> _log = new List<LogEntry>();
        private readonly Dictionary<int, int> _shownMoney = new Dictionary<int, int>();
        private int _shownPot;

        /// <summary>What the playback has shown so far of the turn: who last moved, which phase was reached, which trade is open; the bubbles' lines depend on them.</summary>
        private int _lastMover = GameState.NoOwner;
        private GamePhase _shownPhase;
        private bool _boughtOnLanding;
        private TradeOffer _shownOffer;

        /// <summary>The bottom panel's height is rounded up to this many pixels before the camera makes room, so a line more or less in the panel does not nudge the view.</summary>
        private const float BottomStep = 40f;

        private BoardData _data;
        private IGameSession _session;
        private HostSession _host;
        private ClientSession _client;
        private ILobbyService _lobby;
        private ulong _roomId;
        private ulong _hostPeer;
        private int _nextRejoinAttempt = RejoinAfterAttempts;
        private float _nextPresenceRefresh;
        private LocalGame _localGame;
        private float _nextAiMoveTime;

        /// <summary>A new game's draw for who rolls first waits to be shown until the loading screen has gone.</summary>
        private bool _drawToAnnounce;

        /// <summary>A new game's starting money still to come up through the hatches before the draw (the owner's wish), and whether it is on its way.</summary>
        private bool _dealToShow;
        private bool _dealing;
        private bool _logAfterDeal;

        /// <summary>The game was loaded from a save: its money is already in the cases.</summary>
        private bool _resumedGame;
        private SettingsService _settings;
        private IAchievementService _achievements;
        private IPresenceService _presence;
        private AchievementTracker _tracker;
        private BoardView _board;
        private DiceView _dice;
        private Coroutine _playback;
        private bool _playing;
        private bool _isOnline;
        private int _framedWidth;
        private int _framedHeight;
        private float _framedReserve;
        private float _framedBottom;
        private float _labelYaw = BoardCameraRig.BaseYaw;
        private GameHud _hud;
        private int _screenshotPreset = -1;

        /// <summary>Unanswered knocks on the host's door after which a guest leaves and re-enters the room, in case the room dropped it too.</summary>
        private const int RejoinAfterAttempts = 2;

        /// <summary>How often the side panel re-reads everyone's connection and latency, in seconds.</summary>
        private const float PresenceRefreshSeconds = 2f;

        public IGameSession Session => _session;

        public bool IsOnline => _isOnline;

        /// <summary>A guest whose link to the host broke and who is still trying to get back in.</summary>
        public bool IsReconnecting => _client != null && _client.IsReconnecting;

        public int ReconnectAttempts => _client?.ReconnectAttempts ?? 0;

        public double SecondsUntilGiveUp => _client?.SecondsUntilGiveUp ?? 0;

        /// <summary>
        /// Seconds the person on turn here has left before the game decides for them. Locally the
        /// <see cref="DecisionClock"/> times it from the moment the board stopped moving. Online the host
        /// keeps the real clock from its last accepted move, plus <see cref="HostSession.GraceSeconds"/> for
        /// the animations; the count shown here stays full during that grace and then runs to the host's deadline.
        /// Infinite at a table without a clock.
        /// </summary>
        public float SecondsLeft
        {
            get
            {
                if (_session?.State == null) return float.PositiveInfinity;
                if (!_isOnline) return (float)_decisionClock.Left;
                float limit = (float)TurnClock.LimitFor(_session.State.Phase, _session.Rules.TurnSeconds);
                return Mathf.Min(limit, limit + (float)HostSession.GraceSeconds - (Time.unscaledTime - _lastProgressTime));
            }
        }

        private readonly DecisionClock _decisionClock = new DecisionClock();

        /// <summary>The big line over the table (who rolls first), whose it is and when it went up.</summary>
        public string Announcement { get; private set; }
        public int AnnouncementPlayer { get; private set; } = GameState.NoOwner;
        public float AnnouncementTime { get; private set; } = float.NegativeInfinity;

        /// <summary>The last moves and answers, for a problem report.</summary>
        private readonly SessionTrail _trail = new SessionTrail();
        private ErrorLogService _errorLog;
        private bool _trailDumped;
        private int _langVersion;

        /// <summary>Where the last problem report went; null until the player asks for one.</summary>
        public string ReportNote { get; private set; }

        private float _lastProgressTime;
        /// <summary>The last <see cref="MaxLogLines"/> lines of the event log, oldest first.</summary>
        public IReadOnlyList<LogEntry> Log => _log;
        public string LastRejection { get; private set; }

        /// <summary>Zoom and turn of the board view; the HUD's reset button uses it.</summary>
        public BoardCameraRig CameraRig { get; private set; }

        /// <summary>Frame rate and memory watcher; the HUD's F3 strip reads it.</summary>
        public PerfProbe Probe { get; private set; }

        /// <summary>Seconds from the menu's button to the table on screen; -1 when the scene was not entered from the menu.</summary>
        public float LoadSeconds { get; private set; } = -1f;

        /// <summary>Frames the next store screenshot: steps through <see cref="ScreenshotPresets"/> (F11).</summary>
        public void NextScreenshotPreset()
        {
            if (_board == null || CameraRig == null) return;
            _screenshotPreset = (_screenshotPreset + 1) % ScreenshotPresets.All.Length;
            ScreenshotPreset preset = ScreenshotPresets.At(_screenshotPreset);
            CameraRig.SetView(preset.Yaw, preset.Zoom);
            AddLog(F($"Ekran görüntüsü açısı: {L(preset.Name)}"));
        }

        /// <summary>Saves a screenshot to the player's data folder (F12) and notes it in the log.</summary>
        public void TakeScreenshot()
        {
            string path = Screenshots.Capture();
            Debug.Log($"[Lazpoly] Screenshot: {path}");
            AddLog(L("Ekran görüntüsü kaydedildi (Screenshots klasörü)."));
        }

        /// <summary>The board on the table, once the game state is known.</summary>
        public BoardView Board => _board;

        /// <summary>The rings over the squares: where a pawn is going, and what the person here may click.</summary>
        public SquareMarkers Markers { get; private set; }

        /// <summary>Goes up whenever something the HUD shows may have changed, so it knows when to redraw.</summary>
        public int Revision { get; private set; }

        /// <summary>Why an online game stopped; null while it goes on.</summary>
        public SessionEndReason? EndReason { get; private set; }

        /// <summary>The faces on the players' cards (Steam profile pictures and character portraits), once the game state is known.</summary>
        public PlayerFaces Faces { get; private set; }

        /// <summary>The lobby as the game began, for which Steam account plays which seat online.</summary>
        private IReadOnlyList<LobbyMember> _startMembers;

        /// <summary>A player's piece on the board, or null.</summary>
        public PawnView PawnOf(int playerId) => _pawns.TryGetValue(playerId, out PawnView pawn) ? pawn : null;

        /// <summary>True while events are still being animated; the HUD waits before offering actions.</summary>
        public bool IsBusy => _playing || _pendingEvents.Count > 0 || _drawToAnnounce || _dealing;

        /// <summary>
        /// True while the pause menu holds a local game still: the computer does not move and the
        /// next event waits to be shown. An online game cannot be paused; the host's clock keeps running.
        /// </summary>
        public bool Paused { get; private set; }

        /// <summary>Whether this is the machine running an online game; leaving ends the game for everyone.</summary>
        public bool IsHost => _host != null;

        /// <summary>Holds or releases a local game; see <see cref="Paused"/>. Online it does nothing.</summary>
        public void SetPaused(bool paused) => Paused = paused && !_isOnline;

        private GameSettings Settings => _settings?.Current ?? GameSettings.Default;

        /// <summary>
        /// A player's money as far as the animations have got: the state already holds the result of
        /// events still waiting to be shown, and the HUD should not give away a payment before it happens.
        /// </summary>
        public int ShownMoneyOf(int playerId) =>
            _shownMoney.TryGetValue(playerId, out int money) ? money : _session?.State?.FindPlayer(playerId)?.Money ?? 0;

        /// <summary>The pot as far as the animations have got (see <see cref="ShownMoneyOf"/>).</summary>
        public int ShownPot => _shownPot;

        /// <summary>The card being shown (index into the board's deck) and who drew it; -1 when none.</summary>
        public int ShownCard { get; private set; } = -1;
        public int ShownCardPlayer { get; private set; } = -1;

        /// <summary>The square the shown card was drawn on (Sokak Haberi or a Şans square), for the card's title; -1 when none.</summary>
        public int ShownCardSquare { get; private set; } = -1;

        /// <summary>Where each pawn last stopped on the table, as shown (the state may already be further on).</summary>
        private readonly Dictionary<int, int> _shownSquares = new Dictionary<int, int>();

        /// <summary>The game time the shown card was drawn, so the HUD can fade it.</summary>
        public float ShownCardTime { get; private set; }

        /// <summary>What the shown card pays its player (over zero) or costs them (under zero), written on the card; 0 for a card that moves no money.</summary>
        public int ShownCardMoney { get; private set; }

        /// <summary>Takes the drawn card down at once: the person it asked has answered.</summary>
        public void DismissCard()
        {
            if (ShownCard < 0) return;
            ShownCard = -1;
            Revision++;
        }

        /// <summary>
        /// What a card just shown moves for <paramref name="playerId"/>, from the events that follow it: the money it
        /// pays or takes at once, or the debt it opens when the player cannot pay; only a drawn card's own money when
        /// <paramref name="cardOnly"/>. The count stops where the card's own effect ends: at a move, a roll, another
        /// card or the next decision.
        /// </summary>
        public static int CardMoney(int playerId, IEnumerable<GameEvent> next, bool cardOnly)
        {
            int sum = 0;
            if (next == null) return sum;
            foreach (GameEvent after in next)
            {
                switch (after)
                {
                    case MoneyChangedEvent moved when moved.PlayerId == playerId && (!cardOnly || moved.Reason == MoneyReason.Card):
                        sum += moved.Delta;
                        break;
                    case DebtIncurredEvent owed when owed.PlayerId == playerId && (!cardOnly || owed.Reason == MoneyReason.Card):
                        sum -= owed.Amount;
                        break;
                    case PlayerMovedEvent _:
                    case DiceRolledEvent _:
                    case CardDrawnEvent _:
                    case CardPlayedEvent _:
                    case PhaseChangedEvent _:
                    case TurnStartedEvent _:
                        return sum;
                }
            }

            return sum;
        }

        /// <summary>Whether a person at this machine plays <paramref name="playerId"/> themselves.</summary>
        private bool PersonHere(int playerId) => _session.CanControl(playerId) && !_session.IsAiPlaying(playerId);

        /// <summary>The last trade that ended, how it ended and the game time it did, for the result card in the middle; null before any.</summary>
        public TradeOffer ShownTrade { get; private set; }
        public TradeOutcome ShownTradeOutcome { get; private set; }
        public float ShownTradeTime { get; private set; }

        /// <summary>The last payment someone could not make and the game time it came, for the "cannot pay" card in the middle; null before any.</summary>
        public DebtIncurredEvent ShownDebt { get; private set; }

        /// <summary>The last strategic card played and the game time it was, for its card in the middle (Faz 30); null before any.</summary>
        public CardPlayedEvent ShownPlay { get; private set; }
        public float ShownPlayTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Whether the card shown in <see cref="ShownPlay"/> was an attack a protection card's shield stopped.</summary>
        public bool ShownPlayBlocked { get; private set; }

        /// <summary>What the card shown in <see cref="ShownPlay"/> pays or costs its player, written on the card; 0 when it moves none of their money.</summary>
        public int ShownPlayMoney { get; private set; }

        /// <summary>The last takeover of a rival's town (Faz 31), for its card in the middle; the square is -1 before any.</summary>
        public int ShownTakeoverSquare { get; private set; } = -1;
        public int ShownTakeoverBuyer { get; private set; } = GameState.NoOwner;
        public int ShownTakeoverSeller { get; private set; } = GameState.NoOwner;
        public int ShownTakeoverPrice { get; private set; }
        public float ShownTakeoverTime { get; private set; } = float.NegativeInfinity;

        /// <summary>The cup's last move, while its card "Şampiyonlar Kupası maçları X şehrinde yapılıyor" is up (Faz 29).</summary>
        public CupHostedEvent ShownCup { get; private set; }
        public float ShownCupTime { get; private set; } = float.NegativeInfinity;

        /// <summary>
        /// The player whose third double in a row sends them to jail, while "Art arda 3 kez çift attın..." is up (the
        /// owner's wish, Faz 29); <see cref="GameState.NoOwner"/> when none has been.
        /// </summary>
        public int ShownThirdDouble { get; private set; } = GameState.NoOwner;
        public float ShownThirdDoubleTime { get; private set; } = float.NegativeInfinity;

        private IEnumerator ShowTakeover(PropertyOwnerChangedEvent taken, int price)
        {
            ShownTakeoverSquare = taken.SquareIndex;
            ShownTakeoverBuyer = taken.NewOwnerId;
            ShownTakeoverSeller = taken.PreviousOwnerId;
            ShownTakeoverPrice = price;
            ShownTakeoverTime = Time.time;
            Revision++;
            Say(taken.NewOwnerId, L("Bu şehir artık benim!"));
            yield return new WaitForSeconds(CenterNotice.Seconds);
        }

        /// <summary>Players already bankrupt in the state whose fall the table has not shown yet: their corner cards still look as before.</summary>
        private readonly HashSet<int> _bankruptPending = new HashSet<int>();

        /// <summary>
        /// Whether <paramref name="player"/>'s corner card shows them bankrupt: only once their figure starts to fall (the
        /// owner's wish, Faz 29), after the card saying they cannot pay.
        /// </summary>
        public bool ShowsBankrupt(PlayerState player) => player != null && player.IsBankrupt && !_bankruptPending.Contains(player.Id);
        public float ShownDebtTime { get; private set; }

        /// <summary>The last trade agreement card (a whole colour group in one hand): its sentence, whose it is and the game time it came; null before any.</summary>
        public string ShownGroupDeal { get; private set; }
        public int ShownGroupDealPlayer { get; private set; } = GameState.NoOwner;
        public float ShownGroupDealTime { get; private set; }

        /// <summary>Each town's owner as shown on the table (the state may already be further on), to tell when a group completes.</summary>
        private readonly Dictionary<int, int> _shownOwners = new Dictionary<int, int>();

        /// <summary>Groups just completed, (player, group), waiting for their card once the change that completed them is shown.</summary>
        private readonly List<KeyValuePair<int, int>> _groupDealsToShow = new List<KeyValuePair<int, int>>();

        /// <summary>Groups whose towns still show the rent from before their trade agreement, until its fireworks are over (the owner's wish, Faz 27).</summary>
        private readonly HashSet<int> _heldRentGroups = new HashSet<int>();

        /// <summary>The game time the last trade agreement's fireworks went up; -infinity before any.</summary>
        public float ShownFireworksTime { get; private set; } = float.NegativeInfinity;

        /// <summary>The last roll shown on the table (0 before the first), and real time it settled, for the HUD's result popup.</summary>
        public int ShownDie1 { get; private set; }
        public int ShownDie2 { get; private set; }
        public float ShownRollTime { get; private set; } = float.NegativeInfinity;

        /// <summary>How long the roll's total stays up in the middle of the screen (<see cref="RollTotal.Seconds"/> at the game's speed).</summary>
        public float ShownRollLasts { get; private set; } = RollTotal.Seconds;

        /// <summary>Whether the shown roll was the third double in a row, which forfeits the move.</summary>
        public bool ShownRollForfeited { get; private set; }

        /// <summary>True when the roll just shown is a third double that sends the player straight to jail.</summary>
        public bool ShownRollJailed { get; private set; }

        /// <summary>Who made the roll on show.</summary>
        public int ShownRollPlayer { get; private set; } = GameState.NoOwner;

        /// <summary>Whether the roll on show was made by someone at this screen: the "you roll again" note is for them alone.</summary>
        public bool ShownRollByPerson => _session != null && ShownRollPlayer != GameState.NoOwner && _session.CanControl(ShownRollPlayer) && !_session.IsAiPlaying(ShownRollPlayer);

        /// <summary>Payments still in the queue that an earlier flight already carried (more houses bought at once).</summary>
        private readonly HashSet<GameEvent> _flownAlong = new HashSet<GameEvent>();
        // Gains that a player's payment has already carried to the receiver's safe: they do not come up from the bank again.
        private readonly HashSet<GameEvent> _paidGains = new HashSet<GameEvent>();

        public Color ColorOf(int playerId) => _playerColors.TryGetValue(playerId, out Color color) ? color : Color.white;

        /// <summary>The colour group a square belongs to.</summary>
        public int GroupOf(int squareIndex) => _session.Board[squareIndex].GroupId;

        /// <summary>Colour of the group a square belongs to, as the board shows it.</summary>
        public Color GroupColorOf(int squareIndex) => _data.GroupColor(_session.Board[squareIndex].GroupId);

        /// <summary>The letter on the square's group marks when the colour-blind friendly colours are on; null otherwise.</summary>
        public string GroupMarkOf(int squareIndex) => _data.GroupMark(_session.Board[squareIndex].GroupId);

        /// <summary>Name of the group a square belongs to, from the board file (null if it has none).</summary>
        public string GroupNameOf(int squareIndex) => _data.GroupName(_session.Board[squareIndex].GroupId);

        private void Start()
        {
            UiTheme.Use(UiPalette.Paper); // The table wears the menu's paper look: cream panels, green ink, a green band for news.
            AudioPlayer.Ensure();
            MusicPlayer.Ensure();
            _data = BoardDataLoader.LoadFromResources();
            GameServices.Registry?.TryGet(out _settings);
            GameServices.Registry?.TryGet(out _achievements);
            GameServices.Registry?.TryGet(out _presence);
            GameServices.Registry?.TryGet(out _errorLog);
            Application.logMessageReceived += OnLogMessage;
            if (_settings != null)
            {
                Lang.Apply(_settings.Current.Language);
                // Read once here: the board's bands and the pawns are painted at build time, so a change waits for the next table.
                ColorVision.Apply(_settings.Current.ColorBlind);
                _settings.Changed += OnSettingsChanged;
            }

            _langVersion = Lang.Version;
            _session = CreateSession(OnlineGameSetup.Take());
            _tracker = new AchievementTracker(_data.Board, _session.CanControl, _session.IsAiPlaying, _isOnline);

            _session.EventsPublished += OnEventsPublished;
            _session.CommandRejected += OnCommandRejected;
            _session.StateReset += OnStateReset;
            _session.Ended += OnEnded;
            _session.Ready += BuildViews;
            if (_host != null) _host.RemotePlayerConnectionChanged += OnRemoteConnectionChanged;
            if (_isOnline)
            {
                GameServices.Registry?.TryGet(out _lobby);
                _roomId = _lobby != null ? _lobby.RoomId : 0UL;
                _startMembers = _lobby != null ? new List<LobbyMember>(_lobby.Members) : null;
            }

            if (_client != null)
            {
                _client.ConnectionChanged += OnConnectionChanged;
                if (_lobby != null) _lobby.MemberLeft += OnLobbyMemberLeft;
                LastRoomStore.Remember(_roomId);
            }

            SetUpCamera();
            Probe = PerfProbe.Ensure(gameObject);
            _hud = gameObject.AddComponent<GameHud>();
            _hud.Bind(this);

            if (_session.IsReady) BuildViews();
            else AddLog(L("Ev sahibine bağlanılıyor..."));

            if (_session is HostSession host) host.Start();
            else if (_session is ClientSession client) client.Start();
        }

        private void Update()
        {
            if (_session == null) return;
            _session.Tick();
            RunGameClock();
            ShowMoneyPiles();
            if (_client != null) RejoinRoomIfNeeded();
            if (_isOnline && Time.unscaledTime >= _nextPresenceRefresh)
            {
                _nextPresenceRefresh = Time.unscaledTime + PresenceRefreshSeconds;
                Revision++; // Connection dots, latency and the reconnect countdown move without a game event.
            }

            if (Faces != null && Faces.Update(Time.unscaledTime)) Revision++; // A Steam profile picture arrived.
            ShowNextBubble();
            if (_bubble != null && _hud != null) _bubble.TopClearPixels = _hud.ReservedTopPixels;
            AnnounceDraw();
            UpdateDecisionClock();
            if (_board == null || Paused || IsBusy || EndReason.HasValue || Time.time < _nextAiMoveTime) return;
            if (_session.StepAi()) _nextAiMoveTime = Time.time + Settings.AiMoveDelaySeconds;
        }

        /// <summary>
        /// A local game's decision clock: while it waits on a person at this screen it counts down (not while
        /// the board animates or the game is paused) and, when the time is up, makes the decision for them as
        /// <see cref="TimeoutPolicy"/> says. Online the host does this.
        /// </summary>
        private void UpdateDecisionClock()
        {
            GameState state = _session.State;
            int person = _isOnline || _board == null || state == null || state.Phase == GamePhase.GameOver || EndReason.HasValue
                ? GameState.NoOwner
                : PersonToAct(state);
            double limit = person == GameState.NoOwner ? double.PositiveInfinity : TurnClock.LimitFor(state.Phase, _session.Rules.TurnSeconds);
            int key = HashCode.Combine(Revision, person, state?.Phase);
            if (!_decisionClock.Tick(Time.unscaledDeltaTime, key, limit, Paused || IsBusy)) return;

            AddLog(F($"{state.FindPlayer(person)?.Name} için süre doldu"), LogTone.Neutral, person);
            foreach (GameCommand command in TimeoutPolicy.CommandsFor(_session.Board, _session.Rules, state, person)) Submit(command);
        }

        /// <summary>The first person at this screen (not the computer) the game waits on; none when it waits on nobody here.</summary>
        private int PersonToAct(GameState state)
        {
            foreach (int id in AiTurnOrder.PlayersToAct(state))
            {
                if (_session.CanControl(id) && !_session.IsAiPlaying(id)) return id;
            }

            return GameState.NoOwner;
        }

        private void LateUpdate()
        {
            if (_board == null || _hud == null) return;
            // The board's writing turns to stay readable when a screenshot preset looks from another corner.
            if (CameraRig != null && !Mathf.Approximately(CameraRig.Yaw, _labelYaw))
            {
                _labelYaw = CameraRig.Yaw;
                _board.FaceCamera(_labelYaw);
                foreach (PawnView pawn in _pawns.Values) pawn.FaceCamera(_labelYaw);
            }

            float reserved = _hud.ReservedSidePixels;
            float bottom = Mathf.Ceil(_hud.ReservedBottomPixels / BottomStep) * BottomStep;
            if (Screen.width == _framedWidth && Screen.height == _framedHeight && Mathf.Approximately(reserved, _framedReserve) && Mathf.Approximately(bottom, _framedBottom)) return;
            FrameBoard(reserved, bottom);
        }

        /// <summary>Frames the board in the screen area the panels leave free; the rig slides there over a few frames.</summary>
        private void FrameBoard(float reservedSidePixels, float reservedBottomPixels)
        {
            if (CameraRig == null) return;
            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
            _framedReserve = reservedSidePixels;
            _framedBottom = reservedBottomPixels;
            float size = BoardCameraFraming.OrthographicSize(_board.HalfExtent, BoardView.PlateThickness, BoardCameraRig.Pitch,
                Screen.width, Screen.height, reservedSidePixels, reservedBottomPixels);
            Vector3 aim = BoardCameraFraming.Aim(_board.transform.position, BoardView.PlateThickness);
            aim = BoardCameraFraming.AimAbovePanel(aim, BoardCameraRig.Pitch, CameraRig.Yaw, size, Screen.height, reservedBottomPixels);
            CameraRig.SetBase(_board.transform.position, aim, size, _board.HalfExtent);
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            if (_session == null) return;
            _session.EventsPublished -= OnEventsPublished;
            _session.CommandRejected -= OnCommandRejected;
            _session.StateReset -= OnStateReset;
            _session.Ended -= OnEnded;
            _session.Ready -= BuildViews;
            if (_host != null) _host.RemotePlayerConnectionChanged -= OnRemoteConnectionChanged;
            if (_client != null) _client.ConnectionChanged -= OnConnectionChanged;
            if (_client != null && _lobby != null) _lobby.MemberLeft -= OnLobbyMemberLeft;
            _host?.Leave(); // Closing the window counts as leaving; the guests should not wait out their grace time.
            Faces?.Dispose();
        }

        /// <summary>
        /// Whether this machine may send the command and the rules would accept it now. The HUD
        /// offers only such actions; the host still validates whatever arrives.
        /// </summary>
        public bool IsAllowed(GameCommand command)
        {
            if (_session == null || !_session.IsReady || EndReason.HasValue || IsReconnecting || !_session.CanControl(command.PlayerId)) return false;
            return GameRules.Validate(_session.Board, _session.Rules, _session.State, command) == RejectionReason.None;
        }

        public void Submit(GameCommand command)
        {
            LastRejection = null;
            Revision++;
            _trail.Command(command);
            _session.Submit(command);
        }

        /// <summary>
        /// Writes what a reader of the error log needs to follow a problem: the game's shape and the
        /// last moves. The player can then send the log file; the settings window shows where it is.
        /// </summary>
        public void ReportProblem()
        {
            if (_errorLog == null)
            {
                ReportNote = L("Günlük servisi yok; rapor yazılamadı.");
                Revision++;
                return;
            }

            _errorLog.Report("Oyuncu sorun bildirdi", SituationLines());
            ReportNote = F($"Rapor günlüğe yazıldı: {_errorLog.FilePath}");
            Application.OpenURL("file://" + _errorLog.Folder);
            Revision++;
        }

        /// <summary>Session facts plus the trail, one line each.</summary>
        private IEnumerable<string> SituationLines()
        {
            GameState state = _session?.State;
            string mode = !_isOnline ? "yerel" : _host != null ? "çevrim içi, ev sahibi" : "çevrim içi, misafir";
            yield return $"oturum: {mode}; oyuncu {state?.Players.Count ?? 0}; tur {state?.TurnNumber ?? 0}; el {state?.Round ?? 0}; evre {state?.Phase}";
            yield return $"masa: {TableSetup.Summary(_session?.Rules.Table ?? Core.Rules.TableRules.Default, _data.Rules.StartingMoney)}";
            if (_isOnline)
            {
                yield return $"bağlantı: oda {_roomId}; yeniden bağlanıyor {IsReconnecting}; bitiş {EndReason}";
                if (state != null)
                {
                    foreach (PlayerState player in state.Players)
                    {
                        yield return $"  p{player.Id} {player.Name}: bağlı {_session.IsConnected(player.Id)}, bilgisayar {_session.IsAiPlaying(player.Id)}, {_session.PingMs(player.Id)} ms";
                    }
                }
            }

            if (state != null) yield return $"durum özeti: {state.ComputeHash():X16}";
            yield return "son hamleler:";
            foreach (string line in _trail.Lines) yield return "  " + line;
        }

        /// <summary>The first exception in a game gets the trail written next to it, once, so the log tells how it came about.</summary>
        private void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (_trailDumped || _errorLog == null || (type != LogType.Exception && type != LogType.Error)) return;
            _trailDumped = true;
            _errorLog.Report("Hata anındaki oyun durumu", SituationLines());
        }

        /// <summary>Leaves the game (and the online room, if any) and returns to the main menu.</summary>
        public void LeaveToMainMenu()
        {
            SaveLocalGame();

            if (_isOnline)
            {
                _host?.Leave();
                LastRoomStore.Forget();
                _lobby?.Leave();
            }

            SceneFader.Load("MainMenu");
        }

        /// <summary>
        /// A guest whose host is gone can keep the game from the last state it saw: it becomes a local
        /// game, the other seats go to the computer, and it is saved like any local game.
        /// </summary>
        public bool CanContinueOffline =>
            EndReason == SessionEndReason.HostLeft && _client != null && _client.State != null && _client.State.Phase != GamePhase.GameOver;

        public void ContinueAgainstComputers()
        {
            if (!CanContinueOffline) return;
            GameState state = _client.State;
            var dice = new Pcg32Random(NewSeed());
            var ai = new Pcg32Random(NewSeed(), stream: 77UL);
            var save = new SavedGame
            {
                BoardChecksum = _data.Checksum,
                DiceState = dice.State,
                DiceIncrement = dice.Increment,
                AiState = ai.State,
                AiIncrement = ai.Increment,
                Table = _session.Rules.Table,
                Snapshot = GameStateSnapshot.Capture(state),
            };
            foreach (PlayerState player in state.Players)
            {
                if (player.Id != _client.MyPlayerId && !player.IsBankrupt) save.AiPlayers.Add(player.Id, new SavedAiPlayer { Difficulty = HostSession.StandInDifficulty });
            }

            LastRoomStore.Forget();
            _lobby?.Leave();
            LocalGameSetup.SetResume(save);
            SceneFader.Load("Game");
        }

        /// <summary>
        /// After a couple of unanswered knocks the room may have dropped this machine as well (the host
        /// only lets room members in), so it goes out and comes back in; then the next Hello gets through.
        /// </summary>
        private void RejoinRoomIfNeeded()
        {
            if (!_client.IsReconnecting || _lobby == null || !_lobby.IsAvailable || _roomId == 0) return;
            if (_client.ReconnectAttempts < _nextRejoinAttempt) return;
            _nextRejoinAttempt = _client.ReconnectAttempts + 2 * RejoinAfterAttempts;
            _lobby.Leave();
            _lobby.Join(_roomId);
        }

        private void OnConnectionChanged(bool connected)
        {
            if (connected) _nextRejoinAttempt = RejoinAfterAttempts;
            AddLog(connected ? L("Ev sahibine yeniden bağlanıldı.") : L("Ev sahibiyle bağlantı koptu; yeniden bağlanılıyor..."));
            Revision++;
        }

        /// <summary>The room says the host is out: the game cannot go on, whatever the link does.</summary>
        private void OnLobbyMemberLeft(ulong peer)
        {
            if (_client != null && _lobby != null && peer == _hostPeer) _client.ReportHostGone();
        }

        /// <summary>
        /// Starts a fresh local game with the same seats and computer players (the end screen's
        /// "play again"). Online games cannot be restarted from here.
        /// </summary>
        public bool CanRestart => _localGame != null && _session?.State != null;

        public void RestartLocalGame()
        {
            if (!CanRestart) return;
            GameState state = _session.State;
            SavedGame save = _localGame.Save(_data.Checksum);
            var seats = new List<PlayerSeat>();
            var ai = new Dictionary<int, AiDifficulty>();
            foreach (PlayerState player in state.Players)
            {
                seats.Add(new PlayerSeat(player.Id, player.Name, CharacterOf(state, player).Id));
                if (save.AiPlayers.TryGetValue(player.Id, out SavedAiPlayer computer)) ai[player.Id] = computer.Difficulty;
            }

            SaveGameStore.Delete();
            LocalGameSetup.SetSeats(seats, ai);
            SceneFader.Load("Game");
        }

        private IGameSession CreateSession(OnlineLaunch online)
        {
            if (online == null)
            {
                _localGame = CreateLocalGame();
                return _localGame.Session;
            }

            _isOnline = true;
            if (!online.IsHost)
            {
                _hostPeer = online.HostPeer;
                _client = new ClientSession(online.Transport, online.HostPeer, _data.Board, _data.Rules, _data.Checksum);
                return _client;
            }

            OnlineSeatPlan plan = online.HostPlan;
            // The host draws who rolls first; the others get the order with the game.
            IReadOnlyList<PlayerSeat> seats = FirstPlayerDraw.Draw(plan.Seats, DrawRandom());
            var engine = new GameEngine(_data.Board, _data.Rules.WithTable(TableSetup.Chosen), seats, NewRandom());
            _host = new HostSession(engine, online.Transport, plan.HostPlayerId, plan.RemotePlayers, _data.Checksum, NewSeed());
            return _host;
        }

        private LocalGame CreateLocalGame()
        {
            SavedGame save = LocalGameSetup.TakeResume();
            if (save != null)
            {
                try
                {
                    LocalGame resumed = LocalGame.Resume(_data.Board, _data.Rules, _data.Checksum, save);
                    _resumedGame = true;
                    AddLog(L("Kayıtlı oyun yüklendi."));
                    return resumed;
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning($"[Lazpoly] Saved game could not be resumed: {exception.Message}");
                    AddLog(L("Kayıt yüklenemedi, yeni oyun başladı."));
                }
            }

            IReadOnlyList<PlayerSeat> seats = FirstPlayerDraw.Draw(LocalGameSetup.TakeSeats(), DrawRandom());
            LocalGame game = LocalGame.Create(_data.Board, _data.Rules.WithTable(TableSetup.Chosen), seats,
                LocalGameSetup.TakeAiPlayers(), NewSeed());
            if (ScreenTour.Requested && !PerfRun.Requested)
            {
                // -shots: the person stands on a town for sale, for the city card and the rest of the tour.
                SavedGame tour = game.Save(_data.Checksum);
                ScreenTour.Stage(tour, _data.Board);
                return LocalGame.Resume(_data.Board, _data.Rules, _data.Checksum, tour);
            }

            if (!PerfRun.Requested || !(PerfRun.BuiltUp || PerfRun.InDebt)) return game;

            // -perf -built: the same table with every town owned and built, the heaviest board there can be.
            // -perf -debt: the player on turn is a person selling towns for rent they cannot pay.
            SavedGame staged = game.Save(_data.Checksum);
            if (PerfRun.BuiltUp) PerfRun.BuildUp(staged.Snapshot, _data.Board);
            if (PerfRun.InDebt) PerfRun.OpenDebt(staged, _data.Board);
            return LocalGame.Resume(_data.Board, _data.Rules, _data.Checksum, staged);
        }

        private static Pcg32Random NewRandom() => new Pcg32Random(NewSeed());

        /// <summary>The draw for the first roll, on its own stream so it never shares numbers with the dice.</summary>
        private static Pcg32Random DrawRandom() => new Pcg32Random(NewSeed(), stream: 91UL);

        private static ulong NewSeed() => unchecked((ulong)DateTime.UtcNow.Ticks);

        /// <summary>Keeps the single local save slot in step with the game; a finished game leaves no save. The -perf table is nobody's game, so it never touches the slot.</summary>
        private void SaveLocalGame()
        {
            if (_localGame == null || PerfRun.Requested || ScreenTour.Requested) return;
            try
            {
                if (_session.State.Phase == GamePhase.GameOver) SaveGameStore.Delete();
                else SaveGameStore.Write(_localGame.Save(_data.Checksum));
            }
            catch (Exception exception) when (exception is System.IO.IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Lazpoly] Game could not be saved: {exception.Message}");
            }
        }

        private void BuildViews()
        {
            if (_board != null) return;

            GameState state = _session.State;
            _board = BoardView.Create(transform, _data);
            StartCoroutine(Lazpoly.Presentation.Models.MoneyBundle.Preload()); // Read over the first frames, ready before the first payment.
            Markers = SquareMarkers.Create(_board);
            // The dice land on the lawn, below the middle of the screen so they stay clear of the score.
            _dice = DiceView.Create(transform, BoardView.ScreenUp(BoardCameraRig.BaseYaw) * -1.5f + Vector3.up * (BoardView.LawnHeight + 0.02f));
            _dice.Speed = Settings.AnimationSpeed;
            _crowd = new PawnCrowd(state.Players.Count);
            _turnMarker = TurnMarker.Create(transform);
            var portraits = new Dictionary<int, string>();
            foreach (PlayerState player in state.Players) portraits[player.Id] = CharacterOf(state, player).Face;
            Faces = CreateFaces(state, ChoosingPlayer(state), portraits);
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                // Colour and character follow the lobby's seats, not the turn order the draw picked.
                int seat = FirstPlayerDraw.LobbySeat(state.Players, player.Id);
                Color color = PlayerPalette.ForSeat(seat);
                _playerColors[player.Id] = color;
                PawnView pawn = PawnView.Create(transform, _board, seat, color, _crowd, player.Id, CharacterOf(state, player).Id);
                pawn.Speed = Settings.AnimationSpeed;
                pawn.Stepped += _ => AudioPlayer.Play(SoundCue.PawnStep);
                pawn.GateSwung += opening => AudioPlayer.Play(SoundCues.ForJailGate(opening));
                _pawns[player.Id] = pawn;
            }

            // Each player's money at the table's edge facing their corner card, in the turn order the corners use.
            var trayColors = new List<Color>();
            foreach (PlayerState player in state.Players) trayColors.Add(_playerColors[player.Id]);
            _board.CreateMoneyPiles(trayColors);
            SyncViewsToState();
            _drawToAnnounce = IsFirstRoll(state);
            _dealToShow = DealsStartingMoney(state, _resumedGame);
            _logAfterDeal = _dealToShow;
            if (_dealToShow)
            {
                // The cases start empty; each player's money comes up through their hatch before the draw.
                foreach (PlayerState player in state.Players) _shownMoney[player.Id] = 0;
                ShowMoneyPiles();
            }
            else
            {
                if (_drawToAnnounce) AddLog(FirstRollText(state.CurrentPlayer.Name), LogTone.Neutral, state.CurrentPlayer.Id);
                AddLog(F($"Sıra: {state.CurrentPlayer.Name}"), LogTone.Neutral, state.CurrentPlayer.Id, turnStart: true);
            }
            ShowPresence();
            NoteLoadTime(state.Players.Count);
        }

        /// <summary>The game has not begun: the first player, still on Start, waits to roll for the first time.</summary>
        /// <summary>
        /// The character a player plays: the one picked in the lobby, which the game state carries; in a game saved
        /// before Faz 21 none was picked, and the seats take the characters in turn.
        /// </summary>
        public static CharacterOption CharacterOf(GameState state, PlayerState player) =>
            CharacterChoice.Find(player.Figure) ?? CharacterChoice.ForSeat(FirstPlayerDraw.LobbySeat(state.Players, player.Id));

        /// <summary>
        /// The person whose Steam picture this computer knows in a game played on it: the one in the first lobby
        /// seat who is not the computer (the one who set the game up); none online, where the lobby names everyone's
        /// account, and none at the computers' -perf table.
        /// </summary>
        private int ChoosingPlayer(GameState state)
        {
            if (_isOnline || PerfRun.Requested) return GameState.NoOwner;
            int chooser = GameState.NoOwner;
            int firstSeat = int.MaxValue;
            foreach (PlayerState player in state.Players)
            {
                if (_session.IsAiPlaying(player.Id)) continue;
                int seat = FirstPlayerDraw.LobbySeat(state.Players, player.Id);
                if (seat < firstSeat)
                {
                    firstSeat = seat;
                    chooser = player.Id;
                }
            }

            return chooser;
        }

        /// <summary>
        /// The card faces: Steam profile pictures for the real people whose account is known (online every
        /// person's, from the lobby; on this computer the one who set the game up), portraits for the rest.
        /// The -perf table has nobody's face.
        /// </summary>
        private PlayerFaces CreateFaces(GameState state, int chooser, IReadOnlyDictionary<int, string> portraits)
        {
            IPlayerAvatars avatars = null;
            ILobbyService lobby = _lobby;
            if (!PerfRun.Requested)
            {
                GameServices.Registry?.TryGet(out avatars);
                if (lobby == null) GameServices.Registry?.TryGet(out lobby);
            }

            IReadOnlyDictionary<int, ulong> accounts;
            if (avatars == null || lobby == null) accounts = null;
            else if (_isOnline) accounts = SeatAccounts.Online(_host != null ? lobby.LocalId : _hostPeer, _startMembers, state.Players);
            else accounts = SeatAccounts.Local(chooser, lobby.LocalId);
            return new PlayerFaces(avatars, accounts, portraits);
        }

        public static bool IsFirstRoll(GameState state) =>
            state != null && state.TurnNumber == 1 && state.Phase == GamePhase.WaitingForRoll && state.CurrentPlayer.Position == 0;

        public static string FirstRollText(string name) => F($"Kura: ilk zarı {name} atar");

        /// <summary>Whether a table opening on <paramref name="state"/> first hands out the starting money: a new game, not one loaded from a save.</summary>
        public static bool DealsStartingMoney(GameState state, bool resumed) => !resumed && IsFirstRoll(state);

        /// <summary>Each player's money sets off this long after the one before, so the hatches open one after another.</summary>
        public const float DealStagger = 0.3f;

        /// <summary>
        /// The bank hands out the starting money (the owner's wish): one player after another, each one's money comes up
        /// through their hatch into their case, and their card's sum shows it as it lands. The draw follows.
        /// </summary>
        private IEnumerator DealStartingMoney()
        {
            _dealing = true;
            float speed = Speed;
            float last = 0f;
            IReadOnlyList<PlayerState> players = _session.State.Players;
            for (int i = 0; i < players.Count; i++)
            {
                PlayerState player = players[i];
                float seconds = MoneyFlight.Deal(transform, player.Money, _board.MoneyPileOf(i), _board.HatchOf(i), speed);
                StartCoroutine(ShowDealt(player.Id, player.Money, seconds));
                last = seconds;
                if (i < players.Count - 1) yield return new WaitForSeconds(DealStagger / speed);
            }

            yield return new WaitForSeconds(last);
            foreach (PlayerState player in _session.State.Players) _shownMoney[player.Id] = player.Money;
            Revision++;
            _dealing = false;
        }

        private IEnumerator ShowDealt(int playerId, int money, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            _shownMoney[playerId] = money;
            Revision++;
        }

        /// <summary>
        /// Puts the draw over the table once the loading screen has gone, and lets the computer wait while it
        /// is read; the "your turn" line comes after it.
        /// </summary>
        private void AnnounceDraw()
        {
            if (!_drawToAnnounce || SceneFader.IsLoading || _dealing) return;
            if (_dealToShow)
            {
                _dealToShow = false;
                if (_board != null) StartCoroutine(DealStartingMoney());
                return;
            }

            _drawToAnnounce = false;
            PlayerState first = _session.State.CurrentPlayer;
            if (_logAfterDeal)
            {
                AddLog(FirstRollText(first.Name), LogTone.Neutral, first.Id);
                AddLog(F($"Sıra: {first.Name}"), LogTone.Neutral, first.Id, turnStart: true);
            }

            Announcement = FirstRollText(first.Name);
            AnnouncementPlayer = first.Id;
            AnnouncementTime = Time.unscaledTime;
            _nextAiMoveTime = Time.time + AnnouncementSeconds;
            Revision++;
        }

        /// <summary>Records how long the table took from the menu's button, and starts the -perf measurement when asked for.</summary>
        private void NoteLoadTime(int players)
        {
            if (SceneFader.LoadRequestedAt >= 0f) LoadSeconds = Time.realtimeSinceStartup - SceneFader.LoadRequestedAt;
            if (LoadSeconds >= 0f) Debug.Log($"[Lazpoly] Table ready {LoadSeconds * 1000f:0} ms after the menu button.");
            if (Probe == null) return;
            Probe.LoadSeconds = LoadSeconds;
            Probe.Players = players;
            if (PerfRun.Requested && !_isOnline && !Probe.IsRunning)
            {
                Probe.StartRun(PerfRun.WarmUpSeconds, PerfRun.Seconds, sample => StartCoroutine(FinishPerfRun(sample)));
            }
            else if (ScreenTour.Requested && !PerfRun.Requested && !_isOnline && !_touring)
            {
                _touring = true;
                StartCoroutine(TourTable());
            }
        }

        private bool _touring;

        /// <summary>
        /// The table's part of the screen tour (<see cref="ScreenTour"/>): the city card the person stands at, the
        /// building card after buying, a square's card, then, once the computers have played and the roll is the
        /// person's again, the table and each window the game screen opens. Quits when done.
        /// </summary>
        private IEnumerator TourTable()
        {
            GameHud hud = _hud;
            int person = ScreenTour.PersonId;
            yield return ScreenTour.Until(() => hud.CityCardOpen, 20f);
            yield return ScreenTour.Take("sehir-karti");

            var buy = new BuyPropertyCommand(person);
            if (IsAllowed(buy)) Submit(buy);
            yield return ScreenTour.Until(() => hud.BuildCardOpen, 5f);
            if (hud.BuildCardOpen)
            {
                yield return ScreenTour.Take("bina-karti");
                hud.CloseTop();
            }

            hud.PinSquare(_session.State.FindPlayer(person).Position);
            yield return ScreenTour.Take("kare-karti");
            hud.CloseTop();

            for (int i = 0; i < _session.Board.Count; i++)
            {
                if (!_session.Board[i].IsResort || _session.State.GetOwner(i) == GameState.NoOwner) continue;
                hud.PinSquare(i);
                yield return ScreenTour.Take("fabrika-karti");
                hud.CloseTop();
                break;
            }

            yield return ScreenTour.Until(() => !IsBusy && _session.State.CurrentPlayer.Id == person && _session.State.Phase == GamePhase.WaitingForRoll, 120f);
            yield return ScreenTour.Take("masa");
            // The closest view, on the person's figure: how the Meshy models look when the wheel brings them near.
            if (CameraRig != null && _pawns.TryGetValue(person, out PawnView near) && near != null)
            {
                CameraRig.Peek(near.transform.position);
                CameraRig.SetView(BoardCameraRig.BaseYaw, BoardCameraRig.MinZoom);
                yield return ScreenTour.Take("masa-yakin");
                CameraRig.Peek(null);
                CameraRig.ResetView();
                yield return ScreenTour.Until(() => false, 1f);
            }

            // The money hatches from near (2026-11-12): the person's own shut, then standing open, then with a sum going down
            // and a sum coming up written in front of it; and two hatches' sums in the whole view.
            if (CameraRig != null && _board.HatchOf(0) != null)
            {
                MoneyHatch hatch = _board.HatchOf(0);
                CameraRig.SetView(BoardCameraRig.BaseYaw, BoardCameraRig.MinZoom);
                CameraRig.Hold(hatch.Mouth);
                hatch.Show(0f);
                yield return ScreenTour.Take("kapak-kapali");
                hatch.Show(1f);
                yield return ScreenTour.Take("kapak-acik");
                // The hatch a little over the picture's middle, so the roll button there does not stand before the sum.
                CameraRig.Hold(hatch.Mouth - _board.transform.TransformVector(BoardView.ScreenUp(BoardCameraRig.BaseYaw)) * 2.5f);
                HatchAmount.Show(hatch, -50000, 0f, 60f, 1f);
                yield return ScreenTour.Until(() => false, 0.5f);
                yield return ScreenTour.Take("kapak-giden");
                HatchAmount writtenSum = HatchAmount.Show(hatch, 100000, 0f, 60f, 1f);
                yield return ScreenTour.Until(() => false, 0.5f);
                yield return ScreenTour.Take("kapak-gelen");
                CameraRig.Hold(null);
                CameraRig.ResetView();
                yield return ScreenTour.Until(() => false, 1f);
                MoneyHatch otherHatch = _board.HatchOf(_board.MoneyHatches.Count - 1);
                HatchAmount otherSum = HatchAmount.Show(otherHatch, -1250000, 0f, 60f, 1f);
                otherHatch.Show(1f);
                yield return ScreenTour.Until(() => false, 0.5f);
                yield return ScreenTour.Take("kapak-yazilar");
                if (writtenSum != null) writtenSum.Remove();
                if (otherSum != null) otherSum.Remove();
                otherHatch.Show(0f);
                hatch.Show(0f);
            }

            // The jail and the tax square from near (2026-11-12): the staged prisoner behind the bars, a free piece part of
            // the way on its hop to the cell's door, then the tax square's print.
            if (CameraRig != null && _board.JailScene != null)
            {
                int jailSquare = _session.Board.JailIndex;
                CameraRig.SetView(BoardCameraRig.BaseYaw, BoardCameraRig.MinZoom);
                CameraRig.Hold(_board.SquarePosition(jailSquare));
                yield return ScreenTour.Take("hapis");
                PawnView visitor = null;
                foreach (PawnView candidate in _pawns.Values)
                {
                    if (visitor == null && !candidate.Jailed && candidate.gameObject.activeSelf) visitor = candidate;
                }

                if (visitor != null)
                {
                    // Solid and without its ring, as the piece on turn is when it is sent to jail; the gate stands open for it.
                    visitor.SetFaded(false);
                    if (_turnMarker != null) _turnMarker.Follow(null, Color.white);
                    visitor.transform.position = PawnView.JailHopPoint(_board.PawnSpot(jailSquare), _board.CellDoor, _board.CellMiddle, 0.85f);
                    visitor.transform.localScale = Vector3.one * PawnView.JailingSize(0.85f, visitor.CellSize);
                    _board.SetCellGate(PawnView.GateOnTheWayIn(0.85f));
                    yield return ScreenTour.Take("hapis-giris");
                    _board.SetCellGate(0f);
                    visitor.PlaceOn(visitor.Square);
                    MarkTurn(_session.State.CurrentPlayer.Id);
                }

                for (int taxSquare = 0; taxSquare < _session.Board.Count; taxSquare++)
                {
                    if (_board.TaxBill(taxSquare) == null) continue;
                    // The print alone: the pieces step out of the picture, since a tall one before the square would hide it.
                    var hiddenPawns = new List<PawnView>();
                    foreach (PawnView piece in _pawns.Values)
                    {
                        if (!piece.gameObject.activeSelf) continue;
                        piece.gameObject.SetActive(false);
                        hiddenPawns.Add(piece);
                    }

                    CameraRig.Hold(_board.SquarePosition(taxSquare));
                    yield return ScreenTour.Take("vergi");
                    foreach (PawnView piece in hiddenPawns) piece.gameObject.SetActive(true);
                    break;
                }

                CameraRig.Hold(null);
                CameraRig.ResetView();
                yield return ScreenTour.Until(() => false, 1f);
            }

            // The works of a factory city from near (2026-11-12): an owned one posed at each of its four stages in turn,
            // the pieces out of the picture; then as its owner's holdings really have it.
            if (CameraRig != null)
            {
                for (int worksSquare = 0; worksSquare < _session.Board.Count; worksSquare++)
                {
                    int worksOwner = _session.State.GetOwner(worksSquare);
                    if (!_session.Board[worksSquare].IsResort || worksOwner == GameState.NoOwner) continue;
                    var steppedAside = new List<PawnView>();
                    foreach (PawnView figure in _pawns.Values)
                    {
                        if (!figure.gameObject.activeSelf) continue;
                        figure.gameObject.SetActive(false);
                        steppedAside.Add(figure);
                    }

                    CameraRig.SetView(BoardCameraRig.BaseYaw, BoardCameraRig.MinZoom);
                    CameraRig.Hold(_board.SquarePosition(worksSquare));
                    for (int works = 1; works <= Models.ResortParts.Stages; works++)
                    {
                        _board.SetResortStage(worksSquare, works);
                        yield return ScreenTour.Take($"fabrika-{works}");
                    }

                    _board.SetResortStage(worksSquare, GameRules.ResortsOwned(_session.Board, _session.State, worksOwner));
                    foreach (PawnView figure in steppedAside) figure.gameObject.SetActive(true);
                    CameraRig.Hold(null);
                    CameraRig.ResetView();
                    yield return ScreenTour.Until(() => false, 1f);
                    break;
                }
            }

            // The aeroplane that crosses the sky now and then (2026-11-12), held near each side of the whole view in turn;
            // the far islands show in the same pictures.
            SkyBackdrop sky = Camera.main != null ? Camera.main.GetComponentInChildren<SkyBackdrop>() : null;
            if (sky != null)
            {
                sky.HoldPlane(0, 0.15f);
                yield return ScreenTour.Take("ucak-soldan");
                sky.HoldPlane(1, 0.2f);
                yield return ScreenTour.Take("ucak-sagdan");
                sky.HoldPlane(-1, 0f);
            }

            // The bus ride (Faz 33), posed and seen from near: a piece half way into the bus at the stop, the bus on its way
            // down to the road, on the road round the lawn, stopped by a town half the board away with the piece half
            // way out, and the stop without its bus.
            if (CameraRig != null && _board.Bus != null && _pawns.TryGetValue(person, out PawnView rider) && rider != null)
            {
                BusView bus = _board.Bus;
                int town = (_board.StationSquare + _session.Board.Count / 2 - 3) % _session.Board.Count;
                BusRoute way = _board.BusRouteTo(town);
                Vector3 stood = rider.transform.position;
                Vector3 stop = _board.transform.TransformPoint(bus.ParkedSpot);
                CameraRig.SetView(BoardCameraRig.BaseYaw, BoardCameraRig.MinZoom);
                CameraRig.Hold(stop);
                Vector3 ground = _board.SquarePosition(_board.StationSquare);
                ground.y = stood.y;
                rider.RideAt(bus.Roof);
                rider.transform.position = PawnView.BoardingPoint(ground, bus.Roof, 0.8f);
                rider.transform.localScale = Vector3.one * PawnView.BoardingSize(0.8f);
                yield return ScreenTour.Take("otobus-binis");
                foreach ((string frame, float distance) in new[] { ("otobus-iniyor", 1.9f), ("otobus-yolda", way.Length * 0.6f), ("otobus-sehirde", way.Length) })
                {
                    bus.Show(way, Mathf.Min(distance, way.Length));
                    rider.RideAt(bus.Roof);
                    CameraRig.Hold(bus.transform.position);
                    yield return ScreenTour.Take(frame);
                }

                ground = _board.SquarePosition(town);
                ground.y = stood.y;
                rider.transform.position = PawnView.BoardingPoint(ground, bus.Roof, 0.8f);
                rider.transform.localScale = Vector3.one * PawnView.BoardingSize(0.8f);
                yield return ScreenTour.Take("otobus-inis");
                rider.PlaceOn(rider.Square);
                CameraRig.Hold(stop);
                yield return ScreenTour.Take("otobus-duraksiz");
                bus.Park();
                CameraRig.Hold(null);
                CameraRig.ResetView();
                yield return ScreenTour.Until(() => false, 1f);
            }

            // The strategic cards (Faz 30): the person's hand, a card's towns lit on the board, and an attack's card in the middle.
            hud.OpenCards();
            yield return ScreenTour.Until(() => hud.CardsOpen, 3f);
            yield return ScreenTour.Take("kart-eli");
            hud.CloseTop();
            int demolish = GameRules.HeldCard(_session.Board, _session.State.FindPlayer(person), CardKind.Demolish);
            if (demolish >= 0)
            {
                hud.AimCard(demolish);
                List<int> targets = GameRules.CardTargets(_session.Board, _session.State, _session.State.FindPlayer(person), demolish);
                if (targets.Count > 0) hud.PinSquare(targets[0]);
                yield return ScreenTour.Take("kart-hedef");
                hud.CloseTop();
                hud.PinSquare(-1);

                int shielded = -1;
                for (int i = 0; i < _session.Board.Count; i++)
                {
                    if (_session.State.HasShield(i)) shielded = i;
                }

                ShownPlay = new CardPlayedEvent(person, demolish, shielded >= 0 ? shielded : targets.Count > 0 ? targets[0] : -1);
                ShownPlayBlocked = shielded >= 0;
                ShownPlayTime = Time.time;
                Revision++;
                yield return ScreenTour.Until(() => false, 0.4f);
                yield return ScreenTour.Take("kart-oynandi");
                yield return ScreenTour.Until(() => !CenterNotice.Showing(Time.time - ShownPlayTime, CardPlay.Seconds), CardPlay.Seconds + 1f);
            }

            // The other cards in the middle, one of each (the owner's wish, 2026-11-09): a drawn money card and a drawn
            // strategy card, a trade's result and a player who cannot pay.
            int deckSquare = -1;
            for (int i = 0; i < _session.Board.Count && deckSquare < 0; i++)
            {
                if (_session.Board[i].Type == SquareType.Card) deckSquare = i;
            }

            foreach (CardKind kind in new[] { CardKind.Collect, CardKind.ForceSale })
            {
                int drawn = -1;
                for (int i = 0; i < _session.Board.Cards.Count && drawn < 0; i++)
                {
                    if (_session.Board.Cards[i].Kind == kind) drawn = i;
                }

                if (drawn < 0) continue;
                ShownCard = drawn;
                ShownCardPlayer = person;
                ShownCardSquare = deckSquare;
                ShownCardTime = Time.time;
                ShownCardMoney = kind == CardKind.Collect ? _session.Board.Cards[drawn].Amount : 0;
                Revision++;
                yield return ScreenTour.Until(() => false, 0.4f);
                yield return ScreenTour.Take(kind == CardKind.Collect ? "sans-karti" : "sans-strateji-karti");
                ShownCard = -1;
                Revision++;
            }

            // A card just drawn that may be kept, with its two buttons (the owner's wish, 2026-11-12).
            if (demolish >= 0)
            {
                hud.StageCardOffer(demolish);
                yield return ScreenTour.Until(() => false, 0.4f);
                yield return ScreenTour.Take("kart-kullan-sakla");
                hud.StageCardOffer(CardHand.NoCard);
            }

            int rival = GameState.NoOwner;
            foreach (PlayerState player in _session.State.Players)
            {
                if (player.Id != person && !player.IsBankrupt) rival = player.Id;
            }

            if (rival != GameState.NoOwner)
            {
                ShownTrade = new TradeOffer(person, rival, 100000, 0, Array.Empty<int>(), Array.Empty<int>());
                ShownTradeOutcome = TradeOutcome.Accepted;
                ShownTradeTime = Time.time;
                Revision++;
                yield return ScreenTour.Until(() => false, 0.4f);
                yield return ScreenTour.Take("takas-karti");
                ShownTradeTime = float.NegativeInfinity;

                ShownDebt = new DebtIncurredEvent(person, rival, 250000, MoneyReason.Rent);
                ShownDebtTime = Time.time;
                Revision++;
                yield return ScreenTour.Until(() => false, 0.4f);
                yield return ScreenTour.Take("borc-karti");
                ShownDebt = null;
                Revision++;

                // A rival's town to take over (Faz 31): the person's card, then the card the whole table reads.
                int taken = -1;
                for (int i = 0; i < _session.Board.Count && taken < 0; i++)
                {
                    if (_session.State.GetOwner(i) == rival && _session.Board[i].IsDevelopable && !_session.Board[i].IsResort &&
                        _session.State.GetBuildingLevel(i) < SquareDefinition.MaxBuildingLevel && !_session.State.HasShield(i)) taken = i;
                }

                if (taken >= 0)
                {
                    hud.StageTakeover(taken);
                    yield return ScreenTour.Until(() => hud.TakeoverOpen, 3f);
                    yield return ScreenTour.Take("devral-karti");
                    hud.StageTakeover(-1);
                    ShownTakeoverSquare = taken;
                    ShownTakeoverBuyer = person;
                    ShownTakeoverSeller = rival;
                    ShownTakeoverPrice = (int)GameRules.TakeoverPrice(_session.Board, _session.State, taken);
                    ShownTakeoverTime = Time.time;
                    Revision++;
                    yield return ScreenTour.Until(() => false, 0.4f);
                    yield return ScreenTour.Take("devir-karti");
                    ShownTakeoverTime = float.NegativeInfinity;
                    Revision++;
                }
            }

            // The roll's total in the middle of the screen (the owner's wish, Faz 28), staged as a double so ÇİFT shows too.
            ShowRoll(new DiceRolledEvent(person, 3, 3));
            ShownRollLasts = RollTotal.Seconds + ScreenTour.SettleSeconds + 1f; // Up long enough for the picture's settling wait.
            yield return ScreenTour.Until(() => false, RollTotal.PopSeconds + 0.1f);
            yield return ScreenTour.Take("zar-toplami");
            yield return ScreenTour.Until(() => !RollTotal.Showing(Time.unscaledTime - ShownRollTime, ShownRollLasts), ShownRollLasts + 1f);

            // The third double's card (the owner's wish, Faz 29), staged for the person.
            ShownThirdDouble = person;
            ShownThirdDoubleTime = Time.time;
            Revision++;
            yield return ScreenTour.Until(() => false, 0.4f);
            yield return ScreenTour.Take("ucuncu-cift");
            ShownThirdDouble = GameState.NoOwner;
            Revision++;

            // The trade agreement card (the owner's wish, Faz 26), staged on the board's first colour group, as everyone sees it.
            int staged = FirstColourGroup(_session.Board);
            if (staged != 0)
            {
                _groupDealsToShow.Add(new KeyValuePair<int, int>(person, staged));
                StartCoroutine(ShowGroupDeals());
                yield return ScreenTour.Until(() => false, 0.5f);
                yield return ScreenTour.Take("ticaret-anlasmasi");
                yield return ScreenTour.Until(() => !CenterNotice.Showing(Time.time - ShownGroupDealTime, GroupDeal.Seconds), GroupDeal.Seconds + 1f);
                // Then the fireworks over its towns (Faz 27), taken while the first bursts are open.
                float burst = FireworkShow.BurstAt(0, 0) + 0.35f;
                yield return ScreenTour.Until(() => ShownFireworksTime >= ShownGroupDealTime && Time.time - ShownFireworksTime >= burst, burst + 2f);
                yield return ScreenTour.Take("havai-fisek");
                yield return ScreenTour.Until(() => Time.time - ShownFireworksTime >= FireworkShow.Seconds(_session.Board.CountInGroup(staged)), FireworkShow.Seconds(4) + 1f);

                // The cup's card and its fireworks (the owner's wish, Faz 29), staged on that group's first town; the table's
                // own cup is put back after.
                int cupTown = new List<int>(GameRules.GroupSquares(_session.Board, staged))[0];
                StartCoroutine(CelebrateCup(new CupHostedEvent(person, cupTown, 50)));
                yield return ScreenTour.Until(() => false, 0.5f);
                yield return ScreenTour.Take("kupa-karti");
                yield return ScreenTour.Until(() => ShownFireworksTime >= ShownCupTime && Time.time - ShownFireworksTime >= burst, CupNews.Seconds + burst + 2f);
                yield return ScreenTour.Take("kupa-fisek");
                yield return ScreenTour.Until(() => Time.time - ShownFireworksTime >= FireworkShow.Seconds(1) + 0.2f, FireworkShow.Seconds(1) + 1f);
                _board.SetCup(_session.State.CupSquare);
                ShowRents();
            }

            foreach ((HudWindow window, string name) in new[]
            {
                (HudWindow.Trade, "takas"), (HudWindow.Log, "olaylar"),
            })
            {
                hud.Open(window);
                yield return ScreenTour.Take(name);
                hud.CloseTop();
            }

            hud.Open(HudWindow.Pause);
            yield return ScreenTour.Take("duraklat");
            hud.Open(HudWindow.Settings);
            yield return ScreenTour.Take("oyun-ayarlari");
            hud.CloseTop();
            hud.Open(HudWindow.HowToPlay);
            yield return ScreenTour.Take("oyun-nasil-oynanir");
            hud.CloseTop();
            hud.CloseTop();

            Debug.Log($"[Lazpoly] Screen tour done: {ScreenTour.Folder}");
            yield return null;
            Application.Quit();
        }

        /// <summary>Writes the perf report, keeps a picture of the table it measured, and quits.</summary>
        private IEnumerator FinishPerfRun(PerfSample sample)
        {
            string path = PerfProbe.WriteReport(sample);
            Debug.Log($"[Lazpoly] Perf report written to {path}");
            Debug.Log($"[Lazpoly] Perf screenshot: {Screenshots.Capture()}");
            // The screenshot is written over the next frame; quitting at once would lose it.
            yield return null;
            yield return null;
            if (PerfRun.InDebt && _hud != null && _hud.DebtSale != null && _hud.DebtSale.Shown)
            {
                // The sale screen has no place in the tour, so its pictures are kept here: as it opens, and with the
                // towns "Otomatik Seç" ticks.
                yield return ScreenTour.Take("satis-ekrani");
                _hud.DebtSale.AutoPick();
                yield return ScreenTour.Take("satis-otomatik-sec");
            }

            Application.Quit();
        }

        /// <summary>The game time played as shown: the state's, carried on smoothly between the clock's reports.</summary>
        private double _shownPlayed;

        private GameClockSign _clockSign;

        /// <summary>Seconds of game time left at a table with a time limit, as the lawn's clock shows them; infinite without one.</summary>
        public double GameSecondsLeft =>
            _session?.Rules == null || _session.Rules.TimeLimitMinutes <= 0 ? double.PositiveInfinity : Math.Max(0, _session.Rules.TimeLimitSeconds - _shownPlayed);

        /// <summary>
        /// While the table opens: the loading screen, the starting money coming out of the hatches and the draw's banner.
        /// The game clock starts once they are over.
        /// </summary>
        public bool Opening => _drawToAnnounce || _dealing || SceneFader.IsLoading || Time.unscaledTime - AnnouncementTime < AnnouncementSeconds;

        /// <summary>Whether game time passes: not while paused, not once the game is over, not while the table opens.</summary>
        public static bool GameClockRuns(bool paused, bool over, bool opening) => !paused && !over && !opening;

        /// <summary>
        /// Whether <paramref name="rolled"/> is a third double in a row that sends its player straight to jail: the rules
        /// follow a double with a jump to the cell, where a plain double walks.
        /// </summary>
        public static bool SendsToJail(DiceRolledEvent rolled, GameEvent next, int jailIndex) =>
            rolled != null && rolled.Die1 == rolled.Die2 && jailIndex >= 0 && next is PlayerMovedEvent moved &&
            moved.PlayerId == rolled.PlayerId && moved.Kind == MoveKind.Jump && moved.ToSquare == jailIndex;

        /// <summary>True for a batch that only moves the game clock on.</summary>
        public static bool OnlyClock(IReadOnlyList<GameEvent> events)
        {
            if (events == null || events.Count == 0) return false;
            foreach (GameEvent gameEvent in events)
            {
                if (!(gameEvent is GameClockEvent)) return false;
            }

            return true;
        }

        /// <summary>
        /// The game clock (the owner's rule, Faz 28): play time goes to the session while the game runs (not while a local
        /// game is paused or after it ended), and the lawn's board counts down from the table's limit. Between the clock's
        /// reports the shown time runs on by itself, never more than a report ahead of the state.
        /// </summary>
        private void RunGameClock()
        {
            GameState state = _session.State;
            if (state == null || _board == null) return;
            bool running = GameClockRuns(Paused, EndReason.HasValue || state.Phase == GamePhase.GameOver, Opening);
            if (running) _session.AddPlayTime(Time.unscaledDeltaTime);
            double carried = running ? _shownPlayed + Time.unscaledDeltaTime : _shownPlayed;
            _shownPlayed = Math.Min(Math.Max(carried, state.PlayedSeconds), state.PlayedSeconds + GameTimer.ReportSeconds + 1);
            if (state.Phase == GamePhase.GameOver && _session.Rules.TimeLimitMinutes > 0 && GameRules.TimeIsUp(_session.Rules, state))
                _shownPlayed = Math.Max(_shownPlayed, _session.Rules.TimeLimitSeconds);

            if (_session.Rules.TimeLimitMinutes <= 0) return;
            if (_clockSign == null) _clockSign = GameClockSign.Create(_board.transform, _board.Inner);
            _clockSign.Place(_board.ClockSpot(_board.Yaw), _board.Yaw);
            _clockSign.Show(GameSecondsLeft);
        }

        /// <summary>Puts every pawn and owner marker where the state says, without animation.</summary>
        private void SyncViewsToState()
        {
            _shownPlayed = _session.State.PlayedSeconds;
            _bankruptPending.Clear();
            GameState state = _session.State;
            // A ride cut short: the bus is back at its stop. The jail's gate is shut.
            if (_board.Bus != null) _board.Bus.Park();
            _board.SetCellGate(0f);
            foreach (PlayerState player in state.Players)
            {
                _shownMoney[player.Id] = player.Money;
                PawnView pawn = _pawns[player.Id];
                if (player.InJail) pawn.PlaceInJail(player.Position);
                else pawn.PlaceOn(player.Position);
                if (player.IsBankrupt) pawn.SetEliminated();
            }

            MarkTurn(state.CurrentPlayer.Id);
            _shownPot = state.Pot;
            _board.SetPot(state.Pot);
            for (int i = 0; i < state.SquareCount; i++)
            {
                int owner = state.GetOwner(i);
                _shownOwners[i] = owner;
                _board.SetOwner(i, owner == GameState.NoOwner ? (Color?)null : ColorOf(owner));
                _board.SetBuildingLevel(i, state.GetBuildingLevel(i));
            }

            _board.SetCup(state.CupSquare);
            for (int i = 0; i < state.SquareCount; i++)
            {
                _board.SetShield(i, state.HasShield(i));
                _board.SetStrike(i, state.IsStruck(i));
            }

            _heldRentGroups.Clear(); // A table set straight from the state shows the rents as they are.
            ShowRents(animate: false);
        }

        /// <summary>
        /// Prints on every owned town the rent a visitor pays now (it changes with buildings and with a whole
        /// group, so every town is asked again); towns for sale show their price. Each owned resort's beach
        /// shows the stage of the resorts its owner holds, which is what its rent counts too. The towns of a group whose
        /// trade agreement is still being celebrated keep the rent from before it until the fireworks are over (Faz 27).
        /// </summary>
        private void ShowRents(bool animate = true)
        {
            GameState state = _session.State;
            for (int i = 0; i < state.SquareCount; i++)
            {
                int owner = state.GetOwner(i);
                SquareDefinition square = _session.Board[i];
                bool owned = square.Type == SquareType.Property && owner != GameState.NoOwner;
                bool held = owned && !square.IsResort && _heldRentGroups.Contains(square.GroupId);
                int rent = held ? GroupDeal.RentBefore(_session.Board, state, i) : GameRules.CalculateRent(_session.Board, _session.Rules, state, i);
                _board.SetRent(i, owned ? rent : (int?)null);
                if (owned && _session.Board[i].IsResort) _board.SetResortStage(i, GameRules.ResortsOwned(_session.Board, state, owner), animate);
            }
        }

        private void OnEventsPublished(IReadOnlyList<GameEvent> events)
        {
            if (OnlyClock(events))
            {
                // The game clock moved on: kept in the save, but no move to play and no decision clock to restart.
                SaveLocalGame();
                return;
            }

            _lastProgressTime = Time.unscaledTime;
            Revision++;
            _trail.Events(events);
            SaveLocalGame();
            if (_client != null && _client.State.Phase == GamePhase.GameOver) LastRoomStore.Forget();
            UnlockAchievements(events);
            ShowPresence();
            if (_board == null) return;
            foreach (GameEvent gameEvent in PlaybackOrder.For(events))
            {
                if (gameEvent is PlayerBankruptedEvent fallen) _bankruptPending.Add(fallen.PlayerId);
                _pendingEvents.Enqueue(gameEvent);
            }

            if (!_playing) _playback = StartCoroutine(PlayEvents());
        }

        private void UnlockAchievements(IReadOnlyList<GameEvent> events)
        {
            if (_achievements == null || _session.State == null) return;
            foreach (string id in _tracker.Observe(events, _session.State))
            {
                _achievements.Unlock(id);
                AudioPlayer.Play(SoundCue.Achievement);
                AddLog(F($"Başarım: {L(TitleOf(id))}"));
            }
        }

        private static string TitleOf(string achievementId)
        {
            foreach (AchievementDefinition definition in AchievementCatalog.All)
            {
                if (definition.Id == achievementId) return definition.Title;
            }

            return achievementId;
        }

        /// <summary>Tells friends what this player is doing; the service ignores a line it already shows.</summary>
        private void ShowPresence()
        {
            GameState state = _session?.State;
            if (_presence == null || state == null) return;

            if (state.Phase == GamePhase.GameOver)
            {
                _presence.Show(PresenceText.GameOver(_session.CanControl(state.WinnerId)));
                return;
            }

            bool againstComputers = false;
            foreach (PlayerState player in state.Players) againstComputers |= _session.IsAiPlaying(player.Id);
            _presence.Show(PresenceText.InGame(_isOnline, againstComputers, state.TurnNumber, state.ActivePlayerCount()));
        }

        private void OnSettingsChanged(GameSettings settings)
        {
            foreach (PawnView pawn in _pawns.Values) pawn.Speed = settings.AnimationSpeed;
            if (_dice != null) _dice.Speed = settings.AnimationSpeed;
            RoomDressing.ApplyQuality(settings.Quality);
            Lang.Apply(settings.Language);
            UiRoot.ApplyScale(settings.UiScaleFactor);
            if (Lang.Version != _langVersion)
            {
                // The square names on the board are meshes, not views: they are rewritten by hand when the language changes.
                _langVersion = Lang.Version;
                _board?.Relabel();
            }
        }

        private void OnCommandRejected(RejectionReason reason)
        {
            LastRejection = F($"İşlem reddedildi: {reason}");
            _trail.Add($"REJECTED {reason}");
            Revision++;
        }

        private void OnStateReset()
        {
            _lastProgressTime = Time.unscaledTime;
            if (_board == null)
            {
                BuildViews();
                return;
            }

            if (_playing && _playback != null) StopCoroutine(_playback);
            _playback = null;
            _playing = false;
            _pendingEvents.Clear();
            _flownAlong.Clear();
            _paidGains.Clear();
            Markers?.HideTarget();
            SyncViewsToState();
            AddLog(L("Oyun durumu ev sahibinden yeniden alındı."));
        }

        private void OnEnded(SessionEndReason reason)
        {
            EndReason = reason;
            LastRoomStore.Forget();
            AddLog(EndReasonText(reason));
            Revision++;
        }

        private void OnRemoteConnectionChanged(int playerId, bool connected)
        {
            string name = _session.State.FindPlayer(playerId)?.Name ?? $"Oyuncu {playerId}";
            AddLog(connected ? F($"{name} bağlandı.") : F($"{name} bağlantısı koptu."));
        }

        public static string EndReasonText(SessionEndReason reason)
        {
            switch (reason)
            {
                case SessionEndReason.HostLeft: return L("Ev sahibi oyundan ayrıldı; çevrim içi oyun burada bitti. Son görüntüden bilgisayarlara karşı sürdürebilir ya da ana menüye dönebilirsin.");
                case SessionEndReason.NotInThisGame: return L("Bu oyunda yerin yok: oyun sen gelmeden başlamış. Ana menüye dönüp ev sahibinden yeni bir oda kurmasını iste.");
                case SessionEndReason.VersionMismatch: return L("Oyun sürümün ev sahibininkiyle aynı değil. İkiniz de Steam'den son güncellemeyi alıp yeniden deneyin.");
                case SessionEndReason.BoardMismatch: return L("Tahta verin ev sahibininkiyle aynı değil. Oyunu Steam'de doğrulayıp (yerel dosyaları doğrula) yeniden dene.");
                default: return L("Oyun sona erdi. Ana menüye dönebilirsin.");
            }
        }

        private IEnumerator PlayEvents()
        {
            _playing = true;
            while (_pendingEvents.Count > 0)
            {
                while (Paused) yield return null;
                GameEvent gameEvent = _pendingEvents.Dequeue();
                string line = EventDescriber.Describe(gameEvent, _session.State, _session.Board);
                if (line != null) AddLog(line, EventDescriber.ToneOf(gameEvent), EventDescriber.ActorOf(gameEvent), gameEvent is TurnStartedEvent);

                AudioPlayer.Play(SoundCues.For(gameEvent));
                switch (gameEvent)
                {
                    case TurnStartedEvent turn:
                        yield return ShowTurnStart(turn.PlayerId);
                        break;
                    case DiceRolledEvent rolled:
                        AudioPlayer.Play(SoundCue.DiceShake);
                        yield return _dice.Roll(rolled.Die1, rolled.Die2, CameraRig != null ? CameraRig.Yaw : 0f);
                        AudioPlayer.Play(SoundCue.DiceLand);
                        ShowRoll(rolled);
                        // Where the roll takes the pawn shows the moment the dice settle, before it starts walking.
                        if (_pendingEvents.Count > 0 && _pendingEvents.Peek() is PlayerMovedEvent upcoming && upcoming.PlayerId == rolled.PlayerId)
                            Markers.ShowTarget(upcoming.ToSquare, ColorOf(rolled.PlayerId));
                        if (rolled.Die1 == rolled.Die2) Say(rolled.PlayerId, L("Çift!"));
                        // The total stays up in the middle of the screen, then the pawn walks (the owner's wish, Faz 28).
                        yield return new WaitForSeconds(ShownRollLasts);
                        if (ShownRollJailed)
                        {
                            // A third double: the card first, then the flight to the cell once it has gone (the owner's wish, Faz 29).
                            ShownThirdDouble = rolled.PlayerId;
                            ShownThirdDoubleTime = Time.time;
                            Revision++;
                            yield return new WaitForSeconds(CenterNotice.Seconds);
                        }
                        break;
                    case PlayerMovedEvent moved:
                        _lastMover = moved.PlayerId;
                        _shownSquares[moved.PlayerId] = moved.ToSquare;
                        PawnView pawn = _pawns[moved.PlayerId];
                        Markers.ShowTarget(moved.ToSquare, ColorOf(moved.PlayerId));
                        if (moved.Kind == MoveKind.Jump) yield return pawn.JumpTo(moved.ToSquare);
                        else if (moved.Kind == MoveKind.Ride && BusTrip.CanCarry(_board, pawn)) yield return BusTrip.Carry(_board, pawn, moved.ToSquare);
                        else yield return pawn.WalkTo(moved.ToSquare, backward: moved.Kind == MoveKind.Back);
                        Markers.HideTarget();
                        if (moved.PassedStart) _board.PulseTile(0);
                        break;
                    case PotChangedEvent pot:
                        _shownPot += pot.Delta;
                        _board.SetPot(_shownPot);
                        _board.NudgePot(pot.Delta > 0);
                        Revision++;
                        break;
                    case CupHostedEvent cup:
                        Pawn(cup.PlayerId)?.Cheer();
                        Say(cup.PlayerId, F($"Kupa {L(_session.Board[cup.SquareIndex].Name)} şehrine!"));
                        yield return CelebrateCup(cup);
                        break;
                    case JailChangedEvent jail:
                        Say(jail.PlayerId, L(jail.Turns == 0 ? "Özgürüm!" : jail.Turns >= _session.Rules.JailTurns ? "Hapse düştüm!" : "Bekliyorum..."));
                        Revision++;
                        // The owner's wish (2026-11-12): whoever is sent to jail shrinks and goes into the jail, and comes out of it free.
                        if (_pawns.TryGetValue(jail.PlayerId, out PawnView prisoner))
                        {
                            yield return jail.Turns > 0 ? prisoner.EnterJail() : prisoner.LeaveJail();
                        }

                        break;
                    case CardPlayedEvent played:
                        // The card in the middle for everyone, what it is played on under its name (Faz 30); an attack that
                        // meets a shield says so on the same card.
                        ShownPlay = played;
                        ShownPlayBlocked = CardPlay.Blocked(_pendingEvents.Count > 0 ? _pendingEvents.Peek() : null);
                        ShownPlayMoney = ShownPlayBlocked ? 0 : CardMoney(played.PlayerId, _pendingEvents, cardOnly: false);
                        ShownPlayTime = Time.time;
                        // A card used as it was drawn takes the drawn card's place in the middle.
                        ShownCard = -1;
                        Say(played.PlayerId, CardPlay.Name(_session.Board, played.CardIndex) + "!");
                        Revision++;
                        yield return new WaitForSeconds(CardPlay.Seconds);
                        break;
                    case ShieldChangedEvent shield:
                        _board.SetShield(shield.SquareIndex, shield.Raised, animate: true);
                        if (shield.Blocked) Say(_session.State.GetOwner(shield.SquareIndex), L("Kalkan korudu!"));
                        Revision++;
                        break;
                    case StrikeChangedEvent strike:
                        _board.SetStrike(strike.SquareIndex, strike.StrikerId != GameState.NoOwner, animate: true);
                        ShowRents();
                        break;
                    case FundChangedEvent _:
                    case CardDiscardedEvent _:
                        Revision++; // The hands and funds on the corner cards.
                        break;
                    case CardDrawnEvent card:
                        ShownCard = card.CardIndex;
                        ShownCardPlayer = card.PlayerId;
                        ShownCardSquare = _shownSquares.TryGetValue(card.PlayerId, out int drawnOn) ? drawnOn : _session.State.FindPlayer(card.PlayerId)?.Position ?? -1;
                        ShownCardTime = Time.time;
                        ShownCardMoney = card.Kept ? 0 : CardMoney(card.PlayerId, _pendingEvents, cardOnly: true);
                        Revision++;
                        // The card stays up its whole time and only then its effect plays: the money moves once the card
                        // has closed (the owner's wish, 2026-11-12). A card a person here decides on comes with its buttons
                        // instead, and stays until they answer.
                        yield return new WaitForSeconds(card.Kept && PersonHere(card.PlayerId) ? CardOfferSeconds : CardSeconds);
                        break;
                    case MoneyChangedEvent money:
                        // A takeover's card comes first, for the whole table; then the price flies to the owner and the town
                        // changes colour (Faz 31, as with a trade).
                        if (money.Reason == MoneyReason.Takeover && money.Delta < 0)
                        {
                            PropertyOwnerChangedEvent taken = null;
                            foreach (GameEvent next in _pendingEvents)
                            {
                                taken = next as PropertyOwnerChangedEvent;
                                if (taken != null) break;
                            }

                            if (taken != null) yield return ShowTakeover(taken, -money.Delta);
                        }

                        // Money from the bank (passing Start, a card, the pot) comes up through the player's own hatch into
                        // their safe (the owner's wish, 2026-11-12) and counts once it has landed.
                        bool income = money.Delta > 0 && !_paidGains.Remove(money) && !_flownAlong.Remove(money) && ComesUpTheHatch(money);
                        if (income)
                        {
                            ShowMoney(money);
                            React(money.PlayerId, money.Delta);
                            yield return ShowIncome(money);
                        }

                        // The payer's money drops as the notes leave; a player paid by them gains once the flight has landed
                        // (their event comes right after the payment's).
                        _shownMoney[money.PlayerId] = ShownMoneyOf(money.PlayerId) + money.Delta;
                        Revision++;
                        if (!income)
                        {
                            ShowMoney(money);
                            React(money.PlayerId, money.Delta);
                        }

                        if (money.Delta < 0) yield return ShowPayment(money);
                        break;
                    case PropertyOwnerChangedEvent owner:
                        if (GroupDeal.Completes(_session.Board, _shownOwners, owner.SquareIndex, owner.NewOwnerId))
                        {
                            _groupDealsToShow.Add(new KeyValuePair<int, int>(owner.NewOwnerId, _session.Board[owner.SquareIndex].GroupId));
                            _heldRentGroups.Add(_session.Board[owner.SquareIndex].GroupId); // The new rents show after the fireworks.
                        }

                        _shownOwners[owner.SquareIndex] = owner.NewOwnerId;
                        // A town that changes hands loses its shield and its strike (Faz 30).
                        _board.SetShield(owner.SquareIndex, false, animate: true);
                        _board.SetStrike(owner.SquareIndex, false, animate: true);
                        if (owner.NewOwnerId == GameState.NoOwner)
                        {
                            _board.ClearOwner(owner.SquareIndex, animate: true);
                            ShowRents();
                            break;
                        }

                        // Bought with buildings on the same card: the picked level goes up at once and the plot never shows
                        // on its own (the owner's wish). The buildings go up first, so the marker stays hidden when the owner is set.
                        int builtWithPurchase = BuiltWithPurchase(owner, _pendingEvents);
                        if (builtWithPurchase > 0) _board.SetBuildingLevel(owner.SquareIndex, builtWithPurchase, animate: true);
                        _board.SetOwner(owner.SquareIndex, ColorOf(owner.NewOwnerId));
                        ShowRents();
                        if (builtWithPurchase == 0) _board.RaiseFlag(owner.SquareIndex);
                        Pawn(owner.NewOwnerId)?.Cheer();
                        // Bought from the bank on landing; a trade has its own lines.
                        if (owner.PreviousOwnerId == GameState.NoOwner && _shownPhase == GamePhase.AwaitingBuyDecision)
                        {
                            _boughtOnLanding = true;
                            Say(owner.NewOwnerId, L("Aldım!"));
                        }
                        break;
                    case DebtIncurredEvent debt:
                        // Everyone reads "X kirayı ödeyecek parası yok" first; only once the card is gone does the player go
                        // bankrupt or the sale screen open (the owner's wish, Faz 23).
                        ShownDebt = debt;
                        ShownDebtTime = Time.time;
                        Revision++;
                        yield return new WaitForSeconds(CenterNotice.Seconds);
                        break;
                    case PlayerBankruptedEvent bankrupt:
                        // The corner card turns to İFLAS as the figure falls.
                        _bankruptPending.Remove(bankrupt.PlayerId);
                        Revision++;
                        Say(bankrupt.PlayerId, L("Battım..."));
                        yield return _pawns[bankrupt.PlayerId].Topple();
                        break;
                    case BuildingLevelChangedEvent building:
                        if (building.Level > _board.LevelOf(building.SquareIndex))
                        {
                            Say(_session.State.GetOwner(building.SquareIndex), L(building.Level >= SquareDefinition.MaxBuildingLevel ? "Otel vakti!" : "Bir ev daha!"));
                        }

                        if (building.Level > _board.LevelOf(building.SquareIndex)) Pawn(_session.State.GetOwner(building.SquareIndex))?.Cheer();
                        _board.SetBuildingLevel(building.SquareIndex, building.Level, animate: true);
                        ShowRents();
                        if (building.Level >= SquareDefinition.MaxBuildingLevel) AudioPlayer.Play(SoundCue.Hotel);
                        break;
                    case PhaseChangedEvent phase:
                        // Leaving the buy decision without a purchase: the player let the square go, it stays with the bank.
                        if (_shownPhase == GamePhase.AwaitingBuyDecision && !_boughtOnLanding) Say(_lastMover, L("Almıyorum."));
                        if (phase.Phase == GamePhase.AwaitingBuyDecision) _boughtOnLanding = false;
                        _shownPhase = phase.Phase;
                        break;
                    case TradeProposedEvent proposed:
                        _shownOffer = proposed.Offer;
                        // Both sides say they are trading, so a trade between others reads on the table (the owner's wish, Faz 23).
                        Say(proposed.Offer.FromPlayerId, L("Takas yapıyoruz..."));
                        Say(proposed.Offer.ToPlayerId, L("Takas yapıyoruz..."));
                        break;
                    case TradeResolvedEvent resolved:
                        if (_shownOffer != null && resolved.Outcome != TradeOutcome.Cancelled)
                        {
                            Say(_shownOffer.ToPlayerId, L(resolved.Outcome == TradeOutcome.Accepted ? "Anlaştık!" : "Olmaz."));
                        }

                        if (_shownOffer != null)
                        {
                            // The result card in the middle; the table waits a moment so it can be read.
                            ShownTrade = _shownOffer;
                            ShownTradeOutcome = resolved.Outcome;
                            ShownTradeTime = Time.time;
                            Revision++;
                            _shownOffer = null;
                            yield return new WaitForSeconds(CenterNotice.Seconds);
                        }

                        _shownOffer = null;
                        break;
                    case GameEndedEvent _:
                        Pawn(_session.State.WinnerId)?.Cheer();
                        Confetti.Burst(transform, _board.transform.position + Vector3.up * 0.5f, _board.SurfaceY + 0.01f,
                            new List<Color>(_playerColors.Values));
                        break;
                }

                if (_groupDealsToShow.Count > 0 && !GroupDealWaits(_pendingEvents.Count > 0 ? _pendingEvents.Peek() : null))
                {
                    yield return ShowGroupDeals();
                }
            }

            if (_groupDealsToShow.Count > 0) yield return ShowGroupDeals();

            // Everything is shown now, so the money on screen is the state's money.
            foreach (PlayerState player in _session.State.Players) _shownMoney[player.Id] = player.Money;
            _shownPot = _session.State.Pot;
            _board.SetPot(_shownPot);
            Revision++;
            _playing = false;
        }

        private float Speed => Mathf.Max(Settings.AnimationSpeed, 0.1f);

        /// <summary>
        /// Whether a completed group's card waits for <paramref name="next"/>: the rest of a trade's towns and its result card,
        /// and the buildings bought with the town, are shown first.
        /// </summary>
        /// <summary>The group of the board's first town (not a holiday resort), or 0 when there is none.</summary>
        public static int FirstColourGroup(BoardDefinition board)
        {
            for (int i = 0; i < board.Count; i++)
            {
                if (board[i].Type == SquareType.Property && !board[i].IsResort && board[i].GroupId != 0) return board[i].GroupId;
            }

            return 0;
        }

        public static bool GroupDealWaits(GameEvent next) =>
            next is PropertyOwnerChangedEvent || next is TradeResolvedEvent || next is BuildingLevelChangedEvent;

        /// <summary>
        /// The trade agreement card for each group just completed, one after another; the table waits <see cref="GroupDeal.Seconds"/>
        /// for each (the owner's wish, Faz 26). When the card is gone, fireworks go up over the group's towns and, once they
        /// are over, the towns' rents on the board change to the new ones (Faz 27).
        /// </summary>
        private IEnumerator ShowGroupDeals()
        {
            while (_groupDealsToShow.Count > 0)
            {
                KeyValuePair<int, int> deal = _groupDealsToShow[0];
                _groupDealsToShow.RemoveAt(0);
                string name = _session.State.FindPlayer(deal.Key)?.Name ?? $"Oyuncu {deal.Key}";
                ShownGroupDeal = GroupDeal.Text(name, GroupDeal.TownNames(_session.Board, deal.Value));
                ShownGroupDealPlayer = deal.Key;
                ShownGroupDealTime = Time.time;
                AddLog(ShownGroupDeal, LogTone.Gain, deal.Key);
                AudioPlayer.Play(SoundCue.Trade); // The handshake of an agreement.
                Pawn(deal.Key)?.Cheer();
                Revision++;
                yield return new WaitForSeconds(GroupDeal.Seconds);
                yield return CelebrateGroup(deal.Value);
            }
        }

        /// <summary>
        /// The cup goes to a town (the owner's wish, Faz 29): first the card "Şampiyonlar Kupası maçları X şehrinde yapılıyor"
        /// (<see cref="CupNews.Seconds"/>), then the trophy moves there and fireworks go up over it, and only once they are over
        /// do the rents on the board change (the town's, and the one the cup left). The table waits all along.
        /// </summary>
        private IEnumerator CelebrateCup(CupHostedEvent cup)
        {
            ShownCup = cup;
            ShownCupTime = Time.time;
            Revision++;
            yield return new WaitForSeconds(CupNews.Seconds);

            _board.SetCup(cup.SquareIndex, animate: true);
            ShownFireworksTime = Time.time;
            Revision++;
            Fireworks.Launch(transform, new List<Vector3> { _board.SquarePosition(cup.SquareIndex) },
                _board.transform.position + Vector3.up * _board.SurfaceY, CupNews.FireworkColor);
            yield return new WaitForSeconds(FireworkShow.Seconds(1));

            ShowRents();
            _board.PopPrice(cup.SquareIndex);
            Revision++;
        }

        /// <summary>Fireworks over the towns of <paramref name="groupId"/>, in the group's colour; then their new rents show, each with a swell.</summary>
        private IEnumerator CelebrateGroup(int groupId)
        {
            var towns = new List<int>(GameRules.GroupSquares(_session.Board, groupId));
            var tiles = new List<Vector3>(towns.Count);
            foreach (int town in towns) tiles.Add(_board.SquarePosition(town));
            ShownFireworksTime = Time.time;
            Revision++;
            Fireworks.Launch(transform, tiles, _board.transform.position + Vector3.up * _board.SurfaceY, _data.GroupColor(groupId));
            yield return new WaitForSeconds(FireworkShow.Seconds(towns.Count));

            _heldRentGroups.Remove(groupId);
            ShowRents();
            foreach (int town in towns) _board.PopPrice(town);
            Revision++;
        }

        /// <summary>
        /// A computer player's line in a bubble over its pawn, for the choices it makes (buying, letting a
        /// square go, bidding, building, trading). People at the table say nothing: they know what they did.
        /// The line waits in <see cref="BubbleQueue"/>: one bubble on the table at a time, so lines said
        /// together (an auction where everyone passes) come one after the other instead of over each other.
        /// </summary>
        private void Say(int playerId, string line)
        {
            if (playerId == GameState.NoOwner || !_session.IsAiPlaying(playerId)) return;
            _bubbleQueue.Add(playerId, line);
            ShowNextBubble();
        }

        /// <summary>
        /// Puts the next waiting line up once the table is free. When the bubble on show has been read and a line
        /// waits, the bubble is let go first and the next one pops up only after it has gone, so two lines never
        /// show over each other while one fades.
        /// </summary>
        private void ShowNextBubble()
        {
            while (_bubbleQueue.ShouldAdvance(_bubble != null, Time.time - _bubbleShownAt))
            {
                if (_bubble != null)
                {
                    _bubble.Dismiss();
                    return;
                }

                (int playerId, string text) = _bubbleQueue.Take();
                if (!_pawns.TryGetValue(playerId, out PawnView pawn) || !pawn.gameObject.activeInHierarchy) continue;
                _bubble = SpeechBubble.Say(transform, pawn.transform, text, ColorOf(playerId));
                _bubbleShownAt = Time.time;
                return;
            }
        }

        /// <summary>
        /// A new turn: the pawn hops once; a soft note when the turn goes to someone not at this screen
        /// (the HUD plays the "your turn" chord for people here). The camera stays where the player put it.
        /// </summary>
        private IEnumerator ShowTurnStart(int playerId)
        {
            MarkTurn(playerId);
            if (!_pawns.TryGetValue(playerId, out PawnView pawn) || !pawn.gameObject.activeInHierarchy) yield break;
            if (!_session.CanControl(playerId) || _session.IsAiPlaying(playerId)) AudioPlayer.Play(SoundCue.TurnPass);
            yield return pawn.Bob();
        }

        /// <summary>Moves the ring under the pawn whose turn it is; that piece stands solid and the waiting players' pieces fade.</summary>
        private void MarkTurn(int playerId)
        {
            foreach (KeyValuePair<int, PawnView> pair in _pawns) pair.Value.SetFaded(pair.Key != playerId);
            if (_turnMarker == null) return;
            _turnMarker.Follow(_pawns.TryGetValue(playerId, out PawnView pawn) ? pawn : null, ColorOf(playerId));
        }

        private void ShowRoll(DiceRolledEvent rolled)
        {
            ShownDie1 = rolled.Die1;
            ShownDie2 = rolled.Die2;
            ShownRollTime = Time.unscaledTime;
            ShownRollPlayer = rolled.PlayerId;
            ShownRollLasts = RollTotal.Seconds / Speed;
            // The engine ends the turn straight after a third double: the move is forfeited, or the player flies to jail.
            GameEvent after = _pendingEvents.Count > 0 ? _pendingEvents.Peek() : null;
            ShownRollForfeited = rolled.Die1 == rolled.Die2 && after is PhaseChangedEvent next && next.Phase == GamePhase.TurnEnd;
            ShownRollJailed = SendsToJail(rolled, after, _session.Board.JailIndex);
            Revision++;
        }

        /// <summary>
        /// Money seen moving (the owner's wish, Faz 25): the money leaves the payer's case at its size and turned its way, goes
        /// down through the payer's hatch at their corner, and for another player comes up through theirs and into their case;
        /// money for the bank ends under the table. The game waits for it
        /// (<see cref="MoneyFlight.Seconds"/>). Houses bought together fly as one payment.
        /// </summary>
        private IEnumerator ShowPayment(MoneyChangedEvent money)
        {
            if (_flownAlong.Remove(money)) yield break;
            int payer = _session.State.IndexOfPlayer(money.PlayerId);
            if (payer < 0 || _board == null) yield break;

            int amount = -money.Delta;
            int receiver = _session.State.IndexOfPlayer(PaymentReceiver(money, _pendingEvents));
            bool toBank = receiver < 0;
            if (toBank)
            {
                foreach (MoneyChangedEvent along in PaidAlong(money, _pendingEvents))
                {
                    _flownAlong.Add(along);
                    amount -= along.Delta;
                }
            }

            // The receiver's gain comes right after: this flight brings it, so it is not handed out by the bank as well.
            if (!toBank && _pendingEvents.Count > 0 && _pendingEvents.Peek() is MoneyChangedEvent paidGain) _paidGains.Add(paidGain);

            // Out of the payer's case, down their hatch; up the other player's hatch into their case, or to the bank.
            float seconds = MoneyFlight.Fly(transform, amount, _board.MoneyPileOf(payer), _board.HatchOf(payer),
                toBank ? null : _board.MoneyPileOf(receiver), toBank ? null : _board.HatchOf(receiver), Speed);
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>
        /// Money from the bank seen arriving (the owner's wish, 2026-11-12: "money from Şans cards and from passing Start
        /// too"): the player's hatch opens, the money comes up and goes into their safe, as the starting money does, with
        /// the sum written at the hatch. Gains that come together (the Start bonus and a fund's payout) make one flight.
        /// The game waits for it.
        /// </summary>
        private IEnumerator ShowIncome(MoneyChangedEvent money)
        {
            int seat = _session.State.IndexOfPlayer(money.PlayerId);
            if (seat < 0 || _board == null) yield break;
            int amount = money.Delta;
            foreach (MoneyChangedEvent along in GainedAlong(money, _pendingEvents))
            {
                _flownAlong.Add(along);
                amount += along.Delta;
            }

            float seconds = MoneyFlight.Deal(transform, amount, _board.MoneyPileOf(seat), _board.HatchOf(seat), Speed);
            if (seconds > 0f) yield return new WaitForSeconds(seconds);
        }

        /// <summary>
        /// Whether a gain is seen coming up through the player's hatch when the bank pays it: every gain but a sale's
        /// (buildings and towns sold back come in runs on the sell screen, and their money shows at once as before). A
        /// gain another player paid is brought by that payment's own flight instead (<see cref="PaymentReceiver"/>).
        /// </summary>
        public static bool ComesUpTheHatch(MoneyChangedEvent gain) =>
            gain != null && gain.Delta > 0 && gain.Reason != MoneyReason.BuildingSale && gain.Reason != MoneyReason.PropertySale;

        /// <summary>
        /// The bank's gains still waiting that come up with <paramref name="gain"/> in one flight: the same player's gains
        /// right after it (a fund's payout after the Start bonus), up to the first other event.
        /// </summary>
        public static List<MoneyChangedEvent> GainedAlong(MoneyChangedEvent gain, IEnumerable<GameEvent> next)
        {
            var along = new List<MoneyChangedEvent>();
            if (!ComesUpTheHatch(gain)) return along;
            foreach (GameEvent gameEvent in next)
            {
                if (!(gameEvent is MoneyChangedEvent more) || more.PlayerId != gain.PlayerId || !ComesUpTheHatch(more)) break;
                along.Add(more);
            }

            return along;
        }

        /// <summary>
        /// The building level a town bought from the bank gets in the same step (the level picked on the city card), from
        /// the events still waiting; 0 when it is bought bare or changes hands otherwise.
        /// </summary>
        public static int BuiltWithPurchase(PropertyOwnerChangedEvent owner, IEnumerable<GameEvent> next)
        {
            if (owner == null || owner.PreviousOwnerId != GameState.NoOwner || owner.NewOwnerId == GameState.NoOwner) return 0;
            foreach (GameEvent gameEvent in next)
            {
                if (gameEvent is BuildingLevelChangedEvent building && building.SquareIndex == owner.SquareIndex) return building.Level;
                if (gameEvent is PropertyOwnerChangedEvent || gameEvent is PlayerMovedEvent || gameEvent is TurnStartedEvent || gameEvent is MoneyChangedEvent) break;
            }

            return 0;
        }

        /// <summary>
        /// Who gets a payment: the player whose gain of the same sum for the same reason comes right after it (rent, a trade,
        /// a bankrupt's cash), or nobody (<see cref="GameState.NoOwner"/>) when it goes to the bank.
        /// </summary>
        public static int PaymentReceiver(MoneyChangedEvent paid, IEnumerable<GameEvent> next)
        {
            if (paid == null || paid.Delta >= 0) return GameState.NoOwner;
            foreach (GameEvent gameEvent in next)
            {
                return gameEvent is MoneyChangedEvent gain && gain.Delta == -paid.Delta && gain.Reason == paid.Reason && gain.PlayerId != paid.PlayerId
                    ? gain.PlayerId : GameState.NoOwner;
            }

            return GameState.NoOwner;
        }

        /// <summary>
        /// The bank payments still waiting that fly with <paramref name="paid"/> (a purchase or a building payment): the same
        /// player's building payments after it before anyone moves or the turn passes (a town bought and built on the same
        /// card), so they make one flight instead of one each.
        /// </summary>
        public static List<MoneyChangedEvent> PaidAlong(MoneyChangedEvent paid, IEnumerable<GameEvent> next)
        {
            var along = new List<MoneyChangedEvent>();
            if (paid == null || paid.Delta >= 0 || (paid.Reason != MoneyReason.Building && paid.Reason != MoneyReason.PropertyPurchase)) return along;
            foreach (GameEvent gameEvent in next)
            {
                if (gameEvent is TurnStartedEvent || gameEvent is PlayerMovedEvent) break;
                if (gameEvent is MoneyChangedEvent more && more.PlayerId == paid.PlayerId && more.Delta < 0 && more.Reason == MoneyReason.Building) along.Add(more);
            }

            return along;
        }

        /// <summary>Each player's tray at the table's edge shows the money the table shows for them now.</summary>
        private void ShowMoneyPiles()
        {
            if (_board == null || _session.State == null) return;
            IReadOnlyList<PlayerState> players = _session.State.Players;
            for (int i = 0; i < players.Count; i++) _board.ShowSeatMoney(i, ShowsBankrupt(players[i]) ? 0 : ShownMoneyOf(players[i].Id));
        }

        /// <summary>A sum worth reacting to: from this much on a figure cheers at money coming in or sulks at money going out.</summary>
        public const int CheerFrom = 300000;
        public const int SulkFrom = 200000;

        /// <summary>The figure's reaction to a change of money: a cheer for a good sum coming in, a sulk for rent or tax going out.</summary>
        public static int Reaction(int delta) => delta >= CheerFrom ? 1 : delta <= -SulkFrom ? -1 : 0;

        private void React(int playerId, int delta)
        {
            PawnView pawn = Pawn(playerId);
            if (pawn == null) return;
            int reaction = Reaction(delta);
            if (reaction > 0) pawn.Cheer();
            else if (reaction < 0) pawn.Sulk();
        }

        private PawnView Pawn(int playerId) => playerId != GameState.NoOwner && _pawns.TryGetValue(playerId, out PawnView pawn) ? pawn : null;

        /// <summary>
        /// "+200" or "-60" rising beside the player's figure, at chest height and a little to the screen's
        /// right, so it neither hides the figure nor runs into the speech bubble over its head. Several at
        /// once stack (see <see cref="FloatingText"/>).
        /// </summary>
        private void ShowMoney(MoneyChangedEvent money)
        {
            if (money.Delta == 0 || !_pawns.TryGetValue(money.PlayerId, out PawnView pawn) || !pawn.gameObject.activeInHierarchy) return;
            string text = money.Delta > 0 ? "+" + Lang.M(money.Delta) : Lang.M(money.Delta);
            Color color = money.Delta > 0 ? MoneyGain : MoneyLoss;
            FloatingText.Spawn(transform, MoneySpot(pawn), text, color);
        }

        private static readonly Color MoneyGain = new Color(0.30f, 0.80f, 0.38f);
        private static readonly Color MoneyLoss = new Color(0.93f, 0.32f, 0.28f);

        /// <summary>How far to the screen's right of the figure a money line starts, in world units.</summary>
        public const float MoneySide = 0.45f;

        /// <summary>Where a money line starts on a figure, as a share of its height: the waist, so it rises past the chest and stays under any speech bubble.</summary>
        public const float MoneyHeight = 0.45f;

        private Vector3 MoneySpot(PawnView pawn)
        {
            float height = pawn.IsCharacter ? PawnView.CharacterHeight * MoneyHeight : 0.65f;
            return pawn.transform.position + Vector3.up * height + BoardView.ScreenRight(_labelYaw) * MoneySide;
        }

        private void AddLog(string line, LogTone tone = LogTone.Neutral, int playerId = GameState.NoOwner, bool turnStart = false)
        {
            Revision++;
            _log.Add(new LogEntry(line, tone, playerId, turnStart));
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        private void SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }

            camera.transform.position = BoardCameraRig.Place(Vector3.zero, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, BoardCameraRig.Distance);
            camera.transform.rotation = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
            camera.orthographic = true;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            CameraRig = camera.GetComponent<BoardCameraRig>();
            if (CameraRig == null) CameraRig = camera.gameObject.AddComponent<BoardCameraRig>();
            GraphicsQuality quality = RoomDressing.CurrentQuality();
            RoomDressing.DressCamera(camera, quality);
            RoomDressing.LightScene(quality);
            // The pipeline's share of the quality (edge smoothing, shadow map, render scale) was only set when the setting
            // changed, so a table opened with the stored setting drew without MSAA: jagged model edges from afar.
            RoomDressing.ApplyQuality(quality);
        }
    }
}
