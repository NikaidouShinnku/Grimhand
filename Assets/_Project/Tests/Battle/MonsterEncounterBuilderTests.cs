using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Tests.Battle
{
    public class MonsterEncounterBuilderTests
    {
        [Test]
        public void Build_ReplacesEnemiesWithCatalogComposition()
        {
            var baseline = new BattleConfig();
            baseline.Combatants.Add(new CombatantConfig
            {
                Id = "p1",
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight"
            });
            baseline.Combatants.Add(new CombatantConfig
            {
                Id = "old_enemy",
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_wraith"
            });

            var goblin = new CombatantConfig
            {
                CharacterDefinitionId = "char_goblin",
                DisplayName = "哥布林",
                Team = TeamSide.Enemy,
                MaxHp = 20,
                UseSkillPool = true
            };
            goblin.SkillPoolCandidates.Add(new CardTemplate { DefinitionId = "g_bite" });

            var encounter = MonsterEncounterCatalog.GetById(MonsterEncounterCatalog.GoblinTriple);
            var map = MonsterEncounterBuilder.BuildMonsterTemplateMap(new[] { goblin });
            var built = MonsterEncounterBuilder.Build(baseline, encounter, map);

            var enemyCount = 0;
            foreach (var cc in built.Combatants)
            {
                if (cc.Team == TeamSide.Enemy)
                {
                    enemyCount++;
                    Assert.AreEqual("char_goblin", cc.CharacterDefinitionId);
                }
            }

            Assert.AreEqual(3, enemyCount);
            Assert.AreEqual(4, built.EnemyTurnEnergyBudget);
            Assert.AreEqual(5, built.EnemyCardsDrawnPerTurn);
        }

        [Test]
        public void Roll_EliteUsesElitePoolOnly()
        {
            var rng = new BattleRng(7);
            for (var i = 0; i < 30; i++)
            {
                var id = MonsterEncounterCatalog.Roll(12, isElite: true, rng);
                var def = MonsterEncounterCatalog.GetById(id);
                Assert.IsTrue(def.IsElite);
                Assert.GreaterOrEqual(12, def.MinFloor);
                Assert.LessOrEqual(12, def.MaxFloor);
            }
        }
    }
}
