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
