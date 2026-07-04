using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Shop
{
    public static class ExpeditionShopRoller
    {
        const int CommonPackSlot = 0;
        const int AdvancedPackSlot = 1;
        const int RelicSlotA = 2;
        const int RelicSlotB = 3;
        const int ConsumableSlotA = 4;
        const int ConsumableSlotB = 5;

        public static void OpenShop(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            shop.Clear();
            EnsureOfferSlots(shop);
            RollAllSlots(shop, config, run, rng, isRefresh: false);
        }

        public static void RefreshStock(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            EnsureOfferSlots(shop);
            RollUnsoldSlots(shop, config, run, rng);
            shop.RefreshCount++;
        }

        static void EnsureOfferSlots(ExpeditionShopState shop)
        {
            while (shop.Offers.Count < ExpeditionShopState.SlotCount)
                shop.Offers.Add(new ShopOffer());

            if (shop.Offers.Count > ExpeditionShopState.SlotCount)
                shop.Offers.RemoveRange(ExpeditionShopState.SlotCount, shop.Offers.Count - ExpeditionShopState.SlotCount);
        }

        static void RollAllSlots(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng,
            bool isRefresh)
        {
            for (var i = 0; i < ExpeditionShopState.SlotCount; i++)
                RollSlot(shop, i, config, run, rng, isRefresh);
        }

        static void RollUnsoldSlots(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            for (var i = 0; i < ExpeditionShopState.SlotCount; i++)
            {
                if (shop.Offers[i].Sold)
                    continue;

                RollSlot(shop, i, config, run, rng, isRefresh: true);
            }
        }

        static void RollSlot(
            ExpeditionShopState shop,
            int slotIndex,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng,
            bool isRefresh)
        {
            var offer = shop.Offers[slotIndex];
            offer.Sold = false;

            switch (slotIndex)
            {
                case CommonPackSlot:
                    offer.Kind = ShopOfferKind.CardPack;
                    offer.CardPackId = CardPackIds.Common;
                    offer.Price = ExpeditionShopPricing.CommonPackPrice;
                    break;
                case AdvancedPackSlot:
                    offer.Kind = ShopOfferKind.CardPack;
                    offer.CardPackId = isRefresh && rng.NextIndex(100) < 10
                        ? CardPackIds.Master
                        : CardPackIds.Advanced;
                    offer.Price = offer.CardPackId == CardPackIds.Master
                        ? ExpeditionShopPricing.MasterPackPrice
                        : ExpeditionShopPricing.AdvancedPackPrice;
                    break;
                case RelicSlotA:
                case RelicSlotB:
                    RollRelicOffer(offer, run, rng);
                    break;
                default:
                    RollConsumableOffer(offer, rng);
                    break;
            }
        }

        static void RollConsumableOffer(ShopOffer offer, BattleRng rng)
        {
            var pool = new List<string>();
            ConsumableDatabase.CollectRewardPoolIds(pool);

            if (pool.Count == 0)
            {
                offer.Kind = ShopOfferKind.Consumable;
                offer.ConsumableId = "";
                offer.ConsumableDisplayName = "";
                offer.Price = ExpeditionShopPricing.RollConsumablePrice(rng);
                return;
            }

            var id = pool[rng.NextIndex(pool.Count)];
            ConsumableDatabase.TryGet(id, out var def);

            offer.Kind = ShopOfferKind.Consumable;
            offer.ConsumableId = id;
            offer.ConsumableDisplayName = def?.DisplayName ?? id;
            offer.Price = ExpeditionShopPricing.RollConsumablePrice(rng);
        }

        static void RollRelicOffer(ShopOffer offer, ExpeditionRunState run, BattleRng rng)
        {
            var pool = new List<RelicDefinition>();
            foreach (var relic in RelicDatabase.All)
            {
                if (run.Relics.Contains(relic.Id))
                    continue;

                if (!RelicDatabase.CanAppearInRewardPool(relic, run.Party))
                    continue;

                if (relic.EvolutionOnly)
                    continue;

                pool.Add(relic);
            }

            if (pool.Count == 0)
            {
                RollConsumableOffer(offer, rng);
                return;
            }

            var picked = pool[rng.NextIndex(pool.Count)];
            offer.Kind = ShopOfferKind.Relic;
            offer.RelicId = picked.Id;
            offer.RelicDisplayName = picked.DisplayName;
            offer.RelicRarity = picked.Rarity;
            offer.Price = ExpeditionShopPricing.RollRelicPrice(rng);
        }
    }
}
