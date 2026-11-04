using System.Collections.Generic;
using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The buildings of a town, one model per level: the empty plot with its sign (level 0: the owner's
    /// marker on a bought town, and the card's picture of bare land), houses of one to four storeys and the
    /// hotel. They come from Tools/Art/Models/Buildings through Tools/Art/make_buildings.py, which packs each
    /// model whole and as a light copy (about 30% of the triangles). The whole models stand at the High
    /// graphics quality and the light ones at Medium and Low (<see cref="SetFullDetail"/>, switched in place).
    /// Each stands on a <see cref="Width"/> x <see cref="Depth"/> footprint with its pivot in the middle of
    /// the ground and its front towards +Z; it is one white mesh that the board paints in the owner's colour.
    /// When the pack is missing the callers fall back to the drawn pieces of <see cref="BuildingParts"/>.
    /// </summary>
    public static class BuildingModels
    {
        public const string Resource = "Lazpoly/Models/buildings";

        /// <summary>The model of each level, 0 (the plot) to 5 (the hotel).</summary>
        public static readonly string[] Names = { "Plot_00", "House_01", "House_02", "House_03", "House_04", "Hotel_05" };

        /// <summary>The light copy of a model is named with this after the model's name.</summary>
        public const string LowSuffix = "_Low";

        /// <summary>The footprint, along the front (x) and from front to back (z), in model units.</summary>
        public const float Width = 0.9f;
        public const float Depth = 0.72f;

        /// <summary>The name of the one part a building made here has; the board paints it.</summary>
        public const string PartName = "Building";

        /// <summary>The models' own colour, before an owner's colour is put on.</summary>
        public static readonly Color White = new Color(0.92f, 0.92f, 0.92f);

        private static Mesh[] _full;
        private static Mesh[] _light;
        private static bool _loaded;
        private static bool _fullDetail = true;
        private static readonly List<(MeshFilter Filter, int Level)> Live = new List<(MeshFilter, int)>();

        public static bool IsAvailable => Load();

        /// <summary>Whether the whole models are shown (High quality) rather than the light copies.</summary>
        public static bool FullDetail => _fullDetail;

        /// <summary>Loads the pack once; false when it is missing, unreadable or lacks a model.</summary>
        public static bool Load()
        {
            if (_loaded) return _full != null;
            _loaded = true;
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null)
            {
                Report("is missing");
                return false;
            }

            IReadOnlyList<BuildingMesh> pack;
            try
            {
                pack = BuildingPack.Parse(asset.bytes);
            }
            catch (System.FormatException exception)
            {
                Report($"could not be read: {exception.Message}");
                return false;
            }

            var full = new Mesh[Names.Length];
            var light = new Mesh[Names.Length];
            for (int level = 0; level < Names.Length; level++)
            {
                BuildingMesh whole = Find(pack, Names[level]);
                BuildingMesh thin = Find(pack, Names[level] + LowSuffix);
                if (whole == null || thin == null)
                {
                    Report($"has no {Names[level]}");
                    return false;
                }

                full[level] = whole.ToMesh();
                light[level] = thin.ToMesh();
            }

            _full = full;
            _light = light;
            return true;
        }

        /// <summary>The mesh a level shows at the current detail; null when the pack is missing.</summary>
        public static Mesh MeshFor(int level) => MeshFor(level, _fullDetail);

        public static Mesh MeshFor(int level, bool fullDetail)
        {
            if (!Load() || level < 0 || level >= Names.Length) return null;
            return fullDetail ? _full[level] : _light[level];
        }

        /// <summary>
        /// The building of <paramref name="level"/> (0 the plot, 1-4 houses, <see cref="SquareDefinition.MaxBuildingLevel"/>
        /// the hotel) as an object with one white part, or null when the pack is missing.
        /// </summary>
        public static GameObject Create(int level, string name, Transform parent)
        {
            Mesh mesh = MeshFor(level);
            if (mesh == null) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            GameObject part = ModelParts.Create(PartName, root.transform, mesh, White, 0.2f);
            Live.Add((part.GetComponent<MeshFilter>(), level));
            return root;
        }

        /// <summary>Shows the whole models (true, the High quality) or the light copies, on every building already made too.</summary>
        public static void SetFullDetail(bool full)
        {
            if (_fullDetail == full) return;
            _fullDetail = full;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                (MeshFilter filter, int level) = Live[i];
                if (filter == null) Live.RemoveAt(i); // Its scene is gone.
                else filter.sharedMesh = MeshFor(level);
            }
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
            _full = null;
            _light = null;
            _loaded = false;
            _fullDetail = true;
            Live.Clear();
        }
    }
}
