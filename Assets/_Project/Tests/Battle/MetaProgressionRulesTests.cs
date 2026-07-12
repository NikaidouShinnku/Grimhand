using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class MetaProgressionRulesTests
    {
        [Test]
        public void XpRequiredForLevel_MatchesOverviewTable()
        {
            Assert.AreEqual(0, MetaProgressionRules.XpRequiredForLevel(1));
            Assert.AreEqual(100, MetaProgressionRules.XpRequiredForLevel(2));
            Assert.AreEqual(200, MetaProgressionRules.XpRequiredForLevel(3));
            Assert.AreEqual(300, MetaProgressionRules.XpRequiredForLevel(4));
            Assert.AreEqual(500, MetaProgressionRules.XpRequiredForLevel(5));
            Assert.AreEqual(800, MetaProgressionRules.XpRequiredForLevel(6));
            Assert.AreEqual(1000, MetaProgressionRules.XpRequiredForLevel(7));
            Assert.AreEqual(1500, MetaProgressionRules.XpRequiredForLevel(8));
            Assert.AreEqual(2000, MetaProgressionRules.XpRequiredForLevel(9));
            Assert.AreEqual(2500, MetaProgressionRules.XpRequiredForLevel(10));
        }

        [Test]
        public void GrantOutOfRunXp_AutoLevelsWhenThresholdReached()
        {
            var progress = new CharacterMetaProgress
            {
                CharacterDefinitionId = TalentCatalog.KnightId,
                OutOfRunLevel = 1,
                OutOfRunXp = 80
            };

            MetaProgressionRules.GrantOutOfRunXp(progress, 20);

            Assert.AreEqual(2, progress.OutOfRunLevel);
            Assert.AreEqual(0, progress.OutOfRunXp);
        }

        [Test]
        public void GrantOutOfRunXp_ClampsAtMaxLevel()
        {
            var progress = new CharacterMetaProgress
            {
                CharacterDefinitionId = TalentCatalog.KnightId,
                OutOfRunLevel = 9,
                OutOfRunXp = 1990
            };

            MetaProgressionRules.GrantOutOfRunXp(progress, 100);

            Assert.AreEqual(10, progress.OutOfRunLevel);
            Assert.AreEqual(0, progress.OutOfRunXp);
        }

        [Test]
        public void NormalizeProgress_BumpsLegacyLevelZeroToOne()
        {
            var progress = new CharacterMetaProgress { OutOfRunLevel = 0, OutOfRunXp = 12 };
            MetaProgressionRules.NormalizeProgress(progress);
            Assert.AreEqual(1, progress.OutOfRunLevel);
            Assert.AreEqual(12, progress.OutOfRunXp);
        }

        [Test]
        public void TalentUnlocksFollowOutOfRunLevel()
        {
            var progress = new CharacterMetaProgress
            {
                CharacterDefinitionId = TalentCatalog.KnightId,
                OutOfRunLevel = 2
            };
            var lv1Talent = TalentCatalog.Get("talent_knight_s1_lv1");
            var lv3Talent = TalentCatalog.Get("talent_knight_s1_lv3");

            Assert.IsTrue(TalentRules.IsUnlocked(lv1Talent, progress));
            Assert.IsFalse(TalentRules.IsUnlocked(lv3Talent, progress));

            progress.OutOfRunLevel = 3;
            Assert.IsTrue(TalentRules.IsUnlocked(lv3Talent, progress));
        }
    }
}
