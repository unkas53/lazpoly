using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The lid of a player's money safe (<see cref="MoneyPile"/>) swinging on its hinge: <see cref="Swing"/> opens it at one
    /// moment and shuts it at another, as money goes in or comes out; the swing itself is pure (<see cref="MoneyPile.OpennessAt"/>).
    /// </summary>
    public sealed class SafeLid : MonoBehaviour
    {
        private float _openAt = -1f;
        private float _closeAt = -1f;
        private float _age;
        private float _speed = 1f;

        /// <summary>The lid's hinge, turned about its x as the lid opens.</summary>
        public Transform Hinge { get; set; }

        /// <summary>How open the lid is now, from 0 (shut) to 1 (open).</summary>
        public float Openness { get; private set; }

        /// <summary>Opens the lid <paramref name="openAt"/> seconds from now and shuts it <paramref name="closeAt"/> seconds from now (normal-speed seconds).</summary>
        public void Swing(float openAt, float closeAt, float speed)
        {
            _openAt = Mathf.Max(0f, openAt);
            _closeAt = Mathf.Max(closeAt, _openAt + MoneyPile.SwingSeconds);
            _speed = Mathf.Max(speed, 0.1f);
            _age = 0f;
            Show(MoneyPile.OpennessAt(0f, _openAt, _closeAt));
        }

        /// <summary>Sets the lid's angle for an openness from 0 to 1.</summary>
        public void Show(float openness)
        {
            Openness = Mathf.Clamp01(openness);
            if (Hinge != null) Hinge.localRotation = Quaternion.Euler(MoneyPile.OpenAngle * Openness, 0f, 0f);
        }

        private void Update()
        {
            if (_openAt < 0f) return;
            _age += Time.deltaTime * _speed;
            Show(MoneyPile.OpennessAt(_age, _openAt, _closeAt));
            if (_age >= _closeAt + MoneyPile.SwingSeconds) _openAt = -1f;
        }
    }
}
