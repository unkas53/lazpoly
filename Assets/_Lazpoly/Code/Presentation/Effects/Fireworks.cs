using System.Collections.Generic;
using Lazpoly.Presentation.Audio;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// Fireworks over a trade agreement's towns (the owner's wish, Faz 27), played on the timeline of
    /// <see cref="FireworkShow"/>: a rocket with a short trail of embers climbs from each town's tile and bursts into
    /// sparks in the owner's colour and gold, which fall, twinkle and shrink away. Plain unlit meshes moved in code, so
    /// no particle system or material beyond <see cref="SparkMaterialResource"/> is needed; each rocket's pieces are made
    /// when it is launched and the whole show removes itself when it is over.
    /// </summary>
    public sealed class Fireworks : MonoBehaviour
    {
        public const string SparkMaterialResource = "Lazpoly/Materials/Spark";
        private const int Embers = 5;
        private const float EmberLag = 0.035f;
        private const float HeadSize = 0.09f;
        private const float SparkSize = 0.075f;

        /// <summary>Unlit colours above 1 glow where bloom is on (High quality) and read as plain bright colours elsewhere.</summary>
        private const float Glow = 1.6f;

        private static readonly Color Gold = new Color(1f, 0.8f, 0.32f);
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static Material _sparkMaterial;
        private static bool _loaded;

        private readonly List<Rocket> _rockets = new List<Rocket>();
        private float _age;
        private float _seconds;
        private Color _tint;

        private sealed class Rocket
        {
            public int Town;
            public int Index;
            public Vector3 Tile;
            public Vector3 Inward;
            public Transform Head;
            public readonly List<Transform> Trail = new List<Transform>();
            public readonly List<Transform> Sparks = new List<Transform>();
            public List<Vector3> Directions;
            public Vector3 BurstPoint;
            public float Speed;
            public bool Launched;
            public bool Burst;
        }

        /// <summary>
        /// Starts the show over <paramref name="tiles"/> (each town's tile top, in world space, in the order the towns go up),
        /// the rockets drifting towards <paramref name="middle"/>; sparks in <paramref name="tint"/> and gold.
        /// </summary>
        public static Fireworks Launch(Transform parent, IReadOnlyList<Vector3> tiles, Vector3 middle, Color tint)
        {
            var root = new GameObject("Fireworks");
            root.transform.SetParent(parent, false);
            var show = root.AddComponent<Fireworks>();
            show._tint = tint;
            show._seconds = FireworkShow.Seconds(tiles.Count);
            List<Vector3> directions = FireworkShow.SparkDirections(FireworkShow.SparksPerBurst);
            for (int town = 0; town < tiles.Count; town++)
            {
                Vector3 inward = middle - tiles[town];
                inward.y = 0f;
                inward = inward.sqrMagnitude > 0.0001f ? inward.normalized : Vector3.zero;
                for (int rocket = 0; rocket < FireworkShow.RocketsPerTown; rocket++)
                {
                    show._rockets.Add(new Rocket
                    {
                        Town = town, Index = rocket, Tile = tiles[town], Inward = inward, Directions = directions,
                        Speed = FireworkShow.BurstSpeed(town, rocket),
                    });
                }
            }

            return show;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _seconds)
            {
                Destroy(gameObject);
                return;
            }

            foreach (Rocket rocket in _rockets) Play(rocket);
        }

        private void Play(Rocket rocket)
        {
            float launch = FireworkShow.LaunchAt(rocket.Town, rocket.Index);
            float t = _age - launch;
            if (t < 0f) return;
            if (!rocket.Launched) LaunchRocket(rocket);

            if (t < FireworkShow.RiseSeconds)
            {
                rocket.Head.position = FireworkShow.RocketPosition(rocket.Tile, rocket.Inward, t);
                for (int i = 0; i < rocket.Trail.Count; i++)
                {
                    float back = t - (i + 1) * EmberLag;
                    rocket.Trail[i].gameObject.SetActive(back > 0f);
                    if (back > 0f) rocket.Trail[i].position = FireworkShow.RocketPosition(rocket.Tile, rocket.Inward, back);
                }

                return;
            }

            if (!rocket.Burst) BurstRocket(rocket);
            float age = t - FireworkShow.RiseSeconds;
            for (int i = 0; i < rocket.Sparks.Count; i++)
            {
                Transform spark = rocket.Sparks[i];
                float size = FireworkShow.SparkSize(age, i);
                spark.gameObject.SetActive(size > 0f);
                if (size <= 0f) continue;
                spark.position = rocket.BurstPoint + FireworkShow.SparkOffset(rocket.Directions[i], rocket.Speed, age);
                spark.localScale = Vector3.one * (SparkSize * size);
            }
        }

        private void LaunchRocket(Rocket rocket)
        {
            rocket.Launched = true;
            Mesh ball = MeshFactory.Sphere(4);
            rocket.Head = Piece("Rocket", ball, Color.Lerp(Gold, Color.white, 0.5f), HeadSize).transform;
            rocket.Head.position = rocket.Tile;
            for (int i = 0; i < Embers; i++)
            {
                float fade = 1f - (i + 1f) / (Embers + 1f);
                Transform ember = Piece("Ember", ball, Gold, HeadSize * (0.4f + 0.5f * fade)).transform;
                ember.gameObject.SetActive(false);
                rocket.Trail.Add(ember);
            }
        }

        private void BurstRocket(Rocket rocket)
        {
            rocket.Burst = true;
            rocket.BurstPoint = rocket.Head.position;
            Destroy(rocket.Head.gameObject);
            foreach (Transform ember in rocket.Trail) Destroy(ember.gameObject);
            rocket.Trail.Clear();

            Mesh ball = MeshFactory.Sphere(4);
            Color bright = Color.Lerp(_tint, Color.white, 0.3f);
            for (int i = 0; i < rocket.Directions.Count; i++)
            {
                // Every third spark is gold; the second rocket of a town swaps the colours round.
                bool gold = (i + rocket.Index) % 3 == 0;
                Transform spark = Piece("Spark", ball, gold ? Gold : bright, SparkSize).transform;
                spark.position = rocket.BurstPoint;
                rocket.Sparks.Add(spark);
            }

            AudioPlayer.Play(SoundCue.Firework);
        }

        private GameObject Piece(string name, Mesh mesh, Color color, float size)
        {
            Material material = MaterialFor(color);
            GameObject piece;
            if (material == null)
            {
                piece = ModelParts.Create(name, transform, mesh, color, 0.8f);
            }
            else
            {
                piece = new GameObject(name);
                piece.transform.SetParent(transform, false);
                piece.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = piece.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            piece.transform.localScale = Vector3.one * size;
            return piece;
        }

        /// <summary>One unlit material per colour, shared by every show; null when the spark material is missing.</summary>
        private static Material MaterialFor(Color color)
        {
            if (!_loaded)
            {
                _loaded = true;
                _sparkMaterial = Resources.Load<Material>(SparkMaterialResource);
                if (_sparkMaterial == null) Debug.LogWarning($"[Lazpoly] {SparkMaterialResource} is missing; fireworks use the model material.");
            }

            if (_sparkMaterial == null) return null;
            if (Materials.TryGetValue(color, out Material material) && material != null) return material;
            material = new Material(_sparkMaterial) { name = "Spark" };
            Color glowing = color * Glow;
            glowing.a = 1f;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", glowing);
            material.color = glowing;
            Materials[color] = material;
            return material;
        }
    }
}
