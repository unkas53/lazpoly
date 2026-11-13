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
                    Assert.IsNotNull(island.Find(IslandParts.FarModelName).GetComponentInChildren<MeshRenderer>(), "a model, not a picture");
                    if (!backdrop.Modelled) Assert.IsNotNull(island.GetComponentInChildren<IslandDrift>(), "its loose stones drift");
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
                    if (backdrop.Modelled)
                    {
                        // The owner's model has a front: it keeps it towards the camera and only sways.
                        float swung = Mathf.Abs(SkyBackdrop.IslandSwayAt(20f, i) - SkyBackdrop.IslandSwayAt(0f, i));
                        Assert.AreEqual(swung, turn, 0.05f, "it sways about its upright, so its depth shows");
                        backdrop.Show(SkyBackdrop.IslandSwaySecondsOf(i) * (0.25f - 0.23f * i));
                        Vector3 front = island.forward, toCamera = -camera.transform.forward;
                        toCamera.y = 0f;
                        float off = Vector3.SignedAngle(toCamera, front, Vector3.up);
                        Assert.AreEqual(SkySkin.FarIslands[i].Turn + SkyBackdrop.IslandSway, off, 0.05f, "its front towards the camera, turned by its own share and its sway");
                    }
                    else
                    {
                        Assert.AreEqual(360f * 20f / (SkyBackdrop.IslandTurnSeconds * (1f + 0.31f * i)), turn, 0.05f, "it turns about its upright, so its depth shows");
                    }

                    Assert.Less(turn / 20f, 2f, "slowly: under two degrees a second");
                    Assert.Less(Vector3.Angle(island.up, Vector3.up), 0.01f, "and stays upright");
                }

                Assert.Less(Mathf.DeltaAngle(0f, SkyBackdrop.IslandTurnAt(10f, 0)) * Mathf.DeltaAngle(137f, SkyBackdrop.IslandTurnAt(10f, 1)), 0f, "the two turn opposite ways");
                // The model's sway: small, slow, each island in its own time, and never far enough to show its back.
                Assert.That(SkyBackdrop.IslandSway, Is.InRange(4f, 15f));
                Assert.Less(SkyBackdrop.IslandSway * 2f * Mathf.PI / SkyBackdrop.IslandSwaySeconds, 2f, "under two degrees a second at its quickest");
                Assert.AreEqual(SkyBackdrop.IslandSway, SkyBackdrop.IslandSwayAt(SkyBackdrop.IslandSwaySecondsOf(0) / 4f, 0), 1e-3f);
                Assert.AreNotEqual(SkyBackdrop.IslandSwayAt(7f, 0), SkyBackdrop.IslandSwayAt(7f, 1));
                Assert.AreEqual(Mathf.Repeat(-45f + 180f + SkySkin.FarIslands[0].Turn, 360f), SkyBackdrop.IslandFacingAt(0f, 0, -45f), 1e-3f);
                foreach (SkySkin.FarIsland spot in SkySkin.FarIslands) Assert.Less(Mathf.Abs(spot.Turn) + SkyBackdrop.IslandSway, 90f, "its front stays in sight");
                Assert.AreNotEqual(SkySkin.FarIslands[0].Turn, SkySkin.FarIslands[1].Turn, "the one model shows a different side at each place");

                // The aeroplane (the owner's wishes, 2026-11-12 and 2026-11-13): out of sight until its time, then level in
                // the world along one of the board's own directions, behind the far islands and before the sky, its
                // trail a picture behind it; the next one flies up the other side of the sky.
                Transform plane = backdrop.Plane, trail = backdrop.Trail;
                Assert.IsNotNull(plane);
                Assert.IsNotNull(trail, "a trail behind it");
                Material streak = trail.GetComponent<Renderer>().sharedMaterial;
                Assert.AreSame(SkySkin.Trail(), streak.mainTexture);
                Assert.Greater(streak.renderQueue, sky.renderQueue, "the trail is drawn over the sky");
                Assert.Less(streak.renderQueue, far.renderQueue, "and under the clouds");
                backdrop.Show(0f);
                Assert.IsFalse(plane.gameObject.activeSelf, "no plane at first");
                Assert.IsFalse(trail.gameObject.activeSelf, "and no trail");
                backdrop.Show(SkyPlane.FirstAt + SkyPlane.CrossSeconds * 0.3f);
                Assert.IsTrue(plane.gameObject.activeSelf, "one on its way");
                Assert.IsTrue(trail.gameObject.activeSelf, "its trail with it");
                Vector2 place = SkyPlane.SpotAt(0, 0.3f, camera.aspect);
                Assert.AreEqual((place.x - 0.5f) * 10f * camera.aspect, plane.localPosition.x, 1e-3f, "where its pass has it across the view");
                Assert.AreEqual((place.y - 0.5f) * 10f, plane.localPosition.y, 1e-3f, "and up it");
                Assert.Less(plane.localPosition.x, 0f, "the first on the left side of the sky");
                Assert.Greater(plane.localPosition.y, 0f, "in its upper half");
                Assert.Greater(plane.position.z, backdrop.FarIslands[0].position.z, "behind the far islands, so it passes behind the one at its side");
                Assert.Greater(backdrop.FarIslands[0].position.z, backdrop.FarClouds.position.z, "which are behind the clouds");
                Assert.Greater(trail.position.z, plane.position.z, "the trail behind the plane, so the plane hides its head");
                Assert.Less(trail.position.z, backdrop.transform.position.z, "and before the sky");
                Assert.AreEqual(SkyPlane.Size * 10f / PlaneParts.Length, plane.lossyScale.x, 1e-4f, "its length is a share of the view's height");
                Assert.Less(Vector3.Angle(plane.up, Vector3.up), 0.01f, "level in the world");
                Assert.AreEqual(SkyPlane.TurnOf(0), Mathf.DeltaAngle(camera.transform.eulerAngles.y, plane.eulerAngles.y), 0.01f, "a half right angle off the camera's heading: along the board's edge");
                Assert.AreEqual(SkyPlane.TrailLength * SkyPlane.Size * 10f, trail.localScale.x, 1e-3f, "the trail's length in plane lengths");
                Assert.AreEqual(SkyPlane.TrailWidth * SkyPlane.Size * 10f, trail.localScale.y, 1e-3f, "unstretched whatever the window's shape");
                backdrop.Show(SkyPlane.FirstAt + SkyPlane.CrossSeconds + 1f);
                Assert.IsFalse(plane.gameObject.activeSelf, "gone until the next one");
                Assert.IsFalse(trail.gameObject.activeSelf, "its trail gone with it");
                backdrop.Show(SkyPlane.FirstAt + SkyPlane.EverySeconds + SkyPlane.CrossSeconds * 0.25f);
                Assert.IsTrue(plane.gameObject.activeSelf);
                Assert.AreEqual(SkyPlane.TurnOf(1), Mathf.DeltaAngle(camera.transform.eulerAngles.y, plane.eulerAngles.y), 0.01f, "the next along the board's other edge");
                Assert.Greater(plane.localPosition.x, 0f, "on the right side");
                backdrop.HoldPlane(0, 0.15f);
                Assert.IsTrue(plane.gameObject.activeSelf, "held for a picture whatever the time");
                Assert.AreEqual((SkyPlane.SpotAt(0, 0.15f, camera.aspect).x - 0.5f) * 10f * camera.aspect, plane.localPosition.x, 1e-3f);
                backdrop.HoldPlane(-1, 0f);
                backdrop.Show(0f);
                Assert.IsFalse(plane.gameObject.activeSelf, "and let go again");

                // The trail warms with the evening like the rest of the sky.
                backdrop.SetEvening(1f);
                Assert.AreEqual(SkyBackdrop.DuskTint.g, streak.color.g, 1e-4f);
                backdrop.SetEvening(0f);
                Assert.AreEqual(1f, streak.color.g, 1e-4f);

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
                foreach (float seconds in new[] { 0f, 37f, SkyPlane.FirstAt + 3f, SkyPlane.FirstAt + SkyPlane.EverySeconds + 20f, SkyPlane.FirstAt + 2f * SkyPlane.EverySeconds + 9f, SkyPlane.FirstAt + 3f * SkyPlane.EverySeconds + 14f })
                {
                    backdrop.Show(seconds);
                    float nearest = float.MaxValue, farthest = float.MinValue;
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
                            farthest = Mathf.Max(farthest, middle + reach);
                        }
                    }

                    if (!backdrop.Plane.gameObject.activeSelf) continue;
                    float planeFar = float.MinValue;
                    foreach (Renderer part in backdrop.Plane.GetComponentsInChildren<Renderer>())
                    {
                        Bounds bounds = part.bounds;
                        float reach = bounds.extents.magnitude, middle = Deep(bounds.center);
                        Assert.Greater(middle - reach, farthest, $"plane {part.name}: behind the far islands");
                        Assert.Less(middle + reach, sky, $"plane {part.name}: before the sky");
                        foreach (Vector3 corner in part.GetComponent<MeshFilter>().sharedMesh.vertices)
                            planeFar = Mathf.Max(planeFar, Deep(part.transform.TransformPoint(corner)));
                    }

                    Transform plane = backdrop.Plane, trail = backdrop.Trail;
                    Assert.Less(Mathf.Abs(plane.forward.y), 1e-4f, "the plane flies level in the world");
                    Assert.Less(Vector3.Angle(plane.up, Vector3.up), 0.01f, "wings level too");

                    // Seen by the slanted camera it moves along a slope, nose first, and the trail lies along that slope
                    // behind it: a flat picture facing the camera, behind every part of the plane and before the sky.
                    int pass = SkyPlane.PassAt(seconds);
                    Vector2 way = SkyPlane.WayOf(pass);
                    Vector3 nose = camera.transform.InverseTransformDirection(plane.forward);
                    Assert.Less(Vector2.Angle(way, new Vector2(nose.x, nose.y)), 0.05f, "nose first along its path");
                    Assert.IsTrue(trail.gameObject.activeSelf, "the trail with it");
                    Assert.Less(Vector3.Angle(trail.forward, camera.transform.forward), 0.01f, "the trail's picture faces the camera");
                    Vector3 along = camera.transform.InverseTransformDirection(trail.right);
                    Assert.Less(Vector2.Angle(way, new Vector2(along.x, along.y)), 0.05f, "and lies along the plane's path");
                    Assert.Greater(Deep(trail.position), planeFar, "behind every part of the plane");
                    Assert.Less(Deep(trail.position), sky, "before the sky");
                    Assert.Greater(nearest, clouds, "the far islands behind the clouds");
                    Vector3 head = camera.transform.InverseTransformPoint(trail.TransformPoint(new Vector3(0.5f, 0f, 0f)));
                    Vector3 jets = camera.transform.InverseTransformPoint(plane.TransformPoint(PlaneParts.Exhaust));
                    Assert.AreEqual(jets.x, head.x, 1e-3f, "its head where the engines' jets leave the plane");
                    Assert.AreEqual(jets.y, head.y, 1e-3f);
                    Vector3 end = camera.transform.InverseTransformPoint(trail.TransformPoint(new Vector3(-0.5f, 0f, 0f)));
                    Assert.Less(Vector2.Dot(new Vector2(end.x - head.x, end.y - head.y), way), 0f, "and the rest of it behind");
                }
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void AnAeroplaneCrossesTheSkyBesideTheBoardOnlyNowAndThenSlowAndSmall()
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
            Assert.LessOrEqual(SkyPlane.Size, 0.065f, "small");
            Assert.Greater(SkyPlane.Margin, SkyPlane.Size / 2f, "its whole length is outside the view when it starts: it never pops in");

            // The owner's wish (2026-11-13): it flies at the game's isometric angle. Level flight a half right angle off the
            // camera's heading is flight along the board's edges, and shows on the screen as the slope those edges have.
            float slope = Mathf.Atan(Mathf.Sin(BoardCameraRig.Pitch * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
            Quaternion look = Quaternion.Euler(BoardCameraRig.Pitch, BoardCameraRig.BaseYaw, 0f);
            bool left = false, right = false, climbing = false, coming = false;
            for (int pass = 0; pass < 2 * SkyPlane.Passes; pass++)
            {
                float turn = SkyPlane.TurnOf(pass);
                Assert.AreEqual(45f, Mathf.Abs(Mathf.DeltaAngle(0f, turn)) % 90f, 1e-3f, "a half right angle off the camera's heading");
                Assert.AreEqual(turn, SkyPlane.TurnOf(pass + SkyPlane.Passes), "the passes come round again");
                Vector3 heading = Quaternion.Euler(0f, BoardCameraRig.BaseYaw + turn, 0f) * Vector3.forward;
                Assert.Less(Mathf.Min(Mathf.Abs(heading.x), Mathf.Abs(heading.z)), 1e-4f, "which is along one of the board's edges");
                Vector3 seen = Quaternion.Inverse(look) * heading;
                Vector2 way = SkyPlane.WayOf(pass);
                Assert.AreEqual(1f, way.magnitude, 1e-4f);
                Assert.Less(Vector2.Angle(way, new Vector2(seen.x, seen.y)), 0.01f, "its path on the screen is what the camera sees of that flight");
                Assert.AreEqual(slope, Mathf.Abs(Mathf.Atan2(way.y, Mathf.Abs(way.x)) * Mathf.Rad2Deg), 0.01f, "a slope of about thirty degrees");
                Assert.AreEqual(SkyPlane.Climbing(pass), way.y > 0f, "up the screen when it flies away from the camera");
                Assert.AreEqual(SkyPlane.Climbing(pass), seen.z > 0f);
                Assert.AreNotEqual(SkyPlane.OnTheLeft(pass), SkyPlane.OnTheLeft(pass + 1), "the left corner and the right by turns");
                left |= SkyPlane.OnTheLeft(pass);
                right |= !SkyPlane.OnTheLeft(pass);
                climbing |= SkyPlane.Climbing(pass);
                coming |= !SkyPlane.Climbing(pass);

                foreach (float aspect in new[] { 4f / 3f, 16f / 9f, 21f / 9f })
                {
                    SkyPlane.Ends(pass, aspect, out Vector2 from, out Vector2 to);
                    Vector2 side = SkyPlane.Climbing(pass) ? from : to, top = SkyPlane.Climbing(pass) ? to : from;
                    Assert.AreEqual((SkyPlane.OnTheLeft(pass) ? -0.5f : 0.5f) * aspect, side.x, 1e-4f, "one end of its path on the view's side");
                    Assert.AreEqual(SkyPlane.EdgeHeightOf(pass) - 0.5f, side.y, 1e-4f, "at its pass's height");
                    Assert.AreEqual(0.5f, top.y, 1e-4f, "the other on the view's top");
                    Assert.Less(Mathf.Abs(top.x), Mathf.Abs(side.x), "inside the view's width");
                    Assert.Less(Vector2.Angle(way, to - from), 0.01f, "straight along its heading");

                    // It starts just outside the view and ends so far outside that its trail has gone too.
                    Vector2 start = SkyPlane.SpotAt(pass, 0f, aspect), finish = SkyPlane.SpotAt(pass, 1f, aspect), half = SkyPlane.SpotAt(pass, 0.5f, aspect);
                    Vector2 first = new Vector2((start.x - 0.5f) * aspect, start.y - 0.5f), last = new Vector2((finish.x - 0.5f) * aspect, finish.y - 0.5f);
                    Assert.AreEqual(SkyPlane.Margin, (from - first).magnitude, 1e-4f);
                    Assert.AreEqual(SkyPlane.Margin + SkyPlane.TrailLength * SkyPlane.Size, (last - to).magnitude, 1e-4f);
                    Assert.IsTrue(start.x < 0f || start.x > 1f || start.y > 1f, "out of sight at first");
                    Vector2 tail = last - way * (SkyPlane.TrailLength * SkyPlane.Size);
                    Assert.IsTrue(tail.x < -aspect / 2f || tail.x > aspect / 2f || tail.y > 0.5f, "trail and all out of sight at the end");
                    Assert.AreEqual((start.x + finish.x) / 2f, half.x, 1e-4f, "at an even pace");
                    Assert.AreEqual((last - first).magnitude, SkyPlane.Distance(pass, aspect), 1e-4f);
                    Assert.Less(SkyPlane.Distance(pass, aspect) / SkyPlane.CrossSeconds, 0.04f, "slow: under a twenty-fifth of the view's height a second");

                    if (aspect < 1.7f) continue;

                    // In the game's wide view its path keeps to its own side of the board's far corner, and passes under
                    // the players' cards in the view's upper corner (two of them, one under the other, when eight play):
                    // the first time it was tried, the plane and its trail were hidden behind those cards.
                    Assert.IsTrue(SkyPlane.OnTheLeft(pass) ? top.x < 0f : top.x > 0f, $"pass {pass} stays on its side in a view {aspect} wide");
                    if (aspect > 1.8f) continue;
                    float tall = Lazpoly.Presentation.UI.UiTheme.ReferenceResolution.y;
                    float card = Lazpoly.Presentation.UI.UiLayout.CornerWidth * Lazpoly.Presentation.Game.PlayerCardView.DesignHeight / Lazpoly.Presentation.Game.PlayerCardView.DesignWidth;
                    float cardsIn = (Lazpoly.Presentation.UI.UiLayout.Margin + Lazpoly.Presentation.UI.UiLayout.CornerWidth) / tall;
                    float cardsDown = (Lazpoly.Presentation.UI.UiLayout.Margin + 2f * card + Lazpoly.Presentation.UI.UiTheme.Spacing) / tall;
                    float across = side.x + (SkyPlane.OnTheLeft(pass) ? cardsIn : -cardsIn);
                    float there = side.y + (across - side.x) * (top.y - side.y) / (top.x - side.x);
                    Assert.Less(there + 0.5f + SkyPlane.Size / 2f, 1f - cardsDown, $"pass {pass} under the players' cards");
                }

                Assert.That(SkyPlane.EdgeHeightOf(pass), Is.InRange(0.4f, 0.6f), "about half way up the view's side, where the far island is; from there a long way to the view's top");
            }

            Assert.IsTrue(left && right && climbing && coming, "both sides of the sky, flying away and coming back");
        }
    }
}
