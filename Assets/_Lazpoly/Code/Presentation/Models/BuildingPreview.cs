using System;
using Lazpoly.Core.Board;
using Lazpoly.Core.Engine;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lazpoly.Presentation.Models
{
    /// <summary>
    /// The little pictures on the property card: a paper tile with the empty plot, a house of one to
    /// four storeys or the hotel on it (<see cref="BuildingModels"/>; the drawn houses and mansion when the
    /// models are missing), from the board's camera angle; on a holiday resort, its beach at the stage the
    /// player's resorts reach, in their colour (<see cref="BeachPicture"/>).
    /// A stage far below the board holds one tile and the pieces; a camera of its own renders it
    /// into one texture per level the first time a card needs them, and again when the tile's
    /// colour changes. Nothing renders every frame. In the editor, or where the render pipeline
    /// cannot take a render request (or there is no graphics device, as under -nographics), the
    /// pictures stay null and the card shows an icon instead.
    /// </summary>
    public sealed class BuildingPreview
    {
        /// <summary>Levels pictured: bare land, one to four houses, the hotel.</summary>
        public const int Levels = SquareDefinition.MaxBuildingLevel + 1;

        public const int PictureSize = 256;

        /// <summary>The stage tile: wider than deep, like a square of the board.</summary>
        public const float TileWidth = 1.4f;
        public const float TileDepth = 1.0f;
        public const float TileThickness = 0.2f;

        /// <summary>The building model's size on the stage tile, a little behind its middle so the land in front stays visible.</summary>
        public const float ModelScale = 0.55f;
        public const float RowDepth = 0.12f;

        /// <summary>The drawn stand-ins when the models are missing: houses in a row, the mansion for the hotel.</summary>
        public const float HouseSize = 0.24f;
        public const float HouseStep = 0.3f;
        public static readonly Vector3 MansionSize = new Vector3(0.9f, 0.58f, 0.5f);

        /// <summary>Where the stage stands: far under the board, outside the main camera's clip planes and view.</summary>
        public static readonly Vector3 StageOrigin = new Vector3(0f, -300f, 0f);

        /// <summary>Half the height the picture's camera sees; the tile's diagonal fits with a little sky around it.</summary>
        public const float ViewSize = 0.95f;
        public const float CameraDistance = 10f;

        public static readonly Color Sky = new Color(0.84f, 0.92f, 0.97f);

        private readonly GameObject _root;
        private readonly GameObject _tile;
        private readonly GameObject[] _houses = new GameObject[GameRules.MaxHouses];
        private GameObject _mansion;
        private readonly GameObject[] _models;
        private readonly Camera _camera;
        private readonly RenderTexture[] _pictures = new RenderTexture[Levels];
        private readonly GameObject[] _beaches = new GameObject[ResortParts.Stages];
        private RenderTexture _beachPicture;
        private int _beachShown;
        private Color _beachTint = new Color(-1f, 0f, 0f);
        private Color _beachOwner = new Color(-1f, 0f, 0f);
        private Color _tint = new Color(-1f, 0f, 0f);
        private bool _failed;

        public BuildingPreview(Transform parent)
        {
            _root = new GameObject("Building Preview");
            _root.transform.SetParent(parent, false);
            _root.transform.localPosition = StageOrigin;

            _tile = ModelParts.Create("Tile", _root.transform, MeshFactory.RoundedBox(new Vector3(TileWidth, TileThickness, TileDepth), 0.05f, 4), Color.white, 0.05f);
            _tile.transform.localPosition = new Vector3(0f, -TileThickness / 2f, 0f);
            ModelParts.Skin(_tile, TileSkin.Paper());

            if (BuildingModels.IsAvailable)
            {
                // One model per level, its door towards the camera's side of the tile.
                _models = new GameObject[Levels];
                for (int level = 0; level < Levels; level++)
                {
                    GameObject model = BuildingModels.Create(level, BuildingModels.Names[level], _root.transform);
                    model.transform.localPosition = new Vector3(0f, 0f, RowDepth);
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    model.transform.localScale = Vector3.one * ModelScale;
                    model.SetActive(false);
                    _models[level] = model;
                }
            }
            else
            {
                CreateStandIns();
            }

            for (int i = 0; i < _beaches.Length; i++)
            {
                GameObject beach = ResortParts.Stage(i + 1, $"Beach {i + 1}", _root.transform);
                ResortParts.Palms("Palms", beach.transform); // Every resort has its palms (the owner's wish, 2026-11-11).
                beach.transform.localPosition = new Vector3(0f, 0f, RowDepth);
                beach.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                beach.transform.localScale = Vector3.one * ModelScale;
                beach.SetActive(false);
                _beaches[i] = beach;
            }

            _camera = new GameObject("Preview Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform, false);
            Vector3 aim = new Vector3(0f, 0.15f, 0f);
            _camera.transform.localPosition = BoardCameraRig.Place(aim, BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, CameraDistance);
            _camera.transform.localRotation = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
            _camera.orthographic = true;
            _camera.orthographicSize = ViewSize;
            _camera.nearClipPlane = 0.3f;
            _camera.farClipPlane = CameraDistance * 3f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Sky;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.enabled = false; // It renders only on request.
            UniversalAdditionalCameraData data = _camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
        }

        public GameObject Root => _root;

        /// <summary>The houses in a row and the mansion, when the building models are not in the build.</summary>
        private void CreateStandIns()
        {
            for (int i = 0; i < _houses.Length; i++)
            {
                GameObject house = BuildingParts.House("House", _root.transform);
                house.transform.localPosition = new Vector3((i - (_houses.Length - 1) / 2f) * HouseStep, 0f, RowDepth);
                house.transform.localScale = Vector3.one * HouseSize;
                house.SetActive(false);
                _houses[i] = house;
            }

            _mansion = BuildingParts.Mansion("Mansion", _root.transform);
            _mansion.transform.localPosition = new Vector3(0f, 0f, RowDepth);
            _mansion.transform.localScale = MansionSize;
            _mansion.SetActive(false);
        }

        /// <summary>The level the stage shows now: 0 the empty plot, 1-4 the house's storeys, 5 the hotel.</summary>
        public int Shown { get; private set; }

        /// <summary>Puts the pieces of <paramref name="level"/> on the tile and hides the rest.</summary>
        public void Show(int level)
        {
            Shown = Mathf.Clamp(level, 0, Levels - 1);
            if (_models != null)
            {
                for (int i = 0; i < _models.Length; i++) _models[i].SetActive(i == Shown);
                return;
            }

            bool hotel = Shown >= SquareDefinition.MaxBuildingLevel;
            for (int i = 0; i < _houses.Length; i++) _houses[i].SetActive(!hotel && i < Shown);
            _mansion.SetActive(hotel);
        }

        /// <summary>
        /// The picture of <paramref name="level"/> on a tile tinted <paramref name="tint"/>, drawn
        /// now if it is not ready yet. Null when the pictures cannot be made here.
        /// </summary>
        public Texture Picture(int level, Color tint)
        {
            if (level < 0 || level >= Levels) return null;
            if (_tint != tint)
            {
                _tint = tint;
                ModelParts.Paint(_tile, tint, 0.05f);
                if (!Render()) return null;
            }

            return _pictures[level];
        }

        /// <summary>
        /// The picture of a resort's beach at <paramref name="stage"/> (1 to <see cref="ResortParts.Stages"/>) in
        /// <paramref name="owner"/>'s colour on a tile tinted <paramref name="tint"/>, drawn again when any of them
        /// changes. Null when the pictures cannot be made here.
        /// </summary>
        public Texture BeachPicture(int stage, Color tint, Color owner)
        {
            stage = Mathf.Clamp(stage, 1, ResortParts.Stages);
            if (_beachPicture != null && stage == _beachShown && tint == _beachTint && owner == _beachOwner) return _beachPicture;
            if (!CanRender()) return null;
            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (!RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    Fail("the render pipeline takes no render requests");
                    return null;
                }

                if (_beachPicture == null)
                {
                    _beachPicture = new RenderTexture(PictureSize, PictureSize, 24, RenderTextureFormat.ARGB32) { name = "beach" };
                    if (!_beachPicture.Create())
                    {
                        Fail("a render texture could not be made");
                        return null;
                    }
                }

                // The town models step aside while the beach stands on the tile; the tile's tint is put back after.
                bool[] shown = HideTown();
                ModelParts.Paint(_tile, tint, 0.05f);
                for (int i = 0; i < _beaches.Length; i++)
                {
                    _beaches[i].SetActive(i == stage - 1);
                    ResortParts.PaintOwner(_beaches[i], owner);
                }

                request.destination = _beachPicture;
                RenderPipeline.SubmitRenderRequest(_camera, request);
                foreach (GameObject beach in _beaches) beach.SetActive(false);
                RestoreTown(shown);
                if (_tint.r >= 0f) ModelParts.Paint(_tile, _tint, 0.05f);
                _beachShown = stage;
                _beachTint = tint;
                _beachOwner = owner;
                return _beachPicture;
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return null;
            }
        }

        /// <summary>Hides every town piece on the stage and says which were showing.</summary>
        private bool[] HideTown()
        {
            Transform root = _root.transform;
            var shown = new bool[root.childCount];
            for (int i = 0; i < root.childCount; i++)
            {
                GameObject child = root.GetChild(i).gameObject;
                if (child == _tile || child == _camera.gameObject || System.Array.IndexOf(_beaches, child) >= 0) continue;
                shown[i] = child.activeSelf;
                child.SetActive(false);
            }

            return shown;
        }

        private void RestoreTown(bool[] shown)
        {
            Transform root = _root.transform;
            for (int i = 0; i < shown.Length && i < root.childCount; i++)
            {
                if (shown[i]) root.GetChild(i).gameObject.SetActive(true);
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

        private bool Render()
        {
            if (!CanRender()) return false;
            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (!RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    Fail("the render pipeline takes no render requests");
                    return false;
                }

                for (int level = 0; level < Levels; level++)
                {
                    if (_pictures[level] == null)
                    {
                        _pictures[level] = new RenderTexture(PictureSize, PictureSize, 24, RenderTextureFormat.ARGB32) { name = $"building-{level}" };
                        if (!_pictures[level].Create())
                        {
                            Fail("a render texture could not be made");
                            return false;
                        }
                    }

                    Show(level);
                    request.destination = _pictures[level];
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                }

                Show(0);
                return true;
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return false;
            }
        }

        private void Fail(string why)
        {
            _failed = true;
            Debug.LogWarning($"[Lazpoly] Building pictures are off: {why}.");
            Release();
        }

        private void Release()
        {
            if (_beachPicture != null)
            {
                _beachPicture.Release();
                if (Application.isPlaying) UnityEngine.Object.Destroy(_beachPicture);
                else UnityEngine.Object.DestroyImmediate(_beachPicture);
                _beachPicture = null;
            }

            for (int i = 0; i < _pictures.Length; i++)
            {
                if (_pictures[i] == null) continue;
                _pictures[i].Release();
                if (Application.isPlaying) UnityEngine.Object.Destroy(_pictures[i]);
                else UnityEngine.Object.DestroyImmediate(_pictures[i]);
                _pictures[i] = null;
            }
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
