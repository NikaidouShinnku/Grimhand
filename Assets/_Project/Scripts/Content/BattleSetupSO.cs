using System.Collections.Generic;
using Grimhand.Battle.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "BattleSetup", menuName = "Grimhand/Battle Setup")]
    public class BattleSetupSO : ScriptableObject
    {
        [Tooltip("战斗随机种子；0 表示每场开局随机。")]
        public int Seed = 0;
        public int EnergyCap = 8;
        public int TurnStartEnergyRegen = 3;
        public int HandLimit = 8;
        public int CardsDrawnPerTurn = 5;
        public List<CharacterDefinitionSO> Combatants = new();

        public BattleConfig ToBattleConfig()
        {
            var config = new BattleConfig
            {
                Seed = Seed,
                EnergyCap = EnergyCap,
                TurnStartEnergyRegen = TurnStartEnergyRegen,
                HandLimit = HandLimit,
                CardsDrawnPerTurn = CardsDrawnPerTurn
            };

            foreach (var character in Combatants)
            {
                if (character == null)
                    continue;

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
                    Speed = character.Speed
                };

                foreach (var card in character.Deck)
                {
                    if (card == null)
                        continue;
                    cc.DeckTemplates.Add(card.ToTemplate());
                }

                config.Combatants.Add(cc);
            }

            return config;
        }
    }
}
