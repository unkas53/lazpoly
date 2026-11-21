using System.Collections.Generic;
using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Effects
{
    /// <summary>
    /// A missile fired at a town (the owner's card, 2026-11-21: it takes off from its player's hatch, comes down on the
    /// town picked and blows up what is built there; "atmosfere uygun, göze hoş, profesyonel"). The hatch's smoke puffs
    /// out as it lifts off, the missile (<see cref="MissileParts"/>) flies its curve (<see cref="MissileFlight"/>)
    /// turning about itself, its flame flickering and a trail of white cloud behind it, and where it comes down there
    /// is a flash, a cartoon burst star, a ball of fire in two tones, rings over the ground, sparks, dark smoke that
    /// rises after it and a scorch that fades. A missile that meets a protection card's shield bursts on it in a pale
    /// flash and white puffs instead, with no fire. Everything see-through is in the game's own kind: clean shapes
    /// that pop out and dwindle (<see cref="SoftBurst"/>), all laid down at the start, so it always plays the same way.
    /// The building's own going (<see cref="Leaving.Blasted"/>) and its rubble are the board's.
    /// </summary>
    public sealed class MissileStrike : SoftBurst
    {
        /// <summary>The missile from tail to nose, in tile widths: it comes up through a money hatch.</summary>
        public const float Size = 0.66f;

        /// <summary>How often it leaves a puff behind, and how many times a second it turns about itself.</summary>
        public const float TrailEvery = 0.04f;
        public const float Turns = 0.75f;

        public const int LaunchPuffs = 8;
        public const int Flames = 9;
        public const int Smokes = 9;
        public const int Sparks = 14;
        public const int ShieldPuffs = 7;
        public const int ShieldSparks = 8;

        /// <summary>The puffs of its trail: one every <see cref="TrailEvery"/> from just after it starts to move until it strikes.</summary>
        public static int TrailPuffs => Mathf.FloorToInt((MissileFlight.Seconds - TrailFrom) / TrailEvery) + 1;

        private const float TrailFrom = 0.1f;

        /// <summary>
        /// The quads of a strike (a cloud is two: its body and its lit side). Always the lift-off's ring and puffs and the
        /// trail; then the hit: a flash, three stars, the fire, two rings, the sparks, the smoke and the scorch, or on a
        /// shield a flash, a star, two rings, the puffs and the sparks.
        /// </summary>
        public static int FlightParticles => 1 + 2 * LaunchPuffs + 2 * TrailPuffs;
        public static int HitParticles => 1 + 3 + 2 * Flames + 2 + Sparks + 2 * Smokes + 1;
        public static int BlockedParticles => 1 + 1 + 2 + 2 * ShieldPuffs + ShieldSparks;

        /// <summary>The fire's two tones, the smoke's, the scorch, and the pale blue of a shield struck.</summary>
        public static readonly Color32 Fire = new Color32(244, 126, 38, 255);
        public static readonly Color32 FireLight = new Color32(255, 214, 92, 255);
        public static readonly Color32 FireRim = new Color32(214, 70, 34, 255);
        public static readonly Color32 Smoke = new Color32(84, 80, 88, 255);
        public static readonly Color32 SmokeLight = new Color32(132, 128, 136, 255);
        public static readonly Color32 Scorch = new Color32(40, 34, 30, 255);
        public static readonly Color32 ShieldGlow = new Color32(150, 214, 232, 255);

        private readonly List<Material> _materials = new List<Material>();
        private MissileFlight _flight;
        private Transform _missile;
        private Transform _flame;
        private float _tile;
        private bool _blocked;

        /// <summary>When the missile starts to move and when it strikes, in seconds from the start (the lid opens first).</summary>
        public float LaunchAt => MissileFlight.OpenSeconds;
        public float ImpactAt => MissileFlight.OpenSeconds + MissileFlight.Seconds;

        /// <summary>Whether it has left its hatch, and whether it has struck.</summary>
        public bool Launched => Age >= LaunchAt;
        public bool Struck => Age >= ImpactAt;

        /// <summary>Whether it bursts on a shield instead of blowing the town up.</summary>
        public bool Blocked => _blocked;

        /// <summary>Its way, and the missile itself (null once it has struck).</summary>
        public MissileFlight Flight => _flight;
        public Transform Missile => _missile;

        /// <summary>
        /// Fires a missile under <paramref name="parent"/> and in its frame, from the hatch whose mouth is at
        /// <paramref name="mouth"/> at a building that stands with the middle of its foot at <paramref name="foot"/>,
        /// <paramref name="width"/> across and <paramref name="height"/> high, on a board whose tiles are
        /// <paramref name="tile"/> wide. Its trim is <paramref name="color"/> (its player's); <paramref name="blocked"/>
        /// makes it burst on the town's shield; <paramref name="seed"/> settles the scatter.
        /// </summary>
        public static MissileStrike Play(Transform parent, Vector3 mouth, Vector3 foot, float width, float height, float tile, Color color, bool blocked, int seed)
        {
            var root = new GameObject("Missile strike");
            root.transform.SetParent(parent, false);
            var strike = root.AddComponent<MissileStrike>();
            strike._tile = tile;
            strike._blocked = blocked;
            float length = Size * tile;
            // It comes down on the middle of the building; a shield stands over the roof, and it bursts there.
            Vector3 heart = foot + Vector3.up * (blocked ? height + 0.2f * tile : height * 0.5f);
            strike._flight = new MissileFlight(mouth, heart, tile, length);
            strike._missile = MissileParts.Create(root.transform, "Missile", color).transform;
            strike._missile.localScale = Vector3.one * length;
            strike._flame = strike._missile.Find(MissileParts.FlameName);
            foreach (Renderer part in strike._missile.GetComponentsInChildren<Renderer>())
            {
                if (part.sharedMaterial != null) strike._materials.Add(part.sharedMaterial);
            }

            strike.Begin(seed);
            strike.LayLiftOff(mouth);
            strike.LayTrail();
            if (blocked) strike.LayShieldBurst(heart);
            else strike.LayBlast(foot, heart, width, height);
            strike.Outlast(strike.ImpactAt);
            strike.Ready("Missile strike");
            strike.Pose(0f);
            return strike;
        }

        /// <summary>The hatch as it takes off: a ring over the lawn and puffs of white smoke blown out all round it.</summary>
        private void LayLiftOff(Vector3 mouth)
        {
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Ring, Flat = true, Born = LaunchAt + 0.05f, Life = 0.6f,
                From = mouth + Vector3.up * (_tile * 0.012f), SizeFrom = 0.3f * _tile, SizeTo = 1.1f * _tile,
                Color = Puff, Alpha = 0.75f, FadeIn = 0.1f, FadeFrom = 0.3f,
            });
            for (int i = 0; i < LaunchPuffs; i++)
            {
                Vector3 way = Round(i, LaunchPuffs, 0.6f);
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = LaunchAt + 0.02f + Roll(0f, 0.3f), Life = Roll(0.7f, 1f),
                    From = mouth + way * (0.12f * _tile), Drift = way * (Roll(0.28f, 0.5f) * _tile), Rise = Roll(0.04f, 0.16f) * _tile,
                    SizeFrom = 0.12f * _tile, SizeTo = Roll(0.24f, 0.34f) * _tile, Sit = 0.3f, Pop = 0.2f, ShrinkFrom = 0.45f,
                    Color = Puff, Alpha = 1f, FadeFrom = 0.9f, Turn = Roll(-18f, 18f),
                }, SoftSprite.CloudLight, PuffLight);
            }
        }

        /// <summary>The trail: a puff of white cloud left at its tail every <see cref="TrailEvery"/>, blown back a little, swelling and dwindling where it was left.</summary>
        private void LayTrail()
        {
            int count = TrailPuffs;
            for (int i = 0; i < count; i++)
            {
                float time = TrailFrom + i * TrailEvery;
                Vector3 back = -_flight.Heading(time);
                var scatter = new Vector3(Roll(-1f, 1f), Roll(-1f, 1f), Roll(-1f, 1f)) * (0.03f * _tile);
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = LaunchAt + time, Life = Roll(0.55f, 0.85f),
                    From = _flight.Tail(time) + scatter, Drift = back * (Roll(0.08f, 0.18f) * _tile),
                    SizeFrom = 0.1f * _tile, SizeTo = Roll(0.2f, 0.3f) * _tile, Pop = 0.25f, ShrinkFrom = 0.4f,
                    Color = Puff, Alpha = 1f, FadeFrom = 0.9f, Turn = Roll(-18f, 18f),
                }, SoftSprite.CloudLight, PuffLight);
            }
        }

        /// <summary>The hit: the flash, the burst star, the ball of fire, the rings over the ground, the sparks, the smoke and the scorch.</summary>
        private void LayBlast(Vector3 foot, Vector3 heart, float width, float height)
        {
            float hit = ImpactAt;
            float near = Fore(width, height);
            Vector3 ground = foot + Vector3.up * (_tile * 0.012f);

            // The scorch first, so everything else is drawn over it: dark on the tile, fading slowly.
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Dot, Flat = true, Born = hit + 0.05f, Life = 2.6f, From = ground,
                SizeFrom = 0.7f * _tile, SizeTo = 1.05f * _tile, Color = Scorch, Alpha = 0.55f, FadeIn = 0.05f, FadeFrom = 0.35f,
            });

            // Two rings run out over the ground, the first pale and fast, the second the fire's colour.
            for (int i = 0; i < 2; i++)
            {
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Ring, Flat = true, Born = hit + 0.09f * i, Life = 0.5f + 0.1f * i, From = ground,
                    SizeFrom = width * 0.8f, SizeTo = (i == 0 ? 2.1f : 1.6f) * _tile,
                    Color = i == 0 ? Light : Fire, Alpha = i == 0 ? 0.95f : 0.8f, FadeIn = 0.08f, FadeFrom = 0.3f,
                });
            }

            // The flash.
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Dot, Born = hit - 0.02f, Life = 0.34f, From = heart, Fore = near,
                SizeFrom = 0.6f * _tile, SizeTo = 2.4f * _tile, Color = Light, Alpha = 1f, FadeIn = 0.1f, FadeFrom = 0.25f,
            });

            // The burst star, three deep: red rim, orange, and a yellow heart, popping out and gone in a blink.
            Color32[] tones = { FireRim, Fire, FireLight };
            float[] sizes = { 1.55f, 1.3f, 0.85f };
            for (int i = 0; i < 3; i++)
            {
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Star, Born = hit + 0.01f * i, Life = 0.36f, From = heart, Fore = near,
                    SizeFrom = sizes[i] * 0.45f * _tile, SizeTo = sizes[i] * _tile, Pop = 0.3f, ShrinkFrom = 0.6f,
                    Color = tones[i], Alpha = 1f, FadeFrom = 0.85f, Turn = 9f + 18f * i, Spin = 14f,
                });
            }

            // The ball of fire: one cloud at the heart and a wreath of them bursting out, orange with yellow lit sides.
            for (int i = 0; i < Flames; i++)
            {
                bool middle = i == 0;
                Vector3 way = Round(i, Flames - 1, 0.2f);
                float up = Roll(-0.1f, 0.8f);
                Vector3 off = middle ? Vector3.zero : (way * Mathf.Sqrt(1f - up * up * 0.6f) + Vector3.up * up).normalized;
                float size = (middle ? Roll(0.62f, 0.7f) : Roll(0.36f, 0.5f)) * _tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = hit + (middle ? 0f : Roll(0f, 0.08f)), Life = middle ? 0.5f : Roll(0.45f, 0.65f),
                    From = heart + off * (width * 0.2f), Drift = off * (Roll(0.22f, 0.42f) * _tile), Rise = 0.08f * _tile,
                    SizeFrom = size * 0.55f, SizeTo = size, Pop = 0.2f, ShrinkFrom = 0.4f,
                    Color = Fire, Alpha = 1f, FadeFrom = 0.85f, Turn = Roll(-18f, 18f), Fore = near,
                }, SoftSprite.CloudLight, FireLight);
            }

            // Sparks thrown out of it, falling as they fade.
            for (int i = 0; i < Sparks; i++)
            {
                Vector3 way = Round(i, Sparks, 1.3f);
                float thrown = Roll(0.5f, 1.1f) * _tile;
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Dot, Born = hit + Roll(0f, 0.08f), Life = Roll(0.5f, 0.85f),
                    From = heart, Drift = way * (Roll(0.6f, 1.2f) * _tile) + Vector3.up * thrown, Fall = thrown + height * 0.5f,
                    SizeFrom = 0.1f * _tile, SizeTo = 0.04f * _tile, Color = i % 3 == 0 ? Light : FireLight, Alpha = 1f, FadeFrom = 0.7f, Fore = near,
                });
            }

            // Dark smoke after the fire: it comes out of where the fire was and rises, swelling, then dwindles.
            for (int i = 0; i < Smokes; i++)
            {
                Vector3 way = Round(i, Smokes, 2.1f);
                float size = Roll(0.34f, 0.5f) * _tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = hit + 0.2f + 0.3f * i / Smokes + Roll(0f, 0.04f), Life = Roll(1.1f, 1.6f),
                    From = heart + way * (width * Roll(0.05f, 0.3f)) + Vector3.up * (Roll(-0.3f, 0.2f) * height),
                    Drift = way * (Roll(0.05f, 0.2f) * _tile), Rise = Roll(0.5f, 1f) * _tile,
                    SizeFrom = size * 0.5f, SizeTo = size, Pop = 0.2f, ShrinkFrom = 0.5f,
                    Color = Smoke, Alpha = 0.95f, FadeFrom = 0.8f, Turn = Roll(-18f, 18f), Fore = near * (i % 2 == 0 ? 1f : 0.5f),
                }, SoftSprite.CloudLight, SmokeLight);
            }
        }

        /// <summary>A missile that meets a shield: a pale flash and a small white star over the roof, two rings in the shield's blue, white puffs and pale sparks. No fire, and nothing is harmed.</summary>
        private void LayShieldBurst(Vector3 heart)
        {
            float hit = ImpactAt;
            float near = 0.4f * _tile;
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Dot, Born = hit - 0.02f, Life = 0.3f, From = heart, Fore = near,
                SizeFrom = 0.4f * _tile, SizeTo = 1.4f * _tile, Color = Light, Alpha = 0.95f, FadeIn = 0.1f, FadeFrom = 0.25f,
            });
            Add(new SoftParticle
            {
                Sprite = SoftSprite.Star, Born = hit, Life = 0.32f, From = heart, Fore = near,
                SizeFrom = 0.35f * _tile, SizeTo = 0.8f * _tile, Pop = 0.3f, ShrinkFrom = 0.55f, Color = PuffLight, Alpha = 1f, FadeFrom = 0.85f, Turn = 9f, Spin = 14f,
            });
            for (int i = 0; i < 2; i++)
            {
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Ring, Born = hit + 0.08f * i, Life = 0.5f, From = heart, Fore = near,
                    SizeFrom = 0.3f * _tile, SizeTo = (1.3f - 0.3f * i) * _tile, Color = ShieldGlow, Alpha = 0.9f - 0.25f * i, FadeIn = 0.1f, FadeFrom = 0.3f,
                });
            }

            for (int i = 0; i < ShieldPuffs; i++)
            {
                bool middle = i == 0;
                Vector3 way = Round(i, ShieldPuffs - 1, 0.5f);
                Vector3 off = middle ? Vector3.zero : (way + Vector3.up * Roll(0f, 0.7f)).normalized;
                float size = (middle ? 0.38f : Roll(0.22f, 0.3f)) * _tile;
                Pair(new SoftParticle
                {
                    Sprite = SoftSprite.Cloud, Born = hit + (middle ? 0f : Roll(0f, 0.06f)), Life = Roll(0.5f, 0.7f),
                    From = heart + off * (0.06f * _tile), Drift = off * (Roll(0.18f, 0.32f) * _tile), Rise = 0.05f * _tile,
                    SizeFrom = size * 0.6f, SizeTo = size, Pop = 0.22f, ShrinkFrom = 0.4f,
                    Color = Puff, Alpha = 1f, FadeFrom = 0.9f, Turn = Roll(-18f, 18f), Fore = near,
                }, SoftSprite.CloudLight, PuffLight);
            }

            for (int i = 0; i < ShieldSparks; i++)
            {
                Vector3 way = Round(i, ShieldSparks, 0.9f);
                float thrown = Roll(0.2f, 0.5f) * _tile;
                Add(new SoftParticle
                {
                    Sprite = SoftSprite.Dot, Born = hit + Roll(0f, 0.06f), Life = Roll(0.4f, 0.6f),
                    From = heart, Drift = way * (Roll(0.4f, 0.7f) * _tile) + Vector3.up * thrown, Fall = thrown * 1.6f,
                    SizeFrom = 0.08f * _tile, SizeTo = 0.03f * _tile, Color = ShieldGlow, Alpha = 1f, FadeFrom = 0.7f, Fore = near,
                });
            }
        }

        protected override void Move(float seconds) => Pose(Age + seconds);

        /// <summary>
        /// Puts the missile where it is <paramref name="time"/> seconds after the strike began: out of sight until the
        /// lid is open, then along its way with its nose ahead, turning about itself, its flame flickering; gone from
        /// the hit on.
        /// </summary>
        public void Pose(float time)
        {
            if (_missile == null) return;
            float flown = time - LaunchAt;
            bool flying = flown >= 0f && flown < MissileFlight.Seconds;
            if (_missile.gameObject.activeSelf != flying) _missile.gameObject.SetActive(flying);
            if (flown >= MissileFlight.Seconds)
            {
                Discard(_missile.gameObject);
                _missile = null;
                _flame = null;
                return;
            }

            if (!flying) return;
            _missile.localPosition = _flight.Position(flown);
            _missile.localRotation = Quaternion.FromToRotation(Vector3.up, _flight.Heading(flown)) * Quaternion.AngleAxis(360f * Turns * flown, Vector3.up);
            if (_flame != null) _flame.localScale = FlameScale(flown);
        }

        /// <summary>The flame's size at <paramref name="flown"/> seconds of flight: it catches as the missile starts, then flickers in length and a little in width.</summary>
        public static Vector3 FlameScale(float flown)
        {
            float lit = Mathf.Clamp01(flown / 0.12f);
            float flicker = 0.85f + 0.15f * Mathf.Sin(flown * 61f) + 0.08f * Mathf.Sin(flown * 97f + 1.3f);
            float wide = 1f + 0.06f * Mathf.Sin(flown * 43f);
            return new Vector3(wide * lit, flicker * lit, wide * lit);
        }

        private static void Discard(Object thing)
        {
            if (Application.isPlaying) Destroy(thing);
            else DestroyImmediate(thing);
        }

        /// <summary>Throws away what it made: the missile's own materials, with the mesh of the soft pieces.</summary>
        protected override void Clear()
        {
            foreach (Material material in _materials)
            {
                if (material != null) Discard(material);
            }

            _materials.Clear();
            base.Clear();
        }
    }
}
