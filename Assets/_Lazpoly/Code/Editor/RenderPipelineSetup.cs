using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Lazpoly.Editor
{
    /// <summary>
    /// Makes the project render with URP and creates the material every code-built model uses.
    /// Without a pipeline asset the game falls back to the Built-in pipeline and the models' shader
    /// is left out of player builds, which draws everything magenta. Safe to run again: existing
    /// assets are reused. Also callable from the command line with
    /// -executeMethod Lazpoly.Editor.RenderPipelineSetup.Run
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/_Lazpoly/Settings";
        private const string PipelinePath = SettingsFolder + "/Lazpoly_URP.asset";
        private const string RendererPath = SettingsFolder + "/Lazpoly_URP_Renderer.asset";
        private const string MaterialFolder = "Assets/_Lazpoly/Data/Resources/Lazpoly/Materials";
        private const string MaterialPath = MaterialFolder + "/Model.mat";
        private const string LitShader = "Universal Render Pipeline/Lit";

        [MenuItem("Lazpoly/Set Up Render Pipeline")]
        public static void Run()
        {
            Directory.CreateDirectory(SettingsFolder);
            Directory.CreateDirectory(MaterialFolder);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                pipeline.shadowDistance = 40f;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
            {
                Shader shader = Shader.Find(LitShader);
                if (shader == null)
                {
                    Debug.LogError($"[Lazpoly] Shader '{LitShader}' not found; is the URP package installed?");
                    return;
                }
                AssetDatabase.CreateAsset(new Material(shader) { name = "Model" }, MaterialPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Lazpoly] URP is the active render pipeline and the model material exists.");
        }
    }
}
