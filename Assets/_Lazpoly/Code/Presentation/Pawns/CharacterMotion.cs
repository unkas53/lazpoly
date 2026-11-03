using Lazpoly.Presentation.Effects;
using UnityEngine;

namespace Lazpoly.Presentation.Pawns
{
    /// <summary>
    /// Body language for a character without a skeleton, on the model under the pawn (the pawn itself does the
    /// walking hops and squashes). At rest the figure breathes; while walking it leans into the step and sways
    /// from foot to foot; it jumps and spins round once when it is pleased (<see cref="Cheer"/>: a purchase, a
    /// new house, money coming in) and slumps and shakes its head when it is not (<see cref="Sulk"/>: rent, tax).
    /// The poses are pure functions of time so the tests can check that each one starts and ends at rest.
    /// </summary>
    public sealed class CharacterMotion : MonoBehaviour
    {
        public const float BreathSeconds = 3.2f;
        public const float BreathDepth = 0.012f;
        public const float WalkLeanDegrees = 7f;
        public const float WalkSwayDegrees = 5f;
        public const float CheerSeconds = 0.9f;
        public const float CheerHeight = 0.32f;
        public const float SulkSeconds = 1.5f;
        public const float SulkDrop = 0.07f;
        public const float SulkBowDegrees = 14f;
        public const float SulkShakeDegrees = 14f;

        private Quaternion _facing = Quaternion.identity;
        private Vector3 _size = Vector3.one;
        private float _phase;
        private float _walk;
        private float _walkTime;
        private float _cheerAge = -1f;
        private float _sulkAge = -1f;

        /// <summary>Animation speed multiplier from the settings (1 = normal).</summary>
        public float Speed { get; set; } = 1f;

        /// <summary>Whether the pawn is walking now; the lean eases in and out.</summary>
        public bool Walking { get; set; }

        /// <summary>True while a cheer or a sulk plays.</summary>
        public bool IsActing => _cheerAge >= 0f || _sulkAge >= 0f;

        /// <param name="size">The model's scale at rest (the character's height).</param>
        /// <param name="phase">A different phase per player, so a table of figures does not breathe in step.</param>
        public static CharacterMotion Attach(Transform model, Vector3 size, float phase)
        {
            var motion = model.gameObject.AddComponent<CharacterMotion>();
            motion._size = size;
            motion._phase = phase;
            motion._facing = model.localRotation;
            return motion;
        }

        /// <summary>The way the figure looks when nothing else turns it (towards the camera).</summary>
        public void SetFacing(Quaternion facing)
        {
            _facing = facing;
            Apply();
        }

        public void Cheer()
        {
            _sulkAge = -1f;
            _cheerAge = 0f;
        }

        public void Sulk()
        {
            if (_cheerAge >= 0f) return; // A cheer already playing is not cut short by a small payment straight after.
            _sulkAge = 0f;
        }

        /// <summary>How much taller than at rest the figure is while breathing, at <paramref name="time"/>.</summary>
        public static float Breath(float time, float phase) => 1f + BreathDepth * Mathf.Sin(2f * Mathf.PI * (time / BreathSeconds + phase));

        /// <summary>The cheer at <paramref name="t"/> (0..1): height of the jump and degrees turned (a full turn at the end).</summary>
        public static (float Height, float Spin) CheerPose(float t)
        {
            t = Mathf.Clamp01(t);
            // One big jump in the first two thirds, a small happy hop after it.
            float height = t < 0.66f
                ? CheerHeight * Mathf.Sin(Mathf.PI * t / 0.66f)
                : CheerHeight * 0.3f * Mathf.Sin(Mathf.PI * (t - 0.66f) / 0.34f);
            float spin = 360f * Ease.InOutCubic(Mathf.Clamp01(t / 0.66f));
            return (height, spin);
        }

        /// <summary>The sulk at <paramref name="t"/> (0..1): how far the figure has slumped (0..1) and its head shake in degrees.</summary>
        public static (float Slump, float Shake) SulkPose(float t)
        {
            t = Mathf.Clamp01(t);
            float slump = t < 0.2f ? Ease.OutCubic(t / 0.2f) : t < 0.7f ? 1f : 1f - Ease.InOutCubic((t - 0.7f) / 0.3f);
            float shake = t > 0.2f && t < 0.7f ? SulkShakeDegrees * Mathf.Sin((t - 0.2f) / 0.5f * 3f * 2f * Mathf.PI) * Mathf.Sin((t - 0.2f) / 0.5f * Mathf.PI) : 0f;
            return (slump, shake);
        }

        /// <summary>The walking lean and sway at a moment of the walk: lean forward in degrees and the side-to-side tilt.</summary>
        public static (float Lean, float Sway) WalkPose(float amount, float walkTime) =>
            (WalkLeanDegrees * amount, WalkSwayDegrees * amount * Mathf.Sin(walkTime * 2f * Mathf.PI * 2.2f));

        private void Update()
        {
            float step = Time.deltaTime * Mathf.Max(Speed, 0.1f);
            _walk = Mathf.MoveTowards(_walk, Walking ? 1f : 0f, step * 6f);
            _walkTime = _walk > 0f ? _walkTime + step : 0f;
            if (_cheerAge >= 0f && (_cheerAge += step) >= CheerSeconds) _cheerAge = -1f;
            if (_sulkAge >= 0f && (_sulkAge += step) >= SulkSeconds) _sulkAge = -1f;
            Apply();
        }

        private void Apply()
        {
            (float lean, float sway) = WalkPose(_walk, _walkTime);
            (float height, float spin) = _cheerAge >= 0f ? CheerPose(_cheerAge / CheerSeconds) : (0f, 0f);
            (float slump, float shake) = _sulkAge >= 0f ? SulkPose(_sulkAge / SulkSeconds) : (0f, 0f);

            // Breathing and slumping keep the volume: a little taller and thinner, or shorter and wider.
            float tall = Breath(Time.time, _phase) - SulkDrop * slump;
            float wide = 1f / Mathf.Sqrt(tall);
            transform.localScale = new Vector3(_size.x * wide, _size.y * tall, _size.z * wide);
            transform.localPosition = Vector3.up * height;
            // The figure's own frame: +Z is where it looks, so a lean forward is a turn about its +X.
            transform.localRotation = Quaternion.AngleAxis(spin + shake, Vector3.up) * _facing * Quaternion.Euler(lean + SulkBowDegrees * slump, 0f, sway);
        }
    }
}
