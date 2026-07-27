using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Events;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
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
        const float SameTargetMultiHitGap = 0.28f;

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

        /// <summary>放弃远征/回营时中止未播完的立绘演出，避免锁定态带到新局。</summary>
        public void AbortPlayback()
        {
            if (_playback != null)
            {
                StopCoroutine(_playback);
                _playback = null;
            }

            _segmentQueue.Clear();
            _playing = false;
            _screen?.HideActiveCard();
            _screen?.StopAllPortraitIdleLoops();

            if (_screen != null)
            {
                foreach (var view in _screen.AllPortraitViews())
                    view?.ResetInterruptedPresentationState();
            }

            if (_session != null)
                _session.EndPresentation();
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
                        case BattleEventKind.StatusRemoved:
                            HandleStatusRemoved(e);
                            break;
                        case BattleEventKind.PositionSwapped:
                            yield return HandlePositionSwapped(e, card);
                            break;
                        case BattleEventKind.DamageApplied:
                            card?.MarkDamage();
                            var (damageWave, waveGaps, waveEnd) = CollectActorDamageWave(events, i);
                            if (damageWave.Count > 1 && IsSameTargetMultiHit(damageWave))
                            {
                                for (var hit = 0; hit < damageWave.Count; hit++)
                                {
                                    yield return HandleDamage(damageWave[hit], card);
                                    if (hit < damageWave.Count - 1)
                                        yield return new WaitForSeconds(SameTargetMultiHitGap);
                                }
                            }
                            else if (damageWave.Count > 1)
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
                        case BattleEventKind.CombatantSpawned:
                            yield return HandleCombatantSpawned(e);
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

        IEnumerator HandlePositionSwapped(BattleEvent e, CardPlayContext card)
        {
            RebuildLookup();
            if (e == null
                || string.IsNullOrEmpty(e.CombatantId)
                || string.IsNullOrEmpty(e.TargetId)
                || !_portraits.TryGetValue(e.CombatantId, out var viewA)
                || !_portraits.TryGetValue(e.TargetId, out var viewB))
            {
                _session.PresentationSnapshot?.ApplyPositionSwap(e?.CombatantId, e?.TargetId);
                _screen?.InvalidateAllEnemyHpBarLayouts();
                _screen?.Refresh();
                yield return null;
                yield break;
            }

            // 直接横向走到对方站位（保持各自当前 Y，避免 home 异常导致飞出屏幕）。
            var startA = viewA.CurrentWorldPosition;
            var startB = viewB.CurrentWorldPosition;
            var destA = new Vector3(viewB.HomeWorldPosition.x, startA.y, startA.z);
            var destB = new Vector3(viewA.HomeWorldPosition.x, startB.y, startB.z);
            yield return RunParallel(new List<IEnumerator>
            {
                viewA.MoveToWorldPosition(destA),
                viewB.MoveToWorldPosition(destB)
            });

            // 到位后再换绑：隐藏一帧避免看到回弹，再推进演出站位并 Refresh。
            viewA.SetPortraitVisible(false);
            viewB.SetPortraitVisible(false);
            viewA.SnapToHomeImmediate();
            viewB.SnapToHomeImmediate();

            if (card != null
                && (card.ActorId == e.CombatantId || card.ActorId == e.TargetId))
                card.ActorAtCenter = false;

            _session.PresentationSnapshot?.ApplyPositionSwap(e.CombatantId, e.TargetId);
            _screen?.InvalidateAllEnemyHpBarLayouts();
            _screen?.Refresh();
            RebuildLookup();

            if (_portraits.TryGetValue(e.CombatantId, out var settledA))
            {
                settledA.RecaptureHomePosition();
                settledA.SetPortraitVisible(true);
            }

            if (_portraits.TryGetValue(e.TargetId, out var settledB))
            {
                settledB.RecaptureHomePosition();
                settledB.SetPortraitVisible(true);
            }

            yield return null;
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
            // 状态/防御牌：先完成施法姿态，再进入 StatusApplied，避免脚标早于卡面演出。
            if (pose == PortraitPoseKind.Attack)
                yield return actor.HoldPose(AttackWindUpDuration);
            else if (card.CardType is CardType.Status or CardType.Defense)
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
                    // 夹断护甲：不播施法音，改由护甲移除事件播护甲受击
                    if (IsPinchArmorCard(card))
                        break;
                    GameAudioService.Instance.PlayBattleCast();
                    break;
                case CardType.Defense:
                    GameAudioService.Instance.PlayBattleGainArmor();
                    break;
            }
        }

        bool IsPinchArmorCard(CardPlayContext card)
        {
            if (card == null)
                return false;
            var inst = _session?.Engine?.State?.GetCard(card.CardInstanceId);
            return inst != null
                   && inst.DefinitionId == AbyssMonsterCardCatalog.PinchArmorCardId;
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
            // 先播状态特效，再按事件增量刷新脚标（禁止 Sync 实况，否则会提前显示本回合后续状态）。
            if (IsTargetPresentationActive(e.CombatantId)
                && _portraits.TryGetValue(e.CombatantId, out var target))
            {
                var statusFx = BattleActionEffectResolver.ResolveStatus(_effects, e.TargetId);
                if (statusFx != null)
                    yield return target.PlayOverlayEffect(statusFx);

                GameAudioService.Instance.PlayBattleStatusEffect(e.TargetId);
            }

            RevealFootStatusApplied(e);
            ApplyEventDisplayCheckpoint(e);
        }

        IEnumerator HandleCombatantSpawned(BattleEvent e)
        {
            var state = _session?.Engine?.State;
            var unit = state != null && !string.IsNullOrEmpty(e.CombatantId)
                ? state.GetCombatant(e.CombatantId)
                : null;
            if (unit != null)
                _session.PresentationSnapshot?.RegisterSpawnedCombatant(unit, state);

            _screen?.InvalidateAllEnemyHpBarLayouts();
            _screen?.Refresh();
            RebuildLookup();
            yield return null;
        }

        void HandleStatusRemoved(BattleEvent e)
        {
            RevealFootStatusRemoved(e);
            ApplyEventDisplayCheckpoint(e);
        }

        void RevealFootStatusApplied(BattleEvent e)
        {
            if (_session.PresentationSnapshot == null || string.IsNullOrEmpty(e.CombatantId))
                return;

            // 只揭示本事件对应状态，禁止 Sync 实况（否则会把同卡后续状态一并提前显示）。
            if (!string.IsNullOrEmpty(e.TargetId) && e.Amount > 0)
                _session.PresentationSnapshot.ApplyFootStatusApplied(e.CombatantId, e.TargetId, e.Amount);
            _screen?.Refresh();
        }

        void RevealFootStatusRemoved(BattleEvent e)
        {
            if (_session.PresentationSnapshot == null || string.IsNullOrEmpty(e.CombatantId))
                return;

            if (!string.IsNullOrEmpty(e.TargetId) && e.Amount > 0)
                _session.PresentationSnapshot.ApplyFootStatusRemoved(e.CombatantId, e.TargetId, e.Amount);
            else if (!string.IsNullOrEmpty(e.TargetId))
                _session.PresentationSnapshot.ApplyFootStatusRemoved(e.CombatantId, e.TargetId, int.MaxValue);
            _screen?.Refresh();
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
                {
                    // 夹断护甲 / 破甲等：用护甲受击音强化「甲被夹断」手感
                    GameAudioService.Instance.PlayBattleHit(absorbedByArmor: true);
                    _session.PresentationSnapshot?.ApplyBlockConsumed(e.CombatantId, e.Amount);
                }
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
            var hasDedicatedBlocker = !string.IsNullOrEmpty(e.RespondBlockerId);
            var respondDefenseOnTarget = !hasDedicatedBlocker
                                         && (e.HadRespondDefense || e.RespondMitigatedAmount > 0);
            var useDefensePose = respondDefenseOnTarget && !retainCardPose;
            var useHitPose = !retainCardPose && !useDefensePose;

            if (hasDedicatedBlocker)
                yield return PlayRespondBlockPresentation(e.RespondBlockerId);

            if (hpDamage <= 0 && IsDodgeEvent(e))
                target.ShowDodgeNumber();

            if (useDefensePose)
            {
                GameAudioService.Instance.PlayBattleBlocking();
                var blockingSprite = _effects?.Blocking;
                if (blockingSprite != null && target.isActiveAndEnabled && target.gameObject.activeInHierarchy)
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

        IEnumerator PlayRespondBlockPresentation(string blockerId)
        {
            if (string.IsNullOrEmpty(blockerId) || !IsCombatantPresentationActive(blockerId))
                yield break;

            if (!_portraits.TryGetValue(blockerId, out var blocker) || blocker == null)
                yield break;

            GameAudioService.Instance.PlayBattleBlocking();
            var blockingSprite = _effects?.Blocking;
            if (blockingSprite != null && blocker.isActiveAndEnabled && blocker.gameObject.activeInHierarchy)
                blocker.StartCoroutine(blocker.PlayOverlayEffect(blockingSprite));
            yield return blocker.PlayInPlacePose(PortraitPoseKind.Defense, DefenseReactDuration);
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
                case BattleEventKind.CombatantSpawned:
                    yield return HandleCombatantSpawned(e);
                    break;
                case BattleEventKind.StatusRemoved:
                    HandleStatusRemoved(e);
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

        static bool IsSameTargetMultiHit(IReadOnlyList<BattleEvent> batch)
        {
            if (batch == null || batch.Count < 2)
                return false;

            var targetId = batch[0].TargetId;
            if (string.IsNullOrEmpty(targetId))
                return false;

            for (var i = 1; i < batch.Count; i++)
            {
                if (batch[i].TargetId != targetId)
                    return false;
            }

            return true;
        }

        static bool IsDamageWaveGapEvent(BattleEventKind kind, bool aoeWave)
        {
            if (kind is BattleEventKind.CharacterDied
                or BattleEventKind.BlockGained
                or BattleEventKind.IronWallConverted
                or BattleEventKind.HealApplied
                or BattleEventKind.StatusRemoved
                or BattleEventKind.DeckPolluted
                or BattleEventKind.CardDrawn
                or BattleEventKind.EnergyChanged
                or BattleEventKind.ReactionTriggered)
            {
                return true;
            }

            // StatusApplied 不并入 damage wave，保证「伤害 → 上毒 → 下一击」按事件序播放
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
