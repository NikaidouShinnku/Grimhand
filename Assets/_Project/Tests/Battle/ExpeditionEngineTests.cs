using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionEngineTests
    {
        [Test]
        public void StartRun_OpensRouteSelectWithGeneratedMap()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.NotNull(engine.Run.Map);
            Assert.Greater(engine.Run.PendingRoutes.Count, 0);
        }

        [Test]
        public void SkipVictoryOptionalRewards_AdvancesToRouteSelectWithoutTakingCard()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            engine.OnBattleFinished(BuildVictoryState());

            Assert.IsTrue(engine.TryClaimRewardGold());
            var hadCard = engine.Run.PendingRewardPickup?.HasCard == true;
            var bonusBefore = engine.Run.Party[0].BonusCards.Count;

            if (engine.Run.Phase == ExpeditionPhase.RewardPickup)
                Assert.IsTrue(engine.TrySkipVictoryOptionalRewards());

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);

            if (hadCard)
                Assert.AreEqual(bonusBefore, engine.Run.Party[0].BonusCards.Count);
        }

        [Test]
        public void SkipRewardGold_DoesNotAddGold()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            engine.OnBattleFinished(BuildVictoryState());

            var goldAmount = engine.Run.PendingRewardPickup?.Gold ?? 0;
            Assert.Greater(goldAmount, 0);
            Assert.IsTrue(engine.TrySkipRewardGold());
            Assert.AreEqual(0, engine.Run.Gold);

            if (engine.Run.PendingRewardPickup?.HasRelic == true)
                engine.TrySkipRewardRelic();
            if (engine.Run.PendingRewardPickup?.HasCard == true)
                engine.TrySkipRewardCard();

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
        }

        [Test]
        public void VictoryAfterFirstBattle_OpensRewardPickupThenRouteSelect()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Hp = 25,
                MaxHp = 40
            });

            engine.OnBattleFinished(state);

            Assert.AreEqual(ExpeditionPhase.RewardPickup, engine.Run.Phase);
            Assert.AreEqual(1, engine.Run.BattlesWon);
            Assert.AreEqual(1, engine.Run.Map.NodesCompleted);
            Assert.AreEqual(25, engine.Run.Party[0].Hp);
            Assert.AreEqual(0, engine.Run.Gold);

            ResolveRewardPickup(engine);
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
        }

        [Test]
        public void SelectRoute_StartsNextBattleWithPartyHp()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Hp = 18,
                MaxHp = 40
            });
            engine.OnBattleFinished(state);
            ResolveRewardPickup(engine);
            SelectFirstCombatRoute(engine);

            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            Assert.AreEqual(2, engine.CurrentBattleNumber);

            var next = engine.Run.CurrentBattleConfig;
            var player = FindPlayer(next, "char_knight");
            Assert.NotNull(player);
            Assert.AreEqual(18, player.StartHp);
        }

        [Test]
        public void Victory_AwardsGoldInConfiguredRange()
        {
            var config = BuildConfig();
            config.RunSeed = 7;
            config.GoldMinPerVictory = 15;
            config.GoldMaxPerVictory = 25;

            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);

            Assert.GreaterOrEqual(engine.Run.LastGoldReward, 15);
            Assert.LessOrEqual(engine.Run.LastGoldReward, 25);
            Assert.AreEqual(0, engine.Run.Gold);

            engine.TryClaimRewardGold();
            Assert.AreEqual(engine.Run.LastGoldReward, engine.Run.Gold);
        }

        [Test]
        public void SelectRoute_PreservesPartyLevel()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Level = 3,
                Hp = 18,
                MaxHp = 40
            });
            engine.OnBattleFinished(state);
            ResolveRewardPickup(engine);
            SelectFirstCombatRoute(engine);

            var player = FindPlayer(engine.Run.CurrentBattleConfig, "char_knight");
            Assert.NotNull(player);
            Assert.AreEqual(3, player.Level);
            Assert.AreEqual(62, player.MaxHp);
            Assert.AreEqual(18, player.StartHp);
        }

        [Test]
        public void BossRoute_WithoutBossEncounters_SpawnsOnlySkeletonKing()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);
            ResolveRewardPickup(engine);

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 20);
            ResolveRewardPickup(engine);

            SelectFirstBossRoute(engine);

            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            Assert.NotNull(engine.Run.CurrentBattleConfig);

            var enemies = 0;
            CombatantConfig king = null;
            foreach (var cc in engine.Run.CurrentBattleConfig.Combatants)
            {
                if (cc.Team != TeamSide.Enemy)
                    continue;

                enemies++;
                if (cc.CharacterDefinitionId == "char_skeleton_king")
                    king = cc;
            }

            Assert.AreEqual(1, enemies);
            Assert.NotNull(king);
            Assert.AreEqual("骷髅王", king.DisplayName);
            Assert.AreEqual(400, king.MaxHp);
            Assert.IsTrue(engine.Run.CurrentBattleConfig.SkipFloorScaling);
        }

        [Test]
        public void BossVictory_CompletesRun()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);
            ResolveRewardPickup(engine);

            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 20);
            ResolveRewardPickup(engine);

            SelectFirstBossRoute(engine);
            CompleteVictory(engine, 12);
            ResolveRewardPickup(engine);

            Assert.AreEqual(ExpeditionPhase.RunComplete, engine.Run.Phase);
            Assert.AreEqual(3, engine.Run.BattlesWon);
            Assert.AreEqual(3, engine.Run.Map.NodesCompleted);
        }

        [Test]
        public void Defeat_EndsRun()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);

            var state = new BattleState { Outcome = BattleOutcome.PlayerDefeat };
            engine.OnBattleFinished(state);

            Assert.AreEqual(ExpeditionPhase.RunFailed, engine.Run.Phase);
        }

        [Test]
        public void Victory_GrantsXpToParty()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);

            Assert.AreEqual(16, engine.Run.LastXpReward);
            Assert.AreEqual(16, engine.Run.Party[0].Xp);
        }

        [Test]
        public void TryAddRelic_AccumulatesForRun()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            Assert.IsTrue(engine.TryAddRelic(RelicIds.FlameSword));
            Assert.AreEqual(1, engine.Run.Relics.Count);
            Assert.IsFalse(engine.TryAddRelic(RelicIds.FlameSword));
        }

        [Test]
        public void StartRun_ClearsRelicsAndXp()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            engine.TryAddRelic(RelicIds.CatStatue);
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 25);
            engine.StartRun();

            Assert.AreEqual(0, engine.Run.Relics.Count);
            Assert.AreEqual(0, engine.Run.LastXpReward);
        }

        [Test]
        public void TreasureRoute_OpensRewardPickupPhase()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Treasure,
                LayerNumber = 1,
                MapOptionIndex = 0,
                DisplayName = "宝箱",
                Description = "测试宝箱"
            });

            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.RewardPickup, engine.Run.Phase);
            Assert.NotNull(engine.Run.PendingRewardPickup);
            Assert.AreEqual(RewardPickupKind.Chest, engine.Run.PendingRewardPickup.Kind);
        }

        [Test]
        public void EventChoice_ResolvesAndReturnsToRouteSelect()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();

            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Event,
                EventId = Expedition.Events.ExpeditionEventIds.TrainingDummy,
                LayerNumber = 1,
                MapOptionIndex = 0,
                DisplayName = "训练人偶",
                Description = "事件"
            });

            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.EventChoice, engine.Run.Phase);
            Assert.IsTrue(engine.TryResolveEventChoice(2));
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.AreEqual(1, engine.Run.Map.NodesCompleted);
        }

        static void CompleteVictory(ExpeditionEngine engine, int hp)
        {
            var state = BuildVictoryState();
            state.Combatants.Add(new CombatantState
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                Hp = hp,
                MaxHp = 40
            });
            engine.OnBattleFinished(state);
        }

        static void ResolveRewardPickup(ExpeditionEngine engine)
        {
            engine.TryClaimRewardGold();
            if (engine.Run.PendingRewardPickup?.HasRelic == true)
                engine.TryClaimRewardRelic();
            if (engine.Run.PendingRewardPickup?.HasCard == true)
                engine.TryClaimRewardCard();
            if (engine.Run.PendingRewardPickup?.HasConsumable == true)
                engine.TryClaimRewardConsumable();
        }

        static void SelectFirstCombatRoute(ExpeditionEngine engine)
        {
            for (var i = 0; i < engine.Run.PendingRoutes.Count; i++)
            {
                var route = engine.Run.PendingRoutes[i];
                if (route.NodeType is ExpeditionNodeType.Combat or ExpeditionNodeType.Elite or ExpeditionNodeType.Boss)
                {
                    engine.TrySelectRoute(i);
                    return;
                }
            }

            engine.TrySelectRoute(0);
        }

        static void SelectFirstBossRoute(ExpeditionEngine engine)
        {
            for (var i = 0; i < engine.Run.PendingRoutes.Count; i++)
            {
                if (engine.Run.PendingRoutes[i].NodeType == ExpeditionNodeType.Boss)
                {
                    engine.TrySelectRoute(i);
                    return;
                }
            }

            SelectFirstCombatRoute(engine);
        }

        static BattleState BuildVictoryState()
        {
            return new BattleState { Outcome = BattleOutcome.PlayerVictory };
        }

        static ExpeditionConfig BuildConfig()
        {
            var config = new ExpeditionConfig
            {
                RunSeed = 1,
                ChapterLayerCount = 3,
                TargetBattleCount = 2
            };

            var encounter = new BattleConfig();
            encounter.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                MaxHp = 40
            });
            encounter.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_goblin",
                DisplayName = "哥布林",
                MaxHp = 20
            });

            config.CombatEncounters.Add(encounter);
            return config;
        }

        static CombatantConfig FindPlayer(BattleConfig config, string characterId)
        {
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == TeamSide.Player && cc.CharacterDefinitionId == characterId)
                    return cc;
            }

            return null;
        }
    }
}
