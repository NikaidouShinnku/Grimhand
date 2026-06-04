using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Expedition.Model;

namespace Grimhand.Presentation.Battle
{
    public static class BattleUiFormatters
    {
        public static string SlotLabel(FormationSlot slot)
        {
            switch (slot)
            {
                case FormationSlot.Front: return "前排";
                case FormationSlot.Middle: return "中排";
                case FormationSlot.Back: return "后排";
                default: return slot.ToString();
            }
        }

        public static string ShortOwner(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return "?";
            if (ownerId.StartsWith("char_"))
                return ownerId.Substring(5);
            return ownerId;
        }

        public static string FormatUnitLine(CombatantState unit)
        {
            var status = FormatStatusList(unit);
            var core = $"{unit.DisplayName}\nHP {unit.Hp}/{unit.MaxHp}  攻{unit.Attack}  速{StatusRules.GetEffectiveSpeed(unit)}";
            return string.IsNullOrEmpty(status) ? core : core + "\n" + status;
        }

        public static string FormatStatusList(CombatantState unit)
        {
            if (unit.Statuses.Count == 0)
                return "";

            var sb = new StringBuilder();
            for (var i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (i > 0) sb.Append(' ');
                sb.Append($"{s.StatusId}x{s.Stacks}");
            }

            return sb.ToString();
        }

        public static string DescribeReach(CardInstanceState card)
        {
            foreach (var action in card.Actions)
            {
                if (action.Type == EffectActionType.DealDamage)
                    return DescribeReachLabel(action);
            }

            return "";
        }

        public static string BuildCardStatsLine(BattleState state, PlanningDraft draft, CardInstanceState card)
        {
            if (card == null)
                return "";

            var ownerId = PositionRules.GetOwnerCombatantId(state, card);
            var owner = ownerId != null ? state.GetCombatant(ownerId) : null;

            var lines = new List<string>();
            foreach (var action in card.Actions)
            {
                var line = DescribeActionLine(action, owner);
                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            var body = string.Join("\n", lines);

            var assignedId = draft?.GetAssignedTarget(card.InstanceId);
            if (!string.IsNullOrEmpty(assignedId))
            {
                var assigned = state.GetCombatant(assignedId);
                if (assigned != null)
                    body += $"\n→{assigned.DisplayName}";
            }

            return body;
        }

        static string DescribeActionLine(EffectActionSpec action, CombatantState owner)
        {
            var prefix = action.Condition != ReactionConditionType.None ? "受击后 " : "";

            switch (action.Type)
            {
                case EffectActionType.DealDamage:
                    return DescribeDamageLine(action, owner, prefix);
                case EffectActionType.GainBlock:
                    return prefix + DescribeBlockLine(action, owner);
                case EffectActionType.GainBlockFromLastDamagePercent:
                    return prefix + $"护甲+{action.Value}%所受伤害";
                case EffectActionType.ReflectLastDamageToAttacker:
                    return prefix + $"反射{action.Value}%伤害";
                case EffectActionType.ApplyStatus:
                    return prefix + DescribeStatusLine(action);
                case EffectActionType.Heal:
                    return prefix + $"治疗{action.Value}";
                case EffectActionType.DrawCardsNextTurn:
                    return prefix + $"下回合抽{action.Value}张";
                case EffectActionType.DrawCards:
                    return prefix + $"抽{action.Value}张";
                case EffectActionType.RemoveStatus:
                    return prefix + "清除状态";
                case EffectActionType.SwapPositionWithFrontAlly:
                    return prefix + "与前排队友换位";
                default:
                    return "";
            }
        }

        static string DescribeDamageLine(EffectActionSpec action, CombatantState owner, string prefix)
        {
            var dmg = CardPowerRules.ComputeActionValue(action, owner);
            var parts = new List<string> { $"伤害{dmg}" };

            var reach = DescribeReachLabel(action);
            if (!string.IsNullOrEmpty(reach))
                parts.Add(reach);

            if (action.SplashBehindTarget)
                parts.Add($"贯通{action.SplashPowerPercent}%");

            if (action.BackRowPowerPercent > 0 && action.BackRowPowerPercent < 100)
                parts.Add($"后排{action.BackRowPowerPercent}%");

            var slotTarget = DescribeSlotTarget(action.Target);
            if (!string.IsNullOrEmpty(slotTarget))
                parts.Add(slotTarget);

            return prefix + string.Join(" · ", parts);
        }

        static string DescribeBlockLine(EffectActionSpec action, CombatantState owner)
        {
            var block = CardPowerRules.ComputeActionValue(action, owner);
            return $"护甲{block}";
        }

        static string DescribeStatusLine(EffectActionSpec action)
        {
            var def = StatusCatalog.Get(action.StatusId);
            var name = def?.DisplayName ?? action.StatusId;
            var parts = new List<string> { $"{name}{action.Stacks}层" };

            var duration = action.Duration >= 0 ? action.Duration : def?.DefaultDuration ?? -1;
            if (duration > 0 && def?.DurationKind == StatusDurationKind.Turns)
                parts.Add($"{duration}回合");

            var slotTarget = DescribeSlotTarget(action.Target);
            if (!string.IsNullOrEmpty(slotTarget))
                parts.Add(slotTarget);

            return string.Join(" · ", parts);
        }

        static string DescribeReachLabel(EffectActionSpec action)
        {
            switch (action.Reach)
            {
                case TargetReach.FrontAndMiddle: return "前中";
                case TargetReach.BackOnly: return "后排";
                case TargetReach.Any: return "全排";
                default: return "";
            }
        }

        static string DescribeSlotTarget(EffectTarget target)
        {
            switch (target)
            {
                case EffectTarget.EnemyFrontSlot: return "敌前排";
                case EffectTarget.EnemyMiddleSlot: return "敌中排";
                case EffectTarget.EnemyBackSlot: return "敌后排";
                case EffectTarget.AllyFrontSlot: return "友前排";
                case EffectTarget.AllyMiddleSlot: return "友中排";
                case EffectTarget.AllyBackSlot: return "友后排";
                default: return "";
            }
        }

        public static string FormatStatusListDisplay(CombatantState unit)
        {
            if (unit == null || unit.Statuses.Count == 0)
                return "";

            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < unit.Statuses.Count; i++)
            {
                var s = unit.Statuses[i];
                if (i > 0) sb.Append(' ');
                var def = Grimhand.Battle.Status.StatusCatalog.Get(s.StatusId);
                var name = def?.DisplayName ?? s.StatusId;
                sb.Append(name).Append('×').Append(s.Stacks);
            }

            return sb.ToString();
        }

