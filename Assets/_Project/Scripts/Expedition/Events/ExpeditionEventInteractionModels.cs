using System.Collections.Generic;
using Grimhand.Battle.Model;

namespace Grimhand.Expedition.Events
{
    public enum ExpeditionEventStepKind
    {
        ShowTeamHpLoss,
        PickMemberHpLoss,
        PickMemberForBuff,
        PickCardRemove,
        PickCardUpgrade,
        PickTwoCardsForFusion,
        ShowMessage
    }

    public sealed class ExpeditionEventInteractionStep
    {
        public ExpeditionEventStepKind Kind { get; set; }
        public int PercentHpDelta { get; set; }
        public int FlatHpDelta { get; set; }
        public int PersonalAttackBonus { get; set; }
        public string Message { get; set; } = "";
        public string TargetCharacterId { get; set; } = "";
        public CardType RequiredFusionType { get; set; }
    }

    public sealed class ExpeditionEventInteractionState
    {
        public string EventId { get; set; } = "";
        public int ChoiceIndex { get; set; }
        public List<ExpeditionEventInteractionStep> Steps { get; } = new();
        public int StepIndex { get; set; }
        public string SelectedCharacterId { get; set; } = "";
        public string SelectedCardKey { get; set; } = "";
        public string FusionFirstCardKey { get; set; } = "";
        public CardType FusionCardType { get; set; }
        public ExpeditionEventOutcome DeferredOutcome { get; set; }

        /// <summary>选牌步骤确认后暂存，在随后的 ShowMessage 结束时执行。</summary>
        public ExpeditionEventStepKind PendingApplyKind { get; set; }
        public bool HasPendingCardAction { get; set; }
        public string PendingPrimaryCardKey { get; set; } = "";
        public string PendingSecondaryCardKey { get; set; } = "";
        public int PendingUpgradeBonus { get; set; }
    }

    /// <summary>卡牌在选牌 UI 中的唯一键：memberId|definitionId|index</summary>
    public static class ExpeditionDeckCardKey
    {
        public static string Build(string memberId, string definitionId, int index) =>
            $"{memberId}|{definitionId}|{index}";

        public static bool TryParse(string key, out string memberId, out string definitionId, out int index)
        {
            memberId = "";
            definitionId = "";
            index = 0;
            if (string.IsNullOrEmpty(key))
                return false;

            var parts = key.Split('|');
            if (parts.Length < 3)
                return false;

            memberId = parts[0];
            definitionId = parts[1];
            return int.TryParse(parts[2], out index);
        }
    }
}
