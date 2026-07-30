using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class HandPanelView : MonoBehaviour
    {
        [SerializeField] RectTransform contentRoot;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] CardView cardPrefab;
        [SerializeField] Text handCountLabel;

        readonly List<CardView> _pool = new();
        float _savedScrollX = -1f;
        int _lastHandCount = -1;

        public CardView CardPrefab => cardPrefab;

        void Awake() => ResolveReferences();

        void ResolveReferences()
        {
            if (contentRoot == null)
                contentRoot = transform.Find("HandScroll/Viewport/Content") as RectTransform;
            if (scrollRect == null)
                scrollRect = transform.Find("HandScroll")?.GetComponent<ScrollRect>();
            if (handCountLabel == null)
                handCountLabel = transform.Find("HandCount")?.GetComponent<Text>();
        }

        public void Refresh(
            BattleState state,
            BattleSession session,
            CardVisualCatalogSO catalog,
            BattleUiIconCatalogSO uiIcons,
            CharacterVisualCatalogSO characterVisuals,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions,
            Action<int> onCardClick,
            Action<int> onQuickStart,
            Action<CardInstanceState, RectTransform> onHoverEnter,
            Action onHoverExit,
            CombatantState damagePreviewTarget = null)
        {
            if (state == null || session?.Engine == null)
                return;

            ResolveReferences();
            if (contentRoot == null || cardPrefab == null)
                return;

            var presenting = session.PresentationLocked;
            if (scrollRect != null)
                scrollRect.gameObject.SetActive(!presenting);

            if (handCountLabel != null)
                handCountLabel.gameObject.SetActive(false);

            var handCards = ResolveHandCards(state, session);
            var needed = handCards.Count;
            EnsurePool(needed);

            var resolveSteps = session.Engine.PreviewResolutionSteps();

            for (var i = 0; i < _pool.Count; i++)
            {
                var view = _pool[i];
                if (i >= needed)
                {
                    view.gameObject.SetActive(false);
                    continue;
                }

                view.gameObject.SetActive(true);
                var card = handCards[i];
                var displayCard = HolysunSpellbookRules.ApplyForDisplay(state, card);
                var draft = session.Engine.Draft;
                var awaiting = draft.AwaitingTargetCardId;
                var isAwaitingTarget = awaiting == card.InstanceId;
                var isQueued = draft.IsSelected(card.InstanceId);
                var showSelected = isQueued;
                var polluted = CardRules.IsPolluted(card);
                var engravingLocked = CardRules.HasEngravingLock(card);
                var ownerId = PositionRules.GetOwnerCombatantId(state, card);
                var owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                var playCost = draft.GetPlayCost(card);
                var canAfford = draft.EnergyRemaining >= playCost;
                var canSelect = draft.IsCardSelectable(card.InstanceId);
                var interactable = session.CanInteractWithBattle() && !polluted && !engravingLocked
                    && (isAwaitingTarget || isQueued || canSelect);
                var visual = CardVisualResolver.Resolve(card, catalog, characterVisuals, definitions);
                var stats = BattleUiFormatters.BuildCardStatsLineForHand(
                    state, draft, card, damagePreviewTarget, definitions);
                var badge = isQueued
                    ? BattleUiFormatters.BuildSelectionBadge(state, draft, card, resolveSteps)
                    : null;

                var quickStartImmediate = card.Keywords.Contains("quick_start")
                    && !CardRules.ShouldPromptForTarget(state, card, owner);

                view.BindWithCard(displayCard, visual, showSelected, polluted, interactable, badge, stats,
                    uiIcons, characterVisuals, onCardClick, onHoverEnter, onHoverExit, playCost,
                    quickStartImmediate ? onQuickStart : null);
            }

            if (scrollRect != null && contentRoot != null)
            {
                if (_lastHandCount != needed)
                {
                    _savedScrollX = -1f;
                    _lastHandCount = needed;
                }

                var scrollBeforeLayout = _savedScrollX >= 0f
                    ? _savedScrollX
                    : scrollRect.horizontalNormalizedPosition;

                ReapplyPoolLayout();

                scrollRect.horizontalNormalizedPosition = scrollBeforeLayout;
                _savedScrollX = scrollBeforeLayout;
            }
        }

        void LateUpdate()
        {
            if (scrollRect == null || !scrollRect.gameObject.activeInHierarchy)
                return;

            _savedScrollX = scrollRect.horizontalNormalizedPosition;
        }

        static IReadOnlyList<CardInstanceState> ResolveHandCards(BattleState state, BattleSession session)
        {
            if (session?.PresentationLocked == true && session.PresentationSnapshot != null)
                return session.PresentationSnapshot.GetDisplayedPlayerHand(state);

            return state.PlayerHand;
        }

        void EnsurePool(int count)
        {
            while (_pool.Count < count)
            {
                var view = Instantiate(cardPrefab, contentRoot);
                ApplyCardLayout(view);
                _pool.Add(view);
            }
        }

        static void ApplyCardLayout(CardView view)
        {
            CardView.ApplyHandPresentationScale(view, BattleUiLayoutRuntimeFix.HandCardScale);
        }

        public void ReapplyPoolLayout()
        {
            foreach (var view in _pool)
                ApplyCardLayout(view);

            if (contentRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }
    }
}
