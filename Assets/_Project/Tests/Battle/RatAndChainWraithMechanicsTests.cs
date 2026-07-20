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
    public sealed class RatAndChainWraithMechanicsTests
    {
        [Test]
        public void RatAmbush_DoublesDamageWhenTargetHasAnyStatus()
        {
            var state = BuildState(out var rat, out var warrior);
            StatusRules.ApplyStatus(state, warrior, StatusCatalog.Poison, 1, -1, new List<BattleEvent>());

            var action = new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 12,
                UseAlternateIfTargetHasAnyStatus = true,
                AlternateValue = 24,
                Reach = TargetReach.FrontAndMiddle
            };

            var plain = CombatMechanicsRules.ComputeActionValueForTarget(
                state, action, rat, new CombatantState { Id = "clean", Team = TeamSide.Player, Hp = 50, MaxHp = 50 });
            var boosted = CombatMechanicsRules.ComputeActionValueForTarget(state, action, rat, warrior);

            Assert.AreEqual(12, plain);
            Assert.AreEqual(24, boosted);
        }

        [Test]
        public void RatBurrow_OnConsume_SlowsAttacker()
        {
            var state = BuildState(out var rat, out var warrior);
            var events = new List<BattleEvent>();
            DefenderRespondArmRules.ArmMitigation(
                state, rat.Id, 70, slowAttackerStacks: 1, slowAttackerDuration: 2);

            var recipient = rat;
            var hp = 20;
            Assert.IsTrue(DefenderRespondArmRules.TryConsumeForIncomingPlayerAttack(
                state, warrior, ref recipient, ref hp, events, out _, new BattleRng(1)));

            Assert.AreEqual(6, hp);
            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.Slow));
            Assert.AreEqual(1, StatusRules.GetStatusStacks(warrior, StatusCatalog.Slow));
        }

        [Test]
        public void RatMorale_BuffsAllAllies()
        {
            var state = BuildState(out var rat, out _);
            var ally = new CombatantState
            {
                Id = "rat2",
                DisplayName = "鼠人2",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Back,
                CharacterDefinitionId = MinionTraitCatalog.RatCharacterId,
                Hp = 40,
                MaxHp = 40
            };
            state.Combatants.Add(ally);

            var card = new CardInstanceState { InstanceId = 1, DisplayName = "提振士气", CardType = CardType.Status };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllAllies,
                StatusId = StatusCatalog.SpeedUp,
                Stacks = 1,
                Duration = 2
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.AllAllies,
                StatusId = StatusCatalog.AttackUpPercent,
                Stacks = 20,
                Duration = 2
            });

            EffectActionExecutor.ExecuteUnconditionalActions(state, rat, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.IsTrue(StatusRules.HasStatus(rat, StatusCatalog.SpeedUp));
            Assert.IsTrue(StatusRules.HasStatus(ally, StatusCatalog.SpeedUp));
            Assert.AreEqual(20, StatusRules.GetStatusStacks(rat, StatusCatalog.AttackUpPercent));
            Assert.AreEqual(20, StatusRules.GetStatusStacks(ally, StatusCatalog.AttackUpPercent));
        }

        [Test]
        public void RatSwarmCall_OnDeath_SpawnsHalfHpRatAtSameSlot()
        {
            var state = BuildState(out var host, out _);
            host.CharacterDefinitionId = "char_dummy";
            host.MaxHp = 100;
            host.Hp = 1;
            host.Slot = FormationSlot.Middle;
            StatusRules.ApplyStatus(state, host, StatusCatalog.RatSwarmCall, 1, -1, new List<BattleEvent>());

            var events = new List<BattleEvent>();
            host.Hp = 0;
            CombatantDeathRules.OnCharacterDied(state, host, events);

            CombatantState spawned = null;
            foreach (var unit in state.Combatants)
            {
                if (unit.CharacterDefinitionId == MinionTraitCatalog.RatCharacterId && unit.IsAlive)
                    spawned = unit;
            }

            Assert.IsNotNull(spawned);
            Assert.AreEqual(50, spawned.MaxHp);
            Assert.AreEqual(50, spawned.Hp);
            Assert.AreEqual(FormationSlot.Middle, spawned.Slot);
            Assert.AreEqual(host.Team, spawned.Team);
        }

        [Test]
        public void ChainGrudge_AppliesSlowToSelfAndRandomEnemy()
        {
            var state = BuildChainState(out var wraith, out var warrior, out var mage);
            // 去掉特质，避免同步干扰「卡面本身」断言
            wraith.Traits.Clear();

            var card = new CardInstanceState { InstanceId = 1, DisplayName = "怨气缠绕", CardType = CardType.Status };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.Self,
                StatusId = StatusCatalog.Slow,
                Stacks = 1,
                Duration = 2
            });
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyStatus,
                Target = EffectTarget.RandomEnemy,
                StatusId = StatusCatalog.Slow,
                Stacks = 1,
                Duration = 2
            });

            EffectActionExecutor.ExecuteUnconditionalActions(
                state, wraith, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.IsTrue(StatusRules.HasStatus(wraith, StatusCatalog.Slow));
            var playerSlowed = StatusRules.HasStatus(warrior, StatusCatalog.Slow)
                               || StatusRules.HasStatus(mage, StatusCatalog.Slow);
            Assert.IsTrue(playerSlowed);
        }

        [Test]
        public void ChainWraithTrait_MirrorsDebuffToPlayers_AndClearsTogether()
        {
            var state = BuildChainState(out var wraith, out var warrior, out var mage);
            var events = new List<BattleEvent>();
            StatusRules.ApplyStatus(state, wraith, StatusCatalog.Slow, 1, 2, events);

            Assert.IsTrue(StatusRules.HasStatus(warrior, StatusCatalog.Slow));
            Assert.IsTrue(StatusRules.HasStatus(mage, StatusCatalog.Slow));

            // 模拟持续到期
            var slow = StatusRules.FindStatus(wraith, StatusCatalog.Slow);
            Assert.IsNotNull(slow);
            slow.RemainingTurns = 1;
            StatusRules.ProcessEndOfTurnDurations(state, events);

            Assert.IsFalse(StatusRules.HasStatus(wraith, StatusCatalog.Slow));
            Assert.IsFalse(StatusRules.HasStatus(warrior, StatusCatalog.Slow));
            Assert.IsFalse(StatusRules.HasStatus(mage, StatusCatalog.Slow));
        }

        [Test]
        public void GrudgeGuard_FailedRespond_PoisonsSelf()
        {
            var state = BuildChainState(out var wraith, out _, out _);
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DisplayName = "怨气护体",
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
                StatusId = StatusCatalog.Poison,
                Stacks = 5,
                Duration = -1,
                Condition = ReactionConditionType.RespondArmFailed
            });

            EffectActionExecutor.ExecuteFailedRespondActions(
                state, wraith, card, new List<BattleEvent>(), new BattleRng(1));

            Assert.AreEqual(5, StatusRules.GetStatusStacks(wraith, StatusCatalog.Poison));
        }

        static BattleState BuildState(out CombatantState rat, out CombatantState warrior)
        {
            var state = new BattleState { Config = new BattleConfig() };
            rat = new CombatantState
            {
                Id = "rat",
                DisplayName = "鼠人",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Front,
                CharacterDefinitionId = MinionTraitCatalog.RatCharacterId,
                Hp = 55,
                MaxHp = 55,
                BaseAttack = 9,
                Speed = 6
            };
            rat.Traits.Add(MinionTraitCatalog.RatPackAttackOnAllyDeath);
            warrior = new CombatantState
            {
                Id = "warrior",
                DisplayName = "战士",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                Hp = 50,
                MaxHp = 50
            };
            state.Combatants.Add(rat);
            state.Combatants.Add(warrior);
            return state;
        }

        static BattleState BuildChainState(
            out CombatantState wraith,
            out CombatantState warrior,
            out CombatantState mage)
        {
            var state = new BattleState { Config = new BattleConfig() };
            wraith = new CombatantState
            {
                Id = "wraith",
                DisplayName = "锁链怨灵",
                Team = TeamSide.Enemy,
                Slot = FormationSlot.Middle,
                CharacterDefinitionId = "char_chain_wraith",
                Hp = 65,
                MaxHp = 65
            };
            wraith.Traits.Add(MinionTraitCatalog.ChainWraithDebuffShare);
            warrior = new CombatantState
            {
                Id = "warrior",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                Hp = 50,
                MaxHp = 50
            };
            mage = new CombatantState
            {
                Id = "mage",
                Team = TeamSide.Player,
                Slot = FormationSlot.Middle,
                Hp = 40,
                MaxHp = 40
            };
            state.Combatants.Add(wraith);
            state.Combatants.Add(warrior);
            state.Combatants.Add(mage);
            return state;
        }
    }
}
