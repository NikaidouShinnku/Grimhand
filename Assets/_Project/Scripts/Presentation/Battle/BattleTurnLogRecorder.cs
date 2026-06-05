using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleTurnLogRecorder
    {
        struct CardBatchEntry
        {
            public int CardInstanceId;
            public string CardName;
            public TeamSide Team;
        }

        readonly List<string> _lastRound = new();
        readonly List<string> _building = new();
        readonly List<CardBatchEntry> _discardBuffer = new();
        readonly List<CardBatchEntry> _drawBuffer = new();
        bool _recording;

        public IReadOnlyList<string> LastRound => _lastRound;

        public void Reset()
        {
            _lastRound.Clear();
            _building.Clear();
            _discardBuffer.Clear();
            _drawBuffer.Clear();
            _recording = false;
        }

        public void Feed(BattleEvent e, BattleState state)
        {
            if (ShouldStartRound(e))
                BeginRound();

            if (!_recording)
                return;

            if (ShouldFinishRound(e))
            {
                FlushCardBuffers();
                CommitRound();
                return;
            }

            if (e.Kind == BattleEventKind.CardDiscarded)
            {
                _discardBuffer.Add(new CardBatchEntry
                {
                    CardInstanceId = e.CardInstanceId,
                    CardName = BattleEventLogFormatter.DescribeCard(e, state),
                    Team = InferCardTeam(e, state)
                });
                return;
            }

            if (e.Kind == BattleEventKind.CardDrawn)
            {
                _drawBuffer.Add(new CardBatchEntry
                {
                    CardInstanceId = e.CardInstanceId,
                    CardName = BattleEventLogFormatter.DescribeCard(e, state),
                    Team = InferCardTeam(e, state)
                });
                return;
            }

            if (!IsLogWorthy(e))
                return;

            FlushCardBuffers();
            _building.Add(BattleEventLogFormatter.Format(e, state));
        }

        static bool ShouldStartRound(BattleEvent e) =>
            e.Kind is BattleEventKind.PlanCommitted or BattleEventKind.TurnSkipped;

        static bool ShouldFinishRound(BattleEvent e) =>
            e.Kind == BattleEventKind.PhaseChanged && e.Phase == TurnPhase.Planning;

        static bool IsLogWorthy(BattleEvent e) =>
            e.Kind switch
            {
                BattleEventKind.PortraitPoseChanged => false,
                BattleEventKind.PortraitIdleRestored => false,
                BattleEventKind.EnergyChanged => false,
                BattleEventKind.CardSelectedForPlay => false,
                BattleEventKind.CardDeselectedFromPlay => false,
                BattleEventKind.TargetSelectionRequired => false,
                BattleEventKind.PhaseChanged => false,
                BattleEventKind.CardDiscarded => false,
                BattleEventKind.CardDrawn => false,
                _ => true
            };

        void BeginRound()
        {
            _building.Clear();
            _discardBuffer.Clear();
            _drawBuffer.Clear();
            _recording = true;
        }

        void CommitRound()
        {
            if (_building.Count == 0)
            {
                _recording = false;
                return;
            }

            _lastRound.Clear();
            _lastRound.AddRange(_building);
            _building.Clear();
            _recording = false;
        }

        void FlushCardBuffers()
        {
            FlushDiscardBuffer();
            FlushDrawBuffer();
        }

        void FlushDiscardBuffer()
        {
            if (_discardBuffer.Count == 0)
                return;

            AppendCardBatch("弃牌", _discardBuffer);
            _discardBuffer.Clear();
        }

        void FlushDrawBuffer()
        {
            if (_drawBuffer.Count == 0)
                return;

            AppendCardBatch("抽牌", _drawBuffer);
            _drawBuffer.Clear();
        }

        void AppendCardBatch(string actionLabel, List<CardBatchEntry> entries)
        {
            var player = new List<string>();
            var enemy = new List<string>();

            foreach (var entry in entries)
            {
                if (entry.Team == TeamSide.Player)
                    player.Add(entry.CardName);
                else
                    enemy.Add(entry.CardName);
            }

            if (player.Count > 0)
                _building.Add(FormatCardBatch("玩家", actionLabel, player));

            if (enemy.Count > 0)
                _building.Add(FormatCardBatch("敌方", actionLabel, enemy));
        }

        static string FormatCardBatch(string sideLabel, string actionLabel, List<string> cardNames)
        {
            var sb = new StringBuilder();
            sb.Append('【').Append(sideLabel).Append('】').Append(actionLabel);
            foreach (var name in cardNames)
                sb.Append('【').Append(name).Append('】');

            return sb.ToString();
        }

        static TeamSide InferCardTeam(BattleEvent e, BattleState state)
        {
            if (state == null || e.CardInstanceId <= 0)
                return TeamSide.Player;

            var card = state.GetCard(e.CardInstanceId);
            if (card == null)
                return TeamSide.Player;

            foreach (var combatant in state.Combatants)
            {
                if (combatant.CharacterDefinitionId == card.OwnerCharacterId)
                    return combatant.Team;
            }

            return TeamSide.Player;
        }
    }
}
