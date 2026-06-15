using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    public static class CombatantDisplayHelper
    {
        public static int GetAttack(CombatantState unit, PresentationSnapshot presentation) =>
            TryGetStats(unit, presentation, out var stats) ? stats.Attack : unit.Attack;

        public static int GetDefense(CombatantState unit, PresentationSnapshot presentation) =>
            TryGetStats(unit, presentation, out var stats) ? stats.Defense : unit.Defense;

        public static int GetSpeed(CombatantState unit, PresentationSnapshot presentation) =>
            TryGetStats(unit, presentation, out var stats)
                ? stats.Speed
                : StatusRules.GetEffectiveSpeed(unit);

        public static int GetBloodRageStacks(CombatantState unit, PresentationSnapshot presentation) =>
            TryGetStats(unit, presentation, out var stats) ? stats.BloodRageStacks : unit.BloodRageStacks;

        public static string GetStatusSummary(CombatantState unit, PresentationSnapshot presentation) =>
            TryGetStats(unit, presentation, out var stats)
                ? stats.StatusSummary ?? ""
                : BattleUiFormatters.FormatStatusListDisplay(unit);

        public static string GetTraitFootnote(CombatantState unit, PresentationSnapshot presentation) =>
            TryGetStats(unit, presentation, out var stats)
                ? stats.TraitFootnote ?? ""
                : MinionTraitDisplayFormatter.FormatFootnote(unit);

        static bool TryGetStats(
            CombatantState unit,
            PresentationSnapshot presentation,
            out CombatantDisplayStats stats)
        {
            stats = default;
            return unit != null
                   && presentation != null
                   && presentation.TryGetDisplayStats(unit.Id, out stats);
        }
    }
}
