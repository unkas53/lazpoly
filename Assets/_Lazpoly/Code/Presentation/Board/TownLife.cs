using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The small movements that keep the island alive: the boat drifts slowly round its mooring and
    /// rocks, the ripples breathe, and in the evening the lighthouse beam comes on and turns.
    /// </summary>
    public sealed class TownLife : MonoBehaviour
    {
        /// <summary>Seconds for the boat to go once round its drift.</summary>
        public const float DriftSeconds = 48f;
        public const float BeamDegreesPerSecond = 40f;

        private static readonly Color BeamColor = new Color(1f, 0.92f, 0.62f);

        private readonly List<Transform> _ripples = new List<Transform>();
        private readonly List<Vector3> _rippleScale = new List<Vector3>();
        private Transform _boat;
        private Vector3 _mooring;
        private Vector2 _drift;
        private Transform _beam;
        private Material _beamMaterial;
        private float _evening;

        public float Evening => _evening;

        public static TownLife Attach(GameObject town) => town.AddComponent<TownLife>();

        public void AddRipple(Transform ripple)
        {
            _ripples.Add(ripple);
            _rippleScale.Add(ripple.localScale);
        }

        /// <summary>The boat drifts on an ellipse of the given half-widths around where it was placed.</summary>
        public void SetBoat(Transform boat, Vector2 drift)
        {
            _boat = boat;
            _mooring = boat.localPosition;
            _drift = drift;
        }

        /// <summary>Puts a beam on the lighthouse, turning around <paramref name="lampLocal"/> (in the town's space); it shows only in the evening.</summary>
        public void AddBeam(Vector3 lampLocal, float length)
        {
            var pivot = new GameObject("Lighthouse beam");
            pivot.transform.SetParent(transform, false);
            pivot.transform.localPosition = lampLocal;
            // A long thin cone lying flat, tip away from the lamp.
            GameObject cone = ModelParts.Create("Beam", pivot.transform, MeshFactory.Cone(8), BeamColor, 0.9f);
            // The cone's apex (+Y) is turned to -Z, so with the cone pushed out along +Z the apex sits at the lamp.
            cone.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            cone.transform.localPosition = new Vector3(0f, 0f, length);
            cone.transform.localScale = new Vector3(length * 0.12f, length, length * 0.12f);
            _beamMaterial = cone.GetComponent<Renderer>().sharedMaterial;
            if (_beamMaterial != null && _beamMaterial.HasProperty("_EmissionColor"))
            {
                _beamMaterial.EnableKeyword("_EMISSION");
                _beamMaterial.SetColor("_EmissionColor", BeamColor * 1.6f);
            }

            _beam = pivot.transform;
            _beam.gameObject.SetActive(false);
        }

        /// <summary>0 by day, 1 at dusk; the beam fades in from 0.3.</summary>
        public void SetEvening(float evening)
        {
            _evening = Mathf.Clamp01(evening);
            if (_beam == null) return;
            float glow = Mathf.InverseLerp(0.3f, 0.8f, _evening);
            _beam.gameObject.SetActive(glow > 0f);
            _beam.localScale = new Vector3(glow, glow, 1f);
        }

        /// <summary>Where the boat is at <paramref name="time"/>, relative to its mooring: slowly round an ellipse.</summary>
        public static Vector3 BoatOffset(Vector2 drift, float time)
        {
            float angle = time / DriftSeconds * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * drift.x, 0f, Mathf.Sin(angle) * drift.y);
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = 0; i < _ripples.Count; i++)
            {
                float swell = 1f + 0.03f * Mathf.Sin(time * 0.9f + i * 2.1f);
                _ripples[i].localScale = new Vector3(_rippleScale[i].x * swell, _rippleScale[i].y, _rippleScale[i].z * swell);
            }

            if (_boat != null)
            {
                Vector3 offset = BoatOffset(_drift, time);
                Vector3 ahead = BoatOffset(_drift, time + 0.5f) - offset;
                _boat.localPosition = _mooring + offset;
                if (ahead.sqrMagnitude > 1e-8f)
                {
                    Quaternion heading = Quaternion.LookRotation(ahead);
                    // A gentle rock across the hull on top of the heading.
                    _boat.localRotation = heading * Quaternion.Euler(0f, 0f, Mathf.Sin(time * 1.3f) * 4f);
                }
            }

            if (_beam != null && _beam.gameObject.activeSelf)
            {
                _beam.localRotation = Quaternion.Euler(0f, time * BeamDegreesPerSecond, 0f);
            }
        }
    }
}
