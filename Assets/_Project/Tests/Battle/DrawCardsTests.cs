using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class DrawCardsTests
    {
        [Test]
        public void DrawCardsEffect_DrawsImmediately()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            var engine = new BattleEngine(config);
            engine.StartBattle();

            var state = engine.State;
            var actor = state.Combatants[0];
            var card = new CardInstanceState
            {
                InstanceId = 9999,
                DisplayName = "测试抽牌",
                OwnerCharacterId = actor.CharacterDefinitionId,
                CardType = CardType.Status,
                Cost = 0,
                IsUsable = true
            };
            card.Actions.Add(new EffectActionSpec
            {
                Type = EffectActionType.DrawCards,
                Target = EffectTarget.Self,
                Value = 2
            });

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            var handBefore = state.PlayerHand.Count;
            EffectActionExecutor.ExecuteAll(state, actor, card, events, null);

            // v0.9：抽牌效果当回合立即抽到手中，不再延迟到下回合。
            Assert.AreEqual(handBefore + 2, state.PlayerHand.Count);
            Assert.AreEqual(0, state.PendingDrawNextTurn);
        }

        [Test]
        public void PendingDraw_IncreasesNextTurnHandSize()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            var engine = new BattleEngine(config);
            engine.StartBattle();

            engine.State.PendingDrawNextTurn = 2;
            engine.SkipPlayerTurn();
            engine.FlushPendingEndOfTurn();

            Assert.AreEqual(7, engine.State.PlayerHand.Count);
            Assert.AreEqual(0, engine.State.PendingDrawNextTurn);
        }

        [Test]
        public void TurnStartDraw_DiscardsAtEndOfTurn_BattleDraw_Retains()
        {
            var config = DemoBattleFactory.CreateDefault3v3();
            config.CardsDrawnPerTurn = 5;
            config.HandLimit = 8;
            var engine = new BattleEngine(config);
            engine.StartBattle();

            foreach (var card in engine.State.PlayerHand)
                Assert.IsFalse(card.RetainInHandOverTurnEnd, "回合初抽牌不应标记保留");

            var events = new System.Collections.Generic.List<Events.BattleEvent>();
            engine.State.Phase = TurnPhase.SpeedResolve;
            DeckRules.DrawCards(
                engine.State, TeamSide.Player, null, 1, events, retainInHandOverTurnEnd: true);
            var battleDrawn = engine.State.PlayerHand[^1];
            Assert.IsTrue(battleDrawn.RetainInHandOverTurnEnd, "战斗阶段抽牌应标记保留");

            DeckRules.DiscardHandAtEndOfTurn(engine.State, TeamSide.Player, events);

            Assert.AreEqual(1, engine.State.PlayerHand.Count);
            Assert.AreSame(battleDrawn, engine.State.PlayerHand[0]);
            Assert.IsFalse(battleDrawn.RetainInHandOverTurnEnd);
        }
    }
}
