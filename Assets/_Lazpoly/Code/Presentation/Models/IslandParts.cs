using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// A little floating island for a player's money safe to stand on (the owner's wishes, 2026-11-11: first the island,
    /// then a more professional one that suits the atmosphere): a meadow with grass hanging over its edge in tongues,
    /// tufts and a few flowers at the rim, vines trailing down, and under the soil the board's own warm limestone stepping
    /// in ledge by ledge to a leaning point, with loose stones adrift beside it. The shapes are cut by
    /// <see cref="IslandShape"/> and painted by <see cref="IslandSkin"/>. The top is the size it is given, at y 0, so the
    /// safe stands on it as it stood on its shelf; everything else hangs below, inside the top's outline. A seed makes
    /// each island a little different.
    /// </summary>
    public static class IslandParts
    {
        /// <summary>The vines' green: the grass's picture tinted towards olive, so they show against grass and rock alike.</summary>
        public static readonly Color Vine = new Color(0.80f, 0.84f, 0.62f);

        /// <summary>The flowers: the lawn's own daisies.</summary>
        public static readonly Color Flower = LawnSkin.Petal;

        public const string StonesName = "Drifting stones";
        public const string VinesName = "Vines";

        /// <summary>How deep the whole island hangs under its top.</summary>
        public const float Depth = IslandShape.Depth;

        /// <summary>Builds an island under <paramref name="parent"/> whose top is <paramref name="top"/> (x across, z deep) at y 0.</summary>
        public static GameObject Create(Transform parent, string name, Vector2 top, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform at = root.transform;
            Skinned(at, "Meadow", IslandShape.Meadow(top, seed), IslandSkin.Grass(), Color.white, 0.05f);
            Skinned(at, "Rock", IslandShape.Rock(top, seed), IslandSkin.Rock(), Color.white, 0.12f);

            // The vines, each its own part hanging from its foot, so each can sway by itself.
            var vines = new GameObject(VinesName);
            vines.transform.SetParent(at, false);
            for (int i = 0; i < IslandShape.Vines; i++)
            {
                GameObject vine = Skinned(vines.transform, $"Vine {i + 1}", IslandShape.Vine(top, seed, i), IslandSkin.Grass(), Vine, 0.05f);
                vine.transform.localPosition = IslandShape.VineFoot(top, seed, i);
            }

            Skinned(at, "Tufts", IslandShape.Tufts(top, seed), IslandSkin.Grass(), Color.white, 0.05f);
            ModelParts.Create("Flowers", at, IslandShape.Flowers(top, seed), Flower, 0.1f);

            // Loose stones adrift beside the rock, each its own part so it can bob by itself.
            var stones = new GameObject(StonesName);
            stones.transform.SetParent(at, false);
            Vector4[] drifters = IslandShape.Drifters(top, seed);
            for (int i = 0; i < drifters.Length; i++)
            {
                GameObject stone = Skinned(stones.transform, $"Stone {i + 1}", IslandShape.Drifter(seed * 10 + i), IslandSkin.Rock(), Color.white, 0.12f);
                stone.transform.localPosition = new Vector3(drifters[i].x, drifters[i].y, drifters[i].z);
                stone.transform.localScale = Vector3.one * drifters[i].w;
            }

            return root;
        }

        /// <summary>
        /// A small island for a picture (<see cref="BuildingPreview"/>): the meadow, the rock under it pressed to
        /// <paramref name="squash"/> of its depth so it stays a base rather than the subject, the tufts and the flowers;
        /// no vines and no loose stones, which would only be specks at that size.
        /// </summary>
        public static GameObject Islet(Transform parent, string name, Vector2 top, int seed, float squash)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform at = root.transform;
            Skinned(at, "Meadow", IslandShape.Meadow(top, seed), IslandSkin.Grass(), Color.white, 0.05f);
            GameObject rock = Skinned(at, "Rock", IslandShape.Rock(top, seed), IslandSkin.Rock(), Color.white, 0.12f);
            rock.transform.localScale = new Vector3(1f, Mathf.Clamp(squash, 0.1f, 1f), 1f);
            Skinned(at, "Tufts", IslandShape.Tufts(top, seed), IslandSkin.Grass(), Color.white, 0.05f);
            ModelParts.Create("Flowers", at, IslandShape.Flowers(top, seed), Flower, 0.1f);
            return root;
        }

        public const string TurfName = "Turf";
        public const string RockName = "Rock";

        /// <summary>
        /// The tint on the great island's rock: a shade deeper and cooler than the little islands' (which wear the
        /// picture untinted), as a thing further off in the sky, so the safes' islands stand out before it and it
        /// weighs less on the eye.
        /// </summary>
        public static readonly Color TableRock = new Color(0.80f, 0.84f, 0.90f);

        /// <summary>
        /// The great island under the table (the owner's wish, 2026-11-12), hung from the underside of the table's stone:
        /// its top is a square <paramref name="width"/> wide at y 0, flush with the table's own edge and never past it.
        /// Grass hangs from under the edge, long vines trail down, and the rock steps in to a leaning point with loose
        /// stones adrift beside it, all in the little islands' colours.
        /// </summary>
        public static GameObject Table(Transform parent, string name, float width, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            Transform at = root.transform;
            Skinned(at, TurfName, IslandShape.TableTurf(width, seed), IslandSkin.Grass(), Color.white, 0.05f);
            Skinned(at, RockName, IslandShape.TableRock(width, seed), IslandSkin.Rock(), TableRock, 0.12f);

            var vines = new GameObject(VinesName);
            vines.transform.SetParent(at, false);
            for (int i = 0; i < IslandShape.TableVines; i++)
            {
                GameObject vine = Skinned(vines.transform, $"Vine {i + 1}", IslandShape.TableVine(width, seed, i), IslandSkin.Grass(), Vine, 0.05f);
                vine.transform.localPosition = IslandShape.TableVineFoot(width, seed, i);
            }

            var stones = new GameObject(StonesName);
            stones.transform.SetParent(at, false);
            Vector4[] drifters = IslandShape.TableDrifters(width, seed);
            for (int i = 0; i < drifters.Length; i++)
            {
                GameObject stone = Skinned(stones.transform, $"Stone {i + 1}", IslandShape.Drifter(seed * 10 + i), IslandSkin.Rock(), TableRock, 0.12f);
                stone.transform.localPosition = new Vector3(drifters[i].x, drifters[i].y, drifters[i].z);
                stone.transform.localScale = Vector3.one * drifters[i].w;
            }

            return root;
        }

        public const string FarModelName = "Model";
        public const string TrunksName = "Trunks";
        public const string CrownsName = "Crowns";

        /// <summary>
        /// A far island in the sky (the owner's wish, 2026-11-12: the islands in the background three-dimensional): the
        /// little islands' meadow and rock at <see cref="IslandShape.FarTop"/> (the rock drawn out deeper by
        /// <see cref="IslandShape.FarRockStretch"/>), a few round trees on the meadow and
        /// loose stones adrift beside the rock, all in pictures and colours gone pale in the haze
        /// (<see cref="SkySkin.Hazed"/>). The whole hangs about its own middle (the model lifted by
        /// <see cref="IslandShape.FarMiddle"/>), so it can be placed, sized and turned by that point. It casts and takes
        /// no shadows: it is far beyond the table's light.
        /// </summary>
        public static GameObject Far(Transform parent, string name, int seed)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var model = new GameObject(FarModelName);
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = new Vector3(0f, IslandShape.FarMiddle, 0f);
            Transform at = model.transform;
            Vector2 top = IslandShape.FarTop;
            Skinned(at, "Meadow", IslandShape.Meadow(top, seed), SkySkin.FarGrassPicture(), Color.white, 0.05f);
            // The rock drawn out downwards (and its loose stones with it), so it shows under the meadow from the slanted view.
            GameObject rock = Skinned(at, RockName, IslandShape.Rock(top, seed), SkySkin.FarRockPicture(), Color.white, 0.1f);
            rock.transform.localScale = new Vector3(1f, IslandShape.FarRockStretch, 1f);
            ModelParts.Create(TrunksName, at, IslandShape.FarTrunks(seed), SkySkin.Hazed(SkySkin.FarTrunk), 0.05f);
            ModelParts.Create(CrownsName, at, IslandShape.FarCrowns(seed), SkySkin.Hazed(SkySkin.FarLeaf), 0.05f);

            var stones = new GameObject(StonesName);
            stones.transform.SetParent(at, false);
            Vector4[] drifters = IslandShape.Drifters(top, seed);
            for (int i = 0; i < drifters.Length; i++)
            {
                GameObject stone = Skinned(stones.transform, $"Stone {i + 1}", IslandShape.Drifter(seed * 10 + i), SkySkin.FarRockPicture(), Color.white, 0.1f);
                stone.transform.localPosition = new Vector3(drifters[i].x, drifters[i].y * IslandShape.FarRockStretch, drifters[i].z);
                stone.transform.localScale = Vector3.one * drifters[i].w;
            }

            foreach (MeshRenderer part in root.GetComponentsInChildren<MeshRenderer>())
            {
                part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                part.receiveShadows = false;
            }

            return root;
        }

        /// <summary>Whether the far islands are the owner's model (its files are in the build) rather than the ones cut in code.</summary>
        public static bool FarModelled => FigureModels.IsAvailable(FigureModels.SkyIslandId);

        /// <summary>
        /// A far island from the owner's model (<see cref="FigureModels.SkyIslandId"/>; the owner's wish, 2026-11-13: the
        /// islands in the background made with Higgsfield), or null when its files are missing and the island cut in
        /// code (<see cref="Far"/>) stands in. Held like that one: about its own middle as the slanted view shows it, and sized so it stands
        /// <see cref="IslandShape.FarSpan"/> high there, so the backdrop places, sizes and turns
        /// either the same way. Its front is its +Z. Gone into the haze (<see cref="Haze"/>), and without shadows.
        /// </summary>
        public static GameObject FarModel(Transform parent, string name)
        {
            if (!FarModelled) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            GameObject model = FigureModels.Create(FigureModels.SkyIslandId, FarModelName, root.transform);
            Transform body = model.transform.Find(FigureModels.BodyPart);
            (float size, Vector3 middle) = FarFit(body.GetComponent<MeshFilter>().sharedMesh);
            model.transform.localScale = Vector3.one * size;
            model.transform.localPosition = -middle * size;
            var part = body.GetComponent<MeshRenderer>();
            Haze(part.sharedMaterial, SkySkin.FarModelHaze);
            part.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.receiveShadows = false;
            return root;
        }

        /// <summary>
        /// How the model is fitted, reckoned once from its own shape: the size at which it stands
        /// <see cref="IslandShape.FarSpan"/> high in the slanted view, and its middle there (the upright through the
        /// middle of its footprint, at the height that shows half way between its lowest and its highest on the screen).
        /// </summary>
        private static (float size, Vector3 middle) FarFit(Mesh mesh)
        {
            if (_farFitted == mesh && mesh != null) return _farFit;
            Bounds bounds = mesh.bounds;
            var pivot = new Vector2(bounds.center.x, bounds.center.z);
            Vector2 reach = IslandShape.SeenReach(mesh.vertices, pivot);
            float span = Mathf.Max(1e-4f, reach.y - reach.x);
            float height = (reach.x + reach.y) / 2f / Mathf.Cos(IslandShape.ViewPitch * Mathf.Deg2Rad);
            _farFitted = mesh;
            return _farFit = (IslandShape.FarSpan / span, new Vector3(pivot.x, height, pivot.y));
        }

        private static Mesh _farFitted;
        private static (float size, Vector3 middle) _farFit;

        /// <summary>
        /// Sends a model's material into the haze, as a thing far off in the sky: its own colours dimmed by
        /// <paramref name="haze"/> and that share of the haze's blue (<see cref="SkySkin.FarHaze"/>) added as its own
        /// glow, so the haze lies over lit and shaded faces alike.
        /// </summary>
        public static void Haze(Material material, float haze)
        {
            if (material == null) return;
            haze = Mathf.Clamp01(haze);
            float clear = Mathf.LinearToGammaSpace(1f - haze);
            material.color = new Color(clear, clear, clear, 1f);
            if (!material.HasProperty(EmissionColor)) return;
            material.EnableKeyword("_EMISSION");
            Color glow = (SkySkin.FarHaze.linear * haze).gamma;
            glow.a = 1f;
            material.SetColor(EmissionColor, glow);
        }

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        private static GameObject Skinned(Transform parent, string name, Mesh mesh, Texture texture, Color tint, float smoothness)
        {
            GameObject part = ModelParts.Create(name, parent, mesh, tint, smoothness);
            ModelParts.Skin(part, texture);
            return part;
        }
    }
}
