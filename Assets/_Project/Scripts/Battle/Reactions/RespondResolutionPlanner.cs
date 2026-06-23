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
            var respondEntries = new List<(int index, ResolutionStep step)>();

            for (var i = 0; i < baseline.Count; i++)
            {
                var step = baseline[i];
                var card = state.GetCard(step.CardInstanceId);
                var actor = state.GetCombatant(step.CombatantId);
                if (actor?.Team == TeamSide.Player && RespondRules.IsRespondCard(card))
                    respondEntries.Add((i, step));
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
                    var matching = CollectMatchingResponds(state, respondEntries, consumed, step);
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

                if (actor?.Team == TeamSide.Player && RespondRules.IsRespondCard(card))
                {
                    if (!RespondTriggerMatcher.AnyMatchingEnemyStep(state, actor, card, baseline))
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

        static List<ResolutionStep> CollectMatchingResponds(
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
                var ai = IndexInPlayerPlan(state, a.step.CardInstanceId);
                var bi = IndexInPlayerPlan(state, b.step.CardInstanceId);
                return ai.CompareTo(bi);
            });

            var steps = new List<ResolutionStep>(matching.Count);
            foreach (var pair in matching)
                steps.Add(pair.step);

            return steps;
        }

        static int IndexInPlayerPlan(BattleState state, int cardInstanceId)
        {
            var index = state.PlayerPlan.PlayQueue.IndexOf(cardInstanceId);
            return index < 0 ? int.MaxValue : index;
        }
    }
}
