using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
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
        public CardRarity Rarity = CardRarity.Common;
        public List<string> Keywords = new();
        public List<EffectActionDefinition> Actions = new();

        [Header("Visual (optional — 也可在 Card Visual Catalog 统一配置)")]
        public Sprite CardArt;
        public Sprite CardFrame;
        public Sprite CardIcon;

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

            template.Keywords.AddRange(Keywords);
            foreach (var action in Actions)
                template.Actions.Add(action.ToSpec());

            // 女王关键卡曾出现空 Actions / 错 Type；运行时强制用代码权威定义，避免 SO 脏数据导致「完全没效果」
            GhostQueenCardCatalog.TryApplyCanonical(template);
            V09BossCardCatalog.TryApplyCanonical(template);

            return template;
        }
    }
}
