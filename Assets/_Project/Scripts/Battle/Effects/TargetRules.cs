using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Battle.Effects
{
    public static class TargetRules
    {
        public static CombatantState ResolveTarget(
            BattleState state,
            CombatantState actor,
            EffectTarget targetKind,
            int cardInstanceId,
            BattleRng rng = null,
            EffectActionSpec action = null) =>
            ResolveTarget(state, actor, targetKind, cardInstanceId, rng, action?.Reach ?? TargetReach.Any, action);

        public static CombatantState ResolveTarget(
            BattleState state,
            CombatantState actor,
            EffectTarget targetKind,
            int cardInstanceId,
            BattleRng rng,
            TargetReach reach,
            EffectActionSpec action)
        {
            switch (targetKind)
            {
                case EffectTarget.Self:
                    return actor;
                case EffectTarget.RandomEnemy:
                    return PickRandomEnemy(state, actor.Team, rng);
                case EffectTarget.RandomAlly:
                    return PickRandomAlly(state, actor.Team, rng);
                case EffectTarget.DefaultEnemy:
                case EffectTarget.ManualSelected:
                    if (TryGetSelectedTarget(state, cardInstanceId, out var picked)
                        && IsTargetValidForAction(state, picked, reach, action))
                    {
                        return picked;
                    }

                    if (UsesAutoReachRoll(action))
                    {
                        var rolled = PickRandomTargetForReach(state, actor.Team, reach, action, rng);
                        if (rolled != null)
                        {
                            state.ResolutionTargets[cardInstanceId] = rolled.Id;
                            return rolled;
                        }

                        return null;
                    }

                    return PositionRules.PickDefaultTarget(state, actor.Team);
                case EffectTarget.FrontAlly:
                case EffectTarget.BackAlly:
                    if (TryGetSelectedTarget(state, cardInstanceId, out var allyPicked))
                        return allyPicked;
                    return targetKind == EffectTarget.FrontAlly
                        ? PickAllyBySlotOffset(state, actor, -1)
                        : PickAllyBySlotOffset(state, actor, 1);
                case EffectTarget.EnemyFrontSlot:
                    return PositionRules.PickCombatantInSlot(state, OppositeTeam(actor.Team), FormationSlot.Front);
                case EffectTarget.EnemyMiddleSlot:
                    return PositionRules.PickCombatantInSlot(state, OppositeTeam(actor.Team), FormationSlot.Middle);
                case EffectTarget.EnemyBackSlot:
                    return PositionRules.PickCombatantInSlot(state, OppositeTeam(actor.Team), FormationSlot.Back);
                case EffectTarget.AllyFrontSlot:
                    return PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Front);
                case EffectTarget.AllyMiddleSlot:
                    return PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Middle);
                case EffectTarget.AllyBackSlot:
                    return PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Back);
                case EffectTarget.LastActionActor:
                    return state.GetCombatant(state.LastAction.ActorId);
                default:
                    return PositionRules.PickDefaultTarget(state, actor.Team);
            }
        }

        public static bool IsTargetValidForAction(
            BattleState state,
            CombatantState target,
            TargetReach reach,
            EffectActionSpec action)
        {
            if (target == null || !target.IsAlive)
                return false;

            if (action != null && UsesExplicitSlotTarget(action.Target))
                return true;

            if (action == null || reach == TargetReach.Any)
                return true;

            if (action.Type != EffectActionType.DealDamage
                && action.Type != EffectActionType.ApplyStatus
                && action.Type != EffectActionType.RemoveStatus)
            {
                return true;
            }

            var slot = PositionRules.GetEffectiveSlot(state, target);
            return TargetReachRules.IsSlotAllowed(reach, slot);
        }

        public static bool UsesExplicitSlotTarget(EffectTarget targetKind) =>
            targetKind is EffectTarget.AllyFrontSlot
                or EffectTarget.AllyMiddleSlot
                or EffectTarget.AllyBackSlot
                or EffectTarget.EnemyFrontSlot
                or EffectTarget.EnemyMiddleSlot
                or EffectTarget.EnemyBackSlot
                or EffectTarget.Self;

        static CombatantState PickRandomTargetForReach(
            BattleState state,
            TeamSide attackerTeam,
            TargetReach reach,
            EffectActionSpec action,
            BattleRng rng)
        {
            if (action == null)
                return null;

            if (action.Type != EffectActionType.DealDamage
                && action.Type != EffectActionType.ApplyStatus
                && action.Type != EffectActionType.RemoveStatus)
            {
                return null;
            }

            var candidates = CollectReachCandidates(state, attackerTeam, reach, action);
            return PickRandomCandidate(candidates, rng);
        }

        static List<CombatantState> CollectReachCandidates(
            BattleState state,
            TeamSide attackerTeam,
            TargetReach reach,
            EffectActionSpec action)
        {
            var result = new List<CombatantState>();
            if (state == null || action == null)
                return result;

            var targetTeam = OppositeTeam(attackerTeam);
            var taunt = CombatMechanicsRules.FindTauntHolder(state, targetTeam);
            if (taunt != null
                && TargetReachRules.IsSlotAllowed(reach, PositionRules.GetEffectiveSlot(state, taunt)))
            {
                result.Add(taunt);
                return result;
            }

            foreach (var unit in PositionRules.GetAliveSortedByPhysicalSlot(state, targetTeam))
            {
                if (TargetReachRules.IsSlotAllowed(reach, PositionRules.GetEffectiveSlot(state, unit)))
                    result.Add(unit);
            }

            return result;
        }

        static CombatantState PickRandomCandidate(IReadOnlyList<CombatantState> candidates, BattleRng rng)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
                return candidates[0];

            if (rng == null)
                return candidates[0];

            return candidates[rng.NextIndex(candidates.Count)];
        }

        static bool TryGetSelectedTarget(BattleState state, int cardInstanceId, out CombatantState target)
        {
            target = null;
            if (!state.ResolutionTargets.TryGetValue(cardInstanceId, out var targetId))
                return false;

            target = state.GetCombatant(targetId);
            return target != null && target.IsAlive;
        }

        static TeamSide OppositeTeam(TeamSide team) =>
            team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;

        static CombatantState PickAllyBySlotOffset(BattleState state, CombatantState actor, int slotOffset)
        {
            var desired = (int)actor.Slot + slotOffset;
            if (desired < 1) desired = 1;
            if (desired > 3) desired = 3;

            foreach (var ally in state.GetTeam(actor.Team))
            {
                if (ally.IsAlive && (int)ally.Slot == desired)
                    return ally;
            }

            return actor;
        }

        static CombatantState PickRandomEnemy(BattleState state, TeamSide actorTeam, BattleRng rng)
        {
            var targetTeam = actorTeam == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var candidates = PositionRules.GetAliveSortedByPhysicalSlot(state, targetTeam);
            return PickRandomCandidate(candidates, rng);
        }

        static CombatantState PickRandomAlly(BattleState state, TeamSide actorTeam, BattleRng rng)
        {
            var candidates = PositionRules.GetAliveSortedByPhysicalSlot(state, actorTeam);
            return PickRandomCandidate(candidates, rng);
        }

        /// <summary>敌方规划阶段预掷自动选敌，保证意图预览与结算一致。</summary>
        public static void PrerollEnemyAutoTargets(BattleState state, BattlePlan enemyPlan, BattleRng rng)
        {
            if (state == null || enemyPlan?.PlayQueue == null || rng == null)
                return;

            foreach (var cardInstanceId in enemyPlan.PlayQueue)
            {
                if (state.ResolutionTargets.ContainsKey(cardInstanceId))
                    continue;

                var card = state.GetCard(cardInstanceId);
                var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                var actor = ownerId != null ? state.GetCombatant(ownerId) : null;
                if (card == null || actor == null || !actor.IsAlive)
                    continue;

                var target = RollPrimaryAutoTarget(state, actor, card, rng);
                if (target != null)
                    state.ResolutionTargets[cardInstanceId] = target.Id;
            }
        }

        static CombatantState RollPrimaryAutoTarget(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            BattleRng rng)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (!UsesAutoReachRoll(action))
                    continue;

                var target = PickRandomTargetForReach(
                    state, actor.Team, action.Reach, action, rng);
                if (target != null)
                    return target;
            }

            return null;
        }

        static bool UsesAutoReachRoll(EffectActionSpec action)
        {
            if (action == null)
                return false;

            switch (action.Target)
            {
                case EffectTarget.DefaultEnemy:
                case EffectTarget.ManualSelected:
                    return action.Type is EffectActionType.DealDamage
                        or EffectActionType.ApplyStatus
                        or EffectActionType.RemoveStatus;
                default:
                    return false;
            }
        }

        public static List<CombatantState> PickRandomEnemies(
            BattleState state,
            TeamSide actorTeam,
            int count,
            BattleRng rng)
        {
            var result = new List<CombatantState>();
            if (state == null || rng == null || count <= 0)
                return result;

            var targetTeam = actorTeam == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var pool = new List<CombatantState>(PositionRules.GetAliveSortedByPhysicalSlot(state, targetTeam));
            while (result.Count < count && pool.Count > 0)
            {
                var index = rng.NextIndex(pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        /// <summary>规划阶段预览敌方意图时，优先使用本回合已预掷的目标。</summary>
        public static CombatantState PredictIntentTarget(
            BattleState state,
            CombatantState actor,
            CardInstanceState card)
        {
            if (state == null || actor == null || card == null)
                return null;

            if (state.ResolutionTargets.TryGetValue(card.InstanceId, out var assignedId))
            {
                var assigned = state.GetCombatant(assignedId);
                if (assigned != null && assigned.IsAlive)
                    return assigned;
            }

            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                if (action.Target is EffectTarget.AllEnemies or EffectTarget.RandomEnemies or EffectTarget.RandomEnemy)
                    continue;

                if (action.Type == EffectActionType.GainBlock && action.Target == EffectTarget.Self)
                    return actor;

                var target = ResolveTarget(
                    state, actor, action.Target, card.InstanceId, null, action.Reach, action);
                if (target != null)
                    return target;
            }

            return null;
        }
    }
}
