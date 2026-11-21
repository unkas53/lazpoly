using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// The way a missile goes from its player's hatch to the town it is fired at (the owner's card, 2026-11-21: "rogar
    /// kapağından bir füze havalansın ve seçtiği şehire düşüp patlatsın"): one smooth curve that leaves the hatch
    /// straight up, leans over the table and comes down steeply on the town. It starts out of sight inside the hatch,
    /// lifts off slowly and is at its fastest as it strikes. Pure arithmetic in the board's own lengths, so it is tested
    /// without a table.
    /// </summary>
    public readonly struct MissileFlight
    {
        /// <summary>How long the hatch's lid has to swing open before the missile moves, and how long the flight then takes to the hit.</summary>
        public const float OpenSeconds = 0.3f;
        public const float Seconds = 1.7f;

        /// <summary>How fast it gets going: its way along the curve is the time to this power, slow off the ground and fastest at the hit.</summary>
        public const float Thrust = 1.6f;

        /// <summary>How far up the curve is pulled over the hatch and over the town, in tile widths, and how far short of the town it starts to come down, as a share of the way.</summary>
        public const float Climb = 2.9f;
        public const float Dive = 1.9f;
        public const float Short = 0.22f;

        /// <summary>How deep under the hatch's mouth its middle starts, in its own lengths: all of it out of sight.</summary>
        public const float Hidden = 0.62f;

        private readonly Vector3 _from;
        private readonly Vector3 _over;
        private readonly Vector3 _above;
        private readonly Vector3 _to;

        /// <summary>
        /// A flight from the hatch whose mouth is at <paramref name="mouth"/> to <paramref name="target"/>, for a missile
        /// <paramref name="length"/> long on a board whose tiles are <paramref name="tile"/> wide.
        /// </summary>
        public MissileFlight(Vector3 mouth, Vector3 target, float tile, float length)
        {
            Length = length;
            _from = mouth + Vector3.down * (length * Hidden);
            _over = mouth + Vector3.up * (Climb * tile);
            Vector3 way = target - mouth;
            way.y = 0f;
            _above = target - way * Short + Vector3.up * (Dive * tile);
            _to = target;
        }

        /// <summary>The missile from tail to nose.</summary>
        public float Length { get; }

        /// <summary>Where it starts, inside the hatch, and where it strikes.</summary>
        public Vector3 From => _from;
        public Vector3 To => _to;

        /// <summary>How far along its curve it is <paramref name="time"/> seconds after it starts to move: 0 before, 1 from the hit on.</summary>
        public static float Along(float time) => Mathf.Pow(Mathf.Clamp01(time / Seconds), Thrust);

        /// <summary>Where its middle is <paramref name="time"/> seconds after it starts to move.</summary>
        public Vector3 Position(float time) => Point(Along(time));

        /// <summary>The way its nose points at <paramref name="time"/>: along the curve, straight up at the start.</summary>
        public Vector3 Heading(float time)
        {
            float u = Along(time);
            float v = 1f - u;
            Vector3 heading = 3f * v * v * (_over - _from) + 6f * v * u * (_above - _over) + 3f * u * u * (_to - _above);
            return heading.sqrMagnitude > 1e-10f ? heading.normalized : Vector3.up;
        }

        /// <summary>Where its tail is at <paramref name="time"/>: where its fire and its smoke come out.</summary>
        public Vector3 Tail(float time) => Position(time) - Heading(time) * (Length * 0.5f);

        /// <summary>Where its nose is at <paramref name="time"/>.</summary>
        public Vector3 Nose(float time) => Position(time) + Heading(time) * (Length * 0.5f);

        /// <summary>The highest its middle gets on the way.</summary>
        public float Top()
        {
            float top = Mathf.Max(_from.y, _to.y);
            for (int i = 1; i < 64; i++) top = Mathf.Max(top, Point(i / 64f).y);
            return top;
        }

        private Vector3 Point(float u)
        {
            float v = 1f - u;
            return v * v * v * _from + 3f * v * v * u * _over + 3f * v * u * u * _above + u * u * u * _to;
        }
    }
}
