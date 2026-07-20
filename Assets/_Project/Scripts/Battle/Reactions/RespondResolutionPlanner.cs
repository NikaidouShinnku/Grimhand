using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Reactions
{
    public sealed class ScheduledResolution
    {
        public ResolutionStep Step { get; set; }
        /// <summary>应对步：配对的攻击上下文。</summary>
        public RespondTriggerContext? RespondContext { get; set; }
        public bool ApplyConditionalEffects { get; set; } = true;
        /// <summary>攻击步：配对的唯一应对牌 InstanceId（严格 1:1，0=无）。</summary>
        public int PairedRespondCardInstanceId { get; set; }
        /// <summary>应对步：减伤已在配对攻击前预武装，出牌时勿重复武装。</summary>
        public bool MitigationWasPreArmed { get; set; }
    }

    /// <summary>
    /// 应对调度：每张应对 ↔ 一张打到自己的攻击，严格 1:1。
    /// 一张攻击最多只被一张应对占用；多余应对失败（仅无条件效果）。
    /// 权威规范：Assets/_Project/Docs/RespondCombat_Spec.md
    /// </summary>
    public static class RespondResolutionPlanner
    {
        public static List<ScheduledResolution> BuildSchedule(
            BattleState state,
            IReadOnlyList<ResolutionStep> baseline)
        {
            var result = new List<ScheduledResolution>();
            if (state == null || baseline == null || baseline.Count == 0)
                return result;

            // attackCardId → 唯一配对的应对 step
            var respondForAttack = new Dictionary<int, ResolutionStep>();
            var pairedRespondIds = new HashSet<int>();
            var claimedAttackIds = new HashSet<int>();

            PairRespondsToAttacks(
                state, baseline, TeamSide.Player, respondForAttack, pairedRespondIds, claimedAttackIds);
            PairRespondsToAttacks(
                state, baseline, TeamSide.Enemy, respondForAttack, pairedRespondIds, claimedAttackIds);

            var emitted = new HashSet<int>();

            for (var i = 0; i < baseline.Count; i++)
            {
                var step = baseline[i];
                if (emitted.Contains(step.CardInstanceId))
                    continue;

                var actor = state.GetCombatant(step.CombatantId);
                var card = state.GetCard(step.CardInstanceId);

                if (respondForAttack.TryGetValue(step.CardInstanceId, out var respondStep))
                {
                    if (actor != null && actor.IsAlive)
                    {
                        emitted.Add(step.CardInstanceId);
                        result.Add(new ScheduledResolution
                        {
                            Step = step,
                            ApplyConditionalEffects = true,
                            PairedRespondCardInstanceId = respondStep.CardInstanceId
                        });
                    }

                    if (!emitted.Contains(respondStep.CardInstanceId))
                    {
                        emitted.Add(respondStep.CardInstanceId);
                        result.Add(new ScheduledResolution
                        {
                            Step = respondStep,
                            RespondContext = RespondTriggerContext.FromStep(state, step),
                            ApplyConditionalEffects = true,
                            MitigationWasPreArmed = true
                        });
                    }

                    continue;
                }

                if (RespondRules.IsRespondCard(card) && actor != null)
                {
                    if (pairedRespondIds.Contains(step.CardInstanceId))
                        continue;

                    emitted.Add(step.CardInstanceId);
                    result.Add(new ScheduledResolution
                    {
                        Step = step,
                        ApplyConditionalEffects = false
                    });
                    continue;
                }

                emitted.Add(step.CardInstanceId);
                result.Add(new ScheduledResolution { Step = step });
            }

            return result;
        }

        static void PairRespondsToAttacks(
            BattleState state,
            IReadOnlyList<ResolutionStep> baseline,
            TeamSide respondTeam,
            Dictionary<int, ResolutionStep> respondForAttack,
            HashSet<int> pairedRespondIds,
            HashSet<int> claimedAttackIds)
        {
            var respondEntries = new List<ResolutionStep>();
            for (var i = 0; i < baseline.Count; i++)
            {
                var step = baseline[i];
                var card = state.GetCard(step.CardInstanceId);
                var actor = state.GetCombatant(step.CombatantId);
                if (!RespondRules.IsRespondCard(card) || actor == null || actor.Team != respondTeam)
                    continue;
                respondEntries.Add(step);
            }

            // 按计划出牌顺序：先出的应对优先抢走第一张合法攻击
            respondEntries.Sort((a, b) =>
            {
                var plan = respondTeam == TeamSide.Player ? state.PlayerPlan : state.EnemyPlan;
                return IndexInPlan(plan, a.CardInstanceId).CompareTo(IndexInPlan(plan, b.CardInstanceId));
            });

            foreach (var respondStep in respondEntries)
            {
                if (pairedRespondIds.Contains(respondStep.CardInstanceId))
                    continue;

                for (var i = 0; i < baseline.Count; i++)
                {
                    var attackStep = baseline[i];
                    if (claimedAttackIds.Contains(attackStep.CardInstanceId))
                        continue;

                    var attacker = state.GetCombatant(attackStep.CombatantId);
                    if (attacker == null)
                        continue;

                    if (respondTeam == TeamSide.Player)
                    {
                        if (attacker.Team != TeamSide.Enemy
                            || !RespondTriggerMatcher.EnemyStepTriggersPlayerRespond(state, attackStep))
                            continue;
                    }
                    else
                    {
                        if (attacker.Team != TeamSide.Player
                            || !RespondTriggerMatcher.PlayerStepTriggersEnemyRespond(state, attackStep))
                            continue;
                    }

                    var owner = state.GetCombatant(respondStep.CombatantId);
                    var respondCard = state.GetCard(respondStep.CardInstanceId);
                    var matches = respondTeam == TeamSide.Player
                        ? RespondTriggerMatcher.RespondCardMatchesEnemyStep(
                            state, owner, respondCard, attackStep)
                        : RespondTriggerMatcher.RespondCardMatchesPlayerStep(
                            state, owner, respondCard, attackStep);
                    if (!matches)
                        continue;

                    // 严格 1:1 — 这张攻击已被占用，其它应对不能再配
                    respondForAttack[attackStep.CardInstanceId] = respondStep;
                    claimedAttackIds.Add(attackStep.CardInstanceId);
                    pairedRespondIds.Add(respondStep.CardInstanceId);
                    break;
                }
            }
        }

        static int IndexInPlan(BattlePlan plan, int cardInstanceId)
        {
            if (plan?.PlayQueue == null)
                return int.MaxValue;

            for (var i = 0; i < plan.PlayQueue.Count; i++)
            {
                if (plan.PlayQueue[i] == cardInstanceId)
                    return i;
            }

            return int.MaxValue;
        }
    }
}
