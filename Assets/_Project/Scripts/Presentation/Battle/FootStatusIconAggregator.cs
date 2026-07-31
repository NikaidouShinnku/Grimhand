using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.Rules;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Battle
{
    /// <summary>
    /// 脚底状态图标聚合：同效果族合并显示总层数/百分比；
    /// 涨潮/魔化潮汐的增伤与减伤并入对应 icon。
    /// 状态详情框仍按各 StatusInstance（含不同持续时间分桶）分别列出。
    /// </summary>
    public static class FootStatusIconAggregator
    {
        public static List<FootStatusEntry> Aggregate(CombatantState combatant) =>
            Aggregate(combatant, null);

        public static List<FootStatusEntry> Aggregate(CombatantState combatant, BattleState state) =>
            Aggregate(combatant, state, null);

        public static List<FootStatusEntry> Aggregate(
            CombatantState combatant,
            BattleState state,
            int? blockOverride)
        {
            var list = new List<FootStatusEntry>();
            if (combatant?.Statuses == null)
                return list;

            var byId = new Dictionary<string, int>();
            foreach (var status in combatant.Statuses)
            {
                if (status == null || status.Stacks <= 0 || string.IsNullOrEmpty(status.StatusId))
                    continue;
                // 魂火节流：机制状态，不显示脚标
                if (status.StatusId == StatusCatalog.LichSoulFireThrottle)
                    continue;

                if (!byId.ContainsKey(status.StatusId))
                    byId[status.StatusId] = 0;
                byId[status.StatusId] += status.Stacks;
            }

            var attackPercent = SumAttackPercentForIcon(combatant, byId, state);
            var damageReduction = SumDamageReductionForIcon(combatant, byId, state, blockOverride);
            var vulnerable = SumVulnerableForIcon(byId);
            var blockGainPercent = SumBlockGainPercentForIcon(combatant, byId, state);

            // 并入增伤/减伤/易伤 icon 的成员不再单独占格（涨潮/退潮层数仍保留）
            byId.Remove(StatusCatalog.AttackUpPercent);
            byId.Remove(StatusCatalog.WaveSurge);
            byId.Remove(StatusCatalog.PhantomCaptainFrenzyAtk);
            byId.Remove(StatusCatalog.KnightAssaultStanceAtk);
            byId.Remove(StatusCatalog.KnightBackToWallAtk);
            byId.Remove(StatusCatalog.RangerLowHpFuryAtk);
            byId.Remove(StatusCatalog.RangerSoloHuntAtk);
            byId.Remove(StatusCatalog.KnightComboAtk);
            byId.Remove(StatusCatalog.RangerBloodDebtAtk);
            byId.Remove(StatusCatalog.DamageReduction);
            byId.Remove(StatusCatalog.TideEmpower);
            byId.Remove(StatusCatalog.Vulnerable);
            byId.Remove(StatusCatalog.SpiderPoisonVulnerable);
            byId.Remove(StatusCatalog.DarkKnightPoisonVulnerable);
            byId.Remove(StatusCatalog.PhantomCaptainFrenzyVuln);
            byId.Remove(StatusCatalog.KnightAssaultStanceVuln);
            byId.Remove(StatusCatalog.DefenseUpPercent);
            // 退潮本身仍显示（无法涨潮），其 50% 易伤另并入易伤 icon

            if (attackPercent > 0)
                byId[StatusCatalog.AttackUpPercent] = attackPercent;
            if (damageReduction > 0)
                byId[StatusCatalog.DamageReduction] = damageReduction;
            if (vulnerable > 0)
                byId[StatusCatalog.Vulnerable] = vulnerable;
            if (blockGainPercent > 0)
                byId[StatusCatalog.DefenseUpPercent] = blockGainPercent;

            // 闪避率：evade icon + 百分比（含技能/遗物/烟雾弹/蝙蝠首击等）
            var dodgePercent = ResolveDodgeChancePercent(state, combatant);
            if (dodgePercent > 0)
                byId[StatusCatalog.DodgeChance] = dodgePercent;

            // 烈火长剑：前排脚标用遗物小 icon（无百分比文字）
            if (IsBurningLongswordActive(state, combatant))
                byId[RelicIds.BurningLongsword] = 1;

            foreach (var pair in byId)
            {
                if (pair.Value <= 0)
                    continue;
                list.Add(new FootStatusEntry { StatusId = pair.Key, Stacks = pair.Value });
            }

            return list;
        }

        public static int ResolveDodgeChancePercent(BattleState state, CombatantState combatant)
        {
            if (combatant == null || !combatant.IsAlive)
                return 0;

            var dodge = combatant.DodgeChanceBonus;
            if (state != null)
                dodge += state.ConsumableDodgeBonusThisTurn;

            var mods = state?.Config?.RunModifiers;
            if (mods != null && combatant.Team == TeamSide.Player)
                dodge += mods.DodgeChanceOnHit;

            // 巨翼蝙蝠：每回合首次受击前 50% 闪避
            if (combatant.FirstHitDodgePending
                && MinionTraitRules.HasTrait(combatant, MinionTraitCatalog.BatFirstHitDodge))
                dodge += MinionTraitCatalog.BatFirstHitDodgeChance;

            if (dodge <= 0f)
                return 0;

            return System.Math.Max(1, (int)System.Math.Round(dodge * 100f));
        }

        public static bool IsBurningLongswordActive(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null || combatant.Team != TeamSide.Player || !combatant.IsAlive)
                return false;

            var mods = state.Config?.RunModifiers;
            if (mods == null || mods.FrontRowBurnTargetDamageMultiplier <= 1f)
                return false;

            return PositionRules.GetEffectiveSlot(state, combatant) == FormationSlot.Front;
        }

        /// <summary>兼容旧调用：前排烈火长剑生效时返回展示用增伤百分比（脚标已改用遗物 icon，不再显示此值）。</summary>
        public static bool TryGetBurningLongswordDisplayPercent(
            BattleState state,
            CombatantState combatant,
            out int percent)
        {
            percent = 0;
            if (!IsBurningLongswordActive(state, combatant))
                return false;

            var mods = state.Config?.RunModifiers;
            percent = (int)System.Math.Round((mods.FrontRowBurnTargetDamageMultiplier - 1f) * 100f);
            return percent > 0;
        }

        static int SumAttackPercentForIcon(
            CombatantState combatant,
            Dictionary<string, int> byId,
            BattleState state)
        {
            var total = 0;
            if (byId.TryGetValue(StatusCatalog.AttackUpPercent, out var atkPct))
                total += atkPct;
            if (byId.TryGetValue(StatusCatalog.WaveSurge, out var wave))
                total += wave;
            if (byId.TryGetValue(StatusCatalog.PhantomCaptainFrenzyAtk, out var frenzy))
                total += frenzy;
            if (byId.TryGetValue(StatusCatalog.KnightAssaultStanceAtk, out var assaultAtk))
                total += assaultAtk;
            if (byId.TryGetValue(StatusCatalog.KnightBackToWallAtk, out var backToWall))
                total += backToWall;
            if (byId.TryGetValue(StatusCatalog.RangerLowHpFuryAtk, out var lowHpFury))
                total += lowHpFury;
            if (byId.TryGetValue(StatusCatalog.RangerSoloHuntAtk, out var soloHunt))
                total += soloHunt;
            if (byId.TryGetValue(StatusCatalog.KnightComboAtk, out var combo))
                total += combo;
            if (byId.TryGetValue(StatusCatalog.RangerBloodDebtAtk, out var bloodDebt))
                total += bloodDebt;

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

            // 龙纹指环 / 翡翠短刀 / 烈焰之剑等：遗物全队增伤
            if (combatant.Team == TeamSide.Player
                && state?.Config?.RunModifiers != null
                && state.Config.RunModifiers.TeamAttackBonusPercent > 0f)
            {
                total += (int)System.Math.Round(state.Config.RunModifiers.TeamAttackBonusPercent);
            }

            return total;
        }

        static int SumDamageReductionForIcon(
            CombatantState combatant,
            Dictionary<string, int> byId,
            BattleState state,
            int? blockOverride = null)
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

            // 城堡骑士：战士有护甲时 20% 减伤
            total += ResolveWarriorBlockDamageReductionPercent(state, combatant, blockOverride);

            return total;
        }

        static int ResolveWarriorBlockDamageReductionPercent(
            BattleState state,
            CombatantState combatant,
            int? blockOverride = null)
        {
            if (state == null || combatant == null || combatant.Team != TeamSide.Player || !combatant.IsAlive)
                return 0;

            var block = blockOverride ?? combatant.Block;
            if (block <= 0)
                return 0;

            if (combatant.CharacterDefinitionId is not (RelicEffectRules.WarriorCharacterId or "char_warrior"))
                return 0;

            var mods = state.Config?.RunModifiers;
            if (mods == null || mods.WarriorBlockDamageReductionPercent <= 0f)
                return 0;

            return (int)System.Math.Round(mods.WarriorBlockDamageReductionPercent);
        }

        static int SumBlockGainPercentForIcon(
            CombatantState combatant,
            Dictionary<string, int> byId,
            BattleState state)
        {
            var total = 0;
            if (byId.TryGetValue(StatusCatalog.DefenseUpPercent, out var pct))
                total += pct;

            // 铁壁战甲 / 圣骑之盾等：遗物强固（TeamBlockGainBonusPercent）
            if (combatant != null
                && combatant.Team == TeamSide.Player
                && state?.Config?.RunModifiers != null
                && state.Config.RunModifiers.TeamBlockGainBonusPercent > 0f)
            {
                total += (int)System.Math.Round(state.Config.RunModifiers.TeamBlockGainBonusPercent);
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
            if (byId.TryGetValue(StatusCatalog.DarkKnightPoisonVulnerable, out var darkKnight))
                total += darkKnight;
            if (byId.TryGetValue(StatusCatalog.PhantomCaptainFrenzyVuln, out var frenzy))
                total += frenzy;
            if (byId.TryGetValue(StatusCatalog.KnightAssaultStanceVuln, out var assaultVuln))
                total += assaultVuln;

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
