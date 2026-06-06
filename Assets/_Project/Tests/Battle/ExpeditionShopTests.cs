using Grimhand.Battle.Model;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class ExpeditionShopTests
    {
        [Test]
        public void EnterShop_RollsSixOffers()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            EnterShop(engine);

            Assert.AreEqual(ExpeditionPhase.ShopVisit, engine.Run.Phase);
            Assert.AreEqual(ExpeditionShopState.SlotCount, engine.Run.Shop.Offers.Count);
            Assert.AreEqual(ExpeditionShopState.BaseRefreshCost, engine.Run.Shop.NextRefreshCost);
        }

        [Test]
        public void BuyOffer_DeductsGoldAndMarksSold()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            EnterShop(engine);
            engine.Run.Gold = 500;

            var offer = engine.Run.Shop.Offers[0];
            var price = offer.Price;
            Assert.IsTrue(engine.TryBuyShopOffer(0));
            Assert.IsTrue(offer.Sold);
            Assert.AreEqual(500 - price, engine.Run.Gold);
        }

        [Test]
        public void RefreshShop_DoublesCostAndRerollsAllOffers()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            EnterShop(engine);
            engine.Run.Gold = 200;

            Assert.IsTrue(engine.TryRefreshShop());
            Assert.AreEqual(40, engine.Run.Shop.NextRefreshCost);
            Assert.AreEqual(200 - ExpeditionShopState.BaseRefreshCost, engine.Run.Gold);
            Assert.AreEqual(1, engine.Run.Shop.RefreshCount);
            Assert.IsFalse(engine.Run.Shop.Offers[0].Sold);
        }

        [Test]
        public void LeaveShop_AdvancesToRouteSelect()
        {
            var engine = new ExpeditionEngine(BuildConfig());
            engine.StartRun();
            EnterShop(engine);

            Assert.IsTrue(engine.TryLeaveShop());
            Assert.AreEqual(ExpeditionPhase.RouteSelect, engine.Run.Phase);
            Assert.IsFalse(engine.Run.Shop.IsOpen);
        }

        static void EnterShop(ExpeditionEngine engine)
        {
            engine.Run.PendingRoutes.Clear();
            engine.Run.PendingRoutes.Add(new ExpeditionRouteOption
            {
                NodeType = ExpeditionNodeType.Shop,
                LayerNumber = 1,
                MapOptionIndex = 0,
                DisplayName = "商人",
                Description = "商店"
            });
            engine.TrySelectRoute(0);
        }

        static ExpeditionConfig BuildConfig()
        {
            var config = new ExpeditionConfig
            {
                RunSeed = 42,
                ChapterLayerCount = 3,
                TargetBattleCount = 2
            };

            var encounter = new BattleConfig();
            var player = new CombatantConfig
            {
                Team = TeamSide.Player,
                CharacterDefinitionId = "char_knight",
                DisplayName = "骑士",
                MaxHp = 40
            };
            player.DeckTemplates.Add(CardTemplate.Create(
                "Card_k_strike",
                "打击",
                "char_knight",
                1,
                CardType.Attack));
            player.DeckTemplates.Add(CardTemplate.Create(
                "Card_k_slash",
                "斩击",
                "char_knight",
                2,
                CardType.Attack));
            encounter.Combatants.Add(player);
            encounter.Combatants.Add(new CombatantConfig
            {
                Team = TeamSide.Enemy,
                CharacterDefinitionId = "char_goblin",
                DisplayName = "哥布林",
                MaxHp = 20
            });
            config.CombatEncounters.Add(encounter);
            return config;
        }
    }
}
