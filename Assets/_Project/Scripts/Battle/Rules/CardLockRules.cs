using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CardLockRules
    {
        public static void ApplyLock(CombatantState actor, int turnsRemaining)
        {
            if (actor == null)
                return;

            actor.CardsLockedTurnsRemaining = System.Math.Max(
                actor.CardsLockedTurnsRemaining,
                System.Math.Max(1, turnsRemaining));
        }

        public static void ProcessTurnStart(CombatantState combatant)
        {
            if (combatant == null || combatant.CardsLockedTurnsRemaining <= 0)
                return;

            combatant.CardsLockedTurnsRemaining--;
        }

        public static bool AppliesSelfLock(CardInstanceState card)
        {
            if (card?.Actions == null)
                return false;

            foreach (var action in card.Actions)
            {
                if (action?.Type == EffectActionType.LockSelfCards)
                    return true;
            }

            return false;
        }

        public static bool ShouldSkipPlayerCard(CombatantState actor, CardInstanceState card)
        {
            return actor != null
                   && card != null
                   && actor.Team == TeamSide.Player
                   && actor.IsCardsLocked;
        }

        public static void SkipLockedPlayerCard(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            RefundEnergy(state, card, events);
            events.Add(new BattleEvent(
                BattleEventKind.ReactionTriggered,
                $"{actor.DisplayName} 出牌被锁定，{card.DisplayName} 未生效")
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId
            });
            DeckRules.MovePlayedCardToDiscard(state, actor.Team, card, events);
            events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded, card.DisplayName)
            {
                CombatantId = actor.Id,
                CardInstanceId = card.InstanceId
            });
        }

        static void RefundEnergy(BattleState state, CardInstanceState card, List<BattleEvent> events)
        {
            if (state == null || card == null)
                return;

            var refund = 0;
            if (state.PlayerPlan?.EnergySpentPerCard != null
                && state.PlayerPlan.EnergySpentPerCard.TryGetValue(card.InstanceId, out var spent))
            {
                refund = spent;
            }
            else
            {
                var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                refund = TalentBattleRules.GetEffectivePlayCost(state, owner, card);
            }

            if (refund <= 0)
                return;

            state.EnergyCurrent += refund;
            events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "锁定返还能量")
            {
                CombatantId = card.OwnerCharacterId,
                CardInstanceId = card.InstanceId,
                Energy = state.EnergyCurrent,
                EnergyMax = state.EnergyMax,
                EnergyRemaining = state.EnergyCurrent,
                Amount = refund
            });
        }

        public static bool QueueBlocksOwnerCard(
            BattleState state,
            IReadOnlyList<int> selectedQueue,
            CombatantState owner,
            CardInstanceState candidate)
        {
            if (state == null || owner == null || candidate == null || selectedQueue == null)
                return false;

            foreach (var cardId in selectedQueue)
            {
                var queued = state.GetCard(cardId);
                if (queued == null || !AppliesSelfLock(queued))
                    continue;

                var queuedOwnerId = PositionRules.GetOwnerCombatantId(state, queued);
                if (queuedOwnerId == owner.Id && cardId != candidate.InstanceId)
                    return true;
            }

            return false;
        }
    }
}
