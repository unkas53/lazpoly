using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Lazpoly.Infra.Settings
{
    /// <summary>
    /// The game was called Lazpoly until 2026-11-10 and is Cash Father now. Unity keeps a player's files and settings
    /// under the product name (on Windows the folder LocalLow\DefaultCompany\&lt;name&gt; and the registry key
    /// HKCU\Software\DefaultCompany\&lt;name&gt;), so under the new name the save, the error log and the settings would
    /// look lost. On the first start under the new name this copies them over: the files the new folder does not
    /// have yet, and the old settings' values. Then it notes that it ran (<see cref="DoneKey"/>), so a save
    /// deleted later (a finished game) never comes back from the old folder. Nothing under the old name is changed.
    /// </summary>
    public static class OldNameData
    {
        public const string OldProductName = "Lazpoly";

        /// <summary>Set in the new name's settings once the old name's data has been brought over.</summary>
        public const string DoneKey = "lazpoly.oldNameAdopted";

        /// <summary>The old name's data folder beside <paramref name="dataFolder"/>, or null when there is no parent to look in.</summary>
        public static string OldFolder(string dataFolder)
        {
            if (string.IsNullOrEmpty(dataFolder)) return null;
            string parent = Path.GetDirectoryName(dataFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            return string.IsNullOrEmpty(parent) ? null : Path.Combine(parent, OldProductName);
        }

        /// <summary>Copies the files directly in <paramref name="from"/> that <paramref name="to"/> lacks; returns how many. Folders inside are left alone.</summary>
        public static int CopyMissingFiles(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to) || !Directory.Exists(from)) return 0;
            if (string.Equals(Path.GetFullPath(from), Path.GetFullPath(to), StringComparison.OrdinalIgnoreCase)) return 0;

            int copied = 0;
            foreach (string file in Directory.GetFiles(from))
            {
                string target = Path.Combine(to, Path.GetFileName(file));
                if (File.Exists(target)) continue;
                Directory.CreateDirectory(to);
                File.Copy(file, target);
                copied++;
            }

            return copied;
        }

        /// <summary>
        /// Brings the old name's files and settings over, the first time only; called at start, in the player only.
        /// The settings go first (on Windows they replace the new key's values), then the note in <paramref name="storage"/>.
        /// </summary>
        public static void Adopt(ISettingsStorage storage, string dataFolder, string companyName, string productName)
        {
            if (string.Equals(productName, OldProductName, StringComparison.Ordinal) || storage.HasKey(DoneKey)) return;
            try
            {
                bool settings = CopySettings(companyName, productName);
                int files = CopyMissingFiles(OldFolder(dataFolder), dataFolder);
                if (files > 0 || settings) Debug.Log($"[Lazpoly] Brought over from the old name: {files} file(s){(settings ? " and the settings" : string.Empty)}.");
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Lazpoly] The old name's files could not be copied: {exception.Message}");
            }

            storage.SetInt(DoneKey, 1);
            storage.Save();
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static readonly UIntPtr CurrentUser = new UIntPtr(0x80000001u);
        private const int KeyRead = 0x20019;
        private const int KeyAllAccess = 0xF003F;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegOpenKeyExW(UIntPtr key, string subKey, int options, int access, out UIntPtr result);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegCreateKeyExW(UIntPtr key, string subKey, int reserved, string keyClass, int options, int access, IntPtr security, out UIntPtr result, out int disposition);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private static extern int RegCopyTreeW(UIntPtr source, string subKey, UIntPtr target);

        [DllImport("advapi32.dll")]
        private static extern int RegCloseKey(UIntPtr key);

        /// <summary>Copies the old PlayerPrefs key's values into the new one (Unity may already have put its own few there at start).</summary>
        private static bool CopySettings(string companyName, string productName)
        {
            if (RegOpenKeyExW(CurrentUser, $@"Software\{companyName}\{OldProductName}", 0, KeyRead, out UIntPtr old) != 0) return false;
            try
            {
                if (RegCreateKeyExW(CurrentUser, $@"Software\{companyName}\{productName}", 0, null, 0, KeyAllAccess, IntPtr.Zero, out UIntPtr fresh, out _) != 0) return false;
                try
                {
                    return RegCopyTreeW(old, null, fresh) == 0;
                }
                finally
                {
                    RegCloseKey(fresh);
                }
            }
            finally
            {
                RegCloseKey(old);
            }
        }
#else
        private static bool CopySettings(string companyName, string productName) => false;
#endif
    }
}
