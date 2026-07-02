using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CampRosterValidationTests
    {
        [Test]
        public void HasUniqueCharacters_RejectsDuplicateIds()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_snake_queen" });
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_snake_queen" });
            roster.Members.Add(new CampMemberLoadout { CharacterDefinitionId = "char_lich_queen" });

            Assert.IsFalse(CampRosterValidation.HasUniqueCharacters(roster));
        }

        [Test]
        public void SwapMembers_ExchangesLoadouts()
        {
            var roster = new CampRosterState();
            roster.Members.Add(new CampMemberLoadout
            {
                CharacterDefinitionId = "char_snake_queen",
                DisplayName = "毒蛇女王"
            });
            roster.Members.Add(new CampMemberLoadout
            {
                CharacterDefinitionId = "char_lich_queen",
                DisplayName = "巫妖女王"
            });

            CampRosterValidation.SwapMembers(roster, 0, 1);

            Assert.AreEqual("char_lich_queen", roster.Members[0].CharacterDefinitionId);
            Assert.AreEqual("char_snake_queen", roster.Members[1].CharacterDefinitionId);
        }
    }
}
