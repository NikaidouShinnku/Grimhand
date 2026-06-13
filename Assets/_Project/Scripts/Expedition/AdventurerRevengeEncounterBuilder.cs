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

            var skeletonTemplate = FindSkeletonTemplate(standardEncounter);
            config.Combatants.Add(BuildSkeleton("复仇冒险者", FormationSlot.Front, skeletonTemplate));
            config.Combatants.Add(BuildSkeleton("骷髅佣兵", FormationSlot.Middle, skeletonTemplate));
            return config;
        }

        static CombatantConfig FindSkeletonTemplate(BattleConfig standardEncounter)
        {
            if (standardEncounter == null)
                return null;

            foreach (var cc in standardEncounter.Combatants)
            {
                if (cc.Team == TeamSide.Enemy && cc.CharacterDefinitionId == SkeletonId)
                    return cc;
            }

            return null;
        }

        static CombatantConfig BuildSkeleton(string name, FormationSlot slot, CombatantConfig template)
        {
            var sk = template != null
                ? ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(template)
                : new CombatantConfig
                {
                    CharacterDefinitionId = SkeletonId,
                    Team = TeamSide.Enemy,
                    UseSkillPool = true
                };

            sk.Id = $"Revenge_{slot}";
            sk.DisplayName = name;
            sk.Slot = slot;
            sk.Level = 2;
            sk.MaxHp = 42;
            sk.BaseAttack = 10;
            sk.BaseDefense = 6;
            sk.Speed = 6;
            return sk;
        }
    }
}
