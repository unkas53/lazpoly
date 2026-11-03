using System.Collections.Generic;
using UnityEngine;

namespace Lazpoly.Presentation.Models
{
    /// <summary>The two figures a player can be; seats alternate between them.</summary>
    public enum CharacterKind
    {
        Man = 0,
        Woman = 1,
    }

    /// <summary>
    /// The playing pieces: the business man from Tools/Art/Models/BusinessMan and the business woman made
    /// from him by Tools/Art/make_businesswoman.py, turned, merged and scaled by Tools/Art/make_character.py
    /// to one unit tall with the soles at y = 0, facing +Z. They have no skeleton; <see cref="Pawns.CharacterMotion"/>
    /// moves them as one piece. One part per material; the jacket's four materials (and the woman's skirt)
    /// take the player's colour, everything else keeps the model's own palette. When a model is missing the
    /// other one stands in, and when both are, the pawns fall back to the town's shapes (<see cref="PawnShapes"/>).
    /// </summary>
    public static class CharacterParts
    {
        public const string Resource = "Lazpoly/Models/business_man";
        public const string WomanResource = "Lazpoly/Models/business_woman";

        /// <summary>What the man is called where a pawn is named.</summary>
        public const string Name = "İş Adamı";
        public const string WomanName = "İş Kadını";

        /// <summary>Material names in the model that are painted in the player's colour.</summary>
        public const string JacketPart = "jacket";
        public const string JacketLightPart = "jacket_light";
        public const string JacketDarkPart = "jacket_dark";
        public const string LapelPart = "lapel";

        /// <summary>The woman's skirt, painted a shade darker than the jacket like the other half of a suit.</summary>
        public const string SkirtPart = "skirt";

        /// <summary>The parts every figure has and the game paints.</summary>
        public static readonly string[] PaintedParts = { JacketPart, JacketLightPart, JacketDarkPart, LapelPart };

        /// <summary>How far the lit parts of the jacket go towards white, and the pockets, lapels and skirt towards black.</summary>
        public const float LightTint = 0.18f;
        public const float DarkShade = 0.30f;
        public const float LapelShade = 0.14f;
        public const float SkirtShade = 0.45f;

        private static readonly ObjModel[] Models = new ObjModel[2];
        private static readonly List<Mesh>[] Meshes = new List<Mesh>[2];
        private static readonly bool[] Loaded = new bool[2];
        private static bool _reportedMissing;

        /// <summary>Whether a character model is in the build; false means the pawns are the town's shapes.</summary>
        public static bool IsAvailable => Load(CharacterKind.Man) != null || Load(CharacterKind.Woman) != null;

        /// <summary>The figure at a seat: men and women take turns round the table.</summary>
        public static CharacterKind KindFor(int seatIndex) => (seatIndex % 2 + 2) % 2 == 0 ? CharacterKind.Man : CharacterKind.Woman;

        public static string ResourceOf(CharacterKind kind) => kind == CharacterKind.Woman ? WomanResource : Resource;

        public static string NameOf(CharacterKind kind) => kind == CharacterKind.Woman ? WomanName : Name;

        /// <summary>The man's parsed model.</summary>
        public static ObjModel Load() => Load(CharacterKind.Man);

        /// <summary>The parsed model of a figure, loaded once; null when the asset is missing or unreadable.</summary>
        public static ObjModel Load(CharacterKind kind)
        {
            int index = (int)kind;
            if (Loaded[index]) return Models[index];
            Loaded[index] = true;
            string resource = ResourceOf(kind);
            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                Report(resource, "is missing");
                return null;
            }

            ObjModel model;
            try
            {
                model = ObjModel.Parse(asset.text);
            }
            catch (System.FormatException exception)
            {
                Report(resource, $"could not be read: {exception.Message}");
                return null;
            }

            if (model.Find(JacketPart) == null)
            {
                Report(resource, $"has no '{JacketPart}' part to paint");
                return null;
            }

            var meshes = new List<Mesh>(model.Groups.Count);
            foreach (ObjGroup group in model.Groups) meshes.Add(group.ToMesh($"character-{kind}-{group.Material}"));
            Models[index] = model;
            Meshes[index] = meshes;
            return model;
        }

        /// <summary>The man as a parent object with one part per material, or null when no model is there.</summary>
        public static GameObject Character(string name, Transform parent) => Character(CharacterKind.Man, name, parent);

        /// <summary>
        /// A figure as a parent object with one part per material; the other figure when this one's model is
        /// missing; null when neither is there.
        /// </summary>
        public static GameObject Character(CharacterKind kind, string name, Transform parent)
        {
            if (Load(kind) == null) kind = kind == CharacterKind.Man ? CharacterKind.Woman : CharacterKind.Man;
            ObjModel model = Load(kind);
            if (model == null) return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            List<Mesh> meshes = Meshes[(int)kind];
            for (int i = 0; i < model.Groups.Count; i++)
            {
                ObjGroup group = model.Groups[i];
                ModelParts.Create(group.Material, root.transform, meshes[i], ColorFor(group.Material), SmoothnessFor(group.Material));
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
            PaintPart(character, SkirtPart, Skirt(player));
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

        /// <summary>The skirt colour for a player colour: darker than the jacket and the lapels.</summary>
        public static Color Skirt(Color player) => Towards(player, Color.black, SkirtShade);

        private static Color Towards(Color color, Color target, float amount)
        {
            Color mixed = Color.Lerp(color, target, amount);
            mixed.a = color.a;
            return mixed;
        }

        /// <summary>Whether a material of the model is painted in the player's colour.</summary>
        public static bool IsPainted(string material) => material == SkirtPart || System.Array.IndexOf(PaintedParts, material) >= 0;

        /// <summary>The colour a part is made with: the model's own palette (BusinessMan.mtl); the painted parts start white.</summary>
        public static Color ColorFor(string material)
        {
            switch (material)
            {
                case JacketPart:
                case JacketLightPart:
                case JacketDarkPart:
                case LapelPart:
                case SkirtPart: return Color.white;
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
                // The woman's own (Tools/Art/make_businesswoman.py): auburn hair, a plum blouse as in the reference, dark tights and shoes.
                case "hair_w": return new Color(0.400f, 0.215f, 0.150f);
                case "hair_w_light": return new Color(0.520f, 0.300f, 0.200f);
                case "hair_w_dark": return new Color(0.270f, 0.145f, 0.110f);
                case "blouse": return new Color(0.640f, 0.460f, 0.600f);
                case "blouse_shadow": return new Color(0.520f, 0.360f, 0.490f);
                case "lips": return new Color(0.760f, 0.310f, 0.360f);
                case "tights": return new Color(0.230f, 0.205f, 0.225f);
                case "pump": return new Color(0.330f, 0.130f, 0.200f);
                case "pump_sole": return new Color(0.160f, 0.120f, 0.130f);
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
                case "accent":
                case "lips": return 0.6f;
                case "shoe":
                case "shoe_s":
                case "pump":
                case "tablet":
                case "tablet_dark":
                case "tablet_light": return 0.35f;
                default: return 0.15f;
            }
        }

        private static void Report(string resource, string problem)
        {
            if (_reportedMissing) return;
            _reportedMissing = true;
            Debug.LogWarning($"[Lazpoly] Character model '{resource}' {problem}; the other figure or the town's shapes stand in. Run Tools/Art/make_character.py.");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < Models.Length; i++)
            {
                Models[i] = null;
                Meshes[i] = null;
                Loaded[i] = false;
            }

            _reportedMissing = false;
        }
    }
}
