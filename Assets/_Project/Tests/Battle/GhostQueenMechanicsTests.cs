using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Reactions;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Core;
using Grimhand.Expedition;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public sealed class GhostQueenMechanicsTests
    {
        [Test]
        public void Floor10Boss_Roll_ProducesBothKindsOverSeeds()
        {
            var sawKing = false;
            var sawQueen = false;
            for (var seed = 1; seed <= 40; seed++)
            {
                var kind = Floor10BossEncounterBuilder.RollBossKind(new BattleRng(seed));
                if (kind == Floor10BossKind.SkeletonKing)
                    sawKing = true;
                if (kind == Floor10BossKind.GhostQueen)
                    sawQueen = true;
            }

            Assert.IsTrue(sawKing);
            Assert.IsTrue(sawQueen);
        }

        [Test]
        public void GhostQueenEnrage_TriggersOnceBelow120()
        {
            var state = BuildQueenState(out var queen);
            var events = new List<BattleEvent>();

            queen.Hp = 125;
            DamageRules.ApplyDamage(
                state,
                BuildPlayerAttacker(state),
                queen,
                10,
                CardType.Attack,
                events,
                rng: new BattleRng(1));

            Assert.IsTrue(queen.GhostQueenEnrageTriggered);
            Assert.IsTrue(StatusRules.HasStatus(queen, StatusCatalog.Ethereal));
            Assert.AreEqual(1, state.PendingBossBonusHandsNextTurn.Count);
        }

        [Test]
        public void Ethereal_CapsDamageToOne()
        {
            var state = BuildQueenState(out var queen);
            StatusRules.ApplyStatus(state, queen, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());
            queen.Hp = 200;

            DamageRules.ApplyDamage(
                state,
                BuildPlayerAttacker(state),
                queen,
                50,
                CardType.Attack,
                new List<BattleEvent>(),
                rng: new BattleRng(1));

            Assert.AreEqual(199, queen.Hp);
        }

        [Test]
        public void QueenCommand_RedirectsDoubledDamageToAlly()
        {
            var state = BuildQueenState(out var queen);
            state.Combatants.Add(new CombatantState
            {
                Id = "mage",
                DisplayName = "法师",
                Team = TeamSide.Player,
                Slot = FormationSlot.Middle,
                Hp = 40,
                MaxHp = 40,
                Defense = 0
            });

            DefenderRespondArmRules.ArmRedirectDouble(state, queen.Id);
            queen.Hp = 200;
            queen.Block = 50;

            DamageRules.ApplyDamage(
                state,
                BuildPlayerAttacker(state),
                queen,
                10,
                CardType.Attack,
                new List<BattleEvent>(),
                rng: new BattleRng(1));

            Assert.AreEqual(200, queen.Hp, "转伤后女王不应掉血");
            Assert.AreEqual(50, queen.Block, "转伤应在扣护甲前，女王护甲不应被消耗");
            var mage = state.GetCombatant("mage");
            var warrior = state.GetCombatant("warrior");
            Assert.IsTrue(mage.Hp < 40 || warrior.Hp < 50, "伤害应转嫁给随机玩家并翻倍");
        }

        [Test]
        public void QueenDeterrence_LocksPlayerAndAppliesStatus()
        {
            var state = BuildQueenState(out var queen);
            var warrior = BuildPlayerAttacker(state);
            var events = new List<BattleEvent>();

            EffectActionExecutor.ExecuteUnconditionalActions(
                state,
                queen,
                BuildSimpleCard(EffectActionType.LockRandomPlayerPlaysThisTurn, 0),
                events,
                new BattleRng(1));

            Assert.IsFalse(warrior.SkipRemainingPlaysThisTurn, "威慑不应打断本回合");
            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.Deterrence));
            Assert.GreaterOrEqual(warrior.CardsLockedTurnsRemaining, 2, "应锁定下回合出牌");
        }

        [Test]
        public void QueenCurse_AppliesPoisonToAllPlayers()
        {
            var state = BuildQueenState(out var queen);
            var warrior = BuildPlayerAttacker(state);
            state.Combatants.Add(new CombatantState
            {
                Id = "mage",
                Team = TeamSide.Player,
                Slot = FormationSlot.Middle,
                Hp = 40,
                MaxHp = 40
            });

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "诅咒",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllEnemies,
                StatusId = StatusCatalog.Poison,
                Stacks = 6,
                Duration = -1,
                Reach = TargetReach.Any
            });

            EffectActionExecutor.ExecuteUnconditionalActions(
                state, queen, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.Poison));
            Assert.IsTrue(StatusRules.HasStatus(state.GetCombatant("mage"), StatusCatalog.Poison));
        }

        [Test]
        public void SoulDrain_ReducesNextTurnEnergyRegen()
        {
            var state = new BattleState
            {
                Config = new BattleConfig
                {
                    TurnStartEnergyRegen = 4,
                    EnergyCap = 8
                },
                EnergyCurrent = 0,
                EnergyMax = 8,
                IsFirstPlayerTurn = false
            };
            var player = new CombatantState
            {
                Id = "warrior",
                Team = TeamSide.Player,
                Hp = 50,
                MaxHp = 50
            };
            state.Combatants.Add(player);

            EffectActionExecutor.ExecuteUnconditionalActions(
                state,
                new CombatantState { Id = "queen", Team = TeamSide.Enemy },
                BuildSimpleCard(EffectActionType.ReducePlayerEnergyRegenNextTurn, 2),
                new List<BattleEvent>(),
                new BattleRng(1));

            Assert.IsTrue(StatusRules.HasStatus(player, StatusCatalog.SoulDrain));
            EnergyRules.ApplyTurnStartRegen(state);
            Assert.AreEqual(2, state.EnergyCurrent);
            Assert.IsFalse(StatusRules.HasStatus(player, StatusCatalog.SoulDrain));
        }

        [Test]
        public void BoneWorkshop_SummonsImmediatelyOnApply()
        {
            var state = BuildBossBattleStateForQueenTest();
            var king = state.GetCombatant("king");
            var events = new List<BattleEvent>();

            StatusRules.ApplyStatus(state, king, StatusCatalog.BoneWorkshop, 1, -1, events);

            var skull = 0;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == SummonRules.ExplosiveSkullCharacterId && unit.IsAlive)
                    skull++;
            }

            Assert.AreEqual(1, skull);
            Assert.AreEqual(FormationSlot.Middle, state.Combatants[state.Combatants.Count - 1].Slot);
        }

        static BattleState BuildBossBattleStateForQueenTest()
        {
            var state = new BattleState
            {
                Config = new BattleConfig()
            };
            state.Config.SummonTemplates[SummonRules.ExplosiveSkullCharacterId] = new CombatantConfig
            {
                CharacterDefinitionId = SummonRules.ExplosiveSkullCharacterId,
                DisplayName = "易爆骷髅头",
                Team = TeamSide.Enemy,
                MaxHp = 20,
                BaseDefense = 5,
                Speed = 2
            };
            state.Combatants.Add(new CombatantState
            {
                Id = "king",
                DisplayName = "骷髅王",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                Hp = 100,
                MaxHp = 100
            });
            return state;
        }

        static BattleState BuildQueenState(out CombatantState queen)
        {
            var state = new BattleState
            {
                Config = new BattleConfig()
            };

            queen = new CombatantState
            {
                Id = "queen",
                DisplayName = "幽灵女王",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = GhostQueenBossEncounterBuilder.CharacterId,
                Hp = 360,
                MaxHp = 360,
                Defense = 8
            };
            queen.Traits.Add(CharacterTraitCatalog.GhostQueenEnrage);
            state.Combatants.Add(queen);
            return state;
        }

        static CombatantState BuildPlayerAttacker(BattleState state)
        {
            var attacker = new CombatantState
            {
                Id = "warrior",
                DisplayName = "战士",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                Attack = 20,
                Hp = 50,
                MaxHp = 50
            };
            state.Combatants.Add(attacker);
            return attacker;
        }

        static CardInstanceState BuildSimpleCard(EffectActionType type, int value)
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "test",
                CardType = CardType.Status
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = type,
                Target = EffectTarget.AllEnemies,
                Value = value
            });
            return card;
        }
    }
}
