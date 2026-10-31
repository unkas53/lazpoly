using Lazpoly.Presentation.Game;
using Lazpoly.Presentation.Menu;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lazpoly.Editor
{
    /// <summary>
    /// Puts each scene's root controller into its scene. Safe to run again: a scene that already
    /// has its controller is left untouched. Also callable from the command line with
    /// -executeMethod Lazpoly.Editor.SceneControllerSetup.Run
    /// </summary>
    public static class SceneControllerSetup
    {
        private const string SceneFolder = "Assets/_Lazpoly/Scenes";

        [MenuItem("Lazpoly/Add Scene Controllers")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Ensure<MainMenuController>("MainMenu", "Main Menu");
            Ensure<GameSceneController>("Game", "Game");
            Debug.Log("[Lazpoly] Scene controllers are in place.");
        }

        private static void Ensure<T>(string sceneName, string objectName) where T : MonoBehaviour
        {
            string path = $"{SceneFolder}/{sceneName}.unity";
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            if (Object.FindFirstObjectByType<T>() != null) return;

            var root = new GameObject(objectName);
            root.AddComponent<T>();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
