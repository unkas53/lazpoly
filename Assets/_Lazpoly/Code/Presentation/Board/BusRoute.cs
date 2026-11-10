using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The way the bus drives for one ride (the owner's wish, 2026-11-10: the bus takes the player from the bus station
    /// down to the road round the lawn and along it to the chosen square), in the board's frame. From its parking place
    /// at the stop it turns across its own corner square to the square's inner corner (the gate, the only way down that
    /// crosses no other square), rolls down onto the road, joins the painted line a little past the bend and follows it
    /// the way the squares are counted to the point nearest the destination (<see cref="Out"/>). <see cref="Home"/> is
    /// the way back: on round the road to the station's bend, up through the gate and into the parking place.
    /// Everything here is plain geometry: where the bus is, which way it points, how high it stands and how big it is
    /// after a given distance.
    /// </summary>
    public sealed class BusRoute
    {
        /// <summary>How far past the middle of the station's bend the bus joins the line, and how far before it the bus leaves it, as an angle of the bend (radians).</summary>
        public const float JoinAngle = 0.38f;

        /// <summary>How long the handles of a turn are, as a share of the turn's span.</summary>
        public const float Handle = 0.42f;

        /// <summary>The radius of the round the bus swings out of its parking place, until its nose points at the gate.</summary>
        public const float SwingRadius = 0.45f;

        /// <summary>The bus on the road is this much of its size at the stop, so it fits the asphalt (at 0.8 it hung over both edges).</summary>
        public const float RoadScale = 0.6f;

        /// <summary>The step from the squares down to the road is driven as a ramp: it starts this far inside the square's edge and ends this far out on the road.</summary>
        public const float RampOnSquare = 0.15f;
        public const float RampOnRoad = 0.28f;

        /// <summary>
        /// The stop stands on its own low base, the parked bus on it: the bus comes down from it over a ramp that starts
        /// this far inside the base's edge and ends this far outside it. The base's corners are round, by this share
        /// of its shorter side.
        /// </summary>
        public const float BaseRampInside = 0.3f;
        public const float BaseRampOutside = 0.1f;
        public const float BaseRound = 0.3f;

        /// <summary>How fast the bus drives, and the shortest and longest a drive takes at normal speed (a long one is driven faster).</summary>
        public const float Speed = 8f;
        public const float LeastSeconds = 1.6f;
        public const float MostSeconds = 4.2f;

        private readonly List<IPiece> _pieces = new List<IPiece>();
        private readonly List<float> _starts = new List<float>();
        private readonly Ground _ground;

        /// <summary>Where the squares end and the road begins, and the heights the bus stands at.</summary>
        public readonly struct Ground
        {
            /// <param name="stopCentre">The middle of the stop's base, in the board's frame.</param>
            /// <param name="stopHalf">Half the base's size along the board's x and z.</param>
            public Ground(float innerEdge, float squareTop, float roadTop, float stopRise, Vector3 stopCentre, Vector2 stopHalf)
            {
                InnerEdge = innerEdge;
                SquareTop = squareTop;
                RoadTop = roadTop;
                StopRise = stopRise;
                StopCentre = stopCentre;
                StopHalf = stopHalf;
            }

            /// <summary>How far from the board's middle the squares' inner edge runs.</summary>
            public float InnerEdge { get; }
            public float SquareTop { get; }
            public float RoadTop { get; }

            /// <summary>How much higher than its square the bus stands when parked, on the stop's own piece of road.</summary>
            public float StopRise { get; }
            public Vector3 StopCentre { get; }
            public Vector2 StopHalf { get; }

            /// <summary>How much of the stop's rise a point stands on: 1 on the stop's base, 0 off it, between on the ramp at its edge.</summary>
            public float OnStop(Vector3 point)
            {
                float round = BaseRound * Mathf.Min(StopHalf.x, StopHalf.y);
                float x = Mathf.Abs(point.x - StopCentre.x) - (StopHalf.x - round), z = Mathf.Abs(point.z - StopCentre.z) - (StopHalf.y - round);
                float outsideX = Mathf.Max(x, 0f), outsideZ = Mathf.Max(z, 0f);
                // How far outside the base's edge the point is (negative: inside), the corners rounded.
                float beyond = Mathf.Sqrt(outsideX * outsideX + outsideZ * outsideZ) + Mathf.Min(Mathf.Max(x, z), 0f) - round;
                return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((beyond + BaseRampInside) / (BaseRampInside + BaseRampOutside)));
            }

            /// <summary>How much of the way down to the road a point is: 0 on the squares, 1 on the road, between on the ramp.</summary>
            public float Down(Vector3 point)
            {
                float inside = InnerEdge - Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.z));
                return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((inside + RampOnSquare) / (RampOnSquare + RampOnRoad)));
            }
        }

        private BusRoute(Ground ground)
        {
            _ground = ground;
        }

        /// <summary>The whole drive's length.</summary>
        public float Length { get; private set; }

        /// <summary>How long a drive of <paramref name="length"/> takes at normal speed.</summary>
        public static float Seconds(float length) => Mathf.Clamp(length / Speed, LeastSeconds, MostSeconds);

        /// <summary>
        /// From the parking place to the point of the line nearest <paramref name="destination"/>.
        /// <paramref name="gate"/> is the station square's inner corner; the bus crosses it heading for the board's middle.
        /// </summary>
        public static BusRoute Out(Vector3 parked, Vector3 parkedHeading, Vector3 gate, RoadPath lane, Vector3 destination, Ground ground)
        {
            var route = new BusRoute(ground);
            Vector3 inward = -Flat(gate).normalized;
            float join = lane.Nearest(gate) + JoinAngle * lane.Bend;
            // Out of the parking place: round on a steady circle until the nose points at the gate, then straight to it.
            var swing = new Swing(Flat(parked), Flat(parkedHeading).normalized, Flat(gate), SwingRadius);
            Vector3 through = swing.Heading(swing.Length);
            if (Vector3.Dot(through, inward) <= 0.2f) through = inward;
            route.Add(swing);
            Vector3 swungTo = swing.Point(swing.Length);
            if ((Flat(gate) - swungTo).magnitude > 1e-4f) route.Add(new Turn(swungTo, swing.Heading(swing.Length), Flat(gate), through, Handle));
            route.Add(new Turn(Flat(gate), through, lane.Point(join), lane.Heading(join), Handle));
            route.Add(new Stretch(lane, join, lane.Ahead(join, lane.Nearest(destination))));
            return route;
        }

        /// <summary>From the point of the line nearest <paramref name="from"/> on round the road and back into the parking place.</summary>
        public static BusRoute Home(Vector3 parked, Vector3 parkedHeading, Vector3 gate, RoadPath lane, Vector3 from, Ground ground)
        {
            var route = new BusRoute(ground);
            Vector3 outward = Flat(gate).normalized;
            float start = lane.Nearest(from);
            float leave = lane.Nearest(gate) - JoinAngle * lane.Bend;
            route.Add(new Stretch(lane, start, lane.Ahead(start, leave)));
            route.Add(new Turn(lane.Point(leave), lane.Heading(leave), Flat(gate), outward, Handle));
            route.Add(new Turn(Flat(gate), outward, Flat(parked), Flat(parkedHeading).normalized, Handle));
            return route;
        }

        /// <summary>Where the bus is after <paramref name="distance"/>, standing on the ground there.</summary>
        public Vector3 Position(float distance)
        {
            Vector3 point = Find(ref distance).Point(distance);
            point.y = Mathf.Lerp(_ground.SquareTop, _ground.RoadTop, _ground.Down(point)) + _ground.StopRise * _ground.OnStop(point);
            return point;
        }

        /// <summary>Which way the bus points after <paramref name="distance"/> (a unit vector along the ground).</summary>
        public Vector3 Heading(float distance) => Find(ref distance).Heading(distance);

        /// <summary>How big the bus is after <paramref name="distance"/>: 1 at the stop, <see cref="RoadScale"/> on the road.</summary>
        public float Scale(float distance)
        {
            Vector3 point = Find(ref distance).Point(distance);
            return Mathf.Lerp(1f, RoadScale, _ground.Down(point));
        }

        private void Add(IPiece piece)
        {
            _starts.Add(Length);
            _pieces.Add(piece);
            Length += piece.Length;
        }

        /// <summary>The piece a distance falls on; the distance becomes the distance into that piece.</summary>
        private IPiece Find(ref float distance)
        {
            distance = Mathf.Clamp(distance, 0f, Length);
            int index = _pieces.Count - 1;
            while (index > 0 && distance < _starts[index]) index--;
            distance -= _starts[index];
            return _pieces[index];
        }

        private static Vector3 Flat(Vector3 vector) => new Vector3(vector.x, 0f, vector.z);

        private interface IPiece
        {
            float Length { get; }
            Vector3 Point(float distance);
            Vector3 Heading(float distance);
        }

        /// <summary>A part of the painted line, driven forwards.</summary>
        private sealed class Stretch : IPiece
        {
            private readonly RoadPath _lane;
            private readonly float _start;

            public Stretch(RoadPath lane, float start, float length)
            {
                _lane = lane;
                _start = start;
                Length = length;
            }

            public float Length { get; }
            public Vector3 Point(float distance) => _lane.Point(_start + distance);
            public Vector3 Heading(float distance) => _lane.Heading(_start + distance);
        }

        /// <summary>
        /// Round a circle from a point and heading, to the side the target lies on, until the heading points at the
        /// target; it ends there, the target straight ahead.
        /// </summary>
        private sealed class Swing : IPiece
        {
            private readonly Vector3 _centre, _spoke, _heading;
            private readonly float _radius, _side;

            public Swing(Vector3 from, Vector3 heading, Vector3 target, float radius)
            {
                Vector3 right = new Vector3(heading.z, 0f, -heading.x);
                Vector3 to = target - from;
                _side = Vector3.Dot(to, right) >= 0f ? 1f : -1f;
                _radius = Mathf.Max(0.01f, Mathf.Min(radius, to.magnitude * 0.45f));
                _centre = from + right * (_side * _radius);
                _spoke = from - _centre;
                _heading = heading;

                // The first angle at which the target lies straight ahead: found by stepping round, then halving the step.
                float angle = 0f;
                const float Step = Mathf.PI / 90f;
                while (angle < 2f * Mathf.PI && Off(angle + Step, target) * Off(0f, target) > 0f) angle += Step;
                float low = angle, high = angle + Step;
                for (int i = 0; i < 24; i++)
                {
                    float middle = (low + high) / 2f;
                    if (Off(middle, target) * Off(low, target) > 0f) low = middle;
                    else high = middle;
                }

                Length = _radius * (Off(0f, target) == 0f ? 0f : (low + high) / 2f);
            }

            public float Length { get; }

            public Vector3 Point(float distance) => _centre + Turned(_spoke, Mathf.Clamp(distance, 0f, Length) / _radius);

            public Vector3 Heading(float distance) => Turned(_heading, Mathf.Clamp(distance, 0f, Length) / _radius);

            /// <summary>How far off straight ahead the target lies after turning by an angle: its sign says which side.</summary>
            private float Off(float angle, Vector3 target)
            {
                Vector3 ahead = Turned(_heading, angle);
                Vector3 to = target - (_centre + Turned(_spoke, angle));
                float across = ahead.x * to.z - ahead.z * to.x;
                // Straight behind is no better than far off to the side.
                return Vector3.Dot(ahead, to) < 0f && Mathf.Abs(across) < 1e-4f ? -_side : across;
            }

            /// <summary>A vector turned about y towards this swing's side: clockwise seen from above for a turn to the right.</summary>
            private Vector3 Turned(Vector3 vector, float angle)
            {
                float sin = Mathf.Sin(angle) * _side, cos = Mathf.Cos(angle);
                return new Vector3(vector.x * cos + vector.z * sin, 0f, -vector.x * sin + vector.z * cos);
            }
        }

        /// <summary>A smooth turn from one point and heading to another (a cubic curve), measured along its length.</summary>
        private sealed class Turn : IPiece
        {
            private const int Steps = 32;
            private readonly Vector3 _a, _b, _c, _d;
            private readonly float[] _lengths = new float[Steps + 1];

            public Turn(Vector3 from, Vector3 fromHeading, Vector3 to, Vector3 toHeading, float handleShare)
            {
                float handle = handleShare * (to - from).magnitude;
                _a = from;
                _b = from + fromHeading * handle;
                _c = to - toHeading * handle;
                _d = to;
                Vector3 last = _a;
                for (int i = 1; i <= Steps; i++)
                {
                    Vector3 next = At(i / (float)Steps);
                    _lengths[i] = _lengths[i - 1] + (next - last).magnitude;
                    last = next;
                }
            }

            public float Length => _lengths[Steps];

            public Vector3 Point(float distance) => At(Share(distance));

            public Vector3 Heading(float distance)
            {
                float t = Share(distance), u = 1f - t;
                Vector3 along = 3f * u * u * (_b - _a) + 6f * u * t * (_c - _b) + 3f * t * t * (_d - _c);
                return along.sqrMagnitude > 1e-10f ? along.normalized : (_d - _a).normalized;
            }

            private Vector3 At(float t)
            {
                float u = 1f - t;
                return u * u * u * _a + 3f * u * u * t * _b + 3f * u * t * t * _c + t * t * t * _d;
            }

            /// <summary>The curve's own parameter for a distance along it.</summary>
            private float Share(float distance)
            {
                distance = Mathf.Clamp(distance, 0f, Length);
                int step = 1;
                while (step < Steps && _lengths[step] < distance) step++;
                float span = _lengths[step] - _lengths[step - 1];
                return (step - 1 + (span > 1e-8f ? (distance - _lengths[step - 1]) / span : 0f)) / Steps;
            }
        }
    }
}
