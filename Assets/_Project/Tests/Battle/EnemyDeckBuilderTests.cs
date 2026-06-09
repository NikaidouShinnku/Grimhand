using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class EnemyDeckBuilderTests
    {
        [Test]
        public void ShuffleFixedDeck_ChangesOrderButKeepsComposition()
        {
            var deck = new List<CardTemplate>
            {
                Template("a"),
                Template("a"),
                Template("b"),
                Template("c")
            };
            var snapshot = string.Join(",", deck.ConvertAll(t => t.DefinitionId));

            var rng = new BattleRng(42);
            EnemyDeckBuilder.ShuffleFixedDeck(deck, rng);

            Assert.AreEqual(4, deck.Count);
            var counts = new Dictionary<string, int>();
            foreach (var card in deck)
            {
                counts.TryGetValue(card.DefinitionId, out var n);
                counts[card.DefinitionId] = n + 1;
            }

            Assert.AreEqual(2, counts["a"]);
            Assert.AreEqual(1, counts["b"]);
            Assert.AreEqual(1, counts["c"]);

            var shuffled = string.Join(",", deck.ConvertAll(t => t.DefinitionId));
            Assert.AreNotEqual(snapshot, shuffled);
        }

        static CardTemplate Template(string id) =>
            new()
            {
                DefinitionId = id,
                DisplayName = id,
                OwnerCharacterId = "char_test"
            };
    }
}
