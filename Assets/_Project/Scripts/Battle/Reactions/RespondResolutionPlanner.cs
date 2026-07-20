using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Reactions
{
    public sealed class ScheduledResolution
    {
        public ResolutionStep Step { get; set; }
        public RespondTriggerContext? RespondContext { get; set; }
        public bool ApplyConditionalEffects { get; set; } = true;
    }

    public static class RespondResolutionPlanner
    {
        public static List<ScheduledResolution> BuildSchedule(
            BattleState state,
            IReadOnlyList<ResolutionStep> baseline)
        {
            var result = new List<ScheduledResolution>();
            var consumed = new HashSet<int>();
            var playerRespondEntries = new List<(int index, ResolutionStep step)>();
            var enemyRespondEntries = new List<(int index, ResolutionStep step)>();

            for (var i = 0; i < baseline.Count; i++)
            {
                var step = baseline[i];
                var card = state.GetCard(step.CardInstanceId);
                var actor = state.GetCombatant(step.CombatantId);
                if (!RespondRules.IsRespondCard(card) || actor == null)
                    continue;

                if (actor.Team == TeamSide.Player)
                    playerRespondEntries.Add((i, step));
                else if (actor.Team == TeamSide.Enemy)
                    enemyRespondEntries.Add((i, step));
            }

            for (var i = 0; i < baseline.Count; i++)
            {
                var step = baseline[i];
                if (consumed.Contains(step.CardInstanceId))
                    continue;

                var actor = state.GetCombatant(step.CombatantId);
                var card = state.GetCard(step.CardInstanceId);

                if (actor?.Team == TeamSide.Enemy
                    && RespondTriggerMatcher.EnemyStepTriggersPlayerRespond(state, step))
                {
                    var matching = CollectMatchingPlayerResponds(
                        state, playerRespondEntries, consumed, step);
                    if (matching.Count > 0)
                    {
                        var context = RespondTriggerContext.FromStep(state, step);
                        foreach (var respondStep in matching)
                        {
                            consumed.Add(respondStep.CardInstanceId);
                            result.Add(new ScheduledResolution
                            {
                                Step = respondStep,
                                RespondContext = context,
                                ApplyConditionalEffects = true
                            });
                        }

                        if (actor.IsAlive)
                        {
                            result.Add(new ScheduledResolution
                            {
                                Step = step,
                                ApplyConditionalEffects = true
                            });
                        }

                        continue;
                    }
                }

                if (actor?.Team == TeamSide.Player
                    && RespondTriggerMatcher.PlayerStepTriggersEnemyRespond(state, step))
                {
                    var matching = CollectMatchingEnemyResponds(
                        state, enemyRespondEntries, consumed, step);
                    if (matching.Count > 0)
                    {
                        var context = RespondTriggerContext.FromStep(state, step);
                        foreach (var respondStep in matching)
                        {
                            consumed.Add(respondStep.CardInstanceId);
                            result.Add(new ScheduledResolution
                            {
                                Step = respondStep,
                                RespondContext = context,
                                ApplyConditionalEffects = true
                            });
                        }

                        if (actor.IsAlive)
                        {
                            result.Add(new ScheduledResolution
                            {
                                Step = step,
                                ApplyConditionalEffects = true
                            });
                        }

                        continue;
                    }
                }

                if (RespondRules.IsRespondCard(card) && actor != null)
                {
                    var hasMatch = actor.Team == TeamSide.Player
                        ? RespondTriggerMatcher.AnyMatchingEnemyStep(state, actor, card, baseline)
                        : RespondTriggerMatcher.AnyMatchingPlayerStep(state, actor, card, baseline);

                    if (!hasMatch)
                    {
                        result.Add(new ScheduledResolution
                        {
                            Step = step,
                            ApplyConditionalEffects = false
                        });
                    }

                    continue;
                }

                result.Add(new ScheduledResolution { Step = step });
            }

            return result;
        }

        static List<ResolutionStep> CollectMatchingPlayerResponds(
            BattleState state,
            List<(int index, ResolutionStep step)> respondEntries,
            HashSet<int> consumed,
            ResolutionStep enemyStep)
        {
            var matching = new List<(int index, ResolutionStep step)>();
            foreach (var entry in respondEntries)
            {
                if (consumed.Contains(entry.step.CardInstanceId))
                    continue;

                var owner = state.GetCombatant(entry.step.CombatantId);
                var card = state.GetCard(entry.step.CardInstanceId);
                if (!RespondTriggerMatcher.RespondCardMatchesEnemyStep(state, owner, card, enemyStep))
                    continue;

                matching.Add(entry);
            }

            matching.Sort((a, b) =>
            {
                var ai = IndexInPlan(state.PlayerPlan, a.step.CardInstanceId);
                var bi = IndexInPlan(state.PlayerPlan, b.step.CardInstanceId);
                return ai.CompareTo(bi);
            });

            return ToSteps(matching);
        }

        static List<ResolutionStep> CollectMatchingEnemyResponds(
            BattleState state,
            List<(int index, ResolutionStep step)> respondEntries,
            HashSet<int> consumed,
            ResolutionStep playerStep)
        {
            var matching = new List<(int index, ResolutionStep step)>();
            foreach (var entry in respondEntries)
            {
                if (consumed.Contains(entry.step.CardInstanceId))
                    continue;

                var owner = state.GetCombatant(entry.step.CombatantId);
                var card = state.GetCard(entry.step.CardInstanceId);
                if (!RespondTriggerMatcher.RespondCardMatchesPlayerStep(state, owner, card, playerStep))
                    continue;

                matching.Add(entry);
            }

            matching.Sort((a, b) =>
            {
                var ai = IndexInPlan(state.EnemyPlan, a.step.CardInstanceId);
                var bi = IndexInPlan(state.EnemyPlan, b.step.CardInstanceId);
                return ai.CompareTo(bi);
            });

            return ToSteps(matching);
        }

        static List<ResolutionStep> ToSteps(List<(int index, ResolutionStep step)> matching)
        {
            var steps = new List<ResolutionStep>(matching.Count);
            foreach (var pair in matching)
                steps.Add(pair.step);
            return steps;
        }

        static int IndexInPlan(BattlePlan plan, int cardInstanceId)
        {
            if (plan == null)
                return int.MaxValue;

            var index = plan.PlayQueue.IndexOf(cardInstanceId);
            return index < 0 ? int.MaxValue : index;
        }
    }
}
