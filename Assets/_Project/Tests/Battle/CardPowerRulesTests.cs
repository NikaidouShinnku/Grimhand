using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class CardPowerRulesTests
    {
        [Test]
        public void Damage_AddsOwnerAttack()
        {
            var owner = new CombatantState { Attack = 3, Defense = 2 };
            var card = new CardInstanceState();
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Value = 5,
                ScaleWithAttack = true
            });
            Assert.AreEqual(8, CardPowerRules.GetEffectivePower(card, owner));
        }

        [Test]
        public void Block_AddsOwnerDefense()
        {
            var owner = new CombatantState { Attack = 3, Defense = 4 };
            var card = new CardInstanceState();
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Value = 5,
                ScaleWithDefense = true
            });
            Assert.AreEqual(9, CardPowerRules.GetEffectivePower(card, owner));
        }
    }
}
