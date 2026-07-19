using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V091;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class LichQueenCardFixBatch3Tests
    {
        [Test]
        public void SoulDevour_DamagesSelectedAlly_AndGainsEnergyNextTurn()
        {
            var state = new BattleState
            {
                EnergyCurrent = 2,
                EnergyMax = 8,
                IsFirstPlayerTurn = false,
                Config = new BattleConfig { TurnStartEnergyRegen = 4 }
            };
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            var ally = Unit("ally", TeamSide.Player, FormationSlot.Front, 40, 5);
            state.Combatants.Add(lich);
            state.Combatants.Add(ally);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_soul_devour",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.FrontAlly,
                Value = 10,
                Reach = TargetReach.Any
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainEnergyNextTurn,
                Target = EffectTarget.Self,
                Value = 3
            });
            state.CardsById[1] = card;
            state.ResolutionTargets[1] = ally.Id;

            Assert.IsTrue(CardRules.RequiresManualTarget(card));
            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(30, ally.Hp);
            Assert.AreEqual(40, lich.Hp);
            Assert.AreEqual(2, state.EnergyCurrent);
            Assert.AreEqual(3, state.PendingPlayerEnergyGainNextTurn);

            EnergyRules.ApplyTurnStartRegen(state);
            Assert.AreEqual(9, state.EnergyCurrent); // 2 + 4 regen + 3 pending
            Assert.AreEqual(0, state.PendingPlayerEnergyGainNextTurn);
        }

        [Test]
        public void SoulReinforce_BuffsOtherAllies()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            var ally = Unit("ally", TeamSide.Player, FormationSlot.Front, 40, 5);
            state.Combatants.Add(lich);
            state.Combatants.Add(ally);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_soul_reinforce",
                CardType = CardType.Status,
                Keywords = { "sacrifice" }
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.Self,
                Value = 10
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.BuffAllOtherAllies,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 25,
                Duration = 2
            });

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(30, lich.Hp);
            Assert.IsFalse(StatusRules.HasStatus(lich, StatusCatalog.AttackUpPercent));
            Assert.IsTrue(StatusRules.HasStatus(ally, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(25, StatusRules.FindStatus(ally, StatusCatalog.AttackUpPercent).Stacks);
        }

        [Test]
        public void PsionicArrowRain_AppliesTurnStartRandomDamage()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 40, 3);
            state.Combatants.Add(lich);
            state.Combatants.Add(enemy);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = V091MechanicsRules.PsionicArrowRainCardId,
                CardType = CardType.Attack
            };
            Assert.IsTrue(SpecialCardRules.TryResolve(state, lich, card, new List<BattleEvent>(), new BattleRng(1)));
            Assert.IsTrue(StatusRules.HasStatus(lich, StatusCatalog.PsionicArrowRain));

            V091MechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(30, enemy.Hp);
        }

        [Test]
        public void RealmSeal_IsFixedCost_NoXCost()
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_realm_seal",
                Cost = 4,
                CardType = CardType.Status,
                Keywords = { "exhaust" }
            };
            Assert.IsFalse(CardPowerRules.UsesRemainingEnergyCost(card));
            Assert.AreEqual(4, card.Cost);
        }

        static CombatantState Unit(string id, TeamSide team, FormationSlot slot, int hp, int atk) =>
            new CombatantState
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = "char_lich_queen",
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                BaseAttack = atk,
                Attack = atk,
                Defense = 0,
                Speed = 5
            };
    }
}
