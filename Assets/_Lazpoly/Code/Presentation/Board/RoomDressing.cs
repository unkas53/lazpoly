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

        /// <summary>Warm sun from the front left, a cool fill from behind, soft shadows, a dim warm ambient.</summary>
        public static void LightScene()
        {
            Light sun = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.name == "Fill Light") Object.Destroy(light.gameObject);
                else if (light.type == LightType.Directional && sun == null) sun = light;
            }

            if (sun == null) sun = new GameObject("Directional Light").AddComponent<Light>();
            sun.name = "Sun";
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.intensity = 1.25f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            Light fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.55f, 0.70f, 0.90f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;
            fill.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.26f, 0.27f, 0.32f);
        }

        /// <summary>Post-processing on the camera: a little bloom on the highlights, warm grading, a soft vignette.</summary>
        public static void DressCamera(Camera camera, bool postProcessing)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.allowHDR = true;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = postProcessing;
            data.antialiasing = postProcessing ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;

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
