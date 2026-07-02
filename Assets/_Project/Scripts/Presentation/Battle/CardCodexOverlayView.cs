using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>测试图鉴：左上角入口，分类展示全部玩家/敌人卡牌。</summary>
    [DisallowMultipleComponent]
    public sealed class CardCodexOverlayView : MonoBehaviour
    {
        const float PanelWidth = 1180f;
        const float PanelHeight = 820f;
        const float CardScale = 0.72f;
        const int CardsPerRow = 5;
        const float CardGridHorizontalPadding = 20f;

        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        Action<CardDefinitionSO> _onAddToHand;

        RectTransform _panel;
        RectTransform _content;
        ScrollRect _scroll;
        InventoryTooltipView _tooltip;
        Text _titleText;
        readonly List<GameObject> _dynamicObjects = new();
        bool _built;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(
            Transform root,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action<CardDefinitionSO> onAddToHand = null)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onAddToHand = onAddToHand;
            EnsureBuilt(root);
        }

        public void RefreshCardPrefab(CardView cardPrefab)
        {
            if (cardPrefab != null)
                _cardPrefab = cardPrefab;
        }

        public void Toggle()
        {
            if (_panel == null)
                return;

            if (_panel.gameObject.activeSelf)
                Hide();
            else
                Show();
        }

        public void Show()
        {
            EnsureBuilt(transform.parent);
            _panel.gameObject.SetActive(true);
            Rebuild();
            CombatantTooltipLayer.MountToFront(_panel, transform.parent);
        }

        public void Hide()
        {
            _tooltip?.Hide();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        void EnsureBuilt(Transform root)
        {
            if (_built)
                return;

            _built = true;
            var canvas = root.GetComponentInParent<Canvas>();
            var parent = canvas != null ? canvas.transform : root;

            var panelGo = new GameObject("CardCodexPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            _panel = panelGo.GetComponent<RectTransform>();
            panelGo.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.1f, 0.96f);

            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var header = CreateHeaderRow(panelGo.transform);
            CreateCloseButton(header);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panelGo.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(16f, 16f);
            scrollRt.offsetMax = new Vector2(-16f, -56f);
            scrollGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.55f);

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
            _scroll.onValueChanged.AddListener(_ => _tooltip?.Hide());

            var viewport = CreateViewport(scrollGo.transform);
            _content = CreateVerticalContent(viewport);
            _scroll.viewport = viewport;
            _scroll.content = _content;

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_panel);
            _panel.gameObject.SetActive(false);
        }

        void Rebuild()
        {
            ClearDynamicObjects();
            _tooltip?.Hide();

            if (_cardPrefab == null)
            {
                AddWarningRow("卡牌预制体未就绪，无法展示图鉴。");
                ForceLayoutRefresh();
                return;
            }

            var groups = CardCodexCatalog.BuildGroupedCatalog();
            var totalCards = 0;
            foreach (var group in groups)
                totalCards += group.Cards.Count;

            _titleText.text = _onAddToHand != null
                ? $"卡牌图鉴（测试）— 共 {totalCards} 张　点击卡牌直接置入手牌"
                : $"卡牌图鉴（测试）— 共 {totalCards} 张";

            foreach (var group in groups)
            {
                AddCategoryHeader($"{group.Label}（{group.Cards.Count}）");
                AddCategoryGrid(group.Cards);
            }

            ForceLayoutRefresh();
        }

        void ForceLayoutRefresh()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
        }

        void ClearDynamicObjects()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
        }

        void AddCategoryHeader(string label)
        {
            var go = new GameObject("CategoryHeader", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(_content, false);
            _dynamicObjects.Add(go);

            go.GetComponent<LayoutElement>().preferredHeight = 32f;
            var text = go.GetComponent<Text>();
            StyleText(text, 20, TextAnchor.MiddleLeft);
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.85f, 0.78f, 0.55f, 1f);
            text.text = label;
        }

        void AddWarningRow(string message)
        {
            var go = new GameObject("Warning", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(_content, false);
            _dynamicObjects.Add(go);
            go.GetComponent<LayoutElement>().preferredHeight = 48f;
            var text = go.GetComponent<Text>();
            StyleText(text, 18, TextAnchor.MiddleLeft);
            text.color = new Color(1f, 0.75f, 0.55f, 1f);
            text.text = message;
        }

        void AddCategoryGrid(IReadOnlyList<CardDefinitionSO> cards)
        {
            if (cards == null || cards.Count == 0)
                return;

            var cardWidth = 168f * CardScale;
            var cardHeight = 236f * CardScale;
            var grid = CreateCardGrid(_content, cardWidth, cardHeight);

            foreach (var def in cards)
            {
                if (def == null)
                    continue;

                _definitions[def.CardId] = def;

                var holder = new GameObject($"CodexCard_{def.CardId}", typeof(RectTransform), typeof(LayoutElement));
                holder.transform.SetParent(grid, false);
                var holderLe = holder.GetComponent<LayoutElement>();
                holderLe.preferredWidth = cardWidth + 8f;
                holderLe.preferredHeight = cardHeight + 8f;
                _dynamicObjects.Add(holder);

                var preview = CardVisualResolver.CreatePreviewInstance(
                    def.CardId,
                    def.OwnerCharacterId,
                    def.DisplayName,
                    def);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);

                var view = UnityEngine.Object.Instantiate(_cardPrefab, holder.transform);
                CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                view.BindWithCard(
                    preview,
                    visual,
                    selected: false,
                    polluted: false,
                    interactable: _onAddToHand != null,
                    orderBadge: "",
                    statsLine: BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions),
                    uiIcons: _uiIcons,
                    characterVisuals: _characterVisuals,
                    onClick: _onAddToHand != null ? _ => OnCodexCardClicked(def) : null,
                    onHoverEnter: null,
                    onHoverExit: null);

                var canvasGroup = view.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;

                BindCardTooltip(view.gameObject, preview);
            }
        }

        RectTransform CreateCardGrid(Transform parent, float cellWidth, float cellHeight)
        {
            var go = new GameObject("CardGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            _dynamicObjects.Add(go);

            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellWidth + 8f, cellHeight + 8f);
            grid.spacing = new Vector2(10f, 12f);
            grid.padding = new RectOffset(
                (int)CardGridHorizontalPadding,
                (int)CardGridHorizontalPadding,
                4,
                8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = CardsPerRow;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            return go.GetComponent<RectTransform>();
        }

        RectTransform CreateHeaderRow(Transform parent)
        {
            var go = new GameObject("Header", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, 52f);
            go.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.95f);

            var textGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(16f, 0f);
            textRt.offsetMax = new Vector2(-100f, 0f);

            _titleText = textGo.GetComponent<Text>();
            StyleText(_titleText, 24, TextAnchor.MiddleCenter);
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.color = new Color(0.95f, 0.92f, 0.82f, 1f);
            _titleText.text = "卡牌图鉴（测试）";

            return rt;
        }

        void CreateCloseButton(RectTransform header)
        {
            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(header, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.sizeDelta = new Vector2(88f, 40f);
            closeRt.anchoredPosition = new Vector2(-8f, 0f);
            closeGo.GetComponent<Image>().color = new Color(0.55f, 0.18f, 0.18f, 0.95f);

            var btn = closeGo.GetComponent<Button>();
            btn.onClick.AddListener(Hide);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(closeGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            StyleText(label, 18, TextAnchor.MiddleCenter);
            label.text = "关闭";
        }

        static RectTransform CreateViewport(Transform parent)
        {
            var go = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = Color.clear;
            return rt;
        }

        static RectTransform CreateVerticalContent(Transform parent)
        {
            var go = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(8, 8, 8, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return rt;
        }

        void OnCodexCardClicked(CardDefinitionSO def)
        {
            if (def == null)
                return;
            _onAddToHand?.Invoke(def);
            _tooltip?.Hide();
        }

        void BindCardTooltip(GameObject target, CardInstanceState card)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(null, descCard, _definitions);
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            var costLabel = (card.Keywords != null && card.Keywords.Contains("x_cost")) ? "X" : card.Cost.ToString();
            var header = $"{card.DisplayName}  [{card.DefinitionId}]  费用 {costLabel}";
            _tooltip.BindHover(target, header, body.Replace("<b>", "").Replace("</b>", ""), showTitle: true);
        }

        static void StyleText(Text text, int size, TextAnchor anchor)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }
    }
}
