using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// Thin smoke from a mansion's chimney: small grey puffs that rise, swell and fade out in size.
    /// Four puffs are kept and reused; the component runs only while the mansion is shown.
    /// </summary>
    public sealed class ChimneySmoke : MonoBehaviour
    {
        public const float PuffSeconds = 1.6f;
        public const float Interval = 0.55f;
        private const int Puffs = 4;
        private const float Rise = 0.55f;
        private const float StartSize = 0.07f;
        private const float EndSize = 0.2f;

        private static readonly Color SmokeColor = new Color(0.78f, 0.78f, 0.80f);

        private readonly Transform[] _puffs = new Transform[Puffs];
        private readonly float[] _age = new float[Puffs];
        private Vector3 _mouth;
        private float _untilNext;
        private int _next;

        /// <summary>Puts smoke on <paramref name="root"/>, rising from <paramref name="chimneyTop"/> (in the root's local space, unit model).</summary>
        public static ChimneySmoke Attach(GameObject root, Vector3 chimneyTop)
        {
            var smoke = root.AddComponent<ChimneySmoke>();
            smoke._mouth = chimneyTop;
            for (int i = 0; i < Puffs; i++)
            {
                GameObject puff = ModelParts.Create("Smoke", root.transform, MeshFactory.Sphere(10), SmokeColor, 0.05f);
                puff.SetActive(false);
                smoke._puffs[i] = puff.transform;
            }

            return smoke;
        }

        private void OnEnable()
        {
            _untilNext = Interval * 0.5f;
            foreach (Transform puff in _puffs) if (puff != null) puff.gameObject.SetActive(false);
        }

        private void Update()
        {
            _untilNext -= Time.deltaTime;
            if (_untilNext <= 0f)
            {
                _untilNext += Interval;
                _age[_next] = 0f;
                _puffs[_next].gameObject.SetActive(true);
                _next = (_next + 1) % Puffs;
            }

            for (int i = 0; i < Puffs; i++)
            {
                Transform puff = _puffs[i];
                if (!puff.gameObject.activeSelf) continue;
                _age[i] += Time.deltaTime;
                float t = _age[i] / PuffSeconds;
                if (t >= 1f)
                {
                    puff.gameObject.SetActive(false);
                    continue;
                }

                // Drifts up and a little sideways; grows while it is young, then shrinks to nothing.
                float drift = Mathf.Sin(t * 5f + i) * 0.05f;
                puff.localPosition = _mouth + new Vector3(drift, Rise * t, drift * 0.5f);
                // Puffs stay round whatever shape the mansion was scaled to: the size follows the model's height.
                Vector3 parentScale = transform.lossyScale;
                float size = Mathf.Lerp(StartSize, EndSize, Mathf.Sqrt(t)) * (1f - t * t) * Mathf.Max(parentScale.y, 1e-4f);
                puff.localScale = new Vector3(size / Mathf.Max(parentScale.x, 1e-4f), size / Mathf.Max(parentScale.y, 1e-4f), size / Mathf.Max(parentScale.z, 1e-4f));
            }
        }
    }
}
