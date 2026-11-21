using Lazpoly.Core.Events;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Effects;

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
        Firework,

        // The bus's ride from the station (the owner's wish, 2026-11-11); written by Tools/Audio/make_sfx_bus.py.
        BusStart,
        BusRev,
        BusHorn,
        BusBrake,

        // The jail's iron gate swinging open and shutting behind a piece (the owner's wish, 2026-11-12); Kenney recordings.
        JailOpen,
        JailShut,

        // A building leaving its square (the owner's wish, 2026-11-18: professional sounds, like the real thing): sold, the
        // till and its coins; torn down, the collapse and its rubble. Kenney recordings, timed to the animation.
        BuildingSold,
        BuildingFall,

        // The missile card (the owner's wish, 2026-11-21: sounds to go with its animation): the hatch and the rocket taking
        // off, the blast and its rubble where it comes down, and the knock of one that bursts on a shield.
        MissileLaunch,
        MissileBlast,
        MissileBlock,

        // The interface (the owner's wish, 2026-11-21: button sounds and the like): the pointer coming onto a button, a
        // window closing, and the way to the table and back to the menu. Kenney recordings.
        UiHover,
        UiClose,
        UiStart,
        UiLeave,
    }

    /// <summary>Which sound goes with which game event, and where each clip lives. No engine calls, so it is tested directly.</summary>
    public static class SoundCues
    {
        public const string Folder = "Lazpoly/Sfx/";

        /// <summary>
        /// The sound played when an event is shown, or <see cref="SoundCue.None"/>. One action that
        /// sends several events sounds once: a purchase plays on its payment, not on the owner change,
        /// and rent plays on the payer's side only. Dice, steps and the turn banner are timed by
        /// their animations instead; the quiet turn-pass note is played by the scene too,
        /// the bus's sounds by its ride (<see cref="Pawns.BusTrip"/>), the jail's gate by the piece that goes
        /// through it (<see cref="ForJailGate"/>), and a building's going up and its leaving by their animations
        /// (<see cref="ForRising"/>, <see cref="ForLeaving"/>): building is paid for with the sound of money, and the
        /// hammer is heard when the building rises, after the payment has flown.
        /// </summary>
        public static SoundCue For(GameEvent gameEvent)
        {
            switch (gameEvent)
            {
                case MoneyChangedEvent e: return ForMoney(e);
                case TradeProposedEvent _: return SoundCue.UiOpen;
                case CupHostedEvent _: return SoundCue.Achievement;
                case TradeResolvedEvent e: return e.Outcome == TradeOutcome.Accepted ? SoundCue.Trade : SoundCue.None;
                case PlayerBankruptedEvent _: return SoundCue.Bankrupt;
                case GameEndedEvent _: return SoundCue.Win;
                case CardDrawnEvent _: return SoundCue.UiOpen;
                case CardPlayedEvent _: return SoundCue.UiOpen;
                case ShieldChangedEvent e: return e.Blocked ? SoundCue.Achievement : e.Raised ? SoundCue.Build : SoundCue.None;
                default: return SoundCue.None;
            }
        }

        private static SoundCue ForMoney(MoneyChangedEvent e)
        {
            switch (e.Reason)
            {
                case MoneyReason.PropertyPurchase: return SoundCue.Buy;
                case MoneyReason.Takeover: return e.Delta < 0 ? SoundCue.Buy : SoundCue.MoneyIn;
                case MoneyReason.BuildingSale: return SoundCue.Sell;
                case MoneyReason.PropertySale: return SoundCue.PropertySale;
                case MoneyReason.PassedStart: return SoundCue.PassStart;
                case MoneyReason.Pot: return SoundCue.MoneyIn;
                case MoneyReason.Card:
                case MoneyReason.Fund:
                    return e.Delta > 0 ? SoundCue.MoneyIn : SoundCue.MoneyOut;
                case MoneyReason.Building:
                case MoneyReason.Travel:
                case MoneyReason.Cup:
                case MoneyReason.Audit:
                    return SoundCue.MoneyOut;
                case MoneyReason.Tax:
                case MoneyReason.Rent:
                    return e.Delta < 0 ? SoundCue.MoneyOut : SoundCue.None;
                default:
                    // Trade payments sound with the handshake; bankruptcy with its own cue.
                    return SoundCue.None;
            }
        }

        /// <summary>The jail's gate as a piece goes through it: the bolt and the hinges as it opens, the strike and the lock as it shuts.</summary>
        public static SoundCue ForJailGate(bool opening) => opening ? SoundCue.JailOpen : SoundCue.JailShut;

        /// <summary>
        /// A building going up, heard from the moment it starts to (the owner's wish, 2026-11-18: in time with the
        /// hammer): the two blows it rises in (<see cref="RisingMotion.KnockGap"/>), and for a hotel the blows and
        /// then its tune as it stands.
        /// </summary>
        public static SoundCue ForRising(bool grand) => grand ? SoundCue.Hotel : SoundCue.Build;

        /// <summary>
        /// A building leaving its square, heard from the moment it starts to go (the clips are cut to the animation,
        /// <see cref="LeavingMotion"/>): sold, the till rings as it pops and coins fall as the dollar signs rise; torn
        /// down, it cracks at the jolt, crumbles, crashes down and its rubble settles.
        /// </summary>
        public static SoundCue ForLeaving(Leaving how)
        {
            switch (how)
            {
                case Leaving.Demolished: return SoundCue.BuildingFall;
                // What a missile blows up is heard in the missile's own blast (ForMissileHit), rubble and all.
                case Leaving.Blasted: return SoundCue.None;
                default: return SoundCue.BuildingSold;
            }
        }

        /// <summary>A scene change, heard as it is asked for: the way to the table, or the way back to the menu.</summary>
        public static SoundCue ForSceneChange(string sceneName) => sceneName == "Game" ? SoundCue.UiStart : SoundCue.UiLeave;

        /// <summary>A missile coming down: the blast and its rubble, or the knock and hiss of one that bursts on a shield.</summary>
        public static SoundCue ForMissileHit(bool blocked) => blocked ? SoundCue.MissileBlock : SoundCue.MissileBlast;

        /// <summary>Buildings that start to leave the same way within this time of each other are heard once: a street sold in one go rings one till.</summary>
        public const float LeavingGapSeconds = 0.5f;

        /// <summary>Whether a leaving that starts <paramref name="sinceLast"/> seconds after the last one heard sounds too.</summary>
        public static bool LeavingIsHeard(bool sameAsLast, float sinceLast) => !sameAsLast || sinceLast >= LeavingGapSeconds;

        /// <summary>
        /// Whether an event's own sound (<see cref="For"/>) is left out because a building sold
        /// <paramref name="sinceSoldLeaving"/> seconds ago is being heard: the money of a sale, whose knock and coins
        /// the till and its coins take the place of. A sale with nothing on the board to leave keeps its old sound.
        /// </summary>
        public static bool HeardInLeaving(GameEvent gameEvent, float sinceSoldLeaving) =>
            gameEvent is MoneyChangedEvent money && sinceSoldLeaving >= 0f && sinceSoldLeaving < LeavingCoversSeconds
            && (money.Reason == MoneyReason.BuildingSale || money.Reason == MoneyReason.PropertySale);

        /// <summary>How long after a sold building starts to go its sound stands for the sale's money too: the length of the till and its coins.</summary>
        public const float LeavingCoversSeconds = 1.5f;

        /// <summary>The clip's path under a Resources folder, e.g. "Lazpoly/Sfx/dice_shake"; null for <see cref="SoundCue.None"/>.</summary>
        public static string ResourcePath(SoundCue cue) => cue == SoundCue.None ? null : Folder + FileName(cue);

        /// <summary>File name without extension: the cue's name in snake_case, as Tools/Audio/make_sfx_kenney.py (and make_sfx_bus.py) writes it.</summary>
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
                case SoundCue.UiHover:
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
                case SoundCue.UiHover:
                    return 0.04f;
                case SoundCue.MoneyIn:
                case SoundCue.MoneyOut:
                case SoundCue.Build:
                case SoundCue.Sell:
                    return 0.015f; // In tune with the music: a cent or two of colour, not a different note.
                case SoundCue.BuildingFall:
                    return 0.03f; // No two walls come down alike.
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
                case SoundCue.UiClose: return 0.35f; // A window going is quieter than one coming.
                case SoundCue.UiHover: return 0.3f; // Heard many times a minute: barely there.
                case SoundCue.UiStart: return 0.6f;
                case SoundCue.UiLeave: return 0.5f;
                case SoundCue.TurnPass: return 0.45f;
                case SoundCue.Firework: return 0.55f; // Several go off together over a group.
                case SoundCue.BusRev: return 0.5f; // Long, and under whatever else happens on the table.
                case SoundCue.BusStart: return 0.6f;
                case SoundCue.BusHorn: return 0.55f;
                case SoundCue.BusBrake: return 0.5f;
                case SoundCue.JailOpen: return 0.55f; // The bolt and the hinges: a detail under the piece's words.
                case SoundCue.JailShut: return 0.65f;
                case SoundCue.BuildingSold: return 0.7f; // The till's bell is bright: just under the table's big moments.
                case SoundCue.BuildingFall: return 0.8f; // A collapse is the loudest thing on the table while it lasts.
                case SoundCue.MissileLaunch: return 0.7f;
                case SoundCue.MissileBlast: return 0.9f; // The one explosion of the game: nothing is louder.
                case SoundCue.MissileBlock: return 0.7f;
                default: return 0.75f;
            }
        }
    }
}
