using System;
using Grimhand.Persistence;
using Grimhand.Steam;
using NUnit.Framework;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    public sealed class CloudAchievementMergeTests
    {
        const ulong Account = 76561198012345678;
        const string Owner = "76561198012345678";

        static PlayerProfileSaveData Dto(int version = 8, string owner = Owner)
        {
            var dto = SaveDataMapper.ToDto(new PlayerProfileState { AccountGold = 123 });
            dto.saveVersion = version;
            dto.lastSavedUtc = "2026-10-19T10:00:00.0000000Z";
            dto.achievementSteamAccountId = owner;
            return dto;
        }

        static string Signed(PlayerProfileSaveData dto)
        {
            SaveIntegrity.ApplyHash(dto);
            return JsonUtility.ToJson(dto);
        }

        static PlayerProfileSaveData Merge(PlayerProfileSaveData winner, PlayerProfileSaveData other)
        {
            Assert.That(CloudAchievementMerge.TryMerge(Signed(winner), Signed(other), Account, null,
                out var json, out var changed, out var error), Is.True, error);
            Assert.That(changed, Is.True);
            var result = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            Assert.That(SaveIntegrity.Verify(result), Is.True);
            Assert.That(result.lastSavedUtc, Is.EqualTo(winner.lastSavedUtc));
            return result;
        }

        [Test]
        public void NewerCloudWinnerKeepsEveryCoreFieldAndOfflineLocalIds()
        {
            var winner = Dto();
            winner.accountGold = 999;
            winner.hasActiveRun = true;
            winner.activeRunJson = "{\"chosen\":true}";
            winner.activeRunSeed = 42;
            winner.collectionEntries = new[] { "WINNER_CARD" };
            winner.collectionEngravedFlags = new[] { false };
            winner.achievementIds = new[] { "CLOUD" };
            winner.completedEngravings = 3;
            var other = Dto();
            other.lastSavedUtc = "2026-10-18T10:00:00.0000000Z";
            other.accountGold = 999999;
            other.collectionEntries = new[] { "LOSER_CARD" };
            other.achievementIds = new[] { "OFFLINE_PENDING", "CLOUD" };
            other.completedEngravings = 7;
            var result = Merge(winner, other);
            CollectionAssert.AreEquivalent(new[] { "CLOUD", "OFFLINE_PENDING" }, result.achievementIds);
            Assert.That(result.completedEngravings, Is.EqualTo(7));
            // Compare the complete DTO after restoring only the deliberately merged extension fields/hash.
            result.achievementIds = winner.achievementIds;
            result.completedEngravings = winner.completedEngravings;
            result.achievementSteamAccountId = winner.achievementSteamAccountId;
            result.integrityHash = winner.integrityHash;
            Assert.That(JsonUtility.ToJson(result), Is.EqualTo(JsonUtility.ToJson(winner)));
        }

        [Test]
        public void NewerLocalWinnerRetainsOlderDeviceUnlocksWithoutChangingTimestampOrGold()
        {
            var winner = Dto(); winner.accountGold = 17; winner.achievementIds = new[] { "LOCAL" };
            var other = Dto(); other.accountGold = 999; other.achievementIds = new[] { "REMOTE_PENDING" };
            other.lastSavedUtc = "2026-10-18T10:00:00.0000000Z";
            var result = Merge(winner, other);
            Assert.That(result.accountGold, Is.EqualTo(17));
            CollectionAssert.AreEquivalent(new[] { "LOCAL", "REMOTE_PENDING" }, result.achievementIds);
        }

        [Test]
        public void CounterUsesMaximumAndRepeatedMergeIsNoOp()
        {
            var winner = Dto(); winner.completedEngravings = 4;
            var other = Dto(); other.completedEngravings = 7;
            var result = Merge(winner, other);
            Assert.That(result.completedEngravings, Is.EqualTo(7));
            var before = Signed(result);
            Assert.That(CloudAchievementMerge.TryMerge(before, Signed(other), Account, null,
                out var json, out var changed, out var error), Is.True, error);
            Assert.That(changed, Is.False);
            Assert.That(json, Is.EqualTo(before));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DifferentRecordedOwnerPreventsMergeInEitherDirection(bool wrongWinner)
        {
            var winner = Dto(owner: wrongWinner ? "76561198099999999" : Owner);
            var other = Dto(owner: wrongWinner ? Owner : "76561198099999999");
            other.achievementIds = new[] { "FOREIGN" }; other.completedEngravings = 999;
            var before = Signed(winner);
            Assert.That(CloudAchievementMerge.TryMerge(before, Signed(other), Account, null,
                out var json, out var changed, out var error), Is.True, error);
            Assert.That(changed, Is.False);
            Assert.That(json, Is.EqualTo(before));
        }

        [Test]
        public void EmptyOwnersCanMergeWithoutClaimingAccount()
        {
            var winner = Dto(owner: "");
            var other = Dto(owner: ""); other.achievementIds = new[] { "PENDING" };
            var result = Merge(winner, other);
            Assert.That(result.achievementSteamAccountId, Is.Empty);
        }

        [Test]
        public void CompatibleRecordedOwnerIsPreservedWhenWinnerUnclaimed()
        {
            var winner = Dto(owner: "");
            var other = Dto(); other.achievementIds = new[] { "PENDING" };
            Assert.That(Merge(winner, other).achievementSteamAccountId, Is.EqualTo(Owner));
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void LegacyWinnerMigratesFlagsAndCannotPromoteUnsignedAchievementExtensions(int version)
        {
            var winner = Dto(version);
            winner.achievementIds = new[] { "UNSIGNED_INJECTED" };
            winner.completedEngravings = 999;
            winner.achievementSteamAccountId = "76561198099999999";
            winner.hasCompletedTutorial = false;
            winner.berserkerUnlocked = false;
            winner.lichUnlocked = false;
            var other = Dto(); other.achievementIds = new[] { "SIGNED_PENDING" }; other.completedEngravings = 2;
            var result = Merge(winner, other);
            Assert.That(result.saveVersion, Is.EqualTo(8));
            CollectionAssert.AreEqual(new[] { "SIGNED_PENDING" }, result.achievementIds);
            Assert.That(result.completedEngravings, Is.EqualTo(2));
            Assert.That(result.achievementSteamAccountId, Is.EqualTo(Owner));
            Assert.That(result.hasCompletedTutorial, Is.EqualTo(version < 5));
            Assert.That(result.berserkerUnlocked, Is.EqualTo(version < 7));
            Assert.That(result.lichUnlocked, Is.EqualTo(version < 7));
            Assert.That(result.accountGold, Is.EqualTo(winner.accountGold));
        }

        [Test]
        public void LegacyDonorIgnoresUnsignedExtensionsButUsesSignedEngravingFlags()
        {
            var winner = Dto();
            var other = Dto(7);
            other.collectionEntries = new[] { "SIGNED_CARD_A", "SIGNED_CARD_B" };
            other.collectionEngravedFlags = new[] { true, true };
            other.achievementIds = new[] { "UNSIGNED_INJECTED" };
            other.completedEngravings = 999;
            other.achievementSteamAccountId = "76561198099999999";
            var result = Merge(winner, other);
            Assert.That(result.achievementIds, Is.Empty);
            Assert.That(result.completedEngravings, Is.EqualTo(2));
            Assert.That(result.collectionEntries, Is.Empty);
        }

        [Test]
        public void CorruptDonorCannotModifyWinner()
        {
            var winner = Signed(Dto());
            var other = Dto(); Signed(other); other.accountGold++;
            Assert.That(CloudAchievementMerge.TryMerge(winner, JsonUtility.ToJson(other), Account, null,
                out var result, out var changed, out _), Is.False);
            Assert.That(changed, Is.False);
            Assert.That(result, Is.EqualTo(winner));
        }

        [Test]
        public void MissingCurrentAccountSkipsMerge()
        {
            var winner = Signed(Dto());
            var other = Dto(); other.achievementIds = new[] { "PENDING" };
            Assert.That(CloudAchievementMerge.TryMerge(winner, Signed(other), 0, null,
                out var result, out var changed, out var error), Is.True, error);
            Assert.That(changed, Is.False);
            Assert.That(result, Is.EqualTo(winner));
        }
    }
}
