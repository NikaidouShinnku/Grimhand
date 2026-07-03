using System.Collections.Generic;
using Grimhand.Battle.Effects;
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

        /// <summary>诅咒牌（如混沌之触）：占用牌库/手牌位但无法被打出，只能通过弃牌/摧毁事件移除。</summary>
        public static bool IsCurseCard(CardInstanceState card)
        {
            if (card == null)
                return false;
            if (card.Keywords != null && card.Keywords.Contains("curse"))
                return true;
            return !string.IsNullOrEmpty(card.DefinitionId) && card.DefinitionId.StartsWith("curse_");
        }

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
                    if (!c.IsAlive)
                        continue;

                    if (!TargetReachRules.CanPickUnit(state, card, c, owner))
                        continue;

                    result.Add(c);
                }
            }

            return result;
        }

        public static bool RequiresManualTarget(CardInstanceState card) =>
            GetRequiredTargetPick(card) != TargetPickSide.None;

        public static bool ActionRequiresCharacterPickForReach(EffectActionSpec action)
        {
            if (action == null || !IsDirectedCharacterTarget(action.Target))
                return false;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                case EffectActionType.ApplyStatus:
                case EffectActionType.RemoveStatus:
                case EffectActionType.Heal:
                case EffectActionType.GainBlock:
                case EffectActionType.ConsumeBlockDealDamage:
                case EffectActionType.DamagePerRespondCount:
                case EffectActionType.DealDamageScaledByActorHpLoss:
                case EffectActionType.DealDamageAlternateIfHealedThisTurn:
                case EffectActionType.DealDamageBonusPerTargetDebuffStack:
                case EffectActionType.ApplyPoisonBySpeedCompare:
                case EffectActionType.ApplyConstrict:
                case EffectActionType.ApplyDelayedDamage:
                case EffectActionType.DoubleStatusStacks:
                case EffectActionType.SealNextEnemyCard:
                    return true;
                default:
                    return false;
            }
        }

        static bool ActionRequiresCharacterPick(EffectActionSpec action) =>
            ActionRequiresCharacterPickForReach(action);

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

        /// <summary>敌人规划：Reach 范围内至少有一名合法目标时才可入选。</summary>
        public static bool CardHasPlayableTargets(BattleState state, CardInstanceState card, CombatantState owner)
        {
            if (state == null || card == null || owner == null || !owner.IsAlive)
                return false;

            var hasTargetedAction = false;
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!ActionNeedsEnemyReachCheck(action))
                    continue;

                hasTargetedAction = true;
                if (HasReachTarget(state, owner, action))
                    return true;
            }

            return !hasTargetedAction;
        }

        static bool ActionNeedsEnemyReachCheck(EffectActionSpec action)
        {
            if (action == null)
                return false;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                case EffectActionType.ApplyStatus:
                case EffectActionType.RemoveStatus:
                case EffectActionType.ConsumeBlockDealDamage:
                case EffectActionType.DealDamageScaledByActorHpLoss:
                case EffectActionType.DealDamageAlternateIfHealedThisTurn:
                case EffectActionType.DealDamageBonusPerTargetDebuffStack:
                    break;
                default:
                    return false;
            }

            return action.Target is EffectTarget.DefaultEnemy
                or EffectTarget.ManualSelected
                or EffectTarget.AllEnemies
                or EffectTarget.RandomEnemy
                or EffectTarget.RandomEnemies
                or EffectTarget.EnemyFrontSlot
                or EffectTarget.EnemyMiddleSlot
                or EffectTarget.EnemyBackSlot;
        }

        static bool HasReachTarget(BattleState state, CombatantState owner, EffectActionSpec action)
        {
            if (action.Target == EffectTarget.AllEnemies)
            {
                var enemyTeam = owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                foreach (var unit in state.GetTeam(enemyTeam))
                {
                    if (unit.IsAlive && TargetReachRules.IsSlotAllowed(
                            action.Reach, PositionRules.GetEffectiveSlot(state, unit)))
                        return true;
                }

                return false;
            }

            if (action.Target == EffectTarget.RandomEnemy || action.Target == EffectTarget.RandomEnemies)
            {
                var enemyTeam = owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                foreach (var unit in PositionRules.GetAliveSortedByPhysicalSlot(state, enemyTeam))
                {
                    if (TargetReachRules.IsSlotAllowed(
                            action.Reach, PositionRules.GetEffectiveSlot(state, unit)))
                        return true;
                }

                return false;
            }

            var targetTeam = owner.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            foreach (var unit in state.GetTeam(targetTeam))
            {
                if (!unit.IsAlive)
                    continue;

                if (!TargetReachRules.IsSlotAllowed(action.Reach, PositionRules.GetEffectiveSlot(state, unit)))
                    continue;

                if (TargetRules.IsTargetValidForAction(state, unit, action.Reach, action))
                    return true;
            }

            return false;
        }
    }
}
