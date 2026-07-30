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
        public const int AssaultStancePercent = 33;

        public static void OnBattleInitialized(BattleState state)
        {
            if (state?.Config?.Talents == null)
                return;

            state.TalentMageFirstStatusDiscountPending = HasTalent(state, "talent_mage_s2_lv2");
            state.TalentMageFirstStatusDiscountReservedInstanceId = 0;
            state.TalentMageFirstHitSlowPending = HasTalent(state, "talent_mage_s2_lv6");
            state.TalentMageReviveAvailable = state.Config.Talents.MageReviveAvailable;
            state.TalentRangerSacrificeHpBaseline = state.Config.Talents.RangerSacrificeHpTotalAtBattleStart;
            state.TalentRangerBloodDebtAttackBonus = state.Config.Talents.RangerBloodDebtAttackBonus;
            state.TalentRangerPendingRandomCostDiscountNextTurn = false;
            state.TalentRangerDiscountedCardInstanceId = 0;
            state.TalentLichFirstExhaustDiscountPending = HasTalent(state, "talent_lich_s2_lv5");
            state.TalentLichFirstExhaustDiscountReservedInstanceId = 0;

            foreach (var combatant in state.Combatants)
            {
                if (combatant.Team != TeamSide.Player)
                    continue;

                if (combatant.CharacterDefinitionId == KnightId && HasTalent(state, "talent_knight_s2_lv10"))
                    combatant.TalentDisableBlockGain = true;
            }

            // 血债累击等开战快照需在赋值后再刷修饰符/脚标
            RelicBattleRules.RefreshAllDerivedStats(state);
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

                if (combatant.TalentRespondNextTurnBlock > 0
                    && combatant.CharacterDefinitionId == KnightId
                    && HasTalent(state, "talent_knight_s2_lv2"))
                {
                    var block = combatant.TalentRespondNextTurnBlock;
                    combatant.TalentRespondNextTurnBlock = 0;
                    DamageRules.ApplyBlock(combatant, block, events, state);
                }
            }
        }

        public static void ProcessEndOfTurn(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            // 巫妖 s1_lv9：本回合全为巫妖牌 → 下回合全体敌人 10 伤
            if (HasTalent(state, "talent_lich_s1_lv9")
                && state.TalentLichCardsPlayedThisTurn > 0
                && state.TalentLichAllCardsLichOwnedThisTurn)
            {
                state.TalentLichPendingEnemyAoeNextTurn = 10;
            }
        }

        /// <summary>
        /// 回合末清甲之前结算：养精蓄锐等依赖「本回合结束时仍有护甲」的天赋。
        /// </summary>
        public static void ProcessEndOfTurnBeforeBlockClear(BattleState state, List<BattleEvent> events)
        {
            if (state == null)
                return;

            if (!HasTalent(state, "talent_knight_s1_lv5"))
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

            if (combatant.CharacterDefinitionId == KnightId)
            {
                // 突击姿态 / 背水一战 / 连击：由状态挂载结算
            }

            if (combatant.CharacterDefinitionId == RangerId)
            {
                // 低血狂怒 / 孤猎 / 血债累击：由 SyncConditionalTalentStatuses 挂不可净化状态
            }

            if (combatant.SacrificeAttackStacks > 0)
                combatant.OutgoingDamagePercentBonus += combatant.SacrificeAttackStacks;
        }

        /// <summary>
        /// 条件天赋增伤/易伤：同步为不可净化状态（供脚标与伤害结算）。
        /// 在 RefreshCombatantModifiers 读取状态之前调用，避免与 ApplyStatus 递归刷新。
        /// </summary>
        public static void SyncConditionalTalentStatuses(BattleState state, CombatantState combatant)
        {
            SyncAssaultStanceStatuses(state, combatant);
            SyncBackToWallStatus(state, combatant);
            SyncLowHpFuryStatus(state, combatant);
            SyncSoloHuntStatus(state, combatant);
            SyncBloodDebtStatus(state, combatant);
            SyncSoulFireThrottleStatus(state, combatant);
        }

        /// <summary>
        /// 魂火节流：Pending 时挂不可净化隐藏状态；点选占用后移除；取消选择后恢复。
        /// </summary>
        public static void SyncSoulFireThrottleStatus(BattleState state)
        {
            if (state?.Combatants == null)
                return;

            foreach (var combatant in state.Combatants)
                SyncSoulFireThrottleStatus(state, combatant);
        }

        static void SyncSoulFireThrottleStatus(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            var active = combatant.IsAlive
                && combatant.Team == TeamSide.Player
                && combatant.CharacterDefinitionId == LichQueenId
                && HasTalent(state, "talent_lich_s2_lv5")
                && state.TalentLichFirstExhaustDiscountPending;

            SyncTalentStatusStacks(combatant, StatusCatalog.LichSoulFireThrottle, active ? 1 : 0);
        }

        /// <summary>
        /// 突击姿态：非前排时同步 33% 增伤 / 33% 易伤为不可净化状态（供脚标与伤害结算）。
        /// </summary>
        public static void SyncAssaultStanceStatuses(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            var active = combatant.IsAlive
                && combatant.Team == TeamSide.Player
                && combatant.CharacterDefinitionId == KnightId
                && HasTalent(state, "talent_knight_s1_lv3")
                && PositionRules.GetEffectiveSlot(state, combatant) != FormationSlot.Front;

            var desired = active ? AssaultStancePercent : 0;
            SyncTalentStatusStacks(combatant, StatusCatalog.KnightAssaultStanceAtk, desired);
            SyncTalentStatusStacks(combatant, StatusCatalog.KnightAssaultStanceVuln, desired);
        }

        static void SyncBackToWallStatus(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            var active = combatant.IsAlive
                && combatant.Team == TeamSide.Player
                && combatant.CharacterDefinitionId == KnightId
                && HasTalent(state, "talent_knight_s1_lv7")
                && combatant.Hp < combatant.Block;

            SyncTalentStatusStacks(combatant, StatusCatalog.KnightBackToWallAtk, active ? 20 : 0);
        }

        static void SyncLowHpFuryStatus(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            var active = combatant.IsAlive
                && combatant.Team == TeamSide.Player
                && combatant.CharacterDefinitionId == RangerId
                && HasTalent(state, "talent_ranger_s1_lv7")
                && combatant.Hp * 100 / Math.Max(1, combatant.MaxHp) < 30;

            SyncTalentStatusStacks(combatant, StatusCatalog.RangerLowHpFuryAtk, active ? 25 : 0);
        }

        static void SyncSoloHuntStatus(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            var active = combatant.IsAlive
                && combatant.Team == TeamSide.Player
                && combatant.CharacterDefinitionId == RangerId
                && HasTalent(state, "talent_ranger_s2_lv8")
                && IsNonBossSoloEnemyActive(state);

            SyncTalentStatusStacks(combatant, StatusCatalog.RangerSoloHuntAtk, active ? 30 : 0);
        }

        static void SyncBloodDebtStatus(BattleState state, CombatantState combatant)
        {
            if (state == null || combatant == null)
                return;

            var percent = state.TalentRangerBloodDebtAttackBonus;
            var active = combatant.IsAlive
                && combatant.Team == TeamSide.Player
                && combatant.CharacterDefinitionId == RangerId
                && HasTalent(state, "talent_ranger_s1_lv10")
                && percent > 0;

            SyncTalentStatusStacks(combatant, StatusCatalog.RangerBloodDebtAtk, active ? percent : 0);
        }

        static void SyncTalentStatusStacks(CombatantState combatant, string statusId, int desiredStacks)
        {
            var current = StatusRules.GetStatusStacks(combatant, statusId);
            if (desiredStacks <= 0)
            {
                if (current <= 0)
                    return;

                for (var i = combatant.Statuses.Count - 1; i >= 0; i--)
                {
                    if (combatant.Statuses[i]?.StatusId == statusId)
                        combatant.Statuses.RemoveAt(i);
                }

                return;
            }

            if (current == desiredStacks)
                return;

            StatusInstance existing = null;
            foreach (var status in combatant.Statuses)
            {
                if (status?.StatusId == statusId)
                {
                    existing = status;
                    break;
                }
            }

            if (existing == null)
            {
                combatant.Statuses.Add(new StatusInstance
                {
                    StatusId = statusId,
                    Stacks = desiredStacks,
                    RemainingTurns = -1
                });
                return;
            }

            existing.Stacks = desiredStacks;
            existing.RemainingTurns = -1;
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

            // 铁壁转化改为 flat bonus（GetOutgoingDamageFlatBonus）
            return 1f;
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
            List<BattleEvent> events,
            ref int extraBlocked)
        {
            if (target == null || hpDamage <= 0)
                return hpDamage;

            if (hpDamage > 0 && target.Hp - hpDamage <= 0)
                TryKnightLastStand(state, target, events, ref hpDamage, ref extraBlocked);

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
            if (state == null || combatant == null)
                return;

            // 孤猎：敌方减员后动态刷新恶魔增伤
            if (combatant.Team == TeamSide.Enemy && HasTalent(state, "talent_ranger_s2_lv8"))
            {
                foreach (var ally in CollectAlivePlayerTeam(state))
                {
                    if (ally.CharacterDefinitionId == RangerId)
                        RelicBattleRules.RefreshDerivedStats(state, ally, state.Config?.RunModifiers);
                }
            }

            if (combatant.Team != TeamSide.Player)
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

        public static void OnRespondSuccess(
            BattleState state,
            CombatantState actor,
            RespondTriggerContext context,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || actor.CharacterDefinitionId != KnightId)
                return;

            var enemyCard = state.GetCard(context.EnemyCardInstanceId);
            var respondedToAttack = enemyCard != null && enemyCard.CardType == CardType.Attack;

            // 应对护甲：仅「应对攻击」成功后，下回合开始 +5 护甲
            if (respondedToAttack && HasTalent(state, "talent_knight_s2_lv2"))
                actor.TalentRespondNextTurnBlock += 5;

            // 应对增伤：仅「应对攻击」成功后，20% 增伤（2 回合）
            if (respondedToAttack && HasTalent(state, "talent_knight_s2_lv3") && events != null)
                StatusRules.ApplyStatus(state, actor, StatusCatalog.AttackUpPercent, 20, 2, events);
        }

        /// <summary>
        /// 选牌提交后、结算开始前：若本回合计划中含 ≥3 张战士攻击牌（不含快速启动），
        /// 立刻挂 33% 连击增伤，使本回合所有攻击（含第 1、2 张）都能吃到加成。
        /// </summary>
        public static void TryApplyComboFromCommittedPlan(BattleState state, List<BattleEvent> events)
        {
            if (state == null || !HasTalent(state, "talent_knight_s2_lv8"))
                return;

            CombatantState knight = null;
            foreach (var c in state.Combatants)
            {
                if (c.Team == TeamSide.Player
                    && c.CharacterDefinitionId == KnightId
                    && c.IsAlive)
                {
                    knight = c;
                    break;
                }
            }

            if (knight == null)
                return;

            if (StatusRules.GetStatusStacks(knight, StatusCatalog.KnightComboAtk) > 0)
                return;

            var attackCount = 0;
            foreach (var cardId in state.PlayerPlan.PlayQueue)
            {
                var card = state.GetCard(cardId);
                if (card == null || card.CardType != CardType.Attack)
                    continue;
                if (card.Keywords != null && card.Keywords.Contains("quick_start"))
                    continue;

                var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                if (ownerId != knight.Id)
                    continue;

                attackCount++;
            }

            if (attackCount < 3)
                return;

            var list = events ?? new List<BattleEvent>();
            StatusRules.ApplyStatus(state, knight, StatusCatalog.KnightComboAtk, 33, 1, list);
            RelicBattleRules.RefreshDerivedStats(state, knight, state.Config?.RunModifiers);
        }

        /// <summary>
        /// 出牌效果结算前调用：保留连击计数（展示/调试）；增伤已在计划提交时挂上。
        /// </summary>
        public static void OnCardAboutToResolve(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events = null)
        {
            if (state == null || actor == null || card == null)
                return;

            if (actor.CharacterDefinitionId != KnightId || !HasTalent(state, "talent_knight_s2_lv8"))
                return;

            // 快速启动不计入连击，也不打断连击
            if (card.Keywords != null && card.Keywords.Contains("quick_start"))
                return;

            if (IsWarriorAttackCard(actor, card))
                actor.TalentAttackCardsThisTurn++;
        }

        public static void OnCardResolved(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || card == null)
                return;

            if (actor.Team != TeamSide.Player || !HasTalent(state, "talent_lich_s1_lv9"))
                return;

            state.TalentLichCardsPlayedThisTurn++;
            if (!IsLichOwnedCard(actor, card))
                state.TalentLichAllCardsLichOwnedThisTurn = false;
        }

        static bool IsLichOwnedCard(CombatantState actor, CardInstanceState card)
        {
            if (!string.IsNullOrEmpty(card.OwnerCharacterId))
                return card.OwnerCharacterId == LichQueenId;

            return actor != null && actor.CharacterDefinitionId == LichQueenId;
        }

        static bool IsWarriorAttackCard(CombatantState actor, CardInstanceState card)
        {
            if (card.CardType != CardType.Attack)
                return false;

            if (actor.CharacterDefinitionId != KnightId)
                return false;

            // 无 Owner 时视为该角色自有牌；有 Owner 则必须是战士牌。
            if (!string.IsNullOrEmpty(card.OwnerCharacterId)
                && card.OwnerCharacterId != KnightId)
                return false;

            return true;
        }

        public static void OnSacrificeHpSpent(
            BattleState state,
            CombatantState actor,
            int hpSpent,
            List<BattleEvent> events = null)
        {
            if (state == null || actor == null || hpSpent <= 0)
                return;

            if (actor.CharacterDefinitionId != RangerId)
                return;

            state.TalentSacrificeHpAccumulatedBattle += hpSpent;
            RefreshBloodDebtAttackBonus(state, actor, events);
        }

        /// <summary>献祭后即时重算血债累击增伤（训练场/远征同场生效）。</summary>
        static void RefreshBloodDebtAttackBonus(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events)
        {
            if (!HasTalent(state, "talent_ranger_s1_lv10"))
                return;

            var total = state.TalentRangerSacrificeHpBaseline + state.TalentSacrificeHpAccumulatedBattle;
            var bonus = 0;
            if (total >= 50)
                bonus = Math.Min(20, (total / 50) * 2);

            if (bonus == state.TalentRangerBloodDebtAttackBonus)
                return;

            var previous = state.TalentRangerBloodDebtAttackBonus;
            state.TalentRangerBloodDebtAttackBonus = bonus;
            RelicBattleRules.RefreshDerivedStats(state, actor, state.Config?.RunModifiers);

            if (events == null)
                return;

            if (bonus > 0)
            {
                events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} 血债累击 +{bonus}%")
                {
                    CombatantId = actor.Id,
                    TargetId = StatusCatalog.RangerBloodDebtAtk,
                    Amount = bonus
                });
            }
            else if (previous > 0)
            {
                events.Add(new BattleEvent(BattleEventKind.StatusRemoved, $"{actor.DisplayName} 血债累击结束")
                {
                    CombatantId = actor.Id,
                    TargetId = StatusCatalog.RangerBloodDebtAtk,
                    Amount = previous
                });
            }
        }

        /// <summary>打出献祭牌即触发（即便微献保护等使实际扣血为 0）。</summary>
        public static void OnSacrificeCardPlayed(BattleState state, CombatantState actor, CardInstanceState card)
        {
            if (state == null || actor == null || card == null)
                return;
            if (actor.CharacterDefinitionId != RangerId)
                return;
            if (card.Keywords == null || !card.Keywords.Contains("sacrifice"))
                return;

            if (HasTalent(state, "talent_ranger_s2_lv4"))
                state.TalentRangerPendingRandomCostDiscountNextTurn = true;
        }

        /// <summary>抽牌完成后：血祭节流为手牌随机挂 -1 费。</summary>
        public static void ProcessAfterHandDrawn(BattleState state, BattleRng rng, List<BattleEvent> events)
        {
            if (state == null)
                return;

            state.TalentRangerDiscountedCardInstanceId = 0;

            if (!state.TalentRangerPendingRandomCostDiscountNextTurn
                || !HasTalent(state, "talent_ranger_s2_lv4"))
            {
                state.TalentRangerPendingRandomCostDiscountNextTurn = false;
                return;
            }

            state.TalentRangerPendingRandomCostDiscountNextTurn = false;
            if (rng == null || state.PlayerHand.Count <= 0)
                return;

            var pick = state.PlayerHand[rng.NextIndex(state.PlayerHand.Count)];
            if (pick == null)
                return;

            state.TalentRangerDiscountedCardInstanceId = pick.InstanceId;
            events?.Add(new BattleEvent(BattleEventKind.StatusApplied, $"血祭节流：{pick.DisplayName} 费用-1")
            {
                CardInstanceId = pick.InstanceId,
                Amount = 1
            });
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

            if ((HasTalent(state, "talent_ranger_s1_lv3") || HasTalent(state, "talent_ranger_s2_lv3"))
                && damage <= 5)
                return 0;

            return Math.Max(1, damage);
        }

        public static int GetEffectivePlayCost(
            BattleState state,
            CombatantState owner,
            CardInstanceState card)
        {
            if (state == null || card == null)
                return card?.Cost ?? 0;

            var cost = CardPowerRules.UsesRemainingEnergyCost(card)
                ? CardPowerRules.GetRemainingEnergyPlayCost(state, card)
                : card.Cost;

            if (state.TalentRangerDiscountedCardInstanceId == card.InstanceId
                && HasTalent(state, "talent_ranger_s2_lv4"))
            {
                cost = Math.Max(0, cost - 1);
            }

            if (card.CardType == CardType.Status
                && HasTalent(state, "talent_mage_s2_lv2")
                && (state.TalentMageFirstStatusDiscountPending
                    || state.TalentMageFirstStatusDiscountReservedInstanceId == card.InstanceId))
            {
                cost = Math.Max(0, cost - 1);
            }

            // 魂火节流：Pending 时全巫妖牌 -1；点选后仅 Reserved 那张保持 -1（取消选择可恢复 Pending）
            if (IsLichSoulFireDiscountCard(owner, card)
                && HasTalent(state, "talent_lich_s2_lv5")
                && (state.TalentLichFirstExhaustDiscountPending
                    || state.TalentLichFirstExhaustDiscountReservedInstanceId == card.InstanceId))
            {
                cost = Math.Max(0, cost - 1);
            }

            cost = V091MechanicsRules.AdjustPlayCost(state, owner, card, cost);

            return cost;
        }

        static bool IsLichSoulFireDiscountCard(CombatantState owner, CardInstanceState card)
        {
            if (card == null)
                return false;

            if (owner != null && owner.CharacterDefinitionId == LichQueenId)
                return true;

            return card.OwnerCharacterId == LichQueenId;
        }

        public static bool IsLichSoulFireDiscountEligible(
            BattleState state,
            CombatantState owner,
            CardInstanceState card) =>
            state != null
            && IsLichSoulFireDiscountCard(owner, card)
            && HasTalent(state, "talent_lich_s2_lv5");

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

            // 烈日焚心：中毒会转灼烧，不再做层数-1
        }

        public static int AdjustPoisonDuration(BattleState state, CombatantState applier, int duration)
        {
            if (applier?.CharacterDefinitionId != MageId)
                return duration;

            // 烈日焚心：中毒转灼烧，沿用原持续（或卡面默认）
            return duration;
        }

        /// <summary>烈日焚心：法老施加的中毒改为灼烧。</summary>
        public static string ResolveAppliedStatusId(
            BattleState state,
            CombatantState applier,
            string statusId)
        {
            if (statusId != StatusCatalog.Poison)
                return statusId;
            if (applier?.CharacterDefinitionId != MageId)
                return statusId;
            if (!HasTalent(state, "talent_mage_s2_lv10"))
                return statusId;
            return StatusCatalog.Burn;
        }

        /// <summary>烈日焚心：敌人被灼烧掉血时，随机友方回复 2 HP。</summary>
        public static void OnBurnTickHpDamage(
            BattleState state,
            CombatantState burned,
            int damage,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || burned == null || damage <= 0 || events == null)
                return;
            if (burned.Team != TeamSide.Enemy)
                return;
            if (!HasTalent(state, "talent_mage_s2_lv10"))
                return;

            var allies = CollectAlivePlayerTeam(state);
            if (allies.Count <= 0)
                return;

            var pick = rng != null
                ? allies[rng.NextIndex(allies.Count)]
                : allies[0];
            DamageRules.ApplyHeal(state, pick, 2, events, pick);
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

            // 巫妖女王 s1_lv4：虚化中受伤不掉血且回 2HP —— 由 DamageRules 的 ethereal 分支处理，此处不重复。
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
            StatusRules.ApplyStatus(state, target, StatusCatalog.Slow, 1, -1, events);
        }

        static void TryKnightLastStand(
            BattleState state,
            CombatantState target,
            List<BattleEvent> events,
            ref int hpDamage,
            ref int extraBlocked)
        {
            if (target.CharacterDefinitionId != KnightId
                || target.TalentLastStandBlockUsed
                || !HasTalent(state, "talent_knight_s1_lv10"))
                return;

            target.TalentLastStandBlockUsed = true;
            var blockGain = 50;
            var beforeEvents = events?.Count ?? 0;
            DamageRules.ApplyBlock(target, blockGain, events, state);
            if (events != null)
            {
                for (var i = beforeEvents; i < events.Count; i++)
                {
                    if (events[i].Kind == BattleEventKind.BlockGained
                        && events[i].CombatantId == target.Id)
                        events[i].IsRespondStyleBlock = true;
                }
            }

            var absorbed = Math.Min(hpDamage, blockGain);
            hpDamage -= absorbed;
            if (absorbed > 0)
            {
                target.Block -= absorbed;
                // 计入 DamageApplied.BlockedAmount，演出才能先显示护甲再正确消耗，避免残甲常驻
                extraBlocked += absorbed;
            }
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

        static CombatantState FindAlivePlayerCharacterAny(BattleState state)
        {
            foreach (var c in state.Combatants)
            {
                if (c != null && c.Team == TeamSide.Player && c.IsAlive)
                    return c;
            }
            return null;
        }

        /// <summary>回合开始时触发的 v0.9 天赋（蛇 / 巫妖）。</summary>
        /// <param name="isFirstPlayerTurn">须在 ApplyTurnStartRegen 之前读取：首回合回满不算「剩余 0」。</param>
        public static void ProcessTurnStartV09Talents(
            BattleState state,
            List<BattleEvent> events,
            int energyBeforeRegen = -1,
            bool isFirstPlayerTurn = false)
        {
            if (state == null)
                return;

            // 封印武装：上回合夺取的牌于本回合开始直接入手
            DeliverPendingSealedCards(state, events);

            // 巫妖 s1_lv9：上回合齐奏 → 本回合开始全体敌人 10 伤
            if (state.TalentLichPendingEnemyAoeNextTurn > 0 && HasTalent(state, "talent_lich_s1_lv9"))
            {
                var amount = state.TalentLichPendingEnemyAoeNextTurn;
                state.TalentLichPendingEnemyAoeNextTurn = 0;
                var source = FindAlivePlayerCharacter(state, LichQueenId)
                    ?? FindAlivePlayerCharacterAny(state);
                if (source != null)
                {
                    foreach (var enemy in state.Combatants)
                    {
                        if (enemy == null || enemy.Team != TeamSide.Enemy || !enemy.IsAlive)
                            continue;

                        DamageRules.ApplyDamage(
                            state,
                            source,
                            enemy,
                            amount,
                            CardType.Status,
                            events);
                    }
                }
            }

            state.TalentLichCardsPlayedThisTurn = 0;
            state.TalentLichAllCardsLichOwnedThisTurn = true;

            // 巫妖 s2_lv8：每 4 回合获得虚化 1 回合
            if (HasTalent(state, "talent_lich_s2_lv8")
                && state.TurnNumber > 0
                && state.TurnNumber % 4 == 0)
            {
                var lich = FindAlivePlayerCharacter(state, LichQueenId);
                if (lich != null)
                    StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, events);
            }

            // 巫妖女王 s1_lv7：常规能量回复前若能量为 0，则额外 +1（首回合开战回满不算）
            var checkEnergy = energyBeforeRegen >= 0 ? energyBeforeRegen : state.EnergyCurrent;
            if (!isFirstPlayerTurn
                && checkEnergy == 0
                && HasTalent(state, "talent_lich_s1_lv7"))
            {
                var lich = FindAlivePlayerCharacter(state, LichQueenId);
                if (lich != null)
                {
                    EnergyRules.GainTemporary(state, 1);
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

        static bool IsNonBossSoloEnemyActive(BattleState state)
        {
            if (state?.Config?.Talents?.IsBossBattle == true)
                return false;

            return CountAliveEnemies(state) == 1;
        }

        static int CountAliveEnemies(BattleState state)
        {
            var count = 0;
            if (state == null)
                return 0;

            foreach (var c in state.Combatants)
            {
                if (c != null && c.Team == TeamSide.Enemy && c.IsAlive)
                    count++;
            }

            return count;
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
                }
            }
        }

        /// <summary>
        /// 毒息汲取：敌人已因中毒掉血后触发回血（演出顺序：先掉血再汲取）。
        /// </summary>
        public static void AfterEnemyPoisonTickDamage(
            BattleState state,
            CombatantState combatant,
            List<BattleEvent> events)
        {
            if (state == null || combatant == null || combatant.Team != TeamSide.Enemy)
                return;
            if (!HasTalent(state, "talent_snake_s1_lv6"))
                return;

            var snake = FindAlivePlayerCharacter(state, SnakeQueenId);
            if (snake != null)
                DamageRules.ApplyHeal(state, snake, 1, events, snake);
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
                if (s == null || StatusRules.IsUnclearedBuff(s.StatusId))
                    continue;
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
            if (!HasTalent(state, "talent_snake_s2_lv10"))
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

        /// <summary>巫妖 s1_lv4：虚化中受伤改为0并回2HP。返回 true 表示已接管 ethereal 分支。</summary>
        public static bool TryHandleEtherealDamage(BattleState state, CombatantState recipient, ref int hpDamage, List<BattleEvent> events)
        {
            if (state == null || recipient == null || hpDamage <= 0)
                return false;
            if (recipient.CharacterDefinitionId != LichQueenId || !HasTalent(state, "talent_lich_s1_lv4"))
                return false;
            if (!StatusRules.HasStatus(recipient, StatusCatalog.Ethereal))
                return false;

            hpDamage = 0;
            DamageRules.ApplyHeal(state, recipient, 2, events, recipient);
            return true;
        }

        /// <summary>巫妖 s2_lv10：封印成功时将被封印卡克隆，立即临时入手（费用+1；未打出则回合结束消除）。</summary>
        public static void OnEnemyCardSealed(
            BattleState state,
            CardInstanceState sealedCard,
            List<BattleEvent> events)
        {
            if (state == null || sealedCard == null)
                return;
            if (!HasTalent(state, "talent_lich_s2_lv10"))
                return;

            var lich = FindAlivePlayerCharacter(state, LichQueenId);
            var clone = CloneSealedCardForPlayer(state, sealedCard, lich);
            if (clone == null)
                return;

            // 立即入手：封印发生在结算中，当回合末不清除（BonusHandGrantedTurn），下回合可打出
            if (!state.PlayerHand.Contains(clone))
                state.PlayerHand.Add(clone);

            events?.Add(new BattleEvent(BattleEventKind.CardDrawn,
                $"{clone.DisplayName} 已被封印武装夺取，临时加入手牌（费用+1）")
            {
                CardInstanceId = clone.InstanceId,
                CombatantId = lich?.Id
            });
        }

        /// <summary>
        /// 完整克隆被封印卡：保留 DefinitionId / 全部 Actions / 原角色定义 Id（效果逻辑依赖），
        /// 归属战斗单位绑到巫妖以便玩家打出；费用+1；临时手牌，无消耗词条。
        /// </summary>
        static CardInstanceState CloneSealedCardForPlayer(
            BattleState state,
            CardInstanceState sealedCard,
            CombatantState lich)
        {
            if (state == null || sealedCard == null)
                return null;

            var clone = new CardInstanceState
            {
                InstanceId = state.NextCardInstanceId++,
                DefinitionId = sealedCard.DefinitionId,
                // 保留原怪物角色 Id，避免丢失 Definition 相关特殊效果；打出归属靠 OwnerCombatantId
                OwnerCharacterId = sealedCard.OwnerCharacterId,
                OwnerCombatantId = lich != null ? lich.Id : "",
                Cost = Math.Max(0, sealedCard.Cost + 1),
                BaseCost = Math.Max(
                    0,
                    (sealedCard.BaseCost != 0 || sealedCard.Cost == 0
                        ? sealedCard.BaseCost
                        : sealedCard.Cost) + 1),
                CardType = sealedCard.CardType,
                IsUsable = true,
                IsBonusHandCard = true,
                BonusHandGrantedTurn = state.TurnNumber,
                DisplayName = sealedCard.DisplayName,
                UpgradeLevel = sealedCard.UpgradeLevel
            };

            foreach (var keyword in sealedCard.Keywords)
            {
                if (string.IsNullOrEmpty(keyword) || keyword == "exhaust")
                    continue;
                if (!clone.Keywords.Contains(keyword))
                    clone.Keywords.Add(keyword);
            }

            foreach (var action in sealedCard.Actions)
            {
                if (action == null)
                    continue;
                clone.Actions.Add(EffectActionSpec.Clone(action));
            }

            state.CardsById[clone.InstanceId] = clone;
            return clone;
        }

        /// <summary>兼容旧存档/测试：若仍有待交付列表则于回合开始入手。</summary>
        public static void DeliverPendingSealedCards(BattleState state, List<BattleEvent> events)
        {
            if (state?.TalentLichSealedCardsPendingNextTurn == null
                || state.TalentLichSealedCardsPendingNextTurn.Count == 0)
                return;

            var lich = FindAlivePlayerCharacter(state, LichQueenId);
            foreach (var card in state.TalentLichSealedCardsPendingNextTurn)
            {
                if (card == null)
                    continue;

                if (!state.CardsById.ContainsKey(card.InstanceId))
                    state.CardsById[card.InstanceId] = card;

                card.IsBonusHandCard = true;
                if (card.BonusHandGrantedTurn <= 0)
                    card.BonusHandGrantedTurn = state.TurnNumber;
                card.Keywords.Remove("exhaust");

                if (lich != null)
                    card.OwnerCombatantId = lich.Id;

                if (!state.PlayerHand.Contains(card))
                    state.PlayerHand.Add(card);

                events?.Add(new BattleEvent(BattleEventKind.CardDrawn, $"{card.DisplayName}（封印武装）")
                {
                    CardInstanceId = card.InstanceId,
                    CombatantId = lich?.Id
                });
            }

            state.TalentLichSealedCardsPendingNextTurn.Clear();
        }
    }
}
