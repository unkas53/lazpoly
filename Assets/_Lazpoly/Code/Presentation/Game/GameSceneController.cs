using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.Achievements;
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

        /// <summary>Seconds the light takes to slide from noon all the way to dusk; a single round's step is a fraction of it.</summary>
        private const float DaySeconds = 4f;

        /// <summary>Pause after a card is drawn so it can be read before it acts.</summary>
        public const float CardReadSeconds = 1.6f;

        /// <summary>Pause after the dice settle, so the result is read before the pawn moves.</summary>
        public const float RollReadSeconds = 0.55f;

        /// <summary>A payment of this much or more flies as several coins.</summary>
        private const int CoinStep = 120;
        private const int MaxCoins = 4;

        private readonly Queue<GameEvent> _pendingEvents = new Queue<GameEvent>();
        private readonly Dictionary<int, PawnView> _pawns = new Dictionary<int, PawnView>();
        private readonly Dictionary<int, SpeechBubble> _bubbles = new Dictionary<int, SpeechBubble>();
        private PawnCrowd _crowd;
        private TurnMarker _turnMarker;
        private readonly Dictionary<int, Color> _playerColors = new Dictionary<int, Color>();
        private readonly List<LogEntry> _log = new List<LogEntry>();
        private readonly Dictionary<int, int> _shownMoney = new Dictionary<int, int>();
        private int _shownPot;

        /// <summary>What the playback has shown so far of the turn: who last moved, which phase was reached, which trade is open; the bubbles' lines depend on them.</summary>
        private int _lastMover = GameState.NoOwner;
        private GamePhase _shownPhase;
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
        private float _dayShown = -1f;
        private float _dayApplied = -1f;
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
        /// Seconds an online player has left before the host moves for them, counted from the last
        /// moves this machine saw (the host keeps the real clock). Infinite in a local game.
        /// </summary>
        public float SecondsLeft => _isOnline && _session?.State != null
            ? (float)TurnClock.LimitFor(_session.State.Phase, _session.Rules.TurnSeconds) - (Time.unscaledTime - _lastProgressTime)
            : float.PositiveInfinity;

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

        /// <summary>How far the shown day has gone, 0 (noon) to 1 (dusk); -1 before the board is up.</summary>
        public float DayProgress => _dayShown;

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

        /// <summary>Goes up whenever something the HUD shows may have changed, so it knows when to redraw.</summary>
        public int Revision { get; private set; }

        /// <summary>Why an online game stopped; null while it goes on.</summary>
        public SessionEndReason? EndReason { get; private set; }

        /// <summary>True while events are still being animated; the HUD waits before offering actions.</summary>
        public bool IsBusy => _playing || _pendingEvents.Count > 0;

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

        /// <summary>Real time the shown card was drawn, so the HUD can fade it.</summary>
        public float ShownCardTime { get; private set; }

        /// <summary>The last roll shown on the table (0 before the first), and real time it settled, for the HUD's result popup.</summary>
        public int ShownDie1 { get; private set; }
        public int ShownDie2 { get; private set; }
        public float ShownRollTime { get; private set; } = float.NegativeInfinity;

        /// <summary>Whether the shown roll was the third double in a row, which forfeits the move.</summary>
        public bool ShownRollForfeited { get; private set; }

        /// <summary>Who paid last and why, so the matching gain can be shown as coins flying from them.</summary>
        private int _lastPayer = -1;
        private MoneyReason _lastPaymentReason;

        public Color ColorOf(int playerId) => _playerColors.TryGetValue(playerId, out Color color) ? color : Color.white;

        /// <summary>Colour of the group a square belongs to, as the board shows it.</summary>
        public Color GroupColorOf(int squareIndex) => _data.GroupColor(_session.Board[squareIndex].GroupId);

        /// <summary>The letter on the square's group marks when the colour-blind friendly colours are on; null otherwise.</summary>
        public string GroupMarkOf(int squareIndex) => _data.GroupMark(_session.Board[squareIndex].GroupId);

        /// <summary>Name of the group a square belongs to, from the board file (null if it has none).</summary>
        public string GroupNameOf(int squareIndex) => _data.GroupName(_session.Board[squareIndex].GroupId);

        private void Start()
        {
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
            if (_client != null) RejoinRoomIfNeeded();
            if (_isOnline && Time.unscaledTime >= _nextPresenceRefresh)
            {
                _nextPresenceRefresh = Time.unscaledTime + PresenceRefreshSeconds;
                Revision++; // Connection dots, latency and the reconnect countdown move without a game event.
            }

            UpdateDayLight();
            if (_board == null || Paused || IsBusy || EndReason.HasValue || Time.time < _nextAiMoveTime) return;
            if (_session.StepAi()) _nextAiMoveTime = Time.time + Settings.AiMoveDelaySeconds;
        }

        /// <summary>
        /// Slides the light towards where the game's round puts it (see <see cref="DayCycle"/>), a little
        /// each frame so a new round is felt rather than seen. With the setting off the day stays at noon.
        /// </summary>
        private void UpdateDayLight()
        {
            GameState state = _session.State;
            if (_board == null || state == null) return;
            bool over = state.Phase == GamePhase.GameOver || EndReason.HasValue;
            float target = Settings.DayCycle ? DayCycle.Progress(state.Round, _session.Rules.RoundLimit, over) : 0f;
            _dayShown = _dayShown < 0f ? target : Mathf.MoveTowards(_dayShown, target, Time.deltaTime / DaySeconds);
            if (Mathf.Approximately(_dayShown, _dayApplied)) return;
            _dayApplied = _dayShown;
            DayLight light = DayCycle.At(_dayShown);
            RoomDressing.ApplyDayLight(light);
        }

        private void LateUpdate()
        {
            if (_board == null || _hud == null) return;
            // The board's writing turns to stay readable when a screenshot preset looks from another corner.
            if (CameraRig != null && !Mathf.Approximately(CameraRig.Yaw, _labelYaw))
            {
                _labelYaw = CameraRig.Yaw;
                _board.FaceCamera(_labelYaw);
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
                seats.Add(new PlayerSeat(player.Id, player.Name));
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
            var engine = new GameEngine(_data.Board, _data.Rules.WithTable(TableSetup.Chosen), plan.Seats, NewRandom());
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
                    AddLog(L("Kayıtlı oyun yüklendi."));
                    return resumed;
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning($"[Lazpoly] Saved game could not be resumed: {exception.Message}");
                    AddLog(L("Kayıt yüklenemedi, yeni oyun başladı."));
                }
            }

            return LocalGame.Create(_data.Board, _data.Rules.WithTable(TableSetup.Chosen), LocalGameSetup.TakeSeats(),
                LocalGameSetup.TakeAiPlayers(), NewSeed());
        }

        private static Pcg32Random NewRandom() => new Pcg32Random(NewSeed());

        private static ulong NewSeed() => unchecked((ulong)DateTime.UtcNow.Ticks);

        /// <summary>Keeps the single local save slot in step with the game; a finished game leaves no save. The -perf table is nobody's game, so it never touches the slot.</summary>
        private void SaveLocalGame()
        {
            if (_localGame == null || PerfRun.Requested) return;
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
            // The dice land on the lawn, below the middle of the screen so they stay clear of the score.
            _dice = DiceView.Create(transform, BoardView.ScreenUp(BoardCameraRig.BaseYaw) * -1.5f + Vector3.up * (BoardView.LawnHeight + 0.02f));
            _dice.Speed = Settings.AnimationSpeed;
            _crowd = new PawnCrowd(state.Players.Count);
            _turnMarker = TurnMarker.Create(transform);
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Color color = PlayerPalette.ForSeat(i);
                _playerColors[player.Id] = color;
                PawnView pawn = PawnView.Create(transform, _board, i, color, _crowd, player.Id);
                pawn.Speed = Settings.AnimationSpeed;
                pawn.Stepped += _ => AudioPlayer.Play(SoundCue.PawnStep);
                PawnTrail trail = PawnTrail.Create(transform, color);
                pawn.Departed += trail.Leave;
                _pawns[player.Id] = pawn;
            }

            SyncViewsToState();
            AddLog(F($"Sıra: {state.CurrentPlayer.Name}"), LogTone.Neutral, state.CurrentPlayer.Id, turnStart: true);
            ShowPresence();
            NoteLoadTime(state.Players.Count);
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
            Application.Quit();
        }

        /// <summary>Puts every pawn and owner marker where the state says, without animation.</summary>
        private void SyncViewsToState()
        {
            GameState state = _session.State;
            foreach (PlayerState player in state.Players)
            {
                _shownMoney[player.Id] = player.Money;
                PawnView pawn = _pawns[player.Id];
                pawn.PlaceOn(player.Position);
                if (player.IsBankrupt) pawn.SetEliminated();
            }

            MarkTurn(state.CurrentPlayer.Id);
            _shownPot = state.Pot;
            _board.SetPot(state.Pot);
            _board.ShowRoll(state.LastDie1, state.LastDie2);
            for (int i = 0; i < state.SquareCount; i++)
            {
                int owner = state.GetOwner(i);
                _board.SetOwner(i, owner == GameState.NoOwner ? (Color?)null : ColorOf(owner));
                _board.SetBuildingLevel(i, state.GetBuildingLevel(i));
            }
        }

        private void OnEventsPublished(IReadOnlyList<GameEvent> events)
        {
            _lastProgressTime = Time.unscaledTime;
            Revision++;
            _trail.Events(events);
            SaveLocalGame();
            if (_client != null && _client.State.Phase == GamePhase.GameOver) LastRoomStore.Forget();
            UnlockAchievements(events);
            ShowPresence();
            if (_board == null) return;
            foreach (GameEvent gameEvent in events) _pendingEvents.Enqueue(gameEvent);
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
                        if (rolled.Die1 == rolled.Die2) Say(rolled.PlayerId, L("Çift!"));
                        yield return new WaitForSeconds(RollReadSeconds / Speed);
                        break;
                    case PlayerMovedEvent moved:
                        _lastMover = moved.PlayerId;
                        PawnView pawn = _pawns[moved.PlayerId];
                        if (moved.Kind == MoveKind.Jump) yield return pawn.JumpTo(moved.ToSquare);
                        else yield return pawn.WalkTo(moved.ToSquare, backward: moved.Kind == MoveKind.Back);
                        if (moved.PassedStart) _board.PulseTile(0);
                        break;
                    case PotChangedEvent pot:
                        _shownPot += pot.Delta;
                        _board.SetPot(_shownPot);
                        _board.NudgePot(pot.Delta > 0);
                        Revision++;
                        break;
                    case CardDrawnEvent card:
                        ShownCard = card.CardIndex;
                        ShownCardPlayer = card.PlayerId;
                        ShownCardTime = Time.unscaledTime;
                        Revision++;
                        // A moment to read the card before its effect plays.
                        yield return new WaitForSeconds(CardReadSeconds / Mathf.Max(Settings.AnimationSpeed, 0.1f));
                        break;
                    case MoneyChangedEvent money:
                        yield return ShowPayment(money);
                        _shownMoney[money.PlayerId] = ShownMoneyOf(money.PlayerId) + money.Delta;
                        Revision++;
                        ShowMoney(money);
                        break;
                    case PropertyOwnerChangedEvent owner:
                        if (owner.NewOwnerId == GameState.NoOwner)
                        {
                            _board.ClearOwner(owner.SquareIndex, animate: true);
                            break;
                        }

                        _board.SetOwner(owner.SquareIndex, ColorOf(owner.NewOwnerId));
                        _board.PulseOwner(owner.SquareIndex);
                        _board.RaiseFlag(owner.SquareIndex);
                        // Bought from the bank on landing; an auction win and a trade have their own lines.
                        if (owner.PreviousOwnerId == GameState.NoOwner && _shownPhase == GamePhase.AwaitingBuyDecision) Say(owner.NewOwnerId, L("Aldım!"));
                        break;
                    case PlayerBankruptedEvent bankrupt:
                        Say(bankrupt.PlayerId, L("Battım..."));
                        yield return _pawns[bankrupt.PlayerId].Topple();
                        break;
                    case BuildingLevelChangedEvent building:
                        if (building.Level > _board.LevelOf(building.SquareIndex))
                        {
                            Say(_session.State.GetOwner(building.SquareIndex), L(building.Level >= SquareDefinition.MaxBuildingLevel ? "Otel vakti!" : "Bir ev daha!"));
                        }

                        _board.SetBuildingLevel(building.SquareIndex, building.Level, animate: true);
                        if (building.Level >= SquareDefinition.MaxBuildingLevel) AudioPlayer.Play(SoundCue.Hotel);
                        break;
                    case PhaseChangedEvent phase:
                        _shownPhase = phase.Phase;
                        break;
                    case AuctionStartedEvent _:
                        // Straight after a buy decision the auction means the player who landed let the square go.
                        if (_shownPhase == GamePhase.AwaitingBuyDecision) Say(_lastMover, L("Almıyorum, artırmaya."));
                        break;
                    case BidPlacedEvent bid:
                        Say(bid.PlayerId, L("Artırıyorum!"));
                        break;
                    case BidPassedEvent passed:
                        Say(passed.PlayerId, L("Ben yokum."));
                        break;
                    case AuctionEndedEvent ended:
                        if (ended.WinnerId != GameState.NoOwner) Say(ended.WinnerId, L("Benim oldu!"));
                        break;
                    case TradeProposedEvent proposed:
                        _shownOffer = proposed.Offer;
                        Say(proposed.Offer.FromPlayerId, L("Takas edelim mi?"));
                        break;
                    case TradeResolvedEvent resolved:
                        if (_shownOffer != null && resolved.Outcome != TradeOutcome.Cancelled)
                        {
                            Say(_shownOffer.ToPlayerId, L(resolved.Outcome == TradeOutcome.Accepted ? "Anlaştık!" : "Olmaz."));
                        }

                        _shownOffer = null;
                        break;
                    case GameEndedEvent _:
                        Confetti.Burst(transform, _board.transform.position + Vector3.up * 0.5f, _board.SurfaceY + 0.01f,
                            new List<Color>(_playerColors.Values));
                        break;
                }
            }

            // Everything is shown now, so the money on screen is the state's money.
            foreach (PlayerState player in _session.State.Players) _shownMoney[player.Id] = player.Money;
            _shownPot = _session.State.Pot;
            _board.SetPot(_shownPot);
            Revision++;
            _playing = false;
        }

        private float Speed => Mathf.Max(Settings.AnimationSpeed, 0.1f);

        /// <summary>
        /// A computer player's line in a bubble over its pawn, for the choices it makes (buying, letting a
        /// square go, bidding, building, trading). People at the table say nothing: they know what they did.
        /// A newer line replaces the pawn's earlier bubble.
        /// </summary>
        private void Say(int playerId, string line)
        {
            if (playerId == GameState.NoOwner || !_session.IsAiPlaying(playerId)) return;
            if (!_pawns.TryGetValue(playerId, out PawnView pawn) || !pawn.gameObject.activeInHierarchy) return;
            if (_bubbles.TryGetValue(playerId, out SpeechBubble earlier) && earlier != null) earlier.Dismiss();
            _bubbles[playerId] = SpeechBubble.Say(transform, pawn.transform, line, ColorOf(playerId));
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

        /// <summary>Moves the ring under the pawn whose turn it is.</summary>
        private void MarkTurn(int playerId)
        {
            if (_turnMarker == null) return;
            _turnMarker.Follow(_pawns.TryGetValue(playerId, out PawnView pawn) ? pawn : null, ColorOf(playerId));
        }

        private void ShowRoll(DiceRolledEvent rolled)
        {
            ShownDie1 = rolled.Die1;
            ShownDie2 = rolled.Die2;
            ShownRollTime = Time.unscaledTime;
            _board.ShowRoll(rolled.Die1, rolled.Die2);
            // The engine ends the turn straight after a third double; the move is forfeited.
            ShownRollForfeited = rolled.Die1 == rolled.Die2 && _pendingEvents.Count > 0 &&
                                 _pendingEvents.Peek() is PhaseChangedEvent next && next.Phase == GamePhase.TurnEnd;
            Revision++;
        }

        /// <summary>
        /// Money seen moving: rent flies from the payer's pawn to the owner's, taxes and fines to the
        /// pot. The gain side waits for the coins to land before its number changes.
        /// </summary>
        private IEnumerator ShowPayment(MoneyChangedEvent money)
        {
            if (money.Delta < 0)
            {
                _lastPayer = money.PlayerId;
                _lastPaymentReason = money.Reason;
                bool toPot = _session.Board.HasPot && (money.Reason == MoneyReason.Tax || money.Reason == MoneyReason.Card);
                if (toPot && _pawns.TryGetValue(money.PlayerId, out PawnView payer) && payer.gameObject.activeInHierarchy)
                {
                    FlyCoins(payer.transform.position, _board.transform.position + Vector3.up * 0.2f, -money.Delta);
                }

                yield break;
            }

            bool rent = money.Reason == MoneyReason.Rent && _lastPayer >= 0 && _lastPaymentReason == MoneyReason.Rent && _lastPayer != money.PlayerId;
            if (!rent || !_pawns.TryGetValue(_lastPayer, out PawnView from) || !_pawns.TryGetValue(money.PlayerId, out PawnView to)) yield break;
            if (!from.gameObject.activeInHierarchy || !to.gameObject.activeInHierarchy) yield break;
            _lastPayer = -1;
            float seconds = FlyCoins(from.transform.position, to.transform.position, money.Delta);
            yield return new WaitForSeconds(seconds);
        }

        /// <summary>Starts one coin per <see cref="CoinStep"/> of the amount (at least one, at most four) and says how long they fly.</summary>
        private float FlyCoins(Vector3 from, Vector3 to, int amount)
        {
            int coins = Mathf.Clamp(amount / CoinStep + 1, 1, MaxCoins);
            float total = 0f;
            for (int i = 0; i < coins; i++)
            {
                CoinFlight coin = CoinFlight.Spawn(transform, from + Vector3.up * 0.3f, to + Vector3.up * 0.3f, Speed, i * 0.08f);
                total = Mathf.Max(total, coin.TotalSeconds);
            }

            return total;
        }

        /// <summary>"+200" or "-60" rising over the player's pawn.</summary>
        private void ShowMoney(MoneyChangedEvent money)
        {
            if (money.Delta == 0 || !_pawns.TryGetValue(money.PlayerId, out PawnView pawn) || !pawn.gameObject.activeInHierarchy) return;
            string text = money.Delta > 0 ? "+" + Lang.N(money.Delta) : Lang.N(money.Delta);
            Color color = money.Delta > 0 ? new Color(0.45f, 0.92f, 0.5f) : new Color(1f, 0.42f, 0.38f);
            FloatingText.Spawn(transform, pawn.transform.position + Vector3.up * 0.65f, text, color);
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
        }
    }
}
