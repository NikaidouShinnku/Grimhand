using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class FormationSlotRules
    {
        static readonly FormationSlot[] SlotOrder =
        {
            FormationSlot.Front,
            FormationSlot.Middle,
            FormationSlot.Back
        };

        /// <summary>按队伍内出现顺序分配前/中/后，保证每槽仅一名角色。</summary>
        public static void AssignUniqueSlotsPerTeam(IList<CombatantConfig> combatants)
        {
            if (combatants == null || combatants.Count == 0)
                return;

            AssignForTeam(combatants, TeamSide.Player);
            AssignForTeam(combatants, TeamSide.Enemy);
        }

        static void AssignForTeam(IList<CombatantConfig> combatants, TeamSide team)
        {
            var index = 0;
            foreach (var cc in combatants)
            {
                if (cc == null || cc.Team != team)
                    continue;

                if (index >= SlotOrder.Length)
                    break;

                cc.Slot = SlotOrder[index++];
            }
        }
    }
}
