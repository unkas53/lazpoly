using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The backdrop behind the table: the sky the board floats in (<see cref="SkySkin"/>; the owner's wish, 2026-11-11,
    /// was a backdrop that suits the game's atmosphere, after Faz 25's plain glow). Two pictures filling the camera's view
    /// at the far end of it, so the board and everything on it stand in front: the sky with its glow behind the board,
    /// its far clouds and far islands, and in front of it a layer of nearer clouds drifting slowly sideways. Both follow
    /// the zoom and the window's shape without being stretched, and warm with the evening like the flat sky did
    /// (<see cref="SetEvening"/>). Without the picture shader the camera's flat sky colour shows, as before.
    /// </summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        /// <summary>How the whole backdrop is tinted at dusk (white at noon).</summary>
        public static readonly Color DuskTint = new Color(1f, 0.78f, 0.66f);

        /// <summary>How long the near clouds take to drift once round, in seconds: slow enough to go unnoticed while playing.</summary>
        public const float DriftSeconds = 480f;

        /// <summary>The pictures are drawn before everything else that is see-through, the sky first.</summary>
        public const int SkyQueue = 2900;

        public const string CloudsName = "Sky Clouds";

        private Camera _camera;
        private Material _material, _cloudMaterial;
        private Transform _clouds;

        /// <summary>The cloud layer in front of the sky; null without the picture shader.</summary>
        public Transform Clouds => _clouds;

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
            backdrop._material = plate.GetComponent<MeshRenderer>().sharedMaterial;
            backdrop._material.renderQueue = SkyQueue;
            GameObject clouds = PicturePlate.Create(CloudsName, plate.transform, SkySkin.Clouds(), Vector2.one);
            backdrop._clouds = clouds.transform;
            backdrop._cloudMaterial = clouds.GetComponent<MeshRenderer>().sharedMaterial;
            backdrop._cloudMaterial.renderQueue = SkyQueue + 1;
            backdrop.Fit();
            return backdrop;
        }

        /// <summary>Warms the backdrop towards dusk: 0 by day, 1 at dusk.</summary>
        public void SetEvening(float evening)
        {
            Color tint = Color.Lerp(Color.white, DuskTint, Mathf.Clamp01(evening));
            if (_material != null) _material.color = tint;
            if (_cloudMaterial != null) _cloudMaterial.color = tint;
        }

        private void LateUpdate() => Fit();

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

        /// <summary>How far the near clouds have drifted at a time: a share of their picture's width, 0 to 1, round and round.</summary>
        public static float DriftAt(float seconds) => Mathf.Repeat(seconds / DriftSeconds, 1f);

        /// <summary>Fills the camera's view just inside its far end, whatever the zoom and the window's shape.</summary>
        private void Fit()
        {
            if (_camera == null) return;
            float height = _camera.orthographic
                ? 2f * _camera.orthographicSize
                : 2f * _camera.farClipPlane * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            // A little larger than the view, so no edge shows while the camera eases between zooms.
            transform.localScale = new Vector3(height * _camera.aspect * 1.05f, height * 1.05f, 1f);
            transform.localPosition = new Vector3(0f, 0f, _camera.farClipPlane * 0.95f);
            transform.localRotation = Quaternion.identity;
            Vector4 cover = Cover(_camera.aspect, (float)SkySkin.Width / SkySkin.Height);
            _material.mainTextureScale = new Vector2(cover.x, cover.y);
            _material.mainTextureOffset = new Vector2(cover.z, cover.w);
            if (_clouds == null) return;
            // The clouds a touch nearer than the sky, which carries them; their picture repeats sideways, so a wide view
            // just shows more of it.
            _clouds.localPosition = new Vector3(0f, 0f, -_camera.farClipPlane * 0.01f);
            _cloudMaterial.mainTextureScale = new Vector2(_camera.aspect * SkySkin.CloudHeight / SkySkin.CloudWidth, 1f);
            _cloudMaterial.mainTextureOffset = new Vector2(DriftAt(Time.time), 0f);
        }
    }
}
