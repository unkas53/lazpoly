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
    }
}
