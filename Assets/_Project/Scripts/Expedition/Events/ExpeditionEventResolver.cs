using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Events
{
    public sealed class ExpeditionEventOutcome
    {
        public string Message { get; set; } = "";
        public bool StartsCombat { get; set; }
        public int CombatEncounterIndex { get; set; }
        public bool AdvanceNode { get; set; } = true;
        public ExpeditionRewardPickup PendingRewardPickup { get; set; }
        public string EventBattleKey { get; set; } = "";
        public System.Collections.Generic.List<ExpeditionEventInteractionStep> InteractionSteps { get; } = new();
        public ExpeditionEventOutcome DeferredOutcome { get; set; }
        public System.Action<ExpeditionRunState> DeferredRunAction { get; set; }
    }

    public static class ExpeditionEventResolver
    {
        public static ExpeditionEventOutcome ResolveChoice(
            ExpeditionRunState run,
            ExpeditionConfig config,
            string eventId,
            int choiceIndex,
            BattleRng rng)
        {
            if (!ExpeditionEventCatalog.TryGet(eventId, out var definition))
                return new ExpeditionEventOutcome { Message = "事件已结束。" };

            if (choiceIndex < 0 || choiceIndex >= definition.Choices.Count)
                return new ExpeditionEventOutcome { Message = "无效选择。" };

            var outcome = ExpeditionEventPlanner.Resolve(run, config, eventId, choiceIndex, rng);

            if (eventId == ExpeditionEventIds.SoulRift)
            {
                if (choiceIndex is 0 or 1)
                {
                    run.UsedEventIds.Add(eventId);
                    run.EventFlags.Add(ExpeditionEventRoller.SoulRiftResolvedFlag);
                }
            }
            else if (eventId == ExpeditionEventIds.MysteriousTraveler)
            {
                // 仅选项 B（接受礼物）在 PlanTravelerGift 中标记，避免本远征再出现
            }
            else
            {
                run.UsedEventIds.Add(eventId);
            }

            return outcome;
        }
    }
}
