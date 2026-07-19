using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class HolyInfusionPlanningTests
    {
        [Test]
        public void HolyInfusion_CannotSelectWhenQueueEmpty()
        {
            var state = BuildPlanningState();
            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var holy = AddHandCard(state, 1, PassiveCardMechanicsRules.HolyInfusionCardId, cost: 0);

            Assert.False(draft.TrySelectCard(holy.InstanceId));
        }

        [Test]
        public void HolyInfusion_CostIsPreviousCardCostPlusOne()
        {
            var state = BuildPlanningState();
            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var first = AddHandCard(state, 1, "p_sand_ray", cost: 2, ownerCharacterId: "char_knight");
            var holy = AddHandCard(state, 2, PassiveCardMechanicsRules.HolyInfusionCardId, cost: 0);

            Assert.True(draft.TrySelectCard(first.InstanceId));
            Assert.AreEqual(3, draft.GetPlayCost(holy));
        }

        [Test]
        public void HolyInfusion_DeselectCopiedCardAlsoDeselectsInfusionAndRefundsEnergy()
        {
            var state = BuildPlanningState();
            state.EnergyCurrent = 10;
            state.EnergyMax = 10;
            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var first = AddHandCard(state, 1, "p_sand_ray", cost: 2, ownerCharacterId: "char_knight");
            var holy = AddHandCard(state, 2, PassiveCardMechanicsRules.HolyInfusionCardId, cost: 0);

            Assert.True(draft.TrySelectCard(first.InstanceId));
            Assert.True(draft.TrySelectCard(holy.InstanceId));
            Assert.AreEqual(5, state.EnergyCurrent); // 10 - 2 - 3

            Assert.True(draft.TryDeselectCard(first.InstanceId));
            Assert.AreEqual(0, draft.SelectedQueue.Count);
            Assert.AreEqual(10, state.EnergyCurrent);
        }

        [Test]
        public void HolyInfusion_RepeatTargetIsPreviousCard()
        {
            var state = BuildPlanningState();
            var events = new List<BattleEvent>();
            var draft = new PlanningDraft(state, events);
            var ray = AddHandCard(state, 1, "p_sand_ray", cost: 2, ownerCharacterId: "char_knight");
            var holy = AddHandCard(state, 2, PassiveCardMechanicsRules.HolyInfusionCardId, cost: 0);

            Assert.True(draft.TrySelectCard(ray.InstanceId));
            Assert.True(draft.TrySelectCard(holy.InstanceId));

            var plan = draft.CommitToPlan();
            state.PlayerPlan.PlayQueue.AddRange(plan.PlayQueue);

            Assert.True(PassiveCardMechanicsRules.TryGetHolyInfusionRepeatTarget(
                state, holy.InstanceId, out var repeatId));
            Assert.AreEqual(ray.InstanceId, repeatId);
        }

        [Test]
        public void HolyInfusion_RepeatUsesPreviousCardOwnerNotInfusionCaster()
        {
            var state = BuildPlanningState();
            state.EnergyCurrent = 10;
            AddUnit(state, "enemy", TeamSide.Enemy, FormationSlot.Front, hp: 40);
            var ray = AddHandCard(state, 1, "p_sand_ray", cost: 2, damage: 10, ownerCharacterId: "char_knight");
            var holy = AddHandCard(state, 2, PassiveCardMechanicsRules.HolyInfusionCardId, cost: 0);

            state.PlayerPlan.PlayQueue.Add(ray.InstanceId);
            state.PlayerPlan.PlayQueue.Add(holy.InstanceId);

            Assert.True(PassiveCardMechanicsRules.TryGetHolyInfusionRepeatTarget(
                state, holy.InstanceId, out var repeatId));
            Assert.AreEqual(ray.InstanceId, repeatId);

            var repeatOwnerId = PositionRules.GetOwnerCombatantId(state, state.GetCard(repeatId));
            Assert.AreEqual("knight", repeatOwnerId);

            var knight = state.GetCombatant("knight");
            var events = new List<BattleEvent>();
            var rng = new BattleRng(1);
            var enemyBefore = state.GetCombatant("enemy").Hp;

            state.ResolutionTargets[ray.InstanceId] = "enemy";
            EffectActionExecutor.ExecuteAll(state, knight, ray, events, rng);

            Assert.Less(state.GetCombatant("enemy").Hp, enemyBefore);
            Assert.AreNotEqual("mage", repeatOwnerId,
                "重复效果应归属上一张牌的所属角色，而非出灌注的法老");
            Assert.AreEqual("char_knight", ray.OwnerCharacterId);
            Assert.AreEqual("char_mage", holy.OwnerCharacterId);
        }

        static BattleState BuildPlanningState()
        {
            var state = new BattleState
            {
                Config = new BattleConfig(),
                Phase = TurnPhase.Planning,
                EnergyCurrent = 10,
                EnergyMax = 10
            };
            var mage = AddUnit(state, "mage", TeamSide.Player, FormationSlot.Middle);
            mage.CharacterDefinitionId = "char_mage";
            var knight = AddUnit(state, "knight", TeamSide.Player, FormationSlot.Front);
            knight.CharacterDefinitionId = "char_knight";
            return state;
        }

        static CardInstanceState AddHandCard(
            BattleState state,
            int instanceId,
            string definitionId,
            int cost,
            int damage = 0,
            string ownerCharacterId = "char_mage")
        {
            var card = new CardInstanceState
            {
                InstanceId = instanceId,
                DefinitionId = definitionId,
                DisplayName = definitionId,
                OwnerCharacterId = ownerCharacterId,
                Cost = cost,
                CardType = CardType.Status,
                IsUsable = true
            };
            if (damage > 0)
            {
                card.CardType = CardType.Attack;
                card.Actions.Add(new EffectActionSpec
                {
                    Type = EffectActionType.DealDamage,
                    Target = EffectTarget.DefaultEnemy,
                    Value = damage,
                    Reach = TargetReach.FrontAndMiddle
                });
            }

            state.CardsById[instanceId] = card;
            state.PlayerHand.Add(card);
            return card;
        }

        static CombatantState AddUnit(
            BattleState state, string id, TeamSide team, FormationSlot slot, int hp = 30)
        {
            var unit = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                Attack = 5,
                Defense = 0
            };
            state.Combatants.Add(unit);
            return unit;
        }
    }
}
