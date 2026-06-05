using Grimhand.Battle;
using Grimhand.Battle.Demo;
using Grimhand.Battle.Effects;
using Grimhand.Battle.Model;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class DrawCardsTests
    {
        [Test]
        public void DrawCardsEffect_AddsPendingDrawForNextTurn()
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
            EffectActionExecutor.ExecuteAll(state, actor, card, events, null);

            Assert.AreEqual(2, state.PendingDrawNextTurn);
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

            Assert.AreEqual(7, engine.State.PlayerHand.Count);
            Assert.AreEqual(0, engine.State.PendingDrawNextTurn);
        }
    }
}
