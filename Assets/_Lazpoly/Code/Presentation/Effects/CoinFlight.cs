using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// A small coin that arcs from the payer to whoever gets the money (a pawn or the pot in the
    /// middle), spinning as it flies, so a payment is seen moving across the table and not only as
    /// two numbers changing.
    /// </summary>
    public sealed class CoinFlight : MonoBehaviour
    {
        /// <summary>Flight time at normal animation speed.</summary>
        public const float Seconds = 0.45f;

        private const float Radius = 0.11f;
        private const float Arc = 0.9f;
        private static readonly Color Gold = new Color(0.96f, 0.78f, 0.30f);

        private Vector3 _from;
        private Vector3 _to;
        private float _duration;
        private float _delay;
        private float _age;
        private Renderer _renderer;

        /// <summary>Starts a coin; it destroys itself when it lands.</summary>
        public static CoinFlight Spawn(Transform parent, Vector3 from, Vector3 to, float speed, float delay = 0f)
        {
            GameObject coin = ModelParts.Create("Coin", parent, MeshFactory.Disc(0.18f), Gold, 0.7f);
            coin.transform.position = from;
            coin.transform.localScale = Vector3.one * Radius;
            var flight = coin.AddComponent<CoinFlight>();
            flight._from = from;
            flight._to = to;
            flight._duration = Seconds / Mathf.Max(speed, 0.1f);
            flight._delay = delay / Mathf.Max(speed, 0.1f);
            flight._age = -flight._delay;
            flight._renderer = coin.GetComponent<Renderer>();
            // A delayed coin waits unseen; the object stays active so it keeps counting.
            flight._renderer.enabled = delay <= 0f;
            return flight;
        }

        /// <summary>How long the flight takes from the call until the coin lands, for the scene to wait on.</summary>
        public float TotalSeconds => _duration + _delay;

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age < 0f) return;
            if (!_renderer.enabled) _renderer.enabled = true;

            float t = _age / _duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 position = Vector3.Lerp(_from, _to, Ease.OutCubic(t));
            position.y += Mathf.Sin(t * Mathf.PI) * Arc;
            transform.position = position;
            transform.rotation = Quaternion.Euler(t * 720f, t * 180f, 0f);
        }
    }
}
