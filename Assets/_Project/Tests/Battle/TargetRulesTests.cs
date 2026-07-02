using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class TargetRulesTests
    {
        [Test]
        public void IsTargetValidForAction_ExplicitAllyBackSlot_IgnoresFrontAndMiddleReach()
        {
            var state = new BattleState();
            var back = new CombatantState
            {
                Id = "back",
                Team = TeamSide.Player,
                Slot = FormationSlot.Back,
                Hp = 10,
                MaxHp = 10
            };
            state.Combatants.Add(back);

            var action = new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllyBackSlot,
                StatusId = "attack_up_pct",
                Stacks = 10,
                Duration = 1,
                Reach = TargetReach.FrontAndMiddle
            };

            Assert.IsTrue(TargetRules.IsTargetValidForAction(
                state, back, action.Reach, action));
        }
    }
}
