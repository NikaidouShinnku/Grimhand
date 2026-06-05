using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;

namespace Grimhand.Battle.AI
{
    public sealed class EnemyTurnPlanResult
    {
        public BattlePlan Plan { get; } = new();
        public List<EnemyIntentSlot> Intents { get; } = new();
    }

    public static class EnemyTurnPlanner
    {
        public static EnemyTurnPlanResult PrepareEnemyTurn(BattleState state, BattleRng rng, int? energyBudget = null)
        {
            var result = new EnemyTurnPlanResult();
            var budget = energyBudget ?? state.Config?.TurnStartEnergyRegen ?? EnergyRules.DefaultTurnRegen;
            var spent = 0;

            var candidates = new List<CardInstanceState>();
            foreach (var card in state.EnemyHand)
            {
                if (!card.IsUsable)
                    continue;

                var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                if (owner == null || owner.Team != TeamSide.Enemy || !owner.IsAlive)
                    continue;

                candidates.Add(card);
            }

            candidates.Sort((a, b) =>
            {
                var costCmp = a.Cost.CompareTo(b.Cost);
                if (costCmp != 0)
                    return costCmp;

                var nameCmp = string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
                return nameCmp != 0 ? nameCmp : a.InstanceId.CompareTo(b.InstanceId);
            });

            foreach (var card in candidates)
            {
                if (spent + card.Cost > budget)
                    continue;

                result.Plan.PlayQueue.Add(card.InstanceId);
                spent += card.Cost;
            }

            result.Plan.EnergySpent = spent;

            for (var i = 0; i < result.Plan.PlayQueue.Count; i++)
            {
                var cardId = result.Plan.PlayQueue[i];
                var card = state.GetCard(cardId);
                if (card == null)
                    continue;

                var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                var hidden = ShouldHideIntent(result.Plan.PlayQueue.Count, i, rng);

                result.Intents.Add(new EnemyIntentSlot
                {
                    CardInstanceId = cardId,
                    OwnerCombatantId = ownerId ?? "",
                    IsHidden = hidden,
                    OrderIndex = i
                });
            }

            return result;
        }

        static bool ShouldHideIntent(int totalCards, int index, BattleRng rng)
        {
            if (totalCards < 3)
                return false;

            if (index == 1)
                return true;

            return totalCards >= 4 && index == 2 && rng.NextInt(0, 2) == 0;
        }
    }
}
