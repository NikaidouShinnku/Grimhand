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
            var cc = BuildEnemy(20, 10);

            EnemyFloorScaling.Apply(cc, 1, null);

            Assert.AreEqual(20, cc.MaxHp);
            Assert.AreEqual(0, cc.BaseAttack);
            Assert.AreEqual(10, cc.DeckTemplates[0].Actions[0].Value);
        }

        [Test]
        public void Floor5_ScalesHpAndCardValues()
        {
            var cc = BuildEnemy(20, 10);

            EnemyFloorScaling.Apply(cc, 5, null);

            Assert.AreEqual(24, cc.MaxHp);
            Assert.AreEqual(0, cc.BaseAttack);
            Assert.AreEqual(11, cc.DeckTemplates[0].Actions[0].Value);
            Assert.IsFalse(cc.DeckTemplates[0].Actions[0].ScaleWithAttack);
        }

        static CombatantConfig BuildEnemy(int hp, int damage)
        {
            var cc = new CombatantConfig
            {
                Team = TeamSide.Enemy,
                MaxHp = hp,
                BaseAttack = 4,
                BaseDefense = 1
            };
            cc.DeckTemplates.Add(new CardTemplate
            {
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Value = damage,
                        ScaleWithAttack = true
                    }
                }
            });
            return cc;
        }
    }
}
