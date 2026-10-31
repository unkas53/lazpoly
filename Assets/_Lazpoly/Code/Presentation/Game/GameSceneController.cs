using System;
using System.Collections;
using System.Collections.Generic;
using Lazpoly.App.Session;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;
using Lazpoly.Core.Random;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Setup;
using Lazpoly.Presentation.Tabletop;
using Lazpoly.Presentation.Texts;
using UnityEngine;

namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// Root of the Game scene: creates the session from the menu's setup, builds the board and
    /// pawns, and plays the session's events back as animations, one at a time.
    /// </summary>
    public sealed class GameSceneController : MonoBehaviour
    {
        private const int MaxLogLines = 10;

        private readonly Queue<GameEvent> _pendingEvents = new Queue<GameEvent>();
        private readonly Dictionary<int, PawnView> _pawns = new Dictionary<int, PawnView>();
        private readonly Dictionary<int, Color> _playerColors = new Dictionary<int, Color>();
        private readonly List<string> _log = new List<string>();

        private IGameSession _session;
        private BoardView _board;
        private bool _playing;

        public IGameSession Session => _session;
        public IReadOnlyList<string> Log => _log;
        public string LastRejection { get; private set; }

        /// <summary>True while events are still being animated; the HUD waits before offering actions.</summary>
        public bool IsBusy => _playing || _pendingEvents.Count > 0;

        public Color ColorOf(int playerId) => _playerColors.TryGetValue(playerId, out Color color) ? color : Color.white;

        private void Start()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            IReadOnlyList<PlayerSeat> seats = LocalGameSetup.TakeSeats();
            var random = new Pcg32Random(unchecked((ulong)DateTime.UtcNow.Ticks));

            _session = new LocalGameSession(new GameEngine(data.Board, data.Rules, seats, random));
            _session.EventsPublished += OnEventsPublished;

            _board = BoardView.Create(transform, data);
            for (int i = 0; i < seats.Count; i++)
            {
                Color color = PlayerPalette.ForSeat(i);
                _playerColors[seats[i].Id] = color;
                _pawns[seats[i].Id] = PawnView.Create(transform, _board, i, seats.Count, color);
            }

            SetUpCamera();
            gameObject.AddComponent<PrototypeHud>().Bind(this);
            AddLog($"Sıra: {_session.State.CurrentPlayer.Name}");
        }

        private void OnDestroy()
        {
            if (_session != null) _session.EventsPublished -= OnEventsPublished;
        }

        public void Submit(GameCommand command)
        {
            CommandResult result = _session.Submit(command);
            LastRejection = result.IsAccepted ? null : $"İşlem reddedildi: {result.Reason}";
        }

        private void OnEventsPublished(IReadOnlyList<GameEvent> events)
        {
            foreach (GameEvent gameEvent in events) _pendingEvents.Enqueue(gameEvent);
            if (!_playing) StartCoroutine(PlayEvents());
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
