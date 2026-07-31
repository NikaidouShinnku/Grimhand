using System;
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
        ShowMessage,
        /// <summary>从已选角色卡组中一次多选最多 3 张可升级卡，确认后各升 1 级。</summary>
        PickThreeCardsUpgrade
    }

    public sealed class ExpeditionEventInteractionStep
    {
        public ExpeditionEventStepKind Kind { get; set; }
        public int PercentHpDelta { get; set; }
        /// <summary>扣血时按最大 HP 百分比计算（如古老神殿「-10% 最大HP」）。</summary>
        public bool PercentFromMaxHp { get; set; }
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
        public System.Action<ExpeditionRunState> DeferredRunAction { get; set; }

        /// <summary>选牌步骤确认后暂存，在随后的 ShowMessage 结束时执行。</summary>
        public ExpeditionEventStepKind PendingApplyKind { get; set; }
        public bool HasPendingCardAction { get; set; }
        public string PendingPrimaryCardKey { get; set; } = "";
        public string PendingSecondaryCardKey { get; set; } = "";
        public int PendingUpgradeBonus { get; set; }
        /// <summary>多选升级等：待应用的卡牌键列表。</summary>
        public List<string> PendingCardKeys { get; } = new();
    }

    /// <summary>卡牌在选牌 UI 中的唯一键：deckInstanceId（memberId|guid）。</summary>
    public static class ExpeditionDeckCardKey
    {
        public static string GenerateInstanceId(string memberId) =>
            $"{memberId}|{Guid.NewGuid():N}";

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
