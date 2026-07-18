using System;
using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Status;
using Grimhand.Battle.V091;
using Grimhand.Core;

namespace Grimhand.Battle.Rules
{
    /// <summary>天赋战斗内钩子。</summary>
    public static class TalentBattleRules
    {
        public const string KnightId = RelicEffectRules.WarriorCharacterId;
        public const string MageId = RelicBattleRules.PharaohCharacterId;
        public const string RangerId = RelicEffectRules.DemonCharacterId;
        public const string SnakeQueenId = "char_snake_queen";
        public const string LichQueenId = "char_lich_queen";

        public static void OnBattleInitialized(BattleState state)
        {
            if (state?.Config?.Talents == null)
                return;

            state.TalentMageFirstStatusDiscountPending = HasTalent(state, "talent_mage_s2_lv2");
            state.TalentMageFirstHitSlowPending = HasTalent(state, "talent_mage_s2_lv6");
            state.TalentMageReviveAvailable = state.Config.Talents.MageReviveAvailable;
            state.TalentRangerBloodDebtAttackBonus = state.Config.Talents.RangerBloodDebtAttackBonus;
            state.TalentLichFirstExhaustDiscountPending = HasTalent(state, "talent_lich_s2_lv5");

            foreach (var combatant in state.Combatants)
            {
                if (combatant.Team != TeamSide.Player)
                    continue;

                if (combatant.CharacterDefinitionId == KnightId && HasTalent(state, "talent_knight_s2_lv10"))
                    combatant.TalentDisableBlockGain = true;
            }
        }

        public static void ProcessTurnStart(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
            {
                if (combatant.Team != TeamSide.Player || !combatant.IsAlive)
                    continue;

                if (HasTalent(state, "talent_knight_s1_lv1")
                    && combatant.CharacterDefinitionId == KnightId
                    && PositionRules.GetEffectiveSlot(state, combatant) == FormationSlot.Front)
                {
                    DamageRules.ApplyBlock(combatant, 2, events, state);
                }
            }
        }

        public static void ProcessEndOfTurn(BattleState state, List<BattleEvent> events)
        {
            if (state == null || !HasTalent(state, "talent_knight_s1_lv5"))
                return;

            foreach (var ally in CollectAlivePlayerTeam(state))
            {
                if (ally.CharacterDefinitionId != KnightId || ally.Block <= 0)
                    continue;

                DamageRules.ApplyHeal(state, ally, 2, events, ally);
            }
        }

        public static void ResetTurnFlags(CombatantState combatant)
        {
            if (combatant == null)
                return;

            combatant.TalentAttackCardsThisTurn = 0;
        }

        public static void ApplyDerivedStatModifiers(
            BattleState state,
            CombatantState combatant,
            RunModifierSnapshot mods)
        {
            ApplyCombatModifiers(state, combatant, mods);
        }

        /// <summary>v0.8：天赋/特性出站与护甲修饰符（不再改 ATK/DEF）。</summary>
        public static void ApplyCombatModifiers(
            BattleState state,
            CombatantState combatant,
            RunModifierSnapshot mods)
        {
            if (state == null || combatant == null || !combatant.IsAlive || combatant.Team != TeamSide.Player)
                return;

            var effective = PositionRules.GetEffectiveSlot(state, combatant);

            if (combatant.CharacterDefinitionId == KnightId)
            {
                if (HasTalent(state, "talent_knight_s1_lv3") && effective != FormationSlot.Front)
                {
                    combatant.OutgoingDamagePercentBonus += 33;
                    combatant.IncomingDamagePercentBonus += 33;
                }

                if (HasTalent(state, "talent_knight_s1_lv7")
                    && combatant.Hp < combatant.Block)
                {
                    combatant.OutgoingDamagePercentBonus += 20;
                }

                if (HasTalent(state, "talent_knight_s2_lv8")
                    && combatant.TalentAttackCardsThisTurn >= 3)
                {
                    combatant.OutgoingDamagePercentBonus += 33;
                }
            }

            if (combatant.CharacterDefinitionId == RangerId)
            {
                if (HasTalent(state, "talent_ranger_s1_lv7")
                    && combatant.Hp * 100 / Math.Max(1, combatant.MaxHp) < 30)
                {
                    combatant.OutgoingDamagePercentBonus += 25;
                }

                if (HasTalent(state, "talent_ranger_s2_lv8")
                    && state.Config?.Talents?.NonBossSoloEnemyBattle == true)
                {
                    combatant.OutgoingDamagePercentBonus += 30;
                }

                if (state.TalentRangerBloodDebtAttackBonus > 0)
                    combatant.OutgoingDamageFlatBonus += state.TalentRangerBloodDebtAttackBonus;
            }

            if (combatant.SacrificeAttackStacks > 0)
                combatant.OutgoingDamagePercentBonus += combatant.SacrificeAttackStacks;
        }

