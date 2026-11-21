using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// What is in the air as a building leaves (the owner's wishes, 2026-11-17 and 18: professional, in keeping with the
    /// game, and dollar signs for the gold glints). Sold: a ring of gold spreads over the ground, the building is gone
    /// in a flash and a puff of white cloud, and dollar signs spring up out of it, gold and money green, hang, and
    /// fade. Torn down: puffs at the jolt, then cartoon dust bursting out from the foot and up its height, a ring over
    /// the ground, grit, and rubble in the colours of its walls and roof thrown out to bounce and lie. The clouds are the game's
    /// kind: clean shapes of two tones that pop out and dwindle away, not smoke. Everything see-through is a quad of one
    /// mesh that faces the eye (<see cref="SoftBurst"/>); the rubble is solid and casts shadows. It clears itself away.
    /// </summary>
    public sealed class LeavingBurst : SoftBurst
    {
        public const int Poofs = 9;
        public const int Dollars = 11;
        public const int Kicks = 4;
        public const int Clouds = 12;
        public const int Plumes = 8;
        public const int Caps = 4;
        public const int Grit = 10;
        public const int Wisps = 3;
        public const int Rubble = 16;

        /// <summary>A building blown up (the owner's missile card): the dust round its foot, the last wisps, and more rubble, thrown higher and further.</summary>
        public const int BlastClouds = 8;
        public const int BlastWisps = 4;
        public const int BlastRubble = 22;

        /// <summary>
        /// The quads of each leaving (a cloud and a dollar sign are two each: its body and its lit side or rim). Sold: two
        /// rings, the flash, the puff, the dollar signs. Torn down: the jolt's puffs, a ring, the clouds, the plumes, the
        /// caps, the grit, the last wisps.
        /// </summary>
        public const int SoldParticles = 2 + 1 + 2 * Poofs + 2 * Dollars;
        public const int DemolishedParticles = 2 * Kicks + 1 + 2 * Clouds + 2 * Plumes + 2 * Caps + Grit + 2 * Wisps;

        /// <summary>Blown up: a ring of dust, the clouds round the foot and the wisps (the fire is the missile's own, <see cref="MissileStrike"/>).</summary>
        public const int BlastedParticles = 1 + 2 * BlastClouds + 2 * BlastWisps;

        /// <summary>How the rubble falls, bounces and lies, in widths of the square (per second where it is a speed).</summary>
        public const float Gravity = 7.5f;
        public const float Bounce = 0.32f;
        public const float Grip = 0.5f;
        public const float RubbleSeconds = 1.3f;
        public const float RubbleGone = 0.3f;

        /// <summary>How far from the middle of the building the dust up its walls starts, in its widths: at its walls.</summary>
        public const float PlumeReach = 0.6f;

        /// <summary>How much of its height the dust at the walls comes down in its life, and the dust before the building: they fall in with it.</summary>
        public const float PlumeSinks = 0.6f;
        public const float CapSinks = 0.75f;

        /// <summary>The longest step the rubble is moved in one go, so a slow frame cannot throw it through the ground.</summary>
        public const float Tick = 1f / 90f;

        private sealed class Chunk
        {
            public Transform Piece;
            public Vector3 Place;
            public Vector3 Speed;
            public Vector3 Axis;
            public float Whirl;
            public float Turned;
            public float Born;
            public float Size;
            public Vector3 Shape;
            public float Floor;
        }

        private readonly List<Chunk> _chunks = new List<Chunk>();
        private readonly List<Material> _materials = new List<Material>();
        private readonly Material[] _tones = new Material[3];
        private Leaving _how;
        private float _tile;
        private float _bound;

        /// <summary>The kind of burst.</summary>
        public Leaving How => _how;

        /// <summary>Its rubble.</summary>
        public int RubbleCount => _chunks.Count;

        /// <summary>Where piece <paramref name="index"/> of the rubble is now, and whether it is shown yet.</summary>
        public Vector3 RubblePlace(int index) => _chunks[index].Place;
        public bool RubbleShown(int index) => _chunks[index].Piece.gameObject.activeSelf;

        /// <summary>
        /// Starts the burst of a building leaving, under <paramref name="parent"/> and in its frame: the building stands
        /// with the middle of its foot at <paramref name="foot"/>, <paramref name="width"/> across and
        /// <paramref name="height"/> high, on a square <paramref name="tile"/> wide; a sold one rises by
        /// <paramref name="lift"/> as it goes. The rubble takes <paramref name="wall"/>'s colour, and where one is given
        /// a third of it <paramref name="roof"/>'s; <paramref name="seed"/> settles the scatter, so the same building
        /// always leaves the same way. No rubble goes further from the parent's middle than <paramref name="bound"/>
        /// along either side (the table's edge), where one is given.
        /// </summary>
        public static LeavingBurst Play(Transform parent, Leaving how, Vector3 foot, float width, float height, float tile, float lift, Color wall, int seed, float bound = 0f, Color? roof = null)
        {
            var root = new GameObject(how == Leaving.Demolished ? "Demolition dust" : how == Leaving.Blasted ? "Blast rubble" : "Sale puff");
            root.transform.SetParent(parent, false);
            var burst = root.AddComponent<LeavingBurst>();
            burst._how = how;
            burst._tile = tile;
            burst._bound = bound;
            burst.Begin(seed);
            if (how == Leaving.Demolished) burst.LayDemolition(foot, width, height, tile, wall, roof);
            else if (how == Leaving.Blasted) burst.LayBlast(foot, width, height, tile, wall, roof);
            else burst.LaySale(foot, width, height, tile, lift);
            foreach (Chunk chunk in burst._chunks) burst.Outlast(chunk.Born + RubbleSeconds + RubbleGone);
            burst.Ready("Leaving burst");
            return burst;
        }

        private void LaySale(Vector3 foot, float width, float height, float tile, float lift)
        {
            float pop = LeavingMotion.SoldSeconds * LeavingMotion.PopAt;
            // The middle of the building as it pops, sprung up tall off its tile: where it dwindles to.
            Vector3 heart = foot + Vector3.up * (lift * LeavingMotion.SpringLift + height * (1f + LeavingMotion.Spring) * 0.5f);

            // Two rings of gold over the ground as it pops, the second fainter and a breath later.
            for (int i = 0; i < 2; i++)
            {
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Ring, Flat = true, Born = pop - 0.04f + 0.11f * i, Life = 0.55f,
                    From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width * 0.8f, SizeTo = Mathf.Min(tile * 1.15f, Mathf.Max(width * 2.4f, tile * 0.8f)),
                    Color = i == 0 ? Gold : PaleGold, Alpha = i == 0 ? 1f : 0.65f, FadeIn = 0.12f, FadeFrom = 0.35f,
                });
            }

            // The flash it goes in.
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Dot, Born = pop - 0.05f, Life = 0.4f, From = heart,
                SizeFrom = width * 0.8f, SizeTo = Mathf.Min(tile * 1.4f, width * 2.6f),
                Color = Light, Alpha = 0.95f, FadeIn = 0.12f, FadeFrom = 0.3f,
            });

            // The puff that takes it: one cloud where it stood and a wreath of them bursting out, popping and dwindling.
            for (int i = 0; i < Poofs; i++)
            {
                bool middle = i == 0;
                Vector3 way = Round(i, Poofs - 1, 0.3f);
                float up = Roll(-0.15f, 0.75f);
                Vector3 off = middle ? Vector3.zero : (way * Mathf.Sqrt(1f - up * up * 0.6f) + Vector3.up * up).normalized;
                float size = (middle ? Roll(0.5f, 0.56f) : Roll(0.3f, 0.42f)) * tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = pop - 0.03f + (middle ? 0f : Roll(0f, 0.07f)), Life = middle ? 0.5f : Roll(0.5f, 0.7f),
                    From = heart + off * (width * 0.2f), Drift = off * (Roll(0.2f, 0.36f) * tile), Rise = 0.06f * tile,
                    SizeFrom = size * 0.6f, SizeTo = size, Pop = 0.22f, ShrinkFrom = 0.4f,
                    Color = Puff, Alpha = 1f, FadeFrom = 0.9f, Turn = Roll(-18f, 18f), Fore = Fore(width, height),
                }, SoftSprite.CloudLight, PuffLight);
            }

            // The dollar signs: up out of the puff one after another, popping in, hanging, drooping a little as they fade.
            for (int i = 0; i < Dollars; i++)
            {
                Vector3 way = Round(i, Dollars, 1.1f);
                bool green = i % 3 == 2;
                float size = Roll(0.26f, 0.38f) * tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.DollarEdge, Born = pop + 0.02f + 0.3f * i / Dollars + Roll(0f, 0.03f), Life = Roll(1f, 1.4f),
                    From = heart + way * (width * 0.15f),
                    Drift = way * (Roll(0.12f, 0.46f) * tile) + Vector3.up * (Roll(0.3f, 0.7f) * tile), Fall = Roll(0.06f, 0.16f) * tile,
                    SizeFrom = size, SizeTo = size, Pop = 0.16f, ShrinkFrom = 0.8f,
                    Color = green ? MoneyRim : GoldRim, Alpha = 1f, FadeFrom = 0.72f, Turn = Roll(-14f, 14f), Spin = Roll(-20f, 20f),
                }, SoftSprite.Dollar, green ? Money : Gold);
            }
        }

        private void LayDemolition(Vector3 foot, float width, float height, float tile, Color wall, Color? roof)
        {
            float drop = LeavingMotion.DemolishedSeconds * LeavingMotion.Jolt;

            // The jolt kicks small puffs out from under the walls.
            for (int i = 0; i < Kicks; i++)
            {
                Vector3 way = Round(i, Kicks, 0.9f);
                Dust(new SoftParticle
                {
                    Born = Roll(0f, 0.05f), Life = 0.45f,
                    From = foot + way * (width * 0.45f), Drift = way * (0.14f * tile), Rise = 0.04f * tile,
                    SizeFrom = 0.1f * tile, SizeTo = 0.22f * tile, Sit = 0.3f, Pop = 0.3f, ShrinkFrom = 0.4f,
                }, i);
            }

            // From here on the dust is drawn in front of the building: it is what the building goes down into.
            Near = Fore(width, height);

            // As it gives way a ring of dust runs out over the ground.
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Ring, Flat = true, Born = drop + 0.03f, Life = 0.5f,
                From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width, SizeTo = tile * 1.15f,
                Color = DustTones[1], Alpha = 0.7f, FadeIn = 0.1f, FadeFrom = 0.3f,
            });

            // The dust bursts out from the foot all round, each cloud a moment after the one before: out fast, swelling,
            // then dwindling where it has stopped.
            for (int i = 0; i < Clouds; i++)
            {
                Vector3 way = Round(i, Clouds, 0.4f);
                Dust(new SoftParticle
                {
                    Born = drop + 0.25f * i / Clouds + Roll(0f, 0.03f), Life = Roll(0.8f, 1.15f),
                    From = foot + way * (width * 0.35f), Drift = way * (Roll(0.12f, 0.24f) * tile), Rise = Roll(0.05f, 0.18f) * tile,
                    SizeFrom = 0.16f * tile, SizeTo = Roll(0.3f, 0.4f) * tile, Sit = 0.3f, Pop = 0.2f, ShrinkFrom = 0.5f,
                }, i);
            }

            // And up its walls all round, coming down with them, so it drops out of sight into dust, not into the open.
            for (int i = 0; i < Plumes; i++)
            {
                Vector3 way = Round(i, Plumes, 0.2f);
                float start = Roll(0.3f, 0.8f) * height;
                Dust(new SoftParticle
                {
                    Born = drop + 0.05f + 0.25f * i / Plumes + Roll(0f, 0.04f), Life = Roll(0.85f, 1.1f),
                    From = foot + way * (width * PlumeReach) + Vector3.up * start,
                    Drift = way * (Roll(0.02f, 0.08f) * tile), Rise = -start * PlumeSinks,
                    SizeFrom = 0.2f * tile, SizeTo = Roll(0.36f, 0.46f) * tile, Pop = 0.2f, ShrinkFrom = 0.55f,
                }, i + 1);
            }

            // And a column of it before the building itself, from near its foot to its roof, falling in as it does: the
            // middle of the cloud, which a ring of dust alone leaves open.
            for (int i = 0; i < Caps; i++)
            {
                float start = Mathf.Lerp(0.2f, 0.9f, i / (Caps - 1f)) * height;
                Dust(new SoftParticle
                {
                    Born = drop + 0.1f + 0.05f * i, Life = Roll(0.85f, 0.95f),
                    From = foot + new Vector3(Roll(-0.15f, 0.15f) * width, start, Roll(-0.15f, 0.15f) * width), Rise = -start * CapSinks,
                    SizeFrom = 0.22f * tile, SizeTo = Roll(0.4f, 0.46f) * tile, Pop = 0.2f, ShrinkFrom = 0.55f,
                }, i);
            }

            // Grit flung out of it, landing as it fades.
            for (int i = 0; i < Grit; i++)
            {
                Vector3 way = Round(i, Grit, 1.7f);
                float start = Roll(0.2f, 0.7f) * height;
                float thrown = Roll(0.2f, 0.5f) * tile;
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Dot, Born = drop + Roll(0f, 0.3f), Life = Roll(0.5f, 0.8f),
                    From = foot + Vector3.up * start, Drift = way * (Roll(0.4f, 0.7f) * tile) + Vector3.up * thrown,
                    Fall = start + thrown - tile * 0.02f, SizeFrom = 0.09f * tile, SizeTo = 0.05f * tile,
                    Color = GritTone, Alpha = 0.9f, FadeIn = 0.05f, FadeFrom = 0.8f,
                });
            }

            // The last of it: a few small clouds lifting off where it stood.
            for (int i = 0; i < Wisps; i++)
            {
                Dust(new SoftParticle
                {
                    Born = LeavingMotion.DemolishedSeconds - 0.1f + 0.12f * i, Life = 0.7f,
                    From = foot + new Vector3(Roll(-0.2f, 0.2f) * width, 0f, Roll(-0.2f, 0.2f) * width), Rise = 0.2f * tile,
                    SizeFrom = 0.12f * tile, SizeTo = 0.24f * tile, Sit = 0.3f, Pop = 0.25f, ShrinkFrom = 0.45f,
                }, i + 2);
            }

            // The rubble: broken slabs of wall, of its roof (or its darker courses) and of mortar, thrown up and out of the lower storeys.
            Color[] tones = RubbleTones(wall, roof);
            Mesh block = MeshFactory.RoundedBox(Vector3.one, 0.07f, 1);
            for (int i = 0; i < Rubble; i++)
            {
                Vector3 way = Round(i, Rubble, 1.1f);
                float size = Roll(0.045f, 0.09f) * tile;
                var chunk = new Chunk
                {
                    Piece = Block(block, tones, i % tones.Length),
                    Place = foot + new Vector3(Roll(-0.35f, 0.35f) * width, Roll(0.15f, 0.75f) * height, Roll(-0.35f, 0.35f) * width),
                    Speed = (way * Roll(0.25f, 0.7f) + Vector3.up * Roll(1.2f, 2.5f)) * tile,
                    Axis = new Vector3(Roll(-1f, 1f), Roll(-0.4f, 0.4f), Roll(-1f, 1f)).normalized,
                    Whirl = Roll(360f, 900f) * (i % 2 == 0 ? 1f : -1f),
                    Born = drop + Roll(0f, 0.3f),
                    Size = size,
                    Shape = new Vector3(1f, Roll(0.4f, 0.8f), Roll(0.55f, 1f)),
                    Floor = foot.y + size * 0.3f,
                };
                chunk.Piece.gameObject.SetActive(false);
                _chunks.Add(chunk);
            }
        }

        /// <summary>
        /// A building blown up: dust runs out over the ground and bursts from its foot at once, its rubble is thrown high
        /// and far (and kept on the table), and a few wisps lift off where it stood.
        /// </summary>
        private void LayBlast(Vector3 foot, float width, float height, float tile, Color wall, Color? roof)
        {
            Near = Fore(width, height);
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Ring, Flat = true, Born = 0.02f, Life = 0.55f,
                From = foot + Vector3.up * (tile * 0.012f), SizeFrom = width, SizeTo = tile * 1.5f,
                Color = DustTones[1], Alpha = 0.7f, FadeIn = 0.1f, FadeFrom = 0.3f,
            });

            for (int i = 0; i < BlastClouds; i++)
            {
                Vector3 way = Round(i, BlastClouds, 0.7f);
                Dust(new SoftParticle
                {
                    Born = 0.04f + Roll(0f, 0.08f), Life = Roll(0.7f, 1f),
                    From = foot + way * (width * 0.4f), Drift = way * (Roll(0.3f, 0.5f) * tile), Rise = Roll(0.04f, 0.14f) * tile,
                    SizeFrom = 0.16f * tile, SizeTo = Roll(0.28f, 0.38f) * tile, Sit = 0.3f, Pop = 0.2f, ShrinkFrom = 0.45f,
                }, i);
            }

            for (int i = 0; i < BlastWisps; i++)
            {
                Dust(new SoftParticle
                {
                    Born = 0.55f + 0.14f * i, Life = 0.8f,
                    From = foot + new Vector3(Roll(-0.25f, 0.25f) * width, 0f, Roll(-0.25f, 0.25f) * width), Rise = 0.26f * tile,
                    SizeFrom = 0.12f * tile, SizeTo = 0.24f * tile, Sit = 0.3f, Pop = 0.25f, ShrinkFrom = 0.45f,
                }, i + 1);
            }

            Color[] tones = RubbleTones(wall, roof);
            Mesh block = MeshFactory.RoundedBox(Vector3.one, 0.07f, 1);
            for (int i = 0; i < BlastRubble; i++)
            {
                Vector3 way = Round(i, BlastRubble, 0.5f);
                float size = Roll(0.045f, 0.1f) * tile;
                var chunk = new Chunk
                {
                    Piece = Block(block, tones, i % tones.Length),
                    Place = foot + new Vector3(Roll(-0.3f, 0.3f) * width, Roll(0.2f, 0.85f) * height, Roll(-0.3f, 0.3f) * width),
                    Speed = (way * Roll(0.7f, 1.9f) + Vector3.up * Roll(2.2f, 3.8f)) * tile,
                    Axis = new Vector3(Roll(-1f, 1f), Roll(-0.4f, 0.4f), Roll(-1f, 1f)).normalized,
                    Whirl = Roll(500f, 1200f) * (i % 2 == 0 ? 1f : -1f),
                    Born = Roll(0.02f, 0.12f),
                    Size = size,
                    Shape = new Vector3(1f, Roll(0.4f, 0.8f), Roll(0.55f, 1f)),
                    Floor = foot.y + size * 0.3f,
                };
                chunk.Piece.gameObject.SetActive(false);
                _chunks.Add(chunk);
            }
        }

        /// <summary>
        /// The three tones a torn down building's rubble is in, a third of the pieces each: its <paramref name="wall"/>
        /// a little soiled, its <paramref name="roof"/> (the wall's darker courses where it has none), and mortar.
        /// </summary>
        public static Color[] RubbleTones(Color wall, Color? roof) => new[]
        {
            Color.Lerp(wall, Color.black, 0.18f), roof ?? Color.Lerp(wall, Color.black, 0.5f), Color.Lerp(wall, new Color(0.6f, 0.57f, 0.52f), 0.75f),
        };

        /// <summary>One piece of rubble; pieces of a tone share its material.</summary>
        private Transform Block(Mesh mesh, Color[] tones, int tone)
        {
            if (_tones[tone] == null)
            {
                GameObject first = ModelParts.Create("Rubble", transform, mesh, tones[tone], 0.08f);
                _tones[tone] = first.GetComponent<Renderer>().sharedMaterial;
                if (_tones[tone] != null) _materials.Add(_tones[tone]);
                return first.transform;
            }

            var part = new GameObject("Rubble");
            part.transform.SetParent(transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = _tones[tone];
            return part.transform;
        }

        /// <summary>The rubble is moved in short steps, so a slow frame cannot throw it through the ground.</summary>
        protected override void Move(float seconds)
        {
            for (float left = seconds; left > 0f; left -= Tick) Tumble(Mathf.Min(left, Tick), Age + (seconds - left));
        }

        /// <summary>The rubble's flight for one short step that starts at <paramref name="time"/>: thrown, pulled down, bouncing with less each time, gripping the ground, and dwindling at the end.</summary>
        private void Tumble(float step, float time)
        {
            foreach (Chunk chunk in _chunks)
            {
                float lived = time + step - chunk.Born;
                if (lived < 0f) continue;
                if (!chunk.Piece.gameObject.activeSelf) chunk.Piece.gameObject.SetActive(true);
                chunk.Speed += Vector3.down * (Gravity * _tile * step);
                chunk.Place += chunk.Speed * step;
                chunk.Turned += chunk.Whirl * step;
                if (chunk.Place.y < chunk.Floor)
                {
                    chunk.Place.y = chunk.Floor;
                    bool rests = Mathf.Abs(chunk.Speed.y) < 0.35f * _tile;
                    chunk.Speed = new Vector3(chunk.Speed.x * Grip, rests ? 0f : -chunk.Speed.y * Bounce, chunk.Speed.z * Grip);
                    chunk.Whirl *= rests ? 0f : Grip;
                }

                // Nothing is thrown off the table: at its edge a piece drops straight down.
                float edge = _bound - chunk.Size;
                if (_bound > 0f && Mathf.Abs(chunk.Place.x) > edge)
                {
                    chunk.Place.x = Mathf.Sign(chunk.Place.x) * edge;
                    chunk.Speed.x = 0f;
                }

                if (_bound > 0f && Mathf.Abs(chunk.Place.z) > edge)
                {
                    chunk.Place.z = Mathf.Sign(chunk.Place.z) * edge;
                    chunk.Speed.z = 0f;
                }

                float size = chunk.Size * RubbleSize(lived);
                chunk.Piece.localPosition = chunk.Place;
                chunk.Piece.localRotation = Quaternion.AngleAxis(chunk.Turned, chunk.Axis);
                chunk.Piece.localScale = chunk.Shape * size;
            }
        }

        /// <summary>A piece of rubble's size over its time, as a share of its own: whole while it flies and lies, then dwindling to nothing.</summary>
        public static float RubbleSize(float lived)
        {
            if (lived < 0f) return 0f;
            if (lived <= RubbleSeconds) return 1f;
            return Mathf.Max(0f, 1f - Ease.InCubic((lived - RubbleSeconds) / RubbleGone));
        }

        /// <summary>Throws away what it made: the rubble's materials, with the mesh of the soft pieces.</summary>
        protected override void Clear()
        {
            foreach (Material material in _materials)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }

            _materials.Clear();
            base.Clear();
        }
    }
}
