using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Lazpoly.Editor
{
    /// <summary>
    /// The player's branding: the name "Cash Father" on the window (the game was called Lazpoly until 2026-11-10;
    /// the code, the folders and the asset names keep that name), the logo's character as the application icon (CashFather.exe,
    /// task bar, window title; his top-hatted face cropped tighter at 48 and under), no
    /// "Made with Unity" splash before the first scene (Unity 6 lets every plan turn it off) and the loading
    /// screen's cream as the colour the player clears the window to before our first frame. The company name
    /// stays "DefaultCompany" for now: the saves and settings live under it and the product name, and moving them
    /// is a release step (the rename brings them over: Lazpoly.Infra.Settings.OldNameData). The
    /// settings are checked in; this writes them again if they were lost, and can run from the command
    /// line with -executeMethod Lazpoly.Editor.Branding.Apply
    /// </summary>
    public static class Branding
    {
        public const string IconPath = "Assets/_Lazpoly/Art/Textures/lazpoly-icon.png";

        /// <summary>The character's face alone, for the sizes at which more of him is a smear (48 and under).</summary>
        public const string SmallIconPath = "Assets/_Lazpoly/Art/Textures/lazpoly-icon-small.png";
        public const int SmallIconUpTo = 48;

        public const string ProductName = "Cash Father";

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
            Debug.Log("[Lazpoly] Branding applied: name, the logo's character as the icon (a tighter crop at 48 and under), Unity splash screen off, cream start colour.");
        }
    }
}
