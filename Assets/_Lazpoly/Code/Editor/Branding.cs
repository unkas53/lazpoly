using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Lazpoly.Editor
{
    /// <summary>
    /// The player's branding: our logo as the application icon (Lazpoly.exe, task bar, window title) and
    /// no "Made with Unity" splash before the first scene (Unity 6 lets every plan turn it off). The
    /// settings are checked in; this writes them again if they were lost, and can run from the command
    /// line with -executeMethod Lazpoly.Editor.Branding.Apply
    /// </summary>
    public static class Branding
    {
        public const string IconPath = "Assets/_Lazpoly/Art/Textures/lazpoly-icon.png";

        [MenuItem("Lazpoly/Apply Branding")]
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"[Lazpoly] Icon '{IconPath}' is missing; run Tools/Art/make_logo.py.");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Lazpoly] Branding applied: default icon set, Unity splash screen off.");
        }
    }
}
