namespace Lazpoly.Core.Random
{
    /// <summary>
    /// Deterministic random source used by game rules (dice, card shuffles, AI tie-breaks).
    /// Only the authoritative host owns an instance; clients receive results through events.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>Returns the next uniformly distributed 32-bit value.</summary>
        uint NextUInt();

        /// <summary>Returns a uniformly distributed integer in [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);
    }
}
