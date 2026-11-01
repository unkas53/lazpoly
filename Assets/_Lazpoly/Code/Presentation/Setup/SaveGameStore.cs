using System;
using System.IO;
using Lazpoly.App.Save;
using UnityEngine;

namespace Lazpoly.Presentation.Setup
{
    /// <summary>The one local save slot, kept in the player's data folder.</summary>
    public static class SaveGameStore
    {
        private const string FileName = "lazpoly-kayit.dat";

        /// <summary>Folder of the save; tests point it at a temporary folder. Null means the player's data folder.</summary>
        public static string FolderOverride { get; set; }

        public static string FilePath => Path.Combine(FolderOverride ?? Application.persistentDataPath, FileName);

        /// <summary>Writes through a temporary file so a crash mid-write never leaves a broken save.</summary>
        public static void Write(SavedGame save)
        {
            byte[] data = SavedGameCodec.Encode(save);
            string path = FilePath;
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, data);
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        /// <summary>Whether a save file is there at all, readable or not.</summary>
        public static bool Exists => File.Exists(FilePath);

        /// <summary>When the save was last written (local time), or null when there is none.</summary>
        public static DateTime? SavedAt => Exists ? File.GetLastWriteTime(FilePath) : (DateTime?)null;

        /// <summary>Reads the save; false when there is none or it cannot be read (the reason is logged).</summary>
        public static bool TryRead(out SavedGame save)
        {
            save = null;
            string path = FilePath;
            if (!File.Exists(path)) return false;

            try
            {
                save = SavedGameCodec.Decode(File.ReadAllBytes(path));
                return true;
            }
            catch (Exception exception) when (exception is FormatException || exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Lazpoly] Save file could not be read: {exception.Message}");
                return false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            FolderOverride = null;
        }

        public static void Delete()
        {
            string path = FilePath;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
