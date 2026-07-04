using Grimhand.Core;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CombatXpRulesTests
    {
        [Test]
        public void CaveNormalCombat_RollsTenToThirteen()
        {
            var rng = new BattleRng(42);
            for (var i = 0; i < 20; i++)
            {
                var xp = CombatXpRules.Roll(rng, floor: 3, isElite: false, isBoss: false);
                Assert.That(xp, Is.InRange(10, 13));
            }
        }

        [Test]
        public void CaveEliteCombat_RollsEighteenToTwentyFive()
        {
            var rng = new BattleRng(7);
            for (var i = 0; i < 20; i++)
            {
                var xp = CombatXpRules.Roll(rng, floor: 5, isElite: true, isBoss: false);
                Assert.That(xp, Is.InRange(18, 25));
            }
        }

        [Test]
        public void FloorTwentyBoss_GrantsForty()
        {
            var xp = CombatXpRules.Roll(new BattleRng(1), floor: 20, isElite: false, isBoss: true);
            Assert.AreEqual(40, xp);
        }

        [Test]
        public void CaveNormalVictory_GrantsFifteenToTwentyGold()
        {
            var rng = new BattleRng(11);
            for (var i = 0; i < 20; i++)
            {
                var gold = CombatRewardRules.RollGold(rng, floor: 3, isElite: false, isBoss: false);
                Assert.That(gold, Is.InRange(15, 20));
            }
        }
    }
}
