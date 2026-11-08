using System.Collections.Generic;

namespace Lazpoly.App.Achievements
{
    /// <summary>One Steam achievement. <see cref="Id"/> is the API name entered on the Steamworks partner site.</summary>
    public sealed class AchievementDefinition
    {
        public AchievementDefinition(string id, string title, string description)
        {
            Id = id;
            Title = title;
            Description = description;
        }

        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Every achievement the game can unlock. The same API names must be created on the Steamworks
    /// partner site for the game's App ID; the Turkish texts are what to enter there.
    /// </summary>
    public static class AchievementCatalog
    {
        public const string FirstWin = "LAZ_FIRST_WIN";
        public const string OnlineWin = "LAZ_ONLINE_WIN";
        public const string BeatComputers = "LAZ_BEAT_COMPUTERS";
        public const string FullGroup = "LAZ_FULL_GROUP";
        public const string FirstHotel = "LAZ_FIRST_HOTEL";
        public const string TradeDone = "LAZ_TRADE_DONE";
        public const string BankruptRival = "LAZ_BANKRUPT_RIVAL";
        public const string Tycoon = "LAZ_TYCOON";

        /// <summary>Money a player must hold at once for <see cref="Tycoon"/>.</summary>
        public const int TycoonMoney = 6000000;

        public static readonly IReadOnlyList<AchievementDefinition> All = new[]
        {
            new AchievementDefinition(FirstWin, "İlk Zafer", "Bir oyunu kazan."),
            new AchievementDefinition(OnlineWin, "Masanın Hâkimi", "Çevrim içi bir oyunu kazan."),
            new AchievementDefinition(BeatComputers, "Makineye Karşı", "Bilgisayar oyuncularının da olduğu bir oyunu kazan."),
            new AchievementDefinition(FullGroup, "Tam Takım", "Bir renk grubunun bütün mülklerine sahip ol."),
            new AchievementDefinition(FirstHotel, "Otelci", "İlk otelini kur."),
            new AchievementDefinition(TradeDone, "Pazarlıkçı", "Bir takası tamamla."),
            new AchievementDefinition(BankruptRival, "Alacaklı", "Bir rakibi sana olan borcu yüzünden iflas ettir."),
            new AchievementDefinition(Tycoon, "Kodaman", $"Aynı anda {TycoonMoney / 1000000}M paraya sahip ol."),
        };
    }
}
