using Lazpoly.Presentation.Models;
using Lazpoly.Presentation.Tabletop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lazpoly.Presentation.Tests
{
    /// <summary>
    /// Guards against the board drawing magenta in player builds: the project must render with a
    /// pipeline asset, and the models' material must live in Resources (so builds include its shader)
    /// with a shader that pipeline supports.
    /// </summary>
    public class RenderingSetupTests
    {
        [Test]
        public void ProjectRendersWithUniversalPipeline()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            Assert.IsNotNull(pipeline, "No render pipeline asset; run Lazpoly/Set Up Render Pipeline.");
            StringAssert.Contains("Universal", pipeline.GetType().Name);

            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                RenderPipelineAsset levelPipeline = QualitySettings.GetRenderPipelineAssetAt(level);
                Assert.IsTrue(levelPipeline == null || levelPipeline == pipeline,
                    $"Quality level '{QualitySettings.names[level]}' uses another pipeline.");
            }
        }

        [Test]
        public void TheRendererHasItsPostProcessingResources()
        {
            // Made from a script (Faz 6c), the renderer never stored URP's post-processing resources; the editor fills
            // them in while it runs, but a built game could go without them (no SMAA, bloom, grading or vignette).
            // So the asset file itself must name them.
            string path = System.IO.Path.Combine(Application.dataPath, "_Lazpoly", "Settings", "Lazpoly_URP_Renderer.asset");
            string text = System.IO.File.ReadAllText(path);
            StringAssert.Contains("postProcessData: {fileID: 11400000, guid: ", text, "the renderer asset names URP's PostProcessData");
            StringAssert.DoesNotContain("postProcessData: {fileID: 0}", text);
        }

        [Test]
        public void EveryQualityKeepsSoftShadowsAndOnlyHighBlooms()
        {
            foreach (Lazpoly.Infra.Settings.GraphicsQuality quality in System.Enum.GetValues(typeof(Lazpoly.Infra.Settings.GraphicsQuality)))
            {
                Assert.AreEqual(LightShadows.Soft, RoomDressing.ShadowsFor(quality), $"{quality} loses its soft shadows");
                Assert.GreaterOrEqual(RoomDressing.ShadowResolutionFor(quality), 1024);
            }

            Assert.Less(RoomDressing.ShadowResolutionFor(Lazpoly.Infra.Settings.GraphicsQuality.Low), RoomDressing.ShadowResolutionFor(Lazpoly.Infra.Settings.GraphicsQuality.High));
            Assert.IsFalse(RoomDressing.PostProcessingOn(Lazpoly.Infra.Settings.GraphicsQuality.Low));
            Assert.IsTrue(RoomDressing.PostProcessingOn(Lazpoly.Infra.Settings.GraphicsQuality.Medium));
            Assert.IsFalse(RoomDressing.BloomOn(Lazpoly.Infra.Settings.GraphicsQuality.Medium), "Medium runs the grading without bloom");
            Assert.IsTrue(RoomDressing.BloomOn(Lazpoly.Infra.Settings.GraphicsQuality.High));
            Assert.IsFalse(RoomDressing.LookEffectsOn, "the owner's bright look: no grading, tonemapping or vignette");

            var holder = new GameObject("Camera Test");
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                RoomDressing.DressCamera(camera, Lazpoly.Infra.Settings.GraphicsQuality.High);
                Assert.IsTrue(RoomDressing.RunsPostProcessing(camera), "post-processing runs for the edge smoothing");
                Assert.IsTrue(RoomDressing.SmoothsEdges(camera), "edges smoothed on High");
                Assert.IsTrue(RoomDressing.SmoothsOverTime(camera), "TAA on High, now that the project draws in linear colour space");
                RoomDressing.DressCamera(camera, Lazpoly.Infra.Settings.GraphicsQuality.Medium);
                Assert.IsTrue(RoomDressing.SmoothsEdges(camera), "FXAA on Medium");
                Assert.IsFalse(RoomDressing.SmoothsOverTime(camera));
                RoomDressing.DressCamera(camera, Lazpoly.Infra.Settings.GraphicsQuality.High);
                Assert.IsFalse(RoomDressing.ShowsLookEffects(camera), "but the colour effects stay off");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void EdgesAreSmoothedWithMsaaAndTexturesFilterAtASlant()
        {
            // In gamma colour space URP's TAA brightened every colour once more; linear lets it run on High.
            Assert.AreEqual(ColorSpace.Linear, QualitySettings.activeColorSpace, "TAA washes the colours out in gamma colour space");
            Assert.IsTrue(RoomDressing.UsesTemporalAntiAliasing(Lazpoly.Infra.Settings.GraphicsQuality.High));
            Assert.IsFalse(RoomDressing.UsesTemporalAntiAliasing(Lazpoly.Infra.Settings.GraphicsQuality.Medium));
            Assert.IsFalse(RoomDressing.UsesTemporalAntiAliasing(Lazpoly.Infra.Settings.GraphicsQuality.Low));
            Assert.AreEqual(1, RoomDressing.MsaaFor(Lazpoly.Infra.Settings.GraphicsQuality.High), "URP's TAA cannot run with MSAA");
            Assert.AreEqual(2, RoomDressing.MsaaFor(Lazpoly.Infra.Settings.GraphicsQuality.Medium));
            Assert.AreEqual(1, RoomDressing.MsaaFor(Lazpoly.Infra.Settings.GraphicsQuality.Low));
            Assert.AreEqual(AnisotropicFiltering.ForceEnable, RoomDressing.AnisotropicFor(Lazpoly.Infra.Settings.GraphicsQuality.High));
            Assert.AreEqual(AnisotropicFiltering.ForceEnable, RoomDressing.AnisotropicFor(Lazpoly.Infra.Settings.GraphicsQuality.Medium));
            Assert.AreEqual(AnisotropicFiltering.Enable, RoomDressing.AnisotropicFor(Lazpoly.Infra.Settings.GraphicsQuality.Low));
        }

        [Test]
        public void TheBackdropGlowsBehindTheBoardAndFillsTheView()
        {
            Color middle = SkySkin.ColourAt(SkySkin.GlowCentre.x, SkySkin.GlowCentre.y);
            Color corner = SkySkin.ColourAt(0f, 0f);
            Assert.Greater(middle.grayscale, corner.grayscale + 0.1f, "light behind the board, deeper at the edges");
            Assert.Greater(corner.b, corner.r + 0.2f, "still a sky blue");

            var holder = new GameObject("Backdrop Camera");
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                SkyBackdrop backdrop = SkyBackdrop.Attach(camera);
                Assert.IsNotNull(backdrop, "the picture shader is missing from Resources");
                Assert.AreSame(backdrop, SkyBackdrop.Attach(camera), "one backdrop per camera");
                Assert.GreaterOrEqual(backdrop.transform.localScale.y, 10f, "it fills the view's height");
                Assert.Less(backdrop.transform.localPosition.z, camera.farClipPlane, "inside the far end, so it is drawn");

                // Two layers of cloud ride in front of the sky, the near one in front, and behind everything else.
                Assert.IsNotNull(backdrop.NearClouds, "a near layer of clouds");
                Assert.IsNotNull(backdrop.FarClouds, "and a far one");
                Assert.Less(backdrop.FarClouds.position.z, backdrop.transform.position.z, "nearer than the sky");
                Assert.Less(backdrop.NearClouds.position.z, backdrop.FarClouds.position.z, "the near layer in front");
                Material sky = backdrop.GetComponent<Renderer>().sharedMaterial;
                Material near = backdrop.NearClouds.GetComponent<Renderer>().sharedMaterial, far = backdrop.FarClouds.GetComponent<Renderer>().sharedMaterial;
                Assert.AreSame(SkySkin.Sky(), sky.mainTexture);
                Assert.AreSame(SkySkin.Clouds(true), near.mainTexture);
                Assert.AreSame(SkySkin.Clouds(false), far.mainTexture);
                Assert.Less(sky.renderQueue, far.renderQueue, "the sky is drawn first");
                Assert.Less(far.renderQueue, near.renderQueue);
                Assert.Less(near.renderQueue, (int)UnityEngine.Rendering.RenderQueue.Transparent, "and all before the table's own see-through things");
                Assert.AreEqual(camera.aspect * SkySkin.CloudHeight / SkySkin.CloudWidth, near.mainTextureScale.x, 1e-4f, "the clouds keep their shape whatever the window's");

                // The far islands hang at the sides between the sky and the clouds: models (the owner's wish, 2026-11-12:
                // three-dimensional), unstretched, upright in the world, rising and sinking a little and turning slowly.
                Assert.AreEqual(SkySkin.FarIslands.Count, backdrop.FarIslands.Count);
                for (int i = 0; i < backdrop.FarIslands.Count; i++)
                {
                    Transform island = backdrop.FarIslands[i];
                    backdrop.Show(0f);
                    Assert.Less(island.position.z, backdrop.transform.position.z, "in front of the sky");
                    Assert.Greater(island.position.z, backdrop.FarClouds.position.z, "behind the clouds");
                    Assert.IsNotNull(island.Find(IslandParts.FarModelName).Find(IslandParts.RockName), "a model, not a picture");
                    Assert.IsNotNull(island.GetComponentInChildren<IslandDrift>(), "its loose stones drift");
                    Vector3 size = island.lossyScale;
                    Assert.AreEqual(size.x, size.y, 1e-4f, "it keeps its shape");
                    Assert.AreEqual(size.x, size.z, 1e-4f);
                    Assert.AreEqual(SkySkin.FarIslands[i].Size * 10f / IslandShape.FarSpan, size.y, 1e-3f, "its size is a share of the view's height");
                    Assert.Greater(Mathf.Abs(island.localPosition.x), 0.35f * 10f * camera.aspect, "at the side of the screen");
                    Assert.Less(Vector3.Angle(island.up, Vector3.up), 0.01f, "upright in the world");
                    float quarter = SkyBackdrop.IslandSecondsOf(i) * (0.25f - 0.37f * i);
                    backdrop.Show(quarter);
                    float up = island.position.y;
                    backdrop.Show(quarter + SkyBackdrop.IslandSecondsOf(i) / 2f);
                    Assert.AreEqual(2f * SkyBackdrop.IslandRise * 10f, up - island.position.y, 1e-3f, "it rises and sinks by a little");
                    backdrop.Show(0f);
                    float turned = island.eulerAngles.y;
                    backdrop.Show(20f);
                    float turn = Mathf.Abs(Mathf.DeltaAngle(turned, island.eulerAngles.y));
                    Assert.AreEqual(360f * 20f / (SkyBackdrop.IslandTurnSeconds * (1f + 0.31f * i)), turn, 0.05f, "it turns about its upright, so its depth shows");
                    Assert.Less(turn / 20f, 2f, "slowly: under two degrees a second");
                    Assert.Less(Vector3.Angle(island.up, Vector3.up), 0.01f, "and stays upright");
                }

                Assert.Less(Mathf.DeltaAngle(0f, SkyBackdrop.IslandTurnAt(10f, 0)) * Mathf.DeltaAngle(137f, SkyBackdrop.IslandTurnAt(10f, 1)), 0f, "the two turn opposite ways");

                // The aeroplane (the owner's wish, 2026-11-12): out of sight until its time, then level and nose first straight
                // across the view, behind the clouds and before the far islands; the next one comes back the other way.
                Transform plane = backdrop.Plane;
                Assert.IsNotNull(plane);
                backdrop.Show(0f);
                Assert.IsFalse(plane.gameObject.activeSelf, "no plane at first");
                backdrop.Show(SkyPlane.FirstAt + SkyPlane.CrossSeconds / 2f);
                Assert.IsTrue(plane.gameObject.activeSelf, "one crossing");
                Assert.AreEqual(0f, plane.localPosition.x, 1e-3f, "half way: in the middle of the view");
                Assert.AreEqual((SkyPlane.HeightOf(0) - 0.5f) * 10f, plane.localPosition.y, 1e-3f, "at its pass's height");
                Assert.Greater(plane.position.z, backdrop.FarClouds.position.z, "behind the clouds");
                Assert.Less(plane.position.z, backdrop.FarIslands[0].position.z, "before the far islands");
                Assert.AreEqual(SkyPlane.Size * 10f / PlaneParts.Length, plane.lossyScale.x, 1e-4f, "its length is a share of the view's height");
                Assert.Less(Vector3.Angle(plane.forward, camera.transform.right), 0.01f, "nose first to the right");
                Assert.AreEqual(SkyPlane.Bank, Vector3.Angle(plane.up, Vector3.up), 0.05f, "banked a little");
                Assert.Greater(Vector3.Dot(plane.up, camera.transform.forward), 0f, "away from the camera, so its side shows");
                backdrop.Show(SkyPlane.FirstAt + SkyPlane.CrossSeconds + 1f);
                Assert.IsFalse(plane.gameObject.activeSelf, "gone until the next one");
                backdrop.Show(SkyPlane.FirstAt + SkyPlane.EverySeconds + SkyPlane.CrossSeconds * 0.25f);
                Assert.IsTrue(plane.gameObject.activeSelf);
                Assert.Less(Vector3.Angle(plane.forward, -camera.transform.right), 0.01f, "the next comes back the other way");
                Assert.AreEqual(SkyPlane.Bank, Vector3.Angle(plane.up, Vector3.up), 0.05f);
                Assert.Greater(Vector3.Dot(plane.up, camera.transform.forward), 0f, "banked away from the camera this way too");
                Assert.Greater(plane.localPosition.x, 0f, "from the right");
                backdrop.HoldPlane(0, 0.15f);
                Assert.IsTrue(plane.gameObject.activeSelf, "held for a picture whatever the time");
                Assert.AreEqual((SkyPlane.SpotAt(0, 0.15f).x - 0.5f) * 10f * camera.aspect, plane.localPosition.x, 1e-3f);
                backdrop.HoldPlane(-1, 0f);
                backdrop.Show(0f);
                Assert.IsFalse(plane.gameObject.activeSelf, "and let go again");

                // The clouds drift, the near layer faster.
                backdrop.Show(0f);
                Vector2 nearAt = near.mainTextureOffset, farAt = far.mainTextureOffset;
                backdrop.Show(30f);
                Assert.AreEqual(30f / SkyBackdrop.NearDriftSeconds, Mathf.Repeat(near.mainTextureOffset.x - nearAt.x, 1f), 1e-4f);
                Assert.AreEqual(30f / SkyBackdrop.FarDriftSeconds, Mathf.Repeat(far.mainTextureOffset.x - farAt.x, 1f), 1e-4f);
                Assert.AreEqual(0f, near.mainTextureOffset.y, 1e-6f, "only sideways");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TheSkyRisesFromDeepBlueToAPaleHazeWithSoftCloudsClearOfTheBoard()
        {
            // The owner's wishes (2026-11-11): a backdrop that suits the atmosphere; calm, plain and natural, the eye on the board.
            Color bottom = SkySkin.ColourAt(0.02f, 0.02f), side = SkySkin.ColourAt(0.02f, 0.5f), top = SkySkin.ColourAt(0.02f, 0.98f);
            Assert.Less(bottom.grayscale, side.grayscale, "deeper at the bottom");
            Assert.Less(side.grayscale, top.grayscale, "paler towards the top");
            foreach (Color colour in new[] { bottom, side, top }) Assert.Greater(colour.b, colour.r, "a sky blue all the way up");
            Assert.Greater(top.r - top.b, side.r - side.b, "the haze at the top is warmer");

            // The clouds keep to the bottom and the top of the screen: none across the middle, where the board is.
            float low = 0f, middle = 0f, high = 0f, softest = 1f;
            for (int i = 0; i < 64; i++)
            {
                float u = (i + 0.5f) / 64f;
                low = Mathf.Max(low, SkySkin.CloudCover(u, 0.03f));
                high = Mathf.Max(high, SkySkin.CloudCover(u, 0.93f));
                for (float v = SkySkin.CloudsBelow; v <= SkySkin.CloudsAbove; v += 0.02f) middle = Mathf.Max(middle, SkySkin.CloudCover(u, v));
                // Soft: nowhere does a cloud's edge go from clear to solid within a hundredth of the screen.
                for (float v = 0.02f; v < SkySkin.FarReach; v += 0.02f)
                    softest = Mathf.Min(softest, 1f - Mathf.Abs(SkySkin.CloudCover(u, v) - SkySkin.CloudCover(u + 0.005f, v)));
            }

            Assert.Greater(low, 0.9f, "a bank of cloud along the bottom");
            Assert.That(high, Is.InRange(0.05f, SkySkin.WispSolid + 1e-3f), "only thin wisps along the top");
            Assert.AreEqual(0f, middle, 1e-4f, "clear sky behind the board");
            Assert.Greater(softest, 0.4f, "soft-edged clouds");
            Assert.Less(SkySkin.NearReach, SkySkin.FarReach, "the far bank shows above the near one");
            Assert.LessOrEqual(SkySkin.FarReach, SkySkin.CloudsBelow);
            for (float v = 0.01f; v < 1f; v += 0.03f)
                Assert.AreEqual(SkySkin.CloudCover(0.0002f, v), SkySkin.CloudCover(0.9998f, v), 0.02f, "the layers repeat sideways without a seam");
            Assert.Greater(SkySkin.CloudShade.grayscale, 0.8f, "the clouds are light even in their shade, never grey");
            Assert.Less(SkySkin.CloudLight.grayscale - SkySkin.CloudShade.grayscale, 0.2f, "and their light and shade are close: no hard modelling");
            Assert.Less(SkySkin.FarSolid, SkySkin.NearSolid, "the far layer fainter");

            // The pictures: the sky fills its picture, the cloud layers are see-through between their clouds and repeat sideways.
            Texture2D skyPicture = SkySkin.Sky(), nearPicture = SkySkin.Clouds(true), farPicture = SkySkin.Clouds(false);
            Assert.AreEqual(SkySkin.Width, skyPicture.width);
            Assert.AreEqual(SkySkin.Height, skyPicture.height);
            Assert.AreNotSame(nearPicture, farPicture);
            Assert.AreEqual(TextureWrapMode.Repeat, nearPicture.wrapModeU);
            Assert.AreEqual(TextureWrapMode.Repeat, farPicture.wrapModeU);
            Assert.AreEqual(1f, skyPicture.GetPixel(10, 10).a, 1e-3f);
            Assert.AreEqual(0f, nearPicture.GetPixel(SkySkin.CloudWidth / 2, SkySkin.CloudHeight / 2).a, 1e-3f, "clear in the middle");
            Assert.AreEqual(0f, farPicture.GetPixel(SkySkin.CloudWidth / 2, SkySkin.CloudHeight / 2).a, 1e-3f);

            // The far islands: few, at the sides, gone pale in the haze yet still solid things.
            Assert.LessOrEqual(SkySkin.FarIslands.Count, 2, "plain: no more than a couple");
            Assert.That(SkySkin.FarIslandHaze, Is.InRange(0.25f, 0.5f), "gone into the haze");
            for (int i = 0; i < SkySkin.FarIslands.Count; i++) Assert.Greater(Mathf.Abs(SkySkin.FarIslands[i].U - 0.5f), 0.35f);

            // Covering a view without stretching.
            Vector4 wide = SkyBackdrop.Cover(21f / 9f, 16f / 9f), tall = SkyBackdrop.Cover(4f / 3f, 16f / 9f), same = SkyBackdrop.Cover(16f / 9f, 16f / 9f);
            Assert.AreEqual(1f, wide.x, 1e-4f);
            Assert.AreEqual((16f / 9f) / (21f / 9f), wide.y, 1e-4f, "a wide window sees less of the picture's height");
            Assert.AreEqual((1f - wide.y) / 2f, wide.w, 1e-4f, "cut evenly");
            Assert.AreEqual(0.75f, tall.x, 1e-4f);
            Assert.AreEqual(1f, tall.y, 1e-4f);
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), same);

            // Movement that goes unnoticed while playing: slow drifts that come round, the near clouds faster, islands rising by little.
            Assert.AreEqual(0f, SkyBackdrop.DriftAt(0f, SkyBackdrop.NearDriftSeconds), 1e-5f);
            Assert.AreEqual(0.25f, SkyBackdrop.DriftAt(SkyBackdrop.NearDriftSeconds * 1.25f, SkyBackdrop.NearDriftSeconds), 1e-4f, "round and round");
            Assert.GreaterOrEqual(SkyBackdrop.NearDriftSeconds, 240f, "slow");
            Assert.Greater(SkyBackdrop.FarDriftSeconds, SkyBackdrop.NearDriftSeconds * 1.5f, "the far layer slower still");
            Assert.LessOrEqual(SkyBackdrop.IslandRise, 0.006f);
            Assert.GreaterOrEqual(SkyBackdrop.IslandSeconds, 8f);
            Assert.AreNotEqual(SkyBackdrop.IslandSecondsOf(0), SkyBackdrop.IslandSecondsOf(1));
        }

        [Test]
        public void LinearLightsGiveTheBoardTopItsGammaColour()
        {
            foreach (DayLight light in new[] { DayCycle.Noon })
            {
                LightRig gamma = DayCycle.GammaRig(light);
                LightRig linear = DayCycle.LinearRig(light);
                Color gammaTop = TopLight(light.SunPitch, gamma.SunColor, gamma.SunIntensity, gamma.FillColor, gamma.FillIntensity, gamma.Ambient);
                Color linearTop = TopLight(light.SunPitch, linear.SunColor.linear, linear.SunIntensity, linear.FillColor.linear,
                    linear.FillIntensity, linear.Ambient.linear);
                Assert.AreEqual(Mathf.Pow(gammaTop.r, DayCycle.Gamma) * DayCycle.LinearGain, linearTop.r, 0.01f);
                Assert.AreEqual(Mathf.Pow(gammaTop.g, DayCycle.Gamma) * DayCycle.LinearGain, linearTop.g, 0.01f);
                Assert.AreEqual(Mathf.Pow(gammaTop.b, DayCycle.Gamma) * DayCycle.LinearGain, linearTop.b, 0.01f);
                Assert.Greater(linear.ShadowStrength, gamma.ShadowStrength, "linear needs deeper shadows to look as dark");
                Assert.LessOrEqual(linear.ShadowStrength, 1f);
            }
        }

        private static Color TopLight(float sunPitch, Color sun, float sunIntensity, Color fill, float fillIntensity, Color ambient) =>
            ambient + sun * (sunIntensity * Mathf.Sin(sunPitch * Mathf.Deg2Rad)) + fill * (fillIntensity * Mathf.Sin(DayCycle.FillPitch * Mathf.Deg2Rad));

        [Test]
        public void ThePaintedShaderTellsTemporalSmoothingHowFiguresMove()
        {
            string path = System.IO.Path.Combine(Application.dataPath, "_Lazpoly", "Data", "Resources", "Lazpoly", "Shaders", "PaintedFigure.shader");
            StringAssert.Contains("\"LightMode\" = \"MotionVectors\"", System.IO.File.ReadAllText(path), "moving figures would ghost under TAA");
        }

        [Test]
        public void ModelMaterialIsInResourcesWithASupportedShader()
        {
            var material = Resources.Load<Material>(ModelParts.MaterialResource);
            Assert.IsNotNull(material, "Model material missing; run Lazpoly/Set Up Render Pipeline.");
            Assert.IsTrue(material.shader.isSupported, $"Shader '{material.shader.name}' is not supported.");
            StringAssert.StartsWith("Universal Render Pipeline/", material.shader.name);
        }

        [Test]
        public void ModelPartsUseTheModelMaterial()
        {
            var parent = new GameObject("Parent");
            try
            {
                GameObject part = ModelParts.Create("Part", parent.transform, MeshFactory.RoundedBox(Vector3.one, 0.1f), Color.red);
                Material material = part.GetComponent<Renderer>().sharedMaterial;
                Assert.AreEqual(Resources.Load<Material>(ModelParts.MaterialResource).shader, material.shader);
                Assert.AreEqual(Color.red, material.color);
                Assert.IsNull(part.GetComponent<Collider>());
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [TestCase(1920f, 1080f, 412f, BoardCameraRig.BaseYaw)]
        [TestCase(1280f, 1024f, 330f, BoardCameraRig.BaseYaw)]
        [TestCase(3440f, 1440f, 549f, BoardCameraRig.BaseYaw)]
        [TestCase(1920f, 1080f, 0f, BoardCameraRig.BaseYaw)]
        [TestCase(1920f, 1080f, 412f, BoardCameraRig.BaseYaw + 180f)]
        public void CameraFitsTheWholeBoardBetweenTheSidePanels(float width, float height, float reserved, float yaw)
        {
            const float half = 8.1f, thickness = 0.6f, pitch = BoardCameraRig.Pitch;
            var center = new Vector3(0f, 0f, 0f);
            float size = BoardCameraFraming.OrthographicSize(half, thickness, pitch, width, height, reserved);
            Vector3 aim = BoardCameraFraming.Aim(center, thickness);
            float freeAspect = BoardCameraFraming.FreeAspect(width, height, reserved);

            float maxX = 0f, minY = float.MaxValue, maxY = float.MinValue;
            for (int corner = 0; corner < 4; corner++)
            for (int level = 0; level < 3; level++)
            {
                var point = center + new Vector3(corner % 2 == 0 ? -half : half, level == 0 ? 0f : level == 1 ? -thickness : BoardCameraFraming.Headroom, corner < 2 ? -half : half);
                Vector2 seen = BoardCameraFraming.Project(point, aim, pitch, yaw, size, freeAspect);
                maxX = Mathf.Max(maxX, Mathf.Abs(seen.x));
                minY = Mathf.Min(minY, seen.y);
                maxY = Mathf.Max(maxY, seen.y);
            }

            Assert.LessOrEqual(maxX, 1f, "A corner is under a side panel.");
            Assert.GreaterOrEqual(minY, -1f, "The plate's bottom edge is below the screen.");
            Assert.LessOrEqual(maxY, 1f, "What stands on the far corner is above the screen.");
            Assert.Greater(Mathf.Max(maxX, (maxY - minY) / 2f), BoardCameraFraming.Fill - 0.02f, "The board could be bigger.");
            Assert.AreEqual(0f, (minY + maxY) / 2f, 0.02f, "The board is not centred vertically.");
            Assert.AreEqual(size, BoardCameraFraming.OrthographicSize(half, thickness, pitch, width, height, reserved), "the framing is deterministic");
        }

        [TestCase(1920f, 1080f, 412f, 260f)]
        [TestCase(1280f, 1024f, 330f, 320f)]
        [TestCase(3440f, 1440f, 549f, 200f)]
        public void CameraLiftsTheBoardClearOfTheBottomPanel(float width, float height, float reserved, float bottom)
        {
            const float half = 8.1f, thickness = 0.6f, pitch = BoardCameraRig.Pitch, yaw = BoardCameraRig.BaseYaw;
            var center = new Vector3(0f, 0f, 0f);
            float plain = BoardCameraFraming.OrthographicSize(half, thickness, pitch, width, height, reserved);
            float size = BoardCameraFraming.OrthographicSize(half, thickness, pitch, width, height, reserved, bottom);
            // A board held by the screen's width keeps its size and only moves up; one held by the height zooms out.
            Assert.GreaterOrEqual(size, plain, "the view never zooms in for the panel");
            Vector3 aim = BoardCameraFraming.AimAbovePanel(BoardCameraFraming.Aim(center, thickness), pitch, yaw, size, height, bottom);
            Assert.AreEqual(BoardCameraFraming.Aim(center, thickness), BoardCameraFraming.AimAbovePanel(BoardCameraFraming.Aim(center, thickness), pitch, yaw, size, height, 0f), "no panel, no lift");
            // Across, the free width is the screen's width less the side panels, as a multiple of the view's half height.
            float across = width / height * BoardCameraFraming.FreeFraction(width, reserved);
            float panelTop = -1f + 2f * bottom / height;

            float maxX = 0f, minY = float.MaxValue, maxY = float.MinValue;
            for (int corner = 0; corner < 4; corner++)
            for (int level = 0; level < 3; level++)
            {
                var point = center + new Vector3(corner % 2 == 0 ? -half : half, level == 0 ? 0f : level == 1 ? -thickness : BoardCameraFraming.Headroom, corner < 2 ? -half : half);
                Vector2 seen = BoardCameraFraming.Project(point, aim, pitch, yaw, size, across);
                maxX = Mathf.Max(maxX, Mathf.Abs(seen.x));
                minY = Mathf.Min(minY, seen.y);
                maxY = Mathf.Max(maxY, seen.y);
            }

            Assert.LessOrEqual(maxX, 1f, "A corner is under a side panel.");
            Assert.GreaterOrEqual(minY, panelTop, "The near corner is under the bottom panel.");
            Assert.LessOrEqual(maxY, 1f, "What stands on the far corner is above the screen.");
            Assert.AreEqual((panelTop + 1f) / 2f, (minY + maxY) / 2f, 0.02f, "The board is not centred in the free area above the panel.");
            float freeHalfHeight = (1f - panelTop) / 2f;
            Assert.Greater(Mathf.Max(maxX, (maxY - minY) / 2f / freeHalfHeight), BoardCameraFraming.Fill - 0.02f, "The board could be bigger.");
        }

        [Test]
        public void EverythingInTheSkyKeepsItsOwnDepthFarBehindTheTable()
        {
            // The game's view: flat, 100 deep, at its widest zoom about 22 high. The table and all on it lie within 50 of the camera.
            var holder = new GameObject("Backdrop Camera");
            try
            {
                Camera camera = holder.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 11f;
                camera.farClipPlane = 100f;
                camera.transform.rotation = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
                SkyBackdrop backdrop = SkyBackdrop.Attach(camera);
                Assert.IsNotNull(backdrop, "the picture shader is missing from Resources");
                float Deep(Vector3 point) => camera.transform.InverseTransformPoint(point).z;
                float sky = Deep(backdrop.transform.position), clouds = Deep(backdrop.FarClouds.position);
                Assert.Less(sky, camera.farClipPlane, "the sky inside the far end");
                Assert.Greater(Deep(backdrop.NearClouds.position), 80f, "the clouds far behind the table");
                foreach (float seconds in new[] { 0f, 37f, SkyPlane.FirstAt + 3f, SkyPlane.FirstAt + SkyPlane.EverySeconds + 20f })
                {
                    backdrop.Show(seconds);
                    float nearest = float.MaxValue;
                    foreach (Transform island in backdrop.FarIslands)
                    {
                        Assert.Less(Vector3.Angle(island.up, Vector3.up), 0.01f, "upright in the world though the camera looks down: its meadow shows");
                        foreach (Renderer part in island.GetComponentsInChildren<Renderer>())
                        {
                            Bounds bounds = part.bounds;
                            float reach = bounds.extents.magnitude, middle = Deep(bounds.center);
                            Assert.Less(middle + reach, sky, $"{island.name} {part.name}: wholly before the sky");
                            Assert.Greater(middle - reach, clouds, $"{island.name} {part.name}: wholly behind the clouds");
                            nearest = Mathf.Min(nearest, middle - reach);
                        }
                    }

                    if (!backdrop.Plane.gameObject.activeSelf) continue;
                    foreach (Renderer part in backdrop.Plane.GetComponentsInChildren<Renderer>())
                    {
                        Bounds bounds = part.bounds;
                        float reach = bounds.extents.magnitude, middle = Deep(bounds.center);
                        Assert.Less(middle + reach, nearest, $"plane {part.name}: before the far islands");
                        Assert.Greater(middle - reach, clouds, $"plane {part.name}: behind the clouds");
                    }

                    Assert.Less(Mathf.Abs(backdrop.Plane.forward.y), 1e-4f, "the plane flies level in the world");
                    Assert.AreEqual(SkyPlane.Bank, Vector3.Angle(backdrop.Plane.up, Vector3.up), 0.05f, "banked a little");
                    Vector3 level = camera.transform.forward;
                    level.y = 0f;
                    Assert.Greater(Vector3.Dot(backdrop.Plane.up, level), 0f, "away from the camera");
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AnAeroplaneCrossesTheSkyOnlyNowAndThenSlowAndSmall()
        {
            // The owner's wish (2026-11-12): a plane passing in the background now and then. Calm, since the eye belongs on the board.
            Assert.AreEqual(-1, SkyPlane.PassAt(0f), "none at first");
            Assert.AreEqual(-1f, SkyPlane.ProgressAt(SkyPlane.FirstAt - 0.5f));
            Assert.AreEqual(0, SkyPlane.PassAt(SkyPlane.FirstAt));
            Assert.AreEqual(0f, SkyPlane.ProgressAt(SkyPlane.FirstAt), 1e-4f, "the first sets out");
            Assert.AreEqual(0.5f, SkyPlane.ProgressAt(SkyPlane.FirstAt + SkyPlane.CrossSeconds / 2f), 1e-4f);
            Assert.AreEqual(-1f, SkyPlane.ProgressAt(SkyPlane.FirstAt + SkyPlane.CrossSeconds + 0.1f), "then the sky is empty");
            Assert.AreEqual(1, SkyPlane.PassAt(SkyPlane.FirstAt + SkyPlane.EverySeconds + 1f));
            Assert.AreEqual(0.25f, SkyPlane.ProgressAt(SkyPlane.FirstAt + 7f * SkyPlane.EverySeconds + SkyPlane.CrossSeconds / 4f), 1e-3f, "and so on, game long");
            Assert.GreaterOrEqual(SkyPlane.EverySeconds, 90f, "now and then: not more than one in a minute and a half");
            Assert.LessOrEqual(SkyPlane.CrossSeconds / SkyPlane.EverySeconds, 0.4f, "the sky is empty most of the time");
            Assert.Less((1f + 2f * SkyPlane.Margin) / SkyPlane.CrossSeconds, 0.04f, "slow: under a twenty-fifth of the view a second");
            Assert.Less(SkyPlane.Size, 0.06f, "small");
            Assert.That(SkyPlane.Bank, Is.InRange(10f, 25f), "banked enough to show its side, not so much that it seems to turn");

            float islandsTop = 0f;
            foreach (SkySkin.FarIsland island in SkySkin.FarIslands) islandsTop = Mathf.Max(islandsTop, island.V + island.Size / 2f);
            for (int pass = 0; pass < 6; pass++)
            {
                Assert.AreNotEqual(SkyPlane.Rightward(pass), SkyPlane.Rightward(pass + 1), "there and back by turns");
                Vector2 from = SkyPlane.SpotAt(pass, 0f), to = SkyPlane.SpotAt(pass, 1f), half = SkyPlane.SpotAt(pass, 0.5f);
                Assert.AreEqual(from.y, to.y, 1e-6f, "straight and level across the view");
                Assert.AreEqual(0.5f, half.x, 1e-4f);
                Assert.AreEqual(SkyPlane.Rightward(pass) ? -SkyPlane.Margin : 1f + SkyPlane.Margin, from.x, 1e-4f, "it starts just outside one side");
                Assert.AreEqual(SkyPlane.Rightward(pass) ? 1f + SkyPlane.Margin : -SkyPlane.Margin, to.x, 1e-4f, "and ends just outside the other");
                Assert.Greater(from.y - SkyPlane.Size / 2f, islandsTop, "over the far islands");
                Assert.Less(from.y, 0.9f, "under the top of the screen");
            }

            // Its whole length is outside even a narrow (4:3) view when it starts: it never pops in.
            Assert.Greater(SkyPlane.Margin, SkyPlane.Size / 2f / (4f / 3f));
        }
    }
}