        public static void ApplyTeamHpBonus(BattleState state, RunModifierSnapshot mods)
        {
            if (state == null || !HasTalent(state, "talent_knight_s2_lv6"))
                return;

            if (!IsKnightAlive(state))
                return;

            foreach (var ally in CollectAlivePlayerTeam(state))
            {
                ally.MaxHp += 10;
                if (!ally.EnteredFromExpeditionDeath)
                    ally.Hp += 10;
            }
        }

        public static float GetOutgoingDamageMultiplier(
            BattleState state,
            CombatantState actor,
            CardType cardType)
        {
            if (state == null || actor == null || cardType != CardType.Attack)
                return 1f;

            var mul = 1f;
            if (actor.TalentRespondAttackBonusPending && HasTalent(state, "talent_knight_s2_lv3"))
            {
                mul *= 1.2f;
                actor.TalentRespondAttackBonusPending = false;
            }

            if (actor.TalentIronWallPendingDamageBonus > 0 && HasTalent(state, "talent_knight_s2_lv10"))
            {
                // 铁壁转化：下一张伤害牌额外 +N（N=被转化的护甲量）
                // 通过 flat bonus 在 GetOutgoingDamageFlatBonus 处理
            }

            return mul;
        }

        public static int GetOutgoingDamageFlatBonus(
            BattleState state,
            CombatantState actor,
            CardType cardType)
        {
            if (state == null || actor == null || cardType != CardType.Attack)
                return 0;

            if (actor.TalentIronWallPendingDamageBonus > 0 && HasTalent(state, "talent_knight_s2_lv10"))
            {
                var bonus = actor.TalentIronWallPendingDamageBonus;
                actor.TalentIronWallPendingDamageBonus = 0;
                return bonus;
            }

            return 0;
        }

        public static int ApplyIncomingDamageTalents(
            BattleState state,
            CombatantState target,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (target == null || hpDamage <= 0)
                return hpDamage;

            if (target.TalentRespondDamageReductionPending && HasTalent(state, "talent_knight_s2_lv2"))
            {
                target.TalentRespondDamageReductionPending = false;
                hpDamage = (int)Math.Round(hpDamage * 0.8f);
            }

            if (hpDamage > 0 && target.Hp - hpDamage <= 0)
                TryKnightLastStand(state, target, events, ref hpDamage);

            return hpDamage;
        }

        public static bool TryMageRevive(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events,
            ref int hpDamage)
        {
            if (state == null || target == null || target.CharacterDefinitionId != MageId)
                return false;

            if (!state.TalentMageReviveAvailable || !HasTalent(state, "talent_mage_s1_lv5"))
                return false;

            if (target.Hp - hpDamage > 0)
                return false;

            state.TalentMageReviveAvailable = false;
            var restore = Math.Max(1, (int)Math.Round(target.MaxHp * 0.3f));
            target.Hp = restore;
            hpDamage = 0;

            events.Add(new BattleEvent(BattleEventKind.CharacterRevived, $"{target.DisplayName} 法老复苏")
            {
                CombatantId = target.Id,
                Amount = restore
            });

            RelicBattleRules.RefreshDerivedStats(state, target, state.Config?.RunModifiers);
            return true;
        }

