using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// A flat ring in the player's colour under the pawn whose turn it is. It follows the pawn
    /// (without taking on its squash and stretch) and breathes slowly so the eye finds it. It stays away while
    /// the pawn rides the bus (<see cref="PawnView.Riding"/>), or it would hang in the air where the pawn got in, and
    /// while the pawn is locked up (<see cref="PawnView.Jailed"/>): the ring is made for a full-size figure, not for the cell.
    /// </summary>
    public sealed class TurnMarker : MonoBehaviour
    {
        /// <summary>The ring's size, grown with the figures (Faz 21: half as big again) so it still shows round their feet.</summary>
        private const float Radius = 0.26f * PawnView.CharacterHeight / 1.8f;
        private const float BreathSeconds = 1.6f;

        private PawnView _pawn;
        private Renderer _renderer;

        public static TurnMarker Create(Transform parent)
        {
            GameObject ring = ModelParts.Create("Turn marker", parent, MeshFactory.Ring(0.72f, 0.06f), Color.white, 0.4f);
            ring.transform.localScale = Vector3.one * Radius;
            var marker = ring.AddComponent<TurnMarker>();
            marker._renderer = ring.GetComponent<Renderer>();
            marker._renderer.enabled = false;
            return marker;
        }

        /// <summary>The pawn the marker sits under; null hides it.</summary>
        public PawnView Following => _pawn;

        public void Follow(PawnView pawn, Color color)
        {
            _pawn = pawn;
            if (pawn != null) ModelParts.Paint(gameObject, color, 0.4f);
            LateUpdate();
        }

        /// <summary>Whether the ring shows under <paramref name="pawn"/>: a pawn on the board that is neither riding the bus nor locked up in the cell.</summary>
        public static bool Shows(PawnView pawn) => pawn != null && pawn.gameObject.activeInHierarchy && !pawn.Riding && !pawn.Jailed;

        private void LateUpdate()
        {
            bool shown = Shows(_pawn);
            if (_renderer.enabled != shown) _renderer.enabled = shown;
            if (!shown) return;

            Vector3 at = _pawn.transform.position;
            at.y = _pawn.transform.position.y + 0.012f;
            transform.position = at;
            float breath = 1f + 0.08f * Mathf.Sin(Time.time * 2f * Mathf.PI / BreathSeconds);
            transform.localScale = new Vector3(Radius * breath, Radius, Radius * breath);
        }
    }
}
