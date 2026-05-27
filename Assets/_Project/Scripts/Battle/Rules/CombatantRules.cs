using Grimhand.Battle.Model;

namespace Grimhand.Battle.Rules
{
    public static class CombatantRules
    {
        public const int AttackPerLevel = 2;
        public const int DefensePerLevel = 1;

        public static void RefreshDerivedStats(CombatantState combatant)
        {
            var levelBonus = combatant.Level - 1;
            if (levelBonus < 0)
                levelBonus = 0;

            combatant.Attack = combatant.BaseAttack + levelBonus * AttackPerLevel;
            combatant.Defense = combatant.BaseDefense + levelBonus * DefensePerLevel;
        }
    }
}
