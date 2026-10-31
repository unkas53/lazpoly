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

        [TestCase(1920f, 1080f, 412f)]
        [TestCase(1280f, 1024f, 330f)]
        [TestCase(3440f, 1440f, 549f)]
        public void CameraFitsTheWholeBoardBetweenTheSidePanels(float width, float height, float reserved)
        {
            const float half = 7.8f, fov = 50f, pitch = 55f;
            Vector3 center = new Vector3(0f, 0f, 0f);
            Vector3 camera = BoardCameraFraming.CameraPosition(center, half, fov, pitch, width, height, reserved);

            float tanV = Mathf.Tan(fov * Mathf.Deg2Rad / 2f);
            float tanH = tanV * (width / height) * BoardCameraFraming.FreeFraction(width, reserved);
            BoardCameraFraming.Measure(camera, center, half, pitch * Mathf.Deg2Rad, tanH, tanV, out float maxX, out float minY, out float maxY);

            Assert.LessOrEqual(maxX, 1f, "A corner is under a side panel.");
            Assert.GreaterOrEqual(minY, -1f, "The near edge is below the screen.");
            Assert.LessOrEqual(maxY, 1f, "The far edge is above the screen.");
            Assert.Greater(Mathf.Max(maxX, (maxY - minY) / 2f), BoardCameraFraming.Fill - 0.02f, "The board could be bigger.");
            Assert.AreEqual(0f, (minY + maxY) / 2f, 0.02f, "The board is not centred vertically.");
        }
    }
}
