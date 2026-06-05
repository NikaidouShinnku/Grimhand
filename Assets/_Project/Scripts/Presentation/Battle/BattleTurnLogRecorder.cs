using System.Collections.Generic;
using System.Text;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleTurnLogRecorder
    {
        const int MaxLines = 40;

        readonly List<string> _lines = new();
        readonly List<string> _effectParts = new();
        bool _recording;
        bool _inResolution;
        StringBuilder _currentLine;

        public IReadOnlyList<string> LastRound => _lines;

        public void Reset()
        {
            _lines.Clear();
            _effectParts.Clear();
            _currentLine = null;
            _recording = false;
            _inResolution = false;
        }

        public void Feed(BattleEvent e, BattleState state)
        {
            if (ShouldStartRound(e))
                BeginRound();

            if (!_recording)
                return;

            if (ShouldFinishRound(e))
            {
                FlushCurrentResolution();
                _recording = false;
                return;
            }

            if (e.Kind == BattleEventKind.CardResolvedStarted)
            {
                FlushCurrentResolution();
                BeginResolution(e, state);
                return;
            }

            if (e.Kind == BattleEventKind.CardResolvedEnded)
            {
                FlushCurrentResolution();
                return;
            }

            if (_inResolution && IsResolutionEffect(e))
                _effectParts.Add(FormatResolutionEffect(e, state));
        }

        static bool ShouldStartRound(BattleEvent e) =>
            e.Kind is BattleEventKind.PlanCommitted or BattleEventKind.TurnSkipped;

        static bool ShouldFinishRound(BattleEvent e) =>
            e.Kind == BattleEventKind.PhaseChanged && e.Phase == TurnPhase.Planning;

        static bool IsResolutionEffect(BattleEvent e) =>
            e.Kind switch
            {
                BattleEventKind.DamageApplied => true,
                BattleEventKind.BlockGained => true,
                BattleEventKind.HealApplied => true,
                BattleEventKind.CharacterRevived => true,
                BattleEventKind.CharacterDied => true,
                BattleEventKind.StatusApplied => true,
                BattleEventKind.StatusTickDamage => true,
                BattleEventKind.ReactionTriggered => true,
                BattleEventKind.ParryTriggered => true,
                BattleEventKind.DeckPolluted => true,
                _ => false
            };

        void BeginRound()
        {
            FlushCurrentResolution();
            _recording = true;
        }

        void BeginResolution(BattleEvent e, BattleState state)
        {
            _inResolution = true;
            _effectParts.Clear();
            _currentLine = new StringBuilder();

            var actor = state?.GetCombatant(e.CombatantId);
            var side = actor != null && actor.Team == TeamSide.Player ? "我方" : "敌方";
            var actorName = actor != null ? actor.DisplayName : "?";
            var cardName = BattleEventLogFormatter.DescribeCard(e, state);

            _currentLine.Append('【').Append(side).Append('】');
            _currentLine.Append(actorName).Append(" · ").Append(cardName);
        }

        void FlushCurrentResolution()
        {
            if (!_inResolution || _currentLine == null)
            {
                _inResolution = false;
                _currentLine = null;
                _effectParts.Clear();
                return;
            }

            if (_effectParts.Count > 0)
            {
                _currentLine.Append(" → ");
                for (var i = 0; i < _effectParts.Count; i++)
                {
                    if (i > 0)
                        _currentLine.Append('，');

                    _currentLine.Append(_effectParts[i]);
                }
            }

            AppendLine(_currentLine.ToString());
            _inResolution = false;
            _currentLine = null;
            _effectParts.Clear();
        }

        void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            _lines.Add(line);
            while (_lines.Count > MaxLines)
                _lines.RemoveAt(0);
        }

        static string FormatResolutionEffect(BattleEvent e, BattleState state)
        {
            switch (e.Kind)
            {
                case BattleEventKind.DamageApplied:
                {
                    var target = CombatantLabel(state, e.TargetId);
                    var block = e.BlockedAmount > 0 ? $"（格挡 {e.BlockedAmount}）" : "";
                    return $"对 {target} 造成 {e.Amount} 伤害{block}";
                }
                case BattleEventKind.BlockGained:
                    return $"{CombatantLabel(state, e.CombatantId)} 获得 {e.Amount} 护甲";
                case BattleEventKind.HealApplied:
                    return $"{CombatantLabel(state, e.CombatantId)} 恢复 {e.Amount} 生命";
                case BattleEventKind.CharacterRevived:
                    return $"{CombatantLabel(state, e.CombatantId)} 复活（{e.Amount} HP）";
                case BattleEventKind.CharacterDied:
                    return $"{CombatantLabel(state, e.CombatantId)} 阵亡";
                case BattleEventKind.StatusApplied:
                    return $"{CombatantLabel(state, e.CombatantId)} 获得 {e.Message} x{e.Amount}";
                case BattleEventKind.StatusTickDamage:
                    return $"{CombatantLabel(state, e.CombatantId)} 受到 {e.Message} {e.Amount} 伤害";
                case BattleEventKind.ReactionTriggered:
                case BattleEventKind.ParryTriggered:
                    return $"{CombatantLabel(state, e.CombatantId)} 触发应对：{e.Message}";
                case BattleEventKind.DeckPolluted:
                    return $"{CombatantLabel(state, e.CombatantId)} 牌堆污染 {e.Amount} 张";
                default:
                    return BattleEventLogFormatter.Format(e, state);
            }
        }

        static string CombatantLabel(BattleState state, string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return "?";

            var c = state?.GetCombatant(combatantId);
            if (c == null)
                return combatantId;

            var slot = PositionRules.GetEffectiveSlot(state, c);
            return $"{c.DisplayName}（{BattleUiFormatters.SlotLabel(slot)}）";
        }
    }
}
