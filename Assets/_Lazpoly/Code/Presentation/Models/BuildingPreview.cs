using System;
using System.Collections.Generic;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The pictures of a property on the cards: a little floating island of the table's own kind
    /// (<see cref="IslandParts.Islet"/>) with the city's tile lying on its meadow and, on the tile, the empty plot, a
    /// house of one to four storeys or the hotel (<see cref="BuildingModels"/>, always the whole models; the drawn
    /// houses and mansion when the models are missing) in its owner's colour, or a factory city's works at the stage
    /// its owner's factory cities reach (<see cref="FactoryPicture"/>); behind it the table's sky with the cloud bank
    /// along the bottom (<see cref="PreviewSkin"/>). All from the board's camera angle, upright (three wide, four
    /// tall), the island at the same place in every picture so a row of them reads as one building growing.
    /// A stage far below the board holds the pieces; a camera of its own draws it at twice the picture's size and
    /// the picture is that halved and kept with its smaller copies, so edges stay smooth at any size on the screen.
    /// A picture is drawn the first time a card asks for it and kept; nothing renders every frame. In the editor, or
    /// where the render pipeline cannot take a render request (or there is no graphics device, as under
    /// -nographics), the pictures stay null and the card shows an icon instead.
    /// </summary>
    public sealed class BuildingPreview
    {
        /// <summary>Levels pictured: bare land, one to four houses, the hotel.</summary>
        public const int Levels = SquareDefinition.MaxBuildingLevel + 1;

        /// <summary>A picture's size, and how many times larger (each way) the stage is drawn before it is halved into it.</summary>
        public const int PictureWidth = 384;
        public const int PictureHeight = 512;
        public const int SuperSample = 2;

        /// <summary>How many pictures are kept; the oldest goes when one more is drawn.</summary>
        public const int MaxPictures = 24;

        /// <summary>The island under the tile: its top, its seed, and how much of its depth the rock keeps.</summary>
        public static readonly Vector2 IslandTop = new Vector2(1.9f, 1.6f);
        public const int IslandSeed = 1;
        public const float RockSquash = 0.7f;

        /// <summary>The city's tile on the meadow: wider than deep, like a square of the board.</summary>
        public const float TileWidth = 1.5f;
        public const float TileDepth = 1.24f;
        public const float TileThickness = 0.07f;

        /// <summary>The building model's size on the tile, a little behind its middle so its steps stay on the tile; a works is wider, so smaller.</summary>
        public const float ModelScale = 1.4f;
        public const float FactoryScale = 1.15f;
        public const float RowDepth = 0.05f;

        /// <summary>The drawn stand-ins when the models are missing: houses in a row, the mansion for the hotel.</summary>
        public const float HouseSize = 0.3f;
        public const float HouseStep = 0.36f;
        public static readonly Vector3 MansionSize = new Vector3(1.2f, 0.8f, 0.7f);

        /// <summary>Where the stage stands: far under the board, outside the main camera's clip planes and view.</summary>
        public static readonly Vector3 StageOrigin = new Vector3(0f, -300f, 0f);

        /// <summary>
        /// What the picture's camera sees, in stage units: how wide, and how far below the tile's middle (measured up the
        /// picture) its bottom edge lies. The island fits across with air at its sides; the hotel's roof fits under the top.
        /// </summary>
        public const float ViewWidth = 2.72f;
        public const float ViewBottom = -1.5f;
        public static readonly float ViewHeight = ViewWidth * PictureHeight / PictureWidth;
        public const float CameraDistance = 12f;

        /// <summary>How far up the picture the island's far corner shows, and how much higher than its eaves a roof's far edge.</summary>
        public const float MeadowRise = 0.92f;
        public const float RoofRise = 0.13f;

        /// <summary>The sky's colour where the backdrop cannot be drawn.</summary>
        public static readonly Color Sky = PreviewSkin.High;

        public const string IslandName = "Island";
        public const string TileName = "Tile";
        public const string SkyName = "Sky";

        private readonly GameObject _root;
        private readonly GameObject _tile;
        private readonly GameObject[] _houses = new GameObject[GameRules.MaxHouses];
        private GameObject _mansion;
        private readonly GameObject[] _models;
        private readonly GameObject[] _factories = new GameObject[ResortParts.Stages];
        private readonly Camera _camera;
        private readonly Dictionary<string, RenderTexture> _pictures = new Dictionary<string, RenderTexture>();
        private readonly List<string> _order = new List<string>();
        private RenderTexture _stage;
        private GameObject _sky;
        private bool _failed;

        public BuildingPreview(Transform parent)
        {
            _root = new GameObject("Building Preview");
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = StageOrigin;

            IslandParts.Islet(_root.transform, IslandName, IslandTop, IslandSeed, RockSquash);
            _tile = ModelParts.Create(TileName, _root.transform, MeshFactory.RoundedBox(new Vector3(TileWidth, TileThickness, TileDepth), 0.03f, 3), Color.white, 0.05f);
            _tile.transform.localPosition = new Vector3(0f, TileThickness / 2f, 0f);
            ModelParts.Skin(_tile, TileSkin.Paper());

            if (BuildingModels.IsAvailable)
            {
                // One model per level, whole whatever the graphics quality, its door towards the camera's side of the tile.
                _models = new GameObject[Levels];
                for (int level = 0; level < Levels; level++)
                {
                    GameObject model = BuildingModels.Create(level, BuildingModels.Names[level], _root.transform, whole: true);
                    Stand(model, Vector3.one * ModelScale);
                    _models[level] = model;
                }
            }
            else
            {
                CreateStandIns();
            }

            for (int i = 0; i < _factories.Length; i++)
            {
                GameObject factory = ResortParts.Stage(i + 1, FactoryName(i + 1), _root.transform);
                Stand(factory, Vector3.one * FactoryScale);
                _factories[i] = factory;
            }

            _camera = new GameObject("Preview Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            _camera.transform.localPosition = BoardCameraRig.Place(Aim, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, CameraDistance);
            _camera.transform.localRotation = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
            _camera.orthographic = true;
            _camera.orthographicSize = ViewHeight / 2f;
            _camera.aspect = (float)PictureWidth / PictureHeight;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = CameraDistance + 8f; // Close behind the stage, so the sun's shadow map is spent on the stage alone.
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Sky;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.enabled = false; // It renders only on request.
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;

        }

        /// <summary>
        /// The sky behind the stage: a plate that fills the view, facing the camera and taking no light. Hung up when the
        /// first picture is taken, so a game that never opens a card never draws it.
        /// </summary>
        private void HangSky()
        {
            if (_sky != null || !PicturePlate.IsAvailable) return;
            _sky = PicturePlate.Create(SkyName, _camera.transform, PreviewSkin.Backdrop(), new Vector2(ViewWidth, ViewHeight));
            _sky.transform.localPosition = new Vector3(0f, 0f, CameraDistance + 7f);
        }

        public GameObject Root => _root;

        /// <summary>What a factory's stage is called on the picture's stage.</summary>
        public static string FactoryName(int stage) => $"Factory {stage}";

        /// <summary>The point the camera looks at: on the tile's axis, as high as puts the picture's bottom edge at <see cref="ViewBottom"/>.</summary>
        public static Vector3 Aim => new Vector3(0f, (ViewBottom + ViewHeight / 2f) / Mathf.Cos(BoardCameraRig.Pitch * Mathf.Deg2Rad), 0f);

        /// <summary>
        /// How far up the picture (0 its bottom edge, 1 its top) something <paramref name="height"/> tall on the tile
        /// reaches, never lower than the island's own far corner.
        /// </summary>
        public static float TopOf(float height)
        {
            float rise = (TileThickness + Mathf.Max(0f, height)) * Mathf.Cos(BoardCameraRig.Pitch * Mathf.Deg2Rad) + RoofRise;
            return Mathf.Clamp01((Mathf.Max(rise, MeadowRise) - ViewBottom) / ViewHeight);
        }

        /// <summary>How far up the picture the building of <paramref name="level"/> reaches.</summary>
        public static float TopOfLevel(int level)
        {
            level = Mathf.Clamp(level, 0, Levels - 1);
            if (BuildingModels.IsAvailable) return TopOf(BuildingModels.HeightOf(level) * ModelScale);
            return TopOf(level >= SquareDefinition.MaxBuildingLevel ? MansionSize.y : level > 0 ? HouseSize : 0f);
        }

        /// <summary>How far up the picture a factory city's works reach, smoke and all.</summary>
        public static float TopOfFactory => TopOf(ResortParts.Height * FactoryScale);

        private static void Stand(GameObject piece, Vector3 scale)
        {
            piece.transform.localPosition = new Vector3(0f, TileThickness, RowDepth);
            piece.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            piece.transform.localScale = scale;
            piece.SetActive(false);
        }

        /// <summary>The houses in a row and the mansion, when the building models are not in the build.</summary>
        private void CreateStandIns()
        {
            for (int i = 0; i < _houses.Length; i++)
            {
                GameObject house = BuildingParts.House("House", _root.transform);
                house.transform.localPosition = new Vector3((i - (_houses.Length - 1) / 2f) * HouseStep, TileThickness, RowDepth);
                house.transform.localScale = Vector3.one * HouseSize;
                house.SetActive(false);
                _houses[i] = house;
            }

            _mansion = BuildingParts.Mansion("Mansion", _root.transform);
            _mansion.transform.localPosition = new Vector3(0f, TileThickness, RowDepth);
            _mansion.transform.localScale = MansionSize;
            _mansion.SetActive(false);
        }

        /// <summary>The level the stage shows now: 0 the empty plot, 1-4 the house's storeys, 5 the hotel.</summary>
        public int Shown { get; private set; }

        /// <summary>Puts the pieces of <paramref name="level"/> on the tile and hides the rest, the factories too.</summary>
        public void Show(int level)
        {
            Shown = Mathf.Clamp(level, 0, Levels - 1);
            foreach (GameObject factory in _factories) factory.SetActive(false);
            if (_models != null)
            {
                for (int i = 0; i < _models.Length; i++) _models[i].SetActive(i == Shown);
                return;
            }

            bool hotel = Shown >= SquareDefinition.MaxBuildingLevel;
            for (int i = 0; i < _houses.Length; i++) _houses[i].SetActive(!hotel && i < Shown);
            _mansion.SetActive(hotel);
        }

        /// <summary>Puts the works of <paramref name="stage"/> (1 to <see cref="ResortParts.Stages"/>) on the tile and hides the town's pieces.</summary>
        public void ShowFactory(int stage)
        {
            stage = Mathf.Clamp(stage, 1, ResortParts.Stages);
            if (_models != null)
            {
                foreach (GameObject model in _models) model.SetActive(false);
            }
            else
            {
                foreach (GameObject house in _houses) house.SetActive(false);
                _mansion.SetActive(false);
            }

            for (int i = 0; i < _factories.Length; i++) _factories[i].SetActive(i == stage - 1);
        }

        /// <summary>
        /// The picture of <paramref name="level"/> on a tile tinted <paramref name="tile"/>, its walls in
        /// <paramref name="owner"/>'s colour (white for nobody's), drawn now if it is not kept yet. Null when the
        /// pictures cannot be made here.
        /// </summary>
        public Texture Picture(int level, Color tile, Color? owner = null)
        {
            if (level < 0 || level >= Levels) return null;
            return Take(Key('t', level, tile, owner), () =>
            {
                ModelParts.Skin(_tile, TileSkin.Paper());
                ModelParts.Paint(_tile, tile, 0.05f);
                Show(level);
                if (_models != null) BuildingModels.PaintOwner(_models[level], owner);
            });
        }

        /// <summary>
        /// The picture of a factory city's works at <paramref name="stage"/> (1 to <see cref="ResortParts.Stages"/>) in
        /// <paramref name="owner"/>'s colour (unpainted for nobody's) on a tile paved in <paramref name="stone"/>. Null
        /// when the pictures cannot be made here.
        /// </summary>
        public Texture FactoryPicture(int stage, Color stone, Color? owner)
        {
            stage = Mathf.Clamp(stage, 1, ResortParts.Stages);
            return Take(Key('f', stage, stone, owner), () =>
            {
                ModelParts.Skin(_tile, TileSkin.Factory(stone, TileDepth / TileWidth));
                ModelParts.Paint(_tile, Color.white, 0.05f); // The paving carries its own colour.
                ShowFactory(stage);
                ResortParts.PaintOwner(_factories[stage - 1], owner);
            });
        }

        /// <summary>What a picture is kept under: its kind, level or stage, the tile's colour and the owner's.</summary>
        public static string Key(char kind, int index, Color tile, Color? owner) =>
            $"{kind}{index}:{ColorUtility.ToHtmlStringRGB(tile)}:{(owner.HasValue ? ColorUtility.ToHtmlStringRGB(owner.Value) : "-")}";

        /// <summary>How many pictures are kept now.</summary>
        public int Kept => _pictures.Count;

        private Texture Take(string key, Action dress)
        {
            if (_pictures.TryGetValue(key, out RenderTexture kept) && kept != null)
            {
                // Asked for again: it becomes the newest, so the cards on the screen never lose theirs.
                _order.Remove(key);
                _order.Add(key);
                return kept;
            }

            if (!CanRender()) return null;
            RenderTexture picture = null;
            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (!RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    Fail("the render pipeline takes no render requests");
                    return null;
                }

                if (_stage == null)
                {
                    _stage = new RenderTexture(PictureWidth * SuperSample, PictureHeight * SuperSample, 24, RenderTextureFormat.ARGB32)
                    {
                        name = "preview-stage", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp,
                    };
                    if (!_stage.Create())
                    {
                        Fail("a render texture could not be made");
                        return null;
                    }
                }

                picture = new RenderTexture(PictureWidth, PictureHeight, 0, RenderTextureFormat.ARGB32)
                {
                    name = $"preview-{key}", useMipMap = true, autoGenerateMips = false, filterMode = FilterMode.Trilinear,
                    wrapMode = TextureWrapMode.Clamp, anisoLevel = 0,
                };
                if (!picture.Create())
                {
                    Release(picture);
                    Fail("a render texture could not be made");
                    return null;
                }

                HangSky();
                dress();
                request.destination = _stage;
                RenderPipeline.SubmitRenderRequest(_camera, request);

                // Halved into the picture (each of its points the mean of four drawn ones), then its smaller copies made.
                RenderTexture active = RenderTexture.active;
                Graphics.Blit(_stage, picture);
                RenderTexture.active = active;
                picture.GenerateMips();
                Show(0);

                _pictures[key] = picture;
                _order.Remove(key);
                _order.Add(key);
                while (_order.Count > MaxPictures)
                {
                    string oldest = _order[0];
                    _order.RemoveAt(0);
                    if (_pictures.TryGetValue(oldest, out RenderTexture gone)) Release(gone);
                    _pictures.Remove(oldest);
                }

                return picture;
            }
            catch (Exception exception)
            {
                if (picture != null && !_pictures.ContainsValue(picture)) Release(picture);
                Fail(exception.Message);
                return null;
            }
        }

        private bool CanRender()
        {
            if (_failed || !Application.isPlaying) return false;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                _failed = true; // No graphics device (-nographics): nothing can be drawn, and nothing is missing.
                return false;
            }

            return true;
        }

        private void Fail(string why)
        {
            _failed = true;
            Debug.LogWarning($"[Lazpoly] Building pictures are off: {why}.");
            Release();
        }

        private static void Release(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
            else UnityEngine.Object.DestroyImmediate(texture);
        }

        private void Release()
        {
            foreach (RenderTexture picture in _pictures.Values) Release(picture);
            _pictures.Clear();
            _order.Clear();
            Release(_stage);
            _stage = null;
        }

        /// <summary>Frees the textures and the stage; the card falls back to icons afterwards.</summary>
        public void Dispose()
        {
            Release();
            if (_root == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
            else UnityEngine.Object.DestroyImmediate(_root);
        }
    }
}
