using System;
using System.IO;
using Lazpoly.Presentation.Tabletop;
using UnityEngine;

namespace Lazpoly.Presentation.Perf
{
    /// <summary>One of the camera set-ups the store screenshots are taken from, so every batch matches.</summary>
    public readonly struct ScreenshotPreset
    {
        public ScreenshotPreset(string name, float yaw, float zoom)
        {
            Name = name;
            Yaw = yaw;
            Zoom = zoom;
        }

        /// <summary>Turkish name shown in the log when the preset is chosen (a localisation key).</summary>
        public string Name { get; }

        /// <summary>Which corner the camera looks from; <see cref="BoardCameraRig.BaseYaw"/> is the game's own view.</summary>
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
            new ScreenshotPreset("Genel görünüm", BoardCameraRig.BaseYaw, 1f),
            new ScreenshotPreset("Yakın plan", BoardCameraRig.BaseYaw, 0.6f),
            new ScreenshotPreset("Karşı köşe", BoardCameraRig.BaseYaw + 180f, 1f),
            new ScreenshotPreset("Sol köşe", BoardCameraRig.BaseYaw - 90f, 0.85f),
            new ScreenshotPreset("Sağ köşe", BoardCameraRig.BaseYaw + 90f, 0.85f),
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
