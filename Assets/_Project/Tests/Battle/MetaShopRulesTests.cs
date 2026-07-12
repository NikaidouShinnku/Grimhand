using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class MetaShopRulesTests
    {
        [Test]
        public void TryBuyPack_DeductsGoldAndRollsChoices()
        {
            var config = BuildConfig();
            var collection = new CampCollectionState();
            var accountGold = 1000;
            var characterIds = new List<string> { "char_knight" };

            Assert.IsTrue(MetaShopRules.TryBuyPack(
                ref accountGold,
                collection,
                CampCollectionState.DefaultCapacity,
                config,
                characterIds,
                CardPackIds.Common,
                new BattleRng(42),
                out var pending,
                out var message));

            Assert.AreEqual(500, accountGold);
            Assert.NotNull(pending);
            Assert.AreEqual(CardPackIds.Common, pending.PackId);
            Assert.AreEqual(CardPackRoller.ChoiceCount, pending.Choices.Count);
            Assert.IsFalse(string.IsNullOrEmpty(message));
        }

        [Test]
        public void TryBuyPack_InsufficientGold_Fails()
        {
            var config = BuildConfig();
            var collection = new CampCollectionState();
            var accountGold = 100;

            Assert.IsFalse(MetaShopRules.TryBuyPack(
                ref accountGold,
                collection,
                CampCollectionState.DefaultCapacity,
                config,
                new List<string> { "char_knight" },
                CardPackIds.Common,
                new BattleRng(1),
                out _,
                out var message));

            Assert.AreEqual(100, accountGold);
            Assert.IsTrue(message.Contains("不足"));
        }

        [Test]
        public void TryCollectAllCards_AddsEveryChoiceToCollection()
        {
            var collection = new CampCollectionState();
            var pending = new MetaShopPendingPack
            {
                PackId = CardPackIds.Common
            };
            pending.Choices.Add(new CardPackChoice
            {
                Template = new CardTemplate { DefinitionId = "card_a", DisplayName = "A" }
            });
            pending.Choices.Add(new CardPackChoice
            {
                Template = new CardTemplate { DefinitionId = "card_b", DisplayName = "B" }
            });
            pending.Choices.Add(new CardPackChoice
            {
                Template = new CardTemplate { DefinitionId = "card_c", DisplayName = "C" }
            });

            Assert.IsTrue(MetaShopRules.TryCollectAllCards(
                collection,
                CampCollectionState.DefaultCapacity,
                pending,
                out var message));

            Assert.AreEqual(3, collection.Count);
            Assert.IsFalse(string.IsNullOrEmpty(message));
        }

        [Test]
        public void TryRemoveCollectionEntry_RemovesEntry()
        {
            var collection = new CampCollectionState();
            collection.TryAddEntry("test_card");

            Assert.IsTrue(CampCollectionRules.TryRemoveCollectionEntry(collection, 0, out var message));
            Assert.AreEqual(0, collection.Count);
            Assert.IsFalse(string.IsNullOrEmpty(message));
        }

        [Test]
        public void RollMetaChoices_UsesSameChoiceCountAsInRun()
        {
            var config = BuildConfig();
            var choices = CardPackRoller.RollMetaChoices(
                CardPackIds.Advanced,
                config,
                new List<string> { "char_knight", "char_ranger" },
                new BattleRng(7));

            Assert.AreEqual(CardPackRoller.ChoiceCount, choices.Count);
        }

        static ExpeditionConfig BuildConfig()
        {
            var config = new ExpeditionConfig();
            config.PlayerCardCatalog.Add(new CardTemplate
            {
                DefinitionId = "knight_attack",
                DisplayName = "骑士斩击",
                OwnerCharacterId = "char_knight",
                CardType = CardType.Attack
            });
            CardRarityTable.Register("knight_attack", CardRarity.Common);
            return config;
        }
    }
}
