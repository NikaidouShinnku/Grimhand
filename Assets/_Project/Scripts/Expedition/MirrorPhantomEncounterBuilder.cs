using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class MirrorPhantomEncounterBuilder
    {
        public const string BattleKey = "event_mirror_phantom";

        public static BattleConfig BuildMirrorBattle(BattleConfig standardEncounter, IReadOnlyList<PartyMemberSnapshot> party)
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

            if (party != null)
            {
                var slot = FormationSlot.Front;
                foreach (var member in party)
                {
                    var stats = CharacterProgression.GetStatsForCharacter(member.CharacterDefinitionId, member.Level);
                    var mirror = new CombatantConfig
                    {
                        Id = $"Mirror_{member.CharacterDefinitionId}",
                        DisplayName = $"镜像·{member.DisplayName}",
                        Team = TeamSide.Enemy,
                        Slot = slot,
                        CharacterDefinitionId = member.CharacterDefinitionId,
                        Level = member.Level,
                        MaxHp = stats.MaxHp,
                        BaseAttack = 0,
                        BaseDefense = 0,
                        Speed = stats.Speed,
                        StartHp = stats.MaxHp
                    };

                    config.Combatants.Add(mirror);
                    slot = slot switch
                    {
                        FormationSlot.Front => FormationSlot.Middle,
                        FormationSlot.Middle => FormationSlot.Back,
                        _ => FormationSlot.Back
                    };
                }
            }

            if (standardEncounter != null)
            {
                foreach (var cc in standardEncounter.Combatants)
                {
                    if (cc.Team != TeamSide.Player)
                        continue;

                    config.Combatants.Add(ExpeditionBattleConfigBuilder.CloneCombatantConfigPublic(cc));
                }
            }

            return config;
        }
    }
}
