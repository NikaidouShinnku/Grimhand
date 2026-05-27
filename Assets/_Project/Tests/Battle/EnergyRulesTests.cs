using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class EnergyRulesTests
    {
        [Test]
        public void FirstTurn_StartsAtMax()
        {
            var state = new Model.BattleState { IsFirstPlayerTurn = true, EnergyMax = 8 };
            EnergyRules.ApplyTurnStartRegen(state);
            Assert.AreEqual(8, state.EnergyCurrent);
            Assert.IsFalse(state.IsFirstPlayerTurn);
        }

        [Test]
        public void SecondTurn_RegenThreeUpToMax()
        {
            var state = new Model.BattleState
            {
                IsFirstPlayerTurn = false,
                EnergyCurrent = 3,
                EnergyMax = 8
            };
            EnergyRules.ApplyTurnStartRegen(state);
            Assert.AreEqual(6, state.EnergyCurrent);
        }

        [Test]
        public void Regen_DoesNotExceedMax()
        {
            var state = new Model.BattleState
            {
                IsFirstPlayerTurn = false,
                EnergyCurrent = 7,
                EnergyMax = 8
            };
            EnergyRules.ApplyTurnStartRegen(state);
            Assert.AreEqual(8, state.EnergyCurrent);
        }
    }
}
