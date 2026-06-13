using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    /// <summary>地牢第 40 层 Boss 占位：三只石傀儡。</summary>
    public static class StoneGolemBossEncounterBuilder
    {
        public const string DisplayName = "石傀儡";
        public const string CharacterId = "char_stone_golem";

        public static BattleConfig BuildTemplate(
            BattleConfig standardEncounter,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates = null)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 3,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 3,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = MonsterEncounterBuilder.EnemyDrawPerTurn,
                EnemyTurnEnergyBudget = MonsterEncounterBuilder.EnemyEnergyBudget,
                SkipFloorScaling = true
            };

            if (standardEncounter != null)
            {
                foreach (var cc in standardEncounter.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    config.Combatants.Add(ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(cc));
                }
            }

            CombatantConfig template = null;
            monsterTemplates?.TryGetValue(CharacterId, out template);

            for (var i = 0; i < 3; i++)
            {
                var golem = template != null
                    ? ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(template)
                    : BuildFallbackGolem();

                golem.Id = $"Character_Stone_Golem_Boss_{i}";
                golem.DisplayName = DisplayName;
                config.Combatants.Add(golem);
            }

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);
            return config;
        }

        static CombatantConfig BuildFallbackGolem() =>
            new()
            {
                DisplayName = DisplayName,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = CharacterId,
                MaxHp = 85,
                BaseAttack = 10,
                BaseDefense = 9,
                Speed = 2
            };
    }
}
