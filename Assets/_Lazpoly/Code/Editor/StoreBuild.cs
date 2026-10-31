using System;
using System.IO;
using System.Linq;
using Lazpoly.Platform.Steam;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Lazpoly.Editor
{
    /// <summary>
    /// Builds the Windows version that goes to Steam: no development options, into Builds/Store/windows,
    /// without steam_appid.txt (Steam itself starts a store build) or Unity's do-not-ship folders. Refuses while the game still runs as
    /// the test App ID, unless -allowDevelopmentAppId is passed for a rehearsal build that is never uploaded.
    /// Also callable from the command line with -executeMethod Lazpoly.Editor.StoreBuild.Run
    /// </summary>
    public static class StoreBuild
    {
        public const string OutputFolder = "Builds/Store/windows";
        private const string AllowDevelopmentAppIdFlag = "-allowDevelopmentAppId";

        [MenuItem("Lazpoly/Build Store Version")]
        public static void Run()
        {
            bool allowDevelopmentAppId = Environment.GetCommandLineArgs().Contains(AllowDevelopmentAppIdFlag);
            if (SteamSettings.IsDevelopmentAppId && !allowDevelopmentAppId)
            {
                Fail($"The game still runs as the test App ID {SteamSettings.DevelopmentAppId}. Set SteamSettings.AppId first " +
                     $"(see Steam/README.md), or pass {AllowDevelopmentAppIdFlag} for a rehearsal build.");
                return;
            }

            string[] scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0)
            {
                Fail("No scenes are enabled in Build Settings.");
                return;
            }

            if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(OutputFolder, "Lazpoly.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Fail($"Store build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
                return;
            }

            string appIdFile = Path.Combine(OutputFolder, "steam_appid.txt");
            if (File.Exists(appIdFile)) File.Delete(appIdFile);

            // Unity leaves debug-only folders next to the player ("..._DoNotShip", "..._ButDontShipItWithYourGame").
            foreach (string folder in Directory.GetDirectories(OutputFolder))
            {
                string name = Path.GetFileName(folder);
                if (name.EndsWith("_DoNotShip") || name.EndsWith("_ButDontShipItWithYourGame")) Directory.Delete(folder, true);
            }

            string kind = SteamSettings.IsDevelopmentAppId ? "REHEARSAL build (test App ID, do not upload)" : $"store build for App ID {SteamSettings.AppId}";
            Debug.Log($"[Lazpoly] {kind} in {OutputFolder}, {report.summary.totalSize / (1024 * 1024)} MB.");
        }

        private static void Fail(string message)
        {
            Debug.LogError($"[Lazpoly] {message}");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
