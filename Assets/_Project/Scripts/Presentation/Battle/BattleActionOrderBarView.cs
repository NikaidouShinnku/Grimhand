using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>顶部行动顺序条：卡牌居中于条内，卡名浮在卡牌上方（可超出条外）。</summary>
    [DisallowMultipleComponent]
    public sealed class BattleActionOrderBarView : MonoBehaviour
    {
        const float EntrySpacing = 10f;
        const int NameFontSize = 15;
        const float NameLabelHeight = 34f;
        const float NameGapAboveCard = 3f;

        CardView _cardPrefab;
        CardVisualCatalogSO _catalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _panel;
        RectTransform _content;
        ScrollRect _scroll;
        readonly List<EntrySlot> _pool = new();
        bool _built;

        sealed class EntrySlot
        {
            public GameObject Root;
            public CardView Card;
            public Text NameLabel;
        }

        public void Initialize(
            Transform chromeRoot,
            CardView cardPrefab,
            CardVisualCatalogSO catalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _cardPrefab = cardPrefab;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(chromeRoot);
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
            bg.color = new Color(0.08f, 0.1f, 0.16f, 0.82f);
            bg.raycastTarget = false;

            BattleUiLayoutRuntimeFix.LayoutActionOrderBar(_panel);
            _panel.SetAsLastSibling();

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(_panel, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(scrollRt, 8f, 6f, -8f, -6f);
            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = true;
            _scroll.vertical = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt, 0f, 0f, 0f, 0f);
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

            while (_pool.Count < count)
            {
                var entryGo = new GameObject($"Entry_{_pool.Count}", typeof(RectTransform));
                entryGo.transform.SetParent(_content, false);

                var entryLe = entryGo.AddComponent<LayoutElement>();
                entryLe.preferredWidth = cardWidth + 12f;
                entryLe.minWidth = cardWidth + 12f;
                entryLe.preferredHeight = cardHeight;
                entryLe.minHeight = cardHeight;

                var card = Instantiate(_cardPrefab, entryGo.transform);
                CardView.ApplyHandPresentationScale(card, BattleUiLayoutRuntimeFix.ActionOrderBarMiniCardScale);
                CenterCardInEntry(card.transform as RectTransform);

                var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
                nameGo.transform.SetParent(entryGo.transform, false);
                var nameText = ConfigureNameLabel(nameGo.GetComponent<Text>());
                LayoutNameAboveCard(nameText, cardWidth, cardHeight);

                _pool.Add(new EntrySlot
                {
                    Root = entryGo,
                    Card = card,
                    NameLabel = nameText
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
                slot.NameLabel.text = "";
                return;
            }

            slot.Card.gameObject.SetActive(true);
            CenterCardInEntry(slot.Card.transform as RectTransform);

            var visual = CardVisualResolver.Resolve(card, _catalog, _characterVisuals, _definitions);
            var displayName = !string.IsNullOrEmpty(entry.DisplayName)
                ? entry.DisplayName
                : (entry.IsHidden ? "?" : card.DisplayName);

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
                onHoverEnter: null,
                onHoverExit: null);
            slot.Card.SetOrderBarPresentation(compact: true, hiddenIntent: entry.IsHidden);

            ApplyNameLabelStyle(slot.NameLabel);
            slot.NameLabel.text = displayName;
            LayoutNameAboveCard(slot.NameLabel, cardWidth, cardHeight);
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

        static void LayoutNameAboveCard(Text nameLabel, float cardWidth, float cardHeight)
        {
            if (nameLabel == null)
                return;

            var rt = nameLabel.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, cardHeight * 0.5f + NameGapAboveCard);
            rt.sizeDelta = new Vector2(cardWidth + 12f, NameLabelHeight);
        }

        static Text ConfigureNameLabel(Text nameText)
        {
            ApplyNameLabelStyle(nameText);
            return nameText;
        }

        static void ApplyNameLabelStyle(Text nameText)
        {
            if (nameText == null)
                return;

            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = NameFontSize;
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.LowerCenter;
            nameText.color = Color.white;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            nameText.lineSpacing = 1f;
            nameText.raycastTarget = false;

            var outline = nameText.GetComponent<Outline>() ?? nameText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.82f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
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
