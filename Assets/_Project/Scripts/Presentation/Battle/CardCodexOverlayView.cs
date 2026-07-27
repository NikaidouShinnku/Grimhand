using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>测试图鉴：左上角入口，分类展示全部玩家/敌人卡牌与遗物。</summary>
    [DisallowMultipleComponent]
    public sealed class CardCodexOverlayView : MonoBehaviour
    {
        const float PanelWidth = 1180f;
        const float PanelHeight = 820f;
        const float CardScale = 0.72f;
        const int CardsPerRow = 5;
        const float CardGridHorizontalPadding = 20f;
        const int RelicColumns = 5;
        const float RelicPlateW = 168f;
        const float RelicPlateH = 182f;

        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        Action<CardDefinitionSO> _onAddToHand;
        Action<RelicDefinition> _onGrantRelic;
        string _titleHint;
        bool _closeOnSelect;
        bool _showRelics = true;

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
            Action<CardDefinitionSO> onAddToHand = null,
            string titleHint = null,
            bool closeOnSelect = true,
            RelicVisualCatalogSO relicCatalog = null,
            Action<RelicDefinition> onGrantRelic = null)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _relicCatalog = relicCatalog;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onGrantRelic = onGrantRelic;
            ConfigureSelection(onAddToHand, titleHint, closeOnSelect);
            EnsureBuilt(root);
        }

        /// <summary>切换点选行为（图鉴加手牌 / 假人出牌排队），不重建面板。</summary>
        public void ConfigureSelection(
            Action<CardDefinitionSO> onSelect,
            string titleHint = null,
            bool closeOnSelect = true,
            bool showRelics = true)
        {
            _onAddToHand = onSelect;
            _titleHint = titleHint;
            _closeOnSelect = closeOnSelect;
            _showRelics = showRelics;
        }

        public void SetRelicGrantHandler(Action<RelicDefinition> onGrantRelic) =>
            _onGrantRelic = onGrantRelic;

        public void SetRelicCatalog(RelicVisualCatalogSO relicCatalog) =>
            _relicCatalog = relicCatalog;

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
            if (_scroll != null)
                _scroll.verticalNormalizedPosition = 1f;
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
            _tooltip.Initialize(_panel, _uiIcons);
            _panel.gameObject.SetActive(false);
        }

        void Rebuild()
        {
            ClearDynamicObjects();
            _tooltip?.Hide();

            var groups = CardCodexCatalog.BuildGroupedCatalog();
            var totalCards = 0;
            foreach (var group in groups)
                totalCards += group.Cards.Count;

            var relicCount = 0;
            if (_showRelics)
            {
                foreach (var relic in RelicDatabase.All)
                {
                    if (relic != null && !string.IsNullOrEmpty(relic.Id))
                        relicCount++;
                }
            }

            if (!string.IsNullOrEmpty(_titleHint))
            {
                _titleText.text = $"{_titleHint}共 {totalCards} 张";
            }
            else if (_onAddToHand != null || _onGrantRelic != null)
            {
                _titleText.text = relicCount > 0
                    ? $"测试图鉴 — 卡牌 {totalCards} / 遗物 {relicCount}　点击卡牌加手牌，点击遗物立即获取"
                    : $"卡牌图鉴（测试）— 共 {totalCards} 张　点击卡牌直接置入手牌";
            }
            else
            {
                _titleText.text = $"卡牌图鉴（测试）— 共 {totalCards} 张";
            }

            if (_cardPrefab == null)
                AddWarningRow("卡牌预制体未就绪，无法展示卡牌。");
            else
            {
                foreach (var group in groups)
                {
                    AddCategoryHeader($"{group.Label}（{group.Cards.Count}）");
                    AddCategoryGrid(group.Cards);
                }
            }

            if (_showRelics)
                AddRelicsSection(relicCount);

            ForceLayoutRefresh();
        }

        void AddRelicsSection(int relicCount)
        {
            if (relicCount <= 0)
            {
                AddCategoryHeader("遗物（0）");
                AddWarningRow("暂无遗物数据。");
                return;
            }

            var relics = new List<RelicDefinition>(RelicDatabase.All);
            relics.Sort((a, b) =>
            {
                var rarity = a.Rarity.CompareTo(b.Rarity);
                if (rarity != 0)
                    return rarity;
                return string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });

            AddCategoryHeader($"遗物（{relics.Count}）— 点击立即获取");
            var grid = CreateRelicGrid(_content);

            foreach (var relic in relics)
            {
                if (relic == null || string.IsNullOrEmpty(relic.Id))
                    continue;

                CreateRelicCell(grid, relic);
            }
        }

        RectTransform CreateRelicGrid(Transform parent)
        {
            var go = new GameObject("RelicGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            _dynamicObjects.Add(go);

            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var grid = go.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(RelicPlateW, RelicPlateH);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(
                (int)CardGridHorizontalPadding,
                (int)CardGridHorizontalPadding,
                4,
                8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = RelicColumns;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;

            return go.GetComponent<RectTransform>();
        }

        void CreateRelicCell(Transform parent, RelicDefinition relic)
        {
            var go = new GameObject($"Relic_{relic.Id}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(Button));
            go.transform.SetParent(parent, false);
            _dynamicObjects.Add(go);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = RelicPlateW;
            le.preferredHeight = RelicPlateH;

            var bg = go.GetComponent<Image>();
            bg.color = Color.white;
            bg.raycastTarget = true;
            bg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiEventPlate != null)
                bg.sprite = _uiIcons.UiEventPlate;
            else
                bg.color = new Color(0.08f, 0.09f, 0.12f, 0.85f);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.18f, 0.32f);
            iconRt.anchorMax = new Vector2(0.82f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _relicCatalog?.GetIcon(relic.Id);
            icon.color = icon.sprite != null
                ? Color.white
                : new Color(0.55f, 0.48f, 0.35f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.08f, 0.06f);
            labelRt.anchorMax = new Vector2(0.92f, 0.28f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            StyleText(label, 14, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            label.text = relic.DisplayName;
            label.raycastTarget = false;

            var rarity = relic.Rarity switch
            {
                RelicRarity.Epic => "史诗",
                RelicRarity.Rare => "稀有",
                _ => "普通"
            };
            var body = string.IsNullOrWhiteSpace(relic.Description)
                ? rarity
                : $"{rarity}\n{relic.Description}";
            _tooltip?.BindHover(go, relic.DisplayName, body, showTitle: true);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            if (_onGrantRelic != null)
            {
                var captured = relic;
                btn.onClick.AddListener(() => OnCodexRelicClicked(captured));
            }
            else
            {
                btn.interactable = false;
            }
        }

        void ForceLayoutRefresh(bool resetScroll = false)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            Canvas.ForceUpdateCanvases();
            if (_scroll == null)
                return;

            if (resetScroll)
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
            if (_closeOnSelect)
                Hide();
        }

        void OnCodexRelicClicked(RelicDefinition relic)
        {
            if (relic == null)
                return;

            _onGrantRelic?.Invoke(relic);
            _tooltip?.Hide();
            // 测试用：连续点取多个遗物时保持图鉴打开
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
