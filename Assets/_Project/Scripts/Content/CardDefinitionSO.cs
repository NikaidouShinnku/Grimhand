using System.Collections.Generic;
using Grimhand.Battle.Model;
using UnityEngine;

namespace Grimhand.Content
{
    [CreateAssetMenu(fileName = "CardDefinition", menuName = "Grimhand/Card Definition")]
    public class CardDefinitionSO : ScriptableObject
    {
        public string CardId = "card_id";
        public string DisplayName = "新卡牌";
        public string OwnerCharacterId = "char_id";
        public int Cost = 1;
        public CardType CardType = CardType.Attack;
        public List<EffectActionDefinition> Actions = new();

        public CardTemplate ToTemplate()
        {
            var template = new CardTemplate
            {
                DefinitionId = CardId,
                DisplayName = DisplayName,
                OwnerCharacterId = OwnerCharacterId,
                Cost = Cost,
                CardType = CardType
            };

            foreach (var action in Actions)
                template.Actions.Add(action.ToSpec());

            return template;
        }
    }
}
