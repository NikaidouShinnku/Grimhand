using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>消费战斗事件队列，按段落驱动立绘 idle / 出牌 / 受击 / 死亡动画。</summary>
    public sealed class BattlePortraitDirector : MonoBehaviour
    {
        const float DefenseReactDuration = 1f;
        const float DefenseCardHoldDuration = 1f;
        const float NeutralCardHoldDuration = 1f;
        const float PostActionPause = 0.15f;
        const float ParryCounterDuration = 0.85f;

        BattleSession _session;
        BattleScreenView _screen;
        CharacterVisualCatalogSO _visuals;

        readonly Dictionary<string, CombatantPortraitView> _portraits = new();
        readonly Queue<List<BattleEvent>> _segmentQueue = new();
        Coroutine _playback;
        bool _playing;

        public bool IsPlaying => _playing;

        public void Initialize(BattleSession session, BattleScreenView screen, CharacterVisualCatalogSO visuals)
        {
            _session = session;
            _screen = screen;
            _visuals = visuals;
            RebuildLookup();
            _session.EventsProduced += OnEventsProduced;
        }

        void OnDestroy()
        {
            if (_session != null)
                _session.EventsProduced -= OnEventsProduced;
        }

        void Update()
        {
            if (_playing || _session?.Engine == null)
                return;

            if (_session.Engine.State.Phase == TurnPhase.Planning && !_session.PresentationLocked)
                _screen?.BeginPlanningIdleLoops();
        }

        public void RebuildLookup()
        {
            _portraits.Clear();
            if (_screen == null)
                return;

            foreach (var view in _screen.AllPortraitViews())
            {
                if (view != null && !string.IsNullOrEmpty(view.CombatantId))
                    _portraits[view.CombatantId] = view;
            }
        }

        void OnEventsProduced(IReadOnlyList<BattleEvent> events)
        {
            if (events == null || events.Count == 0 || !BattleEventPlayback.ContainsPresentationEvents(events))
                return;

            var segments = BattleEventPlayback.SplitIntoSegments(events);
            foreach (var segment in segments)
                _segmentQueue.Enqueue(segment);

            if (_playback == null)
                _playback = StartCoroutine(ProcessSegmentQueue());
        }

        IEnumerator ProcessSegmentQueue()
        {
            _playing = true;
            _session.PresentationLocked = true;
            _screen?.StopAllPortraitIdleLoops();
            RebuildLookup();

            while (_segmentQueue.Count > 0)
            {
                var segment = _segmentQueue.Dequeue();
                yield return PlaySegment(segment);
            }

            RebuildLookup();
            foreach (var view in _portraits.Values)
                view?.ForceSettleHome();

            _session.PresentationSnapshot?.ClearAllBlock();
            _playing = false;
            _playback = null;
            _screen?.HideActiveCard();
            _session.OnPresentationComplete();
            RebuildLookup();
            _screen?.Refresh();

            if (_session?.Engine?.State.Phase == TurnPhase.Planning && !_session.PresentationLocked)
                _screen?.BeginPlanningIdleLoops();
        }

        IEnumerator PlaySegment(IReadOnlyList<BattleEvent> events)
        {
            RebuildLookup();

            CardPlayContext card = null;
            try
            {
                for (var i = 0; i < events.Count; i++)
                {
                    var e = events[i];
                    switch (e.Kind)
                    {
                        case BattleEventKind.PortraitPoseChanged:
                            if (!IsCombatantPresentationActive(e.CombatantId))
                                break;

                            card = new CardPlayContext(e.CombatantId, e.CardType, e.CardInstanceId);
                            _screen?.ShowActiveCard(e.CardInstanceId);
                            yield return BeginCardPlay(card);
                            break;
                        case BattleEventKind.BlockGained:
                            ApplySnapshotAfterBlockGain(e.CombatantId, e.Amount);
                            break;
                        case BattleEventKind.HealApplied:
                            ApplySnapshotAfterHeal(e.CombatantId, e.Amount);
                            if (_portraits.TryGetValue(e.CombatantId, out var healed))
                                healed.ShowHealNumber(e.Amount);
                            break;
                        case BattleEventKind.DamageApplied:
                            card?.MarkDamage();
                            yield return HandleDamage(e, card);
                            break;
                        case BattleEventKind.ParryTriggered:
                            yield return HandleParryCounter(e);
                            break;
                        case BattleEventKind.StatusTickDamage:
                            yield return HandleStatusTick(e);
                            break;
                        case BattleEventKind.CharacterDied:
                            yield return HandleDeath(e);
                            break;
                        case BattleEventKind.CharacterRevived:
                            yield return HandleRevive(e);
                            break;
                        case BattleEventKind.PortraitIdleRestored:
                            if (card != null && card.ActorId == e.CombatantId)
                            {
                                yield return EndCardPlay(card);
                                card = null;
                            }

                            break;
                    }
                }

                if (card != null)
                    yield return EndCardPlay(card);
            }
            finally
            {
                _screen?.HideActiveCard();
            }
        }

        bool IsCombatantPresentationActive(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return false;

            var snapshot = _session.PresentationSnapshot;
            if (snapshot != null)
                return snapshot.IsAlive(combatantId);

            var unit = _session.Engine?.State?.GetCombatant(combatantId);
            return unit != null && unit.IsAlive;
        }

        bool IsTargetPresentationActive(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return false;

            if (_portraits.TryGetValue(combatantId, out var view) && view.IsDeadDisplay)
                return false;

            return IsCombatantPresentationActive(combatantId);
        }

        void ApplySnapshotAfterBlockGain(string combatantId, int amount)
        {
            _session.PresentationSnapshot?.ApplyBlockGain(combatantId, amount);
            _screen?.Refresh();
        }

        void ApplySnapshotAfterBlockConsumed(string combatantId, int amount)
        {
            if (amount <= 0)
                return;

            _session.PresentationSnapshot?.ApplyBlockConsumed(combatantId, amount);
            _screen?.Refresh();
        }

        void ApplySnapshotAfterDamage(string combatantId, int amount)
        {
            _session.PresentationSnapshot?.ApplyDamage(combatantId, amount);
            _screen?.Refresh();
        }

        void ApplySnapshotAfterHeal(string combatantId, int amount)
        {
            _session.PresentationSnapshot?.ApplyHeal(combatantId, amount);
            _screen?.Refresh();
        }

        void ApplySnapshotAfterDeath(string combatantId)
        {
            _session.PresentationSnapshot?.MarkDead(combatantId);
            _screen?.Refresh();
        }

        IEnumerator BeginCardPlay(CardPlayContext card)
        {
            if (!_portraits.TryGetValue(card.ActorId, out var actor))
                yield break;

            // 同一角色连续行动：若仍停留在中央，先完整归位再第二次出场。
            if (actor.IsAwayFromHome)
                yield return actor.ReturnHome();

            var center = _screen.GetDuelCenterWorldPosition();
            var pose = ResolveCardPose(card.CardType);
            card.ActorAtCenter = true;
            yield return actor.MoveToCenter(center);
            actor.ShowPose(pose);
        }

        IEnumerator EndCardPlay(CardPlayContext card)
        {
            if (!_portraits.TryGetValue(card.ActorId, out var actor))
                yield break;

            if (card.HadDamage)
                yield return new WaitForSeconds(PostActionPause);
            else if (card.CardType == CardType.Defense)
                yield return actor.HoldPose(DefenseCardHoldDuration);
            else
                yield return actor.HoldPose(NeutralCardHoldDuration);

            if (card.ActorAtCenter)
            {
                if (actor.IsAwayFromHome)
                    yield return actor.ReturnHome();
                else
                    actor.RestoreHomePosition();
            }
        }

        IEnumerator HandleDamage(BattleEvent e, CardPlayContext card)
        {
            if (!IsTargetPresentationActive(e.TargetId))
                yield break;

            if (!_portraits.TryGetValue(e.TargetId, out var target))
                yield break;

            var retainCardPose = card != null && card.ActorId == e.TargetId;
            var blocked = e.BlockedAmount > 0;
            var hpDamage = e.Amount;

            if (blocked)
                ApplySnapshotAfterBlockConsumed(e.TargetId, e.BlockedAmount);

            if (blocked)
                target.ShowBlockAbsorbedNumber(e.BlockedAmount);

            if (blocked && !retainCardPose)
                yield return target.PlayInPlacePose(PortraitPoseKind.Defense, DefenseReactDuration);

            if (hpDamage > 0)
            {
                ApplySnapshotAfterDamage(e.TargetId, hpDamage);
                yield return target.PlayHitReaction(
                    hpDamage,
                    useHitPose: !blocked && !retainCardPose,
                    retainPoseAfter: retainCardPose);
            }
            else if (blocked)
            {
                if (retainCardPose)
                    yield return target.PlayDamageFlashOnly();
                else
                    yield return target.PlayBlockedReaction(e.BlockedAmount);
            }
            else if (IsDodgeEvent(e))
            {
                target.ShowDodgeNumber();
                _screen?.Refresh();
            }
            else
            {
                _screen?.Refresh();
            }
        }

        IEnumerator HandleStatusTick(BattleEvent e)
        {
            if (!IsTargetPresentationActive(e.CombatantId))
                yield break;

            if (!_portraits.TryGetValue(e.CombatantId, out var target))
                yield break;

            ApplySnapshotAfterDamage(e.CombatantId, e.Amount);
            yield return target.PlayHitReaction(e.Amount, useHitPose: false);
        }

        IEnumerator HandleParryCounter(BattleEvent e)
        {
            if (!IsCombatantPresentationActive(e.CombatantId))
                yield break;

            if (!_portraits.TryGetValue(e.CombatantId, out var defender))
                yield break;

            yield return defender.PlayParryCounterAttack(ParryCounterDuration);
        }

        IEnumerator HandleDeath(BattleEvent e)
        {
            if (_portraits.TryGetValue(e.CombatantId, out var target) && !target.IsDeadDisplay)
                yield return target.PlayDeathSequence();

            ApplySnapshotAfterDeath(e.CombatantId);
        }

        IEnumerator HandleRevive(BattleEvent e)
        {
            ApplySnapshotAfterHeal(e.CombatantId, e.Amount);
            if (_portraits.TryGetValue(e.CombatantId, out var target))
            {
                var unit = _session.Engine?.State?.GetCombatant(e.CombatantId);
                if (unit != null)
                    target.SetIdentity(e.CombatantId, unit.CharacterDefinitionId, true, unit.Team);

                target.ShowHealNumber(e.Amount);
            }

            _screen?.Refresh();
            yield return null;
        }

        static bool IsDodgeEvent(BattleEvent e) =>
            e.Amount == 0
            && e.BlockedAmount <= 0
            && !string.IsNullOrEmpty(e.Message)
            && e.Message.Contains("闪避");

        static PortraitPoseKind ResolveCardPose(CardType cardType) =>
            cardType switch
            {
                CardType.Attack => PortraitPoseKind.Attack,
                CardType.Status => PortraitPoseKind.Attack,
                CardType.Defense => PortraitPoseKind.Defense,
                _ => PortraitPoseKind.Idle
            };

        sealed class CardPlayContext
        {
            public CardPlayContext(string actorId, CardType cardType, int cardInstanceId)
            {
                ActorId = actorId;
                CardType = cardType;
                CardInstanceId = cardInstanceId;
            }

            public string ActorId { get; }
            public CardType CardType { get; }
            public int CardInstanceId { get; }
            public bool ActorAtCenter;
            public bool HadDamage;

            public void MarkDamage() => HadDamage = true;
        }
    }
}
