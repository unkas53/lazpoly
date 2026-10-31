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

        [Test]
        public void CameraKeepsTheBoardClearOfTheSidePanels()
        {
            const float half = 8f, fov = 50f, pitch = 55f;
            float wide = BoardCameraFraming.Distance(half, fov, pitch, 1920f, 1080f, 364f);
            float narrow = BoardCameraFraming.Distance(half, fov, pitch, 1280f, 1024f, 364f);
            Assert.Greater(narrow, wide, "Less free width must move the camera back.");

            // The near corner (closest to the camera, so widest on screen) fits in the free width.
            double halfHorizontal = System.Math.Tan(fov * System.Math.PI / 360.0) * (1920.0 / 1080.0) * (1.0 - 2.0 * 364.0 / 1920.0);
            double depth = wide - half * System.Math.Cos(pitch * System.Math.PI / 180.0);
            Assert.Less(half / depth, halfHorizontal);
        }
    }
}
