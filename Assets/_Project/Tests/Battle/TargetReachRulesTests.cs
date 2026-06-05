using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class TargetReachRulesTests
    {
        [Test]
        public void FrontAndMiddle_ExcludesEffectiveBackRow()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            var state = Team(
                Unit("front", FormationSlot.Front),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            Assert.IsFalse(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("back")));
            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("front")));
            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("middle")));
            Assert.IsTrue(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Front));
            Assert.IsTrue(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Middle));
            Assert.IsFalse(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Back));
        }

        [Test]
        public void FrontAndMiddle_AllowsPromotedBackWhenFrontDead()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 0),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("middle")));
            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("back")));
        }

        [Test]
        public void Any_AllowsAllSlots()
        {
            var card = DamageCard(TargetReach.Any);
            var state = Team(Unit("back", FormationSlot.Back));

            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("back")));
            Assert.AreEqual(TargetReach.Any, TargetReachRules.GetPickReach(card));
        }

        [Test]
        public void BackRowPowerPercent_ReducesDamageOnEffectiveBackTarget()
        {
            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                BackRowPowerPercent = 70
            };
            var state = Team(
                Unit("front", FormationSlot.Front),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            var back = state.GetCombatant("back");
            var front = state.GetCombatant("front");

            Assert.AreEqual(7, TargetReachRules.AdjustPowerForTarget(state, action, back, 10));
            Assert.AreEqual(10, TargetReachRules.AdjustPowerForTarget(state, action, front, 10));
        }

        [Test]
        public void BackRowPowerPercent_DoesNotReduceWhenBackPromotedToMiddle()
        {
            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                BackRowPowerPercent = 70
            };
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 0),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            var back = state.GetCombatant("back");
            Assert.AreEqual(FormationSlot.Middle, PositionRules.GetEffectiveSlot(state, back));
            Assert.AreEqual(10, TargetReachRules.AdjustPowerForTarget(state, action, back, 10));
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

        static BattleState Team(params CombatantState[] units)
        {
            var state = new BattleState();
            foreach (var unit in units)
            {
                unit.Team = TeamSide.Enemy;
                if (unit.Hp <= 0)
                    unit.Hp = 0;
                else if (unit.MaxHp <= 0)
                {
                    unit.Hp = 10;
                    unit.MaxHp = 10;
                }

                state.Combatants.Add(unit);
            }

            return state;
        }

        static CombatantState Unit(string id, FormationSlot slot, int hp = 10) =>
            new()
            {
                Id = id,
                DisplayName = id,
                Slot = slot,
                Hp = hp,
                MaxHp = 10
            };
    }
}
