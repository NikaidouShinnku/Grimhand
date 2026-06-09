using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    public static class StatusRules
    {
        public static int GetEffectiveSpeed(BattleState state, CombatantState combatant)
        {
            if (combatant == null)
                return 0;

            var speed = combatant.Speed;
            speed += RelicEffectRules.GetBattleSpeedBonus(state, combatant);
            foreach (var status in combatant.Statuses)
            {
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;
                speed += def.SpeedModifierPerStack * status.Stacks;
            }

            return speed < 0 ? 0 : speed;
        }

        public static int GetEffectiveSpeed(CombatantState combatant) =>
            GetEffectiveSpeed(null, combatant);

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
                var bonus = state?.Config?.RunModifiers?.StatusDurationBonusTurns ?? 0;
                if (bonus > 0 && def.DurationKind == StatusDurationKind.Turns)
                    turns += bonus;
                if (turns > existing.RemainingTurns)
                    existing.RemainingTurns = turns;
            }

            if (def.MaxHpPercentBonusPerStack > 0 && stacks > 0)
            {
                var beforeMax = target.MaxHp;
                var bonusHp = System.Math.Max(1,
                    (int)System.Math.Round(beforeMax * def.MaxHpPercentBonusPerStack / 100f * stacks));
                target.MaxHp = beforeMax + bonusHp;
                target.Hp = System.Math.Min(target.MaxHp, target.Hp + bonusHp);
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, def.DisplayName)
            {
                CombatantId = target.Id,
                Amount = existing.Stacks,
                TargetId = statusId
            });

            CombatantRules.RefreshDerivedStats(target);
            RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);
        }

        public static bool HasStatus(CombatantState target, string statusId) =>
            FindStatus(target, statusId) != null;

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

            CombatantRules.RefreshDerivedStats(target);
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

                    if (!combatant.IsAlive
                        && CombatMechanicsRules.TryPreventDeathWithReviveBlessing(state, combatant, events))
                    {
                        continue;
                    }

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
                        CombatantRules.RefreshDerivedStats(combatant);
                        RelicBattleRules.RefreshDerivedStats(state, combatant, state?.Config?.RunModifiers);
                    }
                }
            }
        }

        public static StatusInstance FindStatus(CombatantState target, string statusId)
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
