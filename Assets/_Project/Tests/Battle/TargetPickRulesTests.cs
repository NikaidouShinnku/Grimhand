using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class TargetPickRulesTests
    {

        [Test]
        public void EnemyAi_MayAutoRollWhenNoManualTarget()
        {
            var state = BuildState();
            var goblin = AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front);
            AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            AddUnit(state, "mage", TeamSide.Player, FormationSlot.Back);

            var card = new CardInstanceState
            {
                InstanceId = 8,
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

            var rng = new Grimhand.Core.BattleRng(42);
            var target = TargetRules.ResolveTarget(
                state, goblin, EffectTarget.DefaultEnemy, card.InstanceId, rng, card.Actions[0]);
            Assert.IsNotNull(target);
            Assert.AreEqual(TeamSide.Player, target.Team);
        }


        [Test]
        public void ConsumeBlockDealDamage_RequiresTargetPick()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front);

            var action = new EffectActionSpec
            {
                Type = EffectActionType.ConsumeBlockDealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 4,
                Reach = TargetReach.Any
            };
            var card = new CardInstanceState { InstanceId = 10, Actions = { action } };

            Assert.IsTrue(CardRules.ActionRequiresCharacterPickForReach(action));
            Assert.IsTrue(CardRules.ShouldPromptForTarget(state, card, warrior));
        }

        static BattleState BuildState() => new BattleState { Config = new BattleConfig() };

        static CombatantState AddUnit(
            BattleState state, string id, TeamSide team, FormationSlot slot)
        {
            var unit = new CombatantState
            {
                Id = id,
                DisplayName = id,
                Team = team,
                Slot = slot,
                Hp = 30,
                MaxHp = 30,
                Attack = 5,
                Defense = 2
            };
            state.Combatants.Add(unit);
            return unit;
        }
    }
}
