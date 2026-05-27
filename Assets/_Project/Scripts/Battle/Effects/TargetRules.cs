using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Effects
{
    public static class TargetRules
    {
        public static CombatantState ResolveTarget(
            BattleState state,
            CombatantState actor,
            EffectTarget targetKind,
            int cardInstanceId)
        {
            switch (targetKind)
            {
                case EffectTarget.Self:
                    return actor;
                case EffectTarget.DefaultEnemy:
                case EffectTarget.ManualSelected:
                    if (TryGetSelectedTarget(state, cardInstanceId, out var picked))
                        return picked;
                    return PositionRules.PickDefaultTarget(state, actor.Team);
                case EffectTarget.FrontAlly:
                case EffectTarget.BackAlly:
                    if (TryGetSelectedTarget(state, cardInstanceId, out var allyPicked))
                        return allyPicked;
                    return targetKind == EffectTarget.FrontAlly
                        ? PickAllyBySlotOffset(state, actor, -1)
                        : PickAllyBySlotOffset(state, actor, 1);
                case EffectTarget.EnemyFrontSlot:
                    return PositionRules.PickCombatantInSlot(state, OppositeTeam(actor.Team), FormationSlot.Front);
                case EffectTarget.EnemyMiddleSlot:
                    return PositionRules.PickCombatantInSlot(state, OppositeTeam(actor.Team), FormationSlot.Middle);
                case EffectTarget.EnemyBackSlot:
                    return PositionRules.PickCombatantInSlot(state, OppositeTeam(actor.Team), FormationSlot.Back);
                case EffectTarget.AllyFrontSlot:
                    return PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Front);
                case EffectTarget.AllyMiddleSlot:
                    return PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Middle);
                case EffectTarget.AllyBackSlot:
                    return PositionRules.PickCombatantInSlot(state, actor.Team, FormationSlot.Back);
                case EffectTarget.LastActionActor:
                    return state.GetCombatant(state.LastAction.ActorId);
                default:
                    return PositionRules.PickDefaultTarget(state, actor.Team);
            }
        }

        static bool TryGetSelectedTarget(BattleState state, int cardInstanceId, out CombatantState target)
        {
            target = null;
            if (!state.ResolutionTargets.TryGetValue(cardInstanceId, out var targetId))
                return false;

            target = state.GetCombatant(targetId);
            return target != null;
        }

        static TeamSide OppositeTeam(TeamSide team) =>
            team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;

        static CombatantState PickAllyBySlotOffset(BattleState state, CombatantState actor, int slotOffset)
        {
            var desired = (int)actor.Slot + slotOffset;
            if (desired < 1) desired = 1;
            if (desired > 3) desired = 3;

            foreach (var ally in state.GetTeam(actor.Team))
            {
                if (ally.IsAlive && (int)ally.Slot == desired)
                    return ally;
            }

            return actor;
        }
    }
}
