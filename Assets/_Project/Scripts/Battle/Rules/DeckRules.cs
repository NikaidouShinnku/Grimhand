using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class DeckRules
    {
        public static void ShuffleDrawPile(BattleState state, TeamSide team, BattleRng rng, List<BattleEvent> events)
        {
            var pile = state.GetDrawPile(team);
            for (var i = pile.Count - 1; i > 0; i--)
            {
                var j = rng.NextIndex(i + 1);
                (pile[i], pile[j]) = (pile[j], pile[i]);
            }

            if (team == TeamSide.Player)
                PinPriorityDrawCards(pile);

            events.Add(new BattleEvent(BattleEventKind.DeckShuffled, $"{team} draw pile shuffled"));
        }

        static void PinPriorityDrawCards(List<CardInstanceState> pile)
        {
            if (pile == null || pile.Count <= 1)
                return;

            for (var i = 0; i < pile.Count; i++)
            {
                if (pile[i].DefinitionId != TestCardIds.AuthorRealmStrike)
                    continue;

                var card = pile[i];
                pile.RemoveAt(i);
                pile.Insert(0, card);
                return;
            }
        }

        public static void ReshuffleDiscardIntoDraw(BattleState state, TeamSide team, BattleRng rng, List<BattleEvent> events)
        {
            var discard = state.GetDiscardPile(team);
            if (discard.Count == 0)
                return;

            var draw = state.GetDrawPile(team);
            for (var i = discard.Count - 1; i >= 0; i--)
            {
                var card = discard[i];
                if (team == TeamSide.Enemy && PositionRules.GetOwnerCombatantId(state, card) == null)
                    continue;

                draw.Add(card);
                discard.RemoveAt(i);
            }

            if (draw.Count == 0)
                return;

            ShuffleDrawPile(state, team, rng, events);
        }

        public static void DrawCards(BattleState state, TeamSide team, BattleRng rng, int count, List<BattleEvent> events)
        {
            for (var i = 0; i < count; i++)
                DrawOne(state, team, rng, events);
        }

        static void DrawOne(BattleState state, TeamSide team, BattleRng rng, List<BattleEvent> events)
        {
            if (TryDrawPlayableCard(state, team, events))
                return;

            ReshuffleDiscardIntoDraw(state, team, rng, events);
            TryDrawPlayableCard(state, team, events);
        }

        static bool TryDrawPlayableCard(BattleState state, TeamSide team, List<BattleEvent> events)
        {
            var draw = state.GetDrawPile(team);
            while (draw.Count > 0)
            {
                var card = draw[0];
                draw.RemoveAt(0);
                if (team == TeamSide.Enemy && PositionRules.GetOwnerCombatantId(state, card) == null)
                {
                    state.GetDiscardPile(team).Add(card);
                    continue;
                }

                TryAddToHand(state, team, card, events);
                return true;
            }

            return false;
        }

        static void TryAddToHand(BattleState state, TeamSide team, CardInstanceState card, List<BattleEvent> events)
        {
            var hand = state.GetHand(team);
            if (hand.Count < state.Config.HandLimit)
            {
                hand.Add(card);
                events.Add(new BattleEvent(BattleEventKind.CardDrawn, card.DisplayName)
                {
                    CardInstanceId = card.InstanceId
                });
                return;
            }

            state.GetDiscardPile(team).Add(card);
            events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "Hand overflow")
            {
                CardInstanceId = card.InstanceId
            });
        }

        public static void DiscardHandAtEndOfTurn(BattleState state, TeamSide team, List<BattleEvent> events)
        {
            var hand = state.GetHand(team);
            foreach (var card in hand)
            {
                state.GetDiscardPile(team).Add(card);
                events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "End of turn hand discard")
                {
                    CardInstanceId = card.InstanceId
                });
            }

            hand.Clear();
        }

        public static void MovePlayedCardToDiscard(
            BattleState state,
            TeamSide team,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            state.GetHand(team).Remove(card);
            state.GetDiscardPile(team).Add(card);
            events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "Played card")
            {
                CardInstanceId = card.InstanceId
            });
        }

        public static void ExhaustCard(
            BattleState state,
            TeamSide team,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            state.GetHand(team).Remove(card);
            state.GetDrawPile(team).Remove(card);
            state.GetDiscardPile(team).Remove(card);
            card.IsUsable = false;

            events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "Exhausted card")
            {
                CardInstanceId = card.InstanceId
            });
        }
    }
}
