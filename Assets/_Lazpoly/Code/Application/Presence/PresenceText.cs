namespace Lazpoly.App.Presence
{
    /// <summary>The status lines friends see. Kept short: Steam cuts long text.</summary>
    public static class PresenceText
    {
        public static string MainMenu() => "Ana menüde";

        public static string InRoom(int members, int maxMembers) => $"Oda kuruyor ({members}/{maxMembers})";

        public static string InGame(bool online, bool againstComputers, int turn, int activePlayers)
        {
            string kind = online ? "Çevrim içi oyunda" : againstComputers ? "Bilgisayarlara karşı" : "Yerel oyunda";
            return $"{kind}, {turn}. tur, {activePlayers} oyuncu";
        }

        public static string GameOver(bool won) => won ? "Oyunu kazandı!" : "Oyun bitti";
    }
}
