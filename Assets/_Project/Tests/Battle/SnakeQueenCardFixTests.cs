using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Battle.V091;
using Grimhand.Core;
using Grimhand.Presentation.Battle;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class SnakeQueenCardFixTests
    {
        [Test]
        public void PoisonTouch_RequiresManualTarget_AndAppliesFiveWhenSlower()
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "v_poison_touch",
                CardType = CardType.Attack
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyPoisonBySpeedCompare,
                Target = EffectTarget.DefaultEnemy,
                Value = 2,
                Stacks = 5,
                Duration = 2,
                AlternateValue = 3,
                Reach = TargetReach.Any
            });
            Assert.IsTrue(CardRules.RequiresManualTarget(card));

            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, 20, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(goblin);
            state.CardsById[1] = card;
            state.ResolutionTargets[1] = goblin.Id;

            EffectActionExecutor.ExecuteAll(state, queen, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(5, StatusRules.GetStatusStacks(goblin, StatusCatalog.Poison));
            Assert.AreEqual(2, StatusRules.FindStatus(goblin, StatusCatalog.Poison).RemainingTurns);
        }

        [Test]
        public void ScaleHarden_OnlyGainsBlock_NeverDamages()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            state.Combatants.Add(queen);
            StatusRules.ApplyStatus(state, queen, StatusCatalog.Poison, 2, 2, new List<BattleEvent>());

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "v_scale_harden",
                CardType = CardType.Defense
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockBonusIfSelfPoisoned,
                Target = EffectTarget.Self,
                Value = 8,
                Stacks = 6
            });

            var hpBefore = queen.Hp;
            EffectActionExecutor.ExecuteAll(state, queen, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(hpBefore, queen.Hp);
            Assert.AreEqual(14, queen.Block);
        }

        [Test]
        public void Detonate_SumsDurationBucketsSeparately()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 100, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(enemy);

            // 两次 2 层 / 2 回合 → 合桶 4×2
            StatusRules.ApplyStatus(state, enemy, StatusCatalog.Poison, 2, 2, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, enemy, StatusCatalog.Poison, 2, 2, new List<BattleEvent>());
            // 另加 2 层 / 3 回合 → 独立桶 2×3
            StatusRules.ApplyStatus(state, enemy, StatusCatalog.Poison, 2, 3, new List<BattleEvent>());
            Assert.AreEqual(6, StatusRules.GetStatusStacks(enemy, StatusCatalog.Poison));

            var events = new List<BattleEvent>();
            V09NewMechanicsRules.SettlePoisonAndClear(state, queen, enemy, events);
            // 4*2 + 2*3 = 14
            Assert.AreEqual(86, enemy.Hp);
            Assert.IsFalse(StatusRules.HasStatus(enemy, StatusCatalog.Poison));
        }

        [Test]
        public void VenomFeast_IsAoeSettle_NoManualTarget()
        {
            var card = new CardInstanceState
            {
                DefinitionId = "v_venom_feast",
                CardType = CardType.Status
            };
            card.Keywords.Add("aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SettlePoisonAndClear,
                Target = EffectTarget.AllEnemies,
                Reach = TargetReach.Any
            });
            Assert.IsFalse(CardRules.RequiresManualTarget(card));

            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var a = Unit("a", TeamSide.Enemy, FormationSlot.Front, 50, 3);
            var b = Unit("b", TeamSide.Enemy, FormationSlot.Middle, 50, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(a);
            state.Combatants.Add(b);
            StatusRules.ApplyStatus(state, a, StatusCatalog.Poison, 2, 2, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, b, StatusCatalog.Poison, 1, -1, new List<BattleEvent>());

            EffectActionExecutor.ExecuteAll(state, queen, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(46, a.Hp); // 2*2
            Assert.AreEqual(47, b.Hp); // 1*3 permanent
            Assert.IsFalse(StatusRules.HasStatus(a, StatusCatalog.Poison));
            Assert.IsFalse(StatusRules.HasStatus(b, StatusCatalog.Poison));
        }

        [Test]
        public void StatusApplied_BeforeEnemyPose_MergesIntoNextCardSegment()
        {
            // 毒鳞应对：上毒事件并入敌方出牌动画，特效/脚标与该牌同一段播出。
            var events = new List<BattleEvent>
            {
                new(BattleEventKind.StatusApplied, "中毒")
                {
                    CombatantId = "goblin",
                    TargetId = StatusCatalog.Poison,
                    Amount = 3
                },
                new(BattleEventKind.PortraitPoseChanged, "哥布林")
                {
                    CombatantId = "goblin",
                    CardType = CardType.Attack
                },
                new(BattleEventKind.DamageApplied, "伤害")
                {
                    CombatantId = "goblin",
                    TargetId = "queen",
                    Amount = 5
                },
                new(BattleEventKind.PortraitIdleRestored, "哥布林")
                {
                    CombatantId = "goblin"
                }
            };

            Assert.IsTrue(BattleEventPlayback.ContainsPresentationEvents(events));
            var segments = BattleEventPlayback.SplitIntoSegments(events);
            Assert.AreEqual(1, segments.Count);
            Assert.AreEqual(BattleEventKind.PortraitPoseChanged, segments[0][0].Kind);
            Assert.AreEqual(BattleEventKind.StatusApplied, segments[0][1].Kind);
            Assert.AreEqual(BattleEventKind.DamageApplied, segments[0][2].Kind);
        }

        [Test]
        public void PoisonScale_AppliesPoisonImmediately_OnRespondSuccess()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Back, 20, 5);
            state.Combatants.Add(queen);
            state.Combatants.Add(goblin);

            var tear = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "v_poison_scale",
                CardType = CardType.Defense,
                OwnerCharacterId = queen.CharacterDefinitionId
            };
            tear.Keywords.Add("parry");
            tear.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 50,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            tear.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.LastActionActor,
                StatusId = StatusCatalog.Poison,
                Stacks = 3,
                Duration = -1,
                Condition = ReactionConditionType.LastActionAttackOnSelf,
                Reach = TargetReach.Any
            });
            state.CardsById[1] = tear;
            state.CardsById[2] = new CardInstanceState
            {
                InstanceId = 2,
                CardType = CardType.Attack,
                OwnerCharacterId = goblin.CharacterDefinitionId,
                Actions =
                {
                    new EffectActionSpec
                    {
                        Type = EffectActionType.DealDamage,
                        Target = EffectTarget.DefaultEnemy,
                        Value = 10,
                        Reach = TargetReach.Any
                    }
                }
            };

            RespondEffectExecutor.Execute(
                state, queen, tear, new RespondTriggerContext(goblin.Id, 2), new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(3, StatusRules.GetStatusStacks(goblin, StatusCatalog.Poison));
        }

        static CombatantState Unit(string id, TeamSide team, FormationSlot slot, int hp, int speed) =>
            new()
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = id,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                Speed = speed
            };
    }
}
