using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class LichQueenPlanningTests
    {
        [Test]
        public void RealmDescent_AllowsSelectingZeroCostAdjustedCards()
        {
            var state = BuildPlanningState(energyCurrent: 0);
            var lich = state.GetCombatant("lich");
            StatusRules.ApplyStatus(state, lich, StatusCatalog.HandCostZero, 1, 1, new List<BattleEvent>());

            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var expensive = AddHandCard(state, 1, "l_psionic_cannon", cost: 3, ownerCharacterId: "char_lich_queen");

            Assert.AreEqual(0, draft.GetPlayCost(expensive));
            Assert.True(draft.IsCardSelectable(expensive.InstanceId));
            Assert.True(draft.TrySelectCard(expensive.InstanceId));
        }

        [Test]
        public void EtherealForm_BlocksOtherOwnerCardsWhenSelectedFirst()
        {
            var state = BuildPlanningState(energyCurrent: 5);
            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var ethereal = AddEtherealForm(state, 1);
            var claw = AddHandCard(state, 2, "l_ghost_claw", cost: 1, ownerCharacterId: "char_lich_queen");

            Assert.True(draft.TrySelectCard(ethereal.InstanceId));
            Assert.False(draft.IsCardSelectable(claw.InstanceId));
            Assert.False(draft.TrySelectCard(claw.InstanceId));
        }

        [Test]
        public void EtherealForm_CannotSelectWhenOtherOwnerCardAlreadyQueued()
        {
            var state = BuildPlanningState(energyCurrent: 5);
            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var claw = AddHandCard(state, 1, "l_ghost_claw", cost: 1, ownerCharacterId: "char_lich_queen");
            var ethereal = AddEtherealForm(state, 2);

            Assert.True(draft.TrySelectCard(claw.InstanceId));
            Assert.False(draft.IsCardSelectable(ethereal.InstanceId));
            Assert.False(draft.TrySelectCard(ethereal.InstanceId));
        }

        static BattleState BuildPlanningState(int energyCurrent)
        {
            var state = new BattleState
            {
                Config = new BattleConfig(),
                Phase = TurnPhase.Planning,
                EnergyCurrent = energyCurrent,
                EnergyMax = 10
            };

            var lich = new CombatantState
            {
                Id = "lich",
                DisplayName = "lich",
                Team = TeamSide.Player,
                Slot = FormationSlot.Back,
                Hp = 40,
                MaxHp = 40,
                CharacterDefinitionId = "char_lich_queen"
            };
            state.Combatants.Add(lich);
            return state;
        }

        static CardInstanceState AddHandCard(
            BattleState state,
            int instanceId,
            string definitionId,
            int cost,
            string ownerCharacterId)
        {
            var card = new CardInstanceState
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                DisplayName = definitionId,
                OwnerCharacterId = ownerCharacterId,
                Cost = cost,
                CardType = CardType.Attack,
                IsUsable = true,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = 5,
                        Reach = TargetReach.FrontAndMiddle
                    }
                }
            };

            state.CardsById[instanceId] = card;
            state.PlayerHand.Add(card);
            return card;
        }

        static CardInstanceState AddEtherealForm(BattleState state, int instanceId)
        {
            var card = new CardInstanceState
            {
                InstanceId = instanceId,
                DefinitionId = "l_ethereal_form",
                DisplayName = "虚化形态",
                OwnerCharacterId = "char_lich_queen",
                Cost = 1,
                CardType = CardType.Status,
                IsUsable = true,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.ApplyStatus,
                        Target = EffectTarget.Self,
                        StatusId = StatusCatalog.Ethereal,
                        Stacks = 1,
                        Duration = 1
                    },
                    new EffectActionSpec
                    {
                        Type = EffectActionType.LockSelfCards,
                        Target = EffectTarget.Self,
                        Value = 1
                    }
                }
            };

            state.CardsById[instanceId] = card;
            state.PlayerHand.Add(card);
            return card;
        }
    }
}
