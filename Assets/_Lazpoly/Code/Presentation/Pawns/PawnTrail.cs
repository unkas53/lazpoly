using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// The short trail a walking pawn leaves: a flat disc in its colour on each spot it hops from,
    /// shrinking away in half a second. A few discs are kept and reused so a long walk makes no garbage.
    /// </summary>
    public sealed class PawnTrail : MonoBehaviour
    {
        public const float Seconds = 0.5f;
        private const float Radius = 0.17f;
        private const float Thickness = 0.03f;
        private const float Lift = 0.001f;

        /// <summary>How high a disc's top stands over the pawn's foot: under the board's print, so a disc never hides a price.</summary>
        public const float Top = Lift + Thickness * Radius;
        private const int Discs = 6;

        private readonly Transform[] _discs = new Transform[Discs];
        private readonly float[] _age = new float[Discs];
        private int _next;

        public static PawnTrail Create(Transform parent, Color color)
        {
            var root = new GameObject("Pawn trail");
            root.transform.SetParent(parent, false);
            var trail = root.AddComponent<PawnTrail>();
            Color faint = Color.Lerp(color, Color.white, 0.35f);
            for (int i = 0; i < Discs; i++)
            {
                GameObject disc = ModelParts.Create("Footprint", root.transform, MeshFactory.Disc(Thickness), faint, 0.3f);
                disc.SetActive(false);
                trail._discs[i] = disc.transform;
            }

            return trail;
        }

        /// <summary>Drops a disc at <paramref name="at"/> (a pawn's foot position).</summary>
        public void Leave(Vector3 at)
        {
            Transform disc = _discs[_next];
            _age[_next] = 0f;
            _next = (_next + 1) % Discs;
            disc.position = at + Vector3.up * Lift;
            disc.localScale = Vector3.one * Radius;
            disc.gameObject.SetActive(true);
        }

        private void Update()
        {
            for (int i = 0; i < Discs; i++)
            {
                if (!_discs[i].gameObject.activeSelf) continue;
                _age[i] += Time.deltaTime;
                float t = _age[i] / Seconds;
                if (t >= 1f)
                {
                    _discs[i].gameObject.SetActive(false);
                    continue;
                }

                float size = Radius * (1f - t * t);
                _discs[i].localScale = new Vector3(size, Radius, size);
            }
        }
    }
}
