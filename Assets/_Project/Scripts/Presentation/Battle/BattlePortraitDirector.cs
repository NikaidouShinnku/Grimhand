using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
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
        BattleUiIconCatalogSO _uiIcons;

        readonly Dictionary<string, CombatantPortraitView> _portraits = new();
        readonly Queue<List<BattleEvent>> _segmentQueue = new();
        Coroutine _playback;
        bool _playing;

        public bool IsPlaying => _playing;

        public void Initialize(
            BattleSession session,
            BattleScreenView screen,
            CharacterVisualCatalogSO visuals,
            BattleActionEffectCatalogSO effects = null,
            BattleUiIconCatalogSO uiIcons = null)
        {
            _session = session;
            _screen = screen;
            _visuals = visuals;
            _effects = effects;
            _uiIcons = uiIcons;
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

            _session.PresentationSnapshot?.SyncBlockFromLive(_session.Engine?.State);
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
                            yield return PlayBlockGainPresentation(e);
                            break;
                        case BattleEventKind.IronWallConverted:
                            ApplySnapshotAfterIronWallConversion(e.CombatantId, e.Amount);
                            ApplyEventDisplayCheckpoint(e);
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
                                ApplyEventDisplayCheckpoint(e);
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

        void ApplyEventDisplayCheckpoint(BattleEvent e)
        {
            if (e == null || e.EventIndex < 0)
                return;

            _session.PresentationSnapshot?.ApplyEventCheckpoint(e.EventIndex);
            _screen?.Refresh();
        }

        void ApplySnapshotAfterBlockGain(string combatantId, int amount)
        {
            _session.PresentationSnapshot?.ApplyBlockGain(combatantId, amount);
            _screen?.Refresh();
        }

        void ApplySnapshotAfterIronWallConversion(string combatantId, int amount)
        {
            _session.PresentationSnapshot?.ApplyIronWallConversion(combatantId, amount);
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

            var center = _screen.GetDuelCenterWorldPosition(card.ActorId);
            var pose = ResolveCardPose(card.CardType);
            card.ActorAtCenter = true;
            yield return actor.MoveToCenter(center);
            actor.ShowPose(pose);
            PlayCardCastSfx(card);
            if (pose == PortraitPoseKind.Attack)
                yield return actor.HoldPose(AttackWindUpDuration);
        }

        void PlayCardCastSfx(CardPlayContext card)
        {
            if (card == null)
                return;

            var combatant = _session.Engine?.State?.GetCombatant(card.ActorId);
            var characterId = combatant?.CharacterDefinitionId ?? "";
            var isEnemy = combatant != null && combatant.Team == TeamSide.Enemy;

            switch (card.CardType)
            {
                case CardType.Attack:
                    GameAudioService.Instance.PlayBattleAttack(characterId, isEnemy);
                    break;
                case CardType.Status:
                    GameAudioService.Instance.PlayBattleCast();
                    break;
                case CardType.Defense:
                    GameAudioService.Instance.PlayBattleGainArmor();
                    break;
            }
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
                yield return actor.ReturnHome();

            _screen?.SyncCombatantSlotLayout(card.ActorId);
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
                yield return PlayDamageOverlayEffects(e);
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

            GameAudioService.Instance.PlayBattleStatusEffect(e.TargetId);
            yield return target.PlayHitReaction(e.Amount, useHitPose: false);
            ApplySnapshotAfterDamage(e.CombatantId, e.Amount);
            ApplyEventDisplayCheckpoint(e);
        }

        IEnumerator HandleStatusApplied(BattleEvent e)
        {
            if (!IsTargetPresentationActive(e.CombatantId))
                yield break;

            if (!_portraits.TryGetValue(e.CombatantId, out var target))
                yield break;

            var statusFx = BattleActionEffectResolver.ResolveStatus(_effects, e.TargetId);
            if (statusFx != null)
                yield return target.PlayOverlayEffect(statusFx);

            GameAudioService.Instance.PlayBattleStatusEffect(e.TargetId);
            _session.PresentationSnapshot?.ApplyFootStatusApplied(e.CombatantId, e.TargetId, e.Amount);
            ApplyEventDisplayCheckpoint(e);
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
            if (!_portraits.TryGetValue(e.CombatantId, out var healed))
                yield break;

            yield return PlayHealEffect(healed, e.IsLifesteal);
            GameAudioService.Instance.PlayBattleHealing();
            ApplySnapshotAfterHeal(e.CombatantId, e.Amount);
            healed.ShowHealNumber(e.Amount);
            ApplyEventDisplayCheckpoint(e);
        }

        IEnumerator PlayBlockGainPresentation(BattleEvent e)
        {
            if (e == null)
                yield break;

            if (IsBlockRemovalEvent(e))
            {
                if (e.Amount > 0)
                    _session.PresentationSnapshot?.ApplyBlockConsumed(e.CombatantId, e.Amount);
                else
                    _session.PresentationSnapshot?.ClearBlock(e.CombatantId);
                _screen?.Refresh();
                ApplyEventDisplayCheckpoint(e);
                yield break;
            }

            if (ShouldPlayBlockGainOverlay(e)
                && IsCombatantPresentationActive(e.CombatantId)
                && _portraits.TryGetValue(e.CombatantId, out var target))
            {
                GameAudioService.Instance.PlayBattleGainArmor();
                yield return PlayBlockGainOverlay(target);
            }

            ApplySnapshotAfterBlockGain(e.CombatantId, e.Amount);
            ApplyEventDisplayCheckpoint(e);
        }

        static bool IsBlockRemovalEvent(BattleEvent e) =>
            e != null
            && !string.IsNullOrEmpty(e.Message)
            && (e.Message.Contains("护甲被移除") || e.Message.Contains("消耗护甲"));

        static bool ShouldPlayBlockGainOverlay(BattleEvent e) =>
            e is { Amount: > 0 } && !IsBlockRemovalEvent(e);

        IEnumerator PlayBlockGainOverlay(CombatantPortraitView target)
        {
            var sprite = _uiIcons?.ArmorIcon ?? _effects?.Blocking;
            if (sprite == null)
                yield break;

            yield return target.PlayOverlayEffect(sprite);
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

            if (e.Amount <= 0)
                yield break;

            var actorDefId = GetCharacterDefinitionId(e.CombatantId);
            var damageFx = BattleActionEffectResolver.ResolveDamageEffect(_effects, actorDefId);
            if (damageFx != null)
                yield return target.PlayOverlayEffect(damageFx);
        }

        IEnumerator PlayDamageReactionOnly(BattleEvent e, CardPlayContext card, CombatantPortraitView target)
        {
            var retainCardPose = card != null && card.ActorId == e.TargetId;
            var blocked = e.BlockedAmount > 0;
            var hpDamage = e.Amount;
            var respondDefense = e.HadRespondDefense || e.RespondMitigatedAmount > 0;
            var useDefensePose = respondDefense && !retainCardPose;
            var useHitPose = !retainCardPose && !useDefensePose;

            if (hpDamage <= 0 && IsDodgeEvent(e))
                target.ShowDodgeNumber();

            if (useDefensePose)
            {
                GameAudioService.Instance.PlayBattleBlocking();
                var blockingSprite = _effects?.Blocking;
                if (blockingSprite != null)
                    target.StartCoroutine(target.PlayOverlayEffect(blockingSprite));
                yield return target.PlayInPlacePose(PortraitPoseKind.Defense, DefenseReactDuration);
            }

            if (blocked)
            {
                GameAudioService.Instance.PlayBattleHit(absorbedByArmor: true);
                ApplySnapshotAfterBlockConsumed(e.TargetId, e.BlockedAmount);
                target.ShowBlockAbsorbedNumber(e.BlockedAmount);
            }

            if (hpDamage > 0)
            {
                if (!blocked)
                    GameAudioService.Instance.PlayBattleHit(absorbedByArmor: false);

                if (useDefensePose)
                {
                    target.ShowHpDamageNumber(hpDamage);
                    yield return target.PlayDamageFlashOnly();
                }
                else
                {
                    yield return target.PlayHitReaction(
                        hpDamage,
                        useHitPose: useHitPose,
                        retainPoseAfter: retainCardPose);
                }

                ApplySnapshotAfterDamage(e.TargetId, hpDamage);
                _session.PresentationSnapshot?.SyncIronWallPendingFromLive(_session.Engine?.State, e.CombatantId);
                _screen?.Refresh();
            }
            else if (!useDefensePose)
            {
                yield return target.PlayHitReaction(
                    0,
                    useHitPose: useHitPose,
                    retainPoseAfter: retainCardPose);
            }

            ApplyEventDisplayCheckpoint(e);
        }

        IEnumerator HandleDamageWaveGap(BattleEvent e)
        {
            switch (e.Kind)
            {
                case BattleEventKind.CharacterDied:
                    yield return HandleDeath(e);
                    break;
                case BattleEventKind.BlockGained:
                    yield return PlayBlockGainPresentation(e);
                    break;
                case BattleEventKind.IronWallConverted:
                    ApplySnapshotAfterIronWallConversion(e.CombatantId, e.Amount);
                    ApplyEventDisplayCheckpoint(e);
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
                or BattleEventKind.IronWallConverted
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

            var center = _screen.GetDuelCenterWorldPosition(e.CombatantId);
            yield return defender.PlayParryCounterAttack(ParryCounterDuration, center);
        }

        IEnumerator HandleDeath(BattleEvent e)
        {
            if (_portraits.TryGetValue(e.CombatantId, out var target) && !target.IsDeadDisplay)
                yield return target.PlayDeathSequence();

            ApplySnapshotAfterDeath(e.CombatantId);
            ApplyEventDisplayCheckpoint(e);
        }

        IEnumerator HandleRevive(BattleEvent e)
        {
            if (_portraits.TryGetValue(e.CombatantId, out var target))
            {
                var unit = _session.Engine?.State?.GetCombatant(e.CombatantId);
                if (unit != null)
                    target.SetIdentity(e.CombatantId, unit.CharacterDefinitionId, true, unit.Team);

                yield return PlayHealEffect(target);
                ApplySnapshotAfterHeal(e.CombatantId, e.Amount);
                target.ShowHealNumber(e.Amount);
            }

            ApplyEventDisplayCheckpoint(e);
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
