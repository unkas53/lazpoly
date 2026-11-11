using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A floating island afloat (the owner's wish, 2026-11-11: movement, so the islands do not look made, but calm): the
    /// island rises and sinks a little, slowly, and the money safe standing on it with it, every island in its own time.
    /// The board says where the two rest (<see cref="Rest"/>); the float is laid over that after everything else has
    /// moved, so in a still scene both stay exactly at rest. Money in flight follows its safe (<see cref="Effects.MoneyFlight"/>).
    /// The motion is pure (<see cref="RiseAt"/>).
    /// </summary>
    public sealed class IslandFloat : MonoBehaviour
    {
        /// <summary>How far an island rises and sinks from its rest.</summary>
        public const float Rise = 0.045f;

        /// <summary>The quickest and the slowest island's time for one rise and fall, in seconds.</summary>
        public const float QuickSeconds = 6.5f;
        public const float SlowSeconds = 9.5f;

        private Transform _safe;
        private Vector3 _islandRest, _safeRest;
        private int _seed;
        private float _age;

        /// <summary>Sets <paramref name="island"/> afloat with <paramref name="safe"/> on it; <paramref name="seed"/> gives each island its own time.</summary>
        public static IslandFloat Attach(Transform island, Transform safe, int seed)
        {
            IslandFloat afloat = island.gameObject.AddComponent<IslandFloat>();
            afloat._safe = safe;
            afloat._seed = seed;
            afloat.Rest();
            return afloat;
        }

        /// <summary>Takes where the island and its safe are now as their rest: called after the board has placed them.</summary>
        public void Rest()
        {
            _islandRest = transform.localPosition;
            if (_safe != null) _safeRest = _safe.localPosition;
        }

        /// <summary>How long an island takes for one rise and fall.</summary>
        public static float SecondsOf(int seed) => Mathf.Lerp(QuickSeconds, SlowSeconds, Mathf.Repeat(seed * 0.381966f, 1f));

        /// <summary>How far an island is above (or below) its rest at a time.</summary>
        public static float RiseAt(float seconds, int seed) =>
            Rise * Mathf.Sin(2f * Mathf.PI * (seconds / SecondsOf(seed) + Mathf.Repeat(seed * 0.618034f, 1f)));

        /// <summary>Puts the island and its safe where they are <paramref name="seconds"/> into their float.</summary>
        public void Show(float seconds)
        {
            Vector3 rise = Vector3.up * RiseAt(seconds, _seed);
            transform.localPosition = _islandRest + rise;
            if (_safe != null) _safe.localPosition = _safeRest + rise;
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            Show(_age);
        }
    }
}
