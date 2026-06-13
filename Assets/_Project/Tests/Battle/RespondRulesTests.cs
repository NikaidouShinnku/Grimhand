using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class RespondRulesTests
    {
        [Test]
        public void Respond_InterceptsBeforeFirstMatchingEnemyAttack()
        {
            var state = BuildStateWithCards(out var knight, out var goblin, out var parryId, out var attackId);

            var playerPlan = new BattlePlan();
            playerPlan.PlayQueue.Add(parryId);
            var enemyPlan = new BattlePlan();
            enemyPlan.PlayQueue.Add(attackId);

            var baseline = SpeedResolver.BuildResolutionOrder(state, playerPlan, enemyPlan, new BattleRng(1));
            var schedule = RespondResolutionPlanner.BuildSchedule(state, baseline);

            Assert.AreEqual(2, schedule.Count);
            Assert.AreEqual(parryId, schedule[0].Step.CardInstanceId);
            Assert.IsTrue(schedule[0].RespondContext.HasValue);
            Assert.AreEqual(attackId, schedule[0].RespondContext.Value.EnemyCardInstanceId);
            Assert.AreEqual(attackId, schedule[1].Step.CardInstanceId);
        }

        [Test]
        public void Respond_FiresEvenWhenSlowerThanEnemy()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40, speed: 3);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, hp: 50, speed: 8);
            state.Combatants.Add(knight);
            state.Combatants.Add(goblin);

            var parryId = 1;
            var parry = ParryCard();
            parry.InstanceId = parryId;
            parry.OwnerCharacterId = knight.CharacterDefinitionId;
            state.CardsById[parryId] = parry;

            var attackId = 2;
            state.CardsById[attackId] = EnemyAttackCard(attackId, goblin.CharacterDefinitionId);

            var playerPlan = new BattlePlan { PlayQueue = { parryId } };
            var enemyPlan = new BattlePlan { PlayQueue = { attackId } };

            var schedule = RespondResolutionPlanner.BuildSchedule(
                state,
                SpeedResolver.BuildResolutionOrder(state, playerPlan, enemyPlan, new BattleRng(1)));

            Assert.AreEqual(knight.Id, schedule[0].Step.CombatantId);
        }

        [Test]
        public void Respond_AppliesMitigationAndCounterBeforeEnemyDamage()
        {
            var state = BuildStateWithCards(out var knight, out var goblin, out var parryId, out var attackId);
            var context = new RespondTriggerContext(goblin.Id, attackId);
            var events = new List<BattleEvent>();

            RespondEffectExecutor.Execute(state, knight, state.GetCard(parryId), context, events, new BattleRng(1));

            Assert.IsTrue(state.RespondMitigationByEnemyCard.ContainsKey(attackId));
            Assert.AreEqual(1, state.PendingParryStrikes.Count);
            Assert.AreEqual(50, goblin.Hp, "弹反伤害应等到敌方攻击演出后再结算");

            events.Clear();
            var attack = state.GetCard(attackId);
            EffectActionExecutor.ExecuteAll(state, goblin, attack, events, new BattleRng(1));
            RespondEffectExecutor.ResolvePendingParriesForEnemyCard(state, attackId, events, new BattleRng(1));

            Assert.Less(goblin.Hp, 50, "敌方攻击归位后应结算弹反反击");
            Assert.Greater(knight.Hp, 0, "减伤后应仍能存活（相对无应对）");
        }

        [Test]
        public void Respond_DoesNothing_WhenNoMatchingEnemyCard()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            state.Combatants.Add(knight);

            var parryId = 1;
            var parry = ParryCard();
            parry.InstanceId = parryId;
            parry.OwnerCharacterId = knight.CharacterDefinitionId;
            state.CardsById[parryId] = parry;

            var buffId = 2;
            var buff = new CardInstanceState
            {
                InstanceId = buffId,
                DisplayName = "加固",
                CardType = CardType.Defense,
                OwnerCharacterId = "enemy"
            };
            buff.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 5
            });
            state.CardsById[buffId] = buff;

            var playerPlan = new BattlePlan { PlayQueue = { parryId } };
            var enemyPlan = new BattlePlan { PlayQueue = { buffId } };

            var schedule = RespondResolutionPlanner.BuildSchedule(
                state,
                SpeedResolver.BuildResolutionOrder(state, playerPlan, enemyPlan, new BattleRng(1)));

            Assert.AreEqual(2, schedule.Count);
            Assert.IsFalse(schedule[0].RespondContext.HasValue);
            Assert.IsFalse(schedule[0].ApplyConditionalEffects);
        }

        [Test]
        public void Respond_DoesNotMatchFrontRow_WhenEnemyOnlyHitsMiddleReach()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            var mage = Unit("mage", TeamSide.Player, FormationSlot.Middle, hp: 35);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, hp: 50);
            state.Combatants.Add(knight);
            state.Combatants.Add(mage);
            state.Combatants.Add(goblin);

            var parryId = 1;
            var parry = ParryCard();
            parry.InstanceId = parryId;
            parry.OwnerCharacterId = knight.CharacterDefinitionId;
            state.CardsById[parryId] = parry;

            var spearId = 2;
            var spear = new CardInstanceState
            {
                InstanceId = spearId,
                DisplayName = "骨矛",
                CardType = CardType.Attack,
                OwnerCharacterId = goblin.CharacterDefinitionId
            };
            spear.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10,
                Reach = TargetReach.MiddleAndBack
            });
            state.CardsById[spearId] = spear;

            var enemyStep = new ResolutionStep(goblin.Id, spearId, 0);
            Assert.IsFalse(
                RespondTriggerMatcher.RespondCardMatchesEnemyStep(state, knight, parry, enemyStep),
                "中/后排攻击不应触发前排战士的应对");
            Assert.IsTrue(
                RespondTriggerMatcher.WouldEnemyStepAttackCombatant(state, enemyStep, mage.Id));
        }

        [Test]
        public void Respond_DoesNotMatchFrontRow_WhenOnlyFrontAlive_AndEnemyMiddleBackReach()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, hp: 50);
            state.Combatants.Add(knight);
            state.Combatants.Add(goblin);

            var parryId = 1;
            var parry = ParryCard();
            parry.InstanceId = parryId;
            parry.OwnerCharacterId = knight.CharacterDefinitionId;
            state.CardsById[parryId] = parry;

            var spearId = 2;
            var spear = new CardInstanceState
            {
                InstanceId = spearId,
                DisplayName = "骨矛",
                CardType = CardType.Attack,
                OwnerCharacterId = goblin.CharacterDefinitionId
            };
            spear.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10,
                Reach = TargetReach.MiddleAndBack
            });
            state.CardsById[spearId] = spear;

            var enemyStep = new ResolutionStep(goblin.Id, spearId, 0);
            Assert.IsFalse(
                RespondTriggerMatcher.WouldEnemyStepAttackCombatant(state, enemyStep, knight.Id),
                "仅有前排存活时，中/后排攻击不应命中前排");
            Assert.IsFalse(
                RespondTriggerMatcher.RespondCardMatchesEnemyStep(state, knight, parry, enemyStep));
        }

        [Test]
        public void Respond_MatchesFrontRow_WhenEnemyMeleeTargetsFront()
        {
            var state = BuildStateWithCards(out var knight, out var goblin, out _, out var attackId);
            var parry = state.GetCard(1);
            var step = new ResolutionStep(goblin.Id, attackId, 0);

            Assert.IsTrue(RespondTriggerMatcher.RespondCardMatchesEnemyStep(state, knight, parry, step));
        }

        [Test]
        public void MultipleResponds_OrderedBeforeSameTrigger()
        {
            var state = new BattleState();
            var knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            var mage = Unit("mage", TeamSide.Player, FormationSlot.Middle, hp: 35);
            var goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, hp: 80);
            state.Combatants.Add(knight);
            state.Combatants.Add(mage);
            state.Combatants.Add(goblin);

            var parry1 = 1;
            var parry2 = 2;
            var aoeId = 3;
            var p1 = ParryCard();
            p1.InstanceId = parry1;
            p1.OwnerCharacterId = knight.CharacterDefinitionId;
            var p2 = ParryCard();
            p2.InstanceId = parry2;
            p2.OwnerCharacterId = mage.CharacterDefinitionId;
            state.CardsById[parry1] = p1;
            state.CardsById[parry2] = p2;

            var aoe = new CardInstanceState
            {
                InstanceId = aoeId,
                DisplayName = "群体爪击",
                CardType = CardType.Attack,
                OwnerCharacterId = goblin.CharacterDefinitionId
            };
            aoe.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.AllEnemies,
                Value = 12
            });
            state.CardsById[aoeId] = aoe;

            var playerPlan = new BattlePlan { PlayQueue = { parry1, parry2 } };
            var enemyPlan = new BattlePlan { PlayQueue = { aoeId } };

            var schedule = RespondResolutionPlanner.BuildSchedule(
                state,
                SpeedResolver.BuildResolutionOrder(state, playerPlan, enemyPlan, new BattleRng(1)));

            Assert.GreaterOrEqual(schedule.Count, 3);
            Assert.IsTrue(schedule[0].RespondContext.HasValue);
            Assert.IsTrue(schedule[1].RespondContext.HasValue);
            Assert.AreEqual(parry1, schedule[0].Step.CardInstanceId);
            Assert.AreEqual(parry2, schedule[1].Step.CardInstanceId);
            Assert.AreEqual(aoeId, schedule[2].Step.CardInstanceId);
        }

        static BattleState BuildStateWithCards(
            out CombatantState knight,
            out CombatantState goblin,
            out int parryId,
            out int attackId)
        {
            var state = new BattleState();
            knight = Unit("knight", TeamSide.Player, FormationSlot.Front, hp: 40);
            goblin = Unit("goblin", TeamSide.Enemy, FormationSlot.Front, hp: 50, attack: 7);
            state.Combatants.Add(knight);
            state.Combatants.Add(goblin);

            parryId = 1;
            var parry = ParryCard();
            parry.InstanceId = parryId;
            parry.OwnerCharacterId = knight.CharacterDefinitionId;
            state.CardsById[parryId] = parry;

            attackId = 2;
            state.CardsById[attackId] = EnemyAttackCard(attackId, goblin.CharacterDefinitionId);
            return state;
        }

        static CardInstanceState EnemyAttackCard(int id, string ownerId)
        {
            var attack = new CardInstanceState
            {
                InstanceId = id,
                DisplayName = "抓挠",
                CardType = CardType.Attack,
                OwnerCharacterId = ownerId
            };
            attack.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10
            });
            return attack;
        }

        static CombatantState Unit(
            string id,
            TeamSide team,
            FormationSlot slot,
            int hp,
            int attack = 7,
            int speed = 5)
        {
            return new CombatantState
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = id,
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                Attack = attack,
                Speed = speed
            };
        }

        static CardInstanceState ParryCard()
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "铁壁弹反",
                CardType = CardType.Defense
            };
            card.Keywords.Add("parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 50,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReflectLastDamageToAttacker,
                Target = EffectTarget.LastActionActor,
                Value = 100,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            return card;
        }
    }
}
