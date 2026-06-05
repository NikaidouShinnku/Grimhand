using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;

namespace Grimhand.Battle.Planning
{
    public sealed class PlanningDraft
    {
        readonly BattleState _state;
        readonly List<BattleEvent> _events;
        readonly List<int> _selectedQueue = new();
        readonly Dictionary<int, string> _targetByCard = new();
        int? _awaitingTargetCardId;
        string _awaitingConsumableId;
        int _awaitingConsumableSlotIndex = -1;

        public PlanningDraft(BattleState state, List<BattleEvent> events)
        {
            _state = state;
            _events = events;
        }

        public IReadOnlyList<int> SelectedQueue => _selectedQueue;
        public int EnergyRemaining => _state.EnergyCurrent;
        public int? AwaitingTargetCardId => _awaitingTargetCardId;
        public string AwaitingConsumableId => _awaitingConsumableId;
        public int AwaitingConsumableSlotIndex => _awaitingConsumableSlotIndex;
        public bool IsAwaitingConsumableTarget => !string.IsNullOrEmpty(_awaitingConsumableId);

        public bool IsSelected(int instanceId) => _selectedQueue.Contains(instanceId);

        /// <summary>选牌全局先后（1 起算）；未选中返回 0。</summary>
        public int GetGlobalPlayOrder(int instanceId)
        {
            var index = _selectedQueue.IndexOf(instanceId);
            return index < 0 ? 0 : index + 1;
        }

        /// <summary>同一角色多张牌时的出牌先后；未选中返回 false。</summary>
        public bool TryGetOwnerPlayOrder(int instanceId, out int order, out int totalForOwner)
        {
            order = 0;
            totalForOwner = 0;

            var cardIndex = _selectedQueue.IndexOf(instanceId);
            if (cardIndex < 0)
                return false;

            var card = _state.GetCard(instanceId);
            if (card == null)
                return false;

            var ownerCharId = card.OwnerCharacterId;
            foreach (var id in _selectedQueue)
            {
                var c = _state.GetCard(id);
                if (c != null && c.OwnerCharacterId == ownerCharId)
                    totalForOwner++;
            }

            for (var i = 0; i <= cardIndex; i++)
            {
                var c = _state.GetCard(_selectedQueue[i]);
                if (c != null && c.OwnerCharacterId == ownerCharId)
                    order++;
            }

            return true;
        }

        public string GetAssignedTarget(int cardInstanceId)
        {
            _targetByCard.TryGetValue(cardInstanceId, out var targetId);
            return targetId;
        }

        public bool TrySelectCard(int instanceId)
        {
            if (_state.Phase != TurnPhase.Planning)
                return false;

            if (_awaitingTargetCardId == instanceId)
            {
                CancelAwaitingTarget();
                return true;
            }

            if (_selectedQueue.Contains(instanceId))
                return false;

            if (!TryGetSelectableCard(instanceId, out var card))
                return false;

            if (!EnergyRules.CanAfford(_state.EnergyCurrent, card.Cost))
                return false;

            var ownerId = PositionRules.GetOwnerCombatantId(_state, card);
            var owner = ownerId != null ? _state.GetCombatant(ownerId) : null;

            if (CardRules.ShouldPromptForTarget(_state, card, owner) && !_targetByCard.ContainsKey(instanceId))
            {
                _awaitingTargetCardId = instanceId;
                _events.Add(new BattleEvent(BattleEventKind.TargetSelectionRequired, card.DisplayName)
                {
                    CardInstanceId = instanceId
                });
                return true;
            }

            CompleteSelect(card, instanceId);
            return true;
        }

        public bool TryAssignTargetAndSelect(string combatantId)
        {
            if (_awaitingTargetCardId == null)
                return false;

            var cardId = _awaitingTargetCardId.Value;
            var card = _state.GetCard(cardId);
            if (card == null)
                return false;

            var ownerId = PositionRules.GetOwnerCombatantId(_state, card);
            var owner = ownerId != null ? _state.GetCombatant(ownerId) : null;
            var valid = CardRules.GetValidTargetCandidates(_state, card, owner);
            var ok = false;
            foreach (var v in valid)
            {
                if (v.Id == combatantId)
                {
                    ok = true;
                    break;
                }
            }

            if (!ok)
                return false;

            _targetByCard[cardId] = combatantId;
            _awaitingTargetCardId = null;
            CompleteSelect(card, cardId);
            return true;
        }

        public void CancelAwaitingTarget()
        {
            _awaitingTargetCardId = null;
        }

        public bool TryBeginConsumableUse(string consumableId, int slotIndex)
        {
            if (_state.Phase != TurnPhase.Planning || _state.ConsumableUsedThisBattle)
                return false;

            if (!Consumables.ConsumableDatabase.TryGet(consumableId, out var definition))
                return false;

            CancelAwaitingTarget();

            if (!Consumables.ConsumableRules.NeedsTarget(definition))
                return false;

            _awaitingConsumableId = consumableId;
            _awaitingConsumableSlotIndex = slotIndex;
            _events.Add(new BattleEvent(BattleEventKind.TargetSelectionRequired, definition.DisplayName));
            return true;
        }

