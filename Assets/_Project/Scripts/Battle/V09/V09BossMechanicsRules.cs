using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.V09
{
    /// <summary>v0.9 新增 Boss：典狱长 / 黑暗骑士 / 腐化海洋女神。</summary>
    public static class V09BossMechanicsRules
    {
        public const int CageTurnStartSelfDamage = 10;
        public const int WardenNoCageAttackBonusPercent = 50;
        public const int RisingTideEbbThreshold = 6;
        public const int TideLockedStackCount = 4;

        public static void ProcessBattleStart(BattleState state, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null)
                return;

            var wardens = new List<CombatantState>();
            foreach (var combatant in state.Combatants)
            {
                if (combatant.IsAlive && BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.WardenCageMaster))
                    wardens.Add(combatant);
            }

            foreach (var warden in wardens)
            {
                warden.Slot = FormationSlot.Back;
                SpawnInitialCages(state, warden, events, rng, count: 2);
            }
        }

        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (!combatant.IsAlive)
                    continue;

                if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.OceanGoddessTide))
                    ProcessOceanGoddessTurnStart(state, combatant, events);

                if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.PrisonCage))
                    ApplyTickDamage(state, combatant, CageTurnStartSelfDamage, "囚笼损耗", events, rng);

                if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.WardenCageMaster))
                    TryApplyWardenNoCageRage(state, combatant, events);
            }

            if (IsDarkKnightPoisonAuraActive(state))
            {
                foreach (var player in state.GetTeam(TeamSide.Player))
                {
                    if (!player.IsAlive)
                        continue;

                    StatusRules.ApplyStatus(state, player, StatusCatalog.Poison, 1, -1, events);
                }
            }

            SyncAllDarkKnightPoisonVulnerability(state, events);
        }

        public static void OnCharacterDied(BattleState state, CombatantState combatant, List<BattleEvent> events, BattleRng rng)
        {
            if (state == null || combatant == null)
                return;

            if (BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.DarkKnightPoisonAura))
                SyncAllDarkKnightPoisonVulnerability(state, events);

            if (!BossTraitRules.HasTrait(combatant, CharacterTraitCatalog.PrisonCage))
                return;

            ClearBrandFromAllPlayers(state, events);
            SpawnCageReplacementElite(state, combatant, events, rng);
        }

        public static void TryDetonateBrand(BattleState state, CombatantState target, List<BattleEvent> events)
        {
            if (target == null || !target.IsAlive)
                return;

            var brand = StatusRules.FindStatus(target, StatusCatalog.BrandMark);
            if (brand == null || brand.Stacks < 3)
                return;

            StatusRules.RemoveStatus(target, StatusCatalog.BrandMark, brand.Stacks, events);
            ApplyTickDamage(state, target, target.MaxHp, "烙印引爆", events);
        }

        public static bool CanGainRisingTide(CombatantState combatant)
        {
            if (combatant == null)
                return false;

            if (StatusRules.HasStatus(combatant, StatusCatalog.EbbingTide))
                return false;

            return true;
        }

        public static void AdjustRisingTideStacks(
            BattleState state,
            CombatantState combatant,
            int delta,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || delta == 0)
                return;

            if (StatusRules.HasStatus(combatant, StatusCatalog.TideLocked))
            {
                WriteRisingTideStacks(state, combatant, TideLockedStackCount, events, triggerEbb: false);
                return;
            }

            if (delta > 0 && !CanGainRisingTide(combatant))
                return;

            var current = StatusRules.GetStatusStacks(combatant, StatusCatalog.RisingTide);
            var next = System.Math.Max(0, current + delta);
            WriteRisingTideStacks(state, combatant, next, events, triggerEbb: true);
        }

        static void WriteRisingTideStacks(
            BattleState state,
            CombatantState combatant,
            int next,
            List<BattleEvent> events,
            bool triggerEbb)
        {
            if (combatant == null)
                return;

            next = System.Math.Max(0, next);
            var current = StatusRules.GetStatusStacks(combatant, StatusCatalog.RisingTide);
            if (next == current)
                return;

            if (next == 0)
            {
                var existing = StatusRules.FindStatus(combatant, StatusCatalog.RisingTide);
                if (existing != null)
                    StatusRules.RemoveStatus(combatant, StatusCatalog.RisingTide, existing.Stacks, events);
            }
            else if (current == 0)
            {
                StatusRules.ApplyStatus(state, combatant, StatusCatalog.RisingTide, next, -1, events);
            }
            else
            {
                var existing = StatusRules.FindStatus(combatant, StatusCatalog.RisingTide);
                existing.Stacks = next;
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, "涨潮")
                {
                    CombatantId = combatant.Id,
                    Amount = next,
                    TargetId = StatusCatalog.RisingTide
                });
            }

            RelicBattleRules.RefreshDerivedStats(state, combatant, state.Config?.RunModifiers);

            if (triggerEbb && next >= RisingTideEbbThreshold)
                TriggerEbbingTide(state, combatant, events);
        }

        public static void LockRisingTide(BattleState state, CombatantState combatant, int turns, List<BattleEvent> events)
        {
            if (combatant == null)
                return;

            // 卡面「持续 N 回合」：当回合末会扣 1，写入 N+1 以保证完整 N 回合锁定
            var applyTurns = turns > 0 ? turns + 1 : turns;
            StatusRules.ApplyStatus(state, combatant, StatusCatalog.TideLocked, 1, applyTurns, events);
            WriteRisingTideStacks(state, combatant, TideLockedStackCount, events, triggerEbb: false);
        }

        public static void ApplyTideEmpower(BattleState state, CombatantState combatant, List<BattleEvent> events)
        {
            if (combatant == null)
                return;

            StatusRules.ApplyStatus(state, combatant, StatusCatalog.TideEmpower, 1, -1, events);
        }

        public static void ApplyExtraTideDamageReduction(CombatantState combatant)
        {
            if (combatant == null)
                return;

            if (!StatusRules.HasStatus(combatant, StatusCatalog.TideEmpower))
                return;

            var tideStacks = StatusRules.GetStatusStacks(combatant, StatusCatalog.RisingTide);
            if (tideStacks > 0)
                combatant.IncomingDamageReductionPercent += tideStacks * 5;
        }

        /// <summary>
        /// 场上有黑暗骑士时，玩家每层中毒同步为 1 层可见易伤（+1% 受伤）。
        /// 伤害由 <see cref="StatusCatalog.DarkKnightPoisonVulnerable"/> 经 CombatModifierRules 结算。
        /// </summary>
        public static void SyncDarkKnightPoisonVulnerability(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events)
        {
            if (target == null || target.Team != TeamSide.Player)
                return;

            var poisonStacks = StatusRules.GetStatusStacks(target, StatusCatalog.Poison);
            var desired = 0;
            if (target.IsAlive)
            {
                if (state != null)
                {
                    if (IsDarkKnightPoisonAuraActive(state))
                        desired = poisonStacks;
                }
                else
                {
                    // 无 BattleState 时暂按中毒层数保留；带 state 的同步会校正（Boss 死亡等）
                    desired = poisonStacks;
                }
            }

            var current = StatusRules.GetStatusStacks(target, StatusCatalog.DarkKnightPoisonVulnerable);
            if (desired == current)
                return;

            if (desired <= 0)
            {
                StatusRules.RemoveAllStatus(target, StatusCatalog.DarkKnightPoisonVulnerable, events);
                CombatantRules.RefreshDerivedStats(target);
                RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);
                return;
            }

            var existing = StatusRules.FindStatus(target, StatusCatalog.DarkKnightPoisonVulnerable);
            if (existing == null)
            {
                StatusRules.ApplyStatusInternal(
                    state, target, StatusCatalog.DarkKnightPoisonVulnerable, desired, -1, events,
                    mirrorChainWraith: false);
                return;
            }

            existing.Stacks = desired;
            existing.RemainingTurns = -1;
            events?.Add(new BattleEvent(BattleEventKind.StatusApplied, "易伤")
            {
                CombatantId = target.Id,
                Amount = desired,
                TargetId = StatusCatalog.DarkKnightPoisonVulnerable
            });
            CombatantRules.RefreshDerivedStats(target);
            RelicBattleRules.RefreshDerivedStats(state, target, state?.Config?.RunModifiers);
        }

        public static void SyncAllDarkKnightPoisonVulnerability(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var unit in state.GetTeam(TeamSide.Player))
                SyncDarkKnightPoisonVulnerability(state, unit, events);
        }

        public static bool IsDarkKnightPoisonAuraActive(BattleState state)
        {
            if (state == null)
                return false;

            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (enemy.IsAlive && BossTraitRules.HasTrait(enemy, CharacterTraitCatalog.DarkKnightPoisonAura))
                    return true;
            }

            return false;
        }

        public static void DamageRandomAllyByCharacterId(
            BattleState state,
            CombatantState actor,
            string characterDefinitionId,
            int damage,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (state == null || actor == null || damage <= 0 || string.IsNullOrEmpty(characterDefinitionId))
                return;

            var pool = new List<CombatantState>();
            foreach (var ally in state.GetTeam(actor.Team))
            {
                if (ally.IsAlive && ally.CharacterDefinitionId == characterDefinitionId)
                    pool.Add(ally);
            }

            if (pool.Count == 0)
                return;

            var target = pool.Count == 1 || rng == null
                ? pool[0]
                : pool[rng.NextIndex(pool.Count)];
            DamageRules.ApplyDamage(
                state,
                actor,
                target,
                damage,
                CardType.Status,
                events,
                canTriggerParry: false,
                rng: rng);
        }

        public static void StripBlockThenDealDamage(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            if (target == null || !target.IsAlive)
                return;

            var stripped = target.Block;
            if (stripped > 0)
            {
                events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{target.DisplayName} 护甲被移除")
                {
                    CombatantId = target.Id,
                    Amount = stripped
                });
                target.Block = 0;
            }

            var bonusHits = stripped / 10;
            var total = action.Value + bonusHits * System.Math.Max(0, action.Stacks);
            if (total <= 0)
                return;

            DamageRules.ApplyDamage(
                state,
                actor,
                target,
                total,
                card?.CardType ?? CardType.Attack,
                events,
                rng: rng,
                cardCost: card?.Cost ?? 0,
                sourceCardInstanceId: sourceCardInstanceId);
        }

        public static void SwapRandomEnemies(
            BattleState state,
            TeamSide enemyTeam,
            int pairCount,
            BattleRng rng,
            List<BattleEvent> events,
            string applyStatusId = null,
            int applyStacks = 0,
            int applyDuration = 0)
        {
            if (state == null || pairCount <= 0)
                return;

            var alive = new List<CombatantState>();
            foreach (var unit in state.GetTeam(enemyTeam))
            {
                if (unit.IsAlive)
                    alive.Add(unit);
            }

            if (alive.Count < 2)
                return;

            for (var i = 0; i < pairCount; i++)
            {
                var aIndex = rng.NextIndex(alive.Count);
                var bIndex = rng.NextIndex(alive.Count);
                if (aIndex == bIndex)
                    bIndex = (bIndex + 1) % alive.Count;

                var a = alive[aIndex];
                var b = alive[bIndex];
                PositionRules.SwapCombatants(state, a, b, events, "站位交换");

                if (!string.IsNullOrEmpty(applyStatusId) && applyStacks > 0)
                {
                    StatusRules.ApplyStatus(state, a, applyStatusId, applyStacks, applyDuration, events);
                    StatusRules.ApplyStatus(state, b, applyStatusId, applyStacks, applyDuration, events);
                }
            }
        }

        public static void ApplyAttackUpPerSelfStatusStack(
            BattleState state,
            CombatantState actor,
            EffectActionSpec action,
            List<BattleEvent> events)
        {
            if (actor == null || action == null)
                return;

            var repeats = 1;
            if (!string.IsNullOrEmpty(action.RepeatPerStatusId))
                repeats = System.Math.Max(1, StatusRules.GetStatusStacks(actor, action.RepeatPerStatusId));

            for (var i = 0; i < repeats; i++)
            {
                StatusRules.ApplyStatus(
                    state,
                    actor,
                    action.StatusId,
                    System.Math.Max(1, action.Stacks),
                    action.Duration >= 0 ? action.Duration : 2,
                    events);
            }
        }

        static void ProcessOceanGoddessTurnStart(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events)
        {
            if (StatusRules.HasStatus(combatant, StatusCatalog.TideLocked))
            {
                WriteRisingTideStacks(state, combatant, TideLockedStackCount, events, triggerEbb: false);
                return;
            }

            if (!CanGainRisingTide(combatant))
                return;

            AdjustRisingTideStacks(state, combatant, 1, events);
        }

        static void TriggerEbbingTide(BattleState state, CombatantState combatant, List<BattleEvent> events)
        {
            var tide = StatusRules.FindStatus(combatant, StatusCatalog.RisingTide);
            if (tide != null)
                StatusRules.RemoveStatus(combatant, StatusCatalog.RisingTide, tide.Stacks, events);

            StatusRules.ApplyStatus(state, combatant, StatusCatalog.EbbingTide, 1, 2, events);
            RelicBattleRules.RefreshDerivedStats(state, combatant, state.Config?.RunModifiers);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "退潮")
            {
                CombatantId = combatant.Id
            });
        }

        static void SetRisingTideStacks(
            BattleState state,
            CombatantState combatant,
            int stacks,
            List<BattleEvent> events)
        {
            var current = StatusRules.GetStatusStacks(combatant, StatusCatalog.RisingTide);
            AdjustRisingTideStacks(state, combatant, stacks - current, events);
        }

        static void TryApplyWardenNoCageRage(
            BattleState state,
            CombatantState warden,
            List<BattleEvent> events)
        {
            if (HasAliveCage(state))
                return;

            if (StatusRules.HasStatus(warden, StatusCatalog.AttackUpPercent)
                && StatusRules.GetStatusStacks(warden, StatusCatalog.AttackUpPercent) >= WardenNoCageAttackBonusPercent)
                return;

            StatusRules.ApplyStatus(
                state,
                warden,
                StatusCatalog.AttackUpPercent,
                WardenNoCageAttackBonusPercent,
                -1,
                events);
        }

        static bool HasAliveCage(BattleState state)
        {
            foreach (var enemy in state.GetTeam(TeamSide.Enemy))
            {
                if (!enemy.IsAlive)
                    continue;

                if (enemy.CharacterDefinitionId == CharacterTraitCatalog.PrisonCageCharacterId
                    || BossTraitRules.HasTrait(enemy, CharacterTraitCatalog.PrisonCage))
                    return true;
            }

            return false;
        }

        static readonly FormationSlot[] InitialCageSlots =
        {
            FormationSlot.Front,
            FormationSlot.Middle
        };

        static void SpawnInitialCages(
            BattleState state,
            CombatantState warden,
            List<BattleEvent> events,
            BattleRng rng,
            int count)
        {
            if (!state.Config.SummonTemplates.TryGetValue(CharacterTraitCatalog.PrisonCageCharacterId, out var template))
                return;

            var spawned = 0;
            foreach (var slot in InitialCageSlots)
            {
                if (spawned >= count)
                    break;

                if (IsTeamSlotOccupied(state, warden.Team, slot))
                    continue;

                SummonRules.SpawnFromTemplate(state, template, slot, events);
                spawned++;
            }
        }

        static bool IsTeamSlotOccupied(BattleState state, TeamSide team, FormationSlot slot)
        {
            foreach (var unit in state.GetTeam(team))
            {
                if (unit.IsAlive && unit.Slot == slot)
                    return true;
            }

            return false;
        }

        static void SpawnCageReplacementElite(
            BattleState state,
            CombatantState deadCage,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || deadCage == null)
                return;

            var options = new[]
            {
                "char_skeleton_elite",
                "char_wraith_elite",
                "char_bat"
            };
            var pick = rng != null
                ? options[rng.NextIndex(options.Length)]
                : options[System.Math.Abs(deadCage.Id.GetHashCode()) % options.Length];
            if (!state.Config.SummonTemplates.TryGetValue(pick, out var template))
                return;

            SummonRules.SpawnFromTemplate(state, template, deadCage.Slot, events);
            SummonRules.MergeSummonedSkillPoolIntoTeamDeck(state, template, deadCage.Team, rng, events);
        }

        static void ClearBrandFromAllPlayers(BattleState state, List<BattleEvent> events)
        {
            foreach (var player in state.GetTeam(TeamSide.Player))
            {
                if (!player.IsAlive)
                    continue;

                var brand = StatusRules.FindStatus(player, StatusCatalog.BrandMark);
                if (brand == null || brand.Stacks <= 0)
                    continue;

                StatusRules.RemoveStatus(player, StatusCatalog.BrandMark, brand.Stacks, events);
            }
        }

        static void ApplyTickDamage(
            BattleState state,
            CombatantState combatant,
            int damage,
            string label,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (combatant == null || damage <= 0)
                return;

            combatant.Hp = System.Math.Max(0, combatant.Hp - damage);
            events.Add(new BattleEvent(BattleEventKind.StatusTickDamage, label)
            {
                CombatantId = combatant.Id,
                Amount = damage
            });

            if (!combatant.IsAlive)
            {
                events.Add(new BattleEvent(BattleEventKind.CharacterDied, combatant.DisplayName)
                {
                    CombatantId = combatant.Id
                });
                CombatantDeathRules.OnCharacterDied(state, combatant, events, rng);
            }
        }
    }
}
