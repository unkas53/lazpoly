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
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Dice;
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

        private readonly Queue<GameEvent> _pendingEvents = new Queue<GameEvent>();
        private readonly Dictionary<int, PawnView> _pawns = new Dictionary<int, PawnView>();
        private readonly Dictionary<int, Color> _playerColors = new Dictionary<int, Color>();
        private readonly List<string> _log = new List<string>();

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

        public IGameSession Session => _session;
        public IReadOnlyList<string> Log => _log;
        public string LastRejection { get; private set; }

        /// <summary>Why an online game stopped; null while it goes on.</summary>
        public SessionEndReason? EndReason { get; private set; }

        /// <summary>True while events are still being animated; the HUD waits before offering actions.</summary>
        public bool IsBusy => _playing || _pendingEvents.Count > 0;

        private GameSettings Settings => _settings?.Current ?? GameSettings.Default;

        public Color ColorOf(int playerId) => _playerColors.TryGetValue(playerId, out Color color) ? color : Color.white;

        /// <summary>Whether a player's machine is reachable. Always true except for dropped players seen by the host.</summary>
        public bool IsConnected(int playerId) => _host == null || _host.IsConnected(playerId);

        private void Start()
        {
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
            gameObject.AddComponent<PrototypeHud>().Bind(this);

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
            var engine = new GameEngine(_data.Board, _data.Rules, plan.Seats, NewRandom());
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

            return LocalGame.Create(_data.Board, _data.Rules, LocalGameSetup.TakeSeats(), LocalGameSetup.TakeAiPlayers(), NewSeed());
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
                PawnView pawn = _pawns[player.Id];
                pawn.PlaceOn(player.Position);
                if (player.IsBankrupt) pawn.SetEliminated();
            }

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
                if (line != null) AddLog(line);

                switch (gameEvent)
                {
                    case DiceRolledEvent rolled:
                        yield return _dice.Roll(rolled.Die1, rolled.Die2);
                        break;
                    case PlayerMovedEvent moved:
                        yield return _pawns[moved.PlayerId].WalkTo(moved.ToSquare);
                        break;
                    case PropertyOwnerChangedEvent owner:
                        _board.SetOwner(owner.SquareIndex,
                            owner.NewOwnerId == GameState.NoOwner ? (Color?)null : ColorOf(owner.NewOwnerId));
                        break;
                    case PlayerBankruptedEvent bankrupt:
                        _pawns[bankrupt.PlayerId].SetEliminated();
                        break;
                    case BuildingLevelChangedEvent building:
                        _board.SetBuildingLevel(building.SquareIndex, building.Level);
                        break;
                    case MortgageChangedEvent mortgage:
                        _board.SetMortgaged(mortgage.SquareIndex, mortgage.IsMortgaged);
                        break;
                }
            }

            _playing = false;
        }

        private void AddLog(string line)
        {
            _log.Add(line);
            if (_log.Count > MaxLogLines) _log.RemoveAt(0);
        }

        private void SetUpCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                camera = new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>();
            }

            camera.transform.position = new Vector3(0f, 15f, -11f);
            camera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            camera.fieldOfView = 50f;
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
