using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.V091;
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
                if (team == TeamSide.Player)
                    V091MechanicsRules.OnCardShuffledToDrawPile(state, card);
            }

            if (draw.Count == 0)
                return;

            ShuffleDrawPile(state, team, rng, events);
        }

        public static void DrawCards(
            BattleState state,
            TeamSide team,
            BattleRng rng,
            int count,
            List<BattleEvent> events)
        {
            for (var i = 0; i < count; i++)
                DrawOne(state, team, rng, events);
        }

        /// <summary>从玩家牌库抽取指定角色的卡牌（可触发洗回弃牌堆）。</summary>
        public static void DrawCharacterCards(
            BattleState state,
            string characterDefinitionId,
            BattleRng rng,
            int count,
            List<BattleEvent> events)
        {
            if (state == null || string.IsNullOrEmpty(characterDefinitionId) || count <= 0)
                return;

            for (var i = 0; i < count; i++)
            {
                if (!TryDrawCharacterCard(state, characterDefinitionId, rng, events, reshuffle: true))
                    break;
            }
        }

        static bool TryDrawCharacterCard(
            BattleState state,
            string characterDefinitionId,
            BattleRng rng,
            List<BattleEvent> events,
            bool reshuffle)
        {
            if (TryTakeCharacterCardFromPile(state.PlayerDrawPile, characterDefinitionId, state, events))
                return true;

            if (!reshuffle)
                return false;

            ReshuffleDiscardIntoDraw(state, TeamSide.Player, rng, events);
            return TryTakeCharacterCardFromPile(state.PlayerDrawPile, characterDefinitionId, state, events);
        }

        static bool TryTakeCharacterCardFromPile(
            List<CardInstanceState> pile,
            string characterDefinitionId,
            BattleState state,
            List<BattleEvent> events)
        {
            for (var i = 0; i < pile.Count; i++)
            {
                var card = pile[i];
                if (card.OwnerCharacterId != characterDefinitionId)
                    continue;

                pile.RemoveAt(i);
                TryAddToHand(state, TeamSide.Player, card, events);
                return true;
            }

            return false;
        }

        static void DrawOne(
            BattleState state,
            TeamSide team,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (TryDrawPlayableCard(state, team, events))
                return;

            ReshuffleDiscardIntoDraw(state, team, rng, events);
            TryDrawPlayableCard(state, team, events);
        }

        static bool TryDrawPlayableCard(
            BattleState state,
            TeamSide team,
            List<BattleEvent> events)
        {
            var draw = state.GetDrawPile(team);
            var skipPolluted = team == TeamSide.Player
                               && state.Config?.RunModifiers?.SkipPollutedCardsOnDraw == true;
            var attempts = draw.Count;

            while (draw.Count > 0 && attempts > 0)
            {
                attempts--;
                var card = draw[0];
                draw.RemoveAt(0);
                if (team == TeamSide.Enemy && PositionRules.GetOwnerCombatantId(state, card) == null)
                {
                    state.GetDiscardPile(team).Add(card);
                    continue;
                }

                if (skipPolluted && !card.IsUsable)
                {
                    draw.Add(card);
                    continue;
                }

                TryAddToHand(state, team, card, events);
                return true;
            }

            return false;
        }

        static void TryAddToHand(
            BattleState state,
            TeamSide team,
            CardInstanceState card,
            List<BattleEvent> events)
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
            for (var i = hand.Count - 1; i >= 0; i--)
            {
                var card = hand[i];

                if (card.IsBonusHandCard)
                {
                    hand.RemoveAt(i);
                    card.IsUsable = false;
                    events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "Bonus hand cleared")
                    {
                        CardInstanceId = card.InstanceId
                    });
                    continue;
                }

                if (team == TeamSide.Player && CardRules.HasInheritKeyword(card))
                    continue;

                hand.RemoveAt(i);
                state.GetDiscardPile(team).Add(card);
                events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "End of turn hand discard")
                {
                    CardInstanceId = card.InstanceId
                });
            }
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
            if (state == null || card == null)
                return;

            state.GetHand(team).Remove(card);
            state.GetDrawPile(team).Remove(card);
            state.GetDiscardPile(team).Remove(card);
            var exhaust = state.GetExhaustPile(team);
            if (!exhaust.Contains(card))
                exhaust.Add(card);

            // 保留 exhaust 关键词，供神圣轮回识别；本场不可再打出
            card.IsUsable = false;

            events.Add(new BattleEvent(BattleEventKind.CardDiscarded, "Exhausted card")
            {
                CardInstanceId = card.InstanceId
            });
        }
    }
}
