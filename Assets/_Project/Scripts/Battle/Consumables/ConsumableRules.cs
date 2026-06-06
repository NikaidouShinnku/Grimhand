using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
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
                    if (state.LastPlayerAttackCard == null)
                        break;

                    var actor = state.GetCombatant(state.LastPlayerAttackActorId);
                    if (actor == null)
                        break;

                    return CardRules.GetValidTargetCandidates(state, state.LastPlayerAttackCard, actor);
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

                    DamageRules.ApplyHeal(state, target, definition.Value, events, healer);
                    break;
                }
                case ConsumableEffectKind.HealTeam:
                    foreach (var ally in state.GetTeam(TeamSide.Player))
                    {
                        if (ally.IsAlive)
                            DamageRules.ApplyHeal(state, ally, definition.Value, events, ally);
                    }

                    break;
                case ConsumableEffectKind.BattleAttackBonus:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    target.Attack += definition.Value;
                    events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{target.DisplayName} ATK+{definition.Value}（消耗品）")
                    {
                        CombatantId = target.Id
                    });
                    break;
                }
                case ConsumableEffectKind.BattleDefenseBonus:
                {
                    var target = state.GetCombatant(targetCombatantId);
                    if (target == null || !target.IsAlive)
                    {
                        errorMessage = "目标无效。";
                        return false;
                    }

                    target.Defense += definition.Value;
                    events.Add(new BattleEvent(BattleEventKind.StatusApplied, $"{target.DisplayName} DEF+{definition.Value}（消耗品）")
                    {
                        CombatantId = target.Id
                    });
                    break;
                }
                case ConsumableEffectKind.EnergyThisTurn:
                    state.EnergyCurrent = System.Math.Min(state.EnergyMax, state.EnergyCurrent + definition.Value);
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
            var source = state.LastPlayerAttackCard;
            var actor = state.GetCombatant(state.LastPlayerAttackActorId);
            if (source == null || actor == null)
            {
                errorMessage = "尚无已打出的攻击牌可复制。";
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

        static CardInstanceState CloneCard(CardInstanceState source)
        {
            var clone = new CardInstanceState
            {
                InstanceId = source.InstanceId > 0 ? -source.InstanceId : -1,
                DefinitionId = source.DefinitionId,
                OwnerCharacterId = source.OwnerCharacterId,
                Cost = source.Cost,
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
                    LifestealPercent = action.LifestealPercent,
                    OnKillHealAmount = action.OnKillHealAmount
                });
            }

            return clone;
        }
    }
}
