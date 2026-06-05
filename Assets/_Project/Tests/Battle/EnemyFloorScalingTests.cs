using Grimhand.Battle.Model;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class EnemyFloorScalingTests
    {
        [Test]
        public void Floor1_DoesNotScale()
        {
            var cc = new CombatantConfig
            {
                Team = TeamSide.Enemy,
                MaxHp = 20,
                BaseAttack = 4,
                BaseDefense = 1
            };

            EnemyFloorScaling.Apply(cc, 1, null);

            Assert.AreEqual(20, cc.MaxHp);
            Assert.AreEqual(4, cc.BaseAttack);
            Assert.AreEqual(1, cc.BaseDefense);
        }

        [Test]
        public void Floor5_ScalesByDesignRates()
        {
            var cc = new CombatantConfig
            {
                Team = TeamSide.Enemy,
                MaxHp = 20,
                BaseAttack = 4,
                BaseDefense = 1
            };

            EnemyFloorScaling.Apply(cc, 5, null);

            Assert.AreEqual(40, cc.MaxHp);
            Assert.AreEqual(6, cc.BaseAttack);
            Assert.AreEqual(1, cc.BaseDefense);
        }
    }
}
