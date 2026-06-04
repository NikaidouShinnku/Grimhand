using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>速度结算时在屏幕中央上方展示当前生效的卡牌。</summary>
    public sealed class BattleActiveCardBanner : MonoBehaviour
    {
        const float BannerScale = 1.15f;

        CardView _cardView;
        RectTransform _bannerRoot;

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
            Transform parentCanvasRoot)
        {
            _session = session;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();

            if (cardPrefab == null)
            {
                Debug.LogWarning("[Grimhand] ActiveCardBanner: CardView 预制体为空。");
                return;
            }

            EnsureBuilt(cardPrefab, parentCanvasRoot);
        }

        public void Show(int cardInstanceId)
        {
            if (_cardView == null || _bannerRoot == null)
                return;

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
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_bannerRoot != null)
                _bannerRoot.gameObject.SetActive(false);
        }

        void EnsureBuilt(CardView cardPrefab, Transform parent)
        {
            if (_bannerRoot != null)
                return;

            var rootGo = new GameObject("ActiveCardBanner", typeof(RectTransform));
            rootGo.transform.SetParent(parent, false);
            _bannerRoot = rootGo.GetComponent<RectTransform>();
            _bannerRoot.anchorMin = new Vector2(0.5f, 1f);
            _bannerRoot.anchorMax = new Vector2(0.5f, 1f);
            _bannerRoot.pivot = new Vector2(0.5f, 1f);
            _bannerRoot.anchoredPosition = new Vector2(0f, -24f);
            _bannerRoot.sizeDelta = new Vector2(220f, 300f);

            var canvas = rootGo.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 180;
            rootGo.AddComponent<GraphicRaycaster>().enabled = false;

            _cardView = Instantiate(cardPrefab, _bannerRoot);
            var cardRt = _cardView.transform as RectTransform;
            if (cardRt != null)
            {
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                cardRt.localScale = Vector3.one * BannerScale;
            }

            _bannerRoot.gameObject.SetActive(false);
        }
    }
}
