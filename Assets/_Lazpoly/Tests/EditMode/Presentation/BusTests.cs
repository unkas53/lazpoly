using System.IO;
using System.Linq;
using Lazpoly.Core.Board;
using Lazpoly.Presentation.Data;
using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Pawns;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// The bus station's bus takes a player along the road round the lawn (the owner's wish, 2026-11-10): the road's line
    /// as a path, the bus's way out and home, the bus on the board and the piece's hop in and out.
    /// </summary>
    public class BusTests
    {
        [Test]
        public void TheRoadsLineIsARoundedSquareDrivenTheWayTheSquaresAreCounted()
        {
            var lane = new RoadPath(6f, 2f);
            Assert.AreEqual(2f * 4f + Mathf.PI, lane.Quarter, 1e-4f, "half a side, the bend, half a side");
            Assert.AreEqual(4f * lane.Quarter, lane.Length, 1e-4f);

            // From the middle of the near side towards -x, then up the left side, along the far side, down the right one.
            AssertNear(new Vector3(0f, 0f, -6f), lane.Point(0f));
            AssertNear(Vector3.left, lane.Heading(0f));
            AssertNear(new Vector3(-6f, 0f, 0f), lane.Point(lane.Quarter));
            AssertNear(Vector3.forward, lane.Heading(lane.Quarter));
            AssertNear(new Vector3(0f, 0f, 6f), lane.Point(2f * lane.Quarter));
            AssertNear(Vector3.right, lane.Heading(2f * lane.Quarter));
            AssertNear(new Vector3(6f, 0f, 0f), lane.Point(3f * lane.Quarter));
            AssertNear(Vector3.back, lane.Heading(3f * lane.Quarter));
            float diagonal = 4f + 2f * Mathf.Sqrt(0.5f);
            AssertNear(new Vector3(-diagonal, 0f, -diagonal), lane.Point(4f + Mathf.PI / 2f), "the bend's middle lies on the diagonal");
            AssertNear(lane.Point(1f), lane.Point(lane.Length + 1f), "it wraps round");
            AssertNear(lane.Point(lane.Length - 1f), lane.Point(-1f));

            const int Steps = 2000;
            Vector3 last = lane.Point(0f);
            for (int i = 1; i <= Steps; i++)
            {
                float along = i * lane.Length / Steps;
                Vector3 point = lane.Point(along), heading = lane.Heading(along);
                Assert.AreEqual(lane.Length / Steps, (point - last).magnitude, 1e-3f, "even steps: no jump, no stall");
                Assert.AreEqual(1f, heading.magnitude, 1e-4f);
                Assert.AreEqual(0f, point.y, 1e-6f);
                // The point of the line nearest to a point beside it is the point itself.
                var aside = new Vector3(heading.z, 0f, -heading.x);
                foreach (float off in new[] { -0.5f, 0f, 0.5f })
                {
                    float found = lane.Nearest(point + aside * off);
                    float apart = Mathf.Min(lane.Ahead(found, along), lane.Ahead(along, found));
                    Assert.Less(apart, 2e-3f, $"nearest at {along}, {off} aside");
                }

                last = point;
            }

            Assert.AreEqual(lane.Length - 0.5f, lane.Ahead(1f, 0.5f), 1e-4f, "never backwards: the long way round");
            Assert.AreEqual(0.5f, lane.Ahead(lane.Length - 0.25f, 0.25f), 1e-4f);
        }

        [Test]
        public void TheBusDrivesFromItsStopDownToTheRoadAndAlongItToEverySquare()
        {
            BoardData data = BoardDataLoader.LoadFromResources();
            var holder = new GameObject("Board Test");
            try
            {
                BoardView board = BoardView.Create(holder.transform, data);
                BusView bus = board.Bus;
                Assert.IsNotNull(bus, "the stop's bus is a model of its own (run Tools/Art/make_station.py)");
                int station = board.StationSquare;
                Assert.AreEqual(SquareType.Travel, data.Board[station].Type);
                RoadPath lane = board.Lane;
                float inner = board.Inner, strip = (1f - BoardView.LawnShare) / 2f;
                Assert.AreEqual(RoadSkin.LineReach(strip) * inner / 2f, lane.Reach, 1e-4f, "the bus drives along the painted line");
                Assert.AreEqual(RoadSkin.LineBend(strip) * inner / 2f, lane.Bend, 1e-4f);

                // The squares lie along the line in their own order: once round takes in every square once.
                float round = 0f;
                for (int square = 0; square < board.Layout.SquareCount; square++)
                {
                    float step = lane.Ahead(lane.Nearest(board.Layout.SquareCenter(square)), lane.Nearest(board.Layout.SquareCenter(square + 1)));
                    Assert.That(step, Is.InRange(0.2f, board.Layout.Depth), $"from square {square} to the next");
                    round += step;
                }

                Assert.AreEqual(lane.Length, round, 1e-2f);

                // Parked where the diorama showed it: on the stop's piece of road, its nose along the stop's -X.
                Transform stop = holder.GetComponentsInChildren<Transform>(true).Single(child => child.name == $"Scene {SquareType.Travel}");
                Assert.IsFalse(bus.Away);
                AssertNear(BusModel.ParkedAt, stop.InverseTransformPoint(bus.transform.position), "on its parking place");
                AssertNear(-stop.right, bus.transform.forward);
                Assert.AreEqual(stop.lossyScale.x, bus.transform.lossyScale.x, 1e-4f, "as big as it was in the diorama");
                Assert.AreEqual(BusModel.Height * bus.Size, bus.Roof.y - bus.transform.position.y, 1e-4f);

                float edge = board.Layout.HalfSize - board.Layout.Depth / 2f;
                Vector3 corner = board.Layout.SquareCenter(station);
                AssertNear(new Vector3(Mathf.Sign(corner.x) * edge, 0f, Mathf.Sign(corner.z) * edge), board.BusGate, "the gate is the station square's inner corner");
                float parkedHeight = bus.ParkedSpot.y;
                Assert.AreEqual(board.SurfaceY + bus.StopRise, parkedHeight, 1e-4f);
                Assert.Greater(lane.Reach + BusModel.Width * bus.Size * BusRoute.RoadScale / 2f, 0f);
                Assert.Less(lane.Reach + BusModel.Width * bus.Size * BusRoute.RoadScale / 2f, edge, "on the road the bus stays off the squares");

                for (int square = 0; square < board.Layout.SquareCount; square++)
                {
                    if (square == station) continue;
                    Vector3 centre = board.Layout.SquareCenter(square);
                    float stopAt = lane.Nearest(centre);

                    BusRoute way = board.BusRouteTo(square);
                    AssertNear(bus.ParkedSpot, way.Position(0f), $"to {square}: starts parked");
                    AssertNear(bus.ParkedHeading, way.Heading(0f));
                    Assert.AreEqual(1f, way.Scale(0f), 1e-4f);
                    AssertNear(lane.Point(stopAt) + Vector3.up * BoardView.RoadHeight, way.Position(way.Length), $"to {square}: stops on the line by the square");
                    AssertNear(lane.Heading(stopAt), way.Heading(way.Length));
                    Assert.AreEqual(BusRoute.RoadScale, way.Scale(way.Length), 1e-4f, "small enough for the lane");
                    Assert.Less((way.Position(way.Length) - centre).magnitude, board.Layout.Depth * 1.25f, "a hop away from the square");
                    AssertDrivable(way, edge, corner, parkedHeight, $"to {square}");
                    float before = float.MaxValue;
                    for (float distance = 0f; distance <= way.Length; distance += 0.05f)
                    {
                        float height = way.Position(distance).y;
                        Assert.LessOrEqual(height, before + 1e-4f, $"to {square}: only ever downhill on the way out");
                        before = height;
                    }

                    BusRoute home = board.BusRouteHome(square);
                    AssertNear(lane.Point(stopAt) + Vector3.up * BoardView.RoadHeight, home.Position(0f), $"from {square}: starts where it stopped");
                    AssertNear(lane.Heading(stopAt), home.Heading(0f));
                    AssertNear(bus.ParkedSpot, home.Position(home.Length), $"from {square}: ends parked");
                    AssertNear(bus.ParkedHeading, home.Heading(home.Length));
                    Assert.AreEqual(1f, home.Scale(home.Length), 1e-4f);
                    AssertDrivable(home, edge, corner, parkedHeight, $"from {square}");

                    // Out and home together are once round the lawn and the two ways between the stop and the road.
                    Assert.That(way.Length + home.Length, Is.InRange(lane.Length, lane.Length + 4f * board.Layout.Depth), $"square {square}");
                }

                // The next square is a short drive, the one before the station nearly once round: the bus never turns back.
                Assert.Less(board.BusRouteTo(station + 1).Length, lane.Quarter);
                Assert.Greater(board.BusRouteTo(station - 1 + board.Layout.SquareCount).Length, lane.Length);

                // On the board: the bus stands where its route says, and parks again.
                BusRoute ride = board.BusRouteTo(0);
                bus.Show(ride, ride.Length);
                Assert.IsTrue(bus.Away);
                AssertNear(ride.Position(ride.Length), bus.transform.localPosition, "it stands still where it stops (no bob)");
                AssertNear(ride.Heading(ride.Length), bus.transform.localRotation * Vector3.forward);
                Assert.AreEqual(bus.Size * BusRoute.RoadScale, bus.transform.localScale.x, 1e-4f);
                bus.Show(ride, ride.Length / 2f);
                Assert.That(bus.transform.localPosition.y - ride.Position(ride.Length / 2f).y, Is.InRange(0f, BusView.Bob + 1e-5f), "a little bob on the way");
                bus.Park();
                Assert.IsFalse(bus.Away);
                AssertNear(bus.ParkedSpot, bus.transform.localPosition);
                Assert.AreEqual(bus.Size, bus.transform.localScale.x, 1e-4f);

                // Turning the table turns the stop; the waiting bus stays on its parking place.
                board.FaceCamera(BoardCameraRig.BaseYaw + 180f);
                AssertNear(BusModel.ParkedAt, stop.InverseTransformPoint(bus.transform.position), "parked after the table turned");
                AssertNear(-stop.right, bus.transform.forward);
                board.FaceCamera(BoardCameraRig.BaseYaw);

                // Only a piece at the station can be taken.
                PawnView pawn = PawnView.Create(holder.transform, board, 0, Color.red, new PawnCrowd(4), 1, "suit");
                Assert.IsFalse(BusTrip.CanCarry(board, pawn), "it stands on the start");
                pawn.PlaceOn(station);
                Assert.IsTrue(BusTrip.CanCarry(board, pawn));
                Assert.IsFalse(BusTrip.CanCarry(null, pawn));
                Assert.IsFalse(BusTrip.CanCarry(board, null));
                pawn.transform.localScale = Vector3.zero;
                pawn.PlaceOn(0);
                AssertNear(Vector3.one, pawn.transform.localScale, "a piece put back on a square has its size again");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void ADriveTakesAsLongAsItsLengthWithinLimits()
        {
            Assert.AreEqual(BusRoute.LeastSeconds, BusRoute.Seconds(1f), 1e-5f, "a short drive is not a blink");
            Assert.AreEqual(BusRoute.MostSeconds, BusRoute.Seconds(1000f), 1e-5f, "a long one is driven faster instead of longer");
            float middle = BusRoute.Speed * (BusRoute.LeastSeconds + BusRoute.MostSeconds) / 2f;
            Assert.AreEqual(middle / BusRoute.Speed, BusRoute.Seconds(middle), 1e-5f);
            Assert.Less(BusRoute.RoadScale, 1f);

            Assert.AreEqual(0f, BusView.Glide(0f), 1e-6f);
            Assert.AreEqual(1f, BusView.Glide(1f), 1e-6f);
            Assert.AreEqual(0.5f, BusView.Glide(0.5f), 1e-6f);
            Assert.Less(BusView.Glide(0.1f), 0.1f, "it pulls away gently");
            Assert.Greater(BusView.Glide(0.9f), 0.9f, "and slows to its stop");
            for (float t = 0.02f; t <= 1f; t += 0.02f) Assert.Greater(BusView.Glide(t), BusView.Glide(t - 0.02f), "never backwards");
        }

        [Test]
        public void ThePieceShrinksIntoTheBusAndGrowsOutOfIt()
        {
            Assert.AreEqual(1f, PawnView.BoardingSize(0f), 1e-6f, "its own size on the square");
            Assert.AreEqual(PawnView.RidingSize, PawnView.BoardingSize(1f), 1e-6f);
            Assert.Less(PawnView.RidingSize, 0.2f, "small enough to slip into the bus");
            Assert.Greater(PawnView.BoardingSize(0.5f), 0.7f, "it shrinks late, so the hop reads");
            for (float t = 0.05f; t <= 1f; t += 0.05f) Assert.Less(PawnView.BoardingSize(t), PawnView.BoardingSize(t - 0.05f));

            Vector3 ground = new Vector3(1f, 0.2f, 2f), seat = new Vector3(3f, 0.8f, 2.5f);
            AssertNear(ground, PawnView.BoardingPoint(ground, seat, 0f));
            AssertNear(seat, PawnView.BoardingPoint(ground, seat, 1f));
            Vector3 middle = PawnView.BoardingPoint(ground, seat, 0.5f);
            Assert.Greater(middle.y, seat.y, "an arc over the ground, up over the roof");
            Assert.AreEqual(2f, middle.x, 1e-4f);
        }

        [Test]
        public void TheBusAndItsStopAreTwoModelsCutFromOne()
        {
            Assert.IsTrue(BusModel.IsAvailable, "run Tools/Art/make_station.py");
            Assert.AreEqual(FigureModels.StationId, FigureModels.SkinOf(FigureModels.BusId), "the bus reads the station's colour map");
            Assert.AreEqual(FigureModels.SkinResource(FigureModels.StationId), FigureModels.SkinResource(FigureModels.BusId));
            Assert.AreEqual("suit", FigureModels.SkinOf("suit"));

            string folder = Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Models");
            var bus = FigurePack.Parse(File.ReadAllBytes(Path.Combine(folder, $"figure_{FigureModels.BusId}.bytes")));
            Assert.That(bus.Single(level => level.Name == FigurePack.HighLevel).TriangleCount, Is.InRange(50000, 80000), "as detailed as it was in the diorama");
            Assert.That(bus.Single(level => level.Name == FigurePack.LowLevel).TriangleCount, Is.InRange(10000, 20000));
            foreach (FigureMesh level in bus)
            {
                var bounds = new Bounds(level.Positions[0], Vector3.zero);
                foreach (Vector3 position in level.Positions) bounds.Encapsulate(position);
                Assert.AreEqual(BusModel.Width, bounds.size.x, 0.005f, $"{level.Name}: its width across x");
                Assert.AreEqual(BusModel.Length, bounds.size.z, 0.005f, $"{level.Name}: its length along z");
                Assert.AreEqual(BusModel.Height, bounds.max.y, 0.005f, $"{level.Name}: its roof");
                Assert.That(bounds.min.y, Is.InRange(0f, 0.01f), $"{level.Name}: the tyres stand on the ground");
                Assert.AreEqual(0f, bounds.center.x, 0.005f, $"{level.Name}: centred");
                Assert.AreEqual(0f, bounds.center.z, 0.005f);
            }

            // The stop no longer carries the bus: nothing of it is left where the bus's body stands.
            var stop = FigurePack.Parse(File.ReadAllBytes(Path.Combine(folder, $"figure_{FigureModels.StationId}.bytes")));
            Quaternion toBus = Quaternion.Inverse(Quaternion.Euler(0f, BusModel.ParkedTurn, 0f));
            foreach (FigureMesh level in stop)
            {
                var bounds = new Bounds(level.Positions[0], Vector3.zero);
                int inside = 0;
                foreach (Vector3 position in level.Positions)
                {
                    bounds.Encapsulate(position);
                    Vector3 local = toBus * (position - BusModel.ParkedAt);
                    if (Mathf.Abs(local.x) < BusModel.Width * 0.4f && Mathf.Abs(local.z) < BusModel.Length * 0.4f
                        && local.y > BusModel.Height * 0.15f && local.y < BusModel.Height * 0.9f) inside++;
                }

                Assert.AreEqual(0, inside, $"{level.Name}: the bus is still part of the stop");
                Assert.AreEqual(1f, bounds.size.x, 0.01f, $"{level.Name}: the stop is still one unit wide");
                Assert.AreEqual(BoardView.StationDepth, bounds.size.z, 0.01f, $"{level.Name}: and as deep as the board places it");
                Assert.AreEqual(0f, bounds.min.y, 0.01f);
            }
        }

        /// <summary>A way the bus can drive: no jumps, no sudden turns, heights between the road and the stop, and over no square but the station's.</summary>
        private static void AssertDrivable(BusRoute route, float edge, Vector3 corner, float parkedHeight, string what)
        {
            const float Step = 0.05f;
            Vector3 last = route.Position(0f), lastHeading = route.Heading(0f);
            for (float distance = Step; distance <= route.Length; distance += Step)
            {
                Vector3 point = route.Position(distance), heading = route.Heading(distance);
                Vector3 moved = point - last;
                moved.y = 0f;
                Assert.That(moved.magnitude, Is.InRange(Step * 0.6f, Step * 1.4f), $"{what}: even steps at {distance}");
                Assert.AreEqual(1f, heading.magnitude, 1e-3f);
                Assert.Less(Vector3.Angle(lastHeading, heading), 10f, $"{what}: no sudden turn at {distance}");
                Assert.Greater(Vector3.Dot(moved.normalized, heading), 0.9f, $"{what}: it drives nose first at {distance}");
                Assert.That(point.y, Is.InRange(BoardView.RoadHeight - 1e-4f, parkedHeight + 1e-4f), $"{what}: on the ground at {distance}");
                Assert.That(route.Scale(distance), Is.InRange(BusRoute.RoadScale - 1e-4f, 1f + 1e-4f));
                if (Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.z)) >= edge)
                {
                    // Over the squares: only over the station's own corner square.
                    Assert.GreaterOrEqual(point.x * Mathf.Sign(corner.x), edge - 0.01f, $"{what}: over another square at {distance}");
                    Assert.GreaterOrEqual(point.z * Mathf.Sign(corner.z), edge - 0.01f, $"{what}: over another square at {distance}");
                }
                else
                {
                    Assert.LessOrEqual(point.y, parkedHeight - 0.01f, $"{what}: down from the stop before the road at {distance}");
                }

                last = point;
                lastHeading = heading;
            }
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, string message = null) =>
            Assert.Less((expected - actual).magnitude, 2e-3f, $"{message}: expected {expected:F4}, was {actual:F4}");
    }
}
