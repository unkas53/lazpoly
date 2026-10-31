using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lazpoly.Editor
{
    /// <summary>
    /// Creates the project's scenes and registers the playable ones in Build Settings.
    /// Safe to run again: existing scenes are kept untouched.
    /// </summary>
    public static class SceneSetupMenu
    {
        private const string SceneFolder = "Assets/_Lazpoly/Scenes";

        // Order matters: the first entry is the scene a build starts in.
        private static readonly string[] BuildScenes = { "MainMenu", "Lobby", "Game" };
        private static readonly string[] DevelopmentScenes = { "Sandbox_Board" };

        [MenuItem("Lazpoly/Create Scenes and Build Settings")]
        private static void CreateScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(SceneFolder);

            var buildEntries = new List<EditorBuildSettingsScene>();
            foreach (string name in BuildScenes)
            {
                string path = EnsureScene(name);
                buildEntries.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (string name in DevelopmentScenes)
            {
                EnsureScene(name);
            }

            EditorBuildSettings.scenes = buildEntries.ToArray();
            EditorSceneManager.OpenScene($"{SceneFolder}/{BuildScenes[0]}.unity");
            Debug.Log($"[Lazpoly] Scenes ready in {SceneFolder}; Build Settings: {string.Join(", ", BuildScenes)}.");
        }

        private static string EnsureScene(string name)
        {
            string path = $"{SceneFolder}/{name}.unity";
            if (File.Exists(path)) return path;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return path;
        }
    }
}
