using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The backdrop behind the table: the sky the board floats in (<see cref="SkySkin"/>; the owner's wishes, 2026-11-11:
    /// a backdrop that suits the game's atmosphere, alive but calm, since the eye belongs on the board). Pictures filling
    /// the camera's view at the far end of it, so the board and everything on it stand in front: the sky with its glow
    /// behind the board; two small far islands rising and sinking a little in the haze; and two layers of soft cloud
    /// drifting sideways, the nearer one faster, so the sky has depth. All of it moves slowly and by little. The
    /// pictures follow the zoom and the window's shape without being stretched, and warm with the evening like the flat
    /// sky did (<see cref="SetEvening"/>). Without the picture shader the camera's flat sky colour shows, as before.
    /// </summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        /// <summary>How the whole backdrop is tinted at dusk (white at noon).</summary>
        public static readonly Color DuskTint = new Color(1f, 0.78f, 0.66f);

        /// <summary>How long the near and the far clouds take to drift once round, in seconds: slow enough to go unnoticed while playing.</summary>
        public const float NearDriftSeconds = 420f;
        public const float FarDriftSeconds = 900f;

        /// <summary>How far a far island rises and sinks from its rest (in screen heights), and how long the first takes for it; the others a little longer each.</summary>
        public const float IslandRise = 0.004f;
        public const float IslandSeconds = 11f;

        /// <summary>The pictures are drawn before everything else that is see-through: the sky first, then the far islands, the far clouds, the near clouds.</summary>
        public const int SkyQueue = 2900;

        public const string NearCloudsName = "Sky Clouds Near";
        public const string FarCloudsName = "Sky Clouds Far";

        // How much larger than the view the sky is, so no edge shows while the camera eases between zooms.
        private const float Spare = 1.05f;

        private Camera _camera;
        private Material _material, _nearMaterial, _farMaterial;
        private Transform _near, _far;
        private Transform[] _islands;
        private Material[] _islandMaterials;

        /// <summary>The cloud layers in front of the sky; null without the picture shader.</summary>
        public Transform NearClouds => _near;
        public Transform FarClouds => _far;

        /// <summary>The far islands, in <see cref="SkySkin.FarIslands"/>' order.</summary>
        public System.Collections.Generic.IReadOnlyList<Transform> FarIslands => _islands;

        /// <summary>Puts the backdrop behind <paramref name="camera"/>'s view, once; null when the picture shader is missing.</summary>
        public static SkyBackdrop Attach(Camera camera)
        {
            if (camera == null) return null;
            SkyBackdrop backdrop = camera.GetComponentInChildren<SkyBackdrop>();
            if (backdrop != null) return backdrop;
            if (!PicturePlate.IsAvailable) return null;
            GameObject plate = PicturePlate.Create("Sky Backdrop", camera.transform, SkySkin.Sky(), Vector2.one);
            backdrop = plate.AddComponent<SkyBackdrop>();
            backdrop._camera = camera;
            backdrop._material = Layer(plate, 0);
            int count = SkySkin.FarIslands.Count;
            backdrop._islands = new Transform[count];
            backdrop._islandMaterials = new Material[count];
            for (int i = 0; i < count; i++)
            {
                GameObject island = PicturePlate.Create($"Far island {i + 1}", plate.transform, SkySkin.FarIslandPicture(i), Vector2.one);
                backdrop._islands[i] = island.transform;
                backdrop._islandMaterials[i] = Layer(island, 1);
            }

            GameObject far = PicturePlate.Create(FarCloudsName, plate.transform, SkySkin.Clouds(false), Vector2.one);
            backdrop._far = far.transform;
            backdrop._farMaterial = Layer(far, 2);
            GameObject near = PicturePlate.Create(NearCloudsName, plate.transform, SkySkin.Clouds(true), Vector2.one);
            backdrop._near = near.transform;
            backdrop._nearMaterial = Layer(near, 3);
            backdrop.Show(0f);
            return backdrop;
        }

        private static Material Layer(GameObject plate, int order)
        {
            Material material = plate.GetComponent<MeshRenderer>().sharedMaterial;
            material.renderQueue = SkyQueue + order;
            return material;
        }

        /// <summary>Warms the backdrop towards dusk: 0 by day, 1 at dusk.</summary>
        public void SetEvening(float evening)
        {
            Color tint = Color.Lerp(Color.white, DuskTint, Mathf.Clamp01(evening));
            if (_material != null) _material.color = tint;
            if (_nearMaterial != null) _nearMaterial.color = tint;
            if (_farMaterial != null) _farMaterial.color = tint;
            if (_islandMaterials == null) return;
            foreach (Material material in _islandMaterials) material.color = tint;
        }

        private void LateUpdate() => Show(Time.time);

        /// <summary>
        /// The part of a picture <paramref name="picture"/> wide for its height that a view <paramref name="view"/> wide
        /// for its height shows, so the picture covers the view without being stretched: the scale (x, y) and the offset
        /// (z, w) of its texture, the cut taken evenly from both sides.
        /// </summary>
        public static Vector4 Cover(float view, float picture)
        {
            float across = Mathf.Min(1f, view / picture), up = Mathf.Min(1f, picture / view);
            return new Vector4(across, up, (1f - across) / 2f, (1f - up) / 2f);
        }

        /// <summary>How far a cloud layer has drifted at a time: a share of its picture's width, 0 to 1, round and round.</summary>
        public static float DriftAt(float seconds, float driftSeconds) => Mathf.Repeat(seconds / driftSeconds, 1f);

        /// <summary>How long a far island takes for one rise and fall.</summary>
        public static float IslandSecondsOf(int island) => IslandSeconds * (1f + 0.27f * island);

        /// <summary>How far a far island is above (or below) its rest at a time, in screen heights; each island in its own time.</summary>
        public static float IslandRiseAt(float seconds, int island) =>
            IslandRise * Mathf.Sin(2f * Mathf.PI * (seconds / IslandSecondsOf(island) + 0.37f * island));

        /// <summary>
        /// Fills the camera's view just inside its far end, whatever the zoom and the window's shape, and puts the clouds
        /// and the far islands where they are <paramref name="seconds"/> into their drift.
        /// </summary>
        public void Show(float seconds)
        {
            if (_camera == null) return;
            float height = _camera.orthographic
                ? 2f * _camera.orthographicSize
                : 2f * _camera.farClipPlane * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float aspect = _camera.aspect, step = _camera.farClipPlane * 0.004f;
            transform.localScale = new Vector3(height * aspect * Spare, height * Spare, 1f);
            transform.localPosition = new Vector3(0f, 0f, _camera.farClipPlane * 0.95f);
            transform.localRotation = Quaternion.identity;
            Vector4 cover = Cover(aspect, (float)SkySkin.Width / SkySkin.Height);
            _material.mainTextureScale = new Vector2(cover.x, cover.y);
            _material.mainTextureOffset = new Vector2(cover.z, cover.w);

            // The far islands: where they are on the screen, their pictures unstretched, each a little nearer than the sky.
            for (int i = 0; i < _islands.Length; i++)
            {
                SkySkin.FarIsland island = SkySkin.FarIslands[i];
                float wide = island.Size * SkySkin.IslandWidth / SkySkin.IslandHeight;
                _islands[i].localScale = new Vector3(wide / (aspect * Spare), island.Size / Spare, 1f);
                _islands[i].localPosition = new Vector3((island.U - 0.5f) / Spare, (island.V - 0.5f + IslandRiseAt(seconds, i)) / Spare, -step);
            }

            // The clouds nearer still; their pictures repeat sideways, so a wide view just shows more of them.
            var scale = new Vector2(aspect * SkySkin.CloudHeight / SkySkin.CloudWidth, 1f);
            _far.localPosition = new Vector3(0f, 0f, -2f * step);
            _farMaterial.mainTextureScale = scale;
            _farMaterial.mainTextureOffset = new Vector2(DriftAt(seconds, FarDriftSeconds), 0f);
            _near.localPosition = new Vector3(0f, 0f, -3f * step);
            _nearMaterial.mainTextureScale = scale;
            // Started half way round, so the two layers never line up as they were drawn.
            _nearMaterial.mainTextureOffset = new Vector2(DriftAt(seconds + NearDriftSeconds / 2f, NearDriftSeconds), 0f);
        }
    }
}
