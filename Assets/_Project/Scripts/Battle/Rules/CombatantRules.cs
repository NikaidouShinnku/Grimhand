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

            foreach (var status in combatant.Statuses)
            {
                var def = Status.StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;

                combatant.Attack += def.AttackModifierPerStack * status.Stacks;
                combatant.Defense += def.DefenseModifierPerStack * status.Stacks;
            }
        }
    }
}
