using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>出牌演出时在原手牌区域中央展示当前生效的卡牌。</summary>
    public sealed class BattleActiveCardBanner : MonoBehaviour
    {
        const float BannerScaleMul = 1.08f;

        CardView _cardView;
        RectTransform _bannerRoot;
        Transform _battleScreenRoot;

        BattleSession _session;
        CardVisualCatalogSO _catalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        public void Initialize(
            BattleSession session,
            CardView cardPrefab,
            CardVisualCatalogSO catalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Transform battleScreenRoot)
        {
            _session = session;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _battleScreenRoot = battleScreenRoot;

            if (cardPrefab == null)
            {
                Debug.LogWarning("[Grimhand] ActiveCardBanner: CardView 预制体为空。");
                return;
            }

            EnsureBuilt(cardPrefab);
        }

        public void Relayout()
        {
            if (_bannerRoot == null)
                return;

            var handArea = FindHandArea();
            if (handArea == null)
                return;

            if (_bannerRoot.parent != handArea)
                _bannerRoot.SetParent(handArea, false);

            const float labelHeight = 20f;
            _bannerRoot.anchorMin = new Vector2(0.5f, 0f);
            _bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            _bannerRoot.pivot = new Vector2(0.5f, 0.5f);
            _bannerRoot.anchoredPosition = new Vector2(0f, -labelHeight * 0.5f);
            _bannerRoot.sizeDelta = Vector2.zero;
            _bannerRoot.offsetMin = new Vector2(-BattleUiLayoutRuntimeFix.ScaledCardWidth * 0.55f, 0f);
            _bannerRoot.offsetMax = new Vector2(BattleUiLayoutRuntimeFix.ScaledCardWidth * 0.55f, -labelHeight);

            if (_cardView != null)
                CardView.ApplyHandPresentationScale(_cardView, BattleUiLayoutRuntimeFix.HandCardScale * BannerScaleMul);
        }

        public void Show(int cardInstanceId)
        {
            if (_cardView == null || _bannerRoot == null)
                return;

            Relayout();

            var state = _session?.Engine?.State;
            var card = state?.GetCard(cardInstanceId);
            if (card == null)
            {
                Hide();
                return;
            }

            var visual = CardVisualResolver.Resolve(card, _catalog, _characterVisuals, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLine(state, null, card);

            _cardView.BindWithCard(
                card,
                visual,
                selected: false,
                polluted: CardRules.IsPolluted(card),
                interactable: false,
                orderBadge: "",
                statsLine: stats,
                uiIcons: _uiIcons,
                characterVisuals: _characterVisuals,
                onClick: null,
                onHoverEnter: null,
                onHoverExit: null);

            _bannerRoot.gameObject.SetActive(true);
            _bannerRoot.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_bannerRoot != null)
                _bannerRoot.gameObject.SetActive(false);
        }

        void EnsureBuilt(CardView cardPrefab)
        {
            if (_bannerRoot != null)
                return;

            var handArea = FindHandArea();
            if (handArea == null)
            {
                Debug.LogWarning("[Grimhand] ActiveCardBanner: 找不到 HandArea。");
                return;
            }

            var rootGo = new GameObject("ActiveCardBanner", typeof(RectTransform));
            rootGo.transform.SetParent(handArea, false);
            _bannerRoot = rootGo.GetComponent<RectTransform>();

            _cardView = Instantiate(cardPrefab, _bannerRoot);
            var cardRt = _cardView.transform as RectTransform;
            if (cardRt != null)
            {
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
            }

            Relayout();
            _bannerRoot.gameObject.SetActive(false);
        }

        Transform FindHandArea()
        {
            if (_battleScreenRoot == null)
                return null;

            return _battleScreenRoot.Find("HudChromeRoot/HandArea")
                ?? _battleScreenRoot.Find("HandArea");
        }
    }
}
