using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;

namespace Grimhand.Battle.Consumables
{
    public static class ConsumableRules
    {
        public static bool NeedsTarget(ConsumableDefinition definition) =>
            definition != null &&
            definition.TargetKind is ConsumableTargetKind.SingleAlly
                or ConsumableTargetKind.SingleEnemy
                or ConsumableTargetKind.MirrorAttack;

        public static List<CombatantState> GetValidTargets(BattleState state, ConsumableDefinition definition)
        {
            var result = new List<CombatantState>();
            if (state == null || definition == null)
                return result;

            switch (definition.TargetKind)
            {
                case ConsumableTargetKind.SingleAlly:
                    foreach (var unit in state.GetTeam(TeamSide.Player))
                    {
                        if (unit.IsAlive)
                            result.Add(unit);
                    }

                    break;
                case ConsumableTargetKind.SingleEnemy:
                    foreach (var unit in state.GetTeam(TeamSide.Enemy))
                    {
                        if (unit.IsAlive)
                            result.Add(unit);
                    }

                    break;
                case ConsumableTargetKind.MirrorAttack:
                    if (!CanUseMirrorShard(state, out _))
                        break;

                    var mirrorActor = GetMirrorAttackActor(state);
                    var mirrorCard = GetMirrorAttackSourceCard(state);
                    if (mirrorActor == null || mirrorCard == null)
                        break;

                    return CardRules.GetValidTargetCandidates(state, mirrorCard, mirrorActor);
            }

            return result;
        }

