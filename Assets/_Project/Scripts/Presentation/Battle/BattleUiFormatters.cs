using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
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
            var core = $"{unit.DisplayName}\nHP {unit.Hp}/{unit.MaxHp}  甲{unit.Block}  攻{unit.Attack}  速{StatusRules.GetEffectiveSpeed(unit)}";
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
                if (action.SplashBehindTarget)
                    return "贯通";
            }

            var reach = TargetReachRules.GetPickReach(card);
            switch (reach)
            {
                case TargetReach.FrontAndMiddle: return "前中";
                case TargetReach.BackOnly: return "后排";
                case TargetReach.Any: return "全排";
                default: return "";
            }
        }

        public static string BuildCardStatsLine(BattleState state, PlanningDraft draft, CardInstanceState card)
        {
            var ownerId = PositionRules.GetOwnerCombatantId(state, card);
            var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
            var power = CardPowerRules.GetEffectivePower(card, owner);
            var powerLabel = CardPowerRules.GetPowerLabel(card);
            var reach = DescribeReach(card);
            var reachPart = string.IsNullOrEmpty(reach) ? "" : $" · {reach}";

            var assignedId = draft?.GetAssignedTarget(card.InstanceId);
            var targetPart = "";
            if (!string.IsNullOrEmpty(assignedId))
            {
                var assigned = state.GetCombatant(assignedId);
                if (assigned != null)
                    targetPart = $" →{assigned.DisplayName}";
            }

            return $"{powerLabel}{power}{reachPart}{targetPart}";
        }

        public static string BuildSelectionBadge(PlanningDraft draft, CardInstanceState card)
        {
            if (draft == null || !draft.IsSelected(card.InstanceId))
                return "";

            var global = draft.GetGlobalPlayOrder(card.InstanceId);
            if (!draft.TryGetOwnerPlayOrder(card.InstanceId, out var ownerOrder, out var ownerTotal))
                return $"#{global}";

            return ownerTotal > 1 ? $"#{global} [{ownerOrder}/{ownerTotal}]" : $"#{global}";
        }

        public static List<string> BuildSelectedQueueSummary(BattleState state, PlanningDraft draft)
        {
            var lines = new List<string>();
            if (draft == null)
                return lines;

            foreach (var id in draft.SelectedQueue)
            {
                var card = state.GetCard(id);
                if (card == null)
                    continue;

                var global = draft.GetGlobalPlayOrder(id);
                draft.TryGetOwnerPlayOrder(id, out var ownerOrder, out var ownerTotal);

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
