using Lazpoly.Infra.Settings;

namespace Lazpoly.Presentation.Texts
{
    /// <summary>Which first-game hints the player has seen, and whether hints are on. Kept between sessions.</summary>
    public sealed class TipMemory
    {
        private const string SeenKey = "lazpoly.tips.seen";
        private const string OffKey = "lazpoly.tips.off";

        private readonly ISettingsStorage _storage;
        private int _seen;

        public TipMemory(ISettingsStorage storage)
        {
            _storage = storage;
            _seen = storage.HasKey(SeenKey) ? storage.GetInt(SeenKey) : 0;
            Enabled = !storage.HasKey(OffKey) || storage.GetInt(OffKey) == 0;
        }

        public bool Enabled { get; private set; }

        public bool Seen(TipId id) => (_seen & (1 << (int)id)) != 0;

        public void MarkSeen(TipId id)
        {
            if (Seen(id)) return;
            _seen |= 1 << (int)id;
            _storage.SetInt(SeenKey, _seen);
            _storage.Save();
        }

        public void TurnOff()
        {
            Enabled = false;
            _storage.SetInt(OffKey, 1);
            _storage.Save();
        }

        /// <summary>Shows every hint again (from the "Nasıl oynanır" page).</summary>
        public void Reset()
        {
            _seen = 0;
            Enabled = true;
            _storage.SetInt(SeenKey, 0);
            _storage.SetInt(OffKey, 0);
            _storage.Save();
        }
    }
}
