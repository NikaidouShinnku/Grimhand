using System.Collections.Generic;
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

        /// <summary>
        /// 按物理站位排序后，存活者从前到后依次占据前/中/后排（死亡则后续顺位前移）。
        /// </summary>
        public static FormationSlot GetEffectiveSlot(BattleState state, CombatantState combatant)
        {
            if (combatant == null)
                return FormationSlot.Front;

            var rank = GetEffectiveRank(state, combatant);
            if (rank < 0)
                return combatant.Slot;

            return RankToSlot(rank);
        }

        public static List<CombatantState> GetAliveSortedByPhysicalSlot(BattleState state, TeamSide team)
        {
            var list = new List<CombatantState>();
            foreach (var c in state.GetTeam(team))
            {
                if (c.IsAlive)
                    list.Add(c);
            }

            list.Sort((a, b) => ((int)a.Slot).CompareTo((int)b.Slot));
            return list;
        }

        /// <summary>
        /// 结算开始时存活单位的 Id 快照（按物理站位排序）。
        /// AOE、链式溅射等效果在主目标死亡或阵型前移后仍应命中快照中的单位。
        /// </summary>
        public static List<string> SnapshotAliveCombatantIds(BattleState state, TeamSide team)
        {
            var alive = GetAliveSortedByPhysicalSlot(state, team);
            var ids = new List<string>(alive.Count);
            foreach (var unit in alive)
                ids.Add(unit.Id);
            return ids;
        }

        /// <summary>
        /// 结算开始时，比 target 更深一格存活单位的 Id；主目标可先死亡，溅射仍命中该 Id。
        /// </summary>
        public static string SnapshotCombatantBehindId(BattleState state, CombatantState target)
        {
            var behind = GetCombatantBehind(state, target);
            return behind?.Id;
        }

        public static int GetEffectiveRank(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null || !combatant.IsAlive)
                return -1;

            var alive = GetAliveSortedByPhysicalSlot(state, combatant.Team);
            for (var i = 0; i < alive.Count; i++)
            {
                if (alive[i].Id == combatant.Id)
                    return i;
            }

            return -1;
        }

        static FormationSlot RankToSlot(int rank) =>
            rank switch
            {
                0 => FormationSlot.Front,
                1 => FormationSlot.Middle,
                _ => FormationSlot.Back
            };

        public static CombatantState PickDefaultTarget(BattleState state, TeamSide attackerTeam)
        {
            var targetTeam = attackerTeam == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var taunt = CombatMechanicsRules.FindTauntHolder(state, targetTeam);
            if (taunt != null)
                return taunt;

            var alive = GetAliveSortedByPhysicalSlot(state, targetTeam);
            return alive.Count > 0 ? alive[0] : null;
        }

        public static CombatantState PickCombatantInSlot(BattleState state, TeamSide team, FormationSlot slot)
        {
            var alive = GetAliveSortedByPhysicalSlot(state, team);
            var rank = (int)slot - (int)FormationSlot.Front;
            if (rank >= 0 && rank < alive.Count)
                return alive[rank];

            return null;
        }

        public static void SwapWithAdjacentAlly(
            BattleState state,
            CombatantState actor,
            int slotOffset,
            List<BattleEvent> events)
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

        /// <summary>有效阵型中，比 target 更深的一格（Front→Middle→Back）。</summary>
        public static CombatantState GetCombatantBehind(BattleState state, CombatantState target)
        {
            if (target == null || !target.IsAlive)
                return null;

            var alive = GetAliveSortedByPhysicalSlot(state, target.Team);
            var rank = GetEffectiveRank(state, target);
            var nextRank = rank + 1;
            if (nextRank < 0 || nextRank >= alive.Count)
                return null;

            return alive[nextRank];
        }

        public static string GetOwnerCombatantId(BattleState state, CardInstanceState card)
        {
            if (card == null)
                return null;

            if (!string.IsNullOrEmpty(card.OwnerCombatantId))
            {
                var bound = state.GetCombatant(card.OwnerCombatantId);
                if (bound != null)
                    return card.OwnerCombatantId;
            }

            foreach (var c in state.Combatants)
            {
                if (c.CharacterDefinitionId == card.OwnerCharacterId && c.IsAlive)
                    return c.Id;
            }

            return null;
        }
    }
}
