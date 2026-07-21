using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Rules;

namespace Grimhand.Presentation.Battle
{
    public struct CombatantDisplayStats
    {
        public int Attack;
        public int Defense;
        public int Speed;
        public int BloodRageStacks;
        public string StatusSummary;
        public string TraitFootnote;
    }

    public struct FootStatusEntry
    {
        public string StatusId;
        public int Stacks;
    }

    /// <summary>战斗演出期间的 HP / 存活 / 属性展示快照，与已结算完毕的逻辑状态解耦。</summary>
    public sealed class PresentationSnapshot
    {
        readonly Dictionary<string, int> _hp = new();
        readonly Dictionary<string, int> _maxHp = new();
        readonly Dictionary<string, int> _block = new();
        readonly Dictionary<string, int> _ironWallPendingAttackBonus = new();
        readonly Dictionary<string, CombatantDisplayStats> _displayStats = new();
        readonly Dictionary<string, List<FootStatusEntry>> _footStatuses = new();
        readonly Dictionary<int, Dictionary<string, CombatantDisplayStats>> _eventCheckpoints = new();
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
                snap._ironWallPendingAttackBonus[c.Id] = c.TalentIronWallPendingDamageBonus;
                snap._displayStats[c.Id] = BuildDisplayStats(c, state);
                snap._footStatuses[c.Id] = CaptureFootStatuses(c);
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

        /// <summary>单位是否已进入本段演出快照（含开场捕获与中途 RegisterSpawned）。</summary>
        public bool IsTracked(string combatantId) =>
            !string.IsNullOrEmpty(combatantId) && _hp.ContainsKey(combatantId);

        public int GetHp(string combatantId) =>
            _hp.TryGetValue(combatantId, out var hp) ? hp : 0;

        public int GetMaxHp(string combatantId) =>
            _maxHp.TryGetValue(combatantId, out var maxHp) ? maxHp : 0;

        public int GetBlock(string combatantId) =>
            _block.TryGetValue(combatantId, out var block) ? block : 0;

        public int GetIronWallPendingAttackBonus(string combatantId) =>
            _ironWallPendingAttackBonus.TryGetValue(combatantId, out var bonus) ? bonus : 0;

        public bool TryGetDisplayStats(string combatantId, out CombatantDisplayStats stats) =>
            _displayStats.TryGetValue(combatantId, out stats);

        public IReadOnlyList<FootStatusEntry> GetFootStatuses(string combatantId)
        {
            if (_footStatuses.TryGetValue(combatantId, out var list))
                return list;

            return System.Array.Empty<FootStatusEntry>();
        }

        public void ApplyFootStatusApplied(string combatantId, string statusId, int totalStacks)
        {
            if (string.IsNullOrEmpty(combatantId) || string.IsNullOrEmpty(statusId) || totalStacks <= 0)
                return;

            if (!_footStatuses.TryGetValue(combatantId, out var list))
            {
                list = new List<FootStatusEntry>();
                _footStatuses[combatantId] = list;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].StatusId != statusId)
                    continue;

                list[i] = new FootStatusEntry { StatusId = statusId, Stacks = totalStacks };
                return;
            }

