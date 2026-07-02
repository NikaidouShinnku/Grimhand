using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
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
        public void SecondTurn_RegenFourUpToMax()
        {
            var state = new Model.BattleState
            {
                IsFirstPlayerTurn = false,
                EnergyCurrent = 3,
                EnergyMax = 8
            };
            state.Config.TurnStartEnergyRegen = 4;
            EnergyRules.ApplyTurnStartRegen(state);
            Assert.AreEqual(7, state.EnergyCurrent);
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

        [Test]
        public void GainTemporary_CanExceedMax()
        {
            var state = new Model.BattleState
            {
                EnergyCurrent = 7,
                EnergyMax = 8
            };

            EnergyRules.GainTemporary(state, 2);

            Assert.AreEqual(9, state.EnergyCurrent);
            Assert.AreEqual(8, state.EnergyMax);
        }

        [Test]
        public void Restore_DoesNotExceedMax()
        {
            var state = new Model.BattleState
            {
                EnergyCurrent = 7,
                EnergyMax = 8
            };

            EnergyRules.Restore(state, 3);

            Assert.AreEqual(8, state.EnergyCurrent);
        }

        [Test]
        public void GainEnergyAction_CanExceedMax()
        {
            var state = new Model.BattleState
            {
                EnergyCurrent = 8,
                EnergyMax = 8,
                Combatants =
                {
                    new Model.CombatantState
                    {
                        Id = "lich",
                        Team = Model.TeamSide.Player,
                        CharacterDefinitionId = "char_lich_queen",
                        Hp = 48,
                        MaxHp = 48
                    }
                }
            };

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "聚能",
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.GainEnergy,
                        Target = EffectTarget.Self,
                        Value = 2
                    }
                }
            };

            EffectActionExecutor.ExecuteAll(state, state.Combatants[0], card, new System.Collections.Generic.List<Events.BattleEvent>());

            Assert.AreEqual(10, state.EnergyCurrent);
            Assert.AreEqual(8, state.EnergyMax);
        }
    }
}
