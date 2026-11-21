using System.Collections.Generic;
using Lazpoly.Core.Board;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The buildings of a town, one model per level: the empty plot with its sign (level 0: the owner's
    /// marker on a bought town, and the card's picture of bare land), houses of one to three storeys and the
    /// hotel. They come from Tools/Art/Models/Buildings through Tools/Art/make_buildings.py, which sorts each
    /// model's parts into <see cref="Slots"/> (walls, rails, glass, doors, roofs, the base, handles, window and
    /// door frames, the belts between the floors) and packs every slot whole and as a light copy (about 30% of the triangles). The whole models
    /// stand at the High graphics quality and the light ones at Medium and Low (<see cref="SetFullDetail"/>,
    /// switched in place). Each stands on a <see cref="Width"/> x <see cref="Depth"/> footprint with its pivot
    /// in the middle of the ground and its front towards +Z. A building is one part per slot. The roof takes the
    /// owner's colour (<see cref="PaintOwner"/>): the owner's one wish for the buildings' colours, 2026-11-21. The
    /// rest was left to be made to suit the table, after white walls with black frames, a concrete slab and houses
    /// with no slab under them were each tried and turned down: ivory walls with white frames and rails, deep blue
    /// glass so a window reads from the game's height, a warm sand slab darker than the walls it carries, and the
    /// thin belts between the floors in the roof's colour. The plot has no roof: its paving and its sign's face
    /// wear the owner's colour.
    /// When the pack is missing the callers fall back to the drawn pieces of <see cref="BuildingParts"/>.
    /// </summary>
    public static class BuildingModels
    {
        public const string Resource = "Lazpoly/Models/buildings";

        /// <summary>
        /// The model of each level, 0 (the plot) to 4 (the hotel). The models keep the names their file gives them: the
        /// file's fourth house (House_04, the flat-roofed block) is not used since the fourth house left the rules
        /// (the owner's rule, 2026-11-21), so the hotel is still Hotel_05.
        /// </summary>
        public static readonly string[] Names = { "Plot_00", "House_01", "House_02", "House_03", "Hotel_05" };

        /// <summary>The light copy of a model is named with this after the model's name.</summary>
        public const string LowSuffix = "_Low";

        /// <summary>
        /// The parts a model is split into; the pack names each mesh "Model/Slot" (or "Model_Low/Slot").
        /// Every model has walls; the others are there when the model has such parts.
        /// </summary>
        public static readonly string[] Slots = { "Wall", "Trim", "Glass", "Door", "Roof", "Base", "Metal", "Frame", "Belt" };

        /// <summary>
        /// The walls, ivory on every building; the slots painted in the owner's colour: the roof (a model without
        /// one, the plot, wears it on its walls) and the belts between the floors; the slab a building stands on;
        /// and the frames of its windows and doors.
        /// </summary>
        public const int WallSlot = 0;
        public const int GlassSlot = 2;
        public const int RoofSlot = 4;
        public const int BaseSlot = 5;
        public const int FrameSlot = 7;
        public const int BeltSlot = 8;

        /// <summary>The colour of each slot; the roofs' and the belts' are the unowned colours.</summary>
        public static readonly Color[] SlotColors =
        {
            new Color(0.91f, 0.88f, 0.82f), // Walls: ivory.
            new Color(0.97f, 0.95f, 0.91f), // Balcony rails, parapets, the hotel's columns, the sign's board: white.
            new Color(0.26f, 0.40f, 0.56f), // Glass (the whole of a window's recess): deep blue.
            new Color(0.36f, 0.23f, 0.16f), // Doors and the sign's posts: dark wood.
            new Color(0.71f, 0.32f, 0.23f), // Roofs, chimneys, canopies, the slabs of flat roofs: terracotta until owned.
            new Color(0.75f, 0.70f, 0.61f), // The slab a building stands on, its kerbs and steps, balcony slabs: warm sand stone. Its top faces the light: at 0.87 it showed near white and glared (the owner, 2026-11-21: "çok parlıyor").
            new Color(0.83f, 0.65f, 0.23f), // Handles and the hotel's letters: brass.
            new Color(0.97f, 0.95f, 0.91f), // The frames of windows and doors, and the bars across the panes: white.
            new Color(0.97f, 0.95f, 0.91f), // The belts between the floors: white until owned.
        };

        /// <summary>How shiny each slot is; glass and brass catch the light.</summary>
        private static readonly float[] SlotSmoothness = { 0.2f, 0.25f, 0.75f, 0.2f, 0.15f, 0.1f, 0.6f, 0.2f, 0.2f };

        /// <summary>How far towards white the owner's colour is taken on the plot's paving and sign.</summary>
        public const float OwnerLightening = 0.15f;

        /// <summary>The footprint, along the front (x) and from front to back (z), in model units.</summary>
        public const float Width = 0.9f;
        public const float Depth = 0.72f;

        /// <summary>A building's walls: ivory, whoever owns it.</summary>
        public static Color WallColor => SlotColors[WallSlot];

        /// <summary>A building's roof for an owner: the owner's own colour, or terracotta for nobody's.</summary>
        public static Color RoofColor(Color? owner) => owner ?? SlotColors[RoofSlot];

        /// <summary>The belts between a building's floors for an owner: the roof's colour, or white for nobody's.</summary>
        public static Color BeltColor(Color? owner) => owner ?? SlotColors[BeltSlot];

        /// <summary>The plot's paving and sign for an owner (lightened a little), or ivory for nobody's.</summary>
        public static Color PlotColor(Color? owner) =>
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
        /// The building of <paramref name="level"/> (0 the plot, 1-3 houses, <see cref="SquareDefinition.MaxBuildingLevel"/>
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

        /// <summary>
        /// Paints a building for its owner: the roof and the belts between its floors in the owner's colour
        /// (terracotta and white when it has none) over ivory walls. A model with no roof (the plot) wears the colour
        /// on its walls instead, lightened a little. The other parts keep their colours.
        /// </summary>
        public static void PaintOwner(GameObject building, Color? owner)
        {
            Renderer[] parts = building.GetComponentsInChildren<Renderer>(true);
            bool roofed = false;
            foreach (Renderer part in parts) roofed |= part.name == Slots[RoofSlot];
            foreach (Renderer part in parts)
            {
                if (part.name == Slots[RoofSlot]) ModelParts.Paint(part.gameObject, RoofColor(owner), SlotSmoothness[RoofSlot]);
                else if (part.name == Slots[BeltSlot]) ModelParts.Paint(part.gameObject, BeltColor(owner), SlotSmoothness[BeltSlot]);
                else if (part.name == Slots[WallSlot]) ModelParts.Paint(part.gameObject, roofed ? WallColor : PlotColor(owner), SlotSmoothness[WallSlot]);
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
