using System;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CardPowerRules
    {
        public static int ComputeActionValue(EffectActionSpec action, CombatantState owner)
        {
            if (action == null)
                return 0;

            var value = action.Value;
            if (owner == null)
                return value;

            if (action.ScaleWithAttack)
            {
                var pct = action.AttackScalePercent > 0 ? action.AttackScalePercent : 100;
                value += (int)Math.Round(owner.Attack * pct / 100f);
            }

            if (action.ScaleWithDefense)
            {
                var pct = action.DefenseScalePercent > 0 ? action.DefenseScalePercent : 100;
                value += (int)Math.Round(owner.Defense * pct / 100f);
            }

            return value;
        }

        public static int GetEffectivePower(CardInstanceState card, CombatantState owner)
        {
            if (owner == null)
                return GetBasePower(card);

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                switch (action.Type)
                {
                    case EffectActionType.DealDamage:
                    case EffectActionType.GainBlock:
                    case EffectActionType.Heal:
                        return ComputeActionValue(action, owner);
                    case EffectActionType.ApplyStatus:
                        return action.Stacks;
                    case EffectActionType.DrawCardsNextTurn:
                    case EffectActionType.DrawCards:
                        return action.Value;
                }
            }

            return GetBasePower(card);
        }

        public static int GetBasePower(CardInstanceState card)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (action.Type == EffectActionType.ApplyStatus)
                    return action.Stacks;

                return action.Value;
            }

            return 0;
        }

        public static string GetPowerLabel(CardInstanceState card)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                switch (action.Type)
                {
                    case EffectActionType.DealDamage:
                        return "伤害";
                    case EffectActionType.GainBlock:
                        return "护甲";
                    case EffectActionType.Heal:
                        return "治疗";
                    case EffectActionType.ApplyStatus:
                        return "状态";
                    case EffectActionType.DrawCardsNextTurn:
                    case EffectActionType.DrawCards:
                        return "抽牌";
                    case EffectActionType.ReflectLastDamageToAttacker:
                        return "反射";
                }
            }

            return "威力";
        }

        public static string DescribeCardEffect(CardInstanceState card, CombatantState owner, bool hidden)
        {
            if (hidden)
                return "???";

            var power = GetEffectivePower(card, owner);
            var label = GetPowerLabel(card);
            return $"{label} {power}";
        }
    }
}
