using System.IO;
using Lazpoly.App.Achievements;
using NUnit.Framework;
using UnityEngine;

namespace Lazpoly.App.Tests
{
    /// <summary>
    /// The files in the repository's Steam folder are typed into (or uploaded to) the Steamworks partner
    /// site by hand; these checks keep them in step with the code.
    /// </summary>
    public class SteamPartnerFilesTests
    {
        private static string SteamFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Steam"));

        [Test]
        public void SetupGuideListsEveryAchievementWithItsTitle()
        {
            string guide = File.ReadAllText(Path.Combine(SteamFolder, "README.md"));
            foreach (AchievementDefinition achievement in AchievementCatalog.All)
            {
                StringAssert.Contains($"| {achievement.Id} | {achievement.Title} |", guide, $"{achievement.Id} is missing from Steam/README.md.");
            }
        }

        [TestCase("rich_presence.vdf", "turkish")]
        [TestCase("rich_presence_english.vdf", "english")]
        public void RichPresenceFilesShowTheStatusLine(string file, string language)
        {
            string text = File.ReadAllText(Path.Combine(SteamFolder, file));
            StringAssert.Contains($"\"Language\"\t\"{language}\"", text);
            StringAssert.Contains("\"#Status\"\t\"{%status%}\"", text);
        }

        [Test]
        public void DepotNeverShipsDevelopmentFiles()
        {
            string depot = File.ReadAllText(Path.Combine(SteamFolder, "depot_build_windows.vdf"));
            StringAssert.Contains("\"FileExclusion\"\t\"steam_appid.txt\"", depot);
            StringAssert.Contains("\"FileExclusion\"\t\"*.pdb\"", depot);
        }
    }
}
