using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Expedition
{
    public static class AdventurerRevengeEncounterBuilder
    {
        public const string BattleKey = "event_adventurer_revenge";
        const string SkeletonId = "char_skeleton";

        public static BattleConfig BuildRevengeBattle(BattleConfig standardEncounter)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 4,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 4,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = 3,
                EnemyTurnEnergyBudget = 3,
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

            config.Combatants.Add(BuildSkeleton("复仇冒险者", FormationSlot.Front));
            config.Combatants.Add(BuildSkeleton("骷髅佣兵", FormationSlot.Middle));
            return config;
        }

        static CombatantConfig BuildSkeleton(string name, FormationSlot slot)
        {
            var sk = new CombatantConfig
            {
                Id = $"Revenge_{slot}",
                DisplayName = name,
                Team = TeamSide.Enemy,
                Slot = slot,
                CharacterDefinitionId = SkeletonId,
                Level = 2,
                MaxHp = 42,
                BaseAttack = 10,
                BaseDefense = 6,
                Speed = 6,
                UseRandomSkillPool = true,
                RandomDeckSize = 6,
                RandomSkillPickMin = 2,
                RandomSkillPickMax = 3
            };

            sk.RandomSkillPickMax = 3;
            return sk;
        }
    }
}
