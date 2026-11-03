using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The playing piece: the business man model from Tools/Art/Models/BusinessMan, turned, merged
    /// and scaled by Tools/Art/make_character.py to one unit tall with his soles at y = 0, facing +Z.
    /// He has no skeleton, so he moves as one piece (the pawn's hops and squashes). One part per
    /// material; the jacket's four materials take the player's colour, everything else keeps the
    /// colours of the model's own palette. When the asset is missing the pawns fall back to the
    /// town's shapes (<see cref="PawnShapes"/>).
    /// </summary>
    public static class CharacterParts
    {
        public const string Resource = "Lazpoly/Models/business_man";

        /// <summary>What the piece is called where a pawn is named.</summary>
        public const string Name = "İş Adamı";

        /// <summary>Material names in the model that are painted in the player's colour.</summary>
        public const string JacketPart = "jacket";
        public const string JacketLightPart = "jacket_light";
        public const string JacketDarkPart = "jacket_dark";
        public const string LapelPart = "lapel";

        public static readonly string[] PaintedParts = { JacketPart, JacketLightPart, JacketDarkPart, LapelPart };

        /// <summary>How far the lit parts of the jacket go towards white, and the pockets and lapels towards black.</summary>
        public const float LightTint = 0.18f;
        public const float DarkShade = 0.30f;
        public const float LapelShade = 0.14f;

        private static ObjModel _model;
        private static List<Mesh> _meshes;
        private static bool _loaded;
        private static bool _reportedMissing;

        /// <summary>Whether the character model is in the build; false means the pawns are the town's shapes.</summary>
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

            if (_model.Find(JacketPart) == null)
            {
                Report($"has no '{JacketPart}' part to paint");
                _model = null;
                return null;
            }

            _meshes = new List<Mesh>(_model.Groups.Count);
            foreach (ObjGroup group in _model.Groups) _meshes.Add(group.ToMesh($"character-{group.Material}"));
            return _model;
        }

        /// <summary>The character as a parent object with one part per material, or null when the model is missing.</summary>
        public static GameObject Character(string name, Transform parent)
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

        /// <summary>Paints the jacket in the player's colour: the body in the colour itself, sleeves and folds lighter, pockets and lapels darker.</summary>
        public static void Paint(GameObject character, Color player)
        {
            PaintPart(character, JacketPart, player);
            PaintPart(character, JacketLightPart, Light(player));
            PaintPart(character, JacketDarkPart, Dark(player));
            PaintPart(character, LapelPart, Lapel(player));
        }

        private static void PaintPart(GameObject character, string material, Color color)
        {
            Transform part = character.transform.Find(material);
            if (part != null) ModelParts.Paint(part.gameObject, color, SmoothnessFor(material));
        }

        /// <summary>The lit jacket colour for a player colour: the same hue, lighter.</summary>
        public static Color Light(Color player) => Towards(player, Color.white, LightTint);

        /// <summary>The pocket colour for a player colour: the same hue, darker.</summary>
        public static Color Dark(Color player) => Towards(player, Color.black, DarkShade);

        /// <summary>The lapel colour for a player colour: a little darker than the jacket.</summary>
        public static Color Lapel(Color player) => Towards(player, Color.black, LapelShade);

        private static Color Towards(Color color, Color target, float amount)
        {
            Color mixed = Color.Lerp(color, target, amount);
            mixed.a = color.a;
            return mixed;
        }

        /// <summary>Whether a material of the model is painted in the player's colour.</summary>
        public static bool IsPainted(string material) => System.Array.IndexOf(PaintedParts, material) >= 0;

        /// <summary>The colour a part is made with: the model's own palette (BusinessMan.mtl); the painted parts start white.</summary>
        public static Color ColorFor(string material)
        {
            switch (material)
            {
                case JacketPart:
                case JacketLightPart:
                case JacketDarkPart:
                case LapelPart: return Color.white;
                case "skin": return new Color(0.866f, 0.572f, 0.392f);
                case "skin_light": return new Color(0.937f, 0.670f, 0.482f);
                case "skin_shadow": return new Color(0.709f, 0.427f, 0.298f);
                case "hair": return new Color(0.278f, 0.200f, 0.168f);
                case "hair_light": return new Color(0.376f, 0.254f, 0.200f);
                case "hair_dark": return new Color(0.184f, 0.149f, 0.133f);
                case "beard": return new Color(0.305f, 0.203f, 0.164f);
                case "eye_white": return new Color(0.976f, 0.968f, 0.929f);
                case "iris": return new Color(0.235f, 0.501f, 0.615f);
                case "pupil": return new Color(0.101f, 0.129f, 0.145f);
                case "brow": return new Color(0.286f, 0.188f, 0.145f);
                case "mouth": return new Color(0.290f, 0.141f, 0.145f);
                case "teeth": return new Color(0.984f, 0.964f, 0.878f);
                case "shirt": return new Color(0.905f, 0.909f, 0.870f);
                case "shirt_shadow": return new Color(0.784f, 0.827f, 0.835f);
                case "jeans": return new Color(0.270f, 0.419f, 0.533f);
                case "jeans_light": return new Color(0.356f, 0.525f, 0.615f);
                case "jeans_dark": return new Color(0.196f, 0.317f, 0.415f);
                case "belt": return new Color(0.184f, 0.176f, 0.203f);
                case "shoe": return new Color(0.913f, 0.937f, 0.929f);
                case "shoe_s": return new Color(0.776f, 0.811f, 0.835f);
                case "sole": return new Color(0.988f, 0.984f, 0.945f);
                case "accent": return new Color(0.690f, 0.529f, 0.333f);
                case "tablet": return new Color(0.345f, 0.411f, 0.466f);
                case "tablet_dark": return new Color(0.215f, 0.262f, 0.305f);
                case "tablet_light": return new Color(0.556f, 0.619f, 0.650f);
                case "screen": return new Color(0.160f, 0.207f, 0.266f);
                default: return Color.magenta; // A part the palette does not know stands out, so the palette gets fixed.
            }
        }

        /// <summary>Cloth is matt; eyes, the belt buckle and the tablet's screen catch the light.</summary>
        public static float SmoothnessFor(string material)
        {
            switch (material)
            {
                case "eye_white":
                case "iris":
                case "pupil":
                case "screen":
                case "accent": return 0.6f;
                case "shoe":
                case "shoe_s":
                case "tablet":
                case "tablet_dark":
                case "tablet_light": return 0.35f;
                default: return 0.15f;
            }
        }

        private static void Report(string problem)
        {
            if (_reportedMissing) return;
            _reportedMissing = true;
            Debug.LogWarning($"[Lazpoly] Character model '{Resource}' {problem}; the town's shapes are the pawns instead. Run Tools/Art/make_character.py.");
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
