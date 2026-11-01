using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// Makes a flag's pennant flutter: it swings a few degrees around the pole and flaps a little
    /// along its length, each flag out of step with the others. Runs only while the flag is shown.
    /// </summary>
    public sealed class FlagWave : MonoBehaviour
    {
        public const float SwingDegrees = 6f;
        public const float FlapDegrees = 3f;

        private Quaternion _rest;
        private float _phase;

        /// <summary>The pennant's rotation with no wind; the flutter is added to it.</summary>
        public void Rest(Quaternion rest, float phase)
        {
            _rest = rest;
            _phase = phase;
            transform.localRotation = rest;
        }

        /// <summary>The flutter at <paramref name="time"/>: swing around the pennant's own Z (the pole's axis) and flap around its length (Y).</summary>
        public static Quaternion Flutter(Quaternion rest, float time, float phase) =>
            rest * Quaternion.Euler(0f, Mathf.Sin(time * 6.3f + phase * 1.7f) * FlapDegrees, Mathf.Sin(time * 3.1f + phase) * SwingDegrees);

        private void Update()
        {
            transform.localRotation = Flutter(_rest, Time.time, _phase);
        }
    }
}