        public static bool TryApply(
            BattleState state,
            ConsumableDefinition definition,
            string targetCombatantId,
            List<BattleEvent> events,
            BattleRng rng,
            out string errorMessage)
        {
            errorMessage = "";
            if (state == null || definition == null)
            {
                errorMessage = "无效消耗品。";
                return false;
            }

            if (definition.EffectKind == ConsumableEffectKind.MirrorLastAttack
                && !CanUseMirrorShard(state, out errorMessage))
                return false;

            if (NeedsTarget(definition))
            {
                var valid = GetValidTargets(state, definition);
                var ok = false;
                foreach (var unit in valid)
                {
                    if (unit.Id == targetCombatantId)
                    {
                        ok = true;
                        break;
                    }
                }

                if (!ok)
                {
                    errorMessage = "无效目标。";
                    return false;
                }
            }

            switch (definition.EffectKind)
            {
                case ConsumableEffectKind.HealSingle:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    CombatantState healer = null;
                    foreach (var ally in state.GetTeam(TeamSide.Player))
                    {
                        healer = ally;
                        break;
                    }

                    var healAmount = System.Math.Max(1, (int)System.Math.Round(target.MaxHp * (definition.Value / 100f)));
                    DamageRules.ApplyHeal(state, target, healAmount, events, healer);
                    break;
                }
                case ConsumableEffectKind.HealTeam:
                    foreach (var ally in state.GetTeam(TeamSide.Player))
                    {
                        if (!ally.IsAlive)
                            continue;

                        var healAmount = System.Math.Max(1, (int)System.Math.Round(ally.MaxHp * (definition.Value / 100f)));
                        DamageRules.ApplyHeal(state, ally, healAmount, events, ally);
                    }

                    break;
                case ConsumableEffectKind.TurnAttackBonusPercent:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    // 使用关键词「增伤」状态：每层 +1% 伤害，持续 1 回合
                    StatusRules.ApplyStatus(
                        state,
                        target,
                        StatusCatalog.AttackUpPercent,
                        definition.Value,
                        1,
                        events);
                    break;
                }
                case ConsumableEffectKind.TurnDefenseBonusPercent:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    target.TurnDefenseBonusPercent = definition.Value;
                    RelicBattleRules.RefreshDerivedStats(state, target, state.Config?.RunModifiers);
                    events.Add(new BattleEvent(BattleEventKind.StatusApplied,
                        $"{target.DisplayName} DEF+{definition.Value}%（本回合·消耗品）")
                    {
                        CombatantId = target.Id
                    });
                    break;
                }
                case ConsumableEffectKind.GainBlock:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    DamageRules.ApplyBlock(target, definition.Value, events, state);
                    break;
                }
                case ConsumableEffectKind.EnergyThisTurn:
                    EnergyRules.Restore(state, definition.Value);
                    events.Add(new BattleEvent(BattleEventKind.EnergyChanged, "古卷残页：能量 +2")
                    {
                        Energy = state.EnergyCurrent,
                        EnergyMax = state.EnergyMax,
                        EnergyRemaining = state.EnergyCurrent
                    });
                    break;
                case ConsumableEffectKind.DodgeAllThisTurn:
                    state.ConsumableDodgeBonusThisTurn = definition.Value / 100f;
                    events.Add(new BattleEvent(BattleEventKind.StatusApplied, "烟雾弹：全队闪避率 +50%"));
                    break;
                case ConsumableEffectKind.MirrorLastAttack:
                    return TryApplyMirrorShard(state, targetCombatantId, events, rng, out errorMessage);
                case ConsumableEffectKind.DrawCharacterCards:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    DeckRules.DrawCharacterCards(
                        state,
                        target.CharacterDefinitionId,
                        rng,
                        definition.Value,
                        events);
                    events.Add(new BattleEvent(BattleEventKind.CardDrawn,
                        $"{target.DisplayName} 专注药剂抽 {definition.Value} 张")
                    {
                        CombatantId = target.Id,
                        Amount = definition.Value
                    });
                    break;
                }
            }

            events.Add(new BattleEvent(BattleEventKind.ConsumableUsed, definition.DisplayName)
            {
                CombatantId = targetCombatantId ?? ""
            });
            return true;
        }

        static bool TryApplyMirrorShard(
            BattleState state,
            string targetCombatantId,
            List<BattleEvent> events,
            BattleRng rng,
            out string errorMessage)
        {
            errorMessage = "";
            if (!CanUseMirrorShard(state, out errorMessage))
                return false;

            var source = GetMirrorAttackSourceCard(state);
            var actor = GetMirrorAttackActor(state);
            if (source == null || actor == null)
            {
                errorMessage = "上一回合未打出可用的攻击牌。";
                return false;
            }

            var clone = CloneCard(source);
            state.ResolutionTargets[clone.InstanceId] = targetCombatantId;
            EffectActionExecutor.ExecuteAll(state, actor, clone, events, rng);
            state.ResolutionTargets.Remove(clone.InstanceId);
            events.Add(new BattleEvent(BattleEventKind.ConsumableUsed, "镜之碎片")
            {
                CombatantId = actor.Id
            });
            return true;
        }

        public static void RecordLastPlayerAttackCard(
            BattleState state,
            CombatantState actor,
            CardInstanceState card)
        {
            if (state == null || actor == null || card == null)
                return;

            if (actor.Team != TeamSide.Player || card.CardType != CardType.Attack)
                return;

            state.LastPlayerAttackActorId = actor.Id;
            state.LastPlayerAttackCard = CloneCard(card);
        }

        /// <summary>回合结束时归档本回合最后攻击牌，供下一回合镜之碎片使用。</summary>
        public static void ArchiveTurnAttackHistory(BattleState state)
        {
            if (state == null)
                return;

            state.PreviousTurnLastPlayerAttackCard = state.LastPlayerAttackCard != null
                ? CloneCard(state.LastPlayerAttackCard)
                : null;
            state.PreviousTurnLastPlayerAttackActorId = state.LastPlayerAttackActorId ?? "";
            state.LastPlayerAttackCard = null;
            state.LastPlayerAttackActorId = "";
        }

        public static CardInstanceState GetMirrorAttackSourceCard(BattleState state) =>
            state?.PreviousTurnLastPlayerAttackCard;

        public static CombatantState GetMirrorAttackActor(BattleState state)
        {
            if (state == null || string.IsNullOrEmpty(state.PreviousTurnLastPlayerAttackActorId))
                return null;

            var actor = state.GetCombatant(state.PreviousTurnLastPlayerAttackActorId);
            return actor != null && actor.IsAlive ? actor : null;
        }

        public static bool CanUseMirrorShard(BattleState state, out string errorMessage)
        {
            errorMessage = "";
            if (state == null)
            {
                errorMessage = "无效战斗。";
                return false;
            }

            if (state.TurnNumber <= 1)
            {
                errorMessage = "第一回合无法使用镜之碎片。";
                return false;
            }

            if (GetMirrorAttackSourceCard(state) == null)
            {
                errorMessage = "上一回合未打出攻击牌。";
                return false;
            }

            if (GetMirrorAttackActor(state) == null)
            {
                errorMessage = "上一张攻击牌的出牌者已无法行动。";
                return false;
            }

            return true;
        }

        static CardInstanceState CloneCard(CardInstanceState source)
        {
            var clone = new CardInstanceState
            {
                InstanceId = source.InstanceId > 0 ? -source.InstanceId : -1,
                DefinitionId = source.DefinitionId,
                OwnerCharacterId = source.OwnerCharacterId,
                Cost = source.Cost,
                BaseCost = source.BaseCost != 0 || source.Cost == 0 ? source.BaseCost : source.Cost,
                CardType = source.CardType,
                IsUsable = source.IsUsable,
                DisplayName = source.DisplayName
            };

            foreach (var keyword in source.Keywords)
                clone.Keywords.Add(keyword);

            foreach (var action in source.Actions)
            {
                clone.Actions.Add(new EffectActionSpec
                {
                    Type = action.Type,
                    Target = action.Target,
                    Value = action.Value,
                    StatusId = action.StatusId,
                    Stacks = action.Stacks,
                    Duration = action.Duration,
                    ScaleWithAttack = action.ScaleWithAttack,
                    ScaleWithDefense = action.ScaleWithDefense,
                    AttackScalePercent = action.AttackScalePercent,
                    DefenseScalePercent = action.DefenseScalePercent,
                    Condition = action.Condition,
                    Reach = action.Reach,
                    SplashBehindTarget = action.SplashBehindTarget,
                    SplashPowerPercent = action.SplashPowerPercent,
                    BackRowPowerPercent = action.BackRowPowerPercent,
                    IgnoreDefPercent = action.IgnoreDefPercent,
                    BonusIfTargetHpBelowPercent = action.BonusIfTargetHpBelowPercent,
                    BonusIfTargetHpBelowFlat = action.BonusIfTargetHpBelowFlat,
                    BonusIfTargetHitThisTurnPercent = action.BonusIfTargetHitThisTurnPercent,
                    BonusIfTargetHasStatusId = action.BonusIfTargetHasStatusId,
                    BonusIfTargetHasStatusFlat = action.BonusIfTargetHasStatusFlat,
                    LifestealPercent = action.LifestealPercent,
                    HealMaxHpPercent = action.HealMaxHpPercent,
                    OnKillHealAmount = action.OnKillHealAmount
                });
            }

            return clone;
        }
    }
}
