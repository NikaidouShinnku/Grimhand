using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    public static class MinionTraitDisplayFormatter
    {
        public static string FormatFootnote(CombatantState combatant, BattleState state = null)
        {
            if (combatant == null || !combatant.IsAlive)
                return "";

            var lines = new System.Collections.Generic.List<string>(4);

            var bloodRage = BattleUiFormatters.FormatBloodRageDisplay(combatant.BloodRageStacks);
            if (!string.IsNullOrEmpty(bloodRage))
                lines.Add(bloodRage);

            if (combatant.RatPackAttackBonusPercent > 0)
                lines.Add($"鼠群狂怒 +{combatant.RatPackAttackBonusPercent}% 攻击");

            if (combatant.MermaidZeroCostAttackBonusPercent > 0)
                lines.Add($"零费加攻 +{combatant.MermaidZeroCostAttackBonusPercent}%");

            if (combatant.LowHpSpeedBonusApplied > 0)
                lines.Add($"低血迅捷 +{combatant.LowHpSpeedBonusApplied} 速度");

            if (HasTrait(combatant, MinionTraitCatalog.BatFirstHitDodge) && combatant.FirstHitDodgePending)
                lines.Add($"首击{MinionTraitCatalog.BatFirstHitDodgeChancePercent}%闪避");

            if (HasTrait(combatant, MinionTraitCatalog.SkeletonCardDef)
                || HasTrait(combatant, MinionTraitCatalog.SkeletonEliteCardStats))
            {
                var n = combatant.CardsResolvedCount;
                var mod = n % MinionTraitCatalog.CardsPerStatBonus;
                var need = mod == 0 ? MinionTraitCatalog.CardsPerStatBonus : MinionTraitCatalog.CardsPerStatBonus - mod;
                lines.Add($"出牌 {n}（再{need}张触发）");
            }

            if (HasTrait(combatant, MinionTraitCatalog.PhantomCaptainFrenzy) && IsPhantomCaptainFrenzyActive(state))
                lines.Add($"狂怒 +{MinionTraitCatalog.PhantomCaptainFrenzyAttackPercent}% 攻击");

            if (HasTrait(combatant, MinionTraitCatalog.StoneGolemArmorRetain) && combatant.CarryOverBlock > 0)
                lines.Add($"下回合保留 {combatant.CarryOverBlock} 护甲");

            if (lines.Count == 0)
                return "";

            return string.Join("\n", lines);
        }

        static bool HasTrait(CombatantState combatant, string traitId) =>
            MinionTraitRules.HasTrait(combatant, traitId);

        static bool IsPhantomCaptainFrenzyActive(BattleState state)
        {
            if (state == null)
                return false;

            foreach (var unit in state.Combatants)
            {
                if (unit.Team != TeamSide.Player)
                    continue;

                if (!unit.IsAlive)
                    return true;

                if (unit.MaxHp > 0 && unit.Hp * 100 / unit.MaxHp < 25)
                    return true;
            }

            return false;
        }
    }
}
