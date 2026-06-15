using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Expedition
{
    public static class AncientFurnaceEncounterBuilder
    {
        public const string BattleKey = "event_ancient_furnace_golem";

        public static BattleConfig BuildGolemBattle(
            BattleConfig standardEncounter,
            IReadOnlyDictionary<string, CombatantConfig> monsterTemplates = null)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 4,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 4,
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
            monsterTemplates?.TryGetValue(StoneGolemBossEncounterBuilder.CharacterId, out template);
            if (template == null)
                template = FindGolemTemplate(standardEncounter);

            var golem = template != null
                ? ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(template)
                : new CombatantConfig
                {
                    CharacterDefinitionId = StoneGolemBossEncounterBuilder.CharacterId,
                    Team = TeamSide.Enemy,
                    UseSkillPool = true
                };

            golem.Id = "Furnace_Golem";
            golem.DisplayName = "石傀儡";
            golem.Slot = FormationSlot.Front;
            config.Combatants.Add(golem);
            return config;
        }

        static CombatantConfig FindGolemTemplate(BattleConfig standardEncounter)
        {
            if (standardEncounter == null)
                return null;

            foreach (var cc in standardEncounter.Combatants)
            {
                if (cc.Team == TeamSide.Enemy &&
                    cc.CharacterDefinitionId == StoneGolemBossEncounterBuilder.CharacterId)
                    return cc;
            }

            return null;
        }
    }
}
