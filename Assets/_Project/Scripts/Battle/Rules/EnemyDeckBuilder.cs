using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class EnemyDeckBuilder
    {
        /// <summary>
        /// 按技能池条目逐张加入（默认每项 1 张；SkillPool 中重复引用即多张）。
        /// 各小怪贡献的牌会在开战时汇入同一敌方抽牌堆并统一洗牌。
        /// </summary>
        public static void ApplySkillPoolEntries(IList<CardTemplate> deck, IReadOnlyList<CardTemplate> pool)
        {
            deck.Clear();
            if (pool == null || pool.Count == 0)
                return;

            foreach (var template in pool)
                deck.Add(CloneTemplate(template));
        }

        /// <summary>固定牌组：保持牌种与数量不变，开战前洗牌。</summary>
        public static void ShuffleFixedDeck(IList<CardTemplate> deck, BattleRng rng)
        {
            if (deck == null || deck.Count <= 1 || rng == null)
                return;

            for (var i = deck.Count - 1; i > 0; i--)
            {
                var j = rng.NextIndex(i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        public static CardTemplate CloneTemplate(CardTemplate source)
        {
            var copy = new CardTemplate
            {
                DefinitionId = source.DefinitionId,
                DisplayName = source.DisplayName,
                OwnerCharacterId = source.OwnerCharacterId,
                Cost = source.Cost,
                CardType = source.CardType
            };
            copy.Keywords.AddRange(source.Keywords);
            foreach (var action in source.Actions)
                copy.Actions.Add(EffectActionSpec.Clone(action));

            return copy;
        }
    }
}
