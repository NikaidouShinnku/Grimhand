using System;
using System.Collections.Generic;
using Grimhand.Steam;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    /// <summary>Pure managed fakes: never initialize SteamManager, native Steam, cloud or disk saves.</summary>
    public sealed class SteamAchievementQueueTests
    {
        [Test]
        public void ProductionAppIdMatchesTheGrimhandSteamworksApplication()
        {
            // Read only a compile-time constant: do not initialize Steam or submit achievements.
            Assert.That(SteamManager.AppId, Is.EqualTo(5111710u));
            Assert.That(SteamManager.AppId, Is.Not.EqualTo(5111712u), "Depot ID must not be used as the App ID.");
        }

        FakeAdapter steam;
        SteamAchievementQueue queue;
        HashSet<string> earned;

        [SetUp]
        public void SetUp()
        {
            steam = new FakeAdapter();
            earned = new HashSet<string> { "ONE" };
            queue = new SteamAchievementQueue(steam);
            queue.Bind(() => earned, 1);
        }

        [TearDown]
        public void TearDown() => queue.Dispose();

        void Ready(double time = 0)
        {
            queue.Tick(time);
            steam.Receive(1, true);
            queue.Tick(time + 1);
        }

        [Test]
        public void DoesNotSetBeforeSuccessfulStatsCallback()
        {
            queue.Tick(0); queue.Tick(1);
            Assert.That(steam.Requests, Is.EqualTo(1));
            Assert.That(steam.Sets, Is.Empty);
            steam.Receive(2, true); queue.Tick(2);
            Assert.That(steam.Sets, Is.Empty);
            steam.Receive(1, true); queue.Tick(3);
            Assert.That(steam.Sets, Is.EqualTo(new[] { "ONE" }));
            Assert.That(steam.Stores, Is.EqualTo(1));
        }

        [Test]
        public void FailedStatsCallbackUsesBackoff()
        {
            queue.Tick(0); steam.Receive(1, false);
            queue.Tick(1);
            Assert.That(steam.Requests, Is.EqualTo(1));
            queue.Tick(2);
            Assert.That(steam.Requests, Is.EqualTo(2));
            Assert.That(steam.Sets, Is.Empty);
        }

        [Test]
        public void RejectedRequestsUseExponentialBackoff()
        {
            steam.AcceptRequest = false;
            queue.Tick(0); queue.Tick(1); queue.Tick(2); queue.Tick(5);
            Assert.That(steam.Requests, Is.EqualTo(2));
            queue.Tick(6);
            Assert.That(steam.Requests, Is.EqualTo(3));
        }

        [Test]
        public void StoreFailureRetriesEvenWhenSetChangedSteamCache()
        {
            Ready();
            Assert.That(steam.Cached.Contains("ONE"), Is.True);
            steam.Stored(false);
            queue.Tick(2);
            Assert.That(steam.Requests, Is.EqualTo(1));
            queue.Tick(3); steam.Receive(1, true); queue.Tick(4);
            Assert.That(steam.Sets, Is.EqualTo(new[] { "ONE", "ONE" }));
            Assert.That(steam.Stores, Is.EqualTo(2));
            steam.Stored(true); queue.Tick(6);
            Assert.That(steam.Stores, Is.EqualTo(2));
        }

        [Test]
        public void RejectedStoreRetainsUncommittedIds()
        {
            steam.AcceptStore = false;
            Ready();
            steam.AcceptStore = true;
            queue.Tick(3); steam.Receive(1, true); queue.Tick(4);
            Assert.That(steam.Sets.Count, Is.EqualTo(2));
            Assert.That(steam.Stores, Is.EqualTo(2));
        }

        [Test]
        public void UnknownBackendIdRetriesWithoutDroppingLocalUnlock()
        {
            steam.AcceptGet = false;
            Ready();
            Assert.That(steam.Stores, Is.Zero);
            Assert.That(earned.Contains("ONE"), Is.True);
            steam.AcceptGet = true;
            queue.Tick(3); steam.Receive(1, true); queue.Tick(4);
            Assert.That(steam.Stores, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulStoreCommitsAndNewLocalIdsAreDiscovered()
        {
            Ready(); steam.Stored(true); queue.Tick(3);
            Assert.That(steam.Sets.Count, Is.EqualTo(1));
            earned.Add("TWO"); queue.Tick(5);
            Assert.That(steam.Sets, Is.EqualTo(new[] { "ONE", "TWO" }));
        }

        [Test]
        public void ExistingSteamUnlocksArePreserved()
        {
            steam.Cached.Add("ONE"); steam.Cached.Add("STEAM_ONLY");
            Ready();
            Assert.That(steam.Sets, Is.Empty);
            Assert.That(steam.Stores, Is.Zero);
            Assert.That(steam.Cached.Contains("STEAM_ONLY"), Is.True);
            Assert.That(earned.Contains("STEAM_ONLY"), Is.False);
        }

        [Test]
        public void ProfileRebindWithDifferentOwnerRejectsOldCallback()
        {
            queue.Tick(0);
            queue.Bind(() => new[] { "OTHER_PROFILE" }, 2);
            steam.Receive(1, true); queue.Tick(1);
            Assert.That(steam.Requests, Is.EqualTo(1));
            Assert.That(steam.Sets, Is.Empty);
            steam.AccountId = 2; queue.Tick(2);
            steam.Receive(1, true); queue.Tick(3);
            Assert.That(steam.Sets, Is.Empty);
            steam.Receive(2, true); queue.Tick(4);
            Assert.That(steam.Sets, Is.EqualTo(new[] { "OTHER_PROFILE" }));
        }

        [Test]
        public void LateStatsCallbackAfterAccountSwitchBeforeBindCannotUnlock()
        {
            queue.Tick(0);
            steam.AccountId = 2;
            steam.Receive(1, true); // SteamManager may pump callbacks before GameFlow rebinds.
            queue.Tick(1);
            Assert.That(steam.Sets, Is.Empty);
            queue.Bind(() => new[] { "OTHER_PROFILE" }, 2);
            queue.Tick(2); steam.Receive(2, true); queue.Tick(3);
            Assert.That(steam.Sets, Is.EqualTo(new[] { "OTHER_PROFILE" }));
        }

        [Test]
        public void LateStoreCallbackBeforeRebindDoesNotCommitPreviousAccountBatch()
        {
            Ready(); steam.AccountId = 2;
            steam.Stored(true); queue.Tick(2);
            Assert.That(steam.Stores, Is.EqualTo(1));
            queue.Bind(() => new[] { "OTHER_PROFILE" }, 2);
            steam.Stored(true); // No new store is pending: ignore the old completion.
            queue.Tick(3); steam.Receive(2, true); queue.Tick(4);
            Assert.That(steam.Sets, Is.EqualTo(new[] { "ONE", "OTHER_PROFILE" }));
            Assert.That(steam.Stores, Is.EqualTo(2));
        }

        [Test]
        public void StatsCallbackTimeoutRetriesAfterBackoff()
        {
            queue.Tick(0); queue.Tick(30); queue.Tick(31);
            Assert.That(steam.Requests, Is.EqualTo(1));
            queue.Tick(32);
            Assert.That(steam.Requests, Is.EqualTo(2));
        }

        [Test]
        public void StoreCallbackTimeoutRetriesUncommittedCachedUnlock()
        {
            Ready(); queue.Tick(31); queue.Tick(32);
            Assert.That(steam.Requests, Is.EqualTo(1));
            queue.Tick(33); steam.Receive(1, true); queue.Tick(34);
            Assert.That(steam.Sets.Count, Is.EqualTo(2));
            Assert.That(steam.Stores, Is.EqualTo(2));
        }

        [Test]
        public void UnbindAndDisposeIgnoreLateCallbacks()
        {
            queue.Tick(0); queue.Bind(null); steam.Receive(1, true); queue.Tick(1);
            Assert.That(steam.Sets, Is.Empty);
            queue.Dispose(); steam.Receive(1, true); steam.Stored(true); queue.Tick(99);
            Assert.That(steam.Disposed, Is.True);
            Assert.That(steam.Sets, Is.Empty);
        }

        [TestCase("", 0UL, false, SteamAchievementOwnership.Status.Waiting, false)]
        [TestCase("", 123UL, false, SteamAchievementOwnership.Status.Waiting, false)]
        [TestCase("", 123UL, true, SteamAchievementOwnership.Status.Ready, true)]
        [TestCase("123", 123UL, false, SteamAchievementOwnership.Status.Ready, false)]
        [TestCase("123", 456UL, true, SteamAchievementOwnership.Status.Mismatch, false)]
        [TestCase("123", 0UL, true, SteamAchievementOwnership.Status.Waiting, false)]
        [TestCase("bad", 123UL, true, SteamAchievementOwnership.Status.Invalid, false)]
        [TestCase(" ", 123UL, true, SteamAchievementOwnership.Status.Invalid, false)]
        [TestCase("0", 123UL, true, SteamAchievementOwnership.Status.Invalid, false)]
        public void OwnershipNeverClaimsEditorOrTestAccountOrOverwritesSavedOwner(
            string saved, ulong account, bool verifiedPlayerLogin, SteamAchievementOwnership.Status expected, bool shouldStamp)
        {
            var status = SteamAchievementOwnership.Resolve(saved, account, verifiedPlayerLogin, out var owner, out var stamp);
            Assert.That(status, Is.EqualTo(expected));
            Assert.That(stamp, Is.EqualTo(shouldStamp));
            if (stamp) Assert.That(owner, Is.EqualTo(account));
            if (saved == "123") Assert.That(owner, Is.EqualTo(123UL));
        }

        sealed class FakeAdapter : ISteamAchievementAdapter
        {
            public bool IsAvailable { get; set; } = true;
            public ulong AccountId { get; set; } = 1;
            public event Action<ulong, bool> StatsReceived;
            public event Action<bool> StatsStored;
            public bool AcceptRequest = true, AcceptGet = true, AcceptStore = true, Disposed;
            public int Requests, Stores;
            public readonly List<string> Sets = new List<string>();
            public readonly HashSet<string> Cached = new HashSet<string>();
            public bool RequestCurrentStats() { Requests++; return AcceptRequest; }
            public bool GetAchievement(string id, out bool unlocked) { unlocked = Cached.Contains(id); return AcceptGet; }
            public bool SetAchievement(string id) { Sets.Add(id); Cached.Add(id); return true; }
            public bool StoreStats() { Stores++; return AcceptStore; }
            public void Receive(ulong account, bool success) => StatsReceived?.Invoke(account, success);
            public void Stored(bool success) => StatsStored?.Invoke(success);
            public void Dispose() => Disposed = true;
        }
    }
}
