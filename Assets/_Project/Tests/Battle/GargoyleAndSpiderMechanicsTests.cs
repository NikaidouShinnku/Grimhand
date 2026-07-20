using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class GargoyleAndSpiderMechanicsTests
    {
        [Test]
        public void SpiderFang_PoisonChanceCanHitOrMiss()
        {
            var state = BuildState(out var spider, out var warrior);
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "m_spider_fang",
                DisplayName = "毒牙刺击",
                Cost = 1,
                CardType = CardType.Attack
            };
            var poison = new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.DefaultEnemy,
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1,
                ChancePercent = 30
            };

            EffectActionExecutor.ExecuteOne(
                state, spider, card, poison, new List<BattleEvent>(), new BattleRng(1), 1,
                targetOverride: warrior);
            Assert.AreEqual(0, StatusRules.GetStatusStacks(warrior, StatusCatalog.Poison));

            EffectActionExecutor.ExecuteOne(
                state, spider, card, poison, new List<BattleEvent>(), new BattleRng(2), 1,
                targetOverride: warrior);
            Assert.AreEqual(5, StatusRules.GetStatusStacks(warrior, StatusCatalog.Poison));
        }

        [Test]
        public void SpiderWrap_ArmLocksAttackerOnConsume()
        {
            var state = BuildState(out var spider, out var warrior);
            var wrap = new CardInstanceState
            {
                InstanceId = 2,
                DefinitionId = "m_spider_wrap",
                DisplayName = "蛛网包裹",
                CardType = CardType.Defense
            };
            wrap.Keywords.Add("parry");
            wrap.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 50,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            wrap.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockAttackCards,
                Target = EffectTarget.LastActionActor,
                Value = 2,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });

            DefenderRespondArmRules.TryArmFromEnemyCardResolve(state, spider, wrap);
            Assert.AreEqual(1, state.DefenderRespondArms.Count);
            Assert.AreEqual(2, state.DefenderRespondArms[0].LockAttackerTurns);

            var hpDamage = 20;
            CombatantState recipient = spider;
            var ok = DefenderRespondArmRules.TryConsumeForIncomingPlayerAttack(
                state, warrior, ref recipient, ref hpDamage, new List<BattleEvent>(), out _);

            Assert.IsTrue(ok);
            Assert.AreEqual(10, hpDamage);
            Assert.AreEqual(2, warrior.AttackCardsLockedTurnsRemaining);
            Assert.IsTrue(CardLockRules.ShouldBlockPlayerCardPlanning(
                warrior, new CardInstanceState { CardType = CardType.Attack }));
        }

        [Test]
        public void SpiderFatalBind_DamagesTargetThenSelfHalfHp()
        {
            var state = BuildState(out var spider, out var warrior);
            warrior.Slot = FormationSlot.Middle;
            spider.Hp = 40;

            var card = new CardInstanceState
            {
                InstanceId = 3,
                DefinitionId = PassiveCardMechanicsRules.SpiderFatalBindCardId,
                DisplayName = "致命缠杀",
                CardType = CardType.Attack
            };
            card.Keywords.Add("exhaust");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 18,
                Reach = TargetReach.MiddleAndBack
            });

            var events = new List<BattleEvent>();
            var warriorHpBefore = warrior.Hp;
            EffectActionExecutor.ExecuteAll(state, spider, card, events, new BattleRng(1));
            Assert.Less(warrior.Hp, warriorHpBefore);

            PassiveCardMechanicsRules.OnSpiderFatalBindResolved(
                state, spider, card, events, new BattleRng(1));
            Assert.AreEqual(20, spider.Hp);
        }

        [Test]
        public void SpiderLadyTrait_IncreasesDamageOnPoisonedPlayers()
        {
            var state = BuildState(out var spider, out var warrior);
            spider.Traits.Add(MinionTraitCatalog.SpiderLadyPoisonVulnerability);
            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Poison, 10, -1, new List<BattleEvent>());

            var adjusted = MinionTraitRules.ApplySpiderPoisonVulnerability(state, warrior, 100);
            Assert.AreEqual(120, adjusted);
        }

        [Test]
        public void GargoyleTrait_AppliesBuffAtNextTurnStartFromPriorFirstCard()
        {
            var state = BuildState(out var gargoyle, out _);
            gargoyle.Id = "gargoyle";
            gargoyle.DisplayName = "石像鬼";
            gargoyle.CharacterDefinitionId = "char_gargoyle";
            gargoyle.Traits.Clear();
            gargoyle.Traits.Add(MinionTraitCatalog.GargoyleFirstCardStance);

            var attack = new CardInstanceState
            {
                InstanceId = 4,
                DefinitionId = "m_gargoyle_claw",
                CardType = CardType.Attack
            };
            MinionTraitRules.OnCardResolved(state, gargoyle, attack, new List<BattleEvent>());
            Assert.AreEqual(CardType.Attack, gargoyle.FirstCardTypeThisTurn);
            Assert.AreEqual(0, StatusRules.GetStatusStacks(gargoyle, StatusCatalog.AttackUpPercent));

            MinionTraitRules.ProcessTurnStart(state, new List<BattleEvent>());
            Assert.AreEqual(
                MinionTraitCatalog.GargoyleTraitPercentBonus,
                StatusRules.GetStatusStacks(gargoyle, StatusCatalog.AttackUpPercent));
            Assert.IsNull(gargoyle.FirstCardTypeThisTurn);
        }

        [Test]
        public void GargoyleEmpower_EnemyCasterPenalizesEnemyEnergyNotPlayer()
        {
            var state = BuildState(out var gargoyle, out _);
            gargoyle.Id = "gargoyle";
            gargoyle.Team = TeamSide.Enemy;
            var card = new CardInstanceState
            {
                InstanceId = 5,
                DefinitionId = "m_gargoyle_empower",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReducePlayerEnergyRegenNextTurn,
                Target = EffectTarget.Self,
                Value = 2
            });

            EffectActionExecutor.ExecuteAll(state, gargoyle, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(2, state.PendingEnemyEnergyRegenPenaltyNextTurn);
            Assert.AreEqual(0, state.PendingPlayerEnergyRegenPenaltyNextTurn);
        }

        [Test]
        public void GolemQuakeSlam_Uses40WhenNotHit()
        {
            var state = BuildState(out var golem, out var warrior);
            golem.Id = "golem";
            golem.DisplayName = "石傀儡";
            golem.HitThisTurn = false;

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 17,
                AlternateValue = 40,
                UseAlternateIfActorNotHitThisTurn = true,
                Reach = TargetReach.FrontAndMiddle
            };

            var power = CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior);
            Assert.AreEqual(40, power);

            golem.HitThisTurn = true;
            power = CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior);
            Assert.AreEqual(17, power);
        }

        [Test]
        public void GolemFist_DoublesWhenSelfBlockAbove20()
        {
            var state = BuildState(out var golem, out var warrior);
            golem.Id = "golem";
            golem.Block = 21;

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10,
                SelfBlockAboveThreshold = 20,
                AlternateValueIfSelfBlockAbove = 20,
                Reach = TargetReach.FrontAndMiddle
            };

            Assert.AreEqual(20, CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior));
            golem.Block = 20;
            Assert.AreEqual(10, CombatMechanicsRules.ComputeActionValueForTarget(state, action, golem, warrior));
        }

        [Test]
        public void GolemUnmovable_AppliesDamageReductionUnconditionally()
        {
            var state = BuildState(out var golem, out _);
            golem.Id = "golem";
            var card = new CardInstanceState
            {
                InstanceId = 6,
                DefinitionId = "m_golem_unmovable",
                CardType = CardType.Defense
            };
            card.Keywords.Add("parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlockFromLastDamagePercent,
                Target = EffectTarget.Self,
                Value = 90,
                Condition = ReactionConditionType.LastActionAttackOnSelf
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.DamageReduction,
                Stacks = 20,
                Duration = 3
            });

            EffectActionExecutor.ExecuteUnconditionalActions(state, golem, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(20, StatusRules.GetStatusStacks(golem, StatusCatalog.DamageReduction));
        }

        [Test]
        public void StoneGolemTrait_RetainsHalfBlockNextTurn()
        {
            var state = BuildState(out var golem, out _);
            golem.Id = "golem";
            golem.Traits.Add(MinionTraitCatalog.StoneGolemArmorRetain);
            golem.Block = 20;

            MinionTraitRules.PrepareTurnEndArmorRetain(state);
            Assert.AreEqual(10, golem.CarryOverBlock);

            golem.Block = 0;
            MinionTraitRules.ProcessTurnStart(state, new List<BattleEvent>());
            Assert.AreEqual(10, golem.Block);
            Assert.AreEqual(0, golem.CarryOverBlock);
        }

        static BattleState BuildState(out CombatantState spider, out CombatantState warrior)
        {
            var state = new BattleState { Config = new BattleConfig() };
            spider = new CombatantState
            {
                Id = "spider",
                DisplayName = "蜘蛛贵妇",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = "char_spider_lady",
                Hp = 60,
                MaxHp = 60,
                BaseAttack = 9,
                Speed = 7
            };
            warrior = new CombatantState
            {
                Id = "warrior",
                DisplayName = "战士",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                Hp = 50,
                MaxHp = 50,
                Speed = 5
            };
            state.Combatants.Add(spider);
            state.Combatants.Add(warrior);
            return state;
        }
    }
}
