using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.V091;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class V091DemonEchoTests
    {
        [Test]
        public void DemonEcho_RequiresManualEnemyTarget()
        {
            var card = new CardInstanceState
            {
                DefinitionId = V091MechanicsRules.DemonEchoCardId,
                DisplayName = "魔神回响",
                CardType = CardType.Attack,
                Cost = 6
            };
            card.Keywords.Add("inherit");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 20,
                Reach = TargetReach.Any
            });

            Assert.IsTrue(CardRules.RequiresManualTarget(card));
            Assert.AreEqual(TargetPickSide.Enemy, CardRules.GetRequiredTargetPick(card));
        }

        [Test]
        public void DemonEcho_SacrificeReducesCost_AndShuffleResets()
        {
            var state = new BattleState();
            var echo = new CardInstanceState
            {
                DefinitionId = V091MechanicsRules.DemonEchoCardId,
                DisplayName = "魔神回响",
                CardType = CardType.Attack,
                Cost = 6
            };
            var owner = new CombatantState
            {
                Id = "ranger",
                CharacterDefinitionId = "char_ranger",
                Team = TeamSide.Player
            };

            V091MechanicsRules.OnSacrificeCardPlayed(state, echo);
            V091MechanicsRules.OnSacrificeCardPlayed(state, echo);
            Assert.AreEqual(
                2,
                V091MechanicsRules.AdjustPlayCost(state, owner, echo, echo.Cost));

            V091MechanicsRules.OnCardShuffledToDrawPile(state, echo);
            Assert.AreEqual(
                6,
                V091MechanicsRules.AdjustPlayCost(state, owner, echo, echo.Cost));
        }
    }
}
