using Lazpoly.Infra;
using Lazpoly.Infra.Settings;
using Lazpoly.Presentation.Models;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lazpoly.Presentation.Tabletop
{
    /// <summary>
    /// The world around the board: the sky it floats in, warm key and cool fill lights, and the
    /// camera's post-processing (bloom, grading, vignette). Shared by the game and the main menu so
    /// both look the same. Quality can switch the costly parts off.
    /// </summary>
    public static class RoomDressing
    {
        /// <summary>The sky behind the board at noon: a light, clear blue.</summary>
        public static readonly Color SkyDay = new Color(0.64f, 0.85f, 0.97f);

        /// <summary>The sky at dusk: warm and rosy.</summary>
        public static readonly Color SkyDusk = new Color(0.93f, 0.62f, 0.48f);

        private static Light _sun;
        private static Light _fill;

        /// <summary>The sky at one moment of the day.</summary>
        public static Color SkyAt(float evening) => Color.Lerp(SkyDay, SkyDusk, Mathf.Clamp01(evening));

        /// <summary>The quality the player chose, or High when no settings service runs (tests, sandbox scenes).</summary>
        public static GraphicsQuality CurrentQuality()
        {
            SettingsService settings = null;
            GameServices.Registry?.TryGet(out settings);
            return settings?.Current.Quality ?? GraphicsQuality.High;
        }

        /// <summary>The whole table fits well inside this distance from the camera, so shadows stop there and stay sharp.</summary>
        public const float ShadowDistance = 40f;

        /// <summary>
        /// Temporal anti-aliasing on High (the owner's wish): it blends each frame with the last ones at jittered sample
        /// positions, so thin lines, far edges and fine texture detail stop crawling. It needs linear colour space (in gamma
        /// URP's TAA brightened every colour once more: the sky went from A2D7F7 to CFEBFB), which the project uses since
        /// Faz 25. URP's TAA cannot run with MSAA, so High draws without it; Medium keeps 2x MSAA with FXAA, Low has none.
        /// </summary>
        public static bool UsesTemporalAntiAliasing(GraphicsQuality quality) => quality == GraphicsQuality.High;

        /// <summary>MSAA samples per quality: none with TAA (High) or on Low, two on Medium.</summary>
        public static int MsaaFor(GraphicsQuality quality) => quality == GraphicsQuality.Medium ? 2 : 1;

        /// <summary>
        /// Anisotropic filtering per quality: forced on every texture on High and Medium (the tiles lie at a slant to the
        /// camera and blurred without it), left to each texture's own level on Low.
        /// </summary>
        public static AnisotropicFiltering AnisotropicFor(GraphicsQuality quality) =>
            quality == GraphicsQuality.Low ? AnisotropicFiltering.Enable : AnisotropicFiltering.ForceEnable;

        /// <summary>
        /// How hard TAA sharpens what its blending softens (URP's contrast adaptive sharpening, 0-1). Off: at 0.4 it made
        /// every edge of a still table shimmer from frame to frame (measured on captured frames; worst on the tax document's
        /// black lines on white), and without it the edges stay as still as with MSAA.
        /// </summary>
        public const float TaaSharpening = 0f;

        /// <summary>Re-applies a quality level to the scene that is up now: the main camera, the sun and the pipeline.</summary>
        public static void ApplyQuality(GraphicsQuality quality)
        {
            QualitySettings.anisotropicFiltering = AnisotropicFor(quality);
            Texture.SetGlobalAnisotropicFilteringLimits(8, 16);
            BuildingModels.SetFullDetail(FullBuildingsFor(quality));
            FigureModels.SetFullDetail(FullBuildingsFor(quality));
            Camera camera = Camera.main;
            if (camera != null && camera.GetComponent<Volume>() != null) DressCamera(camera, quality);
            GameObject sun = GameObject.Find("Sun");
            if (sun != null && sun.TryGetComponent(out Light light)) light.shadows = ShadowsFor(quality);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)
            {
                pipeline.msaaSampleCount = MsaaFor(quality);
                pipeline.renderScale = quality == GraphicsQuality.Low ? 0.85f : 1f;
                // One shadow map over the table is enough; cascades would spend the budget on a distance nobody sees.
                pipeline.shadowCascadeCount = 1;
                pipeline.shadowDistance = ShadowDistance;
                pipeline.mainLightShadowmapResolution = ShadowResolutionFor(quality);
            }
        }

        /// <summary>Shadows stay soft on every level: hard-edged shadows under the pieces look like a mistake. Low pays with a smaller map.</summary>
        public static LightShadows ShadowsFor(GraphicsQuality quality) => LightShadows.Soft;

        /// <summary>Shadow map size per quality; the single cascade keeps even the small one sharp over the board.</summary>
        public static int ShadowResolutionFor(GraphicsQuality quality) =>
            quality == GraphicsQuality.High ? 4096 : quality == GraphicsQuality.Medium ? 2048 : 1024;

        /// <summary>The buildings stand whole, as their maker made them, on High; Medium and Low show their light copies.</summary>
        public static bool FullBuildingsFor(GraphicsQuality quality) => quality == GraphicsQuality.High;

        /// <summary>Post-processing runs on Medium and High; Low draws the plain picture.</summary>
        public static bool PostProcessingOn(GraphicsQuality quality) => quality != GraphicsQuality.Low;

        /// <summary>
        /// Whether the colour effects (grading, tonemapping, vignette, bloom) draw. Built games never had them (the URP
        /// renderer lacked its post-processing resources until Faz 25), so the bright, pastel look the owner approved is the
        /// look without them; switched on, the table turned darker and greyer. Post-processing itself stays on for the
        /// anti-aliasing (TAA on High, FXAA on Medium).
        /// </summary>
        public static readonly bool LookEffectsOn = false;

        /// <summary>Whether the camera runs URP's post-processing (the edge smoothing needs it).</summary>
        public static bool RunsPostProcessing(Camera camera) => camera.GetUniversalAdditionalCameraData().renderPostProcessing;

        /// <summary>Whether the camera smooths edges after drawing (TAA or FXAA).</summary>
        public static bool SmoothsEdges(Camera camera) => camera.GetUniversalAdditionalCameraData().antialiasing != AntialiasingMode.None;

        /// <summary>Whether the camera smooths edges over time (TAA).</summary>
        public static bool SmoothsOverTime(Camera camera) =>
            camera.GetUniversalAdditionalCameraData().antialiasing == AntialiasingMode.TemporalAntiAliasing;

        /// <summary>Whether the camera's colour effects (its volume) draw.</summary>
        public static bool ShowsLookEffects(Camera camera) => camera.TryGetComponent(out Volume volume) && volume.enabled;

        /// <summary>Bloom is the costliest effect and the one that made the labels glow, so only High keeps it.</summary>
        public static bool BloomOn(GraphicsQuality quality) => quality == GraphicsQuality.High;

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
            sun.shadowStrength = DayCycle.GammaShadowStrength;

            Light fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.shadows = LightShadows.None;

            _sun = sun;
            _fill = fill;
            RenderSettings.ambientMode = AmbientMode.Flat;
            ApplyDayLight(DayCycle.Noon);
        }

        /// <summary>
        /// The lights for one moment of the day in the colour space the project draws in: linear since Faz 25 (for TAA),
        /// with the lights re-tuned so the table keeps the look the owner approved in gamma (see <see cref="DayCycle.LinearRig"/>).
        /// </summary>
        public static LightRig RigFor(in DayLight light) =>
            QualitySettings.activeColorSpace == ColorSpace.Linear ? DayCycle.LinearRig(light) : DayCycle.GammaRig(light);

        /// <summary>Sets the sun, the fill, the ambient and the sky to one moment of the day (see <see cref="DayCycle"/>).</summary>
        public static void ApplyDayLight(in DayLight light)
        {
            if (_sun == null) _sun = GameObject.Find("Sun")?.GetComponent<Light>();
            if (_fill == null) _fill = GameObject.Find("Fill Light")?.GetComponent<Light>();
            LightRig rig = RigFor(light);
            if (_sun != null)
            {
                _sun.color = rig.SunColor;
                _sun.intensity = rig.SunIntensity;
                _sun.shadowStrength = rig.ShadowStrength;
                _sun.transform.rotation = Quaternion.Euler(light.SunPitch, light.SunYaw, 0f);
            }

            if (_fill != null)
            {
                _fill.color = rig.FillColor;
                _fill.intensity = rig.FillIntensity;
                // The fill comes from behind the sun's far side, so the evening shadows keep some blue in them.
                _fill.transform.rotation = Quaternion.Euler(DayCycle.FillPitch, light.SunYaw + 185f, 0f);
            }

            RenderSettings.ambientLight = rig.Ambient;
            Camera camera = Camera.main;
            if (camera != null) camera.backgroundColor = SkyAt(light.Evening);
        }

        /// <summary>
        /// Post-processing on the camera: a little bloom on the highlights, warm grading, a soft vignette.
        /// Low quality turns all of it off; Medium keeps it with cheaper anti-aliasing.
        /// </summary>
        public static void DressCamera(Camera camera, GraphicsQuality quality)
        {
            BuildingModels.SetFullDetail(FullBuildingsFor(quality));
            FigureModels.SetFullDetail(FullBuildingsFor(quality));
            bool postProcessing = PostProcessingOn(quality);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = SkyDay;
            camera.allowHDR = postProcessing;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = postProcessing;
            data.antialiasing = UsesTemporalAntiAliasing(quality) ? AntialiasingMode.TemporalAntiAliasing
                : quality == GraphicsQuality.Medium ? AntialiasingMode.FastApproximateAntialiasing : AntialiasingMode.None;
            if (UsesTemporalAntiAliasing(quality))
            {
                data.antialiasingQuality = AntialiasingQuality.High;
                data.taaSettings.quality = TemporalAAQuality.High;
                data.taaSettings.contrastAdaptiveSharpening = TaaSharpening;
            }

            Volume volume = camera.GetComponent<Volume>();
            if (volume == null)
            {
                volume = camera.gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.profile = CreateProfile();
            }

            volume.enabled = postProcessing && LookEffectsOn;
            if (volume.profile != null && volume.profile.TryGet(out Bloom bloom)) bloom.active = BloomOn(quality);
        }

        private static VolumeProfile CreateProfile()
        {
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Lazpoly Look";

            // A gentle bloom on real highlights only; the earlier setting made the white labels glow.
            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1.25f);
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.55f);

            var grading = profile.Add<ColorAdjustments>();
            grading.postExposure.Override(0f);
            grading.contrast.Override(10f);
            grading.saturation.Override(8f);
            grading.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            // A faint darkening of the corners only; the sky must stay light.
            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.12f);
            vignette.smoothness.Override(0.6f);
            vignette.color.Override(new Color(0.25f, 0.35f, 0.50f));
            return profile;
        }
    }
}
