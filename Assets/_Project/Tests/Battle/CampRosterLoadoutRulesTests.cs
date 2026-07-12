using System.Collections.Generic;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CampRosterLoadoutRulesTests
    {
        static Dictionary<string, string> BuildOwners()
        {
            return new Dictionary<string, string>
            {
                ["card_a"] = "char_knight",
                ["card_b"] = "char_knight",
                ["card_mage"] = "char_mage"
            };
        }

        [Test]
        public void TryAssignCollectionEntry_BindsUniqueInstance()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_knight" });
            CampRosterLoadoutRules.EnsureDeckStructure(roster.Members[0]);

            var collection = new CampCollectionState();
            collection.TryAddEntry("card_a");
            collection.TryAddEntry("card_a");

            var owners = BuildOwners();
            Assert.IsTrue(CampRosterLoadoutRules.TryAssignCollectionEntry(
                roster, collection, owners, 0, 0, 0, out _));
            Assert.IsTrue(CampRosterLoadoutRules.TryAssignCollectionEntry(
                roster, collection, owners, 0, 1, 1, out _));
            Assert.AreEqual("card_a", roster.Members[0].DeckCardIds[0]);
            Assert.AreEqual("card_a", roster.Members[0].DeckCardIds[1]);
            Assert.AreEqual(0, roster.Members[0].DeckCollectionEntryIndices[0]);
            Assert.AreEqual(1, roster.Members[0].DeckCollectionEntryIndices[1]);
        }

        [Test]
        public void TryAssignCollectionEntry_RejectsDuplicateInstance()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_knight" });
            CampRosterLoadoutRules.EnsureDeckStructure(roster.Members[0]);

            var collection = new CampCollectionState();
            collection.TryAddEntry("card_a");

            var owners = BuildOwners();
            Assert.IsTrue(CampRosterLoadoutRules.TryAssignCollectionEntry(
                roster, collection, owners, 0, 0, 0, out _));
            Assert.IsFalse(CampRosterLoadoutRules.TryAssignCollectionEntry(
                roster, collection, owners, 0, 1, 0, out _));
        }

        [Test]
        public void ClearSlot_ReleasesCollectionEntryForPool()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_knight" });
            CampRosterLoadoutRules.EnsureDeckStructure(roster.Members[0]);

            var collection = new CampCollectionState();
            collection.TryAddEntry("card_a");

            var owners = BuildOwners();
            CampRosterLoadoutRules.TryAssignCollectionEntry(roster, collection, owners, 0, 0, 0, out _);
            Assert.AreEqual(1, CampRosterLoadoutRules.CollectAssignedCollectionIndices(roster).Count);

            CampRosterLoadoutRules.ClearSlot(roster.Members[0], 0);
            Assert.AreEqual(0, CampRosterLoadoutRules.CollectAssignedCollectionIndices(roster).Count);
        }

        [Test]
        public void OnCollectionEntryRemoved_AdjustsHigherIndices()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_knight" });
            CampRosterLoadoutRules.EnsureDeckStructure(roster.Members[0]);

            var collection = new CampCollectionState();
            collection.TryAddEntry("card_a");
            collection.TryAddEntry("card_b");

            var owners = BuildOwners();
            CampRosterLoadoutRules.TryAssignCollectionEntry(roster, collection, owners, 0, 0, 0, out _);
            CampRosterLoadoutRules.TryAssignCollectionEntry(roster, collection, owners, 0, 1, 1, out _);

            collection.TryRemoveAt(0);
            CampRosterLoadoutRules.OnCollectionEntryRemoved(roster, 0);

            Assert.AreEqual(CampRosterLoadoutRules.EmptyCollectionIndex, roster.Members[0].DeckCollectionEntryIndices[0]);
            Assert.AreEqual(0, roster.Members[0].DeckCollectionEntryIndices[1]);
            Assert.AreEqual("card_b", roster.Members[0].DeckCardIds[1]);
        }

        [Test]
        public void EmptyCarriedDeck_AllowsExpedition_WhenThreeUniqueCharactersAssigned()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_knight" });
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_mage" });
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_ranger" });
            foreach (var member in roster.Members)
                CampRosterLoadoutRules.EnsureDeckStructure(member);

            Assert.IsTrue(roster.IsReadyForExpedition);
        }
    }
}