        public static string BuildSelectionBadge(
            BattleState state,
            PlanningDraft draft,
            CardInstanceState card,
            IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            if (draft == null || card == null)
                return "";

            if (draft.AwaitingTargetCardId == card.InstanceId)
                return "?";

            if (!draft.IsSelected(card.InstanceId))
                return "";

            var global = IndexOfResolutionStep(resolutionSteps, card.InstanceId);
            if (global <= 0)
                return "";

            var playerOrder = CollectPlayerCardIds(state, resolutionSteps);
            TryGetOwnerResolveOrder(state, playerOrder, card, out var ownerOrder, out var ownerTotal);
            return ownerTotal > 1 ? $"#{global} [{ownerOrder}/{ownerTotal}]" : $"#{global}";
        }

        public static List<string> BuildActionOrderSummary(
            BattleState state,
            PlanningDraft draft,
            IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            var lines = new List<string>();
            if (state == null || resolutionSteps == null)
                return lines;

            var playerOrder = CollectPlayerCardIds(state, resolutionSteps);

            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var step = resolutionSteps[i];
                var card = state.GetCard(step.CardInstanceId);
                if (card == null)
                    continue;

                var global = i + 1;
                var owner = state.GetCombatant(step.CombatantId);
                var ownerName = owner?.DisplayName;
                if (string.IsNullOrEmpty(ownerName))
                    ownerName = ShortOwner(card.OwnerCharacterId);

                if (owner != null && owner.Team == TeamSide.Enemy)
                {
                    if (IsEnemyIntentHidden(state, step.CardInstanceId))
                    {
                        lines.Add($"#{global} ? ({ownerName})");
                        continue;
                    }

                    var effect = CardPowerRules.DescribeCardEffect(card, owner, false);
                    lines.Add($"#{global} {ownerName} · {card.DisplayName} 费{card.Cost} {effect}");
                    continue;
                }

                TryGetOwnerResolveOrder(state, playerOrder, card, out var ownerOrder, out var ownerTotal);

                var targetNote = "";
                var assignedId = draft?.GetAssignedTarget(step.CardInstanceId);
                if (!string.IsNullOrEmpty(assignedId))
                {
                    var assigned = state.GetCombatant(assignedId);
                    if (assigned != null)
                        targetNote = $" → {assigned.DisplayName}";
                }

                var ownerOrderNote = ownerTotal > 1 ? $" [{ownerOrder}/{ownerTotal}]" : "";
                lines.Add($"#{global} {ownerName} · {card.DisplayName}{ownerOrderNote}{targetNote}");
            }

