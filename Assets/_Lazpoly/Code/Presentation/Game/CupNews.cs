using static Lazpoly.Presentation.Texts.Lang;
namespace Lazpoly.Presentation.Game
{
    /// <summary>
    /// The card in the middle when the champions' cup goes to a town (the owner's wish, Faz 29): "Şampiyonlar Kupası maçları
    /// X şehrinde yapılıyor", with the raise under it. It stays up for <see cref="Seconds"/> while the table waits; when it is
    /// gone fireworks go up over the town and, once they are over, the rents on the board change (as after a trade
    /// agreement, <see cref="GroupDeal"/>). Pure, so it is tested without a scene.
    /// </summary>
    public static class CupNews
    {
        public const float Seconds = 4f;

        /// <summary>The fireworks' gold, the cup's colour.</summary>
        public static readonly UnityEngine.Color FireworkColor = new UnityEngine.Color(1f, 0.78f, 0.25f);

        public static string Title => L("ŞAMPİYONLAR KUPASI");

        /// <summary>"Şampiyonlar Kupası maçları Paris şehrinde yapılıyor"; <paramref name="town"/> is the board's (Turkish) name.</summary>
        public static string Text(string town) => F($"Şampiyonlar Kupası maçları {L(town)} şehrinde yapılıyor");

        /// <summary>"Şehrin kirası %100 artıyor."</summary>
        public static string RaiseText(int percent) => F($"Şehrin kirası %{percent} artıyor.");
    }
}
