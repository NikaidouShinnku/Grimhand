using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class StatusRulesTests
    {
        [Test]
        public void Poison_AppliesPermanentStacks()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20 };
            state.Combatants.Add(target);
            var events = new System.Collections.Generic.List<Events.BattleEvent>();

            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 10, -1, events);
            Assert.AreEqual(10, target.Statuses[0].Stacks);
            Assert.AreEqual(-1, target.Statuses[0].RemainingTurns);
        }

        [Test]
        public void Poison_TimedDuration_IsRespected()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20 };
            state.Combatants.Add(target);
            var events = new System.Collections.Generic.List<Events.BattleEvent>();

            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 3, 1, events);
            Assert.AreEqual(3, target.Statuses[0].Stacks);
            Assert.AreEqual(1, target.Statuses[0].RemainingTurns);
        }

        [Test]
        public void Poison_TurnStartDealsOneDamagePerStack()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20, Block = 10 };
            state.Combatants.Add(target);
            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 3, -1,
                new System.Collections.Generic.List<Events.BattleEvent>());

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            StatusRules.ProcessTurnStartStatuses(state, events);

            Assert.AreEqual(17, target.Hp);
            Assert.AreEqual(10, target.Block);
            Assert.AreEqual(3, events[0].Amount);
        }

        [Test]
        public void Poison_OneTurn_TicksThenExpiresAtTurnStart()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20 };
            state.Combatants.Add(target);
            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 4, 1,
                new System.Collections.Generic.List<Events.BattleEvent>());

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            StatusRules.ProcessTurnStartStatuses(state, events);
            Assert.AreEqual(16, target.Hp);
            Assert.IsTrue(StatusRules.HasStatus(target, StatusCatalog.Poison));

            StatusRules.ProcessTurnStartDurations(state, events);
            Assert.IsFalse(StatusRules.HasStatus(target, StatusCatalog.Poison));
        }

        [Test]
        public void Poison_TwoTurns_SurvivesOneDurationTick()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20 };
            state.Combatants.Add(target);
            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 2, 2,
                new System.Collections.Generic.List<Events.BattleEvent>());

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            StatusRules.ProcessTurnStartStatuses(state, events);
            StatusRules.ProcessTurnStartDurations(state, events);

            Assert.IsTrue(StatusRules.HasStatus(target, StatusCatalog.Poison));
            Assert.AreEqual(1, StatusRules.FindStatus(target, StatusCatalog.Poison).RemainingTurns);
            Assert.AreEqual(18, target.Hp);
        }

        [Test]
        public void Vulnerable_TimedDuration_ExpiresAtEndOfTurn()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20 };
            state.Combatants.Add(target);
            StatusRules.ApplyStatus(state, target, StatusCatalog.Vulnerable, 20, 1,
                new System.Collections.Generic.List<Events.BattleEvent>());

            Assert.AreEqual(1, StatusRules.FindStatus(target, StatusCatalog.Vulnerable).RemainingTurns);

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            // 非跳伤类不在回合开始扣持续
            StatusRules.ProcessTurnStartDurations(state, events);
            Assert.IsTrue(StatusRules.HasStatus(target, StatusCatalog.Vulnerable));

            StatusRules.ProcessEndOfTurnDurations(state, events);
            Assert.IsFalse(StatusRules.HasStatus(target, StatusCatalog.Vulnerable));
        }

        [Test]
        public void Burn_TurnStartDealsTwoDamagePerStack()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20, BaseDefense = 5 };
            CombatantRules.RefreshDerivedStats(target);
            state.Combatants.Add(target);
            StatusRules.ApplyStatus(state, target, StatusCatalog.Burn, 4, 2,
                new System.Collections.Generic.List<Events.BattleEvent>());

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            StatusRules.ProcessTurnStartStatuses(state, events);

            Assert.AreEqual(12, target.Hp);
            Assert.AreEqual(8, events[0].Amount);
        }

        [Test]
        public void PermanentThenTimed_KeepsPermanent()
        {
            var state = new BattleState();
            var target = new CombatantState { Id = "e1", Hp = 20, MaxHp = 20 };
            state.Combatants.Add(target);
            var events = new System.Collections.Generic.List<Events.BattleEvent>();

            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 5, -1, events);
            StatusRules.ApplyStatus(state, target, StatusCatalog.Poison, 2, 3, events);

            Assert.AreEqual(7, target.Statuses[0].Stacks);
            Assert.AreEqual(-1, target.Statuses[0].RemainingTurns);
        }

        [Test]
        public void Slow_ReducesEffectiveSpeed()
        {
            var combatant = new CombatantState { Speed = 8 };
            combatant.Statuses.Add(new StatusInstance
            {
                StatusId = StatusCatalog.Slow,
                Stacks = 1,
                RemainingTurns = 2
            });

            Assert.AreEqual(6, StatusRules.GetEffectiveSpeed(combatant));
        }
    }
}
