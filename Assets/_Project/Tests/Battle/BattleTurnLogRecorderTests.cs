using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Presentation.Battle;
using NUnit.Framework;

namespace Grimhand.Battle.Tests
{
    public class BattleTurnLogRecorderTests
    {
        [Test]
        public void Feed_IgnoresDrawAndDiscardEvents()
        {
            var recorder = new BattleTurnLogRecorder();
            var state = new BattleState();
            state.CardsById[1] = new CardInstanceState { InstanceId = 1, DisplayName = "沙暴射线" };

            recorder.Feed(new BattleEvent(BattleEventKind.PlanCommitted), state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardDrawn, "draw") { CardInstanceId = 1 }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardDiscarded, "discard") { CardInstanceId = 1 }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.PhaseChanged) { Phase = TurnPhase.Planning }, state);

            Assert.AreEqual(0, recorder.LastRound.Count);
        }

        [Test]
        public void Feed_RecordsCardResolutionInPlayOrder()
        {
            var recorder = new BattleTurnLogRecorder();
            var state = BuildStateWithActor("pharaoh", "法老", TeamSide.Player);
            state.CardsById[10] = new CardInstanceState { InstanceId = 10, DisplayName = "祈祷祝福" };

            recorder.Feed(new BattleEvent(BattleEventKind.PlanCommitted), state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardResolvedStarted, "祈祷祝福")
            {
                CombatantId = "pharaoh",
                CardInstanceId = 10
            }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.HealApplied)
            {
                CombatantId = "ally",
                Amount = 12
            }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.CardResolvedEnded, "祈祷祝福")
            {
                CombatantId = "pharaoh",
                CardInstanceId = 10
            }, state);
            recorder.Feed(new BattleEvent(BattleEventKind.PhaseChanged) { Phase = TurnPhase.Planning }, state);

            Assert.AreEqual(1, recorder.LastRound.Count);
            StringAssert.Contains("【我方】法老 · 祈祷祝福", recorder.LastRound[0]);
            StringAssert.Contains("恢复 12 生命", recorder.LastRound[0]);
        }

        [Test]
        public void Feed_TrimsToMaxFortyLines()
        {
            var recorder = new BattleTurnLogRecorder();
            var state = BuildStateWithActor("pharaoh", "法老", TeamSide.Player);

            for (var i = 1; i <= 42; i++)
            {
                state.CardsById[i] = new CardInstanceState { InstanceId = i, DisplayName = $"卡牌{i}" };
                recorder.Feed(new BattleEvent(BattleEventKind.PlanCommitted), state);
                recorder.Feed(new BattleEvent(BattleEventKind.CardResolvedStarted, $"卡牌{i}")
                {
                    CombatantId = "pharaoh",
                    CardInstanceId = i
                }, state);
                recorder.Feed(new BattleEvent(BattleEventKind.CardResolvedEnded, $"卡牌{i}")
                {
                    CombatantId = "pharaoh",
                    CardInstanceId = i
                }, state);
                recorder.Feed(new BattleEvent(BattleEventKind.PhaseChanged) { Phase = TurnPhase.Planning }, state);
            }

            Assert.AreEqual(40, recorder.LastRound.Count);
            StringAssert.Contains("卡牌3", recorder.LastRound[0]);
            StringAssert.Contains("卡牌42", recorder.LastRound[39]);
        }

        static BattleState BuildStateWithActor(string id, string name, TeamSide team)
        {
            var state = new BattleState();
            state.Combatants.Add(new CombatantState
            {
                Id = id,
                DisplayName = name,
                Team = team,
                Slot = FormationSlot.Middle,
                Hp = 30,
                MaxHp = 40
            });
            state.Combatants.Add(new CombatantState
            {
                Id = "ally",
                DisplayName = "战士",
                Team = TeamSide.Player,
                Slot = FormationSlot.Front,
                Hp = 20,
                MaxHp = 40
            });
            return state;
        }
    }
}
