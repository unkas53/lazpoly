using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The characters' figures: the owner's textured models, made with Meshy AI from pictures (<see cref="Ids"/>).
    /// Tools/Art/make_figures.py turns each into figure_&lt;id&gt;.bytes (two levels of detail, one unit tall, soles at
    /// y = 0, facing +Z) and figure_&lt;id&gt;_skin.bytes (the colour map). A figure keeps its own clothes and stands
    /// right on the tile (the owner had the coloured disc under its feet taken away). The whole model shows at the
    /// High graphics quality and the light one at Medium and Low (<see cref="SetFullDetail"/>, switched in place like
    /// the buildings). When a figure's files are missing another one stands in (<see cref="CreateOrStandIn"/>).
    /// </summary>
    public static class FigureModels
    {
        public const string Folder = "Lazpoly/Models/";

        /// <summary>The part that carries the figure and its colour map.</summary>
        public const string BodyPart = "body";

        /// <summary>
        /// The figures in the build: the woman in a burgundy blazer with a hand on her hip, the woman in a teal suit,
        /// the bearded man in a purple blazer, the man in a navy suit, the woman in a fur coat, the blond man in a
        /// three-piece suit, the boss with a cigar and the gentleman with a cane.
        /// </summary>
        public static readonly string[] Ids = { "hip", "teal", "purple", "navy", "mob", "suit", "boss", "cane" };

        /// <summary>
        /// Not a character: the bus station corner's diorama (a bus at a stop), in the same pack format, one unit
        /// wide instead of one unit tall (Tools/Art/make_corner_models.py; <see cref="CornerParts.Station"/>).
        /// </summary>
        public const string StationId = "station";

        /// <summary>
        /// Not a character either: the owner's champions' cup (Meshy AI; "LazPoly CHAMPIONS" on its front at +Z), one unit
        /// tall like a figure (Tools/Art/make_figures.py; <see cref="CornerParts.ChampionsCup"/>).
        /// </summary>
        public const string TrophyId = "trophy";

        /// <summary>
        /// The Şampiyonlar Kupası corner's diorama (the owner's cartoon stadium, Meshy AI), one unit wide like the bus
        /// station's, its stand at +Z (Tools/Art/make_corner_models.py; <see cref="CornerParts.Stadium"/>).
        /// </summary>
        public const string StadiumId = "stadium";

        /// <summary>
        /// Not a character: the owner's cartoon dollar stack (Meshy AI), the bundle paid money travels as, one unit long
        /// (Tools/Art/make_corner_models.py with a small triangle budget; <see cref="MoneyBundle"/>).
        /// </summary>
        public const string DollarsId = "dollars";

        private const float Smoothness = 0.2f;

        private sealed class Figure
        {
            public Mesh Full;
            public Mesh Light;
            public Texture2D Skin;
        }

        private static readonly Dictionary<string, Figure> Figures = new Dictionary<string, Figure>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static readonly List<(MeshFilter filter, string id)> Live = new List<(MeshFilter, string)>();
        private static bool _fullDetail = true;

        public static string MeshResource(string id) => Folder + "figure_" + id;
        public static string SkinResource(string id) => Folder + "figure_" + id + "_skin";

        /// <summary>Whether the figure's files are in the build and readable.</summary>
        public static bool IsAvailable(string id) => Load(id) != null;

        /// <summary>The figure as a parent object with its body, or null when its files are missing.</summary>
        public static GameObject Create(string id, string name, Transform parent)
        {
            Figure figure = Load(id);
            if (figure == null) return null;

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            GameObject body = ModelParts.Create(BodyPart, root.transform, _fullDetail ? figure.Full : figure.Light, Color.white, Smoothness);
            if (figure.Skin != null) ModelParts.Skin(body, figure.Skin);
            Live.Add((body.GetComponent<MeshFilter>(), id));
            return root;
        }

        /// <summary>The figure, or the first other one whose files are there; null when no figure is in the build.</summary>
        public static GameObject CreateOrStandIn(string id, string name, Transform parent)
        {
            GameObject figure = Create(id, name, parent);
            foreach (string other in Ids)
            {
                if (figure != null) break;
                if (other != id) figure = Create(other, name, parent);
            }

            return figure;
        }

        /// <summary>Shows the whole models (true, the High quality) or the light ones, on every figure already made too.</summary>
        public static void SetFullDetail(bool full)
        {
            if (_fullDetail == full) return;
            _fullDetail = full;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                (MeshFilter filter, string id) = Live[i];
                if (filter == null || !Figures.TryGetValue(id, out Figure figure) || figure == null) Live.RemoveAt(i); // Its scene is gone.
                else filter.sharedMesh = full ? figure.Full : figure.Light;
            }
        }

        /// <summary>Whether the whole models are shown now.</summary>
        public static bool FullDetail => _fullDetail;

        private static Figure Load(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Figures.TryGetValue(id, out Figure cached)) return cached;
            if (Missing.Contains(id)) return null;

            var asset = Resources.Load<TextAsset>(MeshResource(id));
            if (asset == null) return Fail(id, "is missing");
            IReadOnlyList<FigureMesh> levels;
            try
            {
                levels = FigurePack.Parse(asset.bytes);
            }
            catch (FormatException exception)
            {
                return Fail(id, $"could not be read: {exception.Message}");
            }

            FigureMesh full = Find(levels, FigurePack.HighLevel);
            FigureMesh light = Find(levels, FigurePack.LowLevel) ?? full;
            if (full == null) return Fail(id, "has no High level");

            var figure = new Figure
            {
                Full = full.ToMesh($"figure-{id}"),
                Light = light == full ? null : light.ToMesh($"figure-{id}-low"),
                Skin = LoadSkin(id),
            };
            figure.Light ??= figure.Full;
            Figures[id] = figure;
            return figure;
        }

        /// <summary>
        /// Reads a figure over several frames instead of at once, for a big model that is needed a little later (the paid
        /// money): its pack is read in the background and parsed on a worker thread, then its meshes and its colour map
        /// are made a frame apart, so no single frame carries the whole load. A figure asked for before it is ready is
        /// read at once as usual and this stops.
        /// </summary>
        public static IEnumerator Warm(string id)
        {
            if (string.IsNullOrEmpty(id) || Figures.ContainsKey(id) || Missing.Contains(id)) yield break;
            ResourceRequest packRequest = Resources.LoadAsync<TextAsset>(MeshResource(id));
            yield return packRequest;
            if (!(packRequest.asset is TextAsset pack) || Figures.ContainsKey(id) || Missing.Contains(id)) yield break;

            byte[] bytes = pack.bytes;
            Task<IReadOnlyList<FigureMesh>> parse = Task.Run(() => FigurePack.Parse(bytes));
            while (!parse.IsCompleted) yield return null;
            if (parse.IsFaulted || Figures.ContainsKey(id) || Missing.Contains(id)) yield break;

            FigureMesh full = Find(parse.Result, FigurePack.HighLevel);
            FigureMesh light = Find(parse.Result, FigurePack.LowLevel) ?? full;
            if (full == null) yield break;
            Mesh fullMesh = full.ToMesh($"figure-{id}");
            yield return null;
            Mesh lightMesh = light == full ? fullMesh : light.ToMesh($"figure-{id}-low");
            yield return null;

            ResourceRequest skinRequest = Resources.LoadAsync<TextAsset>(SkinResource(id));
            yield return skinRequest;
            if (Figures.ContainsKey(id)) yield break;
            Texture2D skin = SkinFrom(id, skinRequest.asset as TextAsset);
            Figures[id] = new Figure { Full = fullMesh, Light = lightMesh, Skin = skin };
        }

        private static FigureMesh Find(IReadOnlyList<FigureMesh> levels, string name)
        {
            foreach (FigureMesh level in levels)
            {
                if (level.Name == name) return level;
            }

            return null;
        }

        /// <summary>The colour map, compressed on the graphics card once decoded; null (a white figure) when it is missing.</summary>
        private static Texture2D LoadSkin(string id) => SkinFrom(id, Resources.Load<TextAsset>(SkinResource(id)));

        private static Texture2D SkinFrom(string id, TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning($"[Lazpoly] Figure colour map '{SkinResource(id)}' is missing; the figure shows white. Run Tools/Art/make_figures.py.");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = $"figure-{id}-skin", hideFlags = HideFlags.DontSave };
            if (!texture.LoadImage(asset.bytes, false))
            {
                Destroy(texture);
                Debug.LogWarning($"[Lazpoly] Figure colour map '{SkinResource(id)}' could not be read; the figure shows white.");
                return null;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            // Decoding made the mipmaps; compressing keeps them and shrinks the map about eight times on the card.
            if (texture.width % 4 == 0 && texture.height % 4 == 0) texture.Compress(true);
            texture.Apply(false, true);
            return texture;
        }

        private static Figure Fail(string id, string problem)
        {
            Missing.Add(id);
            Debug.LogWarning($"[Lazpoly] Figure '{MeshResource(id)}' {problem}; another figure stands in. Run Tools/Art/make_figures.py.");
            return null;
        }

        private static void Destroy(Object thing)
        {
            if (Application.isPlaying) Object.Destroy(thing);
            else Object.DestroyImmediate(thing);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Figures.Clear();
            Missing.Clear();
            Live.Clear();
            _fullDetail = true;
        }
    }
}
