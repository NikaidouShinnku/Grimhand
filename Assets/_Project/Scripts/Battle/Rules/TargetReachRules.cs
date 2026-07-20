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
                case TargetReach.MiddleAndBack:
                    return slot == FormationSlot.Middle || slot == FormationSlot.Back;
                default:
                    return true;
            }
        }

        public static TargetReach GetPickReach(BattleState state, CardInstanceState card, CombatantState owner)
        {
            if (RelicEffectRules.ShouldExpandBackRowReach(state, owner, card))
                return TargetReach.Any;

            var reach = GetPickReach(card);
            if (card != null
                && card.DefinitionId == "m_musket_shot"
                && reach == TargetReach.BackOnly
                && !HasAliveEnemyInBack(state, owner))
                return TargetReach.FrontAndMiddle;

            return reach;
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

        public static bool CanPickUnit(
            BattleState state,
            CardInstanceState card,
            CombatantState target,
            CombatantState owner = null)
        {
            if (target == null)
                return false;

            if (!target.IsAlive)
                return false;

            var expandReach = owner != null && RelicEffectRules.ShouldExpandBackRowReach(state, owner, card);

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!CardRules.ActionRequiresCharacterPickForReach(action))
                    continue;

                var reach = expandReach ? TargetReach.Any : action.Reach;
                if (card.DefinitionId == "m_musket_shot"
                    && reach == TargetReach.BackOnly
                    && !HasAliveEnemyInBack(state, owner))
                    reach = TargetReach.FrontAndMiddle;

                var effective = PositionRules.GetEffectiveSlot(state, target);
                if (!IsSlotAllowed(reach, effective))
                    return false;
            }

            return true;
        }

        static bool HasAliveEnemyInBack(BattleState state, CombatantState owner)
        {
            if (state == null || owner == null)
                return false;

            var enemyTeam = owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            foreach (var unit in state.GetTeam(enemyTeam))
            {
                if (unit != null
                    && unit.IsAlive
                    && PositionRules.GetEffectiveSlot(state, unit) == FormationSlot.Back)
                    return true;
            }

            return false;
        }

        public static int AdjustPowerForTarget(
            BattleState state,
            EffectActionSpec action,
            CombatantState target,
            int power)
        {
            if (target == null || action.BackRowPowerPercent == 100)
                return power;

            if (PositionRules.GetEffectiveSlot(state, target) != FormationSlot.Back)
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
