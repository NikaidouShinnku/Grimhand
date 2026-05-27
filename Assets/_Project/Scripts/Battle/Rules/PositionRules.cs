using Grimhand.Battle.Events;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class PositionRules
    {
        public static float GetDamageMultiplier(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Front: return 0.7f;
                case FormationSlot.Middle: return 1.15f;
                case FormationSlot.Back: return 1.3f;
                default: return 1f;
            }
        }

        public static float GetIncomingDamageMultiplier(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Front: return 0.7f;
                case FormationSlot.Middle: return 0.85f;
                case FormationSlot.Back: return 1f;
                default: return 1f;
            }
        }

        public static CombatantState PickDefaultTarget(BattleState state, TeamSide attackerTeam)
        {
            var targetTeam = attackerTeam == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            CombatantState front = null;
            CombatantState middle = null;
            CombatantState back = null;

            foreach (var c in state.GetTeam(targetTeam))
            {
                if (!c.IsAlive)
                    continue;

                switch (c.Slot)
                {
                    case FormationSlot.Front: front = c; break;
                    case FormationSlot.Middle: middle = c; break;
                    case FormationSlot.Back: back = c; break;
                }
            }

            if (front != null) return front;
            if (middle != null) return middle;
            return back;
        }

        public static CombatantState PickCombatantInSlot(BattleState state, TeamSide team, FormationSlot slot)
        {
            foreach (var c in state.GetTeam(team))
            {
                if (c.IsAlive && c.Slot == slot)
                    return c;
            }

            foreach (var c in state.GetTeam(team))
            {
                if (c.IsAlive)
                    return c;
            }

            return null;
        }

        public static void SwapWithAdjacentAlly(
            BattleState state,
            CombatantState actor,
            int slotOffset,
            System.Collections.Generic.List<BattleEvent> events)
        {
            var desired = (int)actor.Slot + slotOffset;
            if (desired < 1) desired = 1;
            if (desired > 3) desired = 3;

            CombatantState partner = null;
            foreach (var ally in state.GetTeam(actor.Team))
            {
                if (ally.IsAlive && ally.Id != actor.Id && (int)ally.Slot == desired)
                {
                    partner = ally;
                    break;
                }
            }

            if (partner == null)
                return;

            var temp = actor.Slot;
            actor.Slot = partner.Slot;
            partner.Slot = temp;

            events.Add(new BattleEvent(BattleEventKind.PositionSwapped, "Position swapped")
            {
                CombatantId = actor.Id,
                TargetId = partner.Id
            });
        }

        public static string GetOwnerCombatantId(BattleState state, CardInstanceState card)
        {
            foreach (var c in state.Combatants)
            {
                if (c.CharacterDefinitionId == card.OwnerCharacterId && c.IsAlive)
                    return c.Id;
            }

            foreach (var c in state.Combatants)
            {
                if (c.CharacterDefinitionId == card.OwnerCharacterId)
                    return c.Id;
            }

            return null;
        }
    }
}
