using System.Collections;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// A player's ride from the bus station, the owner's wish (2026-11-10): the piece hops into the bus, the bus drives
    /// down to the road round the lawn and along it to the chosen square, the piece hops out onto the square, and the
    /// bus goes home by itself while the game goes on. It is heard too (the owner's wish, 2026-11-11): the engine starts
    /// as the piece gets in, the horn sounds and the engine pulls away, the brakes squeal and hiss as it stops.
    /// </summary>
    public static class BusTrip
    {
        /// <summary>From the start of the piece's hop in until the bus pulls away, at normal speed: the engine starts meanwhile.</summary>
        public const float StartSeconds = 0.95f;

        /// <summary>How long before the bus stops its brakes are heard, and over how long the engine's sound dies away then.</summary>
        public const float BrakeLead = 0.55f;
        public const float EngineFade = 0.35f;

        /// <summary>On its way home the bus is heard pulling away, and its engine fades over this long: the game goes on.</summary>
        public const float HomeFade = 1.6f;

        /// <summary>Whether the bus can take <paramref name="pawn"/>: the board has its bus and the piece stands at the station.</summary>
        public static bool CanCarry(BoardView board, PawnView pawn) =>
            board != null && pawn != null && board.Bus != null && pawn.Square == board.StationSquare;

        /// <summary>How long the bus waits after the piece is in, for its engine to have started.</summary>
        public static float WaitToStart(float speed) => Mathf.Max(0f, StartSeconds - PawnView.BoardingSeconds) / Mathf.Max(speed, 0.1f);

        /// <summary>When, in a drive of <paramref name="seconds"/>, the brakes are heard: <see cref="BrakeLead"/> before its end, at once in a shorter drive.</summary>
        public static float BrakesAt(float seconds, float speed) => Mathf.Max(0f, seconds - BrakeLead / Mathf.Max(speed, 0.1f));

        /// <summary>The whole ride of <paramref name="pawn"/> to <paramref name="destination"/>; ends when the piece stands there.</summary>
        public static IEnumerator Carry(BoardView board, PawnView pawn, int destination)
        {
            BusView bus = board.Bus;
            float speed = Mathf.Max(pawn.Speed, 0.1f);
            // Still on its way back from the last ride: it is at the stop at once for this one, and quiet.
            bus.Park();
            AudioPlayer.Stop(SoundCue.BusRev, 0f);
            AudioPlayer.Play(SoundCue.BusStart);
            yield return pawn.HopInto(bus.Roof);
            for (float wait = WaitToStart(speed); wait > 0f; wait -= Time.deltaTime) yield return null;

            AudioPlayer.Play(SoundCue.BusHorn);
            AudioPlayer.Play(SoundCue.BusRev);
            BusRoute way = board.BusRouteTo(destination);
            float seconds = BusRoute.Seconds(way.Length) / speed, brakesAt = BrakesAt(seconds, speed), driven = 0f;
            bool braked = false;
            // The piece rides along out of sight, so it gets out where the bus stands and whatever looks for it finds it there.
            IEnumerator drive = bus.Drive(way, seconds);
            while (drive.MoveNext())
            {
                pawn.RideAt(bus.Roof);
                if (!braked && driven >= brakesAt)
                {
                    braked = true;
                    Brake();
                }

                yield return drive.Current;
                driven += Time.deltaTime;
            }

            if (!braked) Brake();
            pawn.RideAt(bus.Roof);
            yield return pawn.HopOutOf(bus.Roof, destination);
            BusRoute home = board.BusRouteHome(destination);
            AudioPlayer.Play(SoundCue.BusRev);
            AudioPlayer.Stop(SoundCue.BusRev, HomeFade);
            bus.DriveHome(home, BusRoute.Seconds(home.Length) / speed);
        }

        private static void Brake()
        {
            AudioPlayer.Stop(SoundCue.BusRev, EngineFade);
            AudioPlayer.Play(SoundCue.BusBrake);
        }
    }
}
