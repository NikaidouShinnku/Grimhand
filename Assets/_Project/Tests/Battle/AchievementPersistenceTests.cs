using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using Grimhand.Persistence;
using NUnit.Framework;
using UnityEngine;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Battle.Tests
{
    public class AchievementPersistenceTests
    {
        // Reconstruct the historical wire payload independently of SaveIntegrity.
        static string HistoricalJson(PlayerProfileSaveData dto)
        {
            dto.integrityHash = "";
            var json = JsonUtility.ToJson(dto);
            json = json.Substring(0, json.LastIndexOf(",\"achievementIds\":", StringComparison.Ordinal)) + "}";
            if (dto.saveVersion < 7)
                json = json.Replace(",\"lichUnlocked\":false}", "}");
            if (dto.saveVersion < 6)
                json = json.Replace(",\"berserkerUnlocked\":false}", "}");
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes("Grimhand.Save.v1.20260710"));
            var hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(json)));
            return json.Replace("\"integrityHash\":\"\"", "\"integrityHash\":\"" + hash + "\"");
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void LegacyUnsignedAchievementExtensionsCannotGrantProgressOrOwnership(int version)
        {
            var dto=SaveDataMapper.ToDto(new PlayerProfileState());
            dto.saveVersion=version;
            dto.collectionEntries=new[] { "signed-engraving", "signed-unengraved" };
            dto.collectionEngravedFlags=new[] { true, false };
            var historical=HistoricalJson(dto);
            var injected=historical.Substring(0,historical.Length-1)
                +",\"achievementIds\":[\"ACH_INJECTED\",\"ACH_FIVE_ENGRAVINGS\"],"
                +"\"completedEngravings\":999,\"achievementSteamAccountId\":\"76561198012345678\"}";
            var parsed=JsonUtility.FromJson<PlayerProfileSaveData>(injected);
            Assert.IsTrue(SaveIntegrity.Verify(parsed),"Extensions must not change the historical signature.");
            var context=new SaveValidationContext();
            context.ValidCardIds.Add("signed-engraving");
            context.ValidCardIds.Add("signed-unengraved");
            Assert.IsTrue(SaveValidator.TryValidate(parsed,context,out var error),error);
            var restored=SaveDataMapper.FromDto(parsed);
            Assert.IsEmpty(restored.Achievements.Unlocked);
            Assert.IsEmpty(restored.Achievements.SteamAccountId);
            Assert.AreEqual(1,restored.Achievements.CompletedEngravings);
            AchievementRules.ReconcilePermanent(restored.Achievements,restored.Meta,restored.Collection);
            Assert.IsEmpty(restored.Achievements.Unlocked);
            var upgraded=SaveDataMapper.ToDto(restored);
            Assert.IsEmpty(upgraded.achievementIds);
            Assert.IsEmpty(upgraded.achievementSteamAccountId);
            Assert.AreEqual(1,upgraded.completedEngravings);
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void HistoricalSignatureMigratesWithoutLosingActiveRun(int version)
        {
            var dto = SaveDataMapper.ToDto(new PlayerProfileState { AccountGold = 736 });
            dto.saveVersion = version;
            dto.hasActiveRun = true;
            dto.activeRunVersion = 1;
            dto.activeRunMapStartLayer = 4;
            dto.activeRunSeed = 987;
            dto.activeRunRngState = "18446744073709551614";
            dto.activeRunMetaGoldSynced = 123;
            dto.activeRunJson = "{\"checkpoint\":\"unchanged\"}";
            var json = HistoricalJson(dto);
            var historical = JsonUtility.FromJson<PlayerProfileSaveData>(json);
            Assert.IsTrue(SaveIntegrity.Verify(historical));
            Assert.IsTrue(CloudProfileSafety.TryValidate(json, new SaveValidationContext(), out _, out var error), error);
            var restored = SaveDataMapper.FromDto(historical);
            Assert.IsTrue(restored.HasActiveRun);
            Assert.IsEmpty(restored.Achievements.Unlocked);
            Assert.AreEqual(0, restored.Achievements.CompletedEngravings);
            Assert.AreEqual(version < 7, restored.Meta.BerserkerUnlocked);
            Assert.AreEqual(version < 7, restored.Meta.LichUnlocked);
            var upgraded = SaveDataMapper.ToDto(restored);
            Assert.AreEqual(8, upgraded.saveVersion);
            Assert.AreEqual(dto.accountGold, upgraded.accountGold);
            Assert.AreEqual(dto.activeRunVersion, upgraded.activeRunVersion);
            Assert.AreEqual(dto.activeRunMapStartLayer, upgraded.activeRunMapStartLayer);
            Assert.AreEqual(dto.activeRunSeed, upgraded.activeRunSeed);
            Assert.AreEqual(dto.activeRunRngState, upgraded.activeRunRngState);
            Assert.AreEqual(dto.activeRunMetaGoldSynced, upgraded.activeRunMetaGoldSynced);
            Assert.AreEqual(dto.activeRunJson, upgraded.activeRunJson);
            SaveIntegrity.ApplyHash(upgraded);
            Assert.IsTrue(SaveIntegrity.Verify(JsonUtility.FromJson<PlayerProfileSaveData>(JsonUtility.ToJson(upgraded))));
        }

        [Test]
        public void ArbitraryAchievementIdsAndCounterSurviveSignedJsonRoundTrip()
        {
            var profile = new PlayerProfileState();
            var ids = new[] { "ACH_FUTURE_UNKNOWN", "future/custom-id", "ACH_\"escaped\\雪" };
            foreach (var id in ids) profile.Achievements.Unlocked.Add(id);
            profile.Achievements.CompletedEngravings = 57;
            profile.Achievements.SteamAccountId = "76561198012345678";
            var dto = SaveDataMapper.ToDto(profile);
            SaveIntegrity.ApplyHash(dto);
            var parsed = JsonUtility.FromJson<PlayerProfileSaveData>(JsonUtility.ToJson(dto));
            Assert.IsTrue(SaveIntegrity.Verify(parsed));
            Assert.IsTrue(SaveValidator.TryValidate(parsed, new SaveValidationContext(), out var error), error);
            var restored = SaveDataMapper.FromDto(parsed);
            CollectionAssert.AreEquivalent(ids, restored.Achievements.Unlocked);
            Assert.AreEqual(57, restored.Achievements.CompletedEngravings);
            Assert.AreEqual(profile.Achievements.SteamAccountId, restored.Achievements.SteamAccountId);
            Assert.IsFalse(restored.Meta.BerserkerUnlocked);
            Assert.IsFalse(restored.Meta.LichUnlocked);
            parsed.completedEngravings++;
            Assert.IsFalse(SaveIntegrity.Verify(parsed));
            parsed.completedEngravings--;
            parsed.achievementSteamAccountId += "changed";
            Assert.IsFalse(SaveIntegrity.Verify(parsed));
            parsed.achievementSteamAccountId = profile.Achievements.SteamAccountId;
            parsed.achievementIds[0] += "changed";
            Assert.IsFalse(SaveIntegrity.Verify(parsed));
        }

        [Test]
        public void MissingAchievementStateAndNullIdsAreSafe()
        {
            var dto = SaveDataMapper.ToDto(new PlayerProfileState { Achievements = null });
            Assert.IsEmpty(dto.achievementIds);
            Assert.AreEqual(0, dto.completedEngravings);
            dto.achievementIds = new[] { null, "", "ACH_UNKNOWN", "ACH_UNKNOWN" };
            var restored = SaveDataMapper.FromDto(dto);
            CollectionAssert.AreEquivalent(new[] { "ACH_UNKNOWN" }, restored.Achievements.Unlocked);
            dto.achievementIds = null;
            Assert.IsEmpty(SaveDataMapper.FromDto(dto).Achievements.Unlocked);
        }

        [Test]
        public void RunAchievementFactsAndEngravingGuardSurviveSaveAndClone()
        {
            var run = new ExpeditionRunState
            {
                WasAbandoned = true, AchievementsDisabled = true,
                AchievementRelicEvolved = true, AchievementBattleStartConsumableCount = 3,
                IsTrainingGround = true
            };
            run.EngravedDeckInstanceIds.Add("completed-instance");
            Assert.IsTrue(ExpeditionRunSaveCodec.TryDeserialize(ExpeditionRunSaveCodec.Serialize(run, 1),
                new ExpeditionConfig(), out var restored, out _));
            var clone = ExpeditionRunStateCopy.Clone(restored);
            Assert.IsTrue(clone.WasAbandoned && clone.AchievementsDisabled && clone.AchievementRelicEvolved && clone.IsTrainingGround);
            Assert.AreEqual(3, clone.AchievementBattleStartConsumableCount);
            Assert.IsTrue(clone.EngravedDeckInstanceIds.Contains("completed-instance"));
            var profile = new PlayerProfileState();
            Assert.IsFalse(CardEngravingRules.TryCompleteEngraveToCollection(clone, profile, "card", "completed-instance", out _));
            Assert.AreEqual(0, profile.Achievements.CompletedEngravings);
        }

        [TestCase("not-an-account")]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("18446744073709551616")]
        public void V8RejectsInvalidSteamAccountIds(string account)
        {
            var dto=SaveDataMapper.ToDto(new PlayerProfileState());
            dto.achievementSteamAccountId=account;
            Assert.IsFalse(SaveValidator.TryValidate(dto,new SaveValidationContext(),out _));
        }

        [Test]
        public void V8ValidatesBoundsWithoutCatalogFiltering()
        {
            var dto=SaveDataMapper.ToDto(new PlayerProfileState());
            dto.completedEngravings=-1;
            Assert.IsFalse(SaveValidator.TryValidate(dto,new SaveValidationContext(),out _));
            dto.completedEngravings=int.MaxValue;
            dto.achievementIds=new[] { "future/catalog-independent/雪",new string('x',256) };
            Assert.IsTrue(SaveValidator.TryValidate(dto,new SaveValidationContext(),out var error),error);
            dto.achievementIds=new[] { new string('x',257) };
            Assert.IsFalse(SaveValidator.TryValidate(dto,new SaveValidationContext(),out _));
            dto.achievementIds=new string[4097];
            Assert.IsFalse(SaveValidator.TryValidate(dto,new SaveValidationContext(),out _));
            dto.achievementIds=new[] { "bad\nidentifier" };
            Assert.IsFalse(SaveValidator.TryValidate(dto,new SaveValidationContext(),out _));
        }

        [Test]
        public void V7MigrationBackupPreservesExactBytesAcrossFurtherSaves()
        {
            var path=Path.Combine(Path.GetTempPath(),"Grimhand-achievement-migration-"+Guid.NewGuid().ToString("N"));
            try
            {
                var storage=new LocalFileSaveStorage(path);
                var old=SaveDataMapper.ToDto(new PlayerProfileState { AccountGold=321 });
                old.saveVersion=7;
                old.hasActiveRun=true;old.activeRunJson="{\"checkpoint\":\"雪\"}";
                var historical=HistoricalJson(old);
                Assert.IsTrue(storage.TryWrite(historical,out var error),error);
                var exactBytes=File.ReadAllBytes(storage.PrimaryPath);
                var migrated=SaveDataMapper.ToDto(SaveDataMapper.FromDto(JsonUtility.FromJson<PlayerProfileSaveData>(historical)));
                SaveIntegrity.ApplyHash(migrated);
                Assert.IsTrue(storage.TryWrite(JsonUtility.ToJson(migrated),out error),error);
                var backup=Path.Combine(path,"migration_backups","profile_v7_before_achievement_update.sav");
                CollectionAssert.AreEqual(exactBytes,File.ReadAllBytes(backup));
                migrated.accountGold++;
                SaveIntegrity.ApplyHash(migrated);
                Assert.IsTrue(storage.TryWrite(JsonUtility.ToJson(migrated),out error),error);
                CollectionAssert.AreEqual(exactBytes,File.ReadAllBytes(backup));
                Assert.IsTrue(storage.TryReadPrimary(out var current));
                Assert.AreEqual(old.activeRunJson,JsonUtility.FromJson<PlayerProfileSaveData>(current).activeRunJson);
            }
            finally { if(Directory.Exists(path))Directory.Delete(path,true); }
        }

        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        public void LegacyEngravedEntriesProveMinimumCountWithoutRecountingV8(int version)
        {
            var dto=SaveDataMapper.ToDto(new PlayerProfileState());
            dto.saveVersion=version;
            dto.collectionEntries=new[] { "a","a","b","c","d",null,"" };
            dto.collectionEngravedFlags=new[] { true,true,true,true,true,true,true,true };
            var restored=SaveDataMapper.FromDto(dto);
            Assert.AreEqual(5,restored.Collection.Count);
            Assert.AreEqual(5,restored.Achievements.CompletedEngravings);
            AchievementRules.ReconcilePermanent(restored.Achievements,restored.Meta,restored.Collection);
            Assert.IsTrue(restored.Achievements.Unlocked.Contains("ACH_FIVE_ENGRAVINGS"));
            var upgraded=SaveDataMapper.ToDto(restored);
            upgraded.completedEngravings=9;
            restored=SaveDataMapper.FromDto(upgraded);
            AchievementRules.ReconcilePermanent(restored.Achievements,restored.Meta,restored.Collection);
            Assert.AreEqual(9,SaveDataMapper.FromDto(SaveDataMapper.ToDto(restored)).Achievements.CompletedEngravings);
            upgraded.completedEngravings=0;
            Assert.AreEqual(0,SaveDataMapper.FromDto(upgraded).Achievements.CompletedEngravings);
        }

        [Test]
        public void LegacyUnengravedOrMissingFlagsCannotProveCompletions()
        {
            var dto=SaveDataMapper.ToDto(new PlayerProfileState());
            dto.saveVersion=7;
            dto.collectionEntries=new[] { "a","b","c","d","e" };
            dto.collectionEngravedFlags=new bool[5];
            Assert.AreEqual(0,SaveDataMapper.FromDto(dto).Achievements.CompletedEngravings);
            dto.collectionEngravedFlags=null;
            Assert.AreEqual(0,SaveDataMapper.FromDto(dto).Achievements.CompletedEngravings);
        }
    }
}
