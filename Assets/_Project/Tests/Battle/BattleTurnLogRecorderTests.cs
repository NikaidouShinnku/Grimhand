using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Presentation.Battle;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class BattleTurnLogRecorderTests
    {
        [Test]
        public void Feed_GroupsDrawEventsIntoSingleLine()
        {
            var recorder = new BattleTurnLogRecorder();
            var state = new BattleState();
            state.CardsById[1] = new CardInstanceState { InstanceId = 1, DisplayName = "沙暴射线" };
            state.CardsById[2] = new CardInstanceState { InstanceId = 2, DisplayName = "生命汲取" };

            recorder.Feed(new BattleEvent(BattleEventKind.PlanCommitted), state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardDrawn, "draw") { CardInstanceId = 1 }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardDrawn, "draw") { CardInstanceId = 2 }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.PhaseChanged) { Phase = TurnPhase.Planning }, state);

            Assert.AreEqual(1, recorder.LastRound.Count);
            Assert.AreEqual("【玩家】抽牌【沙暴射线】【生命汲取】", recorder.LastRound[0]);
        }

        [Test]
        public void Feed_GroupsDiscardEventsIntoSingleLine()
        {
            var recorder = new BattleTurnLogRecorder();
            var state = new BattleState();
            state.CardsById[1] = new CardInstanceState { InstanceId = 1, DisplayName = "战吼鼓舞" };
            state.CardsById[2] = new CardInstanceState { InstanceId = 2, DisplayName = "猛扑" };

            recorder.Feed(new BattleEvent(BattleEventKind.PlanCommitted), state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardDiscarded, "discard") { CardInstanceId = 1 }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardDiscarded, "discard") { CardInstanceId = 2 }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.PhaseChanged) { Phase = TurnPhase.Planning }, state);

            Assert.AreEqual(1, recorder.LastRound.Count);
            Assert.AreEqual("【玩家】弃牌【战吼鼓舞】【猛扑】", recorder.LastRound[0]);
        }
    }
}
