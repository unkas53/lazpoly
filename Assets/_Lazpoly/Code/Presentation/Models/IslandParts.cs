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

        private static GameObject Skinned(Transform parent, string name, Mesh mesh, Texture texture, Color tint, float smoothness)
        {
            GameObject part = ModelParts.Create(name, parent, mesh, tint, smoothness);
            ModelParts.Skin(part, texture);
            return part;
        }
    }
}
