using Lazpoly.Presentation.Effects;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// A player's money hatch on the table, the owner's wish (Faz 25): paid money goes down through the payer's hatch, and
    /// money a player gets comes up through theirs. A metal frame round a dark hole, and a lid in the player's colour hinged on
    /// its back edge (away from the camera, so the open lid never hides the hole). <see cref="Swing"/> opens it at one moment
    /// and closes it at another; the swing itself is pure (<see cref="OpennessAt"/>).
    /// </summary>
    public sealed class MoneyHatch : MonoBehaviour
    {
        /// <summary>The hole: long enough for a money stack lying as it lies in the case, along the hatch's x.</summary>
        public const float HoleLength = 0.9f;
        public const float HoleWidth = 0.6f;

        /// <summary>The frame round the hole, how high it stands on the table, and the lid's thickness.</summary>
        public const float Rim = 0.08f;
        public const float FrameHeight = 0.04f;
        public const float LidThickness = 0.04f;

        /// <summary>How far the lid swings open (a little past upright, leaning back) and how long a swing takes at normal speed.</summary>
        public const float OpenAngle = 105f;
        public const float SwingSeconds = 0.25f;

        public static readonly Color Metal = new Color(0.55f, 0.57f, 0.6f);
        public static readonly Color HoleDark = new Color(0.05f, 0.05f, 0.06f);

        private Transform _lid;
        private float _openAt = -1f;
        private float _closeAt = -1f;
        private float _age;
        private float _speed = 1f;

        /// <summary>The whole hatch's size on the table: the hole and its rim.</summary>
        public static Vector3 Size => new Vector3(HoleLength + 2f * Rim, FrameHeight, HoleWidth + 2f * Rim);

        /// <summary>How open the lid is now, from 0 (shut) to 1 (open).</summary>
        public float Openness { get; private set; }

        /// <summary>The lid, turning on its hinge.</summary>
        public Transform Lid => _lid;

        /// <summary>The middle of the hole at the frame's top (world space): money goes down and comes up here.</summary>
        public Vector3 Mouth => transform.TransformPoint(new Vector3(0f, FrameHeight, 0f));

        public static MoneyHatch Create(Transform parent, string name, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Vector3 size = Size;
            float side = Rim;
            Part(root.transform, "Frame front", Metal, new Vector3(size.x, FrameHeight, side), new Vector3(0f, FrameHeight / 2f, -(size.z - side) / 2f));
            Part(root.transform, "Frame back", Metal, new Vector3(size.x, FrameHeight, side), new Vector3(0f, FrameHeight / 2f, (size.z - side) / 2f));
            Part(root.transform, "Frame left", Metal, new Vector3(side, FrameHeight, HoleWidth), new Vector3(-(size.x - side) / 2f, FrameHeight / 2f, 0f));
            Part(root.transform, "Frame right", Metal, new Vector3(side, FrameHeight, HoleWidth), new Vector3((size.x - side) / 2f, FrameHeight / 2f, 0f));
            Part(root.transform, "Hole", HoleDark, new Vector3(HoleLength, 0.004f, HoleWidth), new Vector3(0f, 0.003f, 0f), 0f);

            // The hinge runs along the back of the hole; the lid lies forward of it over the hole when shut.
            var hinge = new GameObject("Lid");
            hinge.transform.SetParent(root.transform, false);
            hinge.transform.localPosition = new Vector3(0f, FrameHeight, HoleWidth / 2f + Rim / 2f);
            Part(hinge.transform, "Lid plate", color, new Vector3(HoleLength + Rim, LidThickness, HoleWidth + Rim),
                new Vector3(0f, LidThickness / 2f, -(HoleWidth + Rim) / 2f), 0.4f);
            Part(hinge.transform, "Lid grip", Metal, new Vector3(0.22f, 0.03f, 0.05f), new Vector3(0f, LidThickness + 0.015f, -(HoleWidth + Rim) + 0.08f), 0.6f);

            var hatch = root.AddComponent<MoneyHatch>();
            hatch._lid = hinge.transform;
            return hatch;
        }

        /// <summary>
        /// Opens the lid <paramref name="openAt"/> seconds from now and shuts it <paramref name="closeAt"/> seconds from now
        /// (normal-speed seconds, played at <paramref name="speed"/>).
        /// </summary>
        public void Swing(float openAt, float closeAt, float speed)
        {
            _openAt = openAt;
            _closeAt = Mathf.Max(closeAt, openAt + SwingSeconds);
            _speed = Mathf.Max(speed, 0.1f);
            _age = 0f;
        }

        /// <summary>How open the lid is <paramref name="t"/> seconds in: it swings open from <paramref name="openAt"/>, stays, and swings shut from <paramref name="closeAt"/>.</summary>
        public static float OpennessAt(float t, float openAt, float closeAt)
        {
            if (t <= openAt) return 0f;
            if (t < openAt + SwingSeconds) return Ease.InOutCubic((t - openAt) / SwingSeconds);
            if (t <= closeAt) return 1f;
            if (t < closeAt + SwingSeconds) return 1f - Ease.InOutCubic((t - closeAt) / SwingSeconds);
            return 0f;
        }

        /// <summary>Sets the lid's angle for an openness from 0 to 1 (kept public so a test can check the swing).</summary>
        public void Show(float openness)
        {
            Openness = Mathf.Clamp01(openness);
            if (_lid != null) _lid.localRotation = Quaternion.Euler(OpenAngle * Openness, 0f, 0f);
        }

        private void Update()
        {
            if (_openAt < 0f) return;
            _age += Time.deltaTime * _speed;
            Show(OpennessAt(_age, _openAt, _closeAt));
            if (_age >= _closeAt + SwingSeconds) _openAt = -1f;
        }

        private static void Part(Transform parent, string name, Color color, Vector3 size, Vector3 at, float smoothness = 0.5f)
        {
            GameObject part = ModelParts.Create(name, parent, MeshFactory.RoundedBox(size, Mathf.Min(0.01f, size.y / 2f), 2), color, smoothness);
            part.transform.localPosition = at;
        }
    }
}
