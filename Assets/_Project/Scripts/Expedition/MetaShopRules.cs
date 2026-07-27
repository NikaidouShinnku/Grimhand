using System.Collections.Generic;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition
{
    public sealed class MetaShopPendingPack
    {
        public string PackId { get; set; } = "";
        public int PricePaid { get; set; }
        public List<CardPackChoice> Choices { get; } = new();
    }

    /// <summary>局外商店：扣 AccountGold、开包 roll、卡牌入库、收藏上限升级。</summary>
    public static class MetaShopRules
    {
        public static bool TryBuyPack(
            ref int accountGold,
            CampCollectionState collection,
            int collectionCapacity,
            ExpeditionConfig config,
            IReadOnlyList<string> characterIds,
            string packId,
            BattleRng rng,
            out MetaShopPendingPack pending,
            out string message)
        {
            pending = null;
            message = "";

            if (config == null || characterIds == null || characterIds.Count == 0)
            {
                message = "商店数据未就绪。";
                return false;
            }

            if (!CardPackIds.IsValid(packId))
            {
                message = "无效卡包。";
                return false;
            }

            if (CampCollectionRules.BlocksShopCardPack(collection, collectionCapacity))
            {
                var count = collection?.Count ?? 0;
                message = $"军营收藏已满（{count}/{collectionCapacity}），请先整理后再购买。";
                return false;
            }

            var price = MetaShopCatalog.GetPrice(packId);
            if (price <= 0)
            {
                message = "商品未配置。";
                return false;
            }

            if (accountGold < price)
            {
                message = $"局外金币不足（需要 {price}，当前 {accountGold}）。";
                return false;
            }

            accountGold -= price;

            var choices = CardPackRoller.RollMetaChoices(packId, config, characterIds, rng);
            if (choices.Count == 0)
            {
                accountGold += price;
                message = "卡池为空，无法开包。";
                return false;
            }

            pending = new MetaShopPendingPack
            {
                PackId = packId,
                PricePaid = price
            };
            pending.Choices.AddRange(choices);
            message = $"已购买{CardPackIds.GetDisplayName(packId)}，请过目并收下卡牌。";
            return true;
        }

        public static bool TryBuyCollectionCapacityUpgrade(
            ref int accountGold,
            ref int collectionCapacity,
            out string message)
        {
            message = "";
            if (collectionCapacity >= CampCollectionState.MaxCapacity)
            {
                message = $"军营收藏上限已达最大值（{CampCollectionState.MaxCapacity}）。";
                return false;
            }

            var price = MetaShopCatalog.CollectionCapacityUpgradePrice;
            if (accountGold < price)
            {
                message = $"局外金币不足（需要 {price}，当前 {accountGold}）。";
                return false;
            }

            accountGold -= price;
            collectionCapacity++;
            message = $"军营收藏上限已提升至 {collectionCapacity}/{CampCollectionState.MaxCapacity}。";
            return true;
        }

        public static bool TryCollectAllCards(
            CampCollectionState collection,
            int collectionCapacity,
            MetaShopPendingPack pending,
            out string message)
        {
            message = "";
            if (collection == null || pending == null || pending.Choices.Count == 0)
            {
                message = "无效操作。";
                return false;
            }

            var added = 0;
            foreach (var choice in pending.Choices)
            {
                if (choice?.Template == null || string.IsNullOrEmpty(choice.Template.DefinitionId))
                    continue;

                collection.TryAddEntry(choice.Template.DefinitionId);
                added++;
            }

            if (added == 0)
            {
                message = "卡牌数据无效。";
                return false;
            }

            message = $"已收下 {added} 张卡牌（收藏 {collection.Count}/{collectionCapacity}）。";
            return true;
        }
    }
}
