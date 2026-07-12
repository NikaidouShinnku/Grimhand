using System;
using System.IO;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class PlayerSaveTests
    {
        SaveValidationContext _context;
        string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GrimhandSaveTests", Guid.NewGuid().ToString("N"));
            _context = BuildPermissiveContext();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }

        [Test]
        public void RoundTrip_PreservesMetaRosterAndCollection()
        {
            var storage = new LocalFileSaveStorage(_tempDir);
            var service = new SaveService(storage, _context);
            var profile = CreateSampleProfile();

            Assert.IsTrue(service.TrySave(profile, out var saveError), saveError);

            var loaded = service.LoadOrCreate(() => throw new InvalidOperationException("不应创建新档"));
            Assert.AreEqual(SaveLoadSource.Primary, loaded.Source);
            Assert.AreEqual(42, loaded.Profile.AccountGold);
            Assert.AreEqual(30, loaded.Profile.CollectionCapacity);
            Assert.AreEqual(2, loaded.Profile.Collection.Count);
            Assert.AreEqual(1, loaded.Profile.Meta.GetOrCreate(TalentCatalog.KnightId).OutOfRunLevel);
            Assert.AreEqual("card_a", loaded.Profile.Roster.Members[0].DeckCardIds[0]);
        }

        [Test]
        public void TamperedPrimary_FallsBackToBackup()
        {
            var storage = new LocalFileSaveStorage(_tempDir);
            var service = new SaveService(storage, _context);
            var profile = CreateSampleProfile();
            Assert.IsTrue(service.TrySave(profile, out _));

            var tampered = File.ReadAllText(storage.PrimaryPath).Replace("\"accountGold\": 42", "\"accountGold\": 999999");
            File.WriteAllText(storage.PrimaryPath, tampered);

            var loaded = service.LoadOrCreate(CreateSampleProfile);
            Assert.AreEqual(SaveLoadSource.Backup, loaded.Source);
            Assert.AreEqual(42, loaded.Profile.AccountGold);
        }

        [Test]
        public void CampCollectionRules_BlocksExpeditionWhenOverCapacity()
        {
            var collection = new CampCollectionState();
            collection.TryAddEntry("card_a");
            collection.TryAddEntry("card_b");
            collection.TryAddEntry("card_c");

            Assert.IsTrue(CampCollectionRules.BlocksExpeditionStart(collection, capacity: 2));
            Assert.IsFalse(CampCollectionRules.BlocksExpeditionStart(collection, capacity: 3));
            Assert.IsTrue(CampCollectionRules.BlocksShopCardPack(collection, capacity: 2));
        }

        [Test]
        public void RoundTrip_PreservesActiveRunSnapshot()
        {
            var storage = new LocalFileSaveStorage(_tempDir);
            var service = new SaveService(storage, _context);
            var profile = CreateSampleProfile();
            profile.ActiveRun = new ActiveRunSnapshot
            {
                MapStartLayer = 21,
                RunSeed = 12345,
                RngState = 99,
                MetaGoldSyncedRunGold = 10,
                RunJson = "{\"version\":1,\"phase\":1,\"battlesWon\":0}"
            };

            Assert.IsTrue(service.TrySave(profile, out var saveError), saveError);

            var loaded = service.LoadOrCreate(() => throw new InvalidOperationException("不应创建新档"));
            Assert.IsTrue(loaded.Profile.HasActiveRun);
            Assert.AreEqual(21, loaded.Profile.ActiveRun.MapStartLayer);
            Assert.AreEqual(12345, loaded.Profile.ActiveRun.RunSeed);
            Assert.AreEqual(99ul, loaded.Profile.ActiveRun.RngState);
            Assert.AreEqual(10, loaded.Profile.ActiveRun.MetaGoldSyncedRunGold);
        }

        [Test]
        public void MetaEconomySync_AddsOnlyRunGoldGainsToAccountGold()
        {
            var profile = CreateSampleProfile();
            profile.AccountGold = 100;
            profile.ActiveRun = new ActiveRunSnapshot { MetaGoldSyncedRunGold = 5 };
            var run = new ExpeditionRunState { Gold = 12 };

            MetaEconomySync.SyncMetaGoldFromRun(profile, run);

            Assert.AreEqual(107, profile.AccountGold);
            Assert.AreEqual(12, profile.ActiveRun.MetaGoldSyncedRunGold);

            run.Gold = 8;
            MetaEconomySync.SyncMetaGoldFromRun(profile, run);
            Assert.AreEqual(107, profile.AccountGold);
        }

        [Test]
        public void RunSettlementRules_GrantsNodesCompletedTimesFiveXp()
        {
            var meta = CampMetaState.CreateNewProfile();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = TalentCatalog.KnightId });
            run.Map = new ExpeditionMapState { NodesCompleted = 4 };

            RunSettlementRules.ApplyRunEndMetaRewards(run, meta);

            Assert.AreEqual(20, meta.GetOrCreate(TalentCatalog.KnightId).OutOfRunXp);
        }

        [Test]
        public void CreateNewProfile_StartsAtLevelOne()
        {
            var meta = CampMetaState.CreateNewProfile();
            foreach (var characterId in TalentCatalog.PlayableCharacterIds)
                Assert.AreEqual(1, meta.GetOrCreate(characterId).OutOfRunLevel);
        }

        [Test]
        public void RunSettlementRules_AutoLevelsWhenEnoughXp()
        {
            var meta = CampMetaState.CreateNewProfile();
            var progress = meta.GetOrCreate(TalentCatalog.KnightId);
            progress.OutOfRunXp = 90;
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = TalentCatalog.KnightId });
            run.Map = new ExpeditionMapState { NodesCompleted = 2 };

            RunSettlementRules.ApplyRunEndMetaRewards(run, meta);

            Assert.AreEqual(2, progress.OutOfRunLevel);
            Assert.AreEqual(0, progress.OutOfRunXp);
        }

        static PlayerProfileState CreateSampleProfile()
        {
            var profile = new PlayerProfileState
            {
                AccountGold = 42,
                CollectionCapacity = CampCollectionState.DefaultCapacity
            };
            profile.Collection.TryAddEntry("card_a");
            profile.Collection.TryAddEntry("card_b");

            profile.Meta.GetOrCreate(TalentCatalog.KnightId).OutOfRunLevel = 1;
            profile.Meta.GetOrCreate(TalentCatalog.KnightId).SelectedSlot1TalentId = "talent_knight_s1_lv1";

            var member = new CampMemberLoadout
            {
                CharacterDefinitionId = TalentCatalog.KnightId,
                DisplayName = "Knight"
            };
            member.DeckCardIds.Add("card_a");
            while (member.DeckCardIds.Count < CampRosterState.DeckSize)
                member.DeckCardIds.Add("");

            profile.Roster.Members.Add(member);
            while (profile.Roster.Members.Count < CampRosterState.PartySize)
                profile.Roster.Members.Add(new CampMemberLoadout());

            return profile;
        }

        static SaveValidationContext BuildPermissiveContext()
        {
            var context = new SaveValidationContext();
            context.ValidCardIds.Add("card_a");
            context.ValidCardIds.Add("card_b");
            context.ValidCardIds.Add("card_c");
            context.CardOwnerById["card_a"] = TalentCatalog.KnightId;
            context.CardOwnerById["card_b"] = TalentCatalog.KnightId;
            context.CardOwnerById["card_c"] = TalentCatalog.KnightId;

            foreach (var talent in TalentCatalog.GetAll())
            {
                if (!string.IsNullOrEmpty(talent.Id))
                    context.ValidTalentIds.Add(talent.Id);
            }

            return context;
        }
    }
}
