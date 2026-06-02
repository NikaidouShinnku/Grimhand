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

        public void Refresh(
            BattleState state,
            BattleSession session,
            CardVisualCatalogSO catalog,
            BattleUiIconCatalogSO uiIcons,
            CharacterVisualCatalogSO characterVisuals,
            IReadOnlyDictionary<string, CardDefinitionSO> definitions,
            Action<int> onCardClick,
            Action<CardInstanceState, RectTransform> onHoverEnter,
            Action onHoverExit)
        {
            if (state == null || session?.Engine == null)
                return;

            if (handCountLabel != null)
                handCountLabel.text = $"手牌 {state.PlayerHand.Count}/{state.Config.HandLimit}";

            var needed = state.PlayerHand.Count;
            EnsurePool(needed);

            for (var i = 0; i < _pool.Count; i++)
            {
                var view = _pool[i];
                if (i >= needed)
                {
                    view.gameObject.SetActive(false);
                    continue;
                }

                view.gameObject.SetActive(true);
                var card = state.PlayerHand[i];
                var selected = session.Engine.Draft.IsSelected(card.InstanceId);
                var polluted = CardRules.IsPolluted(card);
                var canAfford = session.Engine.Draft.EnergyRemaining >= card.Cost;
                var interactable = session.CanInteractWithBattle() && !polluted && (selected || canAfford);
                var visual = CardVisualResolver.Resolve(card, catalog, characterVisuals, definitions);
                var stats = BattleUiFormatters.BuildCardStatsLine(state, session.Engine.Draft, card);
                var badge = BattleUiFormatters.BuildSelectionBadge(session.Engine.Draft, card);

                view.BindWithCard(card, visual, selected, polluted, interactable, badge, stats,
                    uiIcons, characterVisuals, onCardClick, onHoverEnter, onHoverExit);
            }

            if (scrollRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        }

        void EnsurePool(int count)
        {
            while (_pool.Count < count)
            {
                var view = Instantiate(cardPrefab, contentRoot);
                _pool.Add(view);
            }
        }
    }
}
