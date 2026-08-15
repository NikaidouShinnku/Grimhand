using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Status;
using Grimhand.Battle.V091;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>遗物特殊效果的战斗内钩子（对照 RelicDatabase 图鉴）。</summary>
    public static class RelicEffectRules
    {
        public const string DemonCharacterId = "char_ranger";
        public const string WarriorCharacterId = "char_knight";

        public static void ResetTurnFlags(CombatantState combatant)
        {
            if (combatant == null)
                return;

            combatant.FirstAttackBonusPending = true;
            combatant.FirstDefenseBonusPending = true;
            combatant.FirstHitReductionPending = true;
            combatant.WarriorFirstHitBlockPending = true;
            combatant.UsedAttackThisTurn = false;
            combatant.UsedDefenseThisTurn = false;
            combatant.TurnAttackBonusPercent = 0;
            combatant.TurnDefenseBonusPercent = 0;
            combatant.HealedThisTurn = false;
            V091MechanicsRules.ResetCombatantTurnFlags(combatant);
            TalentBattleRules.ResetTurnFlags(combatant);
        }

        public static void ProcessTurnStart(
            BattleState state,
            BattleRng rng,
            List<BattleEvent> events)
        {
            if (state == null)
                return;

            var mods = state.Config?.RunModifiers;
            state.TeamFirstHitReductionPending = mods != null && mods.FirstHitDamageReductionPercent > 0f;

            foreach (var c in state.Combatants)
            {
                ResetTurnFlags(c);
                if (c.InvulnerableTurnsRemaining > 0)
                    c.InvulnerableTurnsRemaining--;
            }

            RelicBattleRules.RefreshAllDerivedStats(state);

            if (mods == null)
            {
                TalentBattleRules.ProcessTurnStart(state, events);
                return;
            }

            if (mods.TurnStartRandomAllyBlock > 0)
            {
                var allies = CollectAlivePlayerTeam(state);
                if (allies.Count > 0 && rng != null)
                {
                    var pick = allies[rng.NextIndex(allies.Count)];
                    DamageRules.ApplyBlock(pick, mods.TurnStartRandomAllyBlock, events, state);
                }
            }

            if (mods.TurnStartTeamBlock > 0)
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                    DamageRules.ApplyBlock(ally, mods.TurnStartTeamBlock, events, state);
            }

            if (mods.TurnStartEnemyDamage > 0)
            {
                foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                {
                    if (!enemy.IsAlive)
                        continue;

                    var before = enemy.Hp;
                    enemy.Hp = Math.Max(0, enemy.Hp - mods.TurnStartEnemyDamage);
                    var dealt = before - enemy.Hp;
                    if (dealt <= 0)
                        continue;

                    events.Add(new BattleEvent(BattleEventKind.DamageApplied,
                        $"赤红烈焰靴 -> {enemy.DisplayName}")
                    {
                        TargetId = enemy.Id,
                        Amount = dealt,
                        IsAoEWave = true
                    });

                    if (!enemy.IsAlive)
                    {
                        events.Add(new BattleEvent(BattleEventKind.CharacterDied, enemy.DisplayName)
                        {
                            CombatantId = enemy.Id
                        });
                        CombatantDeathRules.OnCharacterDied(state, enemy, events);
                    }
                }
            }

            if (mods.TurnStartEnemyBurnStacks > 0)
            {
                foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                {
                    if (!enemy.IsAlive)
                        continue;

                    // 永久灼烧用 -1：同持续时间分桶合并，避免 999 每回合剩回合数不同拆成多桶
                    StatusRules.ApplyStatus(state, enemy, StatusCatalog.Burn,
                        mods.TurnStartEnemyBurnStacks, -1, events);
                }
            }

            // 瓶中之灵：延迟伤由 DelayedDamage 状态在 V09 跳伤；此处清空本回合锁定的目标
            state.PhantomBottleFocusTargetId = null;

            TalentBattleRules.ProcessTurnStart(state, events);
        }

        public static void ProcessEndOfTurn(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            var mods = state.Config?.RunModifiers;

            if (mods != null && mods.EndTurnTeamHeal > 0)
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                    DamageRules.ApplyHeal(state, ally, mods.EndTurnTeamHeal, events);
            }

            if (mods != null && mods.EndTurnEnemyFireDamage > 0)
            {
                foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                {
                    if (!enemy.IsAlive)
                        continue;

                    var before = enemy.Hp;
                    enemy.Hp = Math.Max(0, enemy.Hp - mods.EndTurnEnemyFireDamage);
                    var dealt = before - enemy.Hp;
                    if (dealt <= 0)
                        continue;

                    events.Add(new BattleEvent(BattleEventKind.DamageApplied,
                        $"赤红烈焰靴 -> {enemy.DisplayName}")
                    {
                        TargetId = enemy.Id,
                        Amount = dealt,
                        IsAoEWave = true
                    });

                    if (!enemy.IsAlive)
                    {
                        events.Add(new BattleEvent(BattleEventKind.CharacterDied, enemy.DisplayName)
                        {
                            CombatantId = enemy.Id
                        });
                        CombatantDeathRules.OnCharacterDied(state, enemy, events);
                    }
                }
            }

            if (mods != null && mods.AttackAndDefenseSameTurnHeal > 0)
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                {
                    if (!ally.UsedAttackThisTurn || !ally.UsedDefenseThisTurn)
                        continue;

                    DamageRules.ApplyHeal(state, ally, mods.AttackAndDefenseSameTurnHeal, events);
                }
            }

            TalentBattleRules.ProcessEndOfTurn(state, events);
        }

        /// <summary>
        /// 开局速度加成已改为挂 SpeedUp 状态（见 BattleEngine.ApplyBattleStartRelicEffects），
        /// 此处保留接口兼容，避免与状态重复加算。
        /// </summary>
        public static int GetBattleSpeedBonus(BattleState state, CombatantState combatant) => 0;

        public static int AdjustSacrificeSelfDamage(
            RunModifierSnapshot mods,
            CombatantState actor,
            int rawDamage)
        {
            return AdjustSacrificeSelfDamage(null, mods, actor, rawDamage);
        }

        public static int AdjustSacrificeSelfDamage(
            BattleState state,
            RunModifierSnapshot mods,
            CombatantState actor,
            int rawDamage)
        {
            return TalentBattleRules.AdjustSacrificeSelfDamage(mods, state, actor, rawDamage);
        }

        public static void OnSacrificeCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (state == null || actor == null || card == null || !card.Keywords.Contains("sacrifice"))
                return;

            TalentBattleRules.OnSacrificeCardPlayed(state, actor, card);

            PassiveCardMechanicsRules.TryTriggerFinalBloodRitualOnSacrifice(
                state, actor, card, events, rng);
            PassiveCardMechanicsRules.TryTriggerBloodFrenzyOnSacrifice(state, actor, events);

            V091MechanicsRules.OnSacrificeCardPlayed(state, card);

            var mods = state.Config?.RunModifiers;
            if (mods == null || mods.SacrificeStackAttackBonus <= 0)
                return;

            if (actor.CharacterDefinitionId != DemonCharacterId)
                return;

            actor.SacrificeAttackStacks += mods.SacrificeStackAttackBonus;
            RelicBattleRules.RefreshDerivedStats(state, actor, mods);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{actor.DisplayName} 血祭坛 增伤+{mods.SacrificeStackAttackBonus}%")
            {
                CombatantId = actor.Id,
                Amount = actor.SacrificeAttackStacks,
                TargetId = StatusCatalog.DamageUp
            });
        }

        public static void OnCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || card == null)
                return;

            if (card.CardType == CardType.Attack)
            {
                actor.UsedAttackThisTurn = true;
                actor.FirstAttackBonusPending = false;
            }
            else if (card.CardType == CardType.Defense)
            {
                actor.UsedDefenseThisTurn = true;
                actor.FirstDefenseBonusPending = false;
            }

            if (card.CardType == CardType.Attack && rng != null)
                TryProcAttackBurn(state, actor, card, events, rng);

            OnSacrificeCardResolved(state, actor, card, events, rng);
            QueuePhantomBottleDamage(state, actor, events, rng);
            if (card.DefinitionId == PassiveCardMechanicsRules.SandSpearReforgeCardId)
                PassiveCardMechanicsRules.OnSandSpearReforgePlayed(state, actor, card, events, rng);
            if (actor.Team == TeamSide.Enemy)
                V091MechanicsRules.OnEnemyCardResolved(state, actor, events, rng);
            TalentBattleRules.OnCardResolved(state, actor, card, events);
            MinionTraitRules.OnCardResolved(state, actor, card, events);
            if (card.CardType == CardType.Attack)
                MinionTraitRules.ConsumeBloodRageAfterAttack(actor, card.CardType);
        }

        static void QueuePhantomBottleDamage(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || actor.Team != TeamSide.Player || rng == null)
                return;

            if (actor.CharacterDefinitionId is not ("char_lich_queen" or "char_lich"))
                return;

            var perCard = state.Config?.RunModifiers?.PhantomBottleDamagePerCard ?? 0;
            if (perCard <= 0)
                return;

            CombatantState target = null;
            if (!string.IsNullOrEmpty(state.PhantomBottleFocusTargetId))
            {
                foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                {
                    if (enemy != null && enemy.IsAlive && enemy.Id == state.PhantomBottleFocusTargetId)
                    {
                        target = enemy;
                        break;
                    }
                }
            }

            if (target == null)
            {
                var enemies = new List<CombatantState>();
                foreach (var enemy in state.GetTeam(TeamSide.Enemy))
                {
                    if (enemy != null && enemy.IsAlive)
                        enemies.Add(enemy);
                }

                if (enemies.Count == 0)
                    return;

                target = enemies[rng.NextIndex(enemies.Count)];
                state.PhantomBottleFocusTargetId = target.Id;
            }

            StatusRules.ApplyStatus(
                state, target, StatusCatalog.DelayedDamage, perCard, 1, events);

            for (var i = target.Statuses.Count - 1; i >= 0; i--)
            {
                var status = target.Statuses[i];
                if (status?.StatusId == StatusCatalog.DelayedDamage && status.Stacks > 0)
                    status.SourceCombatantId = actor.Id;
            }
        }

        public static void OnEnemyKilled(
            BattleState state,
            CombatantState killer,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || killer == null || state.JadeDaggerFirstKillConsumed)
                return;

            var mods = state.Config?.RunModifiers;
            if (mods == null || !mods.JadeDaggerFirstKillBonus)
                return;

            state.JadeDaggerFirstKillConsumed = true;
            // 下回合额外抽 1 / 回 2 能量（非当场）
            state.PendingDrawNextTurn += 1;
            state.PendingEnergyNextTurn += 2;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                "翡翠短刀：下回合额外抽1张牌并回复2点能量")
            {
                CombatantId = killer.Id,
                Amount = 1
            });
        }

        static void TryProcAttackBurn(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var mods = state.Config?.RunModifiers;
            if (mods == null || mods.AttackBurnProcChance <= 0f)
                return;

            if (actor.Team != TeamSide.Player)
                return;

            var roll = rng.NextUInt() % 1000u / 1000f;
            if (roll >= mods.AttackBurnProcChance)
                return;

            var targetId = state.ResolutionTargets.TryGetValue(card.InstanceId, out var id)
                ? id
                : null;
            var target = targetId != null ? state.GetCombatant(targetId) : null;
            target ??= PositionRules.PickDefaultTarget(state, actor.Team);
            if (target == null || !target.IsAlive)
                return;

            var stacks = mods.AttackBurnStacks > 0 ? mods.AttackBurnStacks : 5;
            var duration = mods.AttackBurnDurationTurns > 0 ? mods.AttackBurnDurationTurns : 5;
            StatusRules.ApplyStatus(state, target, StatusCatalog.Burn, stacks, duration, events);
        }

        public static bool TryDodgeIncoming(
            BattleState state,
            RunModifierSnapshot mods,
            CombatantState target,
            BattleRng rng)
        {
            if (target == null || rng == null)
                return false;

            var dodge = state?.ConsumableDodgeBonusThisTurn ?? 0f;
            dodge += target.DodgeChanceBonus;
            if (mods != null && target.Team == TeamSide.Player)
                dodge += mods.DodgeChanceOnHit;

            if (dodge <= 0f)
                return false;

            var roll = rng.NextUInt() % 1000u / 1000f;
            return roll < dodge;
        }

        public static bool TryMiracleLeafSave(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events,
            ref int hpDamage)
        {
            if (state == null || target == null || target.Team != TeamSide.Player)
                return false;

            if (state.MiracleLeafRevivesRemaining <= 0)
                return false;

            if (target.Hp - hpDamage > 0)
                return false;

            state.MiracleLeafRevivesRemaining--;
            var restore = Math.Max(1, (int)Math.Round(
                target.MaxHp * (state.Config?.RunModifiers?.MiracleLeafReviveHpPercent ?? 20) / 100f));
            // 例：10/100 吃到 20 伤 → 不会掉到负，直接落到 20% MaxHp
            target.Hp = restore;
            target.InvulnerableTurnsRemaining = System.Math.Max(target.InvulnerableTurnsRemaining, 1);
            hpDamage = 0;

            StatusRules.ApplyStatus(state, target, StatusCatalog.Invulnerable, 1, 1, events);

            events.Add(new BattleEvent(BattleEventKind.CharacterRevived,
                $"{target.DisplayName} 奇迹之叶（剩余 {state.MiracleLeafRevivesRemaining} 次）")
            {
                CombatantId = target.Id,
                Amount = restore
            });

            return true;
        }

        public static void AdjustRelicOutgoingDamage(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            CardType cardType,
            ref int outgoingPower,
            ref int ignoreDefPercent)
        {
            if (state == null || actor == null || target == null || cardType != CardType.Attack)
                return;

            if (actor.Team != TeamSide.Player || outgoingPower <= 0)
                return;

            var mods = state.Config?.RunModifiers;
            if (mods == null)
                return;

            if (PositionRules.GetEffectiveSlot(state, actor) != FormationSlot.Front)
                return;

            if (mods.FrontRowIgnoreArmorDamagePercent > 0)
            {
                ignoreDefPercent = Math.Max(ignoreDefPercent, 100);
                outgoingPower = Math.Max(1, (int)Math.Round(
                    outgoingPower * mods.FrontRowIgnoreArmorDamagePercent / 100f));
            }

            if (mods.FrontRowBurnTargetDamageMultiplier > 1f
                && StatusRules.HasStatus(target, StatusCatalog.Burn))
            {
                outgoingPower = Math.Max(1, (int)Math.Round(
                    outgoingPower * mods.FrontRowBurnTargetDamageMultiplier));
            }
        }

        public static void ApplyFelskullChoice(BattleState state, int choiceIndex, List<BattleEvent> events)
        {
            if (state == null || !state.AwaitingFelskullChoice)
                return;

            var mods = state.Config?.RunModifiers;
            if (mods == null)
                return;

            if (choiceIndex == 0)
            {
                // A：全队失去 5% HP，本场 +1 能量上限
                mods.ExtraEnergyCap += 1;
                state.EnergyMax += 1;
                foreach (var ally in CollectAlivePlayerTeam(state))
                {
                    var loss = Math.Max(1, (int)Math.Round(ally.MaxHp * 0.05f));
                    ally.Hp = Math.Max(1, ally.Hp - loss);
                    events?.Add(new BattleEvent(BattleEventKind.DamageApplied, $"{ally.DisplayName} 血祭换能")
                    {
                        TargetId = ally.Id,
                        Amount = loss
                    });
                }
            }
            else
            {
                // B：本场 -1 能量上限，全队获得 10% 增伤（永久，脚标用增伤 icon）
                mods.ExtraEnergyCap = Math.Max(0, mods.ExtraEnergyCap - 1);
                state.EnergyMax = Math.Max(1, state.EnergyMax - 1);
                if (state.EnergyCurrent > state.EnergyMax)
                    state.EnergyCurrent = state.EnergyMax;

                foreach (var ally in CollectAlivePlayerTeam(state))
                {
                    StatusRules.ApplyStatus(
                        state,
                        ally,
                        StatusCatalog.AttackUpPercent,
                        10,
                        -1,
                        events);
                }
            }

            mods.RequiresFelskullChoice = false;
        }

        public static bool ShouldExpandBackRowReach(
            BattleState state,
            CombatantState owner,
            CardInstanceState card)
        {
            var mods = state?.Config?.RunModifiers;
            if (mods == null || !mods.BackRowAttackAnyTarget || owner == null || card == null)
                return false;

            if (owner.Team != TeamSide.Player)
                return false;

            if (card.CardType != CardType.Attack)
                return false;

            return PositionRules.GetEffectiveSlot(state, owner) == FormationSlot.Back;
        }

        static List<CombatantState> CollectAlivePlayerTeam(BattleState state)
        {
            var list = new List<CombatantState>();
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player && c.IsAlive)
                    list.Add(c);
            }

            return list;
        }
    }
}
