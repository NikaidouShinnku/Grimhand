using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CombatantDeathRules
    {
        public static void OnCharacterDied(BattleState state, CombatantState combatant, List<BattleEvent> events)
        {
            if (combatant == null)
                return;

            var polluted = 0;
            foreach (var card in state.CardsById.Values)
            {
                if (card.OwnerCharacterId != combatant.CharacterDefinitionId || !card.IsUsable)
                    continue;

                card.IsUsable = false;
                polluted++;
            }

            events.Add(new BattleEvent(BattleEventKind.DeckPolluted, combatant.DisplayName)
            {
                CombatantId = combatant.Id,
                Amount = polluted
            });
        }
    }
}
