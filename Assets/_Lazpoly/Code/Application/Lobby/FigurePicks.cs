using System;
using System.Collections.Generic;

namespace Lazpoly.App.Lobby
{
    /// <summary>
    /// Settles who plays which character so that no two players share one. The seats are taken in order: a seat
    /// keeps the character it asked for unless it is unknown or an earlier seat already has it, and the seats left
    /// without one get the free characters in the catalogue's order. Every machine works it out the same way from
    /// the same picks, so the lobby shows what the game will use.
    /// </summary>
    public static class FigurePicks
    {
        /// <param name="wanted">Each seat's pick in seat order; null or empty for no pick.</param>
        /// <param name="catalog">The characters there are, in the order free ones are handed out.</param>
        /// <returns>One character per seat; a character repeats only when there are more seats than characters.</returns>
        public static IReadOnlyList<string> Resolve(IReadOnlyList<string> wanted, IReadOnlyList<string> catalog)
        {
            if (wanted == null) throw new ArgumentNullException(nameof(wanted));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var result = new string[wanted.Count];
            var taken = new HashSet<string>(StringComparer.Ordinal);
            var known = new HashSet<string>(catalog, StringComparer.Ordinal);
            for (int i = 0; i < wanted.Count; i++)
            {
                string pick = wanted[i];
                if (!string.IsNullOrEmpty(pick) && known.Contains(pick) && taken.Add(pick)) result[i] = pick;
            }

            int next = 0;
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != null) continue;
                result[i] = NextFree(catalog, taken, ref next);
                if (result[i] != null) taken.Add(result[i]);
                else if (catalog.Count > 0) result[i] = catalog[i % catalog.Count]; // More seats than characters.
                else result[i] = string.Empty;
            }

            return result;
        }

        /// <summary>Whether <paramref name="figure"/> is free for the seat at <paramref name="seat"/>: no other seat holds it.</summary>
        public static bool IsFree(IReadOnlyList<string> held, int seat, string figure)
        {
            if (held == null) throw new ArgumentNullException(nameof(held));
            for (int i = 0; i < held.Count; i++)
            {
                if (i != seat && string.Equals(held[i], figure, StringComparison.Ordinal)) return false;
            }

            return true;
        }

        private static string NextFree(IReadOnlyList<string> catalog, HashSet<string> taken, ref int next)
        {
            while (next < catalog.Count)
            {
                string candidate = catalog[next++];
                if (!taken.Contains(candidate)) return candidate;
            }

            return null;
        }
    }
}
