using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionEngineTests
    {
        [Test]
        public void SkipVictoryOptionalRewards_AdvancesToRouteSelectWithoutTakingCard()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            engine.OnBattleFinished(BuildVictoryState());

            Assert.IsTrue(engine.TryClaimVictoryGold());
            var hadCard = engine.Run.PendingVictoryRewards?.HasCard == true;
            var bonusBefore = engine.Run.Party[0].BonusCards.Count;

            Assert.IsTrue(engine.TrySkipVictoryOptionalRewards());
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);

            if (hadCard)
                Assert.AreEqual(bonusBefore, engine.Run.Party[0].BonusCards.Count);
        }

        [Test]
        public void VictoryAfterFirstBattle_OpensVictoryRewardsThenRouteSelect()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

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

            Assert.AreEqual(ExpeditionPhase.VictoryRewards, engine.Run.Phase);
            Assert.AreEqual(1, engine.Run.BattlesWon);
            Assert.AreEqual(3, engine.Run.PendingRoutes.Count);
            Assert.AreEqual(25, engine.Run.Party[0].Hp);
            Assert.AreEqual(0, engine.Run.Gold);

            ResolveVictoryRewards(engine);
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
        }

        [Test]
        public void SelectRoute_StartsNextBattleWithPartyHp()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

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
            ResolveVictoryRewards(engine);
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
            CompleteVictory(engine, 25);

            Assert.GreaterOrEqual(engine.Run.LastGoldReward, 15);
            Assert.LessOrEqual(engine.Run.LastGoldReward, 25);
            Assert.AreEqual(0, engine.Run.Gold);

            engine.TryClaimVictoryGold();
            Assert.AreEqual(engine.Run.LastGoldReward, engine.Run.Gold);
        }

        [Test]
        public void SelectRoute_PreservesPartyLevel()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

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
            ResolveVictoryRewards(engine);
            SelectFirstCombatRoute(engine);

            var player = FindPlayer(engine.Run.CurrentBattleConfig, "char_knight");
            Assert.NotNull(player);
            Assert.AreEqual(3, player.Level);
            Assert.AreEqual(62, player.MaxHp);
            Assert.AreEqual(18, player.StartHp);
        }

        [Test]
        public void ThirdVictory_CompletesRun()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

            CompleteVictory(engine, 25);
            ResolveVictoryRewards(engine);
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 20);
            ResolveVictoryRewards(engine);
            SelectFirstCombatRoute(engine);
            CompleteVictory(engine, 12);
            ResolveVictoryRewards(engine);

            Assert.AreEqual(ExpeditionPhase.RunComplete, engine.Run.Phase);
            Assert.AreEqual(3, engine.Run.BattlesWon);
            Assert.GreaterOrEqual(engine.Run.Gold, 45);
            Assert.LessOrEqual(engine.Run.Gold, 75);
        }

        [Test]
        public void Defeat_EndsRun()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

            var state = new BattleState { Outcome = BattleOutcome.PlayerDefeat };
            engine.OnBattleFinished(state);

            Assert.AreEqual(ExpeditionPhase.RunFailed, engine.Run.Phase);
        }

        [Test]
        public void Victory_GrantsXpToParty()
        {
            var config = BuildConfig();
            config.XpPerVictory = 16;
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            CompleteVictory(engine, 25);

            Assert.AreEqual(16, engine.Run.LastXpReward);
            Assert.AreEqual(16, engine.Run.Party[0].Xp);
        }

        [Test]
        public void TryAddRelic_AccumulatesForRun()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

            Assert.IsTrue(engine.TryAddRelic(RelicIds.FlameSword));
            Assert.AreEqual(1, engine.Run.Relics.Count);
            Assert.IsFalse(engine.TryAddRelic(RelicIds.FlameSword));
        }

        [Test]
        public void StartRun_ClearsRelicsAndXp()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            engine.TryAddRelic(RelicIds.CatStatue);
            CompleteVictory(engine, 25);
            engine.StartRun();

            Assert.AreEqual(0, engine.Run.Relics.Count);
            Assert.AreEqual(0, engine.Run.LastXpReward);
        }

        [Test]
        public void TreasureRoute_OpensTreasureLootPhase()
        {
            var config = BuildConfig();
            config.RunSeed = 99;
            config.CombatRouteWeight = 0;
            config.TreasureRouteWeight = 100;

            var engine = new ExpeditionEngine(config);
            engine.StartRun();
            CompleteVictory(engine, 25);
            ResolveVictoryRewards(engine);

            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.TreasureLoot, engine.Run.Phase);
            Assert.NotNull(engine.Run.PendingChestReward);
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

        static void ResolveVictoryRewards(ExpeditionEngine engine)
        {
            engine.TryClaimVictoryGold();
            if (engine.Run.PendingVictoryRewards?.HasRelic == true)
                engine.TryClaimVictoryRelic();
            if (engine.Run.PendingVictoryRewards?.HasCard == true)
                engine.TryClaimVictoryCard();
        }

        static void SelectFirstCombatRoute(ExpeditionEngine engine)
        {
            for (var i = 0; i < engine.Run.PendingRoutes.Count; i++)
            {
                if (engine.Run.PendingRoutes[i].NodeType == ExpeditionNodeType.Combat)
                {
                    engine.TrySelectRoute(i);
                    return;
                }
            }

            engine.TrySelectRoute(0);
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
                TargetBattleCount = 3,
                RoutesPerVictory = 3
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
