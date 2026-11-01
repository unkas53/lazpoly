using System;
using System.IO;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>One of the camera set-ups the store screenshots are taken from, so every batch matches.</summary>
    public readonly struct ScreenshotPreset
    {
        public ScreenshotPreset(string name, CameraView view, float yaw, float zoom)
        {
            Name = name;
            View = view;
            Yaw = yaw;
            Zoom = zoom;
        }

        /// <summary>Turkish name shown in the log when the preset is chosen (a localisation key).</summary>
        public string Name { get; }
        public CameraView View { get; }
        public float Yaw { get; }
        public float Zoom { get; }
    }

    /// <summary>
    /// The five camera set-ups for the Steam store screenshots (F11 steps through them, F12 saves a
    /// picture). Kept in code so the game itself frames the shot; Tools/Art/screenshots.md says what
    /// each one should show.
    /// </summary>
    public static class ScreenshotPresets
    {
        public static readonly ScreenshotPreset[] All =
        {
            new ScreenshotPreset("Masa başı", CameraView.Table, 0f, 1f),
            new ScreenshotPreset("Kuş bakışı", CameraView.Overhead, 0f, 1f),
            new ScreenshotPreset("Yakın köşe", CameraView.Table, 35f, 0.6f),
            new ScreenshotPreset("Ada ve deniz", CameraView.Table, -30f, 0.7f),
            new ScreenshotPreset("Karşı kıyı", CameraView.Table, 180f, 0.85f),
        };

        public static ScreenshotPreset At(int index) => All[((index % All.Length) + All.Length) % All.Length];
    }

    /// <summary>Saves what is on screen as a PNG in the player's data folder.</summary>
    public static class Screenshots
    {
        public static string Folder => Path.Combine(Application.persistentDataPath, "Screenshots");

        /// <summary>File name for a shot taken at <paramref name="when"/>: lazpoly-20261101-183000.png.</summary>
        public static string FileName(DateTime when) => $"lazpoly-{when:yyyyMMdd-HHmmss}.png";

        /// <summary>Takes the shot at the screen's own size and says where it will land (the file is written over the next frame).</summary>
        public static string Capture()
        {
            Directory.CreateDirectory(Folder);
            string path = Path.Combine(Folder, FileName(DateTime.Now));
            ScreenCapture.CaptureScreenshot(path, 1);
            return path;
        }
    }
}
