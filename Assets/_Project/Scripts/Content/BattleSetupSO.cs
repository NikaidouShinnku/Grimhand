using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "BattleSetup", menuName = "Grimhand/Battle Setup")]
    public class BattleSetupSO : ScriptableObject
    {
        [Tooltip("战斗随机种子；0 表示每场开局随机。")]
        public int Seed = 0;
        public int EnergyCap = 8;
        public int TurnStartEnergyRegen = 4;
        public int HandLimit = 8;
        public int CardsDrawnPerTurn = 5;
        [Tooltip("敌方每回合抽牌数；0 表示与 CardsDrawnPerTurn 相同。")]
        public int EnemyCardsDrawnPerTurn;
        [Tooltip("敌方每回合出牌能量预算；0 表示与 TurnStartEnergyRegen 相同。")]
        public int EnemyTurnEnergyBudget;
        [Tooltip("为 true 时远征楼层缩放不作用于本场敌人（Boss 固定数值）。")]
        public bool SkipFloorScaling;
        public List<CharacterDefinitionSO> Combatants = new();
        [Tooltip("战斗中可召唤的单位模板（不直接参战）。")]
        public List<CharacterDefinitionSO> SummonTemplates = new();

        public BattleConfig ToBattleConfig()
        {
            var config = new BattleConfig
            {
                Seed = Seed,
                EnergyCap = EnergyCap,
                TurnStartEnergyRegen = TurnStartEnergyRegen,
                HandLimit = HandLimit,
                CardsDrawnPerTurn = CardsDrawnPerTurn,
                EnemyCardsDrawnPerTurn = EnemyCardsDrawnPerTurn,
                EnemyTurnEnergyBudget = EnemyTurnEnergyBudget,
                SkipFloorScaling = SkipFloorScaling
            };

            foreach (var character in Combatants)
            {
                if (character == null)
                    continue;

                config.Combatants.Add(BuildCombatantConfig(character));
            }

            foreach (var summon in SummonTemplates)
            {
                if (summon == null || string.IsNullOrEmpty(summon.CharacterId))
                    continue;

                config.SummonTemplates[summon.CharacterId] = BuildCombatantConfig(summon);
            }

            FormationSlotRules.AssignUniqueSlotsPerTeam(config.Combatants);
            return config;
        }

        static CombatantConfig BuildCombatantConfig(CharacterDefinitionSO character)
        {
            var cc = new CombatantConfig
            {
                Id = character.name,
                DisplayName = character.DisplayName,
                Team = character.Team,
                Slot = character.Slot,
                CharacterDefinitionId = character.CharacterId,
                Level = character.Level,
                MaxHp = character.MaxHp,
                BaseAttack = character.BaseAttack,
                BaseDefense = character.BaseDefense,
                Speed = character.Speed,
                UseRandomSkillPool = character.Team == TeamSide.Enemy && character.SkillPool.Count >= 2,
                RandomDeckSize = character.EnemyRandomDeckSize,
                RandomSkillPickMin = character.EnemySkillPickMin,
                RandomSkillPickMax = character.EnemySkillPickMax
            };

            cc.Traits.AddRange(character.Traits);

            if (cc.UseRandomSkillPool)
            {
                foreach (var card in character.SkillPool)
                {
                    if (card == null)
                        continue;
                    cc.SkillPoolCandidates.Add(card.ToTemplate());
                }
            }
            else
            {
                foreach (var card in character.Deck)
                {
                    if (card == null)
                        continue;
                    cc.DeckTemplates.Add(card.ToTemplate());
                }
            }

            return cc;
        }
    }
}
