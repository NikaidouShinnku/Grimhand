using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class EnemyDeckBuilderTests
    {
        [Test]
        public void BuildRandomDeck_PicksWithinRangeAndFillsDeck()
        {
            var pool = new List<CardTemplate>
            {
                Template("a"), Template("b"), Template("c"), Template("d")
            };
            var rng = new BattleRng(42);
            var deck = EnemyDeckBuilder.BuildRandomDeck(pool, rng, 8, 2, 4);

            Assert.AreEqual(8, deck.Count);
            var unique = new HashSet<string>();
            foreach (var card in deck)
                unique.Add(card.DefinitionId);
            Assert.GreaterOrEqual(unique.Count, 2);
            Assert.LessOrEqual(unique.Count, 4);
        }

        static CardTemplate Template(string id) =>
            new()
            {
                DefinitionId = id,
                DisplayName = id,
                OwnerCharacterId = "char_goblin",
                Cost = 1,
                CardType = CardType.Attack
            };
    }
}
