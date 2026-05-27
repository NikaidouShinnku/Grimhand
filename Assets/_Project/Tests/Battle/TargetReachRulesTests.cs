using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class TargetReachRulesTests
    {
        [Test]
        public void FrontAndMiddle_ExcludesBackRow()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            var back = new CombatantState { Slot = FormationSlot.Back, Hp = 10 };

            Assert.IsFalse(TargetReachRules.CanPickUnit(card, back));
            Assert.IsTrue(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Front));
            Assert.IsTrue(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Middle));
            Assert.IsFalse(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Back));
        }

        [Test]
        public void Any_AllowsAllSlots()
        {
            var card = DamageCard(TargetReach.Any);
            var back = new CombatantState { Slot = FormationSlot.Back, Hp = 10 };

            Assert.IsTrue(TargetReachRules.CanPickUnit(card, back));
            Assert.AreEqual(TargetReach.Any, TargetReachRules.GetPickReach(card));
        }

        [Test]
        public void BackRowPowerPercent_ReducesDamageOnBackTarget()
        {
            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                BackRowPowerPercent = 70
            };
            var back = new CombatantState { Slot = FormationSlot.Back };
            var front = new CombatantState { Slot = FormationSlot.Front };

            Assert.AreEqual(7, TargetReachRules.AdjustPowerForTarget(action, back, 10));
            Assert.AreEqual(10, TargetReachRules.AdjustPowerForTarget(action, front, 10));
        }

        [Test]
        public void GetPickReach_DefaultsToFrontAndMiddleForMeleeDamage()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            Assert.AreEqual(TargetReach.FrontAndMiddle, TargetReachRules.GetPickReach(card));
        }

        static CardInstanceState DamageCard(TargetReach reach)
        {
            var card = new CardInstanceState();
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = reach
            });
            return card;
        }
    }
}
