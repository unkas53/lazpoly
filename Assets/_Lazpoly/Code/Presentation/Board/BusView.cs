using System.Collections;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The bus station's bus on the board (the owner's wish, 2026-11-10): it waits at the stop where the diorama always
    /// showed it, drives a <see cref="BusRoute"/> with a player on board (<see cref="Drive"/>) and finds its own way
    /// home afterwards while the game goes on (<see cref="DriveHome"/>). A child of the board, placed in the board's
    /// frame; <see cref="Park"/> puts it back at the stop at once, wherever it is.
    /// </summary>
    public sealed class BusView : MonoBehaviour
    {
        /// <summary>How high the body bobs while it drives, and how often per unit driven.</summary>
        public const float Bob = 0.012f;
        public const float BobRate = 7f;

        private Transform _station;
        private Coroutine _wayHome;

        /// <summary>True while the bus is off its parking place.</summary>
        public bool Away { get; private set; }

        /// <summary>The bus for the stop <paramref name="station"/>, parked; null when the bus's model is not in the build.</summary>
        public static BusView Create(Transform board, Transform station)
        {
            GameObject model = BusModel.Create("Bus", board);
            if (model == null) return null;
            var bus = model.AddComponent<BusView>();
            bus._station = station;
            bus.Park();
            return bus;
        }

        /// <summary>The stop's size on the board: the bus's own size at the stop.</summary>
        public float Size => _station.localScale.x;

        /// <summary>Where the bus stands when parked, in the board's frame.</summary>
        public Vector3 ParkedSpot => _station.localPosition + _station.localRotation * (BusModel.ParkedAt * Size);

        /// <summary>Which way its nose points there.</summary>
        public Vector3 ParkedHeading => ParkedTurn * Vector3.forward;

        /// <summary>How much higher than the stop's square the parked bus stands, on the stop's own piece of road.</summary>
        public float StopRise => BusModel.ParkedAt.y * Size;

        /// <summary>The middle of the roof (world space): a player's piece hops in and out here.</summary>
        public Vector3 Roof => transform.TransformPoint(new Vector3(0f, BusModel.Height, 0f));

        /// <summary>The middle of the stop's base, in the board's frame, and half its size along the board's x and z.</summary>
        public Vector3 BaseCentre => _station.localPosition;

        public Vector2 BaseHalf
        {
            get
            {
                // The stop always stands square to the board, so its turned half size only needs its signs dropped.
                Vector3 half = _station.localRotation * new Vector3(Size / 2f, 0f, BoardView.StationDepth * Size / 2f);
                return new Vector2(Mathf.Abs(half.x), Mathf.Abs(half.z));
            }
        }

        private Quaternion ParkedTurn => _station.localRotation * Quaternion.Euler(0f, BusModel.ParkedTurn, 0f);

        /// <summary>Back at the stop at once; a drive home still under way ends.</summary>
        public void Park()
        {
            if (_wayHome != null) StopCoroutine(_wayHome);
            _wayHome = null;
            Away = false;
            transform.localPosition = ParkedSpot;
            transform.localRotation = ParkedTurn;
            transform.localScale = Vector3.one * Size;
        }

        /// <summary>Puts the bus where it is after <paramref name="distance"/> of <paramref name="route"/> (public so a test can check the drive).</summary>
        public void Show(BusRoute route, float distance)
        {
            Away = true;
            Vector3 heading = route.Heading(distance);
            transform.localPosition = route.Position(distance) + Vector3.up * (Bob * BobAt(route, distance));
            if (heading.sqrMagnitude > 1e-6f) transform.localRotation = Quaternion.LookRotation(heading);
            transform.localScale = Vector3.one * (Size * route.Scale(distance));
        }

        /// <summary>Drives the whole of <paramref name="route"/> in <paramref name="seconds"/>, pulling away and stopping gently.</summary>
        public IEnumerator Drive(BusRoute route, float seconds)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / Mathf.Max(seconds, 0.01f))
            {
                Show(route, route.Length * Glide(t));
                yield return null;
            }

            Show(route, route.Length);
        }

        /// <summary>Drives <paramref name="route"/> home by itself while the game goes on, and parks.</summary>
        public void DriveHome(BusRoute route, float seconds)
        {
            if (_wayHome != null) StopCoroutine(_wayHome);
            if (!isActiveAndEnabled)
            {
                Park();
                return;
            }

            _wayHome = StartCoroutine(GoHome(route, seconds));
        }

        /// <summary>How much of a drive is done after a share of its time: slow away, steady, slow to a stop.</summary>
        public static float Glide(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private IEnumerator GoHome(BusRoute route, float seconds)
        {
            yield return Drive(route, seconds);
            _wayHome = null;
            Park();
        }

        /// <summary>The body's bob: none at either end of the drive, so it stands still where it stops.</summary>
        private static float BobAt(BusRoute route, float distance)
        {
            float fromEnd = Mathf.Min(distance, route.Length - distance);
            return Mathf.Clamp01(fromEnd) * (0.5f + 0.5f * Mathf.Sin(distance * BobRate));
        }
    }
}
