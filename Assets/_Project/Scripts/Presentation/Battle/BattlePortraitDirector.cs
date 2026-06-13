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
        const float AttackWindUpDuration = 0.18f;
        const float ParryCounterDuration = 0.85f;

        BattleSession _session;
        BattleScreenView _screen;
        CharacterVisualCatalogSO _visuals;
        BattleActionEffectCatalogSO _effects;

        readonly Dictionary<string, CombatantPortraitView> _portraits = new();
        readonly Queue<List<BattleEvent>> _segmentQueue = new();
        Coroutine _playback;
        bool _playing;

        public bool IsPlaying => _playing;

        public void Initialize(
            BattleSession session,
            BattleScreenView screen,
            CharacterVisualCatalogSO visuals,
            BattleActionEffectCatalogSO effects = null)
        {
            _session = session;
            _screen = screen;
            _visuals = visuals;
            _effects = effects;
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
                            yield return PlayHealPresentation(e);
                            break;
                        case BattleEventKind.StatusApplied:
                            yield return HandleStatusApplied(e);
                            break;
                        case BattleEventKind.DamageApplied:
                            card?.MarkDamage();
                            var (damageWave, waveGaps, waveEnd) = CollectActorDamageWave(events, i);
                            if (damageWave.Count > 1)
                                yield return HandleDamageBatch(damageWave, card);
                            else
                                yield return HandleDamage(damageWave[0], card);

                            foreach (var gap in waveGaps)
                                yield return HandleDamageWaveGap(gap);

                            i = waveEnd;
                            break;
                        case BattleEventKind.ParryTriggered:
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
            if (pose == PortraitPoseKind.Attack)
                yield return actor.HoldPose(AttackWindUpDuration);
        }

        IEnumerator EndCardPlay(CardPlayContext card)
        {
            if (!_portraits.TryGetValue(card.ActorId, out var actor))
                yield break;

            if (card.HadDamage)
                yield return BattlePresentationSpeed.Wait(PostActionPause);
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

            var enemyAttackingPlayer = card != null
                                       && card.ActorId == e.CombatantId
                                       && IsPlayerTeamActor(e.TargetId);

            if (enemyAttackingPlayer)
            {
                yield return PlayDamageReactionOnly(e, card, target);
                yield break;
            }

            yield return PlayDamageOverlayEffects(e);
            yield return PlayDamageReactionOnly(e, card, target);
        }

        IEnumerator HandleStatusTick(BattleEvent e)
        {
            if (!IsTargetPresentationActive(e.CombatantId))
                yield break;

            if (!_portraits.TryGetValue(e.CombatantId, out var target))
                yield break;

            var statusFx = BattleActionEffectResolver.ResolveStatus(_effects, e.TargetId);
            if (statusFx != null)
                yield return target.PlayOverlayEffect(statusFx);

            ApplySnapshotAfterDamage(e.CombatantId, e.Amount);
            yield return target.PlayHitReaction(e.Amount, useHitPose: false);
        }

        IEnumerator HandleStatusApplied(BattleEvent e)
        {
            if (!IsTargetPresentationActive(e.CombatantId))
                yield break;

            if (!_portraits.TryGetValue(e.CombatantId, out var target))
                yield break;

            var statusFx = BattleActionEffectResolver.ResolveStatus(_effects, e.TargetId);
            if (statusFx == null)
                yield break;

            yield return target.PlayOverlayEffect(statusFx);
        }

        IEnumerator PlayHealEffect(CombatantPortraitView target, bool isLifesteal = false)
        {
            if (target == null)
                yield break;

            if (_effects?.Healing != null)
            {
                var duration = isLifesteal ? 0.75f : 0.55f;
                yield return target.PlayOverlayEffect(_effects.Healing, duration);
                yield break;
            }

            yield return target.PlayHealFlash();
        }

        IEnumerator PlayHealPresentation(BattleEvent e)
        {
            ApplySnapshotAfterHeal(e.CombatantId, e.Amount);
            if (!_portraits.TryGetValue(e.CombatantId, out var healed))
                yield break;

            yield return PlayHealEffect(healed, e.IsLifesteal);
            healed.ShowHealNumber(e.Amount);
        }

        IEnumerator PlayBlockingEffect(CombatantPortraitView target)
        {
            if (_effects?.Blocking == null)
                yield break;

            yield return target.PlayOverlayEffect(_effects.Blocking);
        }

        IEnumerator PlaySacrificeEffect(CombatantPortraitView target)
        {
            if (_effects?.SacrificeBurst == null)
                yield break;

            yield return target.PlayOverlayEffect(_effects.SacrificeBurst);
        }

        bool IsPlayerTeamActor(string combatantId)
        {
            var unit = _session?.Engine?.State?.GetCombatant(combatantId);
            return unit != null && unit.Team == TeamSide.Player;
        }

        string GetCharacterDefinitionId(string combatantId)
        {
            var unit = _session?.Engine?.State?.GetCombatant(combatantId);
            return unit?.CharacterDefinitionId ?? "";
        }

        IEnumerator HandleDamageBatch(IReadOnlyList<BattleEvent> batch, CardPlayContext card)
        {
            var overlayRoutines = new List<IEnumerator>();
            foreach (var e in batch)
            {
                if (!IsTargetPresentationActive(e.TargetId))
                    continue;

                if (!_portraits.TryGetValue(e.TargetId, out _))
                    continue;

                overlayRoutines.Add(PlayDamageOverlayEffects(e));
            }

            if (overlayRoutines.Count > 0)
                yield return RunParallel(overlayRoutines);

            var reactionRoutines = new List<IEnumerator>();
            foreach (var e in batch)
            {
                if (!IsTargetPresentationActive(e.TargetId))
                    continue;

                if (!_portraits.TryGetValue(e.TargetId, out var target))
                    continue;

                reactionRoutines.Add(PlayDamageReactionOnly(e, card, target));
            }

            if (reactionRoutines.Count > 0)
                yield return RunParallel(reactionRoutines);
        }

        IEnumerator PlayDamageOverlayEffects(BattleEvent e)
        {
            if (!_portraits.TryGetValue(e.TargetId, out var target))
                yield break;

            if (e.IsSacrificeDamage)
            {
                yield return PlaySacrificeEffect(target);
                yield break;
            }

            if (e.Amount > 0 && IsPlayerTeamActor(e.CombatantId))
            {
                var actorDefId = GetCharacterDefinitionId(e.CombatantId);
                var damageFx = BattleActionEffectResolver.ResolvePlayerDamage(_effects, actorDefId);
                if (damageFx != null)
                    yield return target.PlayOverlayEffect(damageFx);
            }
        }

        IEnumerator PlayDamageReactionOnly(BattleEvent e, CardPlayContext card, CombatantPortraitView target)
        {
            var retainCardPose = card != null && card.ActorId == e.TargetId;
            var blocked = e.BlockedAmount > 0;
            var hpDamage = e.Amount;
            var respondDefense = e.HadRespondDefense || e.RespondMitigatedAmount > 0;

            if ((blocked || respondDefense) && !retainCardPose && IsPlayerTeamActor(e.TargetId))
            {
                yield return RunParallel(new List<IEnumerator>
                {
                    target.PlayInPlacePose(PortraitPoseKind.Defense, DefenseReactDuration),
                    PlayBlockingEffect(target)
                });
            }
            else if (blocked && !retainCardPose)
                yield return target.PlayInPlacePose(PortraitPoseKind.Defense, DefenseReactDuration);

            if (blocked)
            {
                ApplySnapshotAfterBlockConsumed(e.TargetId, e.BlockedAmount);
                target.ShowBlockAbsorbedNumber(e.BlockedAmount);
            }

            if (hpDamage > 0)
            {
                yield return target.PlayHitReaction(
                    hpDamage,
                    useHitPose: !blocked && !retainCardPose,
                    retainPoseAfter: retainCardPose);
                ApplySnapshotAfterDamage(e.TargetId, hpDamage);
            }
            else if (blocked || respondDefense)
            {
                if (retainCardPose)
                    yield return target.PlayDamageFlashOnly();
                else
                    yield return target.PlayBlockedReaction(blocked ? e.BlockedAmount : 0);
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

        IEnumerator HandleDamageWaveGap(BattleEvent e)
        {
            switch (e.Kind)
            {
                case BattleEventKind.CharacterDied:
                    yield return HandleDeath(e);
                    break;
                case BattleEventKind.BlockGained:
                    ApplySnapshotAfterBlockGain(e.CombatantId, e.Amount);
                    break;
                case BattleEventKind.HealApplied:
                    yield return PlayHealPresentation(e);
                    break;
                case BattleEventKind.StatusApplied:
                    yield return HandleStatusApplied(e);
                    break;
                default:
                    yield break;
            }
        }

        static (List<BattleEvent> damages, List<BattleEvent> gaps, int lastConsumed) CollectActorDamageWave(
            IReadOnlyList<BattleEvent> events,
            int startIndex)
        {
            var gaps = new List<BattleEvent>();
            var batch = new List<BattleEvent> { events[startIndex] };
            var actorId = events[startIndex].CombatantId;
            var aoeWave = events[startIndex].IsAoEWave;
            var lastConsumed = startIndex;

            for (var j = startIndex + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind == BattleEventKind.DamageApplied
                    && next.CombatantId == actorId
                    && (!aoeWave || next.IsAoEWave))
                {
                    batch.Add(next);
                    lastConsumed = j;
                    continue;
                }

                if (IsDamageWaveGapEvent(next.Kind, aoeWave))
                {
                    gaps.Add(next);
                    lastConsumed = j;
                    continue;
                }

                break;
            }

            return (batch, gaps, lastConsumed);
        }

        static bool IsDamageWaveGapEvent(BattleEventKind kind, bool aoeWave)
        {
            if (kind is BattleEventKind.CharacterDied
                or BattleEventKind.BlockGained
                or BattleEventKind.HealApplied
                or BattleEventKind.StatusApplied
                or BattleEventKind.DeckPolluted
                or BattleEventKind.CardDrawn
                or BattleEventKind.EnergyChanged
                or BattleEventKind.ReactionTriggered)
            {
                return true;
            }

            return aoeWave && kind == BattleEventKind.CardDiscarded;
        }

        IEnumerator RunParallel(IReadOnlyList<IEnumerator> routines)
        {
            if (routines == null || routines.Count == 0)
                yield break;

            var remaining = routines.Count;
            foreach (var routine in routines)
                StartCoroutine(RunRoutine(routine, () => remaining--));

            yield return new WaitUntil(() => remaining <= 0);
        }

        static IEnumerator RunRoutine(IEnumerator routine, System.Action onComplete)
        {
            if (routine != null)
                yield return routine;

            onComplete?.Invoke();
        }

        IEnumerator HandleParryCounter(BattleEvent e)
        {
            if (!IsCombatantPresentationActive(e.CombatantId))
                yield break;

            if (!_portraits.TryGetValue(e.CombatantId, out var defender))
                yield break;

            var center = _screen.GetDuelCenterWorldPosition();
            yield return defender.PlayParryCounterAttack(ParryCounterDuration, center);
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

                yield return PlayHealEffect(target);
                target.ShowHealNumber(e.Amount);
            }

            _screen?.Refresh();
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
