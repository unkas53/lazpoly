using System;
using System.Collections.Generic;
using Lazpoly.Core.Commands;
using Lazpoly.Core.Engine;
using Lazpoly.Core.Events;

namespace Lazpoly.App.Session
{
    /// <summary>
    /// The last few things that happened in a game, as short lines, so a problem report shows how
    /// the game got where it is. Keeps only the newest <see cref="Capacity"/> entries.
    /// </summary>
    public sealed class SessionTrail
    {
        public const int DefaultCapacity = 20;

        private readonly Queue<string> _lines = new Queue<string>();
        private int _sequence;

        public SessionTrail(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
        }

        public int Capacity { get; }

        /// <summary>Oldest first.</summary>
        public IReadOnlyCollection<string> Lines => _lines;

        public void Add(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            _sequence++;
            _lines.Enqueue($"{_sequence,4}  {line.Trim()}");
            while (_lines.Count > Capacity) _lines.Dequeue();
        }

        /// <summary>A command this machine sent, and whether the rules took it.</summary>
        public void Command(GameCommand command, RejectionReason outcome = RejectionReason.None)
        {
            if (command == null) return;
            string verdict = outcome == RejectionReason.None ? "ok" : $"REJECTED {outcome}";
            Add($"> {Describe(command)} -> {verdict}");
        }

        /// <summary>One batch of events as the host published them: the kinds, counted.</summary>
        public void Events(IReadOnlyList<GameEvent> events)
        {
            if (events == null || events.Count == 0) return;
            var kinds = new List<string>();
            string last = null;
            int run = 0;
            foreach (GameEvent gameEvent in events)
            {
                string kind = gameEvent.GetType().Name.Replace("Event", string.Empty);
                if (kind == last)
                {
                    run++;
                    continue;
                }

                if (last != null) kinds.Add(run > 1 ? $"{last}x{run}" : last);
                last = kind;
                run = 1;
            }

            if (last != null) kinds.Add(run > 1 ? $"{last}x{run}" : last);
            Add($"< {string.Join(" ", kinds)}");
        }

        public static string Describe(GameCommand command)
        {
            string name = command.GetType().Name.Replace("Command", string.Empty);
            switch (command)
            {
                case SquareCommand c: return $"{name}(p{c.PlayerId}, sq{c.SquareIndex})";
                case PlaceBidCommand c: return $"{name}(p{c.PlayerId}, {c.Amount})";
                case ChooseDiceCommand c: return $"{name}(p{c.PlayerId}, {c.Option})";
                case ProposeTradeCommand c when c.Offer != null:
                    return $"{name}(p{c.Offer.FromPlayerId}->p{c.Offer.ToPlayerId}, give {c.Offer.GiveMoney}+{c.Offer.GiveSquares.Count}sq, take {c.Offer.TakeMoney}+{c.Offer.TakeSquares.Count}sq)";
                default: return $"{name}(p{command.PlayerId})";
            }
        }
    }
}
