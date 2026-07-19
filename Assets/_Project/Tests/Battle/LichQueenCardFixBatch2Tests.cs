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
    public class LichQueenCardFixBatch2Tests
    {
        [Test]
        public void EtherealShield_WithEthereal_DelaysBlock_AndSurvivesOneClear()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = V091MechanicsRules.EtherealShieldCardId,
                CardType = CardType.Defense
            };

            var events = new List<BattleEvent>();
            Assert.IsTrue(SpecialCardRules.TryResolve(state, lich, card, events, new BattleRng(1)));
            Assert.AreEqual(0, lich.Block);
            Assert.IsFalse(events.Exists(e => e.Kind == BattleEventKind.BlockGained));
            Assert.AreEqual(8, state.PendingDelayedBlockByCombatantId[lich.Id]);

            V091MechanicsRules.ProcessTurnStart(state, events, new BattleRng(1));
            Assert.AreEqual(8, lich.Block);
            Assert.IsTrue(state.RetainBlockOnceCombatantIds.Contains(lich.Id));

            // 模拟回合末清甲：应保留一次
            var retained = PassiveCardMechanicsRules.GetFinalBulwarkRetainedBlock(lich);
            if (state.RetainBlockOnceCombatantIds.Remove(lich.Id))
                retained = System.Math.Max(retained, lich.Block);
            lich.Block = retained;
            Assert.AreEqual(8, lich.Block);

            // 再清一次才掉
            lich.Block = PassiveCardMechanicsRules.GetFinalBulwarkRetainedBlock(lich);
            Assert.AreEqual(0, lich.Block);
        }

        [Test]
        public void RealmSeal_NeedsNoTarget_AndNullifiesNextEnemyCard()
        {
            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_realm_seal",
                CardType = CardType.Status,
                Keywords = { "exhaust" }
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.SealNextEnemyCard,
                Target = EffectTarget.Self
            });
            Assert.IsFalse(CardRules.RequiresManualTarget(card));

            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);
            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(1, state.PendingEnemyCardSeals);
        }

        [Test]
        public void RealmBurst_WithEthereal_Deals30AndRemovesEthereal()
        {
            var state = new BattleState();
            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            var enemy = Unit("enemy", TeamSide.Enemy, FormationSlot.Front, 50, 3);
            state.Combatants.Add(lich);
            state.Combatants.Add(enemy);
            StatusRules.ApplyStatus(state, lich, StatusCatalog.Ethereal, 1, 1, new List<BattleEvent>());

            var card = new CardInstanceState
            {
                InstanceId = 1,
                DefinitionId = "l_realm_burst",
                CardType = CardType.Attack
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DealDamage,
                Target = EffectTarget.DefaultEnemy,
                Value = 15,
                Reach = TargetReach.FrontAndMiddle
            });
            state.CardsById[1] = card;
            state.ResolutionTargets[1] = enemy.Id;

            EffectActionExecutor.ExecuteAll(state, lich, card, new List<BattleEvent>(), new BattleRng(1));
            Assert.AreEqual(20, enemy.Hp);
            Assert.IsFalse(StatusRules.HasStatus(lich, StatusCatalog.Ethereal));
        }

        [Test]
        public void PsionicScry_Confirm_DiscardsSelected_ReturnsRestToTop()
        {
            var state = new BattleState { Config = new BattleConfig { HandLimit = 10 } };
            var a = new CardInstanceState { InstanceId = 1, DefinitionId = "a", DisplayName = "A" };
            var b = new CardInstanceState { InstanceId = 2, DefinitionId = "b", DisplayName = "B" };
            var c = new CardInstanceState { InstanceId = 3, DefinitionId = "c", DisplayName = "C" };
            var d = new CardInstanceState { InstanceId = 4, DefinitionId = "d", DisplayName = "D" };
            state.PlayerDrawPile.AddRange(new[] { a, b, c, d });

            var lich = Unit("lich", TeamSide.Player, FormationSlot.Middle, 40, 8);
            state.Combatants.Add(lich);
            var card = new CardInstanceState
            {
                InstanceId = 99,
                DefinitionId = "l_psionic_scry",
                CardType = CardType.Status,
                Keywords = { "quick_start" }
            };

            Assert.IsTrue(SpecialCardRules.TryResolve(state, lich, card, new List<BattleEvent>(), new BattleRng(1)));
            Assert.IsTrue(state.AwaitingPsionicScry);
            Assert.AreEqual(3, state.PendingPsionicScryCards.Count);
            Assert.AreEqual(1, state.PlayerDrawPile.Count);

            V091MechanicsRules.ApplyPsionicScryChoice(state, new[] { 2 }, new List<BattleEvent>());
            Assert.IsFalse(state.AwaitingPsionicScry);
            Assert.AreEqual(1, state.PlayerDiscardPile.Count);
            Assert.AreEqual("b", state.PlayerDiscardPile[0].DefinitionId);
            Assert.AreEqual(3, state.PlayerDrawPile.Count);
            Assert.AreEqual("a", state.PlayerDrawPile[0].DefinitionId);
            Assert.AreEqual("c", state.PlayerDrawPile[1].DefinitionId);
            Assert.AreEqual("d", state.PlayerDrawPile[2].DefinitionId);
        }

        static CombatantState Unit(string id, TeamSide team, FormationSlot slot, int hp, int atk) =>
            new CombatantState
            {
                Id = id,
                DisplayName = id,
                CharacterDefinitionId = "char_lich_queen",
                Team = team,
                Slot = slot,
                Hp = hp,
                MaxHp = hp,
                BaseAttack = atk,
                Attack = atk,
                Defense = 0,
                Speed = 5
            };
    }
}
