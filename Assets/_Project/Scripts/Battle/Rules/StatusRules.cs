using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    public static class StatusRules
    {
        public static int GetEffectiveSpeed(CombatantState combatant)
        {
            var speed = combatant.Speed;
            foreach (var status in combatant.Statuses)
            {
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;
                speed += def.SpeedModifierPerStack * status.Stacks;
            }

            return speed < 0 ? 0 : speed;
        }

        public static void ApplyStatus(
            BattleState state,
            CombatantState target,
            string statusId,
            int stacks,
            int durationOverride,
            List<BattleEvent> events)
        {
            var def = StatusCatalog.Get(statusId);
            if (def == null || target == null || !target.IsAlive)
                return;

            var existing = FindStatus(target, statusId);
            if (existing == null)
            {
                existing = new StatusInstance { StatusId = statusId, Stacks = 0 };
                target.Statuses.Add(existing);
            }

            existing.Stacks += stacks;
            if (def.DurationKind == StatusDurationKind.Permanent)
                existing.RemainingTurns = -1;
            else
            {
                var turns = durationOverride >= 0 ? durationOverride : def.DefaultDuration;
                if (turns > existing.RemainingTurns)
                    existing.RemainingTurns = turns;
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, def.DisplayName)
            {
                CombatantId = target.Id,
                Amount = existing.Stacks,
                TargetId = statusId
            });
        }

        public static void RemoveStatus(CombatantState target, string statusId, int stacks, List<BattleEvent> events)
        {
            var existing = FindStatus(target, statusId);
            if (existing == null)
                return;

            existing.Stacks -= stacks;
            if (existing.Stacks <= 0)
                target.Statuses.Remove(existing);

            events.Add(new BattleEvent(BattleEventKind.StatusRemoved, statusId)
            {
                CombatantId = target.Id,
                Amount = stacks
            });
        }

        public static void ProcessTurnStartStatuses(BattleState state, List<BattleEvent> events)
        {
            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                foreach (var status in combatant.Statuses)
                {
                    var def = StatusCatalog.Get(status.StatusId);
                    if (def == null || def.TurnStartDamagePerStack <= 0)
                        continue;

                    var damage = def.TurnStartDamagePerStack * status.Stacks;
                    if (damage <= 0)
                        continue;

                    combatant.Hp = System.Math.Max(0, combatant.Hp - damage);
                    events.Add(new BattleEvent(BattleEventKind.StatusTickDamage, def.DisplayName)
                    {
                        CombatantId = combatant.Id,
                        Amount = damage,
                        TargetId = status.StatusId
                    });

                    if (!combatant.IsAlive)
                    {
                        events.Add(new BattleEvent(BattleEventKind.CharacterDied, combatant.DisplayName)
                        {
                            CombatantId = combatant.Id
                        });
                        CombatantDeathRules.OnCharacterDied(state, combatant, events);
                    }
                }
            }
        }

        public static void ProcessEndOfTurnDurations(BattleState state, List<BattleEvent> events)
        {
            foreach (var combatant in state.Combatants)
            {
                for (var i = combatant.Statuses.Count - 1; i >= 0; i--)
                {
                    var status = combatant.Statuses[i];
                    var def = StatusCatalog.Get(status.StatusId);
                    if (def == null || def.DurationKind == StatusDurationKind.Permanent)
                        continue;

                    status.RemainingTurns--;
                    if (status.RemainingTurns <= 0)
                    {
                        combatant.Statuses.RemoveAt(i);
                        events.Add(new BattleEvent(BattleEventKind.StatusExpired, def.DisplayName)
                        {
                            CombatantId = combatant.Id,
                            TargetId = status.StatusId
                        });
                    }
                }
            }
        }

        static StatusInstance FindStatus(CombatantState target, string statusId)
        {
            foreach (var s in target.Statuses)
            {
                if (s.StatusId == statusId)
                    return s;
            }

            return null;
        }
    }
}
