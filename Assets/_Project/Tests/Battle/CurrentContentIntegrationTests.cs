using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Content;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using NUnit.Framework;
using UnityEditor;

namespace Grimhand.Battle.Tests
{
    /// <summary>Current production assets; no writes to the player's profile or Steam.</summary>
    public class CurrentContentIntegrationTests
    {
        static ExpeditionConfig Config(int seed = 42)
        {
            var setup = AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(
                "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset");
            Assert.NotNull(setup);
            var config = setup.ToExpeditionConfig();
            config.RunSeed = seed;
            return config;
        }

        public static IEnumerable Encounters()
        {
            foreach (var e in MonsterEncounterCatalog.Entries)
                yield return new TestCaseData(e.Id, e.MinFloor, e.IsElite).SetName("ProductionEncounter_" + e.Id);
        }

        [TestCaseSource(nameof(Encounters))]
        public void EveryEncounter_BuildsAndStartsBattle(string id, int floor, bool elite)
        {
            var config = Config();
            ExpeditionRegionRules.ApplyMapStartLayer(config, floor);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Combat, LayerNumber = floor,
                MonsterEncounterId = id, IsElite = elite
            });
            Assert.IsTrue(engine.TrySelectRoute(0), id);
            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            var battle = new BattleEngine(engine.Run.CurrentBattleConfig);
            battle.StartBattle();
            Assert.AreEqual(BattleOutcome.Ongoing, battle.State.Outcome);
            Assert.Greater(battle.State.EnemyPlan.PlayQueue.Count, 0, id);
        }

        [TestCase(CardPackIds.Common)]
        [TestCase(CardPackIds.Advanced)]
        [TestCase(CardPackIds.Master)]
        public void MetaShop_ProductionPoolsPurchaseAndCollect(string pack)
        {
            var config = Config();
            var ids = new List<string>(TalentCatalog.PlayableCharacterIds);
            for (var seed = 1; seed <= 30; seed++)
            {
                var collection = new CampCollectionState();
                var gold = 10000;
                Assert.IsTrue(MetaShopRules.TryBuyPack(ref gold, collection,
                    CampCollectionState.DefaultCapacity, config, ids, pack, new BattleRng(seed),
                    out var pending, out var message), message);
                Assert.AreEqual(10000 - MetaShopCatalog.GetPrice(pack), gold);
                Assert.AreEqual(3, pending.Choices.Count);
                foreach (var choice in pending.Choices)
                {
                    Assert.IsTrue(ids.Contains(choice.OwnerCharacterId));
                    Assert.IsTrue(PlayerCardCatalogRules.IsAllowedPlayerCardId(choice.Template.DefinitionId));
                    Assert.AreEqual(choice.OwnerCharacterId, choice.Template.OwnerCharacterId);
                }
                Assert.IsTrue(MetaShopRules.TryCollectAllCards(collection,
                    CampCollectionState.DefaultCapacity, pending, out message), message);
                Assert.AreEqual(3, collection.Count);
            }
        }

        [TestCase(ExpeditionEventIds.TrainingDummy, 2)]
        [TestCase(ExpeditionEventIds.CursedBookshelf, 0)]
        public void Events_ConfirmAftermathAndReachUsableNextScreen(string eventId, int choice)
        {
            var engine = new ExpeditionEngine(Config());
            engine.StartRun();
            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Event, EventId = eventId, LayerNumber = 1
            });
            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.IsTrue(engine.TryResolveEventChoice(choice));
            Assert.AreEqual(ExpeditionPhase.EventAftermath, engine.Run.Phase);
            var json = ExpeditionRunSaveCodec.Serialize(engine.Run, engine.RngState);
            Assert.IsTrue(ExpeditionRunSaveCodec.TryDeserialize(json, engine.Config, out var restored, out _));
            Assert.IsNull(restored.PendingDeferredReward, "A null deferred reward must stay null after JSON round-trip.");
            var resumed = new ExpeditionEngine(engine.Config);
            resumed.ResumeRun(restored, engine.RngState);
            if (eventId == ExpeditionEventIds.CursedBookshelf)
                foreach (var member in resumed.Run.Party)
                    Assert.IsTrue(ExpeditionCardPool.TryRollCardRewardForMemberWithFallback(
                        engine.Config, member, CardRarity.SuperRare, new BattleRng(42), out _),
                        "Production reward pool missing for " + member.CharacterDefinitionId);
            Assert.IsTrue(resumed.TryConfirmEventAftermath());
            for (var i = 0; i < 10 && resumed.Run.Phase == ExpeditionPhase.EventInteraction; i++)
                Assert.IsTrue(resumed.CompleteEventInteractionStep());
            Assert.IsTrue(resumed.Run.Phase == ExpeditionPhase.RouteSelect
                || resumed.Run.Phase == ExpeditionPhase.RewardPickup, resumed.Run.Phase.ToString());
            if (eventId == ExpeditionEventIds.CursedBookshelf)
            {
                Assert.NotNull(resumed.Run.PendingRewardPickup);
                var members = string.Join(",", resumed.Run.Party.ConvertAll(p => p.CharacterDefinitionId));
                Assert.IsTrue(resumed.Run.PendingRewardPickup.HasCard,
                    $"party={members}; catalog={engine.Config.PlayerCardCatalog.Count}; message={resumed.Run.LastEventMessage}");
                Assert.AreEqual(10, resumed.Run.PendingRewardPickup.GrantXp);
            }
        }

        [Test]
        public void IdleRun_RoundTripDoesNotInventPendingScreensOrRewards()
        {
            var engine = new ExpeditionEngine(Config());
            engine.StartRun();
            Assert.IsTrue(ExpeditionRunSaveCodec.TryDeserialize(
                ExpeditionRunSaveCodec.Serialize(engine.Run, engine.RngState), engine.Config,
                out var run, out _));
            Assert.IsNull(run.PendingRewardPickup);
            Assert.IsNull(run.PendingDeferredReward);
            Assert.IsNull(run.PendingEventBattleVictoryReward);
            Assert.IsNull(run.PendingEvent);
            Assert.IsNull(run.PendingEventAftermath);
            Assert.IsNull(run.EventInteraction);
            Assert.IsNull(run.PendingShrine);
            Assert.IsNull(run.CardAltar);
            Assert.IsNull(run.PendingCardOffer);
            Assert.IsNull(run.PendingCardPackOffer);
        }

        [TestCase(20)]
        [TestCase(40)]
        [TestCase(60)]
        public void BossCheckpoint_ResumePreservesEnemyAndDoesNotAdvanceRandomState(int floor)
        {
            var config = Config();
            ExpeditionRegionRules.ApplyMapStartLayer(config, floor);
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            var before = engine.Run.CurrentBattleConfig;
            var profile = new PlayerProfileState();
            ActiveRunPersistence.BeginNewRun(profile, engine, floor);
            Assert.IsTrue(ExpeditionRunSaveCodec.TryDeserialize(profile.ActiveRun.RunJson,
                config, out var run, out _));
            var resumed = new ExpeditionEngine(config);
            resumed.ResumeRun(run, profile.ActiveRun.RngState);
            Assert.AreEqual(engine.RngState, resumed.RngState);
            Assert.AreEqual(before.Combatants.Count, resumed.Run.CurrentBattleConfig.Combatants.Count);
            for (var i = 0; i < before.Combatants.Count; i++)
            {
                var expected = before.Combatants[i];
                var actual = resumed.Run.CurrentBattleConfig.Combatants[i];
                Assert.AreEqual(expected.CharacterDefinitionId, actual.CharacterDefinitionId);
                Assert.AreEqual(expected.MaxHp, actual.MaxHp);
                Assert.AreEqual(expected.StartHp, actual.StartHp);
            }
        }

        [Test]
        public void PendingRewardPack_RoundTripRetainsChoicesAndRandomState()
        {
            var engine = new ExpeditionEngine(Config());
            engine.StartRun();
            engine.Run.Phase = ExpeditionPhase.RewardPickup;
            engine.Run.ChestRewardRevealed = true;
            engine.Run.PendingRewardPickup = new ExpeditionRewardPickup { Gold = 10 };
            engine.Run.PendingRewardPickup.CardPacks.Add(new CardPackRewardEntry { PackId = CardPackIds.Common });
            Assert.IsTrue(engine.TryOpenRewardCardPack(0));
            Assert.AreEqual(3, engine.Run.PendingCardPackOffer.Choices.Count);
            var profile = new PlayerProfileState();
            ActiveRunPersistence.BeginNewRun(profile, engine, 1);
            Assert.IsTrue(ExpeditionRunSaveCodec.TryDeserialize(profile.ActiveRun.RunJson,
                engine.Config, out var restored, out _));
            var resumed = new ExpeditionEngine(engine.Config);
            resumed.ResumeRun(restored, profile.ActiveRun.RngState);
            Assert.AreEqual(engine.RngState, resumed.RngState);
            Assert.NotNull(resumed.Run.PendingCardPackOffer);
            Assert.AreEqual(3, resumed.Run.PendingCardPackOffer.Choices.Count);
            for (var i = 0; i < 3; i++)
                Assert.AreEqual(engine.Run.PendingCardPackOffer.Choices[i].Template.DefinitionId,
                    resumed.Run.PendingCardPackOffer.Choices[i].Template.DefinitionId);
        }

        static BattleEngine QueenBattle(string enemyFile = "Character_Ghost_Queen")
        {
            var config = new BattleConfig
            {
                Seed = 42, CardsDrawnPerTurn = 0, EnemyCardsDrawnPerTurn = 0,
                ManualEnemyIntentsOnly = true, SkipFloorScaling = true
            };
            foreach (var file in new[] { "Character_Knight", "Character_Mage", enemyFile })
            {
                var definition = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(
                    "Assets/_Project/Data/Characters/" + file + ".asset");
                Assert.NotNull(definition, file);
                var unit = BattleSetupSO.BuildCombatantConfigPublic(definition);
                unit.Id = definition.CharacterId;
                unit.DeckTemplates.Clear();
                unit.SkillPoolCandidates.Clear();
                unit.Slot = unit.Team == TeamSide.Enemy ? FormationSlot.Front
                    : definition.CharacterId == "char_knight" ? FormationSlot.Front : FormationSlot.Middle;
                config.Combatants.Add(unit);
            }
            var engine = new BattleEngine(config);
            engine.StartBattle();
            return engine;
        }

        [TestCase("m_queen_curse")]
        [TestCase("m_queen_soul_drain")]
        [TestCase("m_queen_deterrence")]
        public void QueenStatusCards_ResolveUsingRealAssets(string id)
        {
            var engine = QueenBattle();
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_" + id + ".asset");
            Assert.NotNull(card);
            Assert.NotNull(engine.EnqueueEnemyIntentCard(card.ToTemplate()));
            Assert.IsTrue(engine.SkipPlayerTurn());
            var found = false;
            foreach (var unit in engine.State.Combatants)
            {
                if (unit.Team != TeamSide.Player) continue;
                if (id == "m_queen_curse") Assert.AreEqual(6, StatusRules.GetStatusStacks(unit, StatusCatalog.Poison));
                if (id == "m_queen_deterrence") found |= StatusRules.HasStatus(unit, StatusCatalog.Deterrence);
                if (id == "m_queen_soul_drain") found |= StatusRules.HasStatus(unit, StatusCatalog.SoulDrain);
            }
            if (id != "m_queen_curse") Assert.IsTrue(found, id);
            if (id == "m_queen_soul_drain") Assert.AreEqual(2, engine.State.PendingPlayerEnergyRegenPenaltyNextTurn);
        }

        [Test]
        public void SpiderFatalBind_RealCardDamagesChosenTargetAndPaysHalfCurrentHp()
        {
            var engine = QueenBattle("Character_Spider_Lady");
            var spider = engine.State.GetCombatant("char_spider_lady");
            var mage = engine.State.GetCombatant("char_mage");
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_m_spider_fatal_bind.asset");
            Assert.NotNull(card);
            Assert.NotNull(spider);
            var hp = spider.Hp;
            var targetHp = mage.Hp;
            var queued = engine.EnqueueEnemyIntentCard(card.ToTemplate());
            Assert.NotNull(queued);
            engine.State.ResolutionTargets[queued.InstanceId] = mage.Id;
            Assert.IsTrue(engine.SkipPlayerTurn());
            Assert.Less(mage.Hp, targetHp);
            Assert.AreEqual(hp - hp / 2, spider.Hp);
        }

        [Test]
        public void QueenCommand_UnmatchedArmProtectsNextTurnOnlyOnce()
        {
            var engine = QueenBattle();
            var queen = engine.State.GetCombatant("char_ghost_queen");
            var hp = queen.Hp;
            var command = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_m_queen_command.asset");
            Assert.NotNull(engine.EnqueueEnemyIntentCard(command.ToTemplate()));
            Assert.IsTrue(engine.SkipPlayerTurn());
            engine.FlushPendingEndOfTurn();
            var attack = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_w_basic_slash.asset");
            var hit = engine.AddCardTemplateToHand(attack.ToTemplate());
            Assert.IsTrue(engine.Draft.TrySelectCard(hit.InstanceId));
            Assert.IsTrue(engine.Draft.TryAssignTargetAndSelect(queen.Id));
            Assert.IsTrue(engine.CommitPlayerPlan());
            Assert.AreEqual(hp, queen.Hp);
            foreach (var arm in engine.State.DefenderRespondArms)
                Assert.IsTrue(arm.Consumed);
        }

        [Test]
        public void ManualTarget_RejectsOutOfReachBeforeCommittingRealCard()
        {
            var expedition = new ExpeditionEngine(Config());
            expedition.StartRun();
            expedition.Run.PendingRoutes.Clear();
            expedition.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Combat, LayerNumber = 1,
                MonsterEncounterId = MonsterEncounterCatalog.GoblinTriple
            });
            Assert.IsTrue(expedition.TrySelectRoute(0));
            var config = expedition.Run.CurrentBattleConfig;
            config.CardsDrawnPerTurn = 0;
            config.EnemyCardsDrawnPerTurn = 0;
            config.ManualEnemyIntentsOnly = true;
            config.SkipFloorScaling = true;
            var engine = new BattleEngine(config);
            engine.StartBattle();
            var enemies = PositionRules.GetAliveSortedByPhysicalSlot(engine.State, TeamSide.Enemy);
            Assert.AreEqual(3, enemies.Count);
            var card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_w_basic_slash.asset");
            var hit = engine.AddCardTemplateToHand(card.ToTemplate());
            Assert.IsTrue(engine.Draft.TrySelectCard(hit.InstanceId));
            Assert.IsFalse(engine.CommitPlayerPlan(), "An unassigned attack must not be committed.");
            Assert.IsFalse(engine.Draft.TryAssignTargetAndSelect(enemies[2].Id));
            Assert.AreEqual(hit.InstanceId, engine.Draft.AwaitingTargetCardId);
            Assert.IsTrue(engine.Draft.TryAssignTargetAndSelect(enemies[0].Id));
            Assert.IsTrue(engine.CommitPlayerPlan());
        }

        [Test]
        public void QueenCommand_RealAttackTargetingRedirectsDamage()
        {
            var engine = QueenBattle();
            var queen = engine.State.GetCombatant("char_ghost_queen");
            var hp = queen.Hp;
            var command = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_m_queen_command.asset");
            var attack = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(
                "Assets/_Project/Data/Cards/Card_w_basic_slash.asset");
            Assert.NotNull(command);
            Assert.NotNull(attack);
            Assert.NotNull(engine.EnqueueEnemyIntentCard(command.ToTemplate()));
            var hit = engine.AddCardTemplateToHand(attack.ToTemplate());
            Assert.NotNull(hit);
            Assert.IsTrue(engine.Draft.TrySelectCard(hit.InstanceId));
            Assert.AreEqual(hit.InstanceId, engine.Draft.AwaitingTargetCardId);
            Assert.IsTrue(engine.Draft.TryAssignTargetAndSelect(queen.Id));
            Assert.IsTrue(engine.CommitPlayerPlan());
            Assert.AreEqual(hp, queen.Hp);
            var redirected = false;
            foreach (var unit in engine.State.Combatants)
                if (unit.Team == TeamSide.Player && unit.Hp < unit.MaxHp) redirected = true;
            Assert.IsTrue(redirected);
            foreach (var arm in engine.State.DefenderRespondArms)
                Assert.IsTrue(arm.Consumed, "A paired command must not arm again after consuming its one response.");
        }
    }
}
