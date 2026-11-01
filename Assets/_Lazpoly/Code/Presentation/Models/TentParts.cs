using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The owner's tent, the piece planted on a bought town until the first house. It is the camp
    /// tent model from Tools/Art/Models/CampTent, trimmed and scaled by Tools/Art/make_tent.py to
    /// one unit long along Z with its base at y = 0. The canvas and its seams take the owner's colour;
    /// poles, straps and groundsheet keep theirs. When the asset is missing the board falls back to
    /// the flag (<see cref="BuildingParts.Flag"/>).
    /// </summary>
    public static class TentParts
    {
        public const string Resource = "Lazpoly/Models/camp_tent";

        /// <summary>Material names in the model that are painted in the owner's colour.</summary>
        public const string CanvasPart = "Canvas_Olive";
        public const string SeamsPart = "Seams_Forest";

        public static readonly Color Aluminium = new Color(0.66f, 0.69f, 0.71f);
        public static readonly Color Strap = new Color(0.93f, 0.90f, 0.82f);
        public static readonly Color Groundsheet = new Color(0.23f, 0.26f, 0.25f);

        /// <summary>How much darker than the canvas the seams are drawn.</summary>
        public const float SeamShade = 0.35f;

        private static ObjModel _model;
        private static List<Mesh> _meshes;
        private static bool _loaded;
        private static bool _reportedMissing;

        /// <summary>Whether the tent model is in the build; false means the board shows flags.</summary>
        public static bool IsAvailable => Load() != null;

        /// <summary>The parsed model, loaded once; null when the asset is missing or unreadable.</summary>
        public static ObjModel Load()
        {
            if (_loaded) return _model;
            _loaded = true;
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null)
            {
                Report("is missing");
                return null;
            }

            try
            {
                _model = ObjModel.Parse(asset.text);
            }
            catch (System.FormatException exception)
            {
                Report($"could not be read: {exception.Message}");
                return null;
            }

            if (_model.Find(CanvasPart) == null)
            {
                Report($"has no '{CanvasPart}' part to paint");
                _model = null;
                return null;
            }

            _meshes = new List<Mesh>(_model.Groups.Count);
            foreach (ObjGroup group in _model.Groups) _meshes.Add(group.ToMesh($"tent-{group.Material}"));
            return _model;
        }

        /// <summary>The tent as a parent object with one part per material, or null when the model is missing.</summary>
        public static GameObject Tent(string name, Transform parent)
        {
            ObjModel model = Load();
            if (model == null) return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            for (int i = 0; i < model.Groups.Count; i++)
            {
                ObjGroup group = model.Groups[i];
                ModelParts.Create(group.Material, root.transform, _meshes[i], ColorFor(group.Material), SmoothnessFor(group.Material));
            }

            return root;
        }

        /// <summary>Paints the canvas in the owner's colour and the seams a shade darker.</summary>
        public static void Paint(GameObject tent, Color owner)
        {
            Transform canvas = tent.transform.Find(CanvasPart);
            if (canvas != null) ModelParts.Paint(canvas.gameObject, owner, SmoothnessFor(CanvasPart));
            Transform seams = tent.transform.Find(SeamsPart);
            if (seams != null) ModelParts.Paint(seams.gameObject, Shade(owner), SmoothnessFor(SeamsPart));
        }

        /// <summary>The seam colour for a canvas colour: the same hue, darker.</summary>
        public static Color Shade(Color canvas)
        {
            Color shade = Color.Lerp(canvas, Color.black, SeamShade);
            shade.a = canvas.a;
            return shade;
        }

        /// <summary>The colour a part is made with; the painted parts start white.</summary>
        public static Color ColorFor(string material)
        {
            switch (material)
            {
                case CanvasPart:
                case SeamsPart: return Color.white;
                case "Poles_Aluminum": return Aluminium;
                case "Groundsheet_Charcoal": return Groundsheet;
                default: return Strap;
            }
        }

        private static float SmoothnessFor(string material) => material == "Poles_Aluminum" ? 0.6f : material == CanvasPart ? 0.1f : 0.2f;

        private static void Report(string problem)
        {
            if (_reportedMissing) return;
            _reportedMissing = true;
            Debug.LogWarning($"[Lazpoly] Tent model '{Resource}' {problem}; owner flags are shown instead. Run Tools/Art/make_tent.py.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _model = null;
            _meshes = null;
            _loaded = false;
            _reportedMissing = false;
        }
    }
}
