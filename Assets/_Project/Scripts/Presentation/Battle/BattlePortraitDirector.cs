using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    /// <summary>消费战斗事件队列，驱动立绘 idle / 出牌 / 受击 / 死亡动画。</summary>
    public sealed class BattlePortraitDirector : MonoBehaviour
    {
        const float DefenseReactDuration = 1f;
        const float DefenseCardHoldDuration = 1f;
        const float NeutralCardHoldDuration = 1f;
        const float PostActionPause = 0.15f;

        BattleSession _session;
        BattleScreenView _screen;
        CharacterVisualCatalogSO _visuals;

        readonly Dictionary<string, CombatantPortraitView> _portraits = new();
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

            if (_session.Engine.State.Phase == TurnPhase.Planning)
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
            if (events == null || events.Count == 0 || !ContainsPresentationEvents(events))
                return;

            if (_playback != null)
                StopCoroutine(_playback);

            _playback = StartCoroutine(PlayEvents(events));
        }

        static bool ContainsPresentationEvents(IReadOnlyList<BattleEvent> events)
        {
            foreach (var e in events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.PortraitPoseChanged:
                    case BattleEventKind.PortraitIdleRestored:
                    case BattleEventKind.DamageApplied:
                    case BattleEventKind.StatusTickDamage:
                    case BattleEventKind.CharacterDied:
                        return true;
                }
            }

            return false;
        }

        IEnumerator PlayEvents(IReadOnlyList<BattleEvent> events)
        {
            _playing = true;
            _session.PresentationLocked = true;
            _screen?.StopAllPortraitIdleLoops();
            _screen?.Refresh();
            RebuildLookup();

            CardPlayContext card = null;
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Kind)
                {
                    case BattleEventKind.PortraitPoseChanged:
                        card = new CardPlayContext(e.CombatantId, e.CardType);
                        yield return BeginCardPlay(card);
                        break;
                    case BattleEventKind.DamageApplied:
                        card?.MarkDamage();
                        yield return HandleDamage(e, card);
                        _screen?.Refresh();
                        break;
                    case BattleEventKind.StatusTickDamage:
                        yield return HandleStatusTick(e);
                        _screen?.Refresh();
                        break;
                    case BattleEventKind.CharacterDied:
                        yield return HandleDeath(e);
                        _screen?.Refresh();
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

            _playing = false;
            _playback = null;
            _session.PresentationLocked = false;
            RebuildLookup();
            _screen?.Refresh();

            if (_session?.Engine?.State.Phase == TurnPhase.Planning)
                _screen?.BeginPlanningIdleLoops();
        }

        IEnumerator BeginCardPlay(CardPlayContext card)
        {
            if (!_portraits.TryGetValue(card.ActorId, out var actor))
                yield break;

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
                yield return actor.ReturnHome();
        }

        IEnumerator HandleDamage(BattleEvent e, CardPlayContext card)
        {
            if (!_portraits.TryGetValue(e.TargetId, out var target))
                yield break;

            var blocked = e.BlockedAmount > 0;
            var hpDamage = e.Amount;

            if (blocked)
                yield return target.PlayInPlacePose(PortraitPoseKind.Defense, DefenseReactDuration);

            if (hpDamage > 0)
                yield return target.PlayHitReaction(hpDamage, useHitPose: !blocked);
            else if (blocked)
                yield return target.PlayBlockedReaction();
        }

        IEnumerator HandleStatusTick(BattleEvent e)
        {
            if (!_portraits.TryGetValue(e.CombatantId, out var target))
                yield break;

            yield return target.PlayHitReaction(e.Amount, useHitPose: false);
        }

        IEnumerator HandleDeath(BattleEvent e)
        {
            if (!_portraits.TryGetValue(e.CombatantId, out var target))
                yield break;

            yield return target.PlayDeathSequence();
        }

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
            public CardPlayContext(string actorId, CardType cardType)
            {
                ActorId = actorId;
                CardType = cardType;
            }

            public string ActorId { get; }
            public CardType CardType { get; }
            public bool ActorAtCenter;
            public bool HadDamage;

            public void MarkDamage() => HadDamage = true;
        }
    }
}
