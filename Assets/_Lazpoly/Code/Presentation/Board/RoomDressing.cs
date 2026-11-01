using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Models;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The room the board sits in: a desk under the board, a dim floor, warm key and cool fill
    /// lights, and the camera's post-processing (bloom, grading, vignette). Shared by the game
    /// and the main menu so both look the same. Quality can switch the costly parts off.
    /// </summary>
    public static class RoomDressing
    {
        private static readonly Color DeskColor = new Color(0.30f, 0.19f, 0.12f);
        private static readonly Color FloorColor = new Color(0.07f, 0.06f, 0.07f);
        private static readonly Color BackgroundColor = new Color(0.04f, 0.05f, 0.07f);

        private static Light _sun;
        private static Light _fill;

        /// <summary>Builds the desk and floor around a board of the given half width.</summary>
        public static GameObject CreateRoom(Transform parent, float boardHalfExtent)
        {
            var room = new GameObject("Room");
            room.transform.SetParent(parent, false);

            float desk = boardHalfExtent * 2f * 1.7f;
            GameObject top = ModelParts.Create("Desk", room.transform, MeshFactory.RoundedBox(new Vector3(desk, 0.5f, desk * 0.8f), 0.12f), DeskColor, 0.3f);
            top.transform.localPosition = new Vector3(0f, -0.56f, 0f);

            GameObject floor = ModelParts.Create("Floor", room.transform, MeshFactory.RoundedBox(new Vector3(120f, 0.2f, 120f), 0.1f, 1), FloorColor, 0.05f);
            floor.transform.localPosition = new Vector3(0f, -6f, 0f);
            return room;
        }

        /// <summary>The quality the player chose, or High when no settings service runs (tests, sandbox scenes).</summary>
        public static GraphicsQuality CurrentQuality()
        {
            SettingsService settings = null;
            GameServices.Registry?.TryGet(out settings);
            return settings?.Current.Quality ?? GraphicsQuality.High;
        }

        /// <summary>Re-applies a quality level to the scene that is up now: the main camera, the sun and the pipeline.</summary>
        public static void ApplyQuality(GraphicsQuality quality)
        {
            Camera camera = Camera.main;
            if (camera != null && camera.GetComponent<Volume>() != null) DressCamera(camera, quality);
            GameObject sun = GameObject.Find("Sun");
            if (sun != null && sun.TryGetComponent(out Light light)) light.shadows = ShadowsFor(quality);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.msaaSampleCount = quality == GraphicsQuality.High ? 4 : quality == GraphicsQuality.Medium ? 2 : 1;
                pipeline.renderScale = quality == GraphicsQuality.Low ? 0.85f : 1f;
            }
        }

        /// <summary>Soft shadows on High, hard on Medium, none on Low.</summary>
        public static LightShadows ShadowsFor(GraphicsQuality quality) =>
            quality == GraphicsQuality.High ? LightShadows.Soft : quality == GraphicsQuality.Medium ? LightShadows.Hard : LightShadows.None;

        /// <summary>Warm sun from the front left, a cool fill from behind, shadows as the quality allows, a dim warm ambient.</summary>
        public static void LightScene(GraphicsQuality quality)
        {
            // Scenes come with Unity's default "Directional Light"; it becomes the sun. A fill from an
            // earlier visit (the menu and the game share one run) is rebuilt, not doubled.
            GameObject oldFill = GameObject.Find("Fill Light");
            if (oldFill != null) Object.Destroy(oldFill);
            GameObject sunObject = GameObject.Find("Sun") ?? GameObject.Find("Directional Light") ?? new GameObject("Sun");
            Light sun = sunObject.GetComponent<Light>();
            if (sun == null) sun = sunObject.AddComponent<Light>();
            sun.name = "Sun";
            sun.type = LightType.Directional;
            sun.shadows = ShadowsFor(quality);
            sun.shadowStrength = 0.55f;

            Light fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;

            _sun = sun;
            _fill = fill;
            RenderSettings.ambientMode = AmbientMode.Flat;
            ApplyDayLight(DayCycle.Noon);
        }

        /// <summary>Sets the sun, the fill and the ambient to one moment of the day (see <see cref="DayCycle"/>).</summary>
        public static void ApplyDayLight(in DayLight light)
        {
            if (_sun == null) _sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (_fill == null) _fill = GameObject.Find("Fill Light")?.GetComponent<Light>();
            if (_sun != null)
            {
                _sun.color = light.SunColor;
                _sun.intensity = light.SunIntensity;
                _sun.transform.rotation = Quaternion.Euler(light.SunPitch, light.SunYaw, 0f);
            }

            if (_fill != null)
            {
                _fill.color = light.FillColor;
                _fill.intensity = light.FillIntensity;
                // The fill comes from behind the sun's far side, so the evening shadows keep some blue in them.
                _fill.transform.rotation = Quaternion.Euler(35f, light.SunYaw + 185f, 0f);
            }

            RenderSettings.ambientLight = light.Ambient;
        }

        /// <summary>
        /// Post-processing on the camera: a little bloom on the highlights, warm grading, a soft vignette.
        /// Low quality turns all of it off; Medium keeps it with cheaper anti-aliasing.
        /// </summary>
        public static void DressCamera(Camera camera, GraphicsQuality quality)
        {
            bool postProcessing = quality != GraphicsQuality.Low;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.allowHDR = postProcessing;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = postProcessing;
            data.antialiasing = quality == GraphicsQuality.High ? AntialiasingMode.SubpixelMorphologicalAntiAliasing
                : quality == GraphicsQuality.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;

            Volume volume = camera.GetComponent<Volume>();
            if (volume == null)
            {
                volume = camera.gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.profile = CreateProfile();
            }

            volume.enabled = postProcessing;
        }

        private static VolumeProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Lazpoly Look";

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);

            var grading = profile.Add<ColorAdjustments>();
            grading.postExposure.Override(0.15f);
            grading.contrast.Override(10f);
            grading.saturation.Override(8f);
            grading.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.02f, 0.03f, 0.05f));
            return profile;
        }
    }
}
