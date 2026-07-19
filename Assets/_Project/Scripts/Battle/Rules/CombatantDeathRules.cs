using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.V09;

namespace Grimhand.Battle.Rules
{
    public static class CombatantDeathRules
    {
        public static void OnCharacterDied(BattleState state, CombatantState combatant, List<BattleEvent> events)
        {
            if (combatant == null)
                return;

            MinionTraitRules.OnCharacterDied(state, combatant, events);
            V09BossMechanicsRules.OnCharacterDied(state, combatant, events, rng: null);
            TalentBattleRules.OnCharacterDied(state, combatant, events);
            V09NewMechanicsRules.OnConstrictTargetDied(state, combatant, events);
            V09NewMechanicsRules.OnConstrictCasterDied(state, combatant, events);

            var polluted = 0;
            foreach (var card in state.CardsById.Values)
            {
                if (!IsCardOwnedByCombatant(card, combatant) || !card.IsUsable)
                    continue;

                card.IsUsable = false;
                polluted++;
            }

            events.Add(new BattleEvent(BattleEventKind.DeckPolluted, combatant.DisplayName)
            {
                CombatantId = combatant.Id,
                Amount = polluted
            });

            RelicBattleRules.RefreshAllDerivedStats(state);
        }

        public static void RestoreUsableCards(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            foreach (var card in state.CardsById.Values)
            {
                if (!IsCardOwnedByCombatant(card, combatant))
                    continue;

                card.IsUsable = true;
            }
        }

        static bool IsCardOwnedByCombatant(CardInstanceState card, CombatantState combatant)
        {
            if (card == null || combatant == null)
                return false;

            if (!string.IsNullOrEmpty(card.OwnerCombatantId))
                return card.OwnerCombatantId == combatant.Id;

            return card.OwnerCharacterId == combatant.CharacterDefinitionId;
        }
    }
}
