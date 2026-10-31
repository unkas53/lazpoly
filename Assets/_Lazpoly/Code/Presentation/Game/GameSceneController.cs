using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.Achievements;
using Lazpoly.App.Lobby;
using Lazpoly.App.Presence;
using Lazpoly.App.Save;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Dice;
using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Root of the Game scene: creates the session (local, online host or online client) from the
    /// menu's setup, builds the board and pawns once the state is known, and plays the session's
    /// events back as animations, one at a time.
    /// </summary>
    public sealed class GameSceneController : MonoBehaviour
    {
        private const int MaxLogLines = 10;
        private const float CameraPitch = 55f;

        /// <summary>Pause after a card is drawn so it can be read before it acts.</summary>
        public const float CardReadSeconds = 1.6f;

        private readonly Queue<GameEvent> _pendingEvents = new Queue<GameEvent>();
        private readonly Dictionary<int, PawnView> _pawns = new Dictionary<int, PawnView>();
        private readonly Dictionary<int, Color> _playerColors = new Dictionary<int, Color>();
        private readonly List<string> _log = new List<string>();
        private readonly List<LogTone> _logTones = new List<LogTone>();
        private readonly Dictionary<int, int> _shownMoney = new Dictionary<int, int>();
        private int _shownPot;

        private BoardData _data;
        private IGameSession _session;
        private HostSession _host;
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
        private GameHud _hud;

        public IGameSession Session => _session;
        public IReadOnlyList<string> Log => _log;

        /// <summary>The colour of each <see cref="Log"/> line, same order.</summary>
        public IReadOnlyList<LogTone> LogTones => _logTones;
        public string LastRejection { get; private set; }

        /// <summary>Zoom and turn of the board view; the HUD's reset button uses it.</summary>
        public BoardCameraRig CameraRig { get; private set; }

        /// <summary>The board on the table, once the game state is known.</summary>
        public BoardView Board => _board;

        /// <summary>Goes up whenever something the HUD shows may have changed, so it knows when to redraw.</summary>
        public int Revision { get; private set; }

        /// <summary>Why an online game stopped; null while it goes on.</summary>
        public SessionEndReason? EndReason { get; private set; }

        /// <summary>True while events are still being animated; the HUD waits before offering actions.</summary>
        public bool IsBusy => _playing || _pendingEvents.Count > 0;

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

        public Color ColorOf(int playerId) => _playerColors.TryGetValue(playerId, out Color color) ? color : Color.white;

        /// <summary>Colour of the group a square belongs to, as the board shows it.</summary>
        public Color GroupColorOf(int squareIndex) => _data.GroupColor(_session.Board[squareIndex].GroupId);

        /// <summary>Name of the group a square belongs to, from the board file (null if it has none).</summary>
        public string GroupNameOf(int squareIndex) => _data.GroupName(_session.Board[squareIndex].GroupId);

        /// <summary>Whether a player's machine is reachable. Always true except for dropped players seen by the host.</summary>
        public bool IsConnected(int playerId) => _host == null || _host.IsConnected(playerId);

        private void Start()
        {
            AudioPlayer.Ensure();
            _data = BoardDataLoader.LoadFromResources();
            GameServices.Registry?.TryGet(out _settings);
            GameServices.Registry?.TryGet(out _achievements);
            GameServices.Registry?.TryGet(out _presence);
            if (_settings != null) _settings.Changed += OnSettingsChanged;

            _session = CreateSession(OnlineGameSetup.Take());
            _tracker = new AchievementTracker(_data.Board, _session.CanControl, _session.IsAiPlaying, _isOnline);

            _session.EventsPublished += OnEventsPublished;
            _session.CommandRejected += OnCommandRejected;
            _session.StateReset += OnStateReset;
            _session.Ended += OnEnded;
            _session.Ready += BuildViews;
            if (_host != null) _host.RemotePlayerConnectionChanged += OnRemoteConnectionChanged;

            SetUpCamera();
            _hud = gameObject.AddComponent<GameHud>();
            _hud.Bind(this);

            if (_session.IsReady) BuildViews();
            else AddLog("Ev sahibine bağlanılıyor...");

            if (_session is HostSession host) host.Start();
            else if (_session is ClientSession client) client.Start();
        }

        private void Update()
        {
            if (_session == null) return;
            _session.Tick();

            if (_board == null || IsBusy || EndReason.HasValue || Time.time < _nextAiMoveTime) return;
            if (_session.StepAi()) _nextAiMoveTime = Time.time + Settings.AiMoveDelaySeconds;
        }

        private void LateUpdate()
        {
            if (_board == null || _hud == null) return;
            float reserved = _hud.ReservedSidePixels;
            if (Screen.width == _framedWidth && Screen.height == _framedHeight && Mathf.Approximately(reserved, _framedReserve)) return;
            FrameBoard(reserved);
        }

        private void FrameBoard(float reservedSidePixels)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            _framedWidth = Screen.width;
            _framedHeight = Screen.height;
            _framedReserve = reservedSidePixels;
            Vector3 framed = BoardCameraFraming.CameraPosition(_board.transform.position, _board.HalfExtent, camera.fieldOfView,
                CameraPitch, Screen.width, Screen.height, reservedSidePixels);
            CameraRig.SetBase(_board.transform.position, framed, CameraPitch);
        }

        private void OnDestroy()
        {
            if (_settings != null) _settings.Changed -= OnSettingsChanged;
            if (_session == null) return;
            _session.EventsPublished -= OnEventsPublished;
            _session.CommandRejected -= OnCommandRejected;
            _session.StateReset -= OnStateReset;
            _session.Ended -= OnEnded;
            _session.Ready -= BuildViews;
            if (_host != null) _host.RemotePlayerConnectionChanged -= OnRemoteConnectionChanged;
        }

        /// <summary>
        /// Whether this machine may send the command and the rules would accept it now. The HUD
        /// offers only such actions; the host still validates whatever arrives.
        /// </summary>
        public bool IsAllowed(GameCommand command)
        {
            if (_session == null || !_session.IsReady || EndReason.HasValue || !_session.CanControl(command.PlayerId)) return false;
            return GameRules.Validate(_session.Board, _session.Rules, _session.State, command) == RejectionReason.None;
        }

        public void Submit(GameCommand command)
        {
            LastRejection = null;
            Revision++;
            _session.Submit(command);
        }

        /// <summary>Leaves the game (and the online room, if any) and returns to the main menu.</summary>
        public void LeaveToMainMenu()
        {
            SaveLocalGame();

            if (_isOnline && GameServices.Registry != null && GameServices.Registry.TryGet(out ILobbyService lobby))
            {
                lobby.Leave();
            }

            SceneManager.LoadScene("MainMenu");
        }

        private IGameSession CreateSession(OnlineLaunch online)
        {
            if (online == null)
            {
                _localGame = CreateLocalGame();
                return _localGame.Session;
            }

            _isOnline = true;
            if (!online.IsHost) return new ClientSession(online.Transport, online.HostPeer, _data.Board, _data.Rules, _data.Checksum);

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
                    AddLog("Kayıtlı oyun yüklendi.");
                    return resumed;
                }
                catch (ArgumentException exception)
                {
                    Debug.LogWarning($"[Lazpoly] Saved game could not be resumed: {exception.Message}");
                    AddLog("Kayıt yüklenemedi, yeni oyun başladı.");
                }
            }

            return LocalGame.Create(_data.Board, _data.Rules.WithTable(TableSetup.Chosen), LocalGameSetup.TakeSeats(),
                LocalGameSetup.TakeAiPlayers(), NewSeed());
        }

        private static Pcg32Random NewRandom() => new Pcg32Random(NewSeed());

        private static ulong NewSeed() => unchecked((ulong)DateTime.UtcNow.Ticks);

        /// <summary>Keeps the single local save slot in step with the game; a finished game leaves no save.</summary>
        private void SaveLocalGame()
        {
            if (_localGame == null) return;
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
            _dice = DiceView.Create(transform, new Vector3(0f, 0.04f, -1.2f));
            _dice.Speed = Settings.AnimationSpeed;
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Color color = PlayerPalette.ForSeat(i);
                _playerColors[player.Id] = color;
                PawnView pawn = PawnView.Create(transform, _board, i, state.Players.Count, color);
                pawn.Speed = Settings.AnimationSpeed;
                pawn.Stepped += _ => AudioPlayer.Play(SoundCue.PawnStep);
                _pawns[player.Id] = pawn;
            }

            SyncViewsToState();
            AddLog($"Sıra: {state.CurrentPlayer.Name}");
            ShowPresence();
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

            _shownPot = state.Pot;
            _board.SetPot(state.Pot);
            for (int i = 0; i < state.SquareCount; i++)
            {
                int owner = state.GetOwner(i);
                _board.SetOwner(i, owner == GameState.NoOwner ? (Color?)null : ColorOf(owner));
                _board.SetBuildingLevel(i, state.GetBuildingLevel(i));
                _board.SetMortgaged(i, state.IsMortgaged(i));
            }
        }

        private void OnEventsPublished(IReadOnlyList<GameEvent> events)
        {
            Revision++;
            SaveLocalGame();
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
                AddLog($"Başarım: {TitleOf(id)}");
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
        }

        private void OnCommandRejected(RejectionReason reason)
        {
            LastRejection = $"İşlem reddedildi: {reason}";
            Revision++;
        }

        private void OnStateReset()
        {
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
            AddLog("Oyun durumu ev sahibinden yeniden alındı.");
        }

        private void OnEnded(SessionEndReason reason)
        {
            EndReason = reason;
            AddLog(EndReasonText(reason));
        }

        private void OnRemoteConnectionChanged(int playerId, bool connected)
        {
            string name = _session.State.FindPlayer(playerId)?.Name ?? $"Oyuncu {playerId}";
            AddLog(connected ? $"{name} bağlandı." : $"{name} bağlantısı koptu.");
        }

        public static string EndReasonText(SessionEndReason reason)
        {
            switch (reason)
            {
                case SessionEndReason.HostLeft: return "Ev sahibi oyundan ayrıldı.";
                case SessionEndReason.NotInThisGame: return "Bu oyunda yerin yok.";
                case SessionEndReason.VersionMismatch: return "Oyun sürümün ev sahibininkiyle aynı değil.";
                case SessionEndReason.BoardMismatch: return "Tahta verin ev sahibininkiyle aynı değil.";
                default: return "Oyun sona erdi.";
            }
        }

        private IEnumerator PlayEvents()
        {
            _playing = true;
            while (_pendingEvents.Count > 0)
            {
                GameEvent gameEvent = _pendingEvents.Dequeue();
                string line = EventDescriber.Describe(gameEvent, _session.State, _session.Board);
                if (line != null) AddLog(line, EventDescriber.ToneOf(gameEvent));

                AudioPlayer.Play(SoundCues.For(gameEvent));
                switch (gameEvent)
                {
                    case DiceRolledEvent rolled:
                        AudioPlayer.Play(SoundCue.DiceShake);
                        yield return _dice.Roll(rolled.Die1, rolled.Die2);
                        AudioPlayer.Play(SoundCue.DiceLand);
                        break;
                    case PlayerMovedEvent moved:
                        PawnView pawn = _pawns[moved.PlayerId];
                        if (moved.Kind == MoveKind.Jump) yield return pawn.JumpTo(moved.ToSquare);
                        else yield return pawn.WalkTo(moved.ToSquare, backward: moved.Kind == MoveKind.Back);
                        break;
                    case PotChangedEvent pot:
                        _shownPot += pot.Delta;
                        _board.SetPot(_shownPot);
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
                        _shownMoney[money.PlayerId] = ShownMoneyOf(money.PlayerId) + money.Delta;
                        Revision++;
                        ShowMoney(money);
                        break;
                    case PropertyOwnerChangedEvent owner:
                        _board.SetOwner(owner.SquareIndex,
                            owner.NewOwnerId == GameState.NoOwner ? (Color?)null : ColorOf(owner.NewOwnerId));
                        if (owner.NewOwnerId != GameState.NoOwner) _board.PulseOwner(owner.SquareIndex);
                        break;
                    case PlayerBankruptedEvent bankrupt:
                        yield return _pawns[bankrupt.PlayerId].Topple();
                        break;
                    case BuildingLevelChangedEvent building:
                        _board.SetBuildingLevel(building.SquareIndex, building.Level, animate: true);
                        break;
                    case MortgageChangedEvent mortgage:
                        _board.SetMortgaged(mortgage.SquareIndex, mortgage.IsMortgaged);
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

        /// <summary>"+200" or "-60" rising over the player's pawn.</summary>
        private void ShowMoney(MoneyChangedEvent money)
        {
            if (money.Delta == 0 || !_pawns.TryGetValue(money.PlayerId, out PawnView pawn) || !pawn.gameObject.activeInHierarchy) return;
            string text = money.Delta > 0 ? $"+{money.Delta}" : money.Delta.ToString();
            Color color = money.Delta > 0 ? new Color(0.45f, 0.92f, 0.5f) : new Color(1f, 0.42f, 0.38f);
            FloatingText.Spawn(transform, pawn.transform.position + Vector3.up * 0.65f, text, color);
        }

        private void AddLog(string line, LogTone tone = LogTone.Neutral)
        {
            Revision++;
            _log.Add(line);
            _logTones.Add(tone);
            if (_log.Count <= MaxLogLines) return;
            _log.RemoveAt(0);
            _logTones.RemoveAt(0);
        }

        private void SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }

            camera.transform.position = new Vector3(0f, 15f, -11f);
            camera.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            camera.fieldOfView = 50f;
            CameraRig = camera.GetComponent<BoardCameraRig>();
            if (CameraRig == null) CameraRig = camera.gameObject.AddComponent<BoardCameraRig>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.10f, 0.11f, 0.13f);

            // A warm key light with soft shadows so the pieces stand out from the board.
            Light sun = FindAnyObjectByType<Light>();
            if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.6f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
