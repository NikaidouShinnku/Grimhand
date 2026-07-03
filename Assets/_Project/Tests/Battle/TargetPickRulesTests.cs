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
        public void FrontMidReach_RequiresManualPickBeforeResolve()
        {
            var state = BuildState();
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front);
            AddUnit(state, "skel", TeamSide.Enemy, FormationSlot.Back);

            var card = new CardInstanceState
            {
                InstanceId = 7,
                CardType = CardType.Attack,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = 8,
                        Reach = TargetReach.FrontAndMiddle
                    }
                }
            };

            Assert.IsTrue(CardRules.ShouldPromptForTarget(state, card, warrior));

            var target = TargetRules.ResolveTarget(
                state, warrior, EffectTarget.DefaultEnemy, card.InstanceId, null, card.Actions[0]);
            Assert.IsNull(target, "玩家未选目标时不应 auto-roll");

            state.ResolutionTargets[card.InstanceId] = "skel";
            var backPick = TargetRules.ResolveTarget(
                state, warrior, EffectTarget.DefaultEnemy, card.InstanceId, null, card.Actions[0]);
            Assert.IsNull(backPick, "后排超出 Reach 应被拒绝");

            state.ResolutionTargets[card.InstanceId] = "goblin";
            var frontPick = TargetRules.ResolveTarget(
                state, warrior, EffectTarget.DefaultEnemy, card.InstanceId, null, card.Actions[0]);
            Assert.AreEqual("goblin", frontPick.Id);
        }

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
        public void PlanningDraft_BlocksCompleteSelectUntilTargetChosen()
        {
            var state = BuildState();
            state.Phase = TurnPhase.Planning;
            state.EnergyCurrent = 5;
            var warrior = AddUnit(state, "warrior", TeamSide.Player, FormationSlot.Front);
            AddUnit(state, "goblin", TeamSide.Enemy, FormationSlot.Front);

            var card = new CardInstanceState
            {
                InstanceId = 9,
                OwnerCharacterId = warrior.Id,
                DisplayName = "测试斩击",
                Cost = 1,
                CardType = CardType.Attack,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = 6,
                        Reach = TargetReach.FrontAndMiddle
                    }
                }
            };
            state.PlayerHand.Add(card);

            var events = new List<Grimhand.Battle.Events.BattleEvent>();
            var draft = new PlanningDraft(state, events);

            Assert.IsTrue(draft.TrySelectCard(9));
            Assert.AreEqual(9, draft.AwaitingTargetCardId);
            Assert.IsFalse(draft.IsSelected(9));

            Assert.IsTrue(draft.TryAssignTargetAndSelect("goblin"));
            Assert.IsTrue(draft.IsSelected(9));
            Assert.IsNull(draft.AwaitingTargetCardId);
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
