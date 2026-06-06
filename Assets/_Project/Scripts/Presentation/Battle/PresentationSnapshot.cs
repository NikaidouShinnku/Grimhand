using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战斗演出期间的 HP / 存活展示快照，与已结算完毕的逻辑状态解耦。</summary>
    public sealed class PresentationSnapshot
    {
        readonly Dictionary<string, int> _hp = new();
        readonly Dictionary<string, int> _maxHp = new();
        readonly Dictionary<string, int> _block = new();
        readonly HashSet<string> _dead = new();
        readonly List<int> _playerHandInstanceIds = new();
        readonly List<EnemyIntentSlot> _turnEnemyIntents = new();
        readonly List<ResolutionStep> _turnResolutionSteps = new();
        readonly Dictionary<int, string> _turnTargetByCardId = new();

        public IReadOnlyList<EnemyIntentSlot> TurnEnemyIntents => _turnEnemyIntents;
        public IReadOnlyList<ResolutionStep> TurnResolutionSteps => _turnResolutionSteps;
        public IReadOnlyDictionary<int, string> TurnTargetByCardId => _turnTargetByCardId;
        public bool HasTurnPresentation => _turnResolutionSteps.Count > 0;

        public static PresentationSnapshot Capture(BattleState state)
        {
            var snap = new PresentationSnapshot();
            if (state == null)
                return snap;

            foreach (var c in state.Combatants)
            {
                snap._hp[c.Id] = c.Hp;
                snap._maxHp[c.Id] = c.MaxHp;
                snap._block[c.Id] = c.Block;
                if (!c.IsAlive)
                    snap._dead.Add(c.Id);
            }

            foreach (var card in state.PlayerHand)
                snap._playerHandInstanceIds.Add(card.InstanceId);

            return snap;
        }

        public static PresentationSnapshot CaptureForTurnPresentation(
            BattleState state,
            PlanningDraft draft,
            BattleEngine engine)
        {
            var snap = Capture(state);
            if (state == null || engine == null)
                return snap;

            foreach (var intent in state.EnemyIntents)
            {
                snap._turnEnemyIntents.Add(new EnemyIntentSlot
                {
                    CardInstanceId = intent.CardInstanceId,
                    OwnerCombatantId = intent.OwnerCombatantId,
                    IsHidden = intent.IsHidden,
                    OrderIndex = intent.OrderIndex
                });
            }

            var playerPlan = draft?.CommitToPlan() ?? new BattlePlan();
            foreach (var pair in playerPlan.TargetByCardInstanceId)
                snap._turnTargetByCardId[pair.Key] = pair.Value;

            foreach (var step in engine.PreviewResolutionSchedule(playerPlan))
                snap._turnResolutionSteps.Add(step);

            return snap;
        }

        /// <summary>演出期间展示的手牌：仅含提交规划时的牌，不含回合中后段新抽的牌。</summary>
        public IReadOnlyList<CardInstanceState> GetDisplayedPlayerHand(BattleState state)
        {
            if (state == null)
                return System.Array.Empty<CardInstanceState>();

            if (_playerHandInstanceIds.Count == 0)
                return state.PlayerHand;

            var result = new List<CardInstanceState>(_playerHandInstanceIds.Count);
            foreach (var id in _playerHandInstanceIds)
            {
                foreach (var card in state.PlayerHand)
                {
                    if (card.InstanceId != id)
                        continue;

                    result.Add(card);
                    break;
                }
            }

            return result;
        }

        public bool IsAlive(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return false;

            return !_dead.Contains(combatantId) && _hp.TryGetValue(combatantId, out var hp) && hp > 0;
        }

        public int GetHp(string combatantId) =>
            _hp.TryGetValue(combatantId, out var hp) ? hp : 0;

        public int GetMaxHp(string combatantId) =>
            _maxHp.TryGetValue(combatantId, out var maxHp) ? maxHp : 0;

        public int GetBlock(string combatantId) =>
            _block.TryGetValue(combatantId, out var block) ? block : 0;

        public void ApplyBlockGain(string combatantId, int amount)
        {
            if (string.IsNullOrEmpty(combatantId) || amount <= 0)
                return;

            _block[combatantId] = GetBlock(combatantId) + amount;
        }

        public void ApplyBlockConsumed(string combatantId, int amount)
        {
            if (string.IsNullOrEmpty(combatantId) || amount <= 0)
                return;

            _block[combatantId] = System.Math.Max(0, GetBlock(combatantId) - amount);
        }

        public void ClearBlock(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return;

            _block[combatantId] = 0;
        }

        public void ClearAllBlock()
        {
            var keys = new List<string>(_block.Keys);
            foreach (var id in keys)
                _block[id] = 0;
        }

        public void ApplyDamage(string combatantId, int amount)
        {
            if (string.IsNullOrEmpty(combatantId) || amount <= 0)
                return;

            if (!_hp.TryGetValue(combatantId, out var hp))
                return;

            hp = System.Math.Max(0, hp - amount);
            _hp[combatantId] = hp;
            if (hp <= 0)
                _dead.Add(combatantId);
        }

        public void ApplyHeal(string combatantId, int amount)
        {
            if (string.IsNullOrEmpty(combatantId) || amount <= 0)
                return;

            if (!_hp.TryGetValue(combatantId, out var hp))
                return;

            var maxHp = GetMaxHp(combatantId);
            _hp[combatantId] = System.Math.Min(maxHp, hp + amount);
            _dead.Remove(combatantId);
        }

        public void MarkDead(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return;

            _dead.Add(combatantId);
            if (_hp.ContainsKey(combatantId))
                _hp[combatantId] = 0;
        }
    }
}
