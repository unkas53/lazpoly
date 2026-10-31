using System.Collections;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Dice
{
    /// <summary>
    /// Two dice in the middle of the board. They only show the result the rules already decided:
    /// they tumble for a moment and settle on the rolled numbers.
    /// </summary>
    public sealed class DiceView : MonoBehaviour
    {
        private const float Size = 0.7f;
        private const float RollSeconds = 0.8f;
        private const float HopHeight = 1.2f;

        private static readonly Color BodyColor = new Color(0.96f, 0.95f, 0.91f);
        private static readonly Color PipColor = new Color(0.12f, 0.12f, 0.14f);

        private readonly Transform[] _dice = new Transform[2];
        private readonly Vector3[] _rest = new Vector3[2];
        private readonly System.Random _look = new System.Random();

        /// <summary>Animation speed multiplier from the settings (1 = normal).</summary>
        public float Speed { get; set; } = 1f;

        public static DiceView Create(Transform parent, Vector3 center)
        {
            var root = new GameObject("Dice");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;
            var view = root.AddComponent<DiceView>();
            for (int i = 0; i < 2; i++)
            {
                view._rest[i] = new Vector3((i == 0 ? -1f : 1f) * Size * 0.9f, Size / 2f, 0f);
                view._dice[i] = CreateDie(root.transform, i);
                view._dice[i].localPosition = view._rest[i];
                view._dice[i].localRotation = DieFaces.ShowOnTop(i == 0 ? 1 : 6, 15f);
            }

            return view;
        }

        /// <summary>Tumbles both dice and leaves <paramref name="first"/> and <paramref name="second"/> on top.</summary>
        public IEnumerator Roll(int first, int second)
        {
            Quaternion[] start = { _dice[0].localRotation, _dice[1].localRotation };
            Quaternion[] end = { DieFaces.ShowOnTop(first, _look.Next(-25, 25)), DieFaces.ShowOnTop(second, _look.Next(-25, 25)) };
            Vector3[] spin = { RandomAxis() * 720f, RandomAxis() * 900f };

            float duration = RollSeconds / Mathf.Max(Speed, 0.1f);
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                for (int i = 0; i < 2; i++)
                {
                    // Free spin first, then ease into the final face.
                    float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, t));
                    Quaternion tumbling = start[i] * Quaternion.Euler(spin[i] * t);
                    _dice[i].localRotation = Quaternion.Slerp(tumbling, end[i], settle);
                    _dice[i].localPosition = _rest[i] + Vector3.up * (Mathf.Sin(t * Mathf.PI) * HopHeight * (1f - t * 0.4f));
                }

                yield return null;
            }

            for (int i = 0; i < 2; i++)
            {
                _dice[i].localRotation = end[i];
                _dice[i].localPosition = _rest[i];
            }
        }

        private Vector3 RandomAxis() => new Vector3(_look.Next(-100, 100), _look.Next(-100, 100), _look.Next(-100, 100)).normalized + Vector3.right * 0.01f;

        private static Transform CreateDie(Transform parent, int index)
        {
            GameObject die = ModelParts.Create($"Die {index + 1}", parent,
                MeshFactory.RoundedBox(Vector3.one * Size, Size * 0.18f), BodyColor, 0.6f);

            Mesh pipMesh = MeshFactory.Lathe("pip", new[]
            {
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0.45f, 0.3f), new Vector2(0f, 0.35f),
            }, 12);
            for (int value = 1; value <= 6; value++)
            {
                Vector3 normal = DieFaces.Normal(value);
                Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(normal, u);
                foreach (Vector2 pip in DieFaces.Pips(value))
                {
                    GameObject dot = ModelParts.Create("Pip", die.transform, pipMesh, PipColor, 0.1f);
                    Transform t = dot.transform;
                    // Slightly raised dots on the flat middle of each face.
                    t.localPosition = (normal * 0.48f + (u * pip.x + v * pip.y) * 0.3f) * Size;
                    t.localRotation = Quaternion.FromToRotation(Vector3.up, normal);
                    t.localScale = new Vector3(0.16f, 0.08f, 0.16f);
                }
            }

            return die.transform;
        }
    }
}
