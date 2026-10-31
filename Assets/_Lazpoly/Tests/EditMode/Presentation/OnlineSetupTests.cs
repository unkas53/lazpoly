using System;
using System.Collections.Generic;
using Lazpoly.App.Lobby;
using Lazpoly.Net.Transport;
using Lazpoly.Presentation.Menu;
using Lazpoly.Presentation.Setup;
using NUnit.Framework;

namespace Lazpoly.Presentation.Tests
{
    public class OnlineSetupTests
    {
        private sealed class FakeLobby : ILobbyService
        {
            public bool IsAvailable { get; set; } = true;
            public ulong LocalId { get; set; } = 2;
            public LobbyStatus Status { get; set; } = LobbyStatus.InLobby;
            public ulong OwnerId { get; set; } = 1;
            public bool IsOwner => OwnerId == LocalId;
            public List<LobbyMember> MemberList { get; } = new List<LobbyMember> { new LobbyMember(1, "Ev"), new LobbyMember(2, "Ben") };
            public IReadOnlyList<LobbyMember> Members => MemberList;
            public bool IsGameStarted { get; set; }
            public int StartCalls { get; private set; }

            public event Action Changed { add { } remove { } }
            public event Action<LobbyError> Failed { add { } remove { } }
            public event Action<ulong> MemberLeft { add { } remove { } }

            public bool IsMember(ulong peer) => MemberList.Exists(m => m.Id == peer);
            public void Create(int maxMembers) { }
            public void Join(ulong lobbyId) { }
            public bool IsOverlayAvailable => false;
            public List<LobbyFriend> FriendList { get; } = new List<LobbyFriend>();
            public List<ulong> Invited { get; } = new List<ulong>();
            public void InviteFriends() { }
            public bool Invite(ulong friendId) { Invited.Add(friendId); return true; }
            public IReadOnlyList<LobbyFriend> GetFriends() => new List<LobbyFriend>(FriendList); // A fresh list, like Steam.
            public void StartGame() => StartCalls++;
            public void Leave() => Status = LobbyStatus.Idle;
        }

        [TearDown]
        public void Clear()
        {
            OnlineGameSetup.Take();
        }

        [Test]
        public void LaunchIsHandedOverOnce()
        {
            ITransport transport = new LoopbackNetwork().CreateTransport(1);
            OnlineGameSetup.Set(OnlineLaunch.AsClient(transport, 5));

            OnlineLaunch launch = OnlineGameSetup.Take();

            Assert.IsFalse(launch.IsHost);
            Assert.AreEqual(5UL, launch.HostPeer);
            Assert.AreSame(transport, launch.Transport);
            Assert.IsNull(OnlineGameSetup.Take());
        }

        [Test]
        public void MemberFollowsTheOwnerIntoTheGameOnce()
        {
            var lobby = new FakeLobby();
            ITransport transport = new LoopbackNetwork().CreateTransport(2);
            int loads = 0;
            var panel = new LobbyPanel(lobby, transport, () => loads++);

            panel.Update();
            Assert.AreEqual(0, loads, "Left before the owner started.");

            lobby.IsGameStarted = true;
            panel.Update();
            panel.Update();

            Assert.AreEqual(1, loads);
            OnlineLaunch launch = OnlineGameSetup.Take();
            Assert.IsNotNull(launch);
            Assert.IsFalse(launch.IsHost);
            Assert.AreEqual(1UL, launch.HostPeer);
            panel.Dispose();
        }

        [Test]
        public void OwnerDoesNotFollowItself()
        {
            var lobby = new FakeLobby { LocalId = 1, IsGameStarted = true };
            int loads = 0;
            var panel = new LobbyPanel(lobby, new LoopbackNetwork().CreateTransport(1), () => loads++);

            panel.Update();

            Assert.AreEqual(0, loads);
            Assert.IsNull(OnlineGameSetup.Take());
        }

        [Test]
        public void FriendsAreLoadedWhenTheRoomChangesAndCanBeInvitedDirectly()
        {
            var lobby = new FakeLobby { LocalId = 1, Status = LobbyStatus.Idle };
            lobby.FriendList.Add(new LobbyFriend(42, "Deniz", true, 0));
            var panel = new LobbyPanel(lobby, new LoopbackNetwork().CreateTransport(1), () => { });

            panel.Update();
            Assert.AreEqual(1, panel.Friends.Count);

            lobby.FriendList.Add(new LobbyFriend(43, "Ece", true, 777));
            panel.Update();
            Assert.AreEqual(1, panel.Friends.Count, "Reloaded although nothing changed.");

            lobby.Status = LobbyStatus.InLobby;
            panel.Update();
            Assert.AreEqual(2, panel.Friends.Count, "Not reloaded after entering the room.");

            panel.InviteFriend(panel.Friends[0]);
            CollectionAssert.AreEqual(new[] { 42UL }, lobby.Invited);
            panel.Dispose();
        }

        [Test]
        public void PanelWithoutSteamIsUnavailable()
        {
            Assert.IsFalse(new LobbyPanel(null, null, () => { }).IsAvailable);
            Assert.IsFalse(new LobbyPanel(new FakeLobby { IsAvailable = false }, new LoopbackNetwork().CreateTransport(1), () => { }).IsAvailable);
        }
    }
}
