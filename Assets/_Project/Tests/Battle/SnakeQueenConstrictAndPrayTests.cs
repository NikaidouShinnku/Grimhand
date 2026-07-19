using System.Collections.Generic;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Battle.Status;
using Grimhand.Battle.V09;
using Grimhand.Battle.V091;
using Grimhand.Core;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class SnakeQueenConstrictAndPrayTests
    {
        [Test]
        public void Constrict_LocksCaster_AllowsWhitelistedCards()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 100, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(enemy);

            V09NewMechanicsRules.ApplyConstrict(state, queen, enemy, 35, 2, new List<BattleEvent>());
            Assert.IsTrue(queen.IsConstrictCardsLocked);
            Assert.IsFalse(queen.IsHardCardsLocked);
            Assert.IsTrue(StatusRules.HasStatus(enemy, StatusCatalog.Constrict));
            Assert.IsTrue(StatusRules.HasStatus(queen, StatusCatalog.Constrict));
            Assert.AreEqual(35, StatusRules.FindStatus(enemy, StatusCatalog.Constrict).Stacks);

            var blocked = new CardInstanceState { DefinitionId = "v_scale_harden", CardType = CardType.Defense };
            var allowed = new CardInstanceState
            {
                DefinitionId = "v_tail_strike",
                CardType = CardType.Attack,
                Keywords = { CardLockRules.UsableWhileConstrictedKeyword }
            };
            Assert.IsTrue(CardLockRules.ShouldBlockPlayerCardPlanning(queen, blocked));
            Assert.IsFalse(CardLockRules.ShouldBlockPlayerCardPlanning(queen, allowed));
        }

        [Test]
        public void PrayHardLock_BlocksEvenWhitelistedCards()
        {
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            CardLockRules.ApplyLock(queen, 2);

            var allowedDuringConstrict = new CardInstanceState
            {
                DefinitionId = "v_tail_strike",
                CardType = CardType.Attack,
                Keywords = { CardLockRules.UsableWhileConstrictedKeyword }
            };
            Assert.IsTrue(CardLockRules.ShouldBlockPlayerCardPlanning(queen, allowedDuringConstrict));
        }

        [Test]
        public void AllSnakesHeart_ConstrainsAllEnemies_LocksCasterOnce()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var a = Unit("a", TeamSide.Enemy, FormationSlot.Front, 80, 3);
            var b = Unit("b", TeamSide.Enemy, FormationSlot.Middle, 80, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(a);
            state.Combatants.Add(b);

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "v_all_snakes_heart",
                CardType = CardType.Attack,
                Keywords = { "aoe" }
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.ApplyConstrict,
                Target = EffectTarget.AllEnemies,
                Value = 20,
                Duration = 2,
                Reach = TargetReach.Any
            });

            EffectActionExecutor.ExecuteAll(state, queen, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(20, StatusRules.FindStatus(a, StatusCatalog.Constrict).Stacks);
            Assert.AreEqual(20, StatusRules.FindStatus(b, StatusCatalog.Constrict).Stacks);
            Assert.IsTrue(queen.IsConstrictCardsLocked);
            Assert.AreEqual(2, queen.ConstrictLockTurnsRemaining);
        }

        [Test]
        public void Constrict_ClearsWhenCasterDies()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 100, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(enemy);

            V09NewMechanicsRules.ApplyConstrict(state, queen, enemy, 35, 2, new List<BattleEvent>());
            queen.Hp = 0;
            V09NewMechanicsRules.OnConstrictCasterDied(state, queen, new List<BattleEvent>());
            Assert.IsFalse(StatusRules.HasStatus(enemy, StatusCatalog.Constrict));
            Assert.IsFalse(StatusRules.HasStatus(queen, StatusCatalog.Constrict));
            Assert.AreEqual(0, queen.ConstrictLockTurnsRemaining);
        }

        [Test]
        public void Constrict_ReleasesCasterWhenSingleTargetDies()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 100, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(enemy);

            V09NewMechanicsRules.ApplyConstrict(state, queen, enemy, 35, 2, new List<BattleEvent>());
            Assert.IsTrue(queen.IsConstrictCardsLocked);

            enemy.Hp = 0;
            V09NewMechanicsRules.OnConstrictTargetDied(state, enemy, new List<BattleEvent>());
            Assert.IsFalse(queen.IsConstrictCardsLocked);
            Assert.IsFalse(StatusRules.HasStatus(queen, StatusCatalog.Constrict));
        }

        [Test]
        public void Constrict_Aoe_KeepsLockUntilAllTargetsDie()
        {
            var state = new BattleState();
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            var a = Unit("a", TeamSide.Enemy, FormationSlot.Front, 80, 3);
            var b = Unit("b", TeamSide.Enemy, FormationSlot.Middle, 80, 3);
            state.Combatants.Add(queen);
            state.Combatants.Add(a);
            state.Combatants.Add(b);

            V09NewMechanicsRules.ApplyConstrict(state, queen, a, 20, 2, new List<BattleEvent>(), applyCasterLock: false);
            V09NewMechanicsRules.ApplyConstrict(state, queen, b, 20, 2, new List<BattleEvent>(), applyCasterLock: true);
            Assert.IsTrue(queen.IsConstrictCardsLocked);

            a.Hp = 0;
            V09NewMechanicsRules.OnConstrictTargetDied(state, a, new List<BattleEvent>());
            Assert.IsTrue(queen.IsConstrictCardsLocked);

            b.Hp = 0;
            V09NewMechanicsRules.OnConstrictTargetDied(state, b, new List<BattleEvent>());
            Assert.IsFalse(queen.IsConstrictCardsLocked);
        }

        [Test]
        public void QueenKiss_ConvertsAllPoisonBuckets_AfterPending()
        {
            var state = new BattleState { QueenKissConversionPending = true };
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 100, 3);
            state.Combatants.Add(enemy);
            StatusRules.ApplyStatus(state, enemy, StatusCatalog.Poison, 30, -1, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, enemy, StatusCatalog.Poison, 4, 2, new List<BattleEvent>());

            V091MechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            Assert.IsFalse(StatusRules.HasStatus(enemy, StatusCatalog.Poison));
            Assert.AreEqual(34, StatusRules.GetStatusStacks(enemy, StatusCatalog.Vulnerable));
            Assert.IsFalse(state.QueenKissConversionPending);
        }

        [Test]
        public void PrayAncient_TokenAppearsSameTurnChannelingEnds()
        {
            var state = new BattleState();
            state.Config = new BattleConfig { HandLimit = 10 };
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            state.Combatants.Add(queen);

            // 易伤/禁牌/启动均 3 回合：前两回合只禁制，第三回合解除时才正规入手回应。
            StatusRules.ApplyStatus(state, queen, StatusCatalog.Vulnerable, 50, 3, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, queen, StatusCatalog.SnakeGodChanneling, 1, 3, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, queen, StatusCatalog.PrayAncientSnakeGod, 1, -1, new List<BattleEvent>());
            CardLockRules.ApplyLock(queen, 3);

            for (var turn = 1; turn <= 2; turn++)
            {
                V09NewMechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
                CardLockRules.ProcessTurnStart(queen);
                StatusRules.ProcessTurnStartDurations(state, new List<BattleEvent>());
                V09NewMechanicsRules.ProcessSnakeGodResponseHand(state, new List<BattleEvent>());
                Assert.AreEqual(0, state.GetHand(TeamSide.Player).Count, $"启动第 {turn} 回合不应发回应");
                Assert.IsTrue(queen.IsHardCardsLocked, $"启动第 {turn} 回合应仍禁出牌");
                Assert.IsTrue(StatusRules.HasStatus(queen, StatusCatalog.SnakeGodChanneling));
            }

            V09NewMechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            CardLockRules.ProcessTurnStart(queen);
            StatusRules.ProcessTurnStartDurations(state, new List<BattleEvent>());
            V09NewMechanicsRules.ProcessSnakeGodResponseHand(state, new List<BattleEvent>());
            Assert.IsFalse(StatusRules.HasStatus(queen, StatusCatalog.SnakeGodChanneling));
            Assert.IsFalse(queen.IsHardCardsLocked);
            Assert.AreEqual(1, state.GetHand(TeamSide.Player).Count);
            Assert.AreEqual(V09NewMechanicsRules.SnakeGodResponseCardId, state.GetHand(TeamSide.Player)[0].DefinitionId);
        }

        [Test]
        public void PrayAncient_NoTokenWhileChanneling()
        {
            var state = new BattleState();
            state.Config = new BattleConfig { HandLimit = 10 };
            var queen = Unit("queen", TeamSide.Player, FormationSlot.Middle, 55, 8);
            state.Combatants.Add(queen);

            StatusRules.ApplyStatus(state, queen, StatusCatalog.Vulnerable, 50, 3, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, queen, StatusCatalog.SnakeGodChanneling, 1, 3, new List<BattleEvent>());
            StatusRules.ApplyStatus(state, queen, StatusCatalog.PrayAncientSnakeGod, 1, -1, new List<BattleEvent>());
            CardLockRules.ApplyLock(queen, 3);

            V09NewMechanicsRules.ProcessTurnStart(state, new List<BattleEvent>(), new BattleRng(1));
            StatusRules.ProcessTurnStartDurations(state, new List<BattleEvent>());
            V09NewMechanicsRules.ProcessSnakeGodResponseHand(state, new List<BattleEvent>());
            Assert.AreEqual(0, state.GetHand(TeamSide.Player).Count);

            StatusRules.RemoveAllStatus(queen, StatusCatalog.SnakeGodChanneling, new List<BattleEvent>());
            V09NewMechanicsRules.ProcessSnakeGodResponseHand(state, new List<BattleEvent>());
            Assert.AreEqual(1, state.GetHand(TeamSide.Player).Count);
            Assert.AreEqual(V09NewMechanicsRules.SnakeGodResponseCardId, state.GetHand(TeamSide.Player)[0].DefinitionId);
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
