using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public static class MirrorPhantomEncounterBuilder
    {
        public const string BattleKey = "event_mirror_phantom";
        public const int MirrorEnemyEnergyBudget = 4;
        public const int MirrorEnemyCardsDrawnPerTurn = 5;

        public static BattleConfig BuildMirrorBattle(BattleConfig standardEncounter, IReadOnlyList<PartyMemberSnapshot> party)
        {
            var config = new BattleConfig
            {
                EnergyCap = standardEncounter?.EnergyCap ?? 4,
                TurnStartEnergyRegen = standardEncounter?.TurnStartEnergyRegen ?? 4,
                HandLimit = standardEncounter?.HandLimit ?? 10,
                CardsDrawnPerTurn = standardEncounter?.CardsDrawnPerTurn ?? 5,
                EnemyCardsDrawnPerTurn = MirrorEnemyCardsDrawnPerTurn,
                EnemyTurnEnergyBudget = MirrorEnemyEnergyBudget,
                SkipFloorScaling = true
            };

            if (party != null)
            {
                var slot = FormationSlot.Front;
                foreach (var member in party)
                {
                    if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                        continue;

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

        /// <summary>
        /// BuildEncounter 填好玩家卡组后调用：把各角色卡组复制给对应镜像敌人，并锁定敌方 4 能量 / 抽 5。
        /// </summary>
        public static void FinalizeMirrorEnemyLoadout(BattleConfig config)
        {
            if (config == null)
                return;

            config.EnemyCardsDrawnPerTurn = MirrorEnemyCardsDrawnPerTurn;
            config.EnemyTurnEnergyBudget = MirrorEnemyEnergyBudget;

            var playerDecks = new Dictionary<string, List<CardTemplate>>();
            foreach (var cc in config.Combatants)
            {
                if (cc == null || cc.Team != TeamSide.Player || string.IsNullOrEmpty(cc.CharacterDefinitionId))
                    continue;

                var list = new List<CardTemplate>();
                if (cc.DeckTemplates != null)
                {
                    foreach (var template in cc.DeckTemplates)
                    {
                        if (template == null || string.IsNullOrEmpty(template.DefinitionId))
                            continue;
                        list.Add(ExpeditionBattleConfigBuilder.CloneTemplate(template));
                    }
                }

                playerDecks[cc.CharacterDefinitionId] = list;
            }

            foreach (var cc in config.Combatants)
            {
                if (cc == null || cc.Team != TeamSide.Enemy)
                    continue;

                if (string.IsNullOrEmpty(cc.Id) || !cc.Id.StartsWith("Mirror_"))
                    continue;

                cc.DeckTemplates.Clear();
                cc.SkillPoolCandidates.Clear();
                cc.UseSkillPool = false;

                if (string.IsNullOrEmpty(cc.CharacterDefinitionId)
                    || !playerDecks.TryGetValue(cc.CharacterDefinitionId, out var source)
                    || source == null)
                {
                    continue;
                }

                foreach (var template in source)
                {
                    var clone = ExpeditionBattleConfigBuilder.CloneTemplate(template);
                    // 归属到镜像单位，供敌方 AI / 意图解析 Owner
                    clone.OwnerCharacterId = cc.CharacterDefinitionId;
                    cc.DeckTemplates.Add(clone);
                }
            }
        }
    }
}
