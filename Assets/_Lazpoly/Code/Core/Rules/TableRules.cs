using System;

namespace Lazpoly.Core.Rules
{
    /// <summary>
    /// The house rules picked when a game is set up: they change one game, not the board data, so
    /// they are sent with the game (and saved with it) instead of being part of the board checksum.
    /// </summary>
    public sealed class TableRules
    {
        /// <summary>The decision time a table gets when nobody picks one.</summary>
        public const int DefaultTurnSeconds = 15;

        /// <summary>Board starting money, no round limit, 15 seconds a decision.</summary>
        public static readonly TableRules Default = new TableRules(0, 0);

        /// <param name="startingMoney">0 keeps the board's starting money.</param>
        /// <param name="roundLimit">0 plays until one player is left.</param>
        /// <param name="turnSeconds">Seconds a player has for a decision (roll, buy, build) before the game makes it for them; 0 means no clock.</param>
        public TableRules(int startingMoney, int roundLimit, int turnSeconds = DefaultTurnSeconds)
        {
            if (startingMoney < 0) throw new ArgumentOutOfRangeException(nameof(startingMoney));
            if (roundLimit < 0) throw new ArgumentOutOfRangeException(nameof(roundLimit));
            if (turnSeconds < 0) throw new ArgumentOutOfRangeException(nameof(turnSeconds));
            StartingMoney = startingMoney;
            RoundLimit = roundLimit;
            TurnSeconds = turnSeconds;
        }

        public int StartingMoney { get; }
        public int RoundLimit { get; }

        /// <summary>
        /// Seconds a person has for a decision, in local and online games alike; selling to pay a debt and
        /// answering a trade get twice as long (see TurnClock). 0 means no clock. Tables saved before the
        /// decision clock kept a whole move's time here (60-120 s); the clock caps those.
        /// </summary>
        public int TurnSeconds { get; }

        public TableRules WithStartingMoney(int value) => new TableRules(value, RoundLimit, TurnSeconds);
        public TableRules WithRoundLimit(int value) => new TableRules(StartingMoney, value, TurnSeconds);
        public TableRules WithTurnSeconds(int value) => new TableRules(StartingMoney, RoundLimit, value);

        public bool SameAs(TableRules other) =>
            other != null && StartingMoney == other.StartingMoney && RoundLimit == other.RoundLimit && TurnSeconds == other.TurnSeconds;
    }
}
