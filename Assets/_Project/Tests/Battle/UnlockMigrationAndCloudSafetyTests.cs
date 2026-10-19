using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using Grimhand.Presentation.Camp;
using NUnit.Framework;
using UnityEngine;

namespace Grimhand.Battle.Tests
{
    public class UnlockMigrationAndCloudSafetyTests
    {
        static PlayerProfileSaveData Profile(int version=7)
        {
            var p=new PlayerProfileState{Meta=CampMetaState.CreateNewProfile(),AccountGold=1234};
            var dto=SaveDataMapper.ToDto(p);dto.saveVersion=version;dto.lastSavedUtc="2026-10-18T10:00:00.0000000Z";
            SaveIntegrity.ApplyHash(dto);return dto;
        }
        [TestCase(5)][TestCase(6)]
        public void HistoricalSignatureAndMigrationPreserveNonUnlockData(int version)
        {
            var dto=Profile(version);
            dto.integrityHash="";
            var signed=JsonUtility.ToJson(dto);
            signed=signed.Substring(0,signed.LastIndexOf(",\"achievementIds\":",StringComparison.Ordinal))+"}";
            signed=signed.Replace(",\"lichUnlocked\":false}","}");
            if(version<6)signed=signed.Replace(",\"berserkerUnlocked\":false}","}");
            using(var hmac=new HMACSHA256(Encoding.UTF8.GetBytes("Grimhand.Save.v1.20260710")))
                dto.integrityHash=Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(signed)));
            Assert.IsTrue(SaveIntegrity.Verify(dto));
            var restored=SaveDataMapper.FromDto(dto);
            Assert.IsTrue(restored.Meta.BerserkerUnlocked);Assert.IsTrue(restored.Meta.LichUnlocked);
            var roundTrip=SaveDataMapper.ToDto(restored);
            roundTrip.saveVersion=dto.saveVersion;roundTrip.lastSavedUtc=dto.lastSavedUtc;roundTrip.integrityHash=dto.integrityHash;
            roundTrip.berserkerUnlocked=dto.berserkerUnlocked;roundTrip.lichUnlocked=dto.lichUnlocked;
            Assert.AreEqual(JsonUtility.ToJson(dto),JsonUtility.ToJson(roundTrip));
        }
        [TestCase("char_skeleton_king",true,false)]
        [TestCase("char_ghost_queen",false,true)]
        [TestCase("char_warden",false,false)]
        [TestCase(null,false,false)]
        public void OnlyTheSpecifiedBossUnlocksTheCorrespondingCharacter(string boss,bool berserker,bool lich)
        {
            var meta=CampMetaState.CreateNewProfile();
            CharacterUnlockRules.RecordVictory(meta,20,true,false,false,boss);
            Assert.AreEqual(berserker,meta.BerserkerUnlocked);Assert.AreEqual(lich,meta.LichUnlocked);
        }
        [Test] public void NewProfilesStayLockedAcrossRoundTripAndBothFlagsAreSigned()
        {
            var dto=Profile();var restored=SaveDataMapper.FromDto(dto);
            Assert.IsFalse(restored.Meta.LichUnlocked);Assert.IsFalse(restored.Meta.BerserkerUnlocked);
            dto.lichUnlocked=true;Assert.IsFalse(SaveIntegrity.Verify(dto));
        }
        [Test] public void OldFiveCharacterProfileGainsUsableNewCharacterWithoutResettingExistingProgress()
        {
            var dto=Profile(5);dto.characters=dto.characters.Where(c=>c.characterDefinitionId!=TalentCatalog.BerserkerId).ToArray();
            var lich=dto.characters.Single(c=>c.characterDefinitionId==TalentCatalog.LichQueenId);
            lich.outOfRunLevel=10;lich.outOfRunXp=0;lich.selectedSlot1TalentId="talent_lich_s1_lv4";
            SaveIntegrity.ApplyHash(dto);
            var profile=SaveDataMapper.FromDto(dto);
            Assert.IsTrue(CodexProgressRules.HasOwnedCharacter(profile,TalentCatalog.BerserkerId));
            Assert.IsTrue(CodexProgressRules.HasOwnedCharacter(profile,TalentCatalog.LichQueenId));
            Assert.AreEqual(10,profile.Meta.GetOrCreate(TalentCatalog.LichQueenId).OutOfRunLevel);
            Assert.AreEqual("talent_lich_s1_lv4",profile.Meta.GetOrCreate(TalentCatalog.LichQueenId).SelectedSlot1TalentId);
            Assert.AreEqual(dto.accountGold,profile.AccountGold);
        }
        [Test] public void CloudCannotOverwriteValidDataWithCorruptFutureOrOlderPayload()
        {
            var dto=Profile();var local=JsonUtility.ToJson(dto);
            Assert.IsTrue(CloudProfileSafety.CanUpload(local,null,null,out _));
            dto.lastSavedUtc="2026-10-19T10:00:00.0000000Z";SaveIntegrity.ApplyHash(dto);var newer=JsonUtility.ToJson(dto);
            Assert.IsFalse(CloudProfileSafety.CanUpload(local,newer,null,out _));
            Assert.IsTrue(CloudProfileSafety.CanUpload(newer,local,null,out _));
            Assert.IsFalse(CloudProfileSafety.CanUpload(newer,"{broken",null,out _));
            dto.saveVersion=PlayerProfileState.CurrentSaveVersion+1;SaveIntegrity.ApplyHash(dto);
            Assert.IsFalse(CloudProfileSafety.TryValidate(JsonUtility.ToJson(dto),null,out _,out _));
            dto=Profile();dto.accountGold++;Assert.IsFalse(CloudProfileSafety.TryValidate(JsonUtility.ToJson(dto),null,out _,out _));
        }
        [Test] public void ActiveExpeditionAndCollectionSurviveOldProfileMigration()
        {
            var battle=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.BattleSetupSO>("Assets/_Project/Data/Setups/BattleSetup_Demo.asset");
            var setup=UnityEditor.AssetDatabase.LoadAssetAtPath<Grimhand.Content.ExpeditionSetupSO>("Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            var profile=PlayerProfileFactory.CreateNew(battle,setup);profile.AccountGold=736;
            profile.Collection.TryAddEntry("w_basic_slash",true);
            var engine=new ExpeditionEngine(setup.ToExpeditionConfig()){MetaProfile=profile};
            engine.StartRun(profile.Roster,profile.Meta);ActiveRunPersistence.BeginNewRun(profile,engine,1);
            var before=SaveDataMapper.ToDto(profile);before.saveVersion=6;SaveIntegrity.ApplyHash(before);
            Assert.IsTrue(CloudProfileSafety.TryValidate(JsonUtility.ToJson(before),SaveValidationContextBuilder.Build(setup),out _,out var error),error);
            var restored=SaveDataMapper.FromDto(before);var after=SaveDataMapper.ToDto(restored);
            Assert.IsTrue(restored.HasActiveRun);Assert.AreEqual(before.activeRunJson,after.activeRunJson);
            Assert.AreEqual(before.activeRunRngState,after.activeRunRngState);
            CollectionAssert.AreEqual(before.collectionEntries,after.collectionEntries);
            CollectionAssert.AreEqual(before.collectionEngravedFlags,after.collectionEngravedFlags);
            Assert.AreEqual(before.accountGold,after.accountGold);
            Assert.AreEqual(JsonUtility.ToJson(before.rosterMembers[0]),JsonUtility.ToJson(after.rosterMembers[0]));
        }
        [Test] public void MigrationKeepsExactHistoricalFileInAnIndependentBackup()
        {
            var path=Path.Combine(Path.GetTempPath(),"Grimhand-unlock-test-"+Guid.NewGuid().ToString("N"));
            try
            {
                var storage=new LocalFileSaveStorage(path);var old=JsonUtility.ToJson(Profile(6));
                Assert.IsTrue(storage.TryWrite(old,out var error),error);
                var updated=SaveDataMapper.ToDto(SaveDataMapper.FromDto(Profile(6)));SaveIntegrity.ApplyHash(updated);
                Assert.IsTrue(storage.TryWrite(JsonUtility.ToJson(updated),out error),error);
                var backup=Path.Combine(path,"migration_backups","profile_v6_before_unlock_update.sav");
                Assert.AreEqual(old,File.ReadAllText(backup));
                updated.accountGold++;SaveIntegrity.ApplyHash(updated);Assert.IsTrue(storage.TryWrite(JsonUtility.ToJson(updated),out error),error);
                Assert.AreEqual(old,File.ReadAllText(backup));Assert.IsTrue(storage.TryReadPrimary(out var primary));
                Assert.IsTrue(CloudProfileSafety.TryValidate(primary,null,out _,out _));
            }
            finally{if(Directory.Exists(path))Directory.Delete(path,true);}
        }
    }
}
