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
        private static readonly Color ShadowColor = new Color(0.05f, 0.17f, 0.25f);

        /// <summary>At most this far from square to the camera a settled die is turned, so its pips read upright.</summary>
        public const int FacingJitterDegrees = 14;

        private readonly Transform[] _dice = new Transform[2];
        private readonly Transform[] _shadows = new Transform[2];
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
                view._shadows[i] = CreateShadow(root.transform, view._rest[i]);
            }

            return view;
        }

        /// <summary>The yaw a settled die ends at: square to the camera, give or take a little, so the pips read upright.</summary>
        public static float RestingYaw(float cameraYaw, int jitterDegrees) => cameraYaw + Mathf.Clamp(jitterDegrees, -FacingJitterDegrees, FacingJitterDegrees);

        /// <summary>Tumbles both dice and leaves <paramref name="first"/> and <paramref name="second"/> on top, squared up to a camera turned by <paramref name="cameraYaw"/>.</summary>
        public IEnumerator Roll(int first, int second, float cameraYaw = 0f)
        {
            Quaternion[] start = { _dice[0].localRotation, _dice[1].localRotation };
            Quaternion[] end =
            {
                DieFaces.ShowOnTop(first, RestingYaw(cameraYaw, _look.Next(-FacingJitterDegrees, FacingJitterDegrees + 1))),
                DieFaces.ShowOnTop(second, RestingYaw(cameraYaw, _look.Next(-FacingJitterDegrees, FacingJitterDegrees + 1))),
            };
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
                    float height = Mathf.Sin(t * Mathf.PI) * HopHeight * (1f - t * 0.4f);
                    _dice[i].localPosition = _rest[i] + Vector3.up * height;
                    PlaceShadow(i, height);
                }

                yield return null;
            }

            for (int i = 0; i < 2; i++)
            {
                _dice[i].localRotation = end[i];
                _dice[i].localPosition = _rest[i];
                PlaceShadow(i, 0f);
            }
        }

        /// <summary>The shadow under a die: a dark disc on the table that shrinks and fades as the die rises.</summary>
        public static float ShadowScale(float height) => Size * 0.62f * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(height / HopHeight));

        private void PlaceShadow(int index, float height)
        {
            Transform shadow = _shadows[index];
            float scale = ShadowScale(height);
            shadow.localScale = new Vector3(scale, 1f, scale);
            // Light comes from the front left, so the shadow trails a little behind and to the right as the die climbs.
            shadow.localPosition = new Vector3(_rest[index].x + height * 0.15f, -0.008f, height * 0.1f);
        }

        private static Transform CreateShadow(Transform parent, Vector3 rest)
        {
            GameObject shadow = ModelParts.Create("Shadow", parent, MeshFactory.Disc(0.004f, 24), ShadowColor, 0.05f);
            shadow.transform.localPosition = new Vector3(rest.x, -0.008f, 0f);
            float scale = ShadowScale(0f);
            shadow.transform.localScale = new Vector3(scale, 1f, scale);
            return shadow.transform;
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
