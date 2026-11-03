using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Lazpoly.Editor
{
    /// <summary>
    /// The player's branding: the name "Lazpoly" on the window, our logo as the application icon (Lazpoly.exe,
    /// task bar, window title; the medallion alone at the small sizes, where the whole logo is a smear), no
    /// "Made with Unity" splash before the first scene (Unity 6 lets every plan turn it off) and the loading
    /// screen's cream as the colour the player clears the window to before our first frame. The company name
    /// stays "DefaultCompany" for now: the saves and settings live under it, and moving them is a release step. The
    /// settings are checked in; this writes them again if they were lost, and can run from the command
    /// line with -executeMethod Lazpoly.Editor.Branding.Apply
    /// </summary>
    public static class Branding
    {
        public const string IconPath = "Assets/_Lazpoly/Art/Textures/lazpoly-icon.png";

        /// <summary>The medallion alone, for the sizes at which the whole logo is a smear (48 and under).</summary>
        public const string SmallIconPath = "Assets/_Lazpoly/Art/Textures/lazpoly-icon-small.png";
        public const int SmallIconUpTo = 48;

        public const string ProductName = "Lazpoly";

        /// <summary>The loading screen's paper (UiPalette.Paper.Panel).</summary>
        public static readonly Color StartColor = new Color(0.9843137f, 0.972549f, 0.9254902f, 1f);

        [MenuItem("Lazpoly/Apply Branding")]
        public static void Apply()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                Debug.LogError($"[Lazpoly] Icon '{IconPath}' is missing; run Tools/Art/make_logo.py.");
                return;
            }

            var small = AssetDatabase.LoadAssetAtPath<Texture2D>(SmallIconPath);
            if (small == null)
            {
                Debug.LogError($"[Lazpoly] Small icon '{SmallIconPath}' is missing; run Tools/Art/make_logo.py.");
                return;
            }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            int[] sizes = PlayerSettings.GetIconSizes(NamedBuildTarget.Standalone, IconKind.Any);
            var standalone = new Texture2D[sizes.Length];
            for (int i = 0; i < sizes.Length; i++) standalone[i] = sizes[i] <= SmallIconUpTo ? small : icon;
            PlayerSettings.SetIcons(NamedBuildTarget.Standalone, standalone, IconKind.Any);
            PlayerSettings.productName = ProductName;
            PlayerSettings.SplashScreen.backgroundColor = StartColor;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            AssetDatabase.SaveAssets();
            Debug.Log("[Lazpoly] Branding applied: name, logo icon (medallion at 48 and under), Unity splash screen off, cream start colour.");
        }
    }
}
