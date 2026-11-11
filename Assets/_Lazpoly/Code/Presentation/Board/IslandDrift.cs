using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The small life of a floating island (<see cref="Models.IslandParts"/>; the owner's wish, 2026-11-11: movement, so
    /// the islands do not look made, but calm, since the eye belongs on the board): the loose stones under it rise and
    /// sink a little, each in its own time, and turn slowly; the vines sway from their feet as in a light breeze, all
    /// leaning much the same way at once, as wind makes them, each a little behind the other. The motion is pure
    /// (<see cref="RiseAt"/>, <see cref="SwayAt"/>). The island's own floating is <see cref="IslandFloat"/>.
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

        /// <summary>How far a vine swings from hanging straight, in degrees, and how long the breeze takes to come and go, in seconds.</summary>
        public const float Sway = 4f;
        public const float BreezeSeconds = 5.5f;

        private Transform[] _stones, _vines;
        private Vector3[] _rest;
        private int _seed;
        private float _age;

        /// <summary>Sets the stones under <paramref name="stones"/> adrift and the vines under <paramref name="vines"/> swaying; <paramref name="seed"/> gives each island its own time.</summary>
        public static IslandDrift Attach(Transform stones, Transform vines, int seed)
        {
            if (stones == null) return null;
            IslandDrift drift = stones.gameObject.AddComponent<IslandDrift>();
            drift._seed = seed;
            drift._stones = Children(stones);
            drift._vines = vines != null ? Children(vines) : new Transform[0];
            drift._rest = new Vector3[drift._stones.Length];
            for (int i = 0; i < drift._stones.Length; i++) drift._rest[i] = drift._stones[i].localPosition;
            return drift;
        }

        private static Transform[] Children(Transform of)
        {
            var children = new Transform[of.childCount];
            for (int i = 0; i < children.Length; i++) children[i] = of.GetChild(i);
            return children;
        }

        /// <summary>How long a stone takes for one rise and fall.</summary>
        public static float SecondsOf(int stone, int seed) => Mathf.Lerp(QuickSeconds, SlowSeconds, Mathf.Repeat((stone * 5 + seed * 3) * 0.381966f, 1f));

        /// <summary>How far a stone is above (or below) its rest at a time.</summary>
        public static float RiseAt(float seconds, int stone, int seed) =>
            Rise * Mathf.Sin(2f * Mathf.PI * (seconds / SecondsOf(stone, seed) + Mathf.Repeat((stone * 7 + seed * 11) * 0.618034f, 1f)));

        /// <summary>
        /// How far a vine swings at a time, in degrees: the breeze coming and going, with a quicker flutter on top, each
        /// vine a little behind the one before. Never past <see cref="Sway"/>.
        /// </summary>
        public static float SwayAt(float seconds, int vine, int seed)
        {
            float behind = vine * 0.11f + seed * 0.23f;
            float breeze = Mathf.Sin(2f * Mathf.PI * (seconds / BreezeSeconds - behind));
            float flutter = Mathf.Sin(2f * Mathf.PI * (seconds / (BreezeSeconds * 0.37f) - behind * 2.3f));
            return Sway * (0.75f * breeze + 0.25f * flutter);
        }

        /// <summary>Puts the stones and the vines where they are <paramref name="seconds"/> into their drift.</summary>
        public void Show(float seconds)
        {
            if (_stones == null) return;
            for (int i = 0; i < _stones.Length; i++)
            {
                if (_stones[i] == null) continue;
                _stones[i].localPosition = _rest[i] + Vector3.up * RiseAt(seconds, i, _seed);
                _stones[i].localRotation = Quaternion.Euler(0f, TurnPerSecond * seconds * (i % 2 == 0 ? 1f : -1f), 0f);
            }

            for (int i = 0; i < _vines.Length; i++)
            {
                if (_vines[i] == null) continue;
                // Mostly along the island's edge one way, a little across it.
                float swing = SwayAt(seconds, i, _seed);
                _vines[i].localRotation = Quaternion.Euler(0.4f * SwayAt(seconds + 1.3f, i, _seed), 0f, swing);
            }
        }

        private void Update()
        {
            _age += Time.deltaTime;
            Show(_age);
        }
    }
}
