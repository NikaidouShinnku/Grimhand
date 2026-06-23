using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    public static class BattleEventLogFormatter
    {
        public static string DescribeCard(BattleEvent e, BattleState state)
        {
            if (e.CardInstanceId <= 0)
                return "卡牌";

            var card = state?.GetCard(e.CardInstanceId);
            return card != null ? card.DisplayName : $"#{e.CardInstanceId}";
        }

        public static string Format(BattleEvent e, BattleState state)
        {
            switch (e.Kind)
            {
                case BattleEventKind.PhaseChanged:
                    return $"→ 阶段: {DescribePhase(e.Phase)}";
                case BattleEventKind.EnergyChanged:
                    return $"能量 {e.EnergyRemaining}/{e.EnergyMax}";
                case BattleEventKind.CardSelectedForPlay:
                    return $"预选 {DescribeCard(e, state)}（能量 {e.EnergyRemaining}/{e.EnergyMax}）";
                case BattleEventKind.CardDeselectedFromPlay:
                    return $"取消预选 {DescribeCard(e, state)}";
                case BattleEventKind.DeckPolluted:
                    return $"牌堆污染 · {CombatantName(state, e.CombatantId)}（{e.Amount} 张）";
                case BattleEventKind.TargetSelectionRequired:
                    return $"选择目标 · {e.Message}";
                case BattleEventKind.EnemyIntentPrepared:
                    return $"敌方意图 · {e.Message}";
                case BattleEventKind.StatusApplied:
                    return $"施加状态 · {e.Message} x{e.Amount} → {CombatantName(state, e.CombatantId)}";
                case BattleEventKind.StatusRemoved:
                case BattleEventKind.StatusExpired:
                    return $"状态结束 · {e.Message} → {CombatantName(state, e.CombatantId)}";
                case BattleEventKind.StatusTickDamage:
                    return $"状态伤害 {e.Amount} · {e.Message} → {CombatantName(state, e.CombatantId)}";
                case BattleEventKind.ReactionTriggered:
                case BattleEventKind.ParryTriggered:
                    return $"应对 · {CombatantName(state, e.CombatantId)}：{e.Message}";
                case BattleEventKind.PositionSwapped:
                    return $"换位 · {CombatantName(state, e.CombatantId)} ↔ {CombatantName(state, e.TargetId)}";
                case BattleEventKind.PlanCommitted:
                    return "【玩家】确认出牌";
                case BattleEventKind.TurnSkipped:
                    return "【玩家】空过回合";
                case BattleEventKind.CardResolvedStarted:
                    return $"【结算】{SidePrefix(state, e.CombatantId)}{DescribeCard(e, state)} · {e.Message}";
                case BattleEventKind.CardResolvedEnded:
                    return $"【结束】{SidePrefix(state, e.CombatantId)}{DescribeCard(e, state)}";
                case BattleEventKind.DamageApplied:
                    return FormatDamage(e, state);
                case BattleEventKind.BlockGained:
                    return $"护甲 +{e.Amount} · {CombatantName(state, e.CombatantId)}";
                case BattleEventKind.IronWallConverted:
                    return $"下一张攻击 +{e.Amount} · {CombatantName(state, e.CombatantId)}（铁壁转化）";
                case BattleEventKind.HealApplied:
                    return $"治疗 +{e.Amount} · {CombatantName(state, e.CombatantId)}";
                case BattleEventKind.CharacterRevived:
                    return $"复活 · {CombatantName(state, e.CombatantId)}（{e.Amount} HP）";
                case BattleEventKind.CharacterDied:
                    return $"阵亡 · {CombatantName(state, e.CombatantId)}";
                case BattleEventKind.CardDrawn:
                    return $"抽牌 · {DescribeCard(e, state)}";
                case BattleEventKind.CardDiscarded:
                    return $"弃牌 · {DescribeCard(e, state)}";
                case BattleEventKind.DeckShuffled:
                    return "洗切抽牌堆";
                case BattleEventKind.BattleEnded:
                    return $"战斗结束 · {e.Outcome}";
                default:
                    return string.IsNullOrEmpty(e.Message) ? e.Kind.ToString() : e.Message;
            }
        }

        static string FormatDamage(BattleEvent e, BattleState state)
        {
            var actor = CombatantName(state, e.CombatantId);
            var target = CombatantName(state, e.TargetId);
            var block = e.BlockedAmount > 0 ? $"（格挡 {e.BlockedAmount}）" : "";
            if (!string.IsNullOrEmpty(e.Message))
                return $"伤害 {e.Amount}{block} · {e.Message}";

            return $"伤害 {e.Amount}{block} · {actor} → {target}";
        }

        static string CombatantName(BattleState state, string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return "?";

            var c = state?.GetCombatant(combatantId);
            if (c == null)
                return combatantId;

            var slot = PositionRules.GetEffectiveSlot(state, c);
            return $"{c.DisplayName}（{BattleUiFormatters.SlotLabel(slot)}）";
        }

        static string SidePrefix(BattleState state, string combatantId)
        {
            var c = state?.GetCombatant(combatantId);
            if (c == null)
                return "";

            return c.Team == TeamSide.Player ? "我方 " : "敌方 ";
        }

        static string DescribePhase(TurnPhase phase) =>
            phase switch
            {
                TurnPhase.Draw => "抽牌",
                TurnPhase.Planning => "规划",
                TurnPhase.SpeedResolve => "速度结算",
                TurnPhase.EndOfTurn => "回合结束",
                TurnPhase.BattleEnd => "战斗结束",
                _ => phase.ToString()
            };
    }
}
