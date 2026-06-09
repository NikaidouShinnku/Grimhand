using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class EnemyDeckBuilder
    {
        public static List<CardTemplate> BuildRandomDeck(
            IReadOnlyList<CardTemplate> pool,
            BattleRng rng,
            int deckSize,
            int pickMin,
            int pickMax)
        {
            var result = new List<CardTemplate>();
            if (pool == null || pool.Count == 0 || deckSize <= 0)
                return result;

            pickMin = System.Math.Max(1, pickMin);
            pickMax = System.Math.Max(pickMin, pickMax);
            var uniqueCount = System.Math.Min(pool.Count, rng.NextInt(pickMin, pickMax + 1));

            var picked = new List<CardTemplate>();
            var indices = new List<int>();
            for (var i = 0; i < pool.Count; i++)
                indices.Add(i);

            for (var i = 0; i < uniqueCount && indices.Count > 0; i++)
            {
                var pick = rng.NextIndex(indices.Count);
                picked.Add(pool[indices[pick]]);
                indices.RemoveAt(pick);
            }

            for (var i = 0; i < deckSize; i++)
                result.Add(CloneTemplate(picked[rng.NextIndex(picked.Count)]));

            return result;
        }

        static CardTemplate CloneTemplate(CardTemplate source)
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
            {
                copy.Actions.Add(new EffectActionSpec
                {
                    Type = action.Type,
                    Target = action.Target,
                    Value = action.Value,
                    StatusId = action.StatusId,
                    Stacks = action.Stacks,
                    Duration = action.Duration,
                    ScaleWithAttack = action.ScaleWithAttack,
                    ScaleWithDefense = action.ScaleWithDefense,
                    AttackScalePercent = action.AttackScalePercent,
                    DefenseScalePercent = action.DefenseScalePercent,
                    Condition = action.Condition,
                    Reach = action.Reach,
                    SplashBehindTarget = action.SplashBehindTarget,
                    SplashPowerPercent = action.SplashPowerPercent,
                    BackRowPowerPercent = action.BackRowPowerPercent
                });
            }

            return copy;
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
    }
}
