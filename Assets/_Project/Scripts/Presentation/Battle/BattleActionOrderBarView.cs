using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>顶部行动顺序条：卡名在上，出卡者→目标在下；无灰色底板以免挡住加速等按钮。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleActionOrderBarView : MonoBehaviour
    {
        const float EntrySpacing = 14f;
        const int TitleFontSize = 16;
        const int RouteFontSize = 14;
        const float TitleLabelHeight = 28f;
        const float RouteLabelHeight = 26f;
        const float NameGapAboveCard = 2f;
        const float RouteGapBelowCard = 2f;

        CardView _cardPrefab;
        CardVisualCatalogSO _catalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        Action<CardInstanceState, RectTransform> _onHoverEnter;
        Action _onHoverExit;

        RectTransform _panel;
        RectTransform _content;
        ScrollRect _scroll;
        readonly List<EntrySlot> _pool = new();
        bool _built;

        sealed class EntrySlot
        {
            public GameObject Root;
            public CardView Card;
            public Text TitleLabel;
            public Text RouteLabel;
        }

        public void Initialize(
            Transform chromeRoot,
            CardView cardPrefab,
            CardVisualCatalogSO catalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action<CardInstanceState, RectTransform> onHoverEnter = null,
            Action onHoverExit = null)
        {
            _cardPrefab = cardPrefab;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;
            EnsureBuilt(chromeRoot);
        }

        public void SetHoverHandlers(
            Action<CardInstanceState, RectTransform> onHoverEnter,
            Action onHoverExit)
        {
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;
        }

        public void SetVisible(bool visible)
        {
            if (_panel != null)
                _panel.gameObject.SetActive(visible);
        }

        public void RefreshEntries(IReadOnlyList<ActionOrderVisualEntry> entries)
        {
            if (!_built || _cardPrefab == null)
                return;

            var count = entries?.Count ?? 0;
            EnsurePool(count);

            for (var i = 0; i < _pool.Count; i++)
            {
                var slot = _pool[i];
                if (i >= count)
                {
                    slot.Root.SetActive(false);
                    continue;
                }

                slot.Root.SetActive(true);
                BindEntry(slot, entries[i]);
            }

            if (_content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

            if (_scroll != null && count > 0)
            {
                _scroll.horizontalNormalizedPosition = 0f;
                CenterScrollContentIfNeeded();
            }
        }

        void CenterScrollContentIfNeeded()
        {
            if (_scroll == null || _content == null || _scroll.viewport == null)
                return;

            var viewportWidth = _scroll.viewport.rect.width;
            var contentWidth = LayoutUtility.GetPreferredWidth(_content);
            if (contentWidth <= viewportWidth)
            {
                _content.anchorMin = new Vector2(0.5f, 0.5f);
                _content.anchorMax = new Vector2(0.5f, 0.5f);
                _content.pivot = new Vector2(0.5f, 0.5f);
                _content.anchoredPosition = Vector2.zero;
                _scroll.horizontal = false;
            }
            else
            {
                _content.anchorMin = new Vector2(0f, 0.5f);
                _content.anchorMax = new Vector2(0f, 0.5f);
                _content.pivot = new Vector2(0f, 0.5f);
                _content.anchoredPosition = Vector2.zero;
                _scroll.horizontal = true;
                _scroll.horizontalNormalizedPosition = 0f;
            }
        }

        void EnsureBuilt(Transform chromeRoot)
        {
            if (_built || chromeRoot == null || _cardPrefab == null)
                return;

            var stale = chromeRoot.Find("ActionOrderBar");
            if (stale != null)
                Destroy(stale.gameObject);

            _pool.Clear();
            _built = true;

            var panelGo = new GameObject("ActionOrderBar", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(chromeRoot, false);
            _panel = panelGo.GetComponent<RectTransform>();
            var bg = panelGo.GetComponent<Image>();
            bg.color = Color.clear;
            bg.raycastTarget = false;

            BattleUiLayoutRuntimeFix.LayoutActionOrderBar(_panel);
            // 不要压到设置/加速按钮之上
            _panel.SetSiblingIndex(0);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(CanvasGroup));
            scrollGo.transform.SetParent(_panel, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(scrollRt, 4f, 4f, -4f, -4f);
            var scrollGroup = scrollGo.GetComponent<CanvasGroup>();
            // 空白区域不拦截点击；卡牌自身仍可悬停
            scrollGroup.blocksRaycasts = true;
            scrollGroup.interactable = true;
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt, 0f, 0f, 0f, 0f);
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = Color.clear;
            viewportImg.raycastTarget = false;
            _scroll.viewport = viewportRt;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0.5f, 0.5f);
            _content.anchorMax = new Vector2(0.5f, 0.5f);
            _content.pivot = new Vector2(0.5f, 0.5f);
            _content.anchoredPosition = Vector2.zero;

            var layout = contentGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = EntrySpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(4, 4, 0, 0);

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            _scroll.content = _content;
        }

        void EnsurePool(int count)
        {
            var cardHeight = BattleUiLayoutRuntimeFix.ScaledOrderBarCardHeight;
            var cardWidth = BattleUiLayoutRuntimeFix.ScaledOrderBarCardWidth;
            var entryHeight = cardHeight + TitleLabelHeight + RouteLabelHeight + NameGapAboveCard + RouteGapBelowCard;

            while (_pool.Count < count)
            {
                var entryGo = new GameObject($"Entry_{_pool.Count}", typeof(RectTransform));
                entryGo.transform.SetParent(_content, false);

                var entryLe = entryGo.AddComponent<LayoutElement>();
                entryLe.preferredWidth = cardWidth + 16f;
                entryLe.minWidth = cardWidth + 16f;
                entryLe.preferredHeight = entryHeight;
                entryLe.minHeight = entryHeight;

                var card = Instantiate(_cardPrefab, entryGo.transform);
                CardView.ApplyHandPresentationScale(card, BattleUiLayoutRuntimeFix.ActionOrderBarMiniCardScale);
                CenterCardInEntry(card.transform as RectTransform);

                var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
                titleGo.transform.SetParent(entryGo.transform, false);
                var titleText = ConfigureLabel(titleGo.GetComponent<Text>(), TitleFontSize, TextAnchor.LowerCenter);
                LayoutTitleAboveCard(titleText, cardWidth, cardHeight);

                var routeGo = new GameObject("Route", typeof(RectTransform), typeof(Text));
                routeGo.transform.SetParent(entryGo.transform, false);
                var routeText = ConfigureLabel(routeGo.GetComponent<Text>(), RouteFontSize, TextAnchor.UpperCenter);
                LayoutRouteBelowCard(routeText, cardWidth, cardHeight);

                _pool.Add(new EntrySlot
                {
                    Root = entryGo,
                    Card = card,
                    TitleLabel = titleText,
                    RouteLabel = routeText
                });
            }
        }

        void BindEntry(EntrySlot slot, ActionOrderVisualEntry entry)
        {
            var cardWidth = BattleUiLayoutRuntimeFix.ScaledOrderBarCardWidth;
            var cardHeight = BattleUiLayoutRuntimeFix.ScaledOrderBarCardHeight;

            var card = entry.Card;
            if (card == null)
            {
                slot.Card.gameObject.SetActive(false);
                slot.TitleLabel.text = "";
                slot.RouteLabel.text = "";
                return;
            }

            slot.Card.gameObject.SetActive(true);
            CenterCardInEntry(slot.Card.transform as RectTransform);

            var visual = CardVisualResolver.Resolve(card, _catalog, _characterVisuals, _definitions);
            var title = !string.IsNullOrEmpty(entry.CardTitle)
                ? entry.CardTitle
                : (entry.IsHidden ? "?" : card.DisplayName);
            var route = !string.IsNullOrEmpty(entry.OwnerArrowTarget)
                ? entry.OwnerArrowTarget
                : (entry.DisplayName ?? "");

            var hoverEnter = entry.IsHidden ? null : _onHoverEnter;
            var hoverExit = entry.IsHidden ? null : _onHoverExit;

            slot.Card.BindWithCard(
                card,
                visual,
                selected: false,
                polluted: false,
                interactable: false,
                orderBadge: null,
                statsLine: "",
                _uiIcons,
                _characterVisuals,
                onClick: null,
                onHoverEnter: hoverEnter,
                onHoverExit: hoverExit);
            slot.Card.SetOrderBarPresentation(compact: true, hiddenIntent: entry.IsHidden);

            slot.TitleLabel.text = title ?? "";
            slot.RouteLabel.text = route ?? "";
            LayoutTitleAboveCard(slot.TitleLabel, cardWidth, cardHeight);
            LayoutRouteBelowCard(slot.RouteLabel, cardWidth, cardHeight);
        }

        static void CenterCardInEntry(RectTransform cardRt)
        {
            if (cardRt == null)
                return;

            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
        }

        static void LayoutTitleAboveCard(Text label, float cardWidth, float cardHeight)
        {
            if (label == null)
                return;

            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, cardHeight * 0.5f + NameGapAboveCard);
            rt.sizeDelta = new Vector2(cardWidth + 20f, TitleLabelHeight);
        }

        static void LayoutRouteBelowCard(Text label, float cardWidth, float cardHeight)
        {
            if (label == null)
                return;

            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -cardHeight * 0.5f - RouteGapBelowCard);
            rt.sizeDelta = new Vector2(cardWidth + 28f, RouteLabelHeight);
        }

        static Text ConfigureLabel(Text text, int fontSize, TextAnchor alignment)
        {
            if (text == null)
                return null;

            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1f;
            text.raycastTarget = false;

            var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            return text;
        }

        static void StretchFull(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }
    }
}
