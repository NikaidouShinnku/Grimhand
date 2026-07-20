using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    public static class StatusRules
    {
        public static int GetEffectiveSpeed(BattleState state, CombatantState combatant)
        {
            if (combatant == null)
                return 0;

            var speed = combatant.Speed;
            speed += RelicEffectRules.GetBattleSpeedBonus(state, combatant);
            foreach (var status in combatant.Statuses)
            {
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;
                speed += def.SpeedModifierPerStack * status.Stacks;
            }

            return speed < 0 ? 0 : speed;
        }

        public static int GetEffectiveSpeed(CombatantState combatant) =>
            GetEffectiveSpeed(null, combatant);

        public static void ApplyStatus(
            BattleState state,
            CombatantState target,
            string statusId,
            int stacks,
            int durationOverride,
            List<BattleEvent> events) =>
            ApplyStatusInternal(state, target, statusId, stacks, durationOverride, events, mirrorChainWraith: true);

        public static void ApplyStatusInternal(
            BattleState state,
            CombatantState target,
            string statusId,
            int stacks,
            int durationOverride,
            List<BattleEvent> events,
            bool mirrorChainWraith)
        {
            var def = StatusCatalog.Get(statusId);
            if (def == null || target == null || !target.IsAlive || stacks <= 0)
                return;

            // 减益免疫：拦截新减益（免疫自身不算减益）
            if (statusId != StatusCatalog.DebuffImmune
                && HasStatus(target, StatusCatalog.DebuffImmune)
                && IsDebuffDefinition(def))
                return;

            // 中毒按「持续时间」分桶叠层：同回合数合在一起，不同回合数分开（引爆时分别结算）。
            if (statusId == StatusCatalog.Poison)
            {
                ApplyPoisonBucket(state, target, stacks, durationOverride, events, mirrorChainWraith);
                return;
            }

            var existing = FindStatus(target, statusId);
            var isNew = existing == null;
            if (isNew)
            {
                // RemainingTurns=0：避免默认 -1 被误判为永久，再由下方规则写入真实持续。
                existing = new StatusInstance { StatusId = statusId, Stacks = 0, RemainingTurns = 0 };
                target.Statuses.Add(existing);
            }

            existing.Stacks += stacks;
            ApplyDuration(state, existing, isNew, durationOverride);

            if (def.MaxHpPercentBonusPerStack > 0 && stacks > 0)
            {
                var beforeMax = target.MaxHp;
                var bonusHp = System.Math.Max(1,
                    (int)System.Math.Round(beforeMax * def.MaxHpPercentBonusPerStack / 100f * stacks));
                target.MaxHp = beforeMax + bonusHp;
                // 血族传承等：只提高上限，不补当前生命
                if (statusId != StatusCatalog.BloodlineLegacy)
                    target.Hp = System.Math.Min(target.MaxHp, target.Hp + bonusHp);
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, def.DisplayName)
            {
                CombatantId = target.Id,
                Amount = existing.Stacks,
                TargetId = statusId
            });

            CombatantRules.RefreshDerivedStats(target);
            RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);

            // 骨之王座：获得状态时立即召唤一只易爆骷髅头（之后每回合开始仍会再召唤）
            if (statusId == StatusCatalog.BoneWorkshop && isNew && state != null)
            {
                if (SummonRules.TrySummonExplosiveSkull(state, target, events))
                    SummonRules.GrantSkullSelfDestructHands(state, events);
            }

            if (statusId == StatusCatalog.Ethereal)
            {
                V09NewMechanicsRules.IncrementEtherealEntryCount(state);
                V09NewMechanicsRules.OnEtherealGained(state, target, events);
            }

            if (mirrorChainWraith)
                MinionTraitRules.ShareChainWraithDebuff(state, target, statusId, stacks, durationOverride, events);

            if (statusId == StatusCatalog.BrandMark)
                V09BossMechanicsRules.TryDetonateBrand(state, target, events);

            if (statusId == StatusCatalog.Taunt && target.Team == TeamSide.Player)
                TargetRules.RefreshEnemyResolutionTargetsForTaunt(state);
        }

        static void ApplyPoisonBucket(
            BattleState state,
            CombatantState target,
            int stacks,
            int durationOverride,
            List<BattleEvent> events,
            bool mirrorChainWraith)
        {
            var def = StatusCatalog.Get(StatusCatalog.Poison);
            if (def == null)
                return;

            var turns = ResolveAppliedDurationTurns(state, durationOverride);
            StatusInstance bucket = null;
            foreach (var status in target.Statuses)
            {
                if (status?.StatusId == StatusCatalog.Poison && status.RemainingTurns == turns)
                {
                    bucket = status;
                    break;
                }
            }

            if (bucket == null)
            {
                bucket = new StatusInstance
                {
                    StatusId = StatusCatalog.Poison,
                    Stacks = 0,
                    RemainingTurns = turns
                };
                target.Statuses.Add(bucket);
            }

            bucket.Stacks += stacks;

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, def.DisplayName)
            {
                CombatantId = target.Id,
                Amount = GetStatusStacks(target, StatusCatalog.Poison),
                TargetId = StatusCatalog.Poison
            });

            CombatantRules.RefreshDerivedStats(target);
            RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);
            V09NewMechanicsRules.OnPoisonAppliedToSelf(state, target, StatusCatalog.Poison, events);
            MinionTraitRules.SyncSpiderPoisonVulnerability(state, target, events);

            if (mirrorChainWraith)
                MinionTraitRules.ShareChainWraithDebuff(
                    state, target, StatusCatalog.Poison, stacks, durationOverride, events);
        }

        static int ResolveAppliedDurationTurns(BattleState state, int durationOverride)
        {
            if (durationOverride < 0)
                return -1;

            var turns = durationOverride;
            var bonus = state?.Config?.RunModifiers?.StatusDurationBonusTurns ?? 0;
            if (bonus > 0)
                turns += bonus;
            return turns;
        }

        public static int GetStatusStacks(CombatantState target, string statusId)
        {
            if (target == null || string.IsNullOrEmpty(statusId))
                return 0;

            var total = 0;
            foreach (var status in target.Statuses)
            {
                if (status?.StatusId == statusId)
                    total += status.Stacks;
            }

            return total;
        }

        public static bool HasStatus(CombatantState target, string statusId) =>
            GetStatusStacks(target, statusId) > 0;

        public static void RemoveStatus(CombatantState target, string statusId, int stacks, List<BattleEvent> events)
        {
            if (target == null || string.IsNullOrEmpty(statusId) || stacks <= 0)
                return;

            if (IsUnclearedBuff(statusId))
                return;

            var remaining = stacks;
            for (var i = target.Statuses.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var existing = target.Statuses[i];
                if (existing?.StatusId != statusId || existing.Stacks <= 0)
                    continue;

                var take = System.Math.Min(existing.Stacks, remaining);
                existing.Stacks -= take;
                remaining -= take;
                if (existing.Stacks <= 0)
                    target.Statuses.RemoveAt(i);
            }

            var removed = stacks - remaining;
            if (removed <= 0)
                return;

            events?.Add(new BattleEvent(BattleEventKind.StatusRemoved, statusId)
            {
                CombatantId = target.Id,
                TargetId = statusId,
                Amount = removed
            });

            CombatantRules.RefreshDerivedStats(target);

            if (statusId == StatusCatalog.Poison)
                MinionTraitRules.SyncSpiderPoisonVulnerability(null, target, events);
        }

        public static void RemoveAllStatus(CombatantState target, string statusId, List<BattleEvent> events)
        {
            if (target == null || string.IsNullOrEmpty(statusId))
                return;

            RemoveStatus(target, statusId, GetStatusStacks(target, statusId), events);
        }

        public static void RemoveAllStatus(
            CombatantState target,
            string statusId,
            List<BattleEvent> events,
            BattleState state)
        {
            RemoveAllStatus(target, statusId, events);
            if (statusId == StatusCatalog.Poison)
                MinionTraitRules.SyncSpiderPoisonVulnerability(state, target, events);
        }

        public static void ProcessTurnStartStatuses(BattleState state, List<BattleEvent> events, BattleRng rng = null)
        {
            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                foreach (var status in combatant.Statuses)
                {
                    var def = StatusCatalog.Get(status.StatusId);
                    if (def == null || def.TurnStartDamagePerStack <= 0)
                        continue;

                    var damage = def.TurnStartDamagePerStack * status.Stacks;
                    ApplyStatusTickDamage(state, combatant, def, status.StatusId, damage, events, rng);
                }
            }
        }

        public static void ProcessTurnEndStatuses(BattleState state, List<BattleEvent> events, BattleRng rng = null)
        {
            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                foreach (var status in combatant.Statuses)
                {
                    var def = StatusCatalog.Get(status.StatusId);
                    if (def == null || def.TurnEndDamagePerStack <= 0)
                        continue;

                    var damage = def.TurnEndDamagePerStack * status.Stacks;
                    ApplyStatusTickDamage(state, combatant, def, status.StatusId, damage, events, rng);
                }
            }
        }

        static void ApplyStatusTickDamage(
            BattleState state,
            CombatantState combatant,
            StatusDefinition def,
            string statusId,
            int damage,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (combatant == null || damage <= 0)
                return;

            // v0.9 天赋：中毒跳伤前判定（免疫 / 转治疗 / 敌人中_dt毒回血）
            if (statusId == StatusCatalog.Poison)
                TalentBattleRules.OnPoisonTick(state, combatant, ref damage, events);
            if (damage <= 0)
                return;

            if (combatant.Team == TeamSide.Enemy)
                damage = V09NewMechanicsRules.ApplyPsionicBodyBonus(state, TeamSide.Player, damage);
            if (damage <= 0)
                return;

            // 中毒/灼烧跳伤：直扣 HP，不经过护甲与 DEF（设计表：中毒忽视护甲、灼烧忽视 DEF）。
            combatant.Hp = System.Math.Max(0, combatant.Hp - damage);
            events.Add(new BattleEvent(BattleEventKind.StatusTickDamage, def.DisplayName)
            {
                CombatantId = combatant.Id,
                Amount = damage,
                TargetId = statusId
            });

            // v0.9 瘟疫蔓延：敌人因中毒受伤时触发传染判定
            if (statusId == StatusCatalog.Poison && combatant.Team == TeamSide.Enemy && damage > 0 && rng != null)
                PassiveCardMechanicsRules.TryTriggerPlagueSpreadOnPoisonTick(state, combatant, events, rng);

            if (!combatant.IsAlive
                && CombatMechanicsRules.TryPreventDeathWithReviveBlessing(state, combatant, events))
            {
                return;
            }

            if (!combatant.IsAlive)
            {
                events.Add(new BattleEvent(BattleEventKind.CharacterDied, combatant.DisplayName)
                {
                    CombatantId = combatant.Id
                });
                CombatantDeathRules.OnCharacterDied(state, combatant, events);
            }
        }

        /// <summary>
        /// 回合开始：扣减「回合开始跳伤类」状态的持续（中毒/灼烧/亡灵毒/缠绕/延迟伤害）。
        /// 须在对应跳伤结算之后调用，保证持续 1 回合也能先跳伤再消失。
        /// 用快照遍历：到期回调可能 SelfDestruct/召唤，会改 Combatants。
        /// </summary>
        public static void ProcessTurnStartDurations(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            var snapshot = new List<CombatantState>(state.Combatants);
            foreach (var combatant in snapshot)
            {
                if (combatant == null || !combatant.IsAlive)
                    continue;

                TickDurationsOnCombatant(state, combatant, events, turnStartDotOnly: true);
            }
        }

        /// <summary>
        /// 回合结束：扣减非跳伤类状态的持续（易伤/强固/减速/虚化等「本回合」效果）。
        /// </summary>
        public static void ProcessEndOfTurnDurations(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            var snapshot = new List<CombatantState>(state.Combatants);
            foreach (var combatant in snapshot)
            {
                if (combatant == null)
                    continue;

                TickDurationsOnCombatant(state, combatant, events, turnStartDotOnly: false);
            }
        }

        /// <summary>回合开始跳伤类：持续在跳伤后结算；其余状态在回合结束结算。</summary>
        public static bool UsesTurnStartDuration(string statusId)
        {
            if (string.IsNullOrEmpty(statusId))
                return false;

            if (statusId == StatusCatalog.Poison
                || statusId == StatusCatalog.NecroticPoison
                || statusId == StatusCatalog.Burn
                || statusId == StatusCatalog.Constrict
                || statusId == StatusCatalog.DelayedDamage
                || statusId == StatusCatalog.SnakeGodChanneling
                || statusId == StatusCatalog.FinalSummonPending)
                return true;

            var def = StatusCatalog.Get(statusId);
            return def != null && def.TurnStartDamagePerStack > 0;
        }

        /// <summary>
        /// 层数与持续时间正交：
        /// - durationOverride &lt; 0（卡面「永久」）→ RemainingTurns = -1
        /// - durationOverride &gt;= 0 → 有限持续；叠层取更长；已永久不被缩短
        /// </summary>
        static void ApplyDuration(
            BattleState state,
            StatusInstance existing,
            bool isNew,
            int durationOverride)
        {
            // 卡面 Duration:-1 → 永久。战斗被动（天神下凡等）也以 -1 施加。
            if (durationOverride < 0)
            {
                existing.RemainingTurns = -1;
                return;
            }

            // durationOverride >= 0：有限持续（尊重卡面回合数，不再被目录 Permanent 吞掉）
            var turns = durationOverride;
            var bonus = state?.Config?.RunModifiers?.StatusDurationBonusTurns ?? 0;
            if (bonus > 0)
                turns += bonus;

            // 终焉召唤「N回合后」：回合开始扣持续；需再经过 N 个完整回合才触发
            // → RemainingTurns = N+1（例：卡面 3 → 挂 4，第 4 次回合初到期）
            if (existing.StatusId == StatusCatalog.FinalSummonPending && turns > 0)
                turns += 1;

            // 已是永久：叠有限持续不降级
            if (!isNew && existing.RemainingTurns < 0)
                return;

            if (isNew || turns > existing.RemainingTurns)
                existing.RemainingTurns = turns;
        }

        static void TickDurationsOnCombatant(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events,
            bool turnStartDotOnly)
        {
            for (var i = combatant.Statuses.Count - 1; i >= 0; i--)
            {
                var status = combatant.Statuses[i];
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;

                var isTurnStartDot = UsesTurnStartDuration(status.StatusId);
                if (turnStartDotOnly != isTurnStartDot)
                    continue;

                // 永久实例：RemainingTurns < 0
                if (status.RemainingTurns < 0)
                    continue;

                status.RemainingTurns--;
                if (status.RemainingTurns > 0)
                    continue;

                if (status.StatusId == StatusCatalog.FinalSummonPending)
                    PassiveCardMechanicsRules.OnFinalSummonPendingExpired(state, combatant, events);

                // v0.9 蛇 s2_lv10：中毒到期层数减半续存而非清除
                if (status.StatusId == StatusCatalog.Poison
                    && TalentBattleRules.TryHandlePoisonExpiry(state, combatant, status, events))
                {
                    CombatantRules.RefreshDerivedStats(combatant);
                    RelicBattleRules.RefreshDerivedStats(state, combatant, state?.Config?.RunModifiers);
                    MinionTraitRules.SyncSpiderPoisonVulnerability(state, combatant, events);
                    continue;
                }

                var expiredStatusId = status.StatusId;
                combatant.Statuses.RemoveAt(i);
                events.Add(new BattleEvent(BattleEventKind.StatusExpired, def.DisplayName)
                {
                    CombatantId = combatant.Id,
                    TargetId = expiredStatusId
                });
                MinionTraitRules.ClearSharedChainWraithDebuff(state, combatant, expiredStatusId, events);
                CombatantRules.RefreshDerivedStats(combatant);
                RelicBattleRules.RefreshDerivedStats(state, combatant, state?.Config?.RunModifiers);
                if (expiredStatusId == StatusCatalog.Poison)
                    MinionTraitRules.SyncSpiderPoisonVulnerability(state, combatant, events);
            }
        }

        public static StatusInstance FindStatus(CombatantState target, string statusId)
        {
            foreach (var s in target.Statuses)
            {
                if (s.StatusId == statusId)
                    return s;
            }

            return null;
        }

        public static bool HasDebuff(CombatantState target)
        {
            if (target == null)
                return false;

            foreach (var status in target.Statuses)
            {
                if (status.Stacks <= 0)
                    continue;

                var def = StatusCatalog.Get(status.StatusId);
                if (def != null && IsDebuffDefinition(def))
                    return true;
            }

            return false;
        }

        public static bool HasAnyStatus(CombatantState target)
        {
            if (target == null)
                return false;

            foreach (var status in target.Statuses)
            {
                if (status.Stacks > 0)
                    return true;
            }

            return false;
        }

        public static bool IsDebuffDefinition(StatusDefinition def)
        {
            if (def == null)
                return false;

            return def.TurnStartDamagePerStack > 0
                   || def.TurnEndDamagePerStack > 0
                   || def.SpeedModifierPerStack < 0
                   || def.AttackModifierPerStack < 0
                   || def.DefenseModifierPerStack < 0
                   || def.AttackPercentBonusPerStack < 0
                   || def.DefensePercentBonusPerStack < 0
                   || def.OutgoingDamageReductionFlatPerStack > 0
                   || def.BlockGainReductionPercentPerStack > 0
                   || def.IncomingDamagePercentPerStack > 0
                   || def.Id == StatusCatalog.Deterrence
                   || def.Id == StatusCatalog.SoulDrain;
        }

        public static bool IsBuffDefinition(StatusDefinition def)
        {
            if (def == null || IsDebuffDefinition(def))
                return false;

            // 标记类/机制状态不算可偷取增益
            if (def.Id is StatusCatalog.DebuffImmune
                or StatusCatalog.SealNextStatusCard
                or StatusCatalog.SealedNextCard
                or StatusCatalog.Taunt
                or StatusCatalog.BoneWorkshop
                or StatusCatalog.GhostQueenWrath
                or StatusCatalog.FinalSummonPending
                or StatusCatalog.RatSwarmCall
                or StatusCatalog.BrandMark
                or StatusCatalog.RisingTide
                or StatusCatalog.WaveSurge
                or StatusCatalog.EbbingTide
                or StatusCatalog.TideLocked
                or StatusCatalog.TideEmpower)
                return false;

            return def.SpeedModifierPerStack > 0
                   || def.AttackModifierPerStack > 0
                   || def.DefenseModifierPerStack > 0
                   || def.AttackPercentBonusPerStack > 0
                   || def.DefensePercentBonusPerStack > 0
                   || def.IncomingDamageReductionPercentPerStack > 0
                   || def.BlockGainPercentPerStack > 0
                   || def.BlockGainFlatPerStack > 0
                   || def.MaxHpPercentBonusPerStack > 0
                   || def.Id is StatusCatalog.AttackUp
                       or StatusCatalog.DefenseUp
                       or StatusCatalog.AttackUpPercent
                       or StatusCatalog.DefenseUpPercent
                       or StatusCatalog.ArmorUp
                       or StatusCatalog.DamageUp
                       or StatusCatalog.Guard
                       or StatusCatalog.VampAura
                       or StatusCatalog.SpeedUp
                       or StatusCatalog.SnakeSwiftness
                       or StatusCatalog.DamageReduction
                       or StatusCatalog.Unyielding;
        }

        /// <summary>不可被驱散/偷取的机制增益（幽灵女王之怒等）。</summary>
        public static bool IsUnclearedBuff(string statusId) =>
            statusId == StatusCatalog.GhostQueenWrath
            || statusId == StatusCatalog.BoneWorkshop;

        public static void ClearAllDebuffs(CombatantState target, List<BattleEvent> events)
        {
            if (target == null)
                return;

            for (var i = target.Statuses.Count - 1; i >= 0; i--)
            {
                var existing = target.Statuses[i];
                if (existing == null || existing.Stacks <= 0)
                    continue;
                var def = StatusCatalog.Get(existing.StatusId);
                if (!IsDebuffDefinition(def))
                    continue;
                RemoveAllStatus(target, existing.StatusId, events);
            }

            CombatantRules.RefreshDerivedStats(target);
        }
    }
}
