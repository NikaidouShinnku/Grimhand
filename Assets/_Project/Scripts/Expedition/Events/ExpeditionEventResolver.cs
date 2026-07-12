using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Core;
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
        public List<ExpeditionEventInteractionStep> InteractionSteps { get; } = new();
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
            else
            {
                run.UsedEventIds.Add(eventId);
            }

            return outcome;
        }

        public static ExpeditionEventOutcome ResolveShrineChoice(
            ExpeditionRunState run,
            string shrineId,
            int choiceIndex,
            BattleRng rng)
        {
            if (!ExpeditionShrineCatalog.TryGet(shrineId, out var definition) ||
                choiceIndex < 0 ||
                choiceIndex >= definition.Choices.Count)
            {
                return new ExpeditionEventOutcome { Message = "你离开了祭坛。" };
            }

            var choice = definition.Choices[choiceIndex];
            if (choice.Label == "C" ||
                (choice.Description != null && choice.Description.Contains("安全离开")))
            {
                return new ExpeditionEventOutcome { Message = "你离开了祭坛。" };
            }

            return shrineId switch
            {
                ExpeditionShrineIds.Blood => ResolveBloodShrine(run, choiceIndex, rng),
                ExpeditionShrineIds.Knowledge => ResolveKnowledgeShrine(run, choiceIndex, rng),
                ExpeditionShrineIds.Soul => ResolveSoulShrine(run, choiceIndex, rng),
                ExpeditionShrineIds.Chaos => ResolveChaosShrine(run, choiceIndex, rng),
                _ => new ExpeditionEventOutcome { Message = "祭坛无回应。" }
            };
        }

        static ExpeditionEventOutcome ResolveBloodShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => WithMemberHpPercent(run, -50, "血祭完成：该角色 ATK+3。", () =>
                    run.Modifiers.TeamAttackBonus += 3),
                1 => ResolveBloodShrineRelic(run, rng),
                _ => new ExpeditionEventOutcome { Message = "你离开血之祭坛。" }
            };
        }

        static ExpeditionEventOutcome ResolveBloodShrineRelic(ExpeditionRunState run, BattleRng rng)
        {
            WithTeamHpPercentSilent(run, -15);
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return WithPickup(
                ExpeditionRewardPickupFactory.Relic(relicId, "血之祭坛"),
                "集体血祭完成，获得随机遗物。");
        }

        static ExpeditionEventOutcome ResolveKnowledgeShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => new ExpeditionEventOutcome { Message = "献祭卡牌后，从三张蓝卡中选一张（占位）。" },
                1 => new ExpeditionEventOutcome { Message = "献祭高级卡牌，获得一张紫色卡牌（占位）。" },
                _ => new ExpeditionEventOutcome { Message = "你离开知识祭坛。" }
            };
        }

        static ExpeditionEventOutcome ResolveSoulShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            return choice switch
            {
                0 => GrantRelicPickup(run, rng, "献祭遗物，获得更高阶遗物。", "灵魂祭坛"),
                1 => new ExpeditionEventOutcome { Message = "等级重置，该角色获得 ATK+3 DEF+2 HP+15（占位）。" },
                _ => new ExpeditionEventOutcome { Message = "你离开灵魂祭坛。" }
            };
        }

        static ExpeditionEventOutcome ResolveChaosShrine(ExpeditionRunState run, int choice, BattleRng rng)
        {
            if (choice >= 1)
                return new ExpeditionEventOutcome { Message = "你拒绝混沌仪式。" };

            var cost = rng.NextIndex(3);
            if (cost == 0) WithTeamHpPercentSilent(run, -20);
            else if (cost == 1) run.Gold = System.Math.Max(0, run.Gold - 20);
            else RemoveRandomBonusCard(run, rng);

            if (rng.NextIndex(100) < 10)
                return new ExpeditionEventOutcome { Message = "混沌仪式毫无收获。" };

            run.Modifiers.TeamAttackBonus += 1;

            if (rng.NextIndex(100) < 50)
            {
                var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
                return WithPickup(
                    ExpeditionRewardPickupFactory.Relic(relicId, "混沌祭坛"),
                    "混沌祭坛给出了未知的回报。");
            }

            return new ExpeditionEventOutcome { Message = "混沌祭坛给出了未知的回报。" };
        }

        static ExpeditionEventOutcome WithPickup(
            ExpeditionRewardPickup pickup,
            string message,
            System.Action extra = null)
        {
            extra?.Invoke();
            if (pickup == null || !pickup.HasAnyReward)
                return new ExpeditionEventOutcome { Message = message };

            pickup.Kind = RewardPickupKind.EventOrShrine;
            if (string.IsNullOrEmpty(pickup.HeaderText))
                pickup.HeaderText = "拾取奖励";

            return new ExpeditionEventOutcome
            {
                Message = message,
                PendingRewardPickup = pickup
            };
        }

        static ExpeditionEventOutcome GrantRelicPickup(
            ExpeditionRunState run,
            BattleRng rng,
            string message,
            string header)
        {
            var relicId = ExpeditionRewardPickupFactory.RollRelicId(run, rng);
            return WithPickup(ExpeditionRewardPickupFactory.Relic(relicId, header), message);
        }

        static void RemoveRandomBonusCard(ExpeditionRunState run, BattleRng rng)
        {
            foreach (var member in run.Party)
            {
                if (member.BonusCards.Count == 0)
                    continue;

                member.BonusCards.RemoveAt(rng.NextIndex(member.BonusCards.Count));
                return;
            }
        }

        static ExpeditionEventOutcome WithTeamHpPercent(ExpeditionRunState run, int percent, string message, System.Action extra = null)
        {
            foreach (var member in run.Party)
            {
                var loss = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                if (percent < 0)
                    member.Hp = System.Math.Max(0, member.Hp - loss);
                else
                    member.Hp = System.Math.Min(member.MaxHp, member.Hp + loss);
            }

            extra?.Invoke();
            return new ExpeditionEventOutcome { Message = message };
        }

        static void WithTeamHpPercentSilent(ExpeditionRunState run, int percent)
        {
            foreach (var member in run.Party)
            {
                var loss = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                member.Hp = System.Math.Max(0, member.Hp - loss);
            }
        }

        static ExpeditionEventOutcome WithMemberHpPercent(ExpeditionRunState run, int percent, string message, System.Action extra)
        {
            if (run.Party.Count > 0)
            {
                var member = run.Party[0];
                var delta = System.Math.Max(1, member.Hp * System.Math.Abs(percent) / 100);
                member.Hp = percent < 0
                    ? System.Math.Max(0, member.Hp - delta)
                    : System.Math.Min(member.MaxHp, member.Hp + delta);
            }

            extra?.Invoke();
            return new ExpeditionEventOutcome { Message = message };
        }

    }
}
