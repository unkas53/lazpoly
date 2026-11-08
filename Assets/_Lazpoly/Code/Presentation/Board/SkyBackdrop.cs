using Lazpoly.Presentation.Models;
using UnityEngine;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The backdrop behind the table (Faz 25, the owner wanted something more modern than the flat sky): a soft light
    /// glow behind the board fading to a deeper sky blue at the screen's edges, with a faint lift towards the top. It is a
    /// picture filling the camera's view at the far end of it, so the board and everything on it stand in front; it
    /// follows the zoom and the window's shape, and warms with the evening like the flat sky did (<see cref="SetEvening"/>).
    /// Without the picture shader the camera's flat sky colour shows, as before.
    /// </summary>
    public sealed class SkyBackdrop : MonoBehaviour
    {
        /// <summary>The glow in the middle, behind the board.</summary>
        public static readonly Color Glow = new Color(0xE6 / 255f, 0xF4 / 255f, 0xFC / 255f);

        /// <summary>The sky at the screen's edges: the approved sky blue (A2D7F7), a shade deeper.</summary>
        public static readonly Color Edge = new Color(0x8E / 255f, 0xC9 / 255f, 0xEE / 255f);

        /// <summary>How the whole backdrop is tinted at dusk (white at noon).</summary>
        public static readonly Color DuskTint = new Color(1f, 0.78f, 0.66f);

        /// <summary>Where the glow sits on the screen (0-1 across and up): a little above the middle, behind the board.</summary>
        public static readonly Vector2 GlowCentre = new Vector2(0.5f, 0.56f);

        public const int PictureSize = 256;

        private static Texture2D _picture;
        private Camera _camera;
        private Material _material;

        /// <summary>Puts the backdrop behind <paramref name="camera"/>'s view, once; null when the picture shader is missing.</summary>
        public static SkyBackdrop Attach(Camera camera)
        {
            if (camera == null) return null;
            SkyBackdrop backdrop = camera.GetComponentInChildren<SkyBackdrop>();
            if (backdrop != null) return backdrop;
            if (!PicturePlate.IsAvailable) return null;
            GameObject plate = PicturePlate.Create("Sky Backdrop", camera.transform, Picture(), Vector2.one);
            backdrop = plate.AddComponent<SkyBackdrop>();
            backdrop._camera = camera;
            backdrop._material = plate.GetComponent<MeshRenderer>().sharedMaterial;
            backdrop.Fit();
            return backdrop;
        }

        /// <summary>Warms the backdrop towards dusk: 0 by day, 1 at dusk.</summary>
        public void SetEvening(float evening)
        {
            if (_material != null) _material.color = Color.Lerp(Color.white, DuskTint, Mathf.Clamp01(evening));
        }

        private void LateUpdate() => Fit();

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
        }

        /// <summary>
        /// The backdrop's picture: <see cref="Glow"/> at <see cref="GlowCentre"/> easing out to <see cref="Edge"/>, the
        /// top a touch lighter than the bottom, with a faint dither so the wide gradient shows no bands.
        /// </summary>
        public static Texture2D Picture()
        {
            if (_picture != null) return _picture;
            var picture = new Texture2D(PictureSize, PictureSize, TextureFormat.RGBA32, false)
            {
                name = "sky-backdrop", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave,
            };
            var pixels = new Color32[PictureSize * PictureSize];
            var random = new System.Random(7);
            for (int y = 0; y < PictureSize; y++)
            {
                for (int x = 0; x < PictureSize; x++)
                {
                    float u = (x + 0.5f) / PictureSize, v = (y + 0.5f) / PictureSize;
                    pixels[y * PictureSize + x] = Dither(ColourAt(u, v), random);
                }
            }

            picture.SetPixels32(pixels);
            picture.Apply(false, false);
            return _picture = picture;
        }

        /// <summary>The backdrop's colour at a point of the screen (u across, v up, both 0-1).</summary>
        public static Color ColourAt(float u, float v)
        {
            // Distance from the glow, the screen's width counting a little more (the view is wide).
            float dx = (u - GlowCentre.x) * 1.25f, dy = v - GlowCentre.y;
            float distance = Mathf.Sqrt(dx * dx + dy * dy) / 0.75f;
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance));
            Color colour = Color.Lerp(Glow, Edge, fade);
            // The top a touch lighter, as a sky is towards its zenith in the light of a table lamp.
            return Color.Lerp(colour, Glow, 0.18f * Mathf.Clamp01(v - 0.5f) * 2f);
        }

        private static Color32 Dither(Color colour, System.Random random)
        {
            float Channel(float c) => Mathf.Clamp01(c + ((float)random.NextDouble() - 0.5f) / 255f);
            return new Color(Channel(colour.r), Channel(colour.g), Channel(colour.b), 1f);
        }
    }
}
