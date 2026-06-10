using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    public sealed class PendingBossBonusHand
    {
        public string CombatantId { get; set; } = "";
        public string CardDefinitionId { get; set; } = "";
    }

    public static class BossBonusHandRules
    {
        public static void QueueBonusHandNextTurn(
            BattleState state,
            string combatantId,
            string cardDefinitionId)
        {
            if (state == null
                || string.IsNullOrEmpty(combatantId)
                || string.IsNullOrEmpty(cardDefinitionId))
            {
                return;
            }

            state.PendingBossBonusHandsNextTurn.Add(new PendingBossBonusHand
            {
                CombatantId = combatantId,
                CardDefinitionId = cardDefinitionId
            });
        }

        public static void GrantPendingBonusHands(BattleState state, List<BattleEvent> events)
        {
            if (state?.Config == null || state.PendingBossBonusHandsNextTurn.Count == 0)
                return;

            foreach (var pending in state.PendingBossBonusHandsNextTurn)
            {
                var owner = state.GetCombatant(pending.CombatantId);
                if (owner == null || !owner.IsAlive)
                    continue;

                var template = FindCardTemplate(state, pending.CardDefinitionId, owner.CharacterDefinitionId);
                if (template == null)
                    continue;

                if (HandAlreadyHasDefinition(state, owner, pending.CardDefinitionId))
                    continue;

                var card = SummonRules.CreateBoundCard(state, template, owner.Id);
                state.EnemyHand.Add(card);
                events.Add(new BattleEvent(BattleEventKind.CardDrawn, card.DisplayName)
                {
                    CardInstanceId = card.InstanceId,
                    CombatantId = owner.Id
                });
            }

            state.PendingBossBonusHandsNextTurn.Clear();
        }

        static CardTemplate FindCardTemplate(BattleState state, string definitionId, string ownerCharacterId)
        {
            foreach (var cc in state.Config.Combatants)
            {
                foreach (var template in cc.DeckTemplates)
                {
                    if (template?.DefinitionId == definitionId)
                        return template;
                }
            }

            foreach (var pair in state.Config.SummonTemplates)
            {
                foreach (var template in pair.Value.DeckTemplates)
                {
                    if (template?.DefinitionId == definitionId)
                        return template;
                }
            }

            return BuildFallbackWrathTemplate(definitionId, ownerCharacterId);
        }

        static CardTemplate BuildFallbackWrathTemplate(string definitionId, string ownerCharacterId)
        {
            if (definitionId != CharacterTraitCatalog.GhostQueenWrathCardId)
                return null;

            var card = new CardTemplate
            {
                DefinitionId = definitionId,
                DisplayName = "幽灵女王之怒",
                OwnerCharacterId = ownerCharacterId,
                Cost = 0,
                CardType = CardType.Status
            };
            card.Keywords.Add("bonus_hand");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.GhostQueenWrath,
                Stacks = 1,
                Duration = -1
            });
            return card;
        }

        static bool HandAlreadyHasDefinition(BattleState state, CombatantState owner, string definitionId)
        {
            foreach (var card in state.EnemyHand)
            {
                if (card.OwnerCombatantId == owner.Id && card.DefinitionId == definitionId)
                    return true;
            }

            return false;
        }
    }
}
