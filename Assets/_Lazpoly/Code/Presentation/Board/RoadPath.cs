using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The painted line of the road round the lawn as a path the bus drives along (<see cref="Models.RoadSkin"/> draws
    /// the same line): a square with rounded corners about the board's middle, in the board's own frame on the ground
    /// (y is 0). Distances along it run the way the squares are counted, clockwise seen from above, from the middle of
    /// the near side (z = -<see cref="Reach"/>, heading towards -x), and wrap round after <see cref="Length"/>.
    /// </summary>
    public sealed class RoadPath
    {
        /// <param name="reach">How far from the middle the line runs along a straight side.</param>
        /// <param name="bend">The radius of its bend at a corner.</param>
        public RoadPath(float reach, float bend)
        {
            Reach = reach;
            Bend = Mathf.Clamp(bend, 0f, reach);
            Quarter = 2f * (Reach - Bend) + Mathf.PI / 2f * Bend;
            Length = 4f * Quarter;
        }

        public float Reach { get; }
        public float Bend { get; }

        /// <summary>From the middle of one side to the middle of the next: half a side, the bend, half a side.</summary>
        public float Quarter { get; }

        /// <summary>Once round.</summary>
        public float Length { get; }

        /// <summary>The point <paramref name="along"/> the line.</summary>
        public Vector3 Point(float along)
        {
            Locate(along, out Vector3 point, out _);
            return point;
        }

        /// <summary>Which way the line runs at <paramref name="along"/> (a unit vector).</summary>
        public Vector3 Heading(float along)
        {
            Locate(along, out _, out Vector3 heading);
            return heading;
        }

        /// <summary>How far forward along the line it is from <paramref name="from"/> to <paramref name="to"/>: never backwards, so up to once round.</summary>
        public float Ahead(float from, float to) => Mathf.Repeat(to - from, Length);

        /// <summary>How far along the line its point nearest to <paramref name="point"/> lies (the point's height is ignored).</summary>
        public float Nearest(Vector3 point)
        {
            float best = 0f, least = float.MaxValue;
            float x = point.x, z = point.z;
            for (int quarter = 0; quarter < 4; quarter++)
            {
                float along = NearestOnFirstQuarter(x, z, out float distance);
                if (distance < least)
                {
                    least = distance;
                    best = quarter * Quarter + along;
                }

                // Turn the point back a quarter, so the next quarter of the line lies where the first one did.
                float turned = -z;
                z = x;
                x = turned;
            }

            return Mathf.Repeat(best, Length);
        }

        private void Locate(float along, out Vector3 point, out Vector3 heading)
        {
            along = Mathf.Repeat(along, Length);
            int quarter = Mathf.Min(3, (int)(along / Quarter));
            float within = along - quarter * Quarter;
            float straight = Reach - Bend;
            float x, z, hx, hz;
            if (within <= straight)
            {
                x = -within; z = -Reach; hx = -1f; hz = 0f;
            }
            else if (within <= straight + Mathf.PI / 2f * Bend)
            {
                float angle = (within - straight) / Bend;
                x = -straight - Bend * Mathf.Sin(angle); z = -straight - Bend * Mathf.Cos(angle);
                hx = -Mathf.Cos(angle); hz = Mathf.Sin(angle);
            }
            else
            {
                x = -Reach; z = -straight + (within - straight - Mathf.PI / 2f * Bend); hx = 0f; hz = 1f;
            }

            // Each later quarter is the first one turned a quarter clockwise (seen from above): (x, z) goes to (z, -x).
            for (int i = 0; i < quarter; i++)
            {
                float turned = z; z = -x; x = turned;
                turned = hz; hz = -hx; hx = turned;
            }

            point = new Vector3(x, 0f, z);
            heading = new Vector3(hx, 0f, hz);
        }

        /// <summary>The first quarter's point nearest to (x, z): how far along the quarter it lies, and how far from the point.</summary>
        private float NearestOnFirstQuarter(float x, float z, out float distance)
        {
            float straight = Reach - Bend;
            // The half side along z = -Reach, from x = 0 to -straight.
            float onNear = Mathf.Clamp(-x, 0f, straight);
            float best = onNear;
            distance = Distance(x + onNear, z + Reach);

            // The bend about (-straight, -straight), from straight down (angle 0) to straight left (a quarter turn).
            if (Bend > 0f)
            {
                float angle = Mathf.Clamp(Mathf.Atan2(-(x + straight), -(z + straight)), 0f, Mathf.PI / 2f);
                float onBend = Distance(x + straight + Bend * Mathf.Sin(angle), z + straight + Bend * Mathf.Cos(angle));
                if (onBend < distance)
                {
                    distance = onBend;
                    best = straight + Bend * angle;
                }
            }

            // The half side along x = -Reach, from z = -straight up to 0.
            float onSide = Mathf.Clamp(z + straight, 0f, straight);
            float toSide = Distance(x + Reach, z + straight - onSide);
            if (toSide < distance)
            {
                distance = toSide;
                best = straight + Mathf.PI / 2f * Bend + onSide;
            }

            return best;
        }

        private static float Distance(float x, float z) => Mathf.Sqrt(x * x + z * z);
    }
}
