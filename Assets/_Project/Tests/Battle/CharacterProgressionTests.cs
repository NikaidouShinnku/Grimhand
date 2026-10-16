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
            Assert.AreEqual(3, result.Level);
            Assert.AreEqual(0, result.Xp);
            Assert.AreEqual(2, result.LevelsGained);
        }

        [Test]
        public void WarriorStats_GrowWithLevel()
        {
            var lv1 = CharacterProgression.GetStatsForCharacter("char_knight", 1);
            var lv5 = CharacterProgression.GetStatsForCharacter("char_knight", 5);
            Assert.Greater(lv5.MaxHp, lv1.MaxHp);
            Assert.AreEqual(80, lv1.MaxHp);
            Assert.AreEqual(104, lv5.MaxHp);
            Assert.AreEqual(7, lv1.Speed);
            Assert.AreEqual(5, CharacterProgression.GetStatsForCharacter("char_mage", 1).Speed);
            Assert.AreEqual(6, CharacterProgression.GetStatsForCharacter("char_ranger", 1).Speed);
        }

        // v0.93, 角色和天赋 B4:P23: all twenty levels, not only endpoints.
        [TestCase("char_knight", 80, 6, 7)]
        [TestCase("char_mage", 60, 5, 5)]
        [TestCase("char_ranger", 45, 4, 6)]
        [TestCase("char_snake_queen", 55, 5, 6)]
        [TestCase("char_lich_queen", 48, 5, 7)]
        public void CharacterStats_MatchOverviewAtEveryLevel(string id, int hp, int hpPerLevel, int speed)
        {
            for (var level = 1; level <= 20; level++)
            {
                var stats = CharacterProgression.GetStatsForCharacter(id, level);
                Assert.AreEqual(hp + hpPerLevel * (level - 1), stats.MaxHp, $"{id} level {level}");
                Assert.AreEqual(speed, stats.Speed, $"{id} level {level}");
            }
        }
    }
}
