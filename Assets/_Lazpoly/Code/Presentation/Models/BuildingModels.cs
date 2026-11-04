using System.Collections.Generic;
using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The buildings of a town, one model per level: the empty plot with its sign (level 0, only in the
    /// card's pictures), houses of one to four storeys and the hotel. They come from Tools/Art/Models/Buildings,
    /// thinned by Tools/Art/make_buildings.py into one pack. Each stands on a <see cref="Width"/> x
    /// <see cref="Depth"/> footprint with its pivot in the middle of the ground and its door towards +Z;
    /// it is one white mesh that the board paints in the owner's colour. When the pack is missing the
    /// callers fall back to the drawn houses and mansion of <see cref="BuildingParts"/>.
    /// </summary>
    public static class BuildingModels
    {
        public const string Resource = "Lazpoly/Models/buildings";

        /// <summary>The model of each level, 0 (the plot) to 5 (the hotel).</summary>
        public static readonly string[] Names = { "Plot_00", "House_01", "House_02", "House_03", "House_04", "Hotel_05" };

        /// <summary>The footprint, along the front (x) and from front to back (z), in model units.</summary>
        public const float Width = 0.9f;
        public const float Depth = 0.72f;

        /// <summary>The name of the one part a building made here has; the board paints it.</summary>
        public const string PartName = "Building";

        /// <summary>The models' own colour, before an owner's colour is put on.</summary>
        public static readonly Color White = new Color(0.92f, 0.92f, 0.92f);

        private static Mesh[] _meshes;
        private static bool _loaded;

        public static bool IsAvailable => Load() != null;

        /// <summary>The meshes by level, loaded once; null when the pack is missing, unreadable or lacks a level.</summary>
        public static Mesh[] Load()
        {
            if (_loaded) return _meshes;
            _loaded = true;
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null)
            {
                Report("is missing");
                return null;
            }

            IReadOnlyList<BuildingMesh> pack;
            try
            {
                pack = BuildingPack.Parse(asset.bytes);
            }
            catch (System.FormatException exception)
            {
                Report($"could not be read: {exception.Message}");
                return null;
            }

            var meshes = new Mesh[Names.Length];
            for (int level = 0; level < Names.Length; level++)
            {
                BuildingMesh model = Find(pack, Names[level]);
                if (model == null)
                {
                    Report($"has no {Names[level]}");
                    return null;
                }

                meshes[level] = model.ToMesh();
            }

            _meshes = meshes;
            return _meshes;
        }

        /// <summary>
        /// The building of <paramref name="level"/> (0 the plot, 1-4 houses, <see cref="SquareDefinition.MaxBuildingLevel"/>
        /// the hotel) as an object with one white part, or null when the pack is missing.
        /// </summary>
        public static GameObject Create(int level, string name, Transform parent)
        {
            Mesh[] meshes = Load();
            if (meshes == null || level < 0 || level >= meshes.Length) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            ModelParts.Create(PartName, root.transform, meshes[level], White, 0.2f);
            return root;
        }

        private static BuildingMesh Find(IReadOnlyList<BuildingMesh> pack, string name)
        {
            foreach (BuildingMesh model in pack)
            {
                if (model.Name == name) return model;
            }

            return null;
        }

        private static void Report(string problem) =>
            Debug.LogWarning($"[Lazpoly] The building models ({Resource}) {problem}; the board draws its own houses. Run Tools/Art/make_buildings.py.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _meshes = null;
            _loaded = false;
        }
    }
}
