using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;

namespace Grimhand.Battle.Rules
{
    public static class AnubisAvatarRules
    {
        public const int StatPercentBonus = 50;
        public const int CardLockTurns = 2;

        public static void Apply(BattleState state, CombatantState actor, List<BattleEvent> events)
        {
            if (actor == null || !actor.IsAlive)
                return;

            if (!StatusRules.HasStatus(actor, StatusCatalog.AnubisAvatar))
            {
                StatusRules.ApplyStatus(
                    state,
                    actor,
                    StatusCatalog.AnubisAvatar,
                    stacks: 1,
                    durationOverride: -1,
                    events);
            }

            CardLockRules.ApplyLock(actor, CardLockTurns);

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "阿努比斯化身")
            {
                CombatantId = actor.Id,
                TargetId = StatusCatalog.AnubisAvatar
            });
        }

        public static void ProcessTurnStart(CombatantState combatant) =>
            CardLockRules.ProcessTurnStart(combatant);
    }
}
