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
            {
                var handSource = ResolveHandCards(state, session);
                var turnHint = state.Phase == TurnPhase.Planning && !presenting
                    ? $" · 回合 {state.TurnNumber}"
                    : presenting ? " · 出牌中" : "";
                handCountLabel.text = $"{handSource.Count}/{state.Config.HandLimit}{turnHint}";
            }

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
                var draft = session.Engine.Draft;
                var awaiting = draft.AwaitingTargetCardId;
                var isAwaitingTarget = awaiting == card.InstanceId;
                var isQueued = draft.IsSelected(card.InstanceId);
                var showSelected = isQueued;
                var polluted = CardRules.IsPolluted(card);
                var canAfford = draft.EnergyRemaining >= card.Cost;
                var interactable = session.CanInteractWithBattle() && !polluted
                    && (isAwaitingTarget || isQueued || canAfford);
                var visual = CardVisualResolver.Resolve(card, catalog, characterVisuals, definitions);
                var stats = BattleUiFormatters.BuildCardStatsLine(state, draft, card, damagePreviewTarget: damagePreviewTarget);
                var badge = isQueued
                    ? BattleUiFormatters.BuildSelectionBadge(state, draft, card, resolveSteps)
                    : null;

                view.BindWithCard(card, visual, showSelected, polluted, interactable, badge, stats,
                    uiIcons, characterVisuals, onCardClick, onHoverEnter, onHoverExit);
            }

            if (scrollRect != null && contentRoot != null)
            {
                scrollRect.horizontalNormalizedPosition = 0f;
                ReapplyPoolLayout();
            }
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
