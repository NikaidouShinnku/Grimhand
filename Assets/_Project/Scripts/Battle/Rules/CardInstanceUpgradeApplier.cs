using System;
using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>对战斗内卡牌实例追加升级层数（可超出卡牌升级上限）。</summary>
    public static class CardInstanceUpgradeApplier
    {
        public static CardInstanceState ApplyBonusLevels(CardInstanceState source, int bonusLevels)
        {
            if (source == null || bonusLevels <= 0)
                return source;

            if (!CardUpgradeCatalog.TryGetByDisplayName(source.DisplayName, out var spec))
                return source;

            var clone = Clone(source);
            var dmg = spec.DamagePerLevel * bonusLevels;
            var block = spec.BlockPerLevel * bonusLevels;
            var heal = spec.HealPerLevel * bonusLevels;
            var poison = spec.PoisonStacksPerLevel * bonusLevels;
            var slow = spec.SlowStacksPerLevel * bonusLevels;
            var costReduce = spec.CostReductionPerLevel * bonusLevels;

            if (costReduce > 0)
                clone.Cost = Math.Max(0, clone.Cost - costReduce);

            foreach (var action in clone.Actions)
            {
                switch (action.Type)
                {
                    case EffectActionType.DealDamage when dmg > 0:
                        action.Value += dmg;
                        break;
                    case EffectActionType.GainBlock when block > 0:
                        action.Value += block;
                        break;
                    case EffectActionType.Heal when heal > 0:
                        action.Value += heal;
                        break;
                    case EffectActionType.ApplyStatus when action.StatusId == "poison" && poison > 0:
                        action.Stacks += poison;
                        break;
                    case EffectActionType.ApplyStatus when action.StatusId == "slow" && slow > 0:
                        action.Stacks += slow;
                        break;
                }
            }

            return clone;
        }

        static CardInstanceState Clone(CardInstanceState source)
        {
            var clone = new CardInstanceState
            {
                InstanceId = source.InstanceId,
                DefinitionId = source.DefinitionId,
                OwnerCharacterId = source.OwnerCharacterId,
                OwnerCombatantId = source.OwnerCombatantId,
                Cost = source.Cost,
                CardType = source.CardType,
                IsUsable = source.IsUsable,
                IsBonusHandCard = source.IsBonusHandCard,
                DisplayName = source.DisplayName,
                UpgradeLevel = source.UpgradeLevel
            };

            foreach (var keyword in source.Keywords)
                clone.Keywords.Add(keyword);

            foreach (var action in source.Actions)
                clone.Actions.Add(EffectActionSpec.Clone(action));

            return clone;
        }
    }
}
