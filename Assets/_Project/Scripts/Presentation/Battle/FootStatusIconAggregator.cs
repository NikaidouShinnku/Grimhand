using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    /// <summary>
    /// 脚底状态图标聚合：同效果族合并显示总层数/百分比；
    /// 涨潮/魔化潮汐的增伤与减伤并入对应 icon。
    /// 状态详情框仍按各 StatusInstance（含不同持续时间分桶）分别列出。
    /// </summary>
    public static class FootStatusIconAggregator
    {
        public static List<FootStatusEntry> Aggregate(CombatantState combatant)
        {
            var list = new List<FootStatusEntry>();
            if (combatant?.Statuses == null)
                return list;

            var byId = new Dictionary<string, int>();
            foreach (var status in combatant.Statuses)
            {
                if (status == null || status.Stacks <= 0 || string.IsNullOrEmpty(status.StatusId))
                    continue;

                if (!byId.ContainsKey(status.StatusId))
                    byId[status.StatusId] = 0;
                byId[status.StatusId] += status.Stacks;
            }

            var attackPercent = SumAttackPercentForIcon(combatant, byId);
            var damageReduction = SumDamageReductionForIcon(combatant, byId);
            var vulnerable = SumVulnerableForIcon(byId);

            // 并入增伤/减伤/易伤 icon 的成员不再单独占格（涨潮/退潮层数仍保留）
            byId.Remove(StatusCatalog.AttackUpPercent);
            byId.Remove(StatusCatalog.WaveSurge);
            byId.Remove(StatusCatalog.PhantomCaptainFrenzyAtk);
            byId.Remove(StatusCatalog.DamageReduction);
            byId.Remove(StatusCatalog.TideEmpower);
            byId.Remove(StatusCatalog.Vulnerable);
            byId.Remove(StatusCatalog.SpiderPoisonVulnerable);
            byId.Remove(StatusCatalog.PhantomCaptainFrenzyVuln);
            // 退潮本身仍显示（无法涨潮），其 50% 易伤另并入易伤 icon

            if (attackPercent > 0)
                byId[StatusCatalog.AttackUpPercent] = attackPercent;
            if (damageReduction > 0)
                byId[StatusCatalog.DamageReduction] = damageReduction;
            if (vulnerable > 0)
                byId[StatusCatalog.Vulnerable] = vulnerable;

            foreach (var pair in byId)
            {
                if (pair.Value <= 0)
                    continue;
                list.Add(new FootStatusEntry { StatusId = pair.Key, Stacks = pair.Value });
            }

            return list;
        }

        static int SumAttackPercentForIcon(CombatantState combatant, Dictionary<string, int> byId)
        {
            var total = 0;
            if (byId.TryGetValue(StatusCatalog.AttackUpPercent, out var atkPct))
                total += atkPct;
            if (byId.TryGetValue(StatusCatalog.WaveSurge, out var wave))
                total += wave;
            if (byId.TryGetValue(StatusCatalog.PhantomCaptainFrenzyAtk, out var frenzy))
                total += frenzy;

            var tide = StatusRules.GetStatusStacks(combatant, StatusCatalog.RisingTide);
            if (tide > 0)
            {
                var def = StatusCatalog.Get(StatusCatalog.RisingTide);
                total += tide * (def?.AttackPercentBonusPerStack ?? 10);
            }

            if (combatant.MermaidZeroCostAttackBonusPercent > 0)
                total += combatant.MermaidZeroCostAttackBonusPercent;
            if (combatant.RatPackAttackBonusPercent > 0)
                total += combatant.RatPackAttackBonusPercent;
            if (combatant.SacrificeAttackStacks > 0)
                total += combatant.SacrificeAttackStacks;
            if (combatant.TurnAttackBonusPercent > 0)
                total += combatant.TurnAttackBonusPercent;

            return total;
        }

        static int SumDamageReductionForIcon(CombatantState combatant, Dictionary<string, int> byId)
        {
            var total = 0;
            if (byId.TryGetValue(StatusCatalog.DamageReduction, out var dr))
                total += dr;

            var tide = StatusRules.GetStatusStacks(combatant, StatusCatalog.RisingTide);
            if (tide > 0)
            {
                var def = StatusCatalog.Get(StatusCatalog.RisingTide);
                total += tide * (def?.IncomingDamageReductionPercentPerStack ?? 15);

                if (StatusRules.HasStatus(combatant, StatusCatalog.TideEmpower))
                    total += tide * 5;
            }

            return total;
        }

        static int SumVulnerableForIcon(Dictionary<string, int> byId)
        {
            var total = 0;
            if (byId.TryGetValue(StatusCatalog.Vulnerable, out var vuln))
                total += vuln;
            if (byId.TryGetValue(StatusCatalog.SpiderPoisonVulnerable, out var spider))
                total += spider;
            if (byId.TryGetValue(StatusCatalog.PhantomCaptainFrenzyVuln, out var frenzy))
                total += frenzy;

            // 退潮：每层 50% 易伤
            if (byId.TryGetValue(StatusCatalog.EbbingTide, out var ebb) && ebb > 0)
            {
                var def = StatusCatalog.Get(StatusCatalog.EbbingTide);
                total += ebb * (def?.IncomingDamagePercentPerStack ?? 50);
            }

            return total;
        }
    }
}
