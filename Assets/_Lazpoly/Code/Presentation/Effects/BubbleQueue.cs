using System.Collections.Generic;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The computer players' lines, one bubble on the table at a time. A line said while another is up
    /// waits its turn; the bubble on show is cut short once it has been read (<see cref="MinShowSeconds"/>)
    /// so the queue moves on. A player's newer line replaces the one it had waiting, and when too many wait
    /// the oldest goes: an auction where three players pass at once shows three short lines in a row,
    /// never four bubbles over each other and the square names.
    /// </summary>
    public sealed class BubbleQueue
    {
        /// <summary>Lines that may wait behind the one on show.</summary>
        public const int MaxWaiting = 3;

        /// <summary>How long a bubble stays at least when another line is waiting.</summary>
        public const float MinShowSeconds = 1.2f;

        private readonly List<(int PlayerId, string Text)> _waiting = new List<(int, string)>();

        /// <summary>Lines waiting behind the bubble on show.</summary>
        public int Waiting => _waiting.Count;

        public void Add(int playerId, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < _waiting.Count; i++)
            {
                if (_waiting[i].PlayerId != playerId) continue;
                _waiting[i] = (playerId, text);
                return;
            }

            if (_waiting.Count >= MaxWaiting) _waiting.RemoveAt(0);
            _waiting.Add((playerId, text));
        }

        /// <summary>Whether the next line should go up now: something waits and the table is free or the bubble on show has been read.</summary>
        public bool ShouldAdvance(bool showing, float shownFor) => _waiting.Count > 0 && (!showing || shownFor >= MinShowSeconds);

        /// <summary>Takes the next line off the queue.</summary>
        public (int PlayerId, string Text) Take()
        {
            (int PlayerId, string Text) next = _waiting[0];
            _waiting.RemoveAt(0);
            return next;
        }

        /// <summary>Drops a player's waiting line (the player went bankrupt).</summary>
        public void Forget(int playerId) => _waiting.RemoveAll(line => line.PlayerId == playerId);

        public void Clear() => _waiting.Clear();
    }
}
