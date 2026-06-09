using Grimhand.Battle.Consumables;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ConsumableRewardPoolTests
    {
        static readonly string[] EventOnlyIds =
        {
            ConsumableIds.SpringBottle,
            ConsumableIds.MirrorShard,
            ConsumableIds.ScrollPage
        };

        [Test]
        public void EventOnlyConsumables_AreExcludedFromRewardPool()
        {
            foreach (var id in EventOnlyIds)
                Assert.IsFalse(ConsumableDatabase.CanAppearInRewardPool(id), id);
        }

        [Test]
        public void StandardConsumables_RemainInRewardPool()
        {
            Assert.IsTrue(ConsumableDatabase.CanAppearInRewardPool(ConsumableIds.SmallHealingPotion));
            Assert.IsTrue(ConsumableDatabase.CanAppearInRewardPool(ConsumableIds.SmokeBomb));
        }

        [Test]
        public void ChestAndVictoryRewards_NeverRollEventOnlyConsumables()
        {
            var config = BuildConfig();
            config.TreasureConsumableChancePercent = 100;
            config.RelicDropChancePercent = 100;
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });

            for (var seed = 0; seed < 200; seed++)
            {
                var rng = new BattleRng(seed);
                var chest = ExpeditionRewardRoller.RollChestReward(config, run, rng);
                if (chest.HasConsumable)
                    AssertEventOnlyExcluded(chest.ConsumableId, $"chest seed {seed}");

                var victory = ExpeditionRewardRoller.RollVictoryRewards(config, run, rng);
                if (victory.HasConsumable)
                    AssertEventOnlyExcluded(victory.ConsumableId, $"victory seed {seed}");
            }
        }

        [Test]
        public void Shop_NeverOffersEventOnlyConsumables()
        {
            var config = BuildConfig();
            var run = new ExpeditionRunState();
            run.Party.Add(new PartyMemberSnapshot { CharacterDefinitionId = "char_knight" });
            var shop = new ExpeditionShopState();

            for (var seed = 0; seed < 100; seed++)
            {
                ExpeditionShopRoller.OpenShop(shop, config, run, new BattleRng(seed));
                foreach (var offer in shop.Offers)
                {
                    if (offer.Kind != ShopOfferKind.Consumable)
                        continue;

                    AssertEventOnlyExcluded(offer.ConsumableId, $"shop seed {seed}");
                }
            }
        }

        static void AssertEventOnlyExcluded(string consumableId, string context)
        {
            foreach (var blocked in EventOnlyIds)
            {
                if (blocked == consumableId)
                    Assert.Fail($"{context}: rolled event-only {consumableId}");
            }
        }

        static ExpeditionConfig BuildConfig()
        {
            var config = new ExpeditionConfig();
            config.CombatEncounters.Add(new Battle.Model.BattleConfig());
            return config;
        }
    }
}
