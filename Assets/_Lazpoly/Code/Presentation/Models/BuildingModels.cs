using System.Collections.Generic;
using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The buildings of a town, one model per level: the empty plot with its sign (level 0: the owner's
    /// marker on a bought town, and the card's picture of bare land), houses of one to four storeys and the
    /// hotel. They come from Tools/Art/Models/Buildings through Tools/Art/make_buildings.py, which sorts each
    /// model's parts into <see cref="Slots"/> (walls, window and door frames, glass, doors, roofs, the base,
    /// handles) and packs every slot whole and as a light copy (about 30% of the triangles). The whole models
    /// stand at the High graphics quality and the light ones at Medium and Low (<see cref="SetFullDetail"/>,
    /// switched in place). Each stands on a <see cref="Width"/> x <see cref="Depth"/> footprint with its pivot
    /// in the middle of the ground and its front towards +Z. A building is one part per slot: the walls take the
    /// owner's colour (<see cref="PaintOwner"/>), the rest keep fixed colours so doors and windows stand out.
    /// When the pack is missing the callers fall back to the drawn pieces of <see cref="BuildingParts"/>.
    /// </summary>
    public static class BuildingModels
    {
        public const string Resource = "Lazpoly/Models/buildings";

        /// <summary>The model of each level, 0 (the plot) to 5 (the hotel).</summary>
        public static readonly string[] Names = { "Plot_00", "House_01", "House_02", "House_03", "House_04", "Hotel_05" };

        /// <summary>The light copy of a model is named with this after the model's name.</summary>
        public const string LowSuffix = "_Low";

        /// <summary>
        /// The parts a model is split into; the pack names each mesh "Model/Slot" (or "Model_Low/Slot").
        /// Every model has walls; the others are there when the model has such parts.
        /// </summary>
        public static readonly string[] Slots = { "Wall", "Trim", "Glass", "Door", "Roof", "Base", "Metal" };

        /// <summary>The slot that is painted in the owner's colour.</summary>
        public const int WallSlot = 0;

        /// <summary>The fixed colour of each slot; the walls' is the unowned colour.</summary>
        public static readonly Color[] SlotColors =
        {
            new Color(0.92f, 0.92f, 0.92f), // Walls, white until owned.
            new Color(0.95f, 0.93f, 0.89f), // Window and door frames, cornices, rails: cream.
            new Color(0.56f, 0.77f, 0.91f), // Glass: light blue.
            new Color(0.36f, 0.23f, 0.16f), // Doors and the sign's posts: dark wood.
            new Color(0.71f, 0.32f, 0.23f), // Roofs, chimneys, canopies: terracotta.
            new Color(0.79f, 0.76f, 0.71f), // Foundations, steps, balcony slabs: warm stone.
            new Color(0.83f, 0.65f, 0.23f), // Handles and the hotel's letters: brass.
        };

        /// <summary>How shiny each slot is; glass and brass catch the light.</summary>
        private static readonly float[] SlotSmoothness = { 0.2f, 0.25f, 0.75f, 0.2f, 0.15f, 0.1f, 0.6f };

        /// <summary>How far towards white the owner's colour is taken on the walls, so the windows still read.</summary>
        public const float OwnerLightening = 0.15f;

        /// <summary>The footprint, along the front (x) and from front to back (z), in model units.</summary>
        public const float Width = 0.9f;
        public const float Depth = 0.72f;

        /// <summary>The walls' colour for an owner, or the unowned white.</summary>
        public static Color WallColor(Color? owner) =>
            owner.HasValue ? Color.Lerp(owner.Value, Color.white, OwnerLightening) : SlotColors[WallSlot];

        // [level][slot], null where a model has no such part.
        private static Mesh[][] _full;
        private static Mesh[][] _light;
        private static bool _loaded;
        private static bool _fullDetail = true;
        private static readonly List<(MeshFilter Filter, int Level, int Slot)> Live = new List<(MeshFilter, int, int)>();

        public static bool IsAvailable => Load();

        /// <summary>Whether the whole models are shown (High quality) rather than the light copies.</summary>
        public static bool FullDetail => _fullDetail;

        /// <summary>Loads the pack once; false when it is missing, unreadable or lacks a model's walls or a light copy.</summary>
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

            var byName = new Dictionary<string, BuildingMesh>();
            foreach (BuildingMesh mesh in pack) byName[mesh.Name] = mesh;

            var full = new Mesh[Names.Length][];
            var light = new Mesh[Names.Length][];
            for (int level = 0; level < Names.Length; level++)
            {
                full[level] = new Mesh[Slots.Length];
                light[level] = new Mesh[Slots.Length];
                for (int slot = 0; slot < Slots.Length; slot++)
                {
                    byName.TryGetValue(PartKey(Names[level], slot), out BuildingMesh whole);
                    byName.TryGetValue(PartKey(Names[level] + LowSuffix, slot), out BuildingMesh thin);
                    if ((whole == null) != (thin == null) || (slot == WallSlot && whole == null))
                    {
                        Report($"has no {Names[level]} {Slots[slot]}");
                        return false;
                    }

                    if (whole == null) continue;
                    full[level][slot] = whole.ToMesh();
                    light[level][slot] = thin.ToMesh();
                }
            }

            _full = full;
            _light = light;
            return true;
        }

        /// <summary>The name of a model's slot in the pack.</summary>
        public static string PartKey(string model, int slot) => model + "/" + Slots[slot];

        /// <summary>A slot's mesh at the current detail; null when the pack is missing or the model has no such part.</summary>
        public static Mesh MeshFor(int level, int slot) => MeshFor(level, slot, _fullDetail);

        public static Mesh MeshFor(int level, int slot, bool fullDetail)
        {
            if (!Load() || level < 0 || level >= Names.Length || slot < 0 || slot >= Slots.Length) return null;
            return fullDetail ? _full[level][slot] : _light[level][slot];
        }

        /// <summary>
        /// The building of <paramref name="level"/> (0 the plot, 1-4 houses, <see cref="SquareDefinition.MaxBuildingLevel"/>
        /// the hotel) as an object with one part per slot, named after it, or null when the pack is missing.
        /// </summary>
        /// <param name="whole">True keeps the whole model whatever the graphics quality: the cards' pictures are taken of these.</param>
        public static GameObject Create(int level, string name, Transform parent, bool whole = false)
        {
            if (MeshFor(level, WallSlot) == null) return null;
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            for (int slot = 0; slot < Slots.Length; slot++)
            {
                Mesh mesh = whole ? MeshFor(level, slot, true) : MeshFor(level, slot);
                if (mesh == null) continue;
                GameObject part = ModelParts.Create(Slots[slot], root.transform, mesh, SlotColors[slot], SlotSmoothness[slot]);
                if (!whole) Live.Add((part.GetComponent<MeshFilter>(), level, slot));
            }

            return root;
        }

        /// <summary>How tall the model of <paramref name="level"/> stands, in model units; 0 when the pack is missing.</summary>
        public static float HeightOf(int level)
        {
            float height = 0f;
            for (int slot = 0; slot < Slots.Length; slot++)
            {
                Mesh mesh = MeshFor(level, slot, true);
                if (mesh != null) height = Mathf.Max(height, mesh.bounds.max.y);
            }

            return height;
        }

        /// <summary>Paints a building's walls for its owner (lightened a little), or white when it has none; the other parts keep their colours.</summary>
        public static void PaintOwner(GameObject building, Color? owner)
        {
            Color color = WallColor(owner);
            foreach (Renderer part in building.GetComponentsInChildren<Renderer>(true))
            {
                if (part.name == Slots[WallSlot]) ModelParts.Paint(part.gameObject, color, SlotSmoothness[WallSlot]);
            }
        }

        /// <summary>Shows the whole models (true, the High quality) or the light copies, on every building already made too.</summary>
        public static void SetFullDetail(bool full)
        {
            if (_fullDetail == full) return;
            _fullDetail = full;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                (MeshFilter filter, int level, int slot) = Live[i];
                if (filter == null) Live.RemoveAt(i); // Its scene is gone.
                else filter.sharedMesh = MeshFor(level, slot);
            }
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
