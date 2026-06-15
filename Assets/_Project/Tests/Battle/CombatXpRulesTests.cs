using Grimhand.Core;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CombatXpRulesTests
    {
        [Test]
        public void CaveNormalCombat_RollsEightToTen()
        {
            var rng = new BattleRng(42);
            for (var i = 0; i < 20; i++)
            {
                var xp = CombatXpRules.Roll(rng, floor: 3, isElite: false, isBoss: false);
                Assert.That(xp, Is.InRange(8, 10));
            }
        }

        [Test]
        public void FloorTwentyBoss_GrantsTwentyFive()
        {
            var xp = CombatXpRules.Roll(new BattleRng(1), floor: 20, isElite: false, isBoss: true);
            Assert.AreEqual(25, xp);
        }
    }
}
