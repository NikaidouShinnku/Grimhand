using System;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Core;

namespace Grimhand.Battle.Effects
{
    public static class EffectActionExecutor
    {
        public static void ExecuteAll(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            ExecuteConditionalActions(state, actor, card, events, rng);
            ExecuteUnconditionalActions(state, actor, card, events, rng);
        }

        public static void ExecuteUnconditionalActions(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.None)
                    continue;

                // 终焉守护护甲由 ApplyFinalGuardBlock 统一发放，避免重复
                if (card.DefinitionId == PassiveCardMechanicsRules.FinalGuardCardId
                    && action.Type == EffectActionType.GainBlock)
                    continue;

                ExecuteOne(state, actor, card, action, events, rng, sourceCardInstanceId: card.InstanceId);
            }

            TryGrantBloodScratchNextAttack(state, actor, card, events);
        }

        static void TryGrantBloodScratchNextAttack(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events)
        {
            if (card?.DefinitionId != "g_blood_scratch" || actor == null)
                return;

            actor.NextAttackFlatBonus = 3;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{actor.DisplayName} 下次攻击+3")
            {
                CombatantId = actor.Id,
                Amount = 3,
                TargetId = StatusCatalog.AttackUp
            });
        }

        public static void ExecuteFailedRespondActions(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            foreach (var action in card.Actions)
            {
                if (action.Condition != ReactionConditionType.RespondArmFailed)
                    continue;

                ExecuteOne(state, actor, card, action, events, rng, sourceCardInstanceId: card.InstanceId);
            }
        }

        static void ExecuteConditionalActions(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (RespondRules.IsRespondCard(card))
                return;

            var triggeredReaction = false;
            foreach (var action in card.Actions)
            {
                if (action.Condition == ReactionConditionType.None)
                    continue;

                if (!ReactionRules.MeetsCondition(state, action.Condition, actor.Id))
                    continue;

                triggeredReaction = true;
                ExecuteOne(state, actor, card, action, events, rng, sourceCardInstanceId: card.InstanceId);
            }

            if (triggeredReaction)
            {
                events.Add(new BattleEvent(BattleEventKind.ReactionTriggered, card.DisplayName)
                {
                    CombatantId = actor.Id
                });
            }
        }

        public static void ExecuteOne(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId,
            CombatantState targetOverride = null)
        {
            var target = targetOverride
                         ?? TargetRules.ResolveTarget(state, actor, action.Target, card.InstanceId, rng, action);
            var value = action.Type == EffectActionType.DealDamage
                ? CombatMechanicsRules.ComputeActionValueForTarget(state, action, actor, target)
                : CardPowerRules.ComputeActionValue(action, actor);
            if (action.Type == EffectActionType.DealDamage
                && action.Target == EffectTarget.Self
                && card.Keywords.Contains("sacrifice"))
            {
                value = RelicEffectRules.AdjustSacrificeSelfDamage(
                    state, state.Config?.RunModifiers, actor, value);
            }

            var beneficiary = target ?? actor;
            var sacrificeSelfDamageAppliedEarly = false;

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    if (action.Target == EffectTarget.AllEnemies)
                        ExecuteDamageToAllEnemies(state, actor, card, action, value, events, rng, sourceCardInstanceId);
                    else if (target != null
                             && (action.Target == EffectTarget.Self
                                 || TargetRules.IsTargetValidForAction(
                                     state, target, GetEffectiveDamageReach(state, actor, card, action), action)))
                        ExecuteDamage(
                            state, actor, card, action, target, value, events, rng, sourceCardInstanceId,
                            isSacrificeSelfDamage: action.Target == EffectTarget.Self
                                && card.Keywords.Contains("sacrifice"));
                    break;
                case EffectActionType.GainBlock:
                {
                    if (action.SelfDamageFlat > 0
                        && card.Keywords.Contains("sacrifice")
                        && actor.IsAlive)
                    {
                        ApplySacrificeFlatSelfDamage(
                            state, actor, card, action, events, rng, sourceCardInstanceId);
                        sacrificeSelfDamageAppliedEarly = true;
                    }

                    var totalBlock = value + RelicBattleRules.GetOutgoingDefenseFlatBonus(
                        state.Config?.RunModifiers, actor);
                    totalBlock = RelicBattleRules.ApplyPharaohBlockBonus(
                        state.Config?.RunModifiers, actor, totalBlock);
                    DamageRules.ApplyBlock(beneficiary, totalBlock, events, state, rng);
                    TalentBattleRules.AfterDefenseBlockApplied(
                        state, actor, beneficiary, totalBlock, events, rng, card);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, beneficiary.Id, false, 0);
                    break;
                }
                case EffectActionType.Heal:
                {
                    var healAmount = action.HealMaxHpPercent > 0
                        ? System.Math.Max(1, (int)System.Math.Round(
                            beneficiary.MaxHp * action.HealMaxHpPercent / 100f))
                        : value;
                    DamageRules.ApplyHeal(state, beneficiary, healAmount, events, actor);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, beneficiary.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyStatus:
                    if (action.Target == EffectTarget.AllEnemies)
                        ExecuteStatusToAllEnemies(state, actor, card, action, events, rng);
                    else if (action.Target == EffectTarget.AllAllies)
                        ExecuteStatusToAllAllies(state, actor, card, action, events, rng);
                    else if (action.Target == EffectTarget.RandomEnemies)
                        ExecuteStatusToRandomEnemies(state, actor, action, events, rng, card);
                    else if (action.Target == EffectTarget.RandomEnemy
                             || action.Target == EffectTarget.RandomAlly)
                    {
                        // ExecuteOne 入口已 Resolve / targetOverride，勿二次重抽随机目标
                        if (target != null)
                            ApplyStatusWithTalents(state, actor, target, action, events, card, rng);
                    }
                    else if (target != null
                             && (action.Target is EffectTarget.Self or EffectTarget.LastActionActor
                                 || TargetRules.IsTargetValidForAction(state, target, action.Reach, action)))
                        ApplyStatusWithTalents(state, actor, target, action, events, card, rng);
                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Status, target?.Id ?? actor.Id, false, 0);
                    break;
                case EffectActionType.ApplyAnubisAvatar:
                    AnubisAvatarRules.Apply(state, actor, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.RemoveStatus:
                    if (target != null)
                        StatusRules.RemoveStatus(target, action.StatusId, action.Stacks, events);
                    break;
                case EffectActionType.SwapPositionWithFrontAlly:
                    PositionRules.SwapWithAdjacentAlly(state, actor, -1, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.SwapTargetWithBehind:
                {
                    // 先伤后换：优先用上一动作的目标（伤害目标），勿重新抽 DefaultEnemy
                    var swapTarget = targetOverride;
                    if (swapTarget == null
                        && !string.IsNullOrEmpty(state.LastAction.TargetId)
                        && state.LastAction.ActionKind == ActionKind.Attack)
                    {
                        swapTarget = state.GetCombatant(state.LastAction.TargetId);
                    }

                    if (swapTarget == null || !swapTarget.IsAlive)
                    {
                        swapTarget = TargetRules.ResolveTarget(
                            state, actor, action.Target, card.InstanceId, rng, action);
                    }

                    if (swapTarget != null && swapTarget.IsAlive)
                        PositionRules.SwapTargetWithBehind(state, swapTarget, events);
                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Status, swapTarget?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DrawCardsNextTurn:
                    QueueDrawNextTurn(state, actor, card, value, action.CostReduction, events);
                    break;
                case EffectActionType.DrawCards:
                    // 无【快速启动】：一律下回合抽；有快速启动：当回合立即抽到手中
                    if (HasQuickStart(card))
                    {
                        DeckRules.DrawCards(state, actor.Team, rng, value, events);
                        events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"立即抽 {value} 张牌")
                        {
                            CombatantId = actor.Id,
                            CardInstanceId = card.InstanceId
                        });
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    }
                    else
                        QueueDrawNextTurn(state, actor, card, value, action.CostReduction, events);
                    break;
                case EffectActionType.ReflectLastDamageToAttacker:
                    var attacker = TargetRules.ResolveTarget(state, actor, EffectTarget.LastActionActor, card.InstanceId);
                    if (attacker != null)
                    {
                        var reflected = state.LastAction.DamageAmount * action.Value / 100;
                        if (reflected > 0)
                            DamageRules.ApplyDamage(
                                state, actor, attacker, reflected, card.CardType, events,
                                canTriggerParry: false,
                                sourceCardInstanceId: sourceCardInstanceId);
                    }
                    break;
                case EffectActionType.GainBlockFromLastDamagePercent:
                {
                    var blockFromDamage = state.LastAction.DamageAmount * action.Value / 100;
                    if (blockFromDamage > 0)
                    {
                        DamageRules.ApplyBlock(actor, blockFromDamage, events, state, rng);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    }

                    break;
                }
                case EffectActionType.LockRandomPlayerPlaysThisTurn:
                    ApplyRandomPlayerPlayLock(state, actor, events, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ReducePlayerEnergyRegenNextTurn:
                    if (action.Value > 0)
                    {
                        // 扣施法者己方队伍能量：敌方怪物扣敌方预算，玩家扣玩家回复
                        if (actor.Team == TeamSide.Enemy)
                        {
                            state.PendingEnemyEnergyRegenPenaltyNextTurn += action.Value;
                            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                                $"敌方下回合能量 -{action.Value}")
                            {
                                CombatantId = actor.Id,
                                Amount = action.Value
                            });
                        }
                        else
                        {
                            state.PendingPlayerEnergyRegenPenaltyNextTurn += action.Value;
                            foreach (var unit in state.GetTeam(TeamSide.Player))
                            {
                                if (unit == null || !unit.IsAlive)
                                    continue;

                                StatusRules.ApplyStatus(
                                    state,
                                    unit,
                                    StatusCatalog.SoulDrain,
                                    Math.Max(1, action.Value),
                                    -1,
                                    events);
                            }

                            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                                $"下回合能量回复 -{action.Value}")
                            {
                                CombatantId = actor.Id,
                                Amount = action.Value,
                                TargetId = StatusCatalog.SoulDrain
                            });
                        }
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.ArmRespondDamageRedirect:
                    // 无条件时立即武装；有应对条件时由应对结算 / TryArmFromEnemyCardResolve 武装
                    if (action.Condition == ReactionConditionType.None)
                    {
                        DefenderRespondArmRules.ArmRedirectDouble(state, actor.Id);
                        actor.RespondArmedThisTurn = true;
                        events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                            $"{actor.DisplayName} 武装「伤害转嫁×2」")
                        {
                            CombatantId = actor.Id
                        });
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    break;
                case EffectActionType.SummonOrGainBlock:
                    ExecuteSummonOrGainBlock(state, actor, action, events, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                case EffectActionType.GrantDodgeChance:
                    if (action.Value > 0)
                    {
                        actor.DodgeChanceBonus = action.Value / 100f;
                        events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                            $"{actor.DisplayName} 闪避率 +{action.Value}%")
                        {
                            CombatantId = actor.Id,
                            Amount = action.Value
                        });
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    break;
                // ===== v0.9 新增动作类型 =====
                case EffectActionType.ConsumeBlockDealDamage:
                {
                    if (target == null)
                        break;
                    var blockConsumed = actor.Block;
                    if (blockConsumed <= 0 && action.Value <= 0)
                        break;
                    actor.Block = 0;
                    if (blockConsumed > 0)
                    {
                        events.Add(new BattleEvent(BattleEventKind.BlockGained,
                            $"{actor.DisplayName} 消耗护甲 {blockConsumed}")
                        {
                            CombatantId = actor.Id,
                            Amount = blockConsumed,
                            TargetId = actor.Id
                        });
                    }
                    var consumeDamage = blockConsumed + Math.Max(0, action.Value);
                    if (consumeDamage > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, consumeDamage, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DamagePerRespondCount:
                {
                    if (target == null)
                        break;
                    var count = state.Config?.RunModifiers?.ExpeditionRespondSuccessCount ?? state.RespondSuccessCount;
                    var dmg = Math.Max(0, count * action.Value);
                    if (dmg > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DoubleStatusStacks:
                {
                    if (target == null
                        || !TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                        break;
                    // 仅翻倍层数，RemainingTurns/Duration 不变
                    DoubleTargetStatusStacks(target, StatusCatalog.Poison, events);
                    DoubleTargetStatusStacks(target, StatusCatalog.Burn, events);
                    MinionTraitRules.SyncSpiderPoisonVulnerability(state, target, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target.Id, false, 0);
                    break;
                }
                case EffectActionType.RecycleExhaustCardsFromDiscard:
                {
                    RecycleExhaustedCardsToDraw(state, actor.Team, events, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageScaledByActorHpLoss:
                {
                    if (target == null)
                        break;
                    var basePower = value;
                    var step = Math.Max(1, action.HpLossStepPercent);
                    var lostPercent = Math.Max(0, (actor.MaxHp - actor.Hp) * 100 / Math.Max(1, actor.MaxHp));
                    var steps = lostPercent / step;
                    basePower += steps * Math.Max(0, action.HpLossStepValue);
                    if (basePower > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, basePower, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageAlternateIfHealedThisTurn:
                {
                    if (target == null)
                        break;
                    var dmg = actor.HealedThisTurn && action.AlternateValueIfHealed > 0
                        ? action.AlternateValueIfHealed
                        : value;
                    if (dmg > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageBonusPerTargetDebuffStack:
                {
                    if (target == null)
                        break;
                    var debuffStacks = CountDebuffStacks(target);
                    var dmg = value + debuffStacks * Math.Max(0, action.Stacks);
                    if (dmg > 0 && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                // ===== v0.9 毒蛇女王 / 巫妖女王 新增动作 =====
                case EffectActionType.GainEnergy:
                {
                    if (action.Value > 0 && actor.IsAlive)
                    {
                        EnergyRules.GainTemporary(state, action.Value);
                        events.Add(new BattleEvent(BattleEventKind.EnergyChanged, $"获得 {action.Value} 能量")
                        {
                            CombatantId = actor.Id,
                            Energy = state.EnergyCurrent,
                            EnergyMax = state.EnergyMax,
                            EnergyRemaining = state.EnergyCurrent,
                            Amount = action.Value
                        });
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.GainEnergyNextTurn:
                {
                    if (action.Value > 0)
                    {
                        state.PendingPlayerEnergyGainNextTurn += action.Value;
                        events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded,
                            $"下回合开始获得 {action.Value} 能量")
                        {
                            CombatantId = actor.Id,
                            CardInstanceId = card?.InstanceId ?? 0,
                            Amount = action.Value
                        });
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.IncreaseRandomPlayerHandCosts:
                {
                    var pickCount = Math.Max(0, action.Value);
                    var bump = Math.Max(1, action.Stacks);
                    if (pickCount > 0 && state.PlayerHand.Count > 0)
                    {
                        var indices = new List<int>(state.PlayerHand.Count);
                        for (var i = 0; i < state.PlayerHand.Count; i++)
                        {
                            if (state.PlayerHand[i] != null && state.PlayerHand[i].IsUsable)
                                indices.Add(i);
                        }

                        for (var n = 0; n < pickCount && indices.Count > 0; n++)
                        {
                            var pick = rng.NextIndex(indices.Count);
                            var handIndex = indices[pick];
                            indices.RemoveAt(pick);
                            var handCard = state.PlayerHand[handIndex];
                            if (handCard == null)
                                continue;
                            handCard.Cost += bump;
                            events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded,
                                $"{handCard.DisplayName} 费用+{bump}")
                            {
                                CardInstanceId = handCard.InstanceId,
                                Amount = bump
                            });
                        }
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DrawToHandLimit:
                {
                    var hand = state.GetHand(actor.Team);
                    var draw = Math.Max(0, state.Config.HandLimit - hand.Count);
                    if (draw > 0)
                        DeckRules.DrawCards(state, actor.Team, rng, draw, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.GainBlockBonusIfSelfPoisoned:
                {
                    // 始终给自己叠甲，不受 Target 解析影响；有中毒时额外 +Stacks。
                    var totalBlock = Math.Max(0, action.Value);
                    if (StatusRules.HasStatus(actor, StatusCatalog.Poison))
                        totalBlock += Math.Max(0, action.Stacks);
                    totalBlock += RelicBattleRules.GetOutgoingDefenseFlatBonus(state.Config?.RunModifiers, actor);
                    totalBlock = RelicBattleRules.ApplyPharaohBlockBonus(state.Config?.RunModifiers, actor, totalBlock);
                    DamageRules.ApplyBlock(actor, totalBlock, events, state, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Defense, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyPoisonBySpeedCompare:
                {
                    if (target != null
                        && TargetRules.IsTargetValidForAction(state, target, action.Reach, action)
                        && value > 0)
                    {
                        DamageRules.ApplyDamage(
                            state, actor, target, value, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            ignoreDefPercent: action.IgnoreDefPercent,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    if (target != null
                        && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        var actorSpeed = StatusRules.GetEffectiveSpeed(state, actor);
                        var targetSpeed = StatusRules.GetEffectiveSpeed(state, target);
                        var targetSlower = targetSpeed < actorSpeed;
                        // 慢于施法者：Stacks 层 / Duration 回合；否则 1 层 / AlternateValue 回合（默认 3）。
                        var stacks = targetSlower ? Math.Max(1, action.Stacks) : 1;
                        var duration = targetSlower
                            ? (action.Duration < 0 ? -1 : Math.Max(1, action.Duration))
                            : (action.AlternateValue > 0 ? action.AlternateValue : 3);
                        StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, stacks, duration, events);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.RemovePoisonHealPerStack:
                {
                    V09NewMechanicsRules.RemovePoisonHealPerStack(state, actor, action.Value, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.TransferHalfPoisonToRandomEnemy:
                {
                    V09NewMechanicsRules.TransferHalfPoisonToRandomEnemy(state, actor, rng, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ApplyConstrict:
                {
                    if (action.Target == EffectTarget.AllEnemies)
                    {
                        var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                        var any = false;
                        foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                        {
                            var t = state.GetCombatant(targetId);
                            if (t == null || !t.IsAlive)
                                continue;

                            V09NewMechanicsRules.ApplyConstrict(
                                state, actor, t, action.Value, action.Duration, events, applyCasterLock: false);
                            any = true;
                        }

                        if (any)
                            V09NewMechanicsRules.ApplyConstrictCasterLock(state, actor, action.Duration, events);

                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, actor.Id, false, 0);
                    }
                    else if (target != null && target.Team != actor.Team)
                    {
                        V09NewMechanicsRules.ApplyConstrict(
                            state, actor, target, action.Value, action.Duration, events);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target.Id, false, 0);
                    }
                    else
                    {
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, actor.Id, false, 0);
                    }

                    break;
                }
                case EffectActionType.SettlePoisonAndClear:
                {
                    if (action.Target == EffectTarget.AllEnemies)
                    {
                        var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                        foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                        {
                            var t = state.GetCombatant(targetId);
                            if (t != null && t.IsAlive)
                                V09NewMechanicsRules.SettlePoisonAndClear(state, actor, t, events);
                        }

                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    }
                    else if (target != null
                             && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        V09NewMechanicsRules.SettlePoisonAndClear(state, actor, target, events);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target.Id, false, 0);
                    }
                    else
                    {
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    }

                    break;
                }
                case EffectActionType.ApplyDelayedDamage:
                {
                    if (action.Target == EffectTarget.AllEnemies)
                    {
                        var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                        foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                        {
                            var t = state.GetCombatant(targetId);
                            if (t != null && t.IsAlive)
                                StatusRules.ApplyStatus(
                                    state, t, StatusCatalog.DelayedDamage, Math.Max(1, action.Value), 1, events);
                        }

                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    }
                    else if (target != null
                             && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                    {
                        StatusRules.ApplyStatus(
                            state, target, StatusCatalog.DelayedDamage, Math.Max(1, action.Value), 1, events);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, target.Id, false, 0);
                    }
                    else
                    {
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    }

                    break;
                }
                case EffectActionType.ApplyStatusNextTurn:
                {
                    if (beneficiary != null
                        && !string.IsNullOrEmpty(action.StatusId)
                        && action.Stacks > 0)
                    {
                        state.PendingStatusesNextTurn.Add(new PendingNextTurnStatus
                        {
                            CombatantId = beneficiary.Id,
                            StatusId = action.StatusId,
                            Stacks = action.Stacks,
                            Duration = action.Duration,
                            SourceLabel = card?.DisplayName ?? action.StatusId
                        });
                        // 禁止发 StatusApplied：演出脚标会当成已挂上 attack_up_pct 等状态
                        events.Add(new BattleEvent(BattleEventKind.CardResolvedEnded,
                            $"下回合开始获得{action.StatusId}×{action.Stacks}")
                        {
                            CombatantId = beneficiary.Id,
                            CardInstanceId = card?.InstanceId ?? 0,
                            Amount = action.Stacks
                        });
                    }

                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Status, beneficiary?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.EtherealCountBonusDamage:
                {
                    var bonus = V09NewMechanicsRules.GetEtherealEntryCount(state) * Math.Max(0, action.Stacks);
                    var dmg = value + bonus;
                    if (action.Target == EffectTarget.AllEnemies)
                    {
                        var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                        foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                        {
                            var t = state.GetCombatant(targetId);
                            if (t != null && t.IsAlive)
                                DamageRules.ApplyDamage(state, actor, t, dmg, card.CardType, events,
                                    canTriggerParry: true, rng: rng, cardCost: card.Cost,
                                    sourceCardInstanceId: sourceCardInstanceId, isAoEWave: true);
                        }
                    }
                    else if (target != null && TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                        DamageRules.ApplyDamage(state, actor, target, dmg, card.CardType, events,
                            canTriggerParry: true, rng: rng, cardCost: card.Cost,
                            sourceCardInstanceId: sourceCardInstanceId);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, target?.Id, false, 0);
                    break;
                }
                case EffectActionType.AddTokenCardToHand:
                {
                    V09NewMechanicsRules.AddTokenCardToHand(state, actor, action.TokenCardId, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ShuffleHandCosts:
                {
                    V09NewMechanicsRules.ShuffleHandCosts(state, actor, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.RandomSnakeGodEffect:
                {
                    ExecuteRandomSnakeGodEffect(state, actor, card, action, events, rng, sourceCardInstanceId);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Attack, "", false, 0);
                    break;
                }
                case EffectActionType.SealNextEnemyCard:
                {
                    state.PendingEnemyCardSeals = Math.Max(0, state.PendingEnemyCardSeals) + 1;
                    events.Add(new BattleEvent(BattleEventKind.StatusApplied, "灵界封印：敌方下一张牌将失效")
                    {
                        CombatantId = actor.Id,
                        Amount = state.PendingEnemyCardSeals
                    });
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.LockSelfCards:
                {
                    if (actor != null && actor.IsAlive)
                        CardLockRules.ApplyLock(actor, action.Value);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.LockAttackCards:
                {
                    var lockTarget = target ?? actor;
                    if (lockTarget != null && lockTarget.IsAlive)
                        CardLockRules.ApplyAttackLock(lockTarget, Math.Max(1, action.Value));
                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Status, lockTarget?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DrawCardsIfEthereal:
                {
                    var draw = StatusRules.HasStatus(actor, StatusCatalog.Ethereal) && action.AlternateValue > 0
                        ? action.AlternateValue
                        : Math.Max(0, action.Value);
                    if (draw <= 0)
                        break;

                    if (HasQuickStart(card))
                    {
                        DeckRules.DrawCards(state, actor.Team, rng, draw, events);
                        state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    }
                    else
                        QueueDrawNextTurn(state, actor, card, draw, action.CostReduction, events);
                    break;
                }
                case EffectActionType.BuffAllOtherAllies:
                {
                    foreach (var ally in state.GetTeam(actor.Team))
                    {
                        if (ally == null || !ally.IsAlive || ally.Id == actor.Id)
                            continue;
                        StatusRules.ApplyStatus(state, ally, action.StatusId, Math.Max(1, action.Stacks), action.Duration, events);
                    }
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.RevealEnemyIntent:
                {
                    var revealCount = System.Math.Max(1, action.Value);
                    var revealed = 0;
                    foreach (var intent in state.EnemyIntents)
                    {
                        if (!intent.IsHidden)
                            continue;

                        intent.IsHidden = false;
                        revealed++;
                        var intentCard = state.GetCard(intent.CardInstanceId);
                        var intentOwnerId = intent.OwnerCombatantId;
                        var intentOwner = intentOwnerId != null ? state.GetCombatant(intentOwnerId) : null;
                        var label = intentCard != null
                            ? CardPowerRules.DescribeCardEffect(intentCard, intentOwner, false)
                            : "未知意图";
                        events.Add(new BattleEvent(BattleEventKind.EnemyIntentPrepared, label)
                        {
                            CardInstanceId = intent.CardInstanceId
                        });
                        if (revealed >= revealCount)
                            break;
                    }

                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DealDamageRandomCharacterAlly:
                {
                    var allyTarget = TargetRules.ResolveTarget(
                        state, actor, EffectTarget.RandomAllyByCharacterId, sourceCardInstanceId, rng, action);
                    if (allyTarget != null)
                    {
                        DamageRules.ApplyDamage(
                            state, actor, allyTarget, action.Value, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            sourceCardInstanceId: sourceCardInstanceId);
                    }
                    break;
                }
                case EffectActionType.StripBlockThenDealDamage:
                {
                    var stripTarget = TargetRules.PickEnemyPreferBlock(
                        state, actor, action.Reach, rng)
                        ?? TargetRules.ResolveTarget(
                            state, actor, action.Target, sourceCardInstanceId, rng, action);

                    V09BossMechanicsRules.StripBlockThenDealDamage(
                        state, actor, stripTarget, card, action, events, rng, sourceCardInstanceId);
                    break;
                }
                case EffectActionType.SwapRandomEnemies:
                {
                    var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
                    V09BossMechanicsRules.SwapRandomEnemies(
                        state,
                        enemyTeam,
                        System.Math.Max(1, action.Value),
                        rng,
                        events,
                        action.StatusId,
                        action.Stacks,
                        action.Duration);
                    break;
                }
                case EffectActionType.AdjustSelfStatusRandom:
                {
                    if (rng == null || string.IsNullOrEmpty(action.StatusId))
                        break;
                    var delta = rng.NextIndex(2) == 0 ? -1 : 1;
                    if (action.StatusId == StatusCatalog.RisingTide)
                        V09BossMechanicsRules.AdjustRisingTideStacks(state, actor, delta, events);
                    break;
                }
                case EffectActionType.ApplyAttackUpPerSelfStatusStack:
                {
                    V09BossMechanicsRules.ApplyAttackUpPerSelfStatusStack(state, actor, action, events);
                    break;
                }
                case EffectActionType.LockRisingTideStacks:
                {
                    V09BossMechanicsRules.LockRisingTide(
                        state, actor, action.Duration >= 0 ? action.Duration : 2, events);
                    break;
                }
                case EffectActionType.StealAllBlockFromRandomEnemyPreferArmored:
                {
                    StealAllBlockFromRandomEnemyPreferArmored(state, actor, events, rng);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.StealAllBuffs:
                {
                    if (action.Target == EffectTarget.AllEnemies)
                        StealAllBuffsFromAllEnemies(state, actor, events);
                    else if (target != null)
                        StealAllBuffsFromTarget(state, actor, target, events);

                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Status, target?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.ClearAllDebuffs:
                {
                    StatusRules.ClearAllDebuffs(actor, events);
                    state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DoubleAllDebuffStacksAndDuration:
                {
                    // 溃烂钳击：仅当上一击被成功应对时翻倍减益
                    if (card?.DefinitionId == AbyssMonsterCardCatalog.FesterClawCardId
                        && (state == null || !state.LastDamageHadRespondDefense))
                        break;

                    if (target != null)
                        DoubleAllDebuffStacksAndDuration(target, events);
                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Status, target?.Id ?? actor.Id, false, 0);
                    break;
                }
                case EffectActionType.DealTrueDamagePerStatusStack:
                {
                    if (target == null || !target.IsAlive)
                        break;
                    if (!TargetRules.IsTargetValidForAction(state, target, action.Reach, action))
                        break;

                    var statusId = string.IsNullOrEmpty(action.StatusId)
                        ? StatusCatalog.Poison
                        : action.StatusId;
                    var perStack = action.Stacks > 0 ? action.Stacks : 1;
                    var stacks = StatusRules.GetStatusStacks(target, statusId);
                    // 贯穿触手：按命中前中毒结算；主伤害触发的深渊被动上毒不计入本段
                    if (card?.DefinitionId == AbyssMonsterCardCatalog.PiercingTentacleCardId
                        && statusId == StatusCatalog.Poison
                        && MinionTraitRules.HasTrait(actor, MinionTraitCatalog.AbyssCreaturePoisonOnDamage)
                        && stacks >= MinionTraitCatalog.AbyssCreaturePoisonStacks)
                    {
                        stacks -= MinionTraitCatalog.AbyssCreaturePoisonStacks;
                    }

                    var trueDmg = stacks * perStack;
                    if (trueDmg > 0)
                    {
                        DamageRules.ApplyTrueDamage(
                            state, actor, target, trueDmg, events, sourceCardInstanceId);
                    }

                    state.LastAction = new LastActionSnapshot(
                        actor.Id, ActionKind.Attack, target.Id, false, trueDmg);
                    break;
                }
            }

            if (action.SelfDamageFlat > 0 && actor.IsAlive && !sacrificeSelfDamageAppliedEarly)
            {
                if (card.Keywords.Contains("sacrifice"))
                {
                    ApplySacrificeFlatSelfDamage(
                        state, actor, card, action, events, rng, sourceCardInstanceId);
                }
                else
                {
                    DamageRules.ApplyDamage(
                        state, actor, actor, action.SelfDamageFlat, CardType.Status, events,
                        canTriggerParry: false, isSacrificeDamage: false, rng: rng,
                        sourceCardInstanceId: sourceCardInstanceId);
                }
            }
        }

        static void ApplySacrificeFlatSelfDamage(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            var dmg = RelicEffectRules.AdjustSacrificeSelfDamage(
                state, state.Config?.RunModifiers, actor, action.SelfDamageFlat);
            DamageRules.ApplyDamage(
                state, actor, actor, dmg, CardType.Status, events,
                canTriggerParry: false, isSacrificeDamage: true, rng: rng,
                sourceCardInstanceId: sourceCardInstanceId);
            if (state.LastAction.DamageAmount > 0)
            {
                TalentBattleRules.OnSacrificeHpSpent(state, actor, state.LastAction.DamageAmount);
                PassiveCardMechanicsRules.TryTriggerBloodFrenzyOnSacrifice(state, actor, events);
            }
        }

        static void ApplyRandomPlayerPlayLock(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var pool = new List<CombatantState>();
            foreach (var unit in state.GetTeam(TeamSide.Player))
            {
                if (unit.IsAlive)
                    pool.Add(unit);
            }

            if (pool.Count == 0)
                return;

            var index = rng != null ? rng.NextIndex(pool.Count) : 0;
            var victim = pool[index];
            // 仅锁下回合：本回合不打断已排队出牌。ApplyLock(2) 经回合开始扣减后，下回合规划/结算仍锁定。
            StatusRules.ApplyStatus(state, victim, StatusCatalog.Deterrence, 1, 2, events);
            CardLockRules.ApplyLock(victim, 2);
            events.Add(new BattleEvent(BattleEventKind.ReactionTriggered,
                $"{victim.DisplayName} 被威慑：下回合无法使用卡牌")
            {
                CombatantId = victim.Id,
                TargetId = StatusCatalog.Deterrence,
                Amount = 1
            });
        }

        static void ExecuteDamageToAllEnemies(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            int value,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var targetIds = PositionRules.SnapshotAliveCombatantIds(state, enemyTeam);
            if (targetIds.Count == 0)
                return;

            var totalLifesteal = 0;
            var anyKill = false;

            foreach (var targetId in targetIds)
            {
                var target = state.GetCombatant(targetId);
                if (target == null || !target.IsAlive)
                    continue;

                var primaryPower = CombatMechanicsRules.ComputeActionValueForTarget(state, action, actor, target);
                primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, primaryPower);
                primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(
                    state, action, target, primaryPower, actor);
                primaryPower = PassiveCardMechanicsRules.ApplyEndlessBladeMultiplier(state, card, primaryPower);

                DamageRules.ApplyDamage(
                    state,
                    actor,
                    target,
                    primaryPower,
                    card.CardType,
                    events,
                    isSacrificeDamage: false,
                    rng: rng,
                    cardCost: card.Cost,
                    ignoreDefPercent: action.IgnoreDefPercent,
                    sourceCardInstanceId: sourceCardInstanceId,
                    isAoEWave: true);

                if (state.LastAction.DamageAmount > 0)
                    totalLifesteal += state.LastAction.DamageAmount;
                if (state.LastAction.WasKill)
                    anyKill = true;
            }

            var lifestealPercent = action.LifestealPercent;
            if (lifestealPercent <= 0)
                lifestealPercent = CombatMechanicsRules.GetPendingLifestealPercent(actor);

            if (lifestealPercent > 0 && totalLifesteal > 0)
                CombatMechanicsRules.ApplyLifesteal(state, actor, totalLifesteal, lifestealPercent, events);

            if (action.OnKillHealAmount > 0 && anyKill)
                DamageRules.ApplyHeal(state, actor, action.OnKillHealAmount, events, actor);
        }

        static void ExecuteStatusToAllEnemies(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
            {
                var target = state.GetCombatant(targetId);
                if (target == null || !target.IsAlive)
                    continue;

                ApplyStatusWithTalents(state, actor, target, action, events, card, rng);
            }
        }

        static void ExecuteStatusToAllAllies(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng = null)
        {
            if (actor == null)
                return;

            foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, actor.Team))
            {
                var target = state.GetCombatant(targetId);
                if (target == null || !target.IsAlive)
                    continue;

                ApplyStatusWithTalents(state, actor, target, action, events, card, rng);
            }
        }

        static void ApplyStatusWithTalents(
            BattleState state,
            CombatantState actor,
            CombatantState target,
            EffectActionSpec action,
            List<BattleEvent> events,
            CardInstanceState card = null,
            BattleRng rng = null)
        {
            if (action.ChancePercent > 0 && action.ChancePercent < 100)
            {
                if (rng != null && rng.NextInt(1, 100) > action.ChancePercent)
                    return;
            }

            var stacks = action.Stacks;
            if (card?.DefinitionId == PassiveCardMechanicsRules.FinalBindCardId
                && action.StatusId == StatusCatalog.Poison)
                stacks = PassiveCardMechanicsRules.ResolveFinalBindPoisonStacks(state, target, stacks);

            // v0.9 毒囊破裂：施毒者有 venom_sac_burst 时 +1 层中毒
            stacks = V09NewMechanicsRules.AdjustPoisonStacksForVenomSac(actor, action.StatusId, stacks);

            var duration = action.Duration;
            if (action.StatusId == StatusCatalog.Poison)
            {
                TalentBattleRules.AdjustPoisonStacks(state, actor, ref stacks);
                duration = TalentBattleRules.AdjustPoisonDuration(state, actor, duration);
            }
            StatusRules.ApplyStatus(state, target, action.StatusId, stacks, duration, events);

            if (!action.SplashBehindTarget || target == null)
                return;

            var splashTargetId = PositionRules.SnapshotCombatantBehindId(state, target);
            if (string.IsNullOrEmpty(splashTargetId))
                return;

            var behind = state.GetCombatant(splashTargetId);
            if (behind == null || !behind.IsAlive)
                return;

            var splashStacks = stacks;
            if (action.SplashPowerPercent > 0 && action.SplashPowerPercent < 100)
            {
                splashStacks = System.Math.Max(1,
                    (int)System.Math.Round(stacks * action.SplashPowerPercent / 100f));
            }

            StatusRules.ApplyStatus(state, behind, action.StatusId, splashStacks, action.Duration, events);
        }

        static void ExecuteStatusToRandomEnemies(
            BattleState state,
            CombatantState actor,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            CardInstanceState card = null)
        {
            var count = action.Value > 0 ? action.Value : 1;
            foreach (var target in TargetRules.PickRandomEnemies(state, actor.Team, count, rng))
                ApplyStatusWithTalents(state, actor, target, action, events, card, rng);
        }

        static void ExecuteDamage(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState target,
            int value,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId,
            bool isSacrificeSelfDamage = false)
        {
            if (target == null)
                return;

            PassiveCardMechanicsRules.PrepareGargoyleSunderTarget(state, target, card, events);

            var hitCount = System.Math.Max(1, action.HitCount);
            var repeatTimes = 1;
            if (action.RepeatPerEnemyAttackCardThisTurn > 0)
            {
                // 基础 1 次 + 对立队伍本回合计划中的每张攻击牌（含尚未结算）各额外 1 次
                repeatTimes = 1 + CountOpponentAttackCardsInPlan(state, actor.Team);
                repeatTimes = System.Math.Max(1, repeatTimes);
            }

            for (var repeat = 0; repeat < repeatTimes; repeat++)
            {
                var damageTarget = target;
                if (action.RepeatPerEnemyAttackCardThisTurn > 0 && repeat > 0)
                {
                    damageTarget = TargetRules.ResolveTarget(
                        state, actor, EffectTarget.RandomEnemy, card.InstanceId, rng, action);
                    if (damageTarget == null)
                        continue;
                }

                for (var hit = 0; hit < hitCount; hit++)
                {
                    var hitTarget = damageTarget;
                    if (action.Target == EffectTarget.RandomEnemy)
                    {
                        hitTarget = TargetRules.ResolveTarget(
                            state, actor, EffectTarget.RandomEnemy, card.InstanceId, rng, action);
                        if (hitTarget == null)
                            continue;
                    }
                    ApplySingleHit(
                        state, actor, card, action, hitTarget, value, events, rng,
                        sourceCardInstanceId, isSacrificeSelfDamage);
                }
            }
        }

        static int CountOpponentAttackCardsInPlan(BattleState state, TeamSide actorTeam)
        {
            if (state == null)
                return 0;

            var plan = actorTeam == TeamSide.Enemy ? state.PlayerPlan : state.EnemyPlan;
            if (plan?.PlayQueue == null)
                return 0;

            var count = 0;
            foreach (var cardId in plan.PlayQueue)
            {
                var planned = state.GetCard(cardId);
                if (planned != null && planned.CardType == CardType.Attack)
                    count++;
            }

            return count;
        }

        static void ApplySingleHit(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            CombatantState target,
            int value,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId,
            bool isSacrificeSelfDamage)
        {
            if (target == null || !target.IsAlive)
                return;

            var primaryPower = CombatMechanicsRules.ComputeActionValueForTarget(state, action, actor, target);
            primaryPower = TargetReachRules.AdjustPowerForTarget(state, action, target, primaryPower);
            primaryPower = CombatMechanicsRules.ComputeConditionalDamageBonus(
                state, action, target, primaryPower, actor);
            primaryPower = PassiveCardMechanicsRules.ApplyEndlessBladeMultiplier(state, card, primaryPower);
            // 须在最终算伤之后：虚化加成/祛除不能被上面的重算覆盖
            primaryPower = V09NewMechanicsRules.AdjustRealmBurstDamage(
                state, actor, card, primaryPower, events);

            if (card.Keywords.Contains("respond_status") && state.PlayerRespondStatusUsedThisTurn)
                primaryPower = System.Math.Max(1, primaryPower * 3);

            var lifestealPercent = action.LifestealPercent;
            if (lifestealPercent <= 0 && !action.LifestealUnblockedOnly)
                lifestealPercent = CombatMechanicsRules.GetPendingLifestealPercent(actor);

            var splashTargetId = action.SplashBehindTarget
                ? PositionRules.SnapshotCombatantBehindId(state, target)
                : null;

            var targetHadBlock = target.Block > 0;

            DamageRules.ApplyDamage(
                state,
                actor,
                target,
                primaryPower,
                isSacrificeSelfDamage ? CardType.Status : card.CardType,
                events,
                isSacrificeDamage: isSacrificeSelfDamage,
                canTriggerParry: !isSacrificeSelfDamage,
                rng: rng,
                cardCost: card.Cost,
                ignoreDefPercent: action.IgnoreDefPercent,
                sourceCardInstanceId: sourceCardInstanceId);

            var hpDamage = state.LastAction.DamageAmount;
            if (isSacrificeSelfDamage && hpDamage > 0)
                TalentBattleRules.OnSacrificeHpSpent(state, actor, hpDamage);

            if (action.LifestealUnblockedOnly && hpDamage > 0)
                DamageRules.ApplyHeal(state, actor, hpDamage, events, actor, isLifesteal: true);
            else if (lifestealPercent > 0 && hpDamage > 0)
            {
                CombatMechanicsRules.ApplyLifesteal(
                    state, actor, hpDamage, lifestealPercent, events);
            }

            if (action.OnKillHealAmount > 0 && state.LastAction.WasKill)
                DamageRules.ApplyHeal(state, actor, action.OnKillHealAmount, events, actor);

            PassiveCardMechanicsRules.AfterSingleHitResolved(
                state, actor, card, target, targetHadBlock, events, rng);

            if (action.SplashBehindTarget && !string.IsNullOrEmpty(splashTargetId))
            {
                var behind = state.GetCombatant(splashTargetId);
                if (behind != null && behind.IsAlive)
                {
                    var splashPower = System.Math.Max(1,
                        (int)System.Math.Round(primaryPower * action.SplashPowerPercent / 100f));
                    DamageRules.ApplyDamage(state, actor, behind, splashPower, card.CardType, events,
                        rng: rng, cardCost: card.Cost, ignoreDefPercent: action.IgnoreDefPercent,
                        sourceCardInstanceId: sourceCardInstanceId);
                }
            }
        }

        static void ExecuteSummonOrGainBlock(
            BattleState state,
            CombatantState actor,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null)
                return;

            var slot = SummonRules.FindEmptyTeamSlot(state, actor.Team);
            if (slot.HasValue && !string.IsNullOrEmpty(action.SummonCharacterId))
            {
                if (!state.Config.SummonTemplates.TryGetValue(action.SummonCharacterId, out var template))
                    template = BuildSummonTemplateFallback(state, actor, action.SummonCharacterId);

                if (template != null)
                {
                    if (!state.Config.SummonTemplates.ContainsKey(action.SummonCharacterId))
                        state.Config.SummonTemplates[action.SummonCharacterId] = template;

                    SummonRules.SpawnFromTemplate(state, template, slot.Value, events);
                    SummonRules.MergeSummonedSkillPoolIntoTeamDeck(state, template, actor.Team, rng, events);
                    return;
                }
            }

            var blockValue = action.FallbackBlockValue;
            if (blockValue > 0)
                DamageRules.ApplyBlock(actor, blockValue, events, state, rng);
        }

        static CombatantConfig BuildSummonTemplateFallback(
            BattleState state,
            CombatantState actor,
            string characterDefinitionId)
        {
            if (state == null || string.IsNullOrEmpty(characterDefinitionId))
                return null;

            foreach (var unit in state.GetTeam(actor.Team))
            {
                if (unit == null || unit.CharacterDefinitionId != characterDefinitionId)
                    continue;

                return new CombatantConfig
                {
                    Id = $"template_{characterDefinitionId}",
                    DisplayName = unit.DisplayName,
                    Team = actor.Team,
                    Slot = FormationSlot.Front,
                    CharacterDefinitionId = characterDefinitionId,
                    Level = unit.Level,
                    MaxHp = unit.MaxHp,
                    StartHp = unit.MaxHp,
                    BaseAttack = unit.BaseAttack,
                    BaseDefense = unit.BaseDefense,
                    Speed = unit.Speed,
                    UseSkillPool = true
                };
            }

            foreach (var pair in state.Config.SummonTemplates)
            {
                if (pair.Key == characterDefinitionId && pair.Value != null)
                    return pair.Value;
            }

            // 骷髅兵等标准召唤物：即使战斗配置未注册模板，也提供可用底稿
            if (characterDefinitionId == MinionTraitCatalog.SkeletonCharacterId)
            {
                return new CombatantConfig
                {
                    Id = "template_char_skeleton",
                    DisplayName = "骷髅兵",
                    Team = actor.Team,
                    Slot = FormationSlot.Front,
                    CharacterDefinitionId = MinionTraitCatalog.SkeletonCharacterId,
                    Level = 1,
                    MaxHp = 25,
                    StartHp = 25,
                    BaseAttack = 0,
                    BaseDefense = 0,
                    Speed = 4,
                    UseSkillPool = true,
                    Traits = { MinionTraitCatalog.SkeletonCardDef }
                };
            }

            return null;
        }

        // ===== v0.9 新增动作类型的辅助方法 =====

        static void ExecuteRandomSnakeGodEffect(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action,
            List<BattleEvent> events,
            BattleRng rng,
            int sourceCardInstanceId)
        {
            if (state == null || actor == null || rng == null)
                return;

            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var roll = rng.NextIndex(3);
            if (roll == 0)
            {
                // AOE 造成 Value 伤害
                foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                {
                    var t = state.GetCombatant(targetId);
                    if (t != null && t.IsAlive)
                        DamageRules.ApplyDamage(state, actor, t, action.Value, card.CardType, events,
                            canTriggerParry: false, rng: rng, cardCost: card.Cost,
                            sourceCardInstanceId: sourceCardInstanceId, isAoEWave: true);
                }
            }
            else if (roll == 1)
            {
                // AOE 施加 Stacks 层中毒（永久）
                foreach (var targetId in PositionRules.SnapshotAliveCombatantIds(state, enemyTeam))
                {
                    var t = state.GetCombatant(targetId);
                    if (t != null && t.IsAlive)
                        StatusRules.ApplyStatus(state, t, StatusCatalog.Poison, Math.Max(1, action.Stacks), -1, events);
                }
            }
            else
            {
                // 随机一名敌人造成 AlternateValue 伤害
                var pick = TargetRules.PickRandomEnemies(state, actor.Team, 1, rng);
                if (pick != null && pick.Count > 0 && pick[0] != null)
                    DamageRules.ApplyDamage(state, actor, pick[0], action.AlternateValue, card.CardType, events,
                        canTriggerParry: false, rng: rng, cardCost: card.Cost,
                        sourceCardInstanceId: sourceCardInstanceId);
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied, "蛇神的回应降临")
            {
                CombatantId = actor.Id,
                Amount = roll
            });
        }

        static bool HasQuickStart(CardInstanceState card) =>
            card?.Keywords != null && card.Keywords.Contains("quick_start");

        static void QueueDrawNextTurn(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            int count,
            int costReduction,
            List<BattleEvent> events)
        {
            if (state == null || count <= 0)
                return;

            state.PendingDrawNextTurn += count;
            if (costReduction > 0)
                state.PendingDrawNextTurnCostReduction = Math.Max(
                    state.PendingDrawNextTurnCostReduction, costReduction);

            events.Add(new BattleEvent(BattleEventKind.CardDrawn, $"下回合额外抽 {count} 张")
            {
                CombatantId = actor?.Id,
                CardInstanceId = card?.InstanceId ?? 0,
                Amount = count
            });
            if (actor != null)
                state.LastAction = new LastActionSnapshot(actor.Id, ActionKind.Status, actor.Id, false, 0);
        }

        static void DoubleTargetStatusStacks(CombatantState target, string statusId, List<BattleEvent> events)
        {
            var existing = StatusRules.FindStatus(target, statusId);
            if (existing == null || existing.Stacks <= 0)
                return;
            existing.Stacks *= 2;
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{target.DisplayName} {StatusCatalog.Get(statusId)?.DisplayName} 层数翻倍")
            {
                CombatantId = target.Id,
                Amount = existing.Stacks,
                TargetId = statusId
            });
        }

        static void DoubleAllDebuffStacksAndDuration(CombatantState target, List<BattleEvent> events)
        {
            if (target == null)
                return;

            foreach (var status in target.Statuses)
            {
                if (status == null || status.Stacks <= 0)
                    continue;
                var def = StatusCatalog.Get(status.StatusId);
                if (!StatusRules.IsDebuffDefinition(def))
                    continue;

                status.Stacks *= 2;
                if (status.RemainingTurns > 0)
                    status.RemainingTurns *= 2;

                events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                    $"{target.DisplayName} {def.DisplayName} 层数与持续时间翻倍")
                {
                    CombatantId = target.Id,
                    Amount = status.Stacks,
                    TargetId = status.StatusId
                });
            }

            CombatantRules.RefreshDerivedStats(target);
        }

        /// <summary>火枪等：结算伤害时使用与选目标相同的有效 Reach（无后排则回退前/中）。</summary>
        static TargetReach GetEffectiveDamageReach(
            BattleState state,
            CombatantState actor,
            CardInstanceState card,
            EffectActionSpec action)
        {
            if (action == null)
                return TargetReach.Any;

            if (card != null
                && card.DefinitionId == "m_musket_shot"
                && action.Reach == TargetReach.BackOnly)
                return TargetReachRules.GetPickReach(state, card, actor);

            return action.Reach;
        }

        static void StealAllBlockFromRandomEnemyPreferArmored(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events,
            BattleRng rng)
        {
            if (state == null || actor == null || !actor.IsAlive)
                return;

            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var armored = new List<CombatantState>();
            var all = new List<CombatantState>();
            foreach (var enemy in state.GetTeam(enemyTeam))
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;
                all.Add(enemy);
                if (enemy.Block > 0)
                    armored.Add(enemy);
            }

            var pool = armored.Count > 0 ? armored : all;
            if (pool.Count == 0 || rng == null)
                return;

            var victim = pool[rng.NextIndex(pool.Count)];
            var stolen = victim.Block;
            if (stolen <= 0)
                return;

            events.Add(new BattleEvent(BattleEventKind.BlockGained, $"{victim.DisplayName} 护甲被移除")
            {
                CombatantId = victim.Id,
                Amount = stolen
            });
            victim.Block = 0;

            // 直接转移数值，避免再吃一次「获得护甲」加成/减成
            actor.Block += stolen;
            events.Add(new BattleEvent(BattleEventKind.BlockGained, actor.DisplayName)
            {
                CombatantId = actor.Id,
                Amount = stolen
            });
            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{actor.DisplayName} 劫掠了 {victim.DisplayName} 的 {stolen} 护甲")
            {
                CombatantId = actor.Id,
                TargetId = victim.Id,
                Amount = stolen
            });
        }

        /// <summary>从全体敌方聚合偷取增益（同 Id 层数相加），再一次性施加到自身。</summary>
        static void StealAllBuffsFromAllEnemies(
            BattleState state,
            CombatantState actor,
            List<BattleEvent> events)
        {
            if (state == null || actor == null || !actor.IsAlive)
                return;

            var enemyTeam = actor.Team == TeamSide.Player ? TeamSide.Enemy : TeamSide.Player;
            var aggregate = new Dictionary<string, (int stacks, int turns)>();
            var victimsWithBuffs = new List<CombatantState>();

            foreach (var enemy in state.GetTeam(enemyTeam))
            {
                if (enemy == null)
                    continue;

                var hadBuff = false;
                foreach (var status in enemy.Statuses)
                {
                    if (status == null || status.Stacks <= 0)
                        continue;
                    var def = StatusCatalog.Get(status.StatusId);
                    if (!StatusRules.IsBuffDefinition(def))
                        continue;

                    hadBuff = true;
                    if (aggregate.TryGetValue(status.StatusId, out var existing))
                    {
                        aggregate[status.StatusId] = (
                            existing.stacks + status.Stacks,
                            MergeStolenDuration(existing.turns, status.RemainingTurns));
                    }
                    else
                    {
                        aggregate[status.StatusId] = (status.Stacks, status.RemainingTurns);
                    }
                }

                if (hadBuff)
                    victimsWithBuffs.Add(enemy);
            }

            if (aggregate.Count == 0)
                return;

            foreach (var victim in victimsWithBuffs)
            {
                foreach (var statusId in aggregate.Keys)
                    StatusRules.RemoveAllStatus(victim, statusId, events);
            }

            foreach (var pair in aggregate)
            {
                StatusRules.ApplyStatus(
                    state, actor, pair.Key, pair.Value.stacks, pair.Value.turns, events);
            }

            events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                $"{actor.DisplayName} 偷取了全体敌人的增益")
            {
                CombatantId = actor.Id,
                Amount = aggregate.Count
            });
        }

        static int MergeStolenDuration(int a, int b)
        {
            if (a < 0 || b < 0)
                return -1;
            return System.Math.Max(a, b);
        }

        static void StealAllBuffsFromTarget(
            BattleState state,
            CombatantState actor,
            CombatantState victim,
            List<BattleEvent> events)
        {
            // 允许从已死亡目标身上偷取（伤害先结算时仍可掠夺）
            if (state == null || actor == null || victim == null || !actor.IsAlive)
                return;

            var toSteal = new List<(string id, int stacks, int turns)>();
            foreach (var status in victim.Statuses)
            {
                if (status == null || status.Stacks <= 0)
                    continue;
                var def = StatusCatalog.Get(status.StatusId);
                if (!StatusRules.IsBuffDefinition(def))
                    continue;
                toSteal.Add((status.StatusId, status.Stacks, status.RemainingTurns));
            }

            foreach (var (id, stacks, turns) in toSteal)
            {
                StatusRules.RemoveAllStatus(victim, id, events);
                StatusRules.ApplyStatus(state, actor, id, stacks, turns, events);
            }

            if (toSteal.Count > 0)
            {
                events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                    $"{actor.DisplayName} 偷取了 {victim.DisplayName} 的增益")
                {
                    CombatantId = actor.Id,
                    TargetId = victim.Id,
                    Amount = toSteal.Count
                });
            }
        }

        static int CountDebuffStacks(CombatantState target)
        {
            if (target == null)
                return 0;
            var total = 0;
            foreach (var status in target.Statuses)
            {
                if (status.Stacks <= 0)
                    continue;
                var def = StatusCatalog.Get(status.StatusId);
                if (def == null)
                    continue;
                // 视为减益：跳伤（中毒/灼烧）、减速、虚弱、破损、易伤、减伤对自身无意义但属负面修饰
                if (def.TurnStartDamagePerStack > 0
                    || def.TurnEndDamagePerStack > 0
                    || def.SpeedModifierPerStack < 0
                    || def.OutgoingDamageReductionFlatPerStack > 0
                    || def.BlockGainReductionPercentPerStack > 0
                    || def.IncomingDamagePercentPerStack > 0)
                {
                    total += status.Stacks;
                }
            }
            return total;
        }

        /// <summary>将消耗堆中已使用的消耗牌祛除 exhaust 后洗回抽牌堆。</summary>
        static void RecycleExhaustedCardsToDraw(
            BattleState state,
            TeamSide team,
            List<BattleEvent> events,
            BattleRng rng)
        {
            var exhaust = state.GetExhaustPile(team);
            var draw = state.GetDrawPile(team);
            var moved = 0;
            for (var i = exhaust.Count - 1; i >= 0; i--)
            {
                var card = exhaust[i];
                if (card == null)
                    continue;

                // 使用过的消耗牌：在消耗堆中；兼容旧逻辑误进弃牌堆且仍带 exhaust 的牌
                card.Keywords.Remove("exhaust");
                card.IsUsable = true;
                exhaust.RemoveAt(i);
                draw.Add(card);
                moved++;
            }

            // 兼容：若此前消耗牌误留在弃牌堆且仍带关键词，一并回收
            var discard = state.GetDiscardPile(team);
            for (var i = discard.Count - 1; i >= 0; i--)
            {
                var card = discard[i];
                if (card == null || card.Keywords == null || !card.Keywords.Contains("exhaust"))
                    continue;
                card.Keywords.Remove("exhaust");
                card.IsUsable = true;
                discard.RemoveAt(i);
                draw.Add(card);
                moved++;
            }

            if (moved <= 0)
                return;

            if (rng != null)
                DeckRules.ShuffleDrawPile(state, team, rng, events);

            events.Add(new BattleEvent(BattleEventKind.CardDrawn,
                $"神圣轮回：将 {moved} 张消耗牌洗回抽牌堆并祛除消耗")
            {
                Amount = moved
            });
        }
    }
}
