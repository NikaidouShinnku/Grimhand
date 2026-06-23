using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CombatantRules
    {
        public static void RefreshDerivedStats(CombatantState combatant)
        {
            CombatModifierRules.RefreshCombatantModifiers(null, combatant, null);
        }
    }
}
