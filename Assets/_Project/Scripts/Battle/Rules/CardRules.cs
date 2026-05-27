using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public enum TargetPickSide
    {
        None,
        Enemy,
        Ally
    }

    public static class CardRules
    {
        public static bool IsPolluted(CardInstanceState card) => !card.IsUsable;

        /// <summary>
        /// 规划阶段是否需要玩家点选具体角色。
        /// 例外：自身防御/治疗/抽牌、按槽位自动解析、应对类效果、敌方全体（未来）。
        /// </summary>
        public static bool ShouldPromptForTarget(BattleState state, CardInstanceState card, CombatantState owner)
        {
            var side = GetRequiredTargetPick(card);
            if (side == TargetPickSide.None)
                return false;

            return GetValidTargetCandidates(state, card, owner).Count > 0;
        }

        public static TargetPickSide GetRequiredTargetPick(CardInstanceState card)
        {
            var side = TargetPickSide.None;

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!ActionRequiresCharacterPick(action))
                    continue;

                var actionSide = GetPickSideForTarget(action.Target);
                side = MergePickSide(side, actionSide);
            }

            return side;
        }

        public static List<CombatantState> GetValidTargetCandidates(
            BattleState state,
            CardInstanceState card,
            CombatantState owner)
        {
            var result = new List<CombatantState>();
            if (owner == null)
                return result;

            var side = GetRequiredTargetPick(card);
            if (side == TargetPickSide.None)
                return result;

            if (side == TargetPickSide.Enemy || side == TargetPickSide.Ally)
            {
                var team = side == TargetPickSide.Enemy
                    ? (owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player)
                    : owner.Team;

                foreach (var c in state.GetTeam(team))
                {
                    if (c.IsAlive)
                        result.Add(c);
                }
            }

            return result;
        }

        public static bool RequiresManualTarget(CardInstanceState card) =>
            GetRequiredTargetPick(card) != TargetPickSide.None;

        static bool ActionRequiresCharacterPick(EffectActionSpec action)
        {
            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                case EffectActionType.ApplyStatus:
                case EffectActionType.RemoveStatus:
                    return IsDirectedCharacterTarget(action.Target);
                case EffectActionType.Heal:
                case EffectActionType.GainBlock:
                    return action.Target != EffectTarget.Self && IsDirectedCharacterTarget(action.Target);
                default:
                    return false;
            }
        }

        static bool IsDirectedCharacterTarget(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.DefaultEnemy:
                case EffectTarget.ManualSelected:
                case EffectTarget.FrontAlly:
                case EffectTarget.BackAlly:
                    return true;
                default:
                    return false;
            }
        }

        static TargetPickSide GetPickSideForTarget(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.DefaultEnemy:
                case EffectTarget.ManualSelected:
                    return TargetPickSide.Enemy;
                case EffectTarget.FrontAlly:
                case EffectTarget.BackAlly:
                    return TargetPickSide.Ally;
                default:
                    return TargetPickSide.None;
            }
        }

        static TargetPickSide MergePickSide(TargetPickSide current, TargetPickSide next)
        {
            if (current == TargetPickSide.None)
                return next;
            if (next == TargetPickSide.None || current == next)
                return current;
            return current;
        }
    }
}
