using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionEngineTests
    {
        [Test]
        public void VictoryAfterFirstBattle_OpensRouteSelect()
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

            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.AreEqual(1, engine.Run.BattlesWon);
            Assert.AreEqual(3, engine.Run.PendingRoutes.Count);
            Assert.AreEqual(25, engine.Run.Party[0].Hp);
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

            Assert.IsTrue(engine.TrySelectRoute(0));
            Assert.AreEqual(ExpeditionPhase.InBattle, engine.Run.Phase);
            Assert.AreEqual(2, engine.CurrentBattleNumber);

            var next = engine.Run.CurrentBattleConfig;
            var player = FindPlayer(next, "char_knight");
            Assert.NotNull(player);
            Assert.AreEqual(18, player.StartHp);
        }

        [Test]
        public void ThirdVictory_CompletesRun()
        {
            var config = BuildConfig();
            var engine = new ExpeditionEngine(config);
            engine.StartRun();

            CompleteVictory(engine, 25);
            engine.TrySelectRoute(0);
            CompleteVictory(engine, 20);
            engine.TrySelectRoute(1);
            CompleteVictory(engine, 12);

            Assert.AreEqual(ExpeditionPhase.RunComplete, engine.Run.Phase);
            Assert.AreEqual(3, engine.Run.BattlesWon);
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
