using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;

namespace Grimhand.Presentation.Battle
{
    public static class MinionTraitDisplayFormatter
    {
        /// <summary>属性框用：列出静态特性名称与描述（富文本）。</summary>
        public static string FormatTraitDescriptions(CombatantState combatant)
        {
            if (combatant?.Traits == null || combatant.Traits.Count == 0)
                return "";

            var sb = new StringBuilder();
            var seen = new HashSet<string>();
            foreach (var traitId in combatant.Traits)
            {
                if (string.IsNullOrEmpty(traitId) || !seen.Add(traitId))
                    continue;

                if (!CharacterTraitDisplayCatalog.TryGet(traitId, out var entry))
                    continue;

                if (sb.Length > 0)
                    sb.Append('\n');
                sb.Append("<b>").Append(entry.Title).Append("</b>\n");
                sb.Append(entry.Description);
            }

            return sb.ToString();
        }

        public static string FormatFootnote(CombatantState combatant, BattleState state = null)
        {
            if (combatant == null || !combatant.IsAlive)
                return "";

            var lines = new List<string>(4);

            var bloodRage = BattleUiFormatters.FormatBloodRageDisplay(combatant.BloodRageStacks);
            if (!string.IsNullOrEmpty(bloodRage))
                lines.Add(bloodRage);

            if (combatant.RatPackAttackBonusPercent > 0)
                lines.Add($"鼠群狂怒 +{combatant.RatPackAttackBonusPercent}% 攻击");

            if (combatant.MermaidZeroCostAttackBonusPercent > 0)
                lines.Add($"增伤 +{combatant.MermaidZeroCostAttackBonusPercent}%");

            if (combatant.LowHpSpeedBonusApplied > 0)
                lines.Add($"低血迅捷 +{combatant.LowHpSpeedBonusApplied} 速度");

            // 巨翼蝙蝠首击闪避改由脚标 evade + 50% 展示，不再叠立绘旁红字

            if (HasTrait(combatant, MinionTraitCatalog.SkeletonCardDef)
                || HasTrait(combatant, MinionTraitCatalog.SkeletonEliteCardStats))
            {
                var n = combatant.CardsResolvedCount;
                var mod = n % MinionTraitCatalog.CardsPerStatBonus;
                var need = mod == 0 ? MinionTraitCatalog.CardsPerStatBonus : MinionTraitCatalog.CardsPerStatBonus - mod;
                lines.Add($"出牌 {n}（再{need}张触发）");
            }

            if (HasTrait(combatant, MinionTraitCatalog.PhantomCaptainFrenzy)
                && StatusRules.GetStatusStacks(combatant, StatusCatalog.PhantomCaptainFrenzyAtk) > 0)
            {
                lines.Add($"狂怒 +{MinionTraitCatalog.PhantomCaptainFrenzyAttackPercent}% 增伤 / "
                    + $"+{MinionTraitCatalog.PhantomCaptainFrenzyDefensePercent}% 易伤");
            }

            if (HasTrait(combatant, MinionTraitCatalog.StoneGolemArmorRetain) && combatant.CarryOverBlock > 0)
                lines.Add($"下回合保留 {combatant.CarryOverBlock} 护甲");

            if (lines.Count == 0)
                return "";

            return string.Join("\n", lines);
        }

        static bool HasTrait(CombatantState combatant, string traitId) =>
            MinionTraitRules.HasTrait(combatant, traitId);
    }
}
