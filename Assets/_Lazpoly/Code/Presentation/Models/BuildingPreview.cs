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
    /// The little pictures on the property card: a paper tile with nothing, one to four houses or
    /// the mansion on it, drawn with the game's own building models from the board's camera angle.
    /// A stage far below the board holds one tile and the pieces; a camera of its own renders it
    /// into one texture per level the first time a card needs them, and again when the tile's
    /// colour changes. Nothing renders every frame. In the editor, or where the render pipeline
    /// cannot take a render request, the pictures stay null and the card shows an icon instead.
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

        /// <summary>The houses in a row across the tile, a little behind its middle so the land in front stays visible.</summary>
        public const float HouseSize = 0.24f;
        public const float HouseStep = 0.3f;
        public const float RowDepth = 0.12f;
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
        private readonly GameObject _mansion;
        private readonly Camera _camera;
        private readonly RenderTexture[] _pictures = new RenderTexture[Levels];
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

        /// <summary>The level the stage shows now: how many houses stand, 5 for the mansion.</summary>
        public int Shown { get; private set; }

        /// <summary>Puts the pieces of <paramref name="level"/> on the tile and hides the rest.</summary>
        public void Show(int level)
        {
            Shown = Mathf.Clamp(level, 0, Levels - 1);
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

        private bool Render()
        {
            if (_failed || !Application.isPlaying) return false;
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
                        _pictures[level].Create();
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