        public bool TryAssignConsumableTarget(string combatantId)
        {
            if (string.IsNullOrEmpty(_awaitingConsumableId))
                return false;

            if (!Consumables.ConsumableDatabase.TryGet(_awaitingConsumableId, out var definition))
                return false;

            if (!Consumables.ConsumableRules.TryApply(
                    _state,
                    definition,
                    combatantId,
                    _events,
                    null,
                    out _))
                return false;

            ClearConsumableTargeting();
            return true;
        }

        public int PendingConsumableSlotIndex => _awaitingConsumableSlotIndex;

        public bool TryApplyInstantConsumable(string consumableId, out string errorMessage)
        {
            errorMessage = "";
            if (_state.Phase != TurnPhase.Planning || _state.ConsumableUsedThisBattle)
            {
                errorMessage = "当前无法使用消耗品。";
                return false;
            }

            if (!Consumables.ConsumableDatabase.TryGet(consumableId, out var definition))
            {
                errorMessage = "未知消耗品。";
                return false;
            }

            if (Consumables.ConsumableRules.NeedsTarget(definition))
            {
                errorMessage = "需要选择目标。";
                return false;
            }

            CancelAwaitingTarget();
            return Consumables.ConsumableRules.TryApply(
                _state,
                definition,
                null,
                _events,
                null,
                out errorMessage);
        }

        public void CancelConsumableTargeting()
        {
            _awaitingConsumableId = null;
            _awaitingConsumableSlotIndex = -1;
        }

        void ClearConsumableTargeting() => CancelConsumableTargeting();

        public bool TryDeselectCard(int instanceId)
        {
            if (_state.Phase != TurnPhase.Planning)
                return false;

            if (_awaitingTargetCardId == instanceId)
            {
                _awaitingTargetCardId = null;
                return true;
            }

            var index = _selectedQueue.IndexOf(instanceId);
            if (index < 0)
                return false;

            var card = _state.GetCard(instanceId);
            if (card == null)
                return false;

            _selectedQueue.RemoveAt(index);
            _targetByCard.Remove(instanceId);
            _state.EnergyCurrent += card.Cost;

            EmitEnergyEvent(BattleEventKind.CardDeselectedFromPlay, card.DisplayName, instanceId);
            return true;
        }

        public bool ToggleCard(int instanceId) =>
            _selectedQueue.Contains(instanceId) ? TryDeselectCard(instanceId) : TrySelectCard(instanceId);

        public BattlePlan CommitToPlan()
        {
            var plan = new BattlePlan();
            plan.PlayQueue.AddRange(_selectedQueue);
            plan.EnergySpent = 0;
            foreach (var cardId in _selectedQueue)
            {
                var card = _state.GetCard(cardId);
                if (card == null)
                    continue;

                plan.EnergySpent += card.Cost;
                if (_targetByCard.TryGetValue(cardId, out var targetId))
                    plan.TargetByCardInstanceId[cardId] = targetId;
            }

            return plan;
        }

        public void Reset()
        {
            _selectedQueue.Clear();
            _targetByCard.Clear();
            _awaitingTargetCardId = null;
            CancelConsumableTargeting();
        }

        public void RefundAllSelections()
        {
            while (_selectedQueue.Count > 0)
                TryDeselectCard(_selectedQueue[0]);

            _awaitingTargetCardId = null;
        }

        void CompleteSelect(CardInstanceState card, int instanceId)
        {
            _selectedQueue.Add(instanceId);
            _state.EnergyCurrent -= card.Cost;
            EmitEnergyEvent(BattleEventKind.CardSelectedForPlay, card.DisplayName, instanceId);
        }

        void EmitEnergyEvent(BattleEventKind kind, string message, int cardInstanceId)
        {
            _events.Add(new BattleEvent(kind, message)
            {
                CardInstanceId = cardInstanceId,
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });
            _events.Add(new BattleEvent(BattleEventKind.EnergyChanged, message)
            {
                Energy = _state.EnergyCurrent,
                EnergyMax = _state.EnergyMax,
                EnergyRemaining = _state.EnergyCurrent
            });
        }

        bool TryGetSelectableCard(int instanceId, out CardInstanceState card)
        {
            card = null;
            if (!_state.CardsById.TryGetValue(instanceId, out var c))
                return false;

            card = c;
            if (!c.IsUsable)
                return false;

            if (!_state.PlayerHand.Contains(c))
                return false;

            var ownerId = PositionRules.GetOwnerCombatantId(_state, c);
            if (ownerId == null)
                return false;

            var owner = _state.GetCombatant(ownerId);
            if (owner == null || owner.Team != TeamSide.Player || !owner.IsAlive)
                return false;

            return true;
        }
    }
}
