using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    public static class MonsterEncounterBuilder
    {
        public const int EnemyEnergyBudget = 4;
        public const int EnemyDrawPerTurn = 5;

        public static BattleConfig Build(
            BattleConfig playerBaseline,
            MonsterEncounterDefinition encounter,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates)
        {
            var config = ExpeditionBattleConfigBuilder.CloneTemplate(ExtractPlayerBaseline(playerBaseline));
            config.EnemyTurnEnergyBudget = EnemyEnergyBudget;
            config.EnemyCardsDrawnPerTurn = EnemyDrawPerTurn;
            config.SkipFloorScaling = false;

            for (var i = config.Combatants.Count - 1; i >= 0; i--)
            {
                if (config.Combatants[i].Team == TeamSide.Enemy)
                    config.Combatants.RemoveAt(i);
            }

            if (encounter?.EnemyCharacterIds != null)
            {
                var slotIndex = 0;
                foreach (var charId in encounter.EnemyCharacterIds)
                {
                    if (string.IsNullOrEmpty(charId)
                        || monsterTemplates == null
                        || !monsterTemplates.TryGetValue(charId, out var template))
                        continue;

                    var copy = ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(template);
                    copy.Id = $"enemy_{charId}_{slotIndex}";
                    config.Combatants.Add(copy);
                    slotIndex++;
                }
            }

            if (monsterTemplates != null
                && monsterTemplates.TryGetValue(MinionTraitCatalog.SkeletonCharacterId, out var skeleton))
            {
                config.SummonTemplates[MinionTraitCatalog.SkeletonCharacterId] =
                    ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(skeleton);
            }

            if (monsterTemplates != null
                && monsterTemplates.TryGetValue(MinionTraitCatalog.AbyssCreatureCharacterId, out var abyss))
            {
                config.SummonTemplates[MinionTraitCatalog.AbyssCreatureCharacterId] =
                    ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(abyss);
            }

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);

            var enemyCount = 0;
            foreach (var cc in config.Combatants)
            {
                if (cc.Team == TeamSide.Enemy)
                    enemyCount++;
            }

            if (enemyCount == 0 && encounter?.EnemyCharacterIds is { Length: > 0 })
            {
                throw new System.InvalidOperationException(
                    $"怪物组合「{encounter.Id}」未能生成任何敌人；请执行 Grimhand/Content/Generate Demo ScriptableObjects 生成小怪资产。");
            }

            return config;
        }

        public static BattleConfig ExtractPlayerBaseline(BattleConfig source)
        {
            var baseline = ExpeditionBattleConfigBuilder.CloneTemplate(source);
            for (var i = baseline.Combatants.Count - 1; i >= 0; i--)
            {
                if (baseline.Combatants[i].Team == TeamSide.Enemy)
                    baseline.Combatants.RemoveAt(i);
            }

            FormationSlotRules.AssignUniqueSlotsPerTeam(baseline.Combatants);
            return baseline;
        }

        public static Dictionary<string, CombatantConfig> BuildMonsterTemplateMap(
            IEnumerable<CombatantConfig> monsterTemplates)
        {
            var map = new Dictionary<string, CombatantConfig>();
            if (monsterTemplates == null)
                return map;

            foreach (var template in monsterTemplates)
            {
                if (template == null || string.IsNullOrEmpty(template.CharacterDefinitionId))
                    continue;

                map[template.CharacterDefinitionId] =
                    ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(template);
            }

            return map;
        }
    }
}