        public static void OnCharacterDied(BattleState state, CombatantState combatant, List<BattleEvent> events)
        {
            if (state == null || combatant == null || combatant.Team != TeamSide.Player)
                return;

            if (combatant.CharacterDefinitionId != MageId || !HasTalent(state, "talent_mage_s1_lv10"))
                return;

            var block = 25;
            foreach (var ally in CollectAlivePlayerTeam(state))
                DamageRules.ApplyBlock(ally, block, events, state);

            events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{combatant.DisplayName} 临终庇护")
            {
                CombatantId = combatant.Id,
                Amount = block
            });
        }

        public static void OnRespondSuccess(BattleState state, CombatantState actor)
        {
            if (state == null || actor == null || actor.CharacterDefinitionId != KnightId)
                return;

            if (HasTalent(state, "talent_knight_s2_lv2"))
                actor.TalentRespondDamageReductionPending = true;

            if (HasTalent(state, "talent_knight_s2_lv3"))
                actor.TalentRespondAttackBonusPending = true;
        }

        public static void OnCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || card == null)
                return;

            if (card.CardType == CardType.Attack
                && actor.CharacterDefinitionId == KnightId
                && HasTalent(state, "talent_knight_s2_lv8"))
            {
                actor.TalentAttackCardsThisTurn++;
                RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);
            }
        }

        public static void OnSacrificeHpSpent(BattleState state, CombatantState actor, int hpSpent)
        {
            if (state == null || actor == null || hpSpent <= 0)
                return;

            if (actor.CharacterDefinitionId != RangerId)
                return;

            state.TalentSacrificeHpAccumulatedBattle += hpSpent;

            if (HasTalent(state, "talent_ranger_s2_lv4"))
                actor.TalentNextSacrificeEnergyDiscount = true;
        }

        public static int AdjustSacrificeSelfDamage(
            RunModifierSnapshot mods,
            BattleState state,
            CombatantState actor,
            int rawDamage)
        {
            if (actor == null || rawDamage <= 0 || actor.CharacterDefinitionId != RangerId)
                return rawDamage;

            var damage = rawDamage;
            if (mods != null)
            {
                if (mods.SacrificeHpCostReductionPercent > 0f)
                {
                    damage = (int)Math.Round(
                        damage * (100f - mods.SacrificeHpCostReductionPercent) / 100f);
                }

                if (mods.SacrificeHpCostIncreasePercent > 0f)
                {
                    damage = (int)Math.Round(
                        damage * (100f + mods.SacrificeHpCostIncreasePercent) / 100f);
                }
            }

            if (HasTalent(state, "talent_ranger_s2_lv3") && damage < 5)
                return 0;

            return Math.Max(1, damage);
        }

        public static int GetEffectivePlayCost(
            BattleState state,
            CombatantState owner,
            CardInstanceState card)
        {
            if (state == null || owner == null || card == null)
                return card?.Cost ?? 0;

            var cost = CardPowerRules.UsesRemainingEnergyCost(card)
                ? CardPowerRules.GetRemainingEnergyPlayCost(state, card)
                : card.Cost;

            if (owner.TalentNextSacrificeEnergyDiscount
                && HasTalent(state, "talent_ranger_s2_lv4"))
            {
                cost = Math.Max(0, cost - 1);
            }

            if (state.TalentMageFirstStatusDiscountPending
                && owner.CharacterDefinitionId == MageId
                && card.CardType == CardType.Status
                && HasTalent(state, "talent_mage_s2_lv2"))
            {
                cost = Math.Max(0, cost - 1);
            }

            // 巫妖女王 s2_lv5：每场战斗首张消耗牌 -1 费
            if (state.TalentLichFirstExhaustDiscountPending
                && owner.CharacterDefinitionId == LichQueenId
                && card.Keywords != null && card.Keywords.Contains("exhaust")
                && HasTalent(state, "talent_lich_s2_lv5"))
            {
                cost = Math.Max(0, cost - 1);
            }

            cost = V091MechanicsRules.AdjustPlayCost(state, owner, card, cost);

            return cost;
        }

        public static void AfterDefenseBlockApplied(
            BattleState state,
            CombatantState actor,
            CombatantState beneficiary,
            int blockAmount,
            List<BattleEvent> events,
            BattleRng rng,
            CardInstanceState card)
        {
            if (state == null || actor == null || blockAmount <= 0)
                return;

            if (card == null || card.CardType != CardType.Defense)
                return;

            if (actor.CharacterDefinitionId != MageId || !HasTalent(state, "talent_mage_s1_lv1"))
                return;

            if (beneficiary == null || beneficiary.Id == actor.Id)
                return;

            var mirror = Math.Max(1, (int)Math.Round(blockAmount * 0.25f));
            DamageRules.ApplyBlock(actor, mirror, events, state, rng);
        }

        public static void AdjustPoisonStacks(BattleState state, CombatantState applier, ref int stacks)
        {
            if (applier?.CharacterDefinitionId != MageId)
                return;

            if (HasTalent(state, "talent_mage_s2_lv4"))
                stacks += 2;

            if (HasTalent(state, "talent_mage_s2_lv10"))
                stacks = Math.Max(0, stacks - 1);
        }

        public static int AdjustPoisonDuration(BattleState state, CombatantState applier, int duration)
        {
            if (applier?.CharacterDefinitionId != MageId)
                return duration;

            if (HasTalent(state, "talent_mage_s2_lv10"))
                return -1;

            return duration;
        }

        /// <summary>v0.9 巫妖女王：获得虚化时触发的天赋钩子（s1_lv1 回 3HP 等）。</summary>
        public static void OnEtherealGained(BattleState state, CombatantState target, List<BattleEvent> events)
        {
            if (state == null || target == null || events == null)
                return;

            // 巫妖女王 s1_lv1：获得虚化时回复 3HP
            if (target.CharacterDefinitionId == LichQueenId
                && HasTalent(state, "talent_lich_s1_lv1"))
            {
                DamageRules.ApplyHeal(state, target, 3, events, target);
            }

            // 巫妖女王 s1_lv4：虚化中受伤不掉血且回 3HP —— 由 DamageRules 的 ethereal 分支处理，此处不重复。
        }

        public static void OnMageDamageDealt(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            int hpDamage,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || target == null || hpDamage <= 0)
                return;

            if (actor.CharacterDefinitionId != MageId || !state.TalentMageFirstHitSlowPending)
                return;

            if (!HasTalent(state, "talent_mage_s2_lv6"))
                return;

            state.TalentMageFirstHitSlowPending = false;
            StatusRules.ApplyStatus(state, target, StatusCatalog.Slow, 1, 1, events);
        }

        static void TryKnightLastStand(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events,
            ref int hpDamage)
        {
            if (target.CharacterDefinitionId != KnightId
                || target.TalentLastStandBlockUsed
                || !HasTalent(state, "talent_knight_s1_lv10"))
                return;

            target.TalentLastStandBlockUsed = true;
            var blockGain = 50;
            DamageRules.ApplyBlock(target, blockGain, events, state);
            var absorbed = Math.Min(hpDamage, blockGain);
            hpDamage -= absorbed;
            if (absorbed > 0)
                target.Block -= absorbed;
        }

        static bool IsKnightAlive(BattleState state)
        {
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player
                    && c.CharacterDefinitionId == KnightId
                    && c.IsAlive)
                    return true;
            }

            return false;
        }

        public static bool HasTalent(BattleState state, string talentId) =>
            state?.Config?.Talents?.Has(talentId) == true;

        static List<CombatantState> CollectAlivePlayerTeam(BattleState state)
        {
            var list = new List<CombatantState>();
            if (state == null)
                return list;

            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player && c.IsAlive)
                    list.Add(c);
            }

            return list;
        }

        // ===== v0.9 毒蛇女王 / 巫妖女王 天赋 =====

        static CombatantState FindAlivePlayerCharacter(BattleState state, string characterId)
        {
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player && c.IsAlive && c.CharacterDefinitionId == characterId)
                    return c;
            }
            return null;
        }

        /// <summary>回合开始时触发的 v0.9 天赋（蛇 s1_lv4 / s2_lv4，巫妖 s1_lv7）。</summary>
        public static void ProcessTurnStartV09Talents(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            // 巫妖女王 s1_lv7：回合开始能量为0则+1
            if (state.EnergyCurrent == 0 && HasTalent(state, "talent_lich_s1_lv7"))
            {
                var lich = FindAlivePlayerCharacter(state, LichQueenId);
                if (lich != null)
                {
                    EnergyRules.Restore(state, 1);
                    events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "零点共鸣 +1 能量")
                    {
                        CombatantId = lich.Id,
                        Energy = state.EnergyCurrent,
                        EnergyMax = state.EnergyMax,
                        EnergyRemaining = state.EnergyCurrent,
                        Amount = 1
                    });
                }
            }

            foreach (var combatant in state.Combatants)
            {
                if (combatant == null || combatant.Team != TeamSide.Player || !combatant.IsAlive)
                    continue;

                if (combatant.CharacterDefinitionId == SnakeQueenId)
                {
                    var poisonStacks = StatusRules.GetStatusStacks(combatant, StatusCatalog.Poison);

                    // s1_lv4：每层中毒 +1% 强固（持续1回合，每回合刷新）
                    if (poisonStacks > 0 && HasTalent(state, "talent_snake_s1_lv4"))
                        StatusRules.ApplyStatus(state, combatant, StatusCatalog.DefenseUpPercent, poisonStacks, 1, events);

                    // s2_lv4：任意敌人中毒则 +1SPD（不叠加）
                    if (HasTalent(state, "talent_snake_s2_lv4")
                        && !StatusRules.HasStatus(combatant, StatusCatalog.SnakeSwiftness)
                        && AnyEnemyHasPoison(state))
                    {
                        StatusRules.ApplyStatus(state, combatant, StatusCatalog.SnakeSwiftness, 1, -1, events);
                    }
                }
            }
        }

        static bool AnyEnemyHasPoison(BattleState state)
        {
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Enemy && c.IsAlive && StatusRules.HasStatus(c, StatusCatalog.Poison))
                    return true;
            }
            return false;
        }

        /// <summary>中毒跳伤前钩子：返回处理后的伤害（可为0）。蛇 s1_lv1 免疫 / s1_lv10 转治疗 / s1_lv6 敌人中_dt毒时自身回1。</summary>
        public static void OnPoisonTick(BattleState state, CombatantState combatant, ref int damage, List<BattleEvent> events)
        {
            if (state == null || combatant == null || damage <= 0)
                return;

            if (combatant.CharacterDefinitionId == SnakeQueenId)
            {
                if (HasTalent(state, "talent_snake_s1_lv1"))
                {
                    damage = 0;
                    return;
                }
                if (HasTalent(state, "talent_snake_s1_lv10"))
                {
                    var heal = damage;
                    damage = 0;
                    DamageRules.ApplyHeal(state, combatant, heal, events, combatant);
                    return;
                }
            }

            if (combatant.Team == TeamSide.Enemy && HasTalent(state, "talent_snake_s1_lv6"))
            {
                var snake = FindAlivePlayerCharacter(state, SnakeQueenId);
                if (snake != null)
                    DamageRules.ApplyHeal(state, snake, 1, events, snake);
            }
        }

        /// <summary>蛇 s2_lv2：单次受到超过 25% 最大HP 的伤害后清除自身所有负面状态。</summary>
        public static void OnDamageTakenV09(BattleState state, CombatantState recipient, int hpDamage, List<BattleEvent> events)
        {
            if (state == null || recipient == null || hpDamage <= 0)
                return;
            if (recipient.CharacterDefinitionId != SnakeQueenId || !HasTalent(state, "talent_snake_s2_lv2"))
                return;

            var threshold = Math.Max(1, recipient.MaxHp / 4);
            if (hpDamage < threshold)
                return;

            ClearAllDebuffs(state, recipient, events);
        }

        static void ClearAllDebuffs(BattleState state, CombatantState target, List<BattleEvent> events)
        {
            for (var i = target.Statuses.Count - 1; i >= 0; i--)
            {
                var s = target.Statuses[i];
                var def = StatusCatalog.Get(s.StatusId);
                if (def == null)
                    continue;
                if (def.TurnStartDamagePerStack > 0
                    || def.TurnEndDamagePerStack > 0
                    || def.SpeedModifierPerStack < 0
                    || def.OutgoingDamageReductionFlatPerStack > 0
                    || def.BlockGainReductionPercentPerStack > 0
                    || def.IncomingDamagePercentPerStack > 0)
                {
                    target.Statuses.RemoveAt(i);
                    events.Add(new BattleEvent(BattleEventKind.StatusRemoved, def.DisplayName)
                    {
                        CombatantId = target.Id,
                        TargetId = s.StatusId
                    });
                }
            }
            CombatantRules.RefreshDerivedStats(target);
        }

        /// <summary>蛇 s2_lv10：中毒持续时间结束时层数减半而非清零。返回 true 表示已接管处理。</summary>
        public static bool TryHandlePoisonExpiry(CombatantState combatant, StatusInstance status)
        {
            if (combatant == null || status == null || status.StatusId != StatusCatalog.Poison)
                return false;
            // 该天赋为 run 级；此处通过 combatant 所属角色与全局天赋上下文判断需 state，
            // 但钩子调用方持有 state，改用带 state 重载。
            return false;
        }

        public static bool TryHandlePoisonExpiry(BattleState state, CombatantState combatant, StatusInstance status, List<BattleEvent> events)
        {
            if (state == null || combatant == null || status == null || status.StatusId != StatusCatalog.Poison)
                return false;
            if (combatant.CharacterDefinitionId != SnakeQueenId || !HasTalent(state, "talent_snake_s2_lv10"))
                return false;
            if (status.Stacks <= 1)
                return false;

            status.Stacks = Math.Max(1, status.Stacks / 2);
            // 续 1 回合而非移除
            status.RemainingTurns = Math.Max(status.RemainingTurns, 1);
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "慢性毒素：中毒层数减半续存")
            {
                CombatantId = combatant.Id,
                Amount = status.Stacks,
                TargetId = StatusCatalog.Poison
            });
            return true;
        }

        /// <summary>巫妖 s1_lv4：虚化中受伤改为0并回3HP。返回 true 表示已接管 ethereal 分支。</summary>
        public static bool TryHandleEtherealDamage(BattleState state, CombatantState recipient, ref int hpDamage, List<BattleEvent> events)
        {
            if (state == null || recipient == null || hpDamage <= 0)
                return false;
            if (recipient.CharacterDefinitionId != LichQueenId || !HasTalent(state, "talent_lich_s1_lv4"))
                return false;
            if (!StatusRules.HasStatus(recipient, StatusCatalog.Ethereal))
                return false;

            hpDamage = 0;
            DamageRules.ApplyHeal(state, recipient, 3, events, recipient);
            return true;
        }
    }
}
