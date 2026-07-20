using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    /// <summary>模拟训练场：假人排队女王卡 → 提交回合 → 断言真实结算结果。</summary>
    public sealed class GhostQueenDummyResolveTests
    {
        [Test]
        public void DummyPlaysQueenCurse_PoisonsAllPlayers()
        {
            var engine = BuildTrainingEngine(out var dummy, out var warrior, out var mage);
            EnqueueEnemyCard(engine, BuildCurseCard());
            PlayOnePlayerAttack(engine, warrior);

            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.Poison), "诅咒应对战士上毒");
            Assert.IsTrue(StatusRules.HasStatus(mage, StatusCatalog.Poison), "诅咒应对法师上毒");
            Assert.GreaterOrEqual(StatusRules.GetStatusStacks(warrior, StatusCatalog.Poison), 6);
        }

        [Test]
        public void DummyPlaysSoulDrain_AppliesPenaltyAndIcon()
        {
            var engine = BuildTrainingEngine(out _, out var warrior, out _);
            engine.State.EnergyCurrent = 0;
            engine.State.IsFirstPlayerTurn = false;
            EnqueueEnemyCard(engine, BuildSoulDrainCard());
            PlayOnePlayerAttack(engine, warrior);

            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.SoulDrain));
            Assert.AreEqual(2, engine.State.PendingPlayerEnergyRegenPenaltyNextTurn);

            EnergyRules.ApplyTurnStartRegen(engine.State);
            Assert.AreEqual(2, engine.State.EnergyCurrent);
            Assert.IsFalse(StatusRules.HasStatus(warrior, StatusCatalog.SoulDrain));
        }

        [Test]
        public void DummyPlaysDeterrence_LocksPlayer()
        {
            var engine = BuildTrainingEngine(out _, out var warrior, out var mage);
            EnqueueEnemyCard(engine, BuildDeterrenceCard());
            PlayOnePlayerAttack(engine, warrior);

            Assert.IsFalse(warrior.SkipRemainingPlaysThisTurn, "威慑不应打断本回合");
            Assert.IsFalse(mage.SkipRemainingPlaysThisTurn, "威慑不应打断本回合");
            Assert.IsTrue(
                StatusRules.HasStatus(warrior, StatusCatalog.Deterrence)
                || StatusRules.HasStatus(mage, StatusCatalog.Deterrence),
                "威慑应施加状态");
            Assert.IsTrue(
                warrior.CardsLockedTurnsRemaining >= 2 || mage.CardsLockedTurnsRemaining >= 2,
                "威慑应锁定下回合出牌");
        }

        [Test]
        public void DummyPlaysCommand_RedirectsIncomingAttack()
        {
            var engine = BuildTrainingEngine(out var dummy, out var warrior, out var mage);
            mage.Hp = 40;
            mage.MaxHp = 40;
            warrior.Hp = 50;
            warrior.MaxHp = 50;
            dummy.Hp = 999;
            dummy.MaxHp = 999;
            dummy.Block = 0;

            EnqueueEnemyCard(engine, BuildCommandCard());

            // 玩家打假人：命令应先武装，再把伤害×2转给我方
            var attack = BuildPlayerAttackCard();
            var attackInstance = engine.AddCardTemplateToHand(attack);
            Assert.IsNotNull(attackInstance);
            // 绑到战士并指定打假人
            attackInstance.OwnerCombatantId = warrior.Id;
            engine.State.ResolutionTargets[attackInstance.InstanceId] = dummy.Id;

            engine.Draft.TrySelectCard(attackInstance.InstanceId);
            Assert.IsTrue(engine.CommitPlayerPlan());

            Assert.AreEqual(999, dummy.Hp, "假人不应掉血（伤害已转嫁）");
            Assert.IsTrue(mage.Hp < 40 || warrior.Hp < 50, "转嫁伤害应打到我方");
        }

        [Test]
        public void CommandArmedLate_StillRedirectsNextTurn()
        {
            var engine = BuildTrainingEngine(out var dummy, out var warrior, out var mage);
            mage.Hp = 40;
            mage.MaxHp = 40;
            warrior.Hp = 50;
            warrior.MaxHp = 50;
            dummy.Hp = 999;
            dummy.MaxHp = 999;
            dummy.Speed = 1; // 假人最后行动

            EnqueueEnemyCard(engine, BuildCommandCard());
            // 本回合不出攻击，避免应对重排把命令提到攻击前并立刻消耗
            var guard = new CardTemplate
            {
                DefinitionId = "test_guard",
                DisplayName = "测试格挡",
                OwnerCharacterId = "char_warrior",
                Cost = 0,
                CardType = CardType.Defense
            };
            guard.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.GainBlock,
                Target = EffectTarget.Self,
                Value = 5
            });
            var guardInstance = engine.AddCardTemplateToHand(guard);
            Assert.IsNotNull(guardInstance);
            guardInstance.OwnerCombatantId = warrior.Id;
            Assert.IsTrue(engine.Draft.TrySelectCard(guardInstance.InstanceId));
            Assert.IsTrue(engine.CommitPlayerPlan());
            engine.FlushPendingEndOfTurn();

            Assert.Greater(engine.State.DefenderRespondArms.Count, 0, "命令武装应跨入下一回合");

            var attack = BuildPlayerAttackCard();
            var attackInstance = engine.AddCardTemplateToHand(attack);
            Assert.IsNotNull(attackInstance);
            attackInstance.OwnerCombatantId = warrior.Id;
            engine.State.ResolutionTargets[attackInstance.InstanceId] = dummy.Id;
            Assert.IsTrue(engine.Draft.TrySelectCard(attackInstance.InstanceId));
            Assert.IsTrue(engine.CommitPlayerPlan());

            Assert.AreEqual(999, dummy.Hp, "下回合转嫁仍应保护假人");
            Assert.IsTrue(mage.Hp < 40 || warrior.Hp < 50, "下回合转嫁应打到我方");
        }

        static BattleEngine BuildTrainingEngine(
            out CombatantState dummy,
            out CombatantState warrior,
            out CombatantState mage)
        {
            var config = new BattleConfig
            {
                Seed = 42,
                EnergyCap = 8,
                TurnStartEnergyRegen = 4,
                HandLimit = 8,
                CardsDrawnPerTurn = 0,
                EnemyCardsDrawnPerTurn = 0,
                ManualEnemyIntentsOnly = true,
                SkipFloorScaling = true
            };

            config.Combatants.Add(new CombatantConfig
            {
                Id = "warrior",
                DisplayName = "战士",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = "char_warrior",
                MaxHp = 50,
                BaseAttack = 20,
                BaseDefense = 0,
                Speed = 5
            });
            config.Combatants.Add(new CombatantConfig
            {
                Id = "mage",
                DisplayName = "法师",
                Team = TeamSide.Player,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = "char_mage",
                MaxHp = 40,
                BaseAttack = 10,
                BaseDefense = 0,
                Speed = 4
            });
            config.Combatants.Add(new CombatantConfig
            {
                Id = "enemy_dummy_0",
                DisplayName = "训练假人",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = "char_dummy",
                MaxHp = 999,
                BaseAttack = 0,
                BaseDefense = 0,
                Speed = 99
            });

            var engine = new BattleEngine(config);
            engine.StartBattle();
            dummy = engine.State.GetCombatant("enemy_dummy_0");
            warrior = engine.State.GetCombatant("warrior");
            mage = engine.State.GetCombatant("mage");
            Assert.IsNotNull(dummy);
            Assert.IsNotNull(warrior);
            Assert.IsNotNull(mage);
            return engine;
        }

        static void EnqueueEnemyCard(BattleEngine engine, CardTemplate template)
        {
            var instance = engine.EnqueueEnemyIntentCard(template);
            Assert.IsNotNull(instance, "假人意图入队失败");
        }

        static void PlayOnePlayerAttack(BattleEngine engine, CombatantState warrior)
        {
            var attack = BuildPlayerAttackCard();
            var instance = engine.AddCardTemplateToHand(attack);
            Assert.IsNotNull(instance);
            instance.OwnerCombatantId = warrior.Id;
            Assert.IsTrue(engine.Draft.TrySelectCard(instance.InstanceId));
            Assert.IsTrue(engine.CommitPlayerPlan());
        }

        static CardTemplate BuildCurseCard()
        {
            var card = new CardTemplate
            {
                DefinitionId = "m_queen_curse",
                DisplayName = "女王的诅咒",
                OwnerCharacterId = "char_ghost_queen",
                Cost = 2,
                CardType = CardType.Status
            };
            card.Keywords.Add("poison");
            card.Keywords.Add("aoe");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Poison,
                Stacks = 6,
                Duration = -1,
                Reach = TargetReach.Any
            });
            return card;
        }

        static CardTemplate BuildSoulDrainCard()
        {
            var card = new CardTemplate
            {
                DefinitionId = "m_queen_soul_drain",
                DisplayName = "摄魂",
                OwnerCharacterId = "char_ghost_queen",
                Cost = 1,
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ReducePlayerEnergyRegenNextTurn,
                Target = EffectTarget.AllEnemies,
                Value = 2
            });
            return card;
        }

        static CardTemplate BuildDeterrenceCard()
        {
            var card = new CardTemplate
            {
                DefinitionId = "m_queen_deterrence",
                DisplayName = "女王的威慑",
                OwnerCharacterId = "char_ghost_queen",
                Cost = 1,
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.LockRandomPlayerPlaysThisTurn,
                Target = EffectTarget.DefaultEnemy
            });
            return card;
        }

        static CardTemplate BuildCommandCard()
        {
            var card = new CardTemplate
            {
                DefinitionId = "m_queen_command",
                DisplayName = "女王的命令",
                OwnerCharacterId = "char_ghost_queen",
                Cost = 2,
                CardType = CardType.Defense
            };
            card.Keywords.Add("parry");
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ArmRespondDamageRedirect,
                Target = EffectTarget.Self,
                Condition = ReactionConditionType.None
            });
            return card;
        }

        static CardTemplate BuildPlayerAttackCard()
        {
            var card = new CardTemplate
            {
                DefinitionId = "test_slash",
                DisplayName = "测试斩击",
                OwnerCharacterId = "char_warrior",
                Cost = 0,
                CardType = CardType.Attack
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 10,
                Reach = TargetReach.Any
            });
            return card;
        }
    }
}
