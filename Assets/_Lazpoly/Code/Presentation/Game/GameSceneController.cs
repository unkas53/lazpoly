using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using Lazpoly.Infra;
using Lazpoly.Presentation.Data;
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
        private BoardView _board;
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

        public Color ColorOf(int playerId) => _playerColors.TryGetValue(playerId, out Color color) ? color : Color.white;

        /// <summary>Whether a player's machine is reachable. Always true except for dropped players seen by the host.</summary>
        public bool IsConnected(int playerId) => _host == null || _host.IsConnected(playerId);

        private void Start()
        {
            _data = BoardDataLoader.LoadFromResources();
            _session = CreateSession(OnlineGameSetup.Take());

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
            _session?.Tick();
        }

        private void OnDestroy()
        {
            if (_session == null) return;
            _session.EventsPublished -= OnEventsPublished;
            _session.CommandRejected -= OnCommandRejected;
            _session.StateReset -= OnStateReset;
            _session.Ended -= OnEnded;
            _session.Ready -= BuildViews;
            if (_host != null) _host.RemotePlayerConnectionChanged -= OnRemoteConnectionChanged;
        }

        public void Submit(GameCommand command)
        {
            LastRejection = null;
            _session.Submit(command);
        }

        /// <summary>Leaves the game (and the online room, if any) and returns to the main menu.</summary>
        public void LeaveToMainMenu()
        {
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
                return new LocalGameSession(new GameEngine(_data.Board, _data.Rules, LocalGameSetup.TakeSeats(), NewRandom()));
            }

            _isOnline = true;
            if (!online.IsHost) return new ClientSession(online.Transport, online.HostPeer, _data.Board, _data.Checksum);

            OnlineSeatPlan plan = online.HostPlan;
            var engine = new GameEngine(_data.Board, _data.Rules, plan.Seats, NewRandom());
            _host = new HostSession(engine, online.Transport, plan.HostPlayerId, plan.RemotePlayers, _data.Checksum);
            return _host;
        }

        private static Pcg32Random NewRandom() => new Pcg32Random(unchecked((ulong)DateTime.UtcNow.Ticks));

        private void BuildViews()
        {
            if (_board != null) return;

            GameState state = _session.State;
            _board = BoardView.Create(transform, _data);
            for (int i = 0; i < state.Players.Count; i++)
            {
                PlayerState player = state.Players[i];
                Color color = PlayerPalette.ForSeat(i);
                _playerColors[player.Id] = color;
                _pawns[player.Id] = PawnView.Create(transform, _board, i, state.Players.Count, color);
            }

            SyncViewsToState();
            AddLog($"Sıra: {state.CurrentPlayer.Name}");
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
            }
        }

        private void OnEventsPublished(IReadOnlyList<GameEvent> events)
        {
            if (_board == null) return;
            foreach (GameEvent gameEvent in events) _pendingEvents.Enqueue(gameEvent);
            if (!_playing) _playback = StartCoroutine(PlayEvents());
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
        }
    }
}
