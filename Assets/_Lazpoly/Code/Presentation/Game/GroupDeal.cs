using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Core.State;

using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The card in the middle when a player comes to hold every town of a colour group, the owner's wish (Faz 26): "X yoğun
    /// bir mesai harcayarak A, B ve C arasında ticaret anlaşması imzaladı", up for <see cref="Seconds"/> while the table
    /// waits. Whether a change of owner completes a group is worked out from the owners as shown on the table, so a trade
    /// that hands over a whole group shows the card once, on its last town. Holiday resorts have their own rule and no card.
    /// When the card is gone, fireworks go up over the group's towns (<see cref="Lazpoly.Presentation.Effects.FireworkShow"/>) and only then
    /// do the towns' rents on the board change to the new ones (Faz 27); until then they show <see cref="RentBefore"/>.
    /// Pure, so it is tested without a scene.
    /// </summary>
    public static class GroupDeal
    {
        public const float Seconds = 4f;

        /// <summary>
        /// Whether giving <paramref name="square"/> to <paramref name="newOwner"/> makes them hold every town of its colour
        /// group, the other towns' owners read from <paramref name="owners"/> (square → owner, as shown before the change).
        /// </summary>
        public static bool Completes(BoardDefinition board, IReadOnlyDictionary<int, int> owners, int square, int newOwner)
        {
            if (board == null || owners == null || square < 0 || square >= board.Count || newOwner == GameState.NoOwner) return false;
            SquareDefinition town = board[square];
            if (town.Type != SquareType.Property || town.IsResort || town.GroupId == 0) return false;
            if (owners.TryGetValue(square, out int before) && before == newOwner) return false;
            foreach (int other in GameRules.GroupSquares(board, town.GroupId))
            {
                if (other == square) continue;
                if (!owners.TryGetValue(other, out int owner) || owner != newOwner) return false;
            }

            return true;
        }

        /// <summary>The names of the towns of <paramref name="groupId"/>, in board order, in the current language.</summary>
        public static List<string> TownNames(BoardDefinition board, int groupId)
        {
            var names = new List<string>();
            foreach (int square in GameRules.GroupSquares(board, groupId)) names.Add(L(board[square].Name));
            return names;
        }

        /// <summary>"A", "A ve B", "A, B ve C".</summary>
        public static string NameList(IReadOnlyList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            string head = string.Join(", ", ListHead(names));
            string last = names[names.Count - 1];
            return F($"{head} ve {last}");
        }

        /// <summary>The card's sentence: "<paramref name="player"/> yoğun bir mesai harcayarak (the towns) arasında ticaret anlaşması imzaladı".</summary>
        public static string Text(string player, IReadOnlyList<string> towns)
        {
            string list = NameList(towns);
            return F($"{player} yoğun bir mesai harcayarak {list} arasında ticaret anlaşması imzaladı");
        }

        /// <summary>The line under it: the towns' rent, houses and hotel too, is now <paramref name="multiplier"/> times.</summary>
        public static string RentText(int multiplier) => F($"Bu şehirlerin kiraları artık {multiplier} katı.");

        /// <summary>
        /// The rent a town of a just completed group shows until the fireworks over it are over (the owner's wish, Faz 27):
        /// its level's rent without the group's multiplier, the cup's raise included. The board then changes to the new rent.
        /// </summary>
        public static int RentBefore(BoardDefinition board, GameState state, int square) =>
            GameRules.WithCup(state, square, GameRules.LevelRent(board[square], state.GetBuildingLevel(square)));

        private static IEnumerable<string> ListHead(IReadOnlyList<string> names)
        {
            for (int i = 0; i < names.Count - 1; i++) yield return names[i];
        }
    }
}
