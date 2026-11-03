using System;

namespace Lazpoly.Core.Rules
{
    /// <summary>
    /// The house rules picked when a game is set up: they change one game, not the board data, so
    /// they are sent with the game (and saved with it) instead of being part of the board checksum.
    /// </summary>
    public sealed class TableRules
    {
        /// <summary>The turn time an online table gets when nobody picks one.</summary>
        public const int DefaultTurnSeconds = 90;

        /// <summary>Board starting money, no round limit, 90-second turns.</summary>
        public static readonly TableRules Default = new TableRules(0, 0);

        /// <param name="startingMoney">0 keeps the board's starting money.</param>
        /// <param name="roundLimit">0 plays until one player is left.</param>
        /// <param name="turnSeconds">Online: seconds a player has for a move before the host moves for them; 0 means no clock.</param>
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

        /// <summary>Seconds per move in an online game; 0 means unlimited. Local games never run a clock.</summary>
        public int TurnSeconds { get; }

        public TableRules WithStartingMoney(int value) => new TableRules(value, RoundLimit, TurnSeconds);
        public TableRules WithRoundLimit(int value) => new TableRules(StartingMoney, value, TurnSeconds);
        public TableRules WithTurnSeconds(int value) => new TableRules(StartingMoney, RoundLimit, value);

        public bool SameAs(TableRules other) =>
            other != null && StartingMoney == other.StartingMoney && RoundLimit == other.RoundLimit && TurnSeconds == other.TurnSeconds;
    }
}
