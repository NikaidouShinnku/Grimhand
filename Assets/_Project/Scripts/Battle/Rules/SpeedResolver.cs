using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public readonly struct ResolutionStep
    {
        public ResolutionStep(string combatantId, int cardInstanceId, int roundIndex)
        {
            CombatantId = combatantId;
            CardInstanceId = cardInstanceId;
            RoundIndex = roundIndex;
        }

        public string CombatantId { get; }
        public int CardInstanceId { get; }
        public int RoundIndex { get; }
    }

    public static class SpeedResolver
    {
        public static List<ResolutionStep> BuildResolutionOrder(
            BattleState state,
            BattlePlan playerPlan,
            BattlePlan enemyPlan,
            BattleRng rng)
        {
            var queueByCombatant = new Dictionary<string, Queue<int>>();

            void EnqueuePlan(BattlePlan plan, TeamSide team)
            {
                foreach (var instanceId in plan.PlayQueue)
                {
                    var card = state.GetCard(instanceId);
                    if (card == null)
                        continue;

                    var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                    if (ownerId == null)
                        continue;

                    var owner = state.GetCombatant(ownerId);
                    if (owner == null || owner.Team != team || !owner.IsAlive)
                        continue;

                    if (!queueByCombatant.TryGetValue(ownerId, out var queue))
                    {
                        queue = new Queue<int>();
                        queueByCombatant[ownerId] = queue;
                    }

                    queue.Enqueue(instanceId);
                }
            }

            EnqueuePlan(playerPlan, TeamSide.Player);
            EnqueuePlan(enemyPlan, TeamSide.Enemy);

            var steps = new List<ResolutionStep>();
            var round = 0;
            var hasMore = true;

            while (hasMore)
            {
                hasMore = false;
                var actors = new List<CombatantState>();

                foreach (var c in state.Combatants)
                {
                    if (!c.IsAlive)
                        continue;

                    if (!queueByCombatant.TryGetValue(c.Id, out var q) || q.Count == 0)
                        continue;

                    actors.Add(c);
                    hasMore = true;
                }

                if (!hasMore)
                    break;

                var ordered = OrderByEffectiveSpeed(state, actors, rng);
                foreach (var actor in ordered)
                {
                    var q = queueByCombatant[actor.Id];
                    if (q.Count == 0)
                        continue;

                    var cardId = q.Dequeue();
                    steps.Add(new ResolutionStep(actor.Id, cardId, round));
                }

                round++;
            }

            return steps;
        }

        public static List<CombatantState> OrderByEffectiveSpeed(BattleState state, List<CombatantState> actors, BattleRng rng)
        {
            var groups = new Dictionary<int, List<CombatantState>>();
            foreach (var actor in actors)
            {
                var speed = StatusRules.GetEffectiveSpeed(state, actor);
                if (!groups.TryGetValue(speed, out var list))
                {
                    list = new List<CombatantState>();
                    groups[speed] = list;
                }

                list.Add(actor);
            }

            var speeds = new List<int>(groups.Keys);
            speeds.Sort((a, b) => b.CompareTo(a));

            var result = new List<CombatantState>();
            foreach (var speed in speeds)
            {
                var list = groups[speed];
                ShuffleInPlace(list, rng);
                result.AddRange(list);
            }

            return result;
        }

        public static Dictionary<string, Queue<int>> BuildPlayQueues(
            BattleState state,
            BattlePlan playerPlan,
            BattlePlan enemyPlan)
        {
            var queueByCombatant = new Dictionary<string, Queue<int>>();

            void EnqueuePlan(BattlePlan plan, TeamSide team)
            {
                foreach (var instanceId in plan.PlayQueue)
                {
                    var card = state.GetCard(instanceId);
                    if (card == null)
                        continue;

                    var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                    if (ownerId == null)
                        continue;

                    var owner = state.GetCombatant(ownerId);
                    if (owner == null || owner.Team != team || !owner.IsAlive)
                        continue;

                    if (!queueByCombatant.TryGetValue(ownerId, out var queue))
                    {
                        queue = new Queue<int>();
                        queueByCombatant[ownerId] = queue;
                    }

                    queue.Enqueue(instanceId);
                }
            }

            EnqueuePlan(playerPlan, TeamSide.Player);
            EnqueuePlan(enemyPlan, TeamSide.Enemy);
            return queueByCombatant;
        }

        public static List<CombatantState> OrderBySpeedWithTiebreak(List<CombatantState> actors, BattleRng rng)
        {
            var groups = new Dictionary<int, List<CombatantState>>();
            foreach (var actor in actors)
            {
                if (!groups.TryGetValue(actor.Speed, out var list))
                {
                    list = new List<CombatantState>();
                    groups[actor.Speed] = list;
                }

                list.Add(actor);
            }

            var speeds = new List<int>(groups.Keys);
            speeds.Sort((a, b) => b.CompareTo(a));

            var result = new List<CombatantState>();
            foreach (var speed in speeds)
            {
                var list = groups[speed];
                ShuffleInPlace(list, rng);
                result.AddRange(list);
            }

            return result;
        }

        static void ShuffleInPlace(List<CombatantState> list, BattleRng rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.NextIndex(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
