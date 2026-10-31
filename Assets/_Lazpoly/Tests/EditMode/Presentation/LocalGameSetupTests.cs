using System.Collections.Generic;
using Lazpoly.App.AI;
using Lazpoly.App.Save;
using Lazpoly.Core.State;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class LocalGameSetupTests
    {
        [Test]
        public void ChosenSeatsAreHandedOverOnce()
        {
            var chosen = new[] { new PlayerSeat(1, "A"), new PlayerSeat(2, "B"), new PlayerSeat(3, "C") };
            LocalGameSetup.SetSeats(chosen);

            Assert.AreSame(chosen, LocalGameSetup.TakeSeats());

            IReadOnlyList<PlayerSeat> fallback = LocalGameSetup.TakeSeats();
            Assert.AreEqual(2, fallback.Count, "Without a menu choice the scene starts with two players.");
        }

        [Test]
        public void DefaultSeatsHaveUniqueIdsAndNames()
        {
            IReadOnlyList<PlayerSeat> seats = LocalGameSetup.DefaultSeats(8);
            var ids = new HashSet<int>();
            foreach (PlayerSeat seat in seats) Assert.IsTrue(ids.Add(seat.Id));
            Assert.AreEqual("Oyuncu 8", seats[7].Name);
        }

        [Test]
        public void ComputerSeatsAreHandedOverOnce()
        {
            var ai = new Dictionary<int, AiDifficulty> { { 2, AiDifficulty.Hard } };
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(2), ai);

            Assert.AreSame(ai, LocalGameSetup.TakeAiPlayers());
            Assert.IsEmpty(LocalGameSetup.TakeAiPlayers());
            LocalGameSetup.TakeSeats();
        }

        [Test]
        public void ChoosingASaveReplacesNewSeats()
        {
            var save = new SavedGame();
            LocalGameSetup.SetSeats(LocalGameSetup.DefaultSeats(3));
            LocalGameSetup.SetResume(save);

            Assert.AreSame(save, LocalGameSetup.TakeResume());
            Assert.IsNull(LocalGameSetup.TakeResume());
            Assert.AreEqual(2, LocalGameSetup.TakeSeats().Count, "The earlier seat choice was dropped.");
        }
    }
}
