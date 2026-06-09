using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class TargetReachRulesTests
    {
        [Test]
        public void FrontAndMiddle_ExcludesEffectiveBackRow()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            var state = Team(
                Unit("front", FormationSlot.Front),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            Assert.IsFalse(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("back")));
            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("front")));
            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("middle")));
            Assert.IsTrue(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Front));
            Assert.IsTrue(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Middle));
            Assert.IsFalse(TargetReachRules.IsSlotAllowed(TargetReach.FrontAndMiddle, FormationSlot.Back));
        }

        [Test]
        public void FrontAndMiddle_AllowsPromotedBackWhenFrontDead()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 0),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("middle")));
            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("back")));
        }

        [Test]
        public void Any_AllowsAllSlots()
        {
            var card = DamageCard(TargetReach.Any);
            var state = Team(Unit("back", FormationSlot.Back));

            Assert.IsTrue(TargetReachRules.CanPickUnit(state, card, state.GetCombatant("back")));
            Assert.AreEqual(TargetReach.Any, TargetReachRules.GetPickReach(card));
        }

        [Test]
        public void BackRowPowerPercent_ReducesDamageOnEffectiveBackTarget()
        {
            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                BackRowPowerPercent = 70
            };
            var state = Team(
                Unit("front", FormationSlot.Front),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            var back = state.GetCombatant("back");
            var front = state.GetCombatant("front");

            Assert.AreEqual(7, TargetReachRules.AdjustPowerForTarget(state, action, back, 10));
            Assert.AreEqual(10, TargetReachRules.AdjustPowerForTarget(state, action, front, 10));
        }

        [Test]
        public void BackRowPowerPercent_DoesNotReduceWhenBackPromotedToMiddle()
        {
            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                BackRowPowerPercent = 70
            };
            var state = Team(
                Unit("front", FormationSlot.Front, hp: 0),
                Unit("middle", FormationSlot.Middle),
                Unit("back", FormationSlot.Back));

            var back = state.GetCombatant("back");
            Assert.AreEqual(FormationSlot.Middle, PositionRules.GetEffectiveSlot(state, back));
            Assert.AreEqual(10, TargetReachRules.AdjustPowerForTarget(state, action, back, 10));
        }

        [Test]
        public void GetPickReach_DefaultsToFrontAndMiddleForMeleeDamage()
        {
            var card = DamageCard(TargetReach.FrontAndMiddle);
            Assert.AreEqual(TargetReach.FrontAndMiddle, TargetReachRules.GetPickReach(card));
        }

        static CardInstanceState DamageCard(TargetReach reach)
        {
            var card = new CardInstanceState();
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = reach
            });
            return card;
        }

        [Test]
        public void AutoTarget_FrontAndMiddle_RandomWithinReachPool()
        {
            var state = PlayerTeam(
                Unit("warrior", FormationSlot.Front),
                Unit("mage", FormationSlot.Middle),
                Unit("demon", FormationSlot.Back));
            var enemy = EnemyUnit("slime");
            state.Combatants.Add(enemy);

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.FrontAndMiddle,
                Value = 5
            };

            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 200; seed++)
            {
                state.ResolutionTargets.Clear();
                var rng = new BattleRng(seed);
                var target = TargetRules.ResolveTarget(
                    state, enemy, EffectTarget.DefaultEnemy, seed, rng, action);
                Assert.IsNotNull(target);
                Assert.IsTrue(target.Id is "warrior" or "mage");
                seen.Add(target.Id);
            }

            Assert.AreEqual(2, seen.Count, "前/中射程应在存活的前排与中排间随机");
        }

        [Test]
        public void AutoTarget_MiddleAndBack_RandomWithinReachPool()
        {
            var state = PlayerTeam(
                Unit("warrior", FormationSlot.Front),
                Unit("mage", FormationSlot.Middle),
                Unit("demon", FormationSlot.Back));
            var enemy = EnemyUnit("ghost");
            state.Combatants.Add(enemy);

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.MiddleAndBack,
                Value = 8
            };

            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 200; seed++)
            {
                state.ResolutionTargets.Clear();
                var rng = new BattleRng(seed);
                var target = TargetRules.ResolveTarget(
                    state, enemy, EffectTarget.DefaultEnemy, seed, rng, action);
                Assert.IsNotNull(target);
                Assert.IsTrue(target.Id is "mage" or "demon");
                seen.Add(target.Id);
            }

            Assert.AreEqual(2, seen.Count, "中/后排射程应在存活的中排与后排间随机");
        }

        [Test]
        public void AutoTarget_Any_RandomAmongAllRows()
        {
            var state = PlayerTeam(
                Unit("warrior", FormationSlot.Front),
                Unit("mage", FormationSlot.Middle),
                Unit("demon", FormationSlot.Back));
            var enemy = EnemyUnit("boss");
            state.Combatants.Add(enemy);

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.Any,
                Value = 12
            };

            var seen = new HashSet<string>();
            for (var seed = 1; seed <= 300; seed++)
            {
                state.ResolutionTargets.Clear();
                var rng = new BattleRng(seed);
                var target = TargetRules.ResolveTarget(
                    state, enemy, EffectTarget.DefaultEnemy, seed, rng, action);
                Assert.IsNotNull(target);
                Assert.IsTrue(target.Id is "warrior" or "mage" or "demon");
                seen.Add(target.Id);
            }

            Assert.AreEqual(3, seen.Count, "任意射程应在全体存活目标间随机");
        }

        [Test]
        public void PrerollEnemyAutoTargets_MatchesResolve()
        {
            var state = PlayerTeam(
                Unit("warrior", FormationSlot.Front),
                Unit("mage", FormationSlot.Middle));
            var enemy = EnemyUnit("slime");
            enemy.CharacterDefinitionId = "slime";
            state.Combatants.Add(enemy);

            var cardId = 77;
            var card = DamageCard(TargetReach.FrontAndMiddle);
            card.InstanceId = cardId;
            card.OwnerCharacterId = enemy.CharacterDefinitionId;
            state.CardsById[cardId] = card;

            var plan = new BattlePlan();
            plan.PlayQueue.Add(cardId);

            var rng = new BattleRng(99);
            TargetRules.PrerollEnemyAutoTargets(state, plan, rng);

            Assert.IsTrue(state.ResolutionTargets.ContainsKey(cardId));
            var action = card.Actions[0];
            var resolved = TargetRules.ResolveTarget(
                state, enemy, EffectTarget.DefaultEnemy, cardId, null, action);
            Assert.AreEqual(state.ResolutionTargets[cardId], resolved.Id);
        }

        [Test]
        public void CachedAutoTarget_ReusedOnSecondResolve()
        {
            var state = PlayerTeam(
                Unit("warrior", FormationSlot.Front),
                Unit("mage", FormationSlot.Middle));
            var enemy = EnemyUnit("slime");
            state.Combatants.Add(enemy);

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Reach = TargetReach.FrontAndMiddle,
                Value = 5
            };

            var first = TargetRules.ResolveTarget(
                state, enemy, EffectTarget.DefaultEnemy, 55, new BattleRng(12), action);
            var second = TargetRules.ResolveTarget(
                state, enemy, EffectTarget.DefaultEnemy, 55, new BattleRng(999), action);

            Assert.AreEqual(first.Id, second.Id, "同一张牌多次结算应锁定同一随机目标");
        }

        static BattleState PlayerTeam(params CombatantState[] units)
        {
            var state = new BattleState();
            foreach (var unit in units)
            {
                unit.Team = TeamSide.Player;
                if (unit.Hp <= 0)
                    unit.Hp = 0;
                else if (unit.MaxHp <= 0)
                {
                    unit.Hp = 10;
                    unit.MaxHp = 10;
                }

                state.Combatants.Add(unit);
            }

            return state;
        }

        static CombatantState EnemyUnit(string id) =>
            new()
            {
                Id = id,
                DisplayName = id,
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                Hp = 20,
                MaxHp = 20
            };

        static BattleState Team(params CombatantState[] units)
        {
            var state = new BattleState();
            foreach (var unit in units)
            {
                unit.Team = TeamSide.Enemy;
                if (unit.Hp <= 0)
                    unit.Hp = 0;
                else if (unit.MaxHp <= 0)
                {
                    unit.Hp = 10;
                    unit.MaxHp = 10;
                }

                state.Combatants.Add(unit);
            }

            return state;
        }

        static CombatantState Unit(string id, FormationSlot slot, int hp = 10) =>
            new()
            {
                Id = id,
                DisplayName = id,
                Slot = slot,
                Hp = hp,
                MaxHp = 10
            };
    }
}
