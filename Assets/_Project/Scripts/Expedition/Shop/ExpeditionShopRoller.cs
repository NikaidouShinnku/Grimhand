using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition.Model;

namespace Grimhand.Expedition.Shop
{
    public static class ExpeditionShopRoller
    {
        static readonly ShopOfferKind[] DefaultLayout =
        {
            ShopOfferKind.Card,
            ShopOfferKind.Card,
            ShopOfferKind.Card,
            ShopOfferKind.Consumable,
            ShopOfferKind.Consumable,
            ShopOfferKind.Relic
        };

        public static void OpenShop(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            shop.Clear();
            RollStock(shop, config, run, rng, DefaultLayout);
        }

        public static void RefreshStock(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng)
        {
            shop.Offers.Clear();
            RollStock(shop, config, run, rng, DefaultLayout);
            shop.RefreshCount++;
        }

        static void RollStock(
            ExpeditionShopState shop,
            ExpeditionConfig config,
            ExpeditionRunState run,
            BattleRng rng,
            ShopOfferKind[] layout)
        {
            var kinds = (ShopOfferKind[])layout.Clone();
            ShuffleKinds(kinds, rng);

            foreach (var kind in kinds)
            {
                var offer = kind switch
                {
                    ShopOfferKind.Card => RollCardOffer(config, run, rng),
                    ShopOfferKind.Consumable => RollConsumableOffer(rng),
                    ShopOfferKind.Relic => RollRelicOffer(run, rng),
                    _ => null
                };

                if (offer != null)
                    shop.Offers.Add(offer);
            }

            while (shop.Offers.Count < ExpeditionShopState.SlotCount)
            {
                var fallback = RollConsumableOffer(rng) ?? RollCardOffer(config, run, rng);
                if (fallback == null)
                    break;

                shop.Offers.Add(fallback);
            }
        }

        static ShopOffer RollCardOffer(ExpeditionConfig config, ExpeditionRunState run, BattleRng rng)
        {
            var templates = CollectShopCardTemplates(config);
            if (templates.Count == 0 || run.Party == null || run.Party.Count == 0)
                return null;

            var member = run.Party[rng.NextIndex(run.Party.Count)];
            CardTemplate picked = null;

            for (var attempt = 0; attempt < 16; attempt++)
            {
                var candidate = templates[rng.NextIndex(templates.Count)];
                if (IsDuplicateOwned(candidate, run))
                    continue;

                picked = candidate;
                break;
            }

            picked ??= templates[rng.NextIndex(templates.Count)];
            var rarity = CardRarityTable.GetOrDefault(picked.DefinitionId);

            return new ShopOffer
            {
                Kind = ShopOfferKind.Card,
                CardDefinitionId = picked.DefinitionId,
                CardDisplayName = picked.DisplayName,
                CardOwnerCharacterId = string.IsNullOrEmpty(picked.OwnerCharacterId)
                    ? member.CharacterDefinitionId
                    : picked.OwnerCharacterId,
                CardRarity = rarity,
                Price = ExpeditionShopPricing.RollCardPrice(rarity, rng)
            };
        }

        static ShopOffer RollConsumableOffer(BattleRng rng)
        {
            var pool = new List<string>();
            ConsumableDatabase.CollectRewardPoolIds(pool);

            if (pool.Count == 0)
                return null;

            var id = pool[rng.NextIndex(pool.Count)];
            ConsumableDatabase.TryGet(id, out var def);

            return new ShopOffer
            {
                Kind = ShopOfferKind.Consumable,
                ConsumableId = id,
                ConsumableDisplayName = def?.DisplayName ?? id,
                Price = ExpeditionShopPricing.RollConsumablePrice(rng)
            };
        }

        static ShopOffer RollRelicOffer(ExpeditionRunState run, BattleRng rng)
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
                return RollConsumableOffer(rng);

            var picked = pool[rng.NextIndex(pool.Count)];
            return new ShopOffer
            {
                Kind = ShopOfferKind.Relic,
                RelicId = picked.Id,
                RelicDisplayName = picked.DisplayName,
                RelicRarity = picked.Rarity,
                Price = ExpeditionShopPricing.RollRelicPrice(picked.Rarity, rng)
            };
        }

        static List<CardTemplate> CollectShopCardTemplates(ExpeditionConfig config) =>
            ExpeditionCardPool.CollectPlayerCardTemplates(config);

        static bool IsDuplicateOwned(CardTemplate candidate, ExpeditionRunState run)
        {
            foreach (var member in run.Party)
            {
                foreach (var owned in member.BonusCards)
                {
                    if (owned.DefinitionId == candidate.DefinitionId)
                        return true;
                }
            }

            return false;
        }

        static void ShuffleKinds(ShopOfferKind[] kinds, BattleRng rng)
        {
            for (var i = kinds.Length - 1; i > 0; i--)
            {
                var j = rng.NextIndex(i + 1);
                (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
            }
        }
    }
}
