using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    /// <summary>远征引擎可读写的局外档案片段（扣局外金 / 写收藏）。由 Persistence 的 PlayerProfileState 实现。</summary>
    public interface IExpeditionMetaProfile
    {
        int AccountGold { get; set; }
        CampCollectionState Collection { get; }
        int CollectionCapacity { get; }
    }

    /// <summary>祭坛「刻印」：把局内牌带出到军营收藏。</summary>
    public static class CardEngravingRules
    {
        public const string LockKeyword = CardRules.EngravingLockKeyword;

        public enum EngraveMethod
        {
            AccountGold = 0,
            BattleProgress = 1,
            SacrificeSameRarity = 2
        }

        public static int GetAccountGoldCost(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => 100,
            CardRarity.Rare => 200,
            CardRarity.SuperRare => 500,
            CardRarity.Epic => 2000,
            CardRarity.Legendary => 10000,
            _ => 100
        };

        public static int GetBattlesRequired(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => 1,
            CardRarity.Rare => 2,
            CardRarity.SuperRare => 3,
            CardRarity.Epic => 4,
            CardRarity.Legendary => 5,
            _ => 1
        };

        public const int SacrificeCountRequired = 2;

        public static string DescribeRarity(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => "白色",
            CardRarity.Rare => "绿色",
            CardRarity.SuperRare => "蓝色",
            CardRarity.Epic => "紫色",
            CardRarity.Legendary => "传说",
            _ => "普通"
        };

        public static CardRarity ResolveRarity(CardTemplate template) =>
            template == null
                ? CardRarity.Common
                : CardRarityTable.GetOrDefault(template.DefinitionId);

        public static bool IsDeckInstanceEngraved(ExpeditionRunState run, string deckInstanceId) =>
            run != null
            && !string.IsNullOrEmpty(deckInstanceId)
            && run.EngravedDeckInstanceIds.Contains(deckInstanceId);

        public static bool IsAltarExtractedCard(ExpeditionRunState run, string deckInstanceId) =>
            run != null
            && !string.IsNullOrEmpty(deckInstanceId)
            && run.AltarExtractedDeckInstanceIds != null
            && run.AltarExtractedDeckInstanceIds.Contains(deckInstanceId);

        public static bool IsDeckInstancePending(ExpeditionRunState run, string deckInstanceId)
        {
            if (run?.PendingCardEngravings == null || string.IsNullOrEmpty(deckInstanceId))
                return false;

            foreach (var pending in run.PendingCardEngravings)
            {
                if (pending != null && pending.DeckInstanceId == deckInstanceId)
                    return true;
            }

            return false;
        }

        public static PendingCardEngraving FindPending(ExpeditionRunState run, string deckInstanceId)
        {
            if (run?.PendingCardEngravings == null || string.IsNullOrEmpty(deckInstanceId))
                return null;

            foreach (var pending in run.PendingCardEngravings)
            {
                if (pending != null && pending.DeckInstanceId == deckInstanceId)
                    return pending;
            }

            return null;
        }

        public static bool HasPendingBattleEngrave(ExpeditionRunState run) =>
            run?.PendingCardEngravings != null && run.PendingCardEngravings.Count > 0;

        public static bool IsAltarEngraveSlotUsed(ExpeditionRunState run) =>
            run?.CardAltar != null && run.CardAltar.EngraveSlotUsed;

        /// <summary>本祭坛是否还能发起新的刻印（未用过本祭坛名额，且无进行中的战斗刻印）。</summary>
        public static bool CanOfferEngraving(ExpeditionRunState run, out string reason)
        {
            reason = "";
            if (HasPendingBattleEngrave(run))
            {
                reason = "战斗刻印进行中，完成前无法在祭坛刻印其他卡牌。";
                return false;
            }

            if (IsAltarEngraveSlotUsed(run))
            {
                reason = "本祭坛已刻印过一次，无法再次刻印。";
                return false;
            }

            return true;
        }

        public static void MarkAltarEngraveSlotUsed(ExpeditionRunState run)
        {
            if (run?.CardAltar != null)
                run.CardAltar.EngraveSlotUsed = true;
        }

        public static bool CanSelectAsEngraveTarget(
            ExpeditionRunState run,
            ExpeditionRunDeckMutations.DeckCardEntry entry)
        {
            if (run == null || entry?.Template == null)
                return false;
            if (string.IsNullOrEmpty(entry.Template.DeckInstanceId))
                return false;
            if (!CanOfferEngraving(run, out _))
                return false;
            if (IsDeckInstanceEngraved(run, entry.Template.DeckInstanceId))
                return false;
            if (IsAltarExtractedCard(run, entry.Template.DeckInstanceId))
                return false;
            if (IsDeckInstancePending(run, entry.Template.DeckInstanceId))
                return false;
            return true;
        }

        public static bool TryCompleteEngraveToCollection(
            ExpeditionRunState run,
            IExpeditionMetaProfile profile,
            string definitionId,
            string deckInstanceId,
            out string message)
        {
            message = "";
            if (run == null || profile?.Collection == null || string.IsNullOrEmpty(definitionId))
            {
                message = "无法刻印：数据无效。";
                return false;
            }

            profile.Collection.TryAddEntry(definitionId, isEngraved: true);
            if (!string.IsNullOrEmpty(deckInstanceId))
                run.EngravedDeckInstanceIds.Add(deckInstanceId);

            ClearPending(run, deckInstanceId);
            RemoveLockKeywordFromParty(run, deckInstanceId);
            message = "刻印成功：卡牌已写入军营收藏。";
            return true;
        }

        public static bool TryStartBattleProgressEngrave(
            ExpeditionRunState run,
            ExpeditionRunDeckMutations.DeckCardEntry entry,
            CardRarity rarity,
            out string message)
        {
            message = "";
            if (!CanOfferEngraving(run, out message))
                return false;
            if (!CanSelectAsEngraveTarget(run, entry))
            {
                message = "该卡无法刻印（已刻印、收藏提取或刻印进行中）。";
                return false;
            }

            var instanceId = entry.Template.DeckInstanceId;
            var required = GetBattlesRequired(rarity);
            run.PendingCardEngravings.Add(new PendingCardEngraving
            {
                MemberId = entry.MemberId ?? "",
                DeckInstanceId = instanceId,
                DefinitionId = entry.Template.DefinitionId ?? "",
                DisplayName = entry.Template.DisplayName ?? "",
                BattlesRequired = required,
                BattlesCompleted = 0
            });

            EnsureLockKeywordOnTemplate(entry.Template);
            ApplyLockKeywordToPartyCard(run, instanceId);
            MarkAltarEngraveSlotUsed(run);

            message = $"已开始战斗刻印：需再胜利 {required} 场战斗（普通/精英/Boss 均计）。期间该牌无法使用；完成前无法在祭坛刻印其他卡。";
            return true;
        }

        public static void OnBattleVictory(ExpeditionRunState run, IExpeditionMetaProfile profile, List<string> completedMessages)
        {
            if (run?.PendingCardEngravings == null || run.PendingCardEngravings.Count == 0)
                return;

            for (var i = run.PendingCardEngravings.Count - 1; i >= 0; i--)
            {
                var pending = run.PendingCardEngravings[i];
                if (pending == null)
                {
                    run.PendingCardEngravings.RemoveAt(i);
                    continue;
                }

                pending.BattlesCompleted = Math.Min(pending.BattlesRequired, pending.BattlesCompleted + 1);
                if (pending.BattlesCompleted < pending.BattlesRequired)
                    continue;

                if (profile != null
                    && TryCompleteEngraveToCollection(
                        run,
                        profile,
                        pending.DefinitionId,
                        pending.DeckInstanceId,
                        out var msg))
                {
                    completedMessages?.Add($"{pending.DisplayName}：{msg}");
                }
                else
                {
                    ClearPending(run, pending.DeckInstanceId);
                    RemoveLockKeywordFromParty(run, pending.DeckInstanceId);
                }
            }
        }

        static void ClearPending(ExpeditionRunState run, string deckInstanceId)
        {
            if (run?.PendingCardEngravings == null || string.IsNullOrEmpty(deckInstanceId))
                return;

            for (var i = run.PendingCardEngravings.Count - 1; i >= 0; i--)
            {
                if (run.PendingCardEngravings[i]?.DeckInstanceId == deckInstanceId)
                    run.PendingCardEngravings.RemoveAt(i);
            }
        }

        static void EnsureLockKeywordOnTemplate(CardTemplate template)
        {
            if (template?.Keywords == null)
                return;
            if (!template.Keywords.Contains(LockKeyword))
                template.Keywords.Add(LockKeyword);
        }

        static void ApplyLockKeywordToPartyCard(ExpeditionRunState run, string deckInstanceId)
        {
            if (run?.Party == null || string.IsNullOrEmpty(deckInstanceId))
                return;

            foreach (var member in run.Party)
            {
                if (member?.BonusCards == null)
                    continue;
                foreach (var card in member.BonusCards)
                {
                    if (card == null || card.DeckInstanceId != deckInstanceId)
                        continue;
                    EnsureLockKeywordOnTemplate(card);
                }
            }
        }

        static void RemoveLockKeywordFromParty(ExpeditionRunState run, string deckInstanceId)
        {
            if (run?.Party == null || string.IsNullOrEmpty(deckInstanceId))
                return;

            foreach (var member in run.Party)
            {
                if (member?.BonusCards == null)
                    continue;
                foreach (var card in member.BonusCards)
                {
                    if (card?.DeckInstanceId != deckInstanceId || card.Keywords == null)
                        continue;
                    card.Keywords.Remove(LockKeyword);
                }
            }
        }
    }
}