            list.Add(new FootStatusEntry { StatusId = statusId, Stacks = totalStacks });
        }

        public void ApplyFootStatusRemoved(string combatantId, string statusId, int removedStacks)
        {
            if (string.IsNullOrEmpty(combatantId) || string.IsNullOrEmpty(statusId) || removedStacks <= 0)
                return;

            if (!_footStatuses.TryGetValue(combatantId, out var list))
                return;

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].StatusId != statusId)
                    continue;

                var remaining = list[i].Stacks - removedStacks;
                if (remaining <= 0)
                    list.RemoveAt(i);
                else
                    list[i] = new FootStatusEntry { StatusId = statusId, Stacks = remaining };

                return;
            }
        }

        public void SyncFootStatusesFromLive(BattleState state, string combatantId)
        {
            if (state == null || string.IsNullOrEmpty(combatantId))
                return;

            var combatant = state.GetCombatant(combatantId);
            if (combatant == null)
                return;

            _footStatuses[combatantId] = CaptureFootStatuses(combatant);
        }

        public void RecordEventCheckpoint(int eventIndex, BattleEventKind kind, BattleState state)
        {
            if (state == null || eventIndex < 0)
                return;

            if (!BattlePresentationCheckpointKinds.ShouldRecord(kind))
                return;

            _eventCheckpoints[eventIndex] = CaptureAllDisplayStats(state);
        }

        public void ApplyEventCheckpoint(int eventIndex)
        {
            if (eventIndex < 0 || !_eventCheckpoints.TryGetValue(eventIndex, out var stats))
                return;

            foreach (var pair in stats)
                _displayStats[pair.Key] = pair.Value;
        }

        public void SyncCombatantFromLive(BattleState state, string combatantId)
        {
            if (state == null || string.IsNullOrEmpty(combatantId))
                return;

            var combatant = state.GetCombatant(combatantId);
            if (combatant == null)
                return;

            _displayStats[combatantId] = BuildDisplayStats(combatant, state);
        }

        /// <summary>中途召唤的单位写入演出快照，避免 IsAlive 因缺 HP 记录而显示为死亡。</summary>
        public void RegisterSpawnedCombatant(CombatantState combatant, BattleState state = null)
        {
            if (combatant == null || string.IsNullOrEmpty(combatant.Id))
                return;

            _hp[combatant.Id] = combatant.Hp;
            _maxHp[combatant.Id] = combatant.MaxHp;
            _block[combatant.Id] = combatant.Block;
            _ironWallPendingAttackBonus[combatant.Id] = combatant.TalentIronWallPendingDamageBonus;
            _displayStats[combatant.Id] = BuildDisplayStats(combatant, state);
            _footStatuses[combatant.Id] = CaptureFootStatuses(combatant);
            if (combatant.IsAlive)
                _dead.Remove(combatant.Id);
            else
                _dead.Add(combatant.Id);
        }

        static Dictionary<string, CombatantDisplayStats> CaptureAllDisplayStats(BattleState state)
        {
            var dict = new Dictionary<string, CombatantDisplayStats>();
            foreach (var combatant in state.Combatants)
                dict[combatant.Id] = BuildDisplayStats(combatant, state);
            return dict;
        }

        static CombatantDisplayStats BuildDisplayStats(CombatantState combatant, BattleState state = null) =>
            new CombatantDisplayStats
            {
                Attack = combatant.Attack,
                Defense = combatant.Defense,
                Speed = StatusRules.GetEffectiveSpeed(state, combatant),
                BloodRageStacks = combatant.BloodRageStacks,
                StatusSummary = BattleUiFormatters.FormatStatusListDisplay(combatant),
                TraitFootnote = MinionTraitDisplayFormatter.FormatFootnote(combatant, state)
            };

        static List<FootStatusEntry> CaptureFootStatuses(CombatantState combatant) =>
            FootStatusIconAggregator.Aggregate(combatant);

        public void ApplyIronWallConversion(string combatantId, int amount)
        {
            if (string.IsNullOrEmpty(combatantId) || amount <= 0)
                return;

            _ironWallPendingAttackBonus[combatantId] = GetIronWallPendingAttackBonus(combatantId) + amount;
        }

        public void SyncIronWallPendingFromLive(BattleState state, string combatantId)
        {
            if (state == null || string.IsNullOrEmpty(combatantId))
                return;

            var combatant = state.GetCombatant(combatantId);
            if (combatant == null)
                return;

            _ironWallPendingAttackBonus[combatantId] = combatant.TalentIronWallPendingDamageBonus;
        }

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

        public void SyncBlockFromLive(BattleState state)
        {
            if (state == null)
                return;

            foreach (var combatant in state.Combatants)
                _block[combatant.Id] = combatant.Block;
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
