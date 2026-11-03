using Lazpoly.Core.Events;
using Lazpoly.Core.State;

namespace Lazpoly.Presentation.Audio
{
    /// <summary>Every sound the game plays. Each one is a clip in Resources/Lazpoly/Sfx (see <see cref="SoundCues.ResourcePath"/>).</summary>
    public enum SoundCue
    {
        None = 0,
        DiceShake,
        DiceLand,
        PawnStep,
        Buy,
        MoneyIn,
        MoneyOut,
        Build,
        Sell,
        UiClick,
        UiOpen,
        YourTurn,
        Achievement,
        Win,
        Bankrupt,
        Trade,
        PassStart,
        PropertySale,
        Hotel,
        TurnPass,
    }

    /// <summary>Which sound goes with which game event, and where each clip lives. No engine calls, so it is tested directly.</summary>
    public static class SoundCues
    {
        public const string Folder = "Lazpoly/Sfx/";

        /// <summary>
        /// The sound played when an event is shown, or <see cref="SoundCue.None"/>. One action that
        /// sends several events sounds once: a purchase plays on its payment, not on the owner change,
        /// and rent plays on the payer's side only. Dice, steps and the turn banner are timed by
        /// their animations instead; the hotel and the quiet turn-pass note are played by the scene too.
        /// </summary>
        public static SoundCue For(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case MoneyChangedEvent e: return ForMoney(e);
                case TradeProposedEvent _: return SoundCue.UiOpen;
                case TradeResolvedEvent e: return e.Outcome == TradeOutcome.Accepted ? SoundCue.Trade : SoundCue.None;
                case PlayerBankruptedEvent _: return SoundCue.Bankrupt;
                case GameEndedEvent _: return SoundCue.Win;
                case CardDrawnEvent _: return SoundCue.UiOpen;
                default: return SoundCue.None;
            }
        }

        private static SoundCue ForMoney(MoneyChangedEvent e)
        {
            switch (e.Reason)
            {
                case MoneyReason.PropertyPurchase: return SoundCue.Buy;
                case MoneyReason.Building: return SoundCue.Build;
                case MoneyReason.BuildingSale: return SoundCue.Sell;
                case MoneyReason.PropertySale: return SoundCue.PropertySale;
                case MoneyReason.PassedStart: return SoundCue.PassStart;
                case MoneyReason.Pot: return SoundCue.MoneyIn;
                case MoneyReason.Card:
                    return e.Delta > 0 ? SoundCue.MoneyIn : SoundCue.MoneyOut;
                case MoneyReason.Travel:
                    return SoundCue.MoneyOut;
                case MoneyReason.Tax:
                case MoneyReason.Rent:
                    return e.Delta < 0 ? SoundCue.MoneyOut : SoundCue.None;
                default:
                    // Trade payments sound with the handshake; bankruptcy with its own cue.
                    return SoundCue.None;
            }
        }

        /// <summary>The clip's path under a Resources folder, e.g. "Lazpoly/Sfx/dice_shake"; null for <see cref="SoundCue.None"/>.</summary>
        public static string ResourcePath(SoundCue cue) => cue == SoundCue.None ? null : Folder + FileName(cue);

        /// <summary>File name without extension: the cue's name in snake_case, as Tools/Audio/make_sfx_kenney.py writes it.</summary>
        public static string FileName(SoundCue cue)
        {
            string name = cue.ToString();
            var result = new System.Text.StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsUpper(c) && i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }

            return result.ToString();
        }

        /// <summary>
        /// How many takes of a cue Tools/Audio/make_sfx_kenney.py writes: the first is <see cref="FileName"/>, the
        /// others add "_2", "_3"... The frequent sounds have several, so a long game does not repeat one sample.
        /// </summary>
        public static int Variants(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.PawnStep: return 4;
                case SoundCue.DiceShake:
                case SoundCue.DiceLand:
                case SoundCue.UiClick: return 3;
                case SoundCue.MoneyIn:
                case SoundCue.MoneyOut:
                case SoundCue.Build: return 2;
                default: return cue == SoundCue.None ? 0 : 1;
            }
        }

        /// <summary>The resource path of a cue's take <paramref name="index"/> (0 = the first, <see cref="ResourcePath"/>).</summary>
        public static string VariantPath(SoundCue cue, int index) =>
            cue == SoundCue.None ? null : index <= 0 ? ResourcePath(cue) : ResourcePath(cue) + "_" + (index + 1);

        /// <summary>
        /// Which take plays next, from a random number in [0, 1): any take but the one that played last,
        /// so two steps in a row never sound the same.
        /// </summary>
        public static int PickVariant(int count, int last, float random01)
        {
            if (count <= 1) return 0;
            bool hasLast = last >= 0 && last < count;
            int choices = hasLast ? count - 1 : count;
            int pick = System.Math.Min((int)(random01 * choices), choices - 1);
            return hasLast && pick >= last ? pick + 1 : pick;
        }

        /// <summary>
        /// How much a cue's pitch may wander each time it plays, so repeated sounds (steps, clicks,
        /// coins) do not sound mechanical. Musical cues stay in tune.
        /// </summary>
        public static float PitchJitter(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.PawnStep: return 0.08f;
                case SoundCue.DiceShake:
                case SoundCue.DiceLand:
                case SoundCue.UiClick:
                    return 0.04f;
                case SoundCue.MoneyIn:
                case SoundCue.MoneyOut:
                case SoundCue.Build:
                case SoundCue.Sell:
                    return 0.015f; // In tune with the music: a cent or two of colour, not a different note.
                default:
                    return 0f;
            }
        }

        /// <summary>Relative loudness, so frequent small sounds sit under the big moments.</summary>
        public static float Volume(SoundCue cue)
        {
            switch (cue)
            {
                case SoundCue.PawnStep: return 0.4f;
                case SoundCue.UiClick: return 0.4f;
                case SoundCue.UiOpen: return 0.4f;
                case SoundCue.TurnPass: return 0.45f;
                default: return 0.75f;
            }
        }
    }
}
