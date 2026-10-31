using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// Small coloured cards thrown over the board when the game is won. Plain meshes moved in code,
    /// so it needs no particle material in the build; the pieces settle on the table and fade away.
    /// </summary>
    public sealed class Confetti : MonoBehaviour
    {
        private const int Count = 90;
        private const float Gravity = 7f;
        private const float LifeSeconds = 6f;

        private readonly List<Piece> _pieces = new List<Piece>();
        private float _floorY;
        private float _age;

        private sealed class Piece
        {
            public Transform Transform;
            public Vector3 Velocity;
            public Vector3 Spin;
            public bool Resting;
        }

        /// <summary>Throws confetti up from <paramref name="center"/> in the given colours; it lands at <paramref name="floorY"/>.</summary>
        public static Confetti Burst(Transform parent, Vector3 center, float floorY, IReadOnlyList<Color> colors)
        {
            var root = new GameObject("Confetti");
            root.transform.SetParent(parent, false);
            var confetti = root.AddComponent<Confetti>();
            confetti._floorY = floorY;

            Mesh card = MeshFactory.RoundedBox(new Vector3(0.16f, 0.01f, 0.1f), 0.004f, 1);
            var random = new System.Random();
            for (int i = 0; i < Count; i++)
            {
                Color color = colors.Count > 0 ? colors[i % colors.Count] : Color.white;
                GameObject part = ModelParts.Create("Confetto", root.transform, card, color, 0.6f);
                part.transform.position = center + new Vector3(Range(random, -0.6f, 0.6f), Range(random, 0f, 0.4f), Range(random, -0.6f, 0.6f));
                part.transform.rotation = Random.rotationUniform;
                float angle = Range(random, 0f, Mathf.PI * 2f);
                float spread = Range(random, 1f, 3.2f);
                confetti._pieces.Add(new Piece
                {
                    Transform = part.transform,
                    Velocity = new Vector3(Mathf.Cos(angle) * spread, Range(random, 5f, 8f), Mathf.Sin(angle) * spread),
                    Spin = new Vector3(Range(random, -540f, 540f), Range(random, -360f, 360f), Range(random, -540f, 540f)),
                });
            }

            return confetti;
        }

        private static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        private void Update()
        {
            float dt = Time.deltaTime;
            _age += dt;
            if (_age >= LifeSeconds)
            {
                Destroy(gameObject);
                return;
            }

            foreach (Piece piece in _pieces)
            {
                if (piece.Resting) continue;
                // Paper falls slowly: strong drag once it is coming down.
                piece.Velocity.y -= Gravity * dt;
                if (piece.Velocity.y < 0f) piece.Velocity *= 1f - Mathf.Min(1f, 2.2f * dt);
                Transform t = piece.Transform;
                t.position += piece.Velocity * dt;
                t.Rotate(piece.Spin * dt, Space.Self);
                if (t.position.y > _floorY) continue;

                piece.Resting = true;
                t.position = new Vector3(t.position.x, _floorY, t.position.z);
                t.rotation = Quaternion.Euler(0f, t.eulerAngles.y, 0f);
            }

            // Shrink away during the last second.
            float fade = Mathf.Clamp01(LifeSeconds - _age);
            if (fade >= 1f) return;
            foreach (Piece piece in _pieces) piece.Transform.localScale = Vector3.one * fade;
        }
    }
}
