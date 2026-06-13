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

            var attackFlat = 0;
            var defenseFlat = 0;
            var attackPercent = 0;
            var defensePercent = 0;

            foreach (var status in combatant.Statuses)
            {
                var def = Status.StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;

                attackFlat += def.AttackModifierPerStack * status.Stacks;
                defenseFlat += def.DefenseModifierPerStack * status.Stacks;
                attackPercent += def.AttackPercentBonusPerStack * status.Stacks;
                defensePercent += def.DefensePercentBonusPerStack * status.Stacks;
            }

            combatant.Attack += attackFlat;
            combatant.Defense += defenseFlat;

            if (attackPercent != 0)
                combatant.Attack = System.Math.Max(1,
                    (int)System.Math.Round(combatant.Attack * (100 + attackPercent) / 100f));

            if (defensePercent != 0)
                combatant.Defense = System.Math.Max(0,
                    (int)System.Math.Round(combatant.Defense * (100 + defensePercent) / 100f));

            combatant.Attack += combatant.GargoyleStanceAttackBonus;
            combatant.Defense += combatant.GargoyleStanceDefenseBonus;

            if (combatant.RatPackAttackBonusPercent > 0)
            {
                combatant.Attack = System.Math.Max(1,
                    (int)System.Math.Round(combatant.Attack * (100 + combatant.RatPackAttackBonusPercent) / 100f));
            }
        }
    }
}