            return lines;
        }

        public static List<string> BuildSelectedQueueSummary(
            BattleState state,
            PlanningDraft draft,
            IReadOnlyList<int> playerResolveOrder)
        {
            var lines = new List<string>();
            if (draft == null || playerResolveOrder == null)
                return lines;

            for (var i = 0; i < playerResolveOrder.Count; i++)
            {
                var id = playerResolveOrder[i];
                var card = state.GetCard(id);
                if (card == null)
                    continue;

                var global = i + 1;
                TryGetOwnerResolveOrder(state, playerResolveOrder, card, out var ownerOrder, out var ownerTotal);

                var ownerCombatantId = PositionRules.GetOwnerCombatantId(state, card);
                var ownerName = ownerCombatantId != null
                    ? state.GetCombatant(ownerCombatantId)?.DisplayName
                    : null;
                if (string.IsNullOrEmpty(ownerName))
                    ownerName = ShortOwner(card.OwnerCharacterId);

                var targetNote = "";
                var assignedId = draft.GetAssignedTarget(id);
                if (!string.IsNullOrEmpty(assignedId))
                {
                    var assigned = state.GetCombatant(assignedId);
                    if (assigned != null)
                        targetNote = $" → {assigned.DisplayName}";
                }

                var ownerOrderNote = ownerTotal > 1 ? $" [{ownerOrder}/{ownerTotal}]" : "";
                lines.Add($"#{global} {ownerName} · {card.DisplayName}{ownerOrderNote}{targetNote}");
            }

            return lines;
        }

        static List<int> CollectPlayerCardIds(BattleState state, IReadOnlyList<ResolutionStep> resolutionSteps)
        {
            var result = new List<int>();
            if (state == null || resolutionSteps == null)
                return result;

            for (var i = 0; i < resolutionSteps.Count; i++)
            {
                var owner = state.GetCombatant(resolutionSteps[i].CombatantId);
                if (owner != null && owner.Team == TeamSide.Player)
                    result.Add(resolutionSteps[i].CardInstanceId);
            }

            return result;
        }

        static int IndexOfResolutionStep(IReadOnlyList<ResolutionStep> steps, int cardInstanceId)
        {
            if (steps == null)
                return 0;

            for (var i = 0; i < steps.Count; i++)
            {
                if (steps[i].CardInstanceId == cardInstanceId)
                    return i + 1;
            }

            return 0;
        }

        static bool IsEnemyIntentHidden(BattleState state, int cardInstanceId)
        {
            foreach (var intent in state.EnemyIntents)
            {
                if (intent.CardInstanceId == cardInstanceId)
                    return intent.IsHidden;
            }

            return false;
        }

        static int IndexOfCard(IReadOnlyList<int> order, int instanceId)
        {
            if (order == null)
                return -1;

            for (var i = 0; i < order.Count; i++)
            {
                if (order[i] == instanceId)
                    return i;
            }

            return -1;
        }

        static void TryGetOwnerResolveOrder(
            BattleState state,
            IReadOnlyList<int> playerResolveOrder,
            CardInstanceState card,
            out int order,
            out int totalForOwner)
        {
            order = 0;
            totalForOwner = 0;
            if (card == null || playerResolveOrder == null)
                return;

            for (var i = 0; i < playerResolveOrder.Count; i++)
            {
                var other = state.GetCard(playerResolveOrder[i]);
                if (other == null || other.OwnerCharacterId != card.OwnerCharacterId)
                    continue;

                totalForOwner++;
                if (other.InstanceId == card.InstanceId)
                    order = totalForOwner;
            }
        }

        public static string FormatPartyHpLine(IReadOnlyList<PartyMemberSnapshot> party)
        {
            if (party == null || party.Count == 0)
                return "队伍 HP: —";

            var sb = new StringBuilder();
            for (var i = 0; i < party.Count; i++)
            {
                var m = party[i];
                if (i > 0) sb.Append("  ");
                sb.Append(m.DisplayName).Append(' ').Append(m.Hp).Append('/').Append(m.MaxHp);
            }

            return sb.ToString();
        }

        public static string DescribeNodeType(ExpeditionNodeType type)
        {
            switch (type)
            {
                case ExpeditionNodeType.Combat: return "普通战斗";
                case ExpeditionNodeType.Elite: return "精英";
                case ExpeditionNodeType.Event: return "事件";
                case ExpeditionNodeType.Shop: return "商店";
                default: return type.ToString();
            }
        }
    }
}
