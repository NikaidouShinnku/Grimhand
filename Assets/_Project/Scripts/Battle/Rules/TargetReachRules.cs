using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class TargetReachRules
    {
        public static bool IsSlotAllowed(TargetReach reach, FormationSlot slot)
        {
            switch (reach)
            {
                case TargetReach.Any:
                    return true;
                case TargetReach.FrontAndMiddle:
                    return slot == FormationSlot.Front || slot == FormationSlot.Middle;
                case TargetReach.BackOnly:
                    return slot == FormationSlot.Back;
                default:
                    return true;
            }
        }

        public static TargetReach GetPickReach(CardInstanceState card)
        {
            var reach = TargetReach.Any;
            var hasPick = false;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!CardRules.ActionRequiresCharacterPickForReach(action))
                    continue;

                hasPick = true;
                reach = NarrowReach(reach, action.Reach);
            }

            return hasPick ? reach : TargetReach.Any;
        }

        public static bool CanPickUnit(CardInstanceState card, CombatantState target)
        {
            if (target == null || !target.IsAlive)
                return false;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!CardRules.ActionRequiresCharacterPickForReach(action))
                    continue;

                if (!IsSlotAllowed(action.Reach, target.Slot))
                    return false;
            }

            return true;
        }

        public static int AdjustPowerForTarget(EffectActionSpec action, CombatantState target, int power)
        {
            if (target == null || action.BackRowPowerPercent >= 100)
                return power;

            if (target.Slot != FormationSlot.Back)
                return power;

            return System.Math.Max(1, (int)System.Math.Round(power * action.BackRowPowerPercent / 100f));
        }

        static TargetReach NarrowReach(TargetReach current, TargetReach next)
        {
            if (current == TargetReach.Any)
                return next;

            if (current == next)
                return current;

            return TargetReach.FrontAndMiddle;
        }
    }
}
