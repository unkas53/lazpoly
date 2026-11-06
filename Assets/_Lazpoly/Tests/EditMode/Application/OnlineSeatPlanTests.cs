using System;
using Lazpoly.App.Lobby;
using NUnit.Framework;

namespace Lazpoly.App.Tests
{
    public class OnlineSeatPlanTests
    {
        [Test]
        public void HostTakesTheFirstSeatAndOthersFollowInLobbyOrder()
        {
            var members = new[]
            {
                new LobbyMember(7, "Ayşe"),
                new LobbyMember(3, "Host"),
                new LobbyMember(9, "Can"),
            };

            OnlineSeatPlan plan = OnlineSeatPlan.Create(3, members);

            Assert.AreEqual(1, plan.HostPlayerId);
            Assert.AreEqual(3, plan.Seats.Count);
            Assert.AreEqual("Host", plan.Seats[0].Name);
            Assert.AreEqual("Ayşe", plan.Seats[1].Name);
            Assert.AreEqual("Can", plan.Seats[2].Name);
            Assert.AreEqual(2, plan.RemotePlayers.Count);
            Assert.AreEqual(2, plan.RemotePlayers[7]);
            Assert.AreEqual(3, plan.RemotePlayers[9]);
            Assert.IsFalse(plan.RemotePlayers.ContainsKey(3));
        }

        private static readonly string[] Catalog = { "hip", "purple", "teal", "navy" };

        [Test]
        public void EachSeatGetsItsOwnCharacter()
        {
            var members = new[]
            {
                new LobbyMember(7, "Ayşe", figure: "teal"),
                new LobbyMember(3, "Host", figure: "teal"),
                new LobbyMember(9, "Can"),
                new LobbyMember(5, "Ece", figure: "nobody"),
            };

            OnlineSeatPlan plan = OnlineSeatPlan.Create(3, members, Catalog);

            Assert.AreEqual("teal", plan.Seats[0].Figure, "the host sits first, so keeps a character two asked for");
            Assert.AreEqual("hip", plan.Seats[1].Figure, "the later seat gets the first free one");
            Assert.AreEqual("purple", plan.Seats[2].Figure, "no pick: the next free one");
            Assert.AreEqual("navy", plan.Seats[3].Figure, "an unknown character is no pick");
            Assert.AreEqual("", OnlineSeatPlan.Create(3, members).Seats[0].Figure, "no catalogue, no characters");
        }

        [Test]
        public void PicksAreSettledTheSameWayEverywhere()
        {
            CollectionAssert.AreEqual(new[] { "navy", "hip", "purple" }, FigurePicks.Resolve(new[] { "navy", null, "" }, Catalog));
            CollectionAssert.AreEqual(new[] { "hip", "purple", "teal", "navy", "hip" }, FigurePicks.Resolve(new string[5], Catalog), "a fifth seat repeats only when all are taken");
            Assert.IsTrue(FigurePicks.IsFree(new[] { "hip", "teal" }, 0, "hip"), "a seat's own character is free for it");
            Assert.IsFalse(FigurePicks.IsFree(new[] { "hip", "teal" }, 0, "teal"));
            Assert.Throws<ArgumentNullException>(() => FigurePicks.Resolve(null, Catalog));
        }

        [Test]
        public void EmptyNamesGetAPlaceholderAndDuplicatesAreIgnored()
        {
            var members = new[] { new LobbyMember(1, "Host"), new LobbyMember(2, "  "), new LobbyMember(2, "again") };

            OnlineSeatPlan plan = OnlineSeatPlan.Create(1, members);

            Assert.AreEqual(2, plan.Seats.Count);
            Assert.AreEqual("Oyuncu 2", plan.Seats[1].Name);
        }

        [Test]
        public void InvalidLobbiesAreRefused()
        {
            Assert.Throws<ArgumentException>(() => OnlineSeatPlan.Create(1, new[] { new LobbyMember(1, "Yalnız") }));
            Assert.Throws<ArgumentException>(() => OnlineSeatPlan.Create(5, new[] { new LobbyMember(1, "A"), new LobbyMember(2, "B") }));

            var nine = new LobbyMember[9];
            for (int i = 0; i < nine.Length; i++) nine[i] = new LobbyMember((ulong)i + 1, $"P{i}");
            Assert.Throws<ArgumentException>(() => OnlineSeatPlan.Create(1, nine));
        }
    }
}
