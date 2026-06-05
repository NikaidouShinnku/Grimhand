using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CharacterProgressionTests
    {
        [Test]
        public void XpRequiredForLevel_MatchesDesignTable()
        {
            Assert.AreEqual(0, CharacterProgression.XpRequiredForLevel(1));
            Assert.AreEqual(8, CharacterProgression.XpRequiredForLevel(2));
            Assert.AreEqual(11, CharacterProgression.XpRequiredForLevel(3));
            Assert.AreEqual(62, CharacterProgression.XpRequiredForLevel(20));
        }

        [Test]
        public void AddXp_LevelsUpAndCarriesRemainder()
        {
            var result = CharacterProgression.AddXp(1, 0, 19);
            Assert.AreEqual(2, result.Level);
            Assert.AreEqual(11, result.Xp);
            Assert.AreEqual(1, result.LevelsGained);
        }

        [Test]
        public void WarriorStats_GrowWithLevel()
        {
            var lv1 = CharacterProgression.GetStatsForCharacter("char_knight", 1);
            var lv5 = CharacterProgression.GetStatsForCharacter("char_knight", 5);
            Assert.Greater(lv5.MaxHp, lv1.MaxHp);
            Assert.Greater(lv5.BaseAttack, lv1.BaseAttack);
        }
    }
}
