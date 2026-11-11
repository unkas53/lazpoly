using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The loose stones under a floating island (<see cref="Models.IslandParts"/>) hanging in the air: each rises and
    /// sinks a little in its own time and turns slowly, so the island looks afloat. The island itself and the safe on
    /// it stay still, so money flies to where it always did. The motion is pure (<see cref="RiseAt"/>).
    /// </summary>
    public sealed class IslandDrift : MonoBehaviour
    {
        /// <summary>How far a stone rises and sinks from its rest.</summary>
        public const float Rise = 0.035f;

        /// <summary>The quickest and the slowest stone's time for one rise and fall, in seconds.</summary>
        public const float QuickSeconds = 4.5f;
        public const float SlowSeconds = 7.5f;

        /// <summary>How fast a stone turns, in degrees a second.</summary>
        public const float TurnPerSecond = 9f;

        private Transform[] _stones;
        private Vector3[] _rest;
        private int _seed;
        private float _age;

        /// <summary>Sets the stones under <paramref name="stones"/> adrift; <paramref name="seed"/> gives each island its own time.</summary>
        public static IslandDrift Attach(Transform stones, int seed)
        {
            if (stones == null) return null;
            IslandDrift drift = stones.gameObject.AddComponent<IslandDrift>();
            drift._seed = seed;
            drift._stones = new Transform[stones.childCount];
            drift._rest = new Vector3[stones.childCount];
            for (int i = 0; i < stones.childCount; i++)
            {
                drift._stones[i] = stones.GetChild(i);
                drift._rest[i] = drift._stones[i].localPosition;
            }

            drift.Show(0f);
            return drift;
        }

        /// <summary>How long a stone takes for one rise and fall.</summary>
        public static float SecondsOf(int stone, int seed) => Mathf.Lerp(QuickSeconds, SlowSeconds, Mathf.Repeat((stone * 5 + seed * 3) * 0.381966f, 1f));

        /// <summary>How far a stone is above (or below) its rest at a time.</summary>
        public static float RiseAt(float seconds, int stone, int seed) =>
            Rise * Mathf.Sin(2f * Mathf.PI * (seconds / SecondsOf(stone, seed) + Mathf.Repeat((stone * 7 + seed * 11) * 0.618034f, 1f)));

        /// <summary>Puts the stones where they are <paramref name="seconds"/> into their drift.</summary>
        public void Show(float seconds)
        {
            if (_stones == null) return;
            for (int i = 0; i < _stones.Length; i++)
            {
                if (_stones[i] == null) continue;
                _stones[i].localPosition = _rest[i] + Vector3.up * RiseAt(seconds, i, _seed);
                _stones[i].localRotation = Quaternion.Euler(0f, TurnPerSecond * seconds * (i % 2 == 0 ? 1f : -1f), 0f);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Show(_age);
        }
    }
}
