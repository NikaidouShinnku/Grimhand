using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CardPowerRules
    {
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
                        return action.Value + (action.ScaleWithAttack ? owner.Attack : 0);
                    case EffectActionType.GainBlock:
                        return action.Value + (action.ScaleWithDefense ? owner.Defense : 0);
                    case EffectActionType.Heal:
                        return action.Value;
                    case EffectActionType.ApplyStatus:
                        return action.Stacks;
                    case EffectActionType.DrawCardsNextTurn:
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
