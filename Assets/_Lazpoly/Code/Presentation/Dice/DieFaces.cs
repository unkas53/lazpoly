using System;
using UnityEngine;

namespace Lazpoly.Presentation.Dice
{
    /// <summary>
    /// Which face of a die shows which number (opposite faces add up to 7), and the rotation that
    /// turns a given number to the top.
    /// </summary>
    public static class DieFaces
    {
        /// <summary>Outward normal of the face that shows <paramref name="value"/>, in the die's own space.</summary>
        public static Vector3 Normal(int value)
        {
            switch (value)
            {
                case 1: return Vector3.up;
                case 2: return Vector3.back;
                case 3: return Vector3.right;
                case 4: return Vector3.left;
                case 5: return Vector3.forward;
                case 6: return Vector3.down;
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "A die shows 1 to 6.");
            }
        }

        /// <summary>A rotation that leaves <paramref name="value"/> on top, turned by <paramref name="yawDegrees"/>.</summary>
        public static Quaternion ShowOnTop(int value, float yawDegrees)
        {
            Vector3 normal = Normal(value);
            Quaternion toUp = value == 6 ? Quaternion.AngleAxis(180f, Vector3.right) : Quaternion.FromToRotation(normal, Vector3.up);
            return Quaternion.AngleAxis(yawDegrees, Vector3.up) * toUp;
        }

        /// <summary>Pip positions on a face, in the face's own 2D coordinates (-1..1).</summary>
        public static Vector2[] Pips(int value)
        {
            const float o = 0.55f;
            switch (value)
            {
                case 1: return new[] { Vector2.zero };
                case 2: return new[] { new Vector2(-o, -o), new Vector2(o, o) };
                case 3: return new[] { new Vector2(-o, -o), Vector2.zero, new Vector2(o, o) };
                case 4: return new[] { new Vector2(-o, -o), new Vector2(o, -o), new Vector2(-o, o), new Vector2(o, o) };
                case 5: return new[] { new Vector2(-o, -o), new Vector2(o, -o), Vector2.zero, new Vector2(-o, o), new Vector2(o, o) };
                case 6: return new[] { new Vector2(-o, -o), new Vector2(o, -o), new Vector2(-o, 0f), new Vector2(o, 0f), new Vector2(-o, o), new Vector2(o, o) };
                default: throw new ArgumentOutOfRangeException(nameof(value), value, "A die shows 1 to 6.");
            }
        }
    }
}
