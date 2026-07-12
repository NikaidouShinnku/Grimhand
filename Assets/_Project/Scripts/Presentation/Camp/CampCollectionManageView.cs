using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>军营收藏管理：筛选、浏览、详情与永久移除。</summary>
    [DisallowMultipleComponent]
    public sealed class CampCollectionManageView : MonoBehaviour
    {
        const int CardsPerRow = 10;
        const float GridHorizontalPadding = 16f;
        const float GridSpacing = 6f;
        const float CardAspect = 236f / 168f;
        const float DetailCardScale = 1.72f;

        struct CollectionRow
        {
            public int Index;
            public string CardId;
            public CardDefinitionSO Definition;
            public string OwnerCharacterId;
        }

        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        List<CharacterDefinitionSO> _characters = new();
        Action _onCollectionChanged;
        Action _onBack;

        CampCollectionState _collection;
        CampRosterState _roster;
        int _collectionCapacity;

        RectTransform _panel;
        RectTransform _listPanel;
        RectTransform _detailPanel;
        RectTransform _cardGrid;
        ScrollRect _cardScroll;
        GridLayoutGroup _gridLayout;
        float _gridCardScale = 0.55f;
        GameObject _confirmPanel;
        Text _confirmBodyText;
        Text _titleText;
        Text _summaryText;
        Text _statusText;
        Text _detailTitleText;
        Text _detailMetaText;
        Text _detailStatsText;
        Text _detailKeywordText;
        RectTransform _detailCardAnchor;
        Button _filterCostButton;
        Button _filterRarityButton;
        Button _filterOwnerButton;
        Button _removeButton;

        int? _filterCost;
        CardRarity? _filterRarity;
        string _filterOwnerId = "";
        int _detailEntryIndex = -1;
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

        static readonly int?[] CostFilters = { null, 0, 1, 2, 3, 4, 5 };
        static readonly CardRarity?[] RarityFilters =
        {
            null,
            CardRarity.Common,
            CardRarity.Rare,
            CardRarity.SuperRare,
            CardRarity.Epic,
            CardRarity.Legendary
        };

        public bool IsDetailOpen => _detailPanel != null && _detailPanel.gameObject.activeSelf;
        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(
            BattleSetupSO battleSetup,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action onCollectionChanged,
            Action onBack)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onCollectionChanged = onCollectionChanged;
            _onBack = onBack;
            _characters = CollectCharacters(battleSetup);
            EnsureBuilt();
        }

        public void Show(CampCollectionState collection, CampRosterState roster, int collectionCapacity)
        {
            _collection = collection;
            _roster = roster;
            _collectionCapacity = collectionCapacity;
            _filterCost = null;
            _filterRarity = null;
            _filterOwnerId = "";
            _detailEntryIndex = -1;
            EnsureBuilt();
            _panel.gameObject.SetActive(true);
            ShowListPanel();
            RebuildList();
        }

        public void Hide()
        {
            HideConfirmPanel();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        void ShowListPanel()
        {
            _detailEntryIndex = -1;
            _listPanel.gameObject.SetActive(true);
            _detailPanel.gameObject.SetActive(false);
        }

        void ShowDetailPanel(int entryIndex)
        {
            _detailEntryIndex = entryIndex;
            _listPanel.gameObject.SetActive(false);
            _detailPanel.gameObject.SetActive(true);
            RebuildDetail();
        }

        void RebuildList()
        {
            ClearDynamic();
            UpdateFilterLabels();
            if (_collection == null)
                return;

            _summaryText.text = $"共 {_collection.Count} 张 · 上限 {_collectionCapacity}";
            var rows = BuildFilteredRows();
            _statusText.text = rows.Count == 0
                ? "没有符合筛选条件的卡牌。"
                : $"显示 {rows.Count} 张 · 点击卡牌查看详情";

            RefreshGridLayout();
            foreach (var row in rows)
                BuildCardCell(row);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_cardGrid);
            Canvas.ForceUpdateCanvases();
            _cardScroll.verticalNormalizedPosition = 1f;
        }

        List<CollectionRow> BuildFilteredRows()
        {
            var rows = new List<CollectionRow>();
            if (_collection == null)
                return rows;

            for (var i = 0; i < _collection.Count; i++)
            {
                var cardId = _collection.Entries[i];
                if (string.IsNullOrEmpty(cardId))
                    continue;

                _definitions.TryGetValue(cardId, out var definition);
                var ownerId = definition != null ? definition.OwnerCharacterId : "";
                if (!PassesFilter(cardId, definition, ownerId))
                    continue;

                rows.Add(new CollectionRow
                {
                    Index = i,
                    CardId = cardId,
                    Definition = definition,
                    OwnerCharacterId = ownerId
                });
            }

            return rows;
        }

        bool PassesFilter(string cardId, CardDefinitionSO definition, string ownerId)
        {
            if (_filterCost.HasValue)
            {
                var cost = definition != null ? definition.Cost : 0;
                if (_filterCost.Value >= 5)
                {
                    if (cost < 5)
                        return false;
                }
                else if (cost != _filterCost.Value)
                {
                    return false;
                }
            }

            if (_filterRarity.HasValue
                && CardRarityTable.GetOrDefault(cardId) != _filterRarity.Value)
                return false;

            if (!string.IsNullOrEmpty(_filterOwnerId)
                && ownerId != _filterOwnerId)
                return false;

            return true;
        }

        void RefreshGridLayout()
        {
            if (_gridLayout == null || _cardScroll?.viewport == null)
                return;

            Canvas.ForceUpdateCanvases();
            var viewportWidth = _cardScroll.viewport.rect.width;
            if (viewportWidth <= 1f)
                viewportWidth = 1600f;

            var innerWidth = viewportWidth - GridHorizontalPadding * 2f;
            var cellWidth = (innerWidth - GridSpacing * (CardsPerRow - 1)) / CardsPerRow;
            var cellHeight = cellWidth * CardAspect;

            _gridLayout.padding = new RectOffset(
                Mathf.RoundToInt(GridHorizontalPadding),
                Mathf.RoundToInt(GridHorizontalPadding),
                10,
                10);
            _gridLayout.spacing = new Vector2(GridSpacing, GridSpacing);
            _gridLayout.cellSize = new Vector2(cellWidth, cellHeight);
            _gridLayout.constraintCount = CardsPerRow;
            _gridLayout.childAlignment = TextAnchor.UpperCenter;
            _gridCardScale = Mathf.Clamp(cellWidth / 168f * 0.9f, 0.38f, 0.82f);
        }

        void BuildCardCell(CollectionRow row)
        {
            if (_cardPrefab == null)
                return;

            var holder = CampUiRuntime.CreateRect($"Card_{row.Index}", _cardGrid);
            var layoutElement = holder.AddComponent<LayoutElement>();
            layoutElement.minWidth = _gridLayout.cellSize.x;
            layoutElement.minHeight = _gridLayout.cellSize.y;
            layoutElement.preferredWidth = _gridLayout.cellSize.x;
            layoutElement.preferredHeight = _gridLayout.cellSize.y;

            var view = Instantiate(_cardPrefab, holder.transform);
            CardView.ApplyHandPresentationScaleCentered(view, _gridCardScale);
            var preview = CardVisualResolver.CreatePreviewInstance(
                row.CardId,
                row.OwnerCharacterId,
                row.Definition?.DisplayName ?? row.CardId,
                row.Definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
            var entryIndex = row.Index;
            view.BindWithCard(
                preview,
                visual,
                false,
                false,
                true,
                "",
                statsLine,
                _uiIcons,
                _characterVisuals,
                _ => ShowDetailPanel(entryIndex),
                null,
                null);

            _dynamicObjects.Add(holder);
        }

        void RebuildDetail()
        {
            ClearDetailCard();
            if (_collection == null || _detailEntryIndex < 0 || _detailEntryIndex >= _collection.Count)
                return;

            var cardId = _collection.Entries[_detailEntryIndex];
            _definitions.TryGetValue(cardId, out var definition);
            var ownerId = definition?.OwnerCharacterId ?? "";
            var preview = CardVisualResolver.CreatePreviewInstance(
                cardId,
                ownerId,
                definition?.DisplayName ?? cardId,
                definition);
            var descCard = CardVisualResolver.ResolveForDescription(preview, _definitions);

            _detailTitleText.text = descCard.DisplayName;
            _detailMetaText.text =
                $"{CampCardUiLabels.FormatRarity(cardId)} · 费用 {descCard.Cost} · {CampCardUiLabels.FormatType(descCard.CardType)} · 归属 {CampCardUiLabels.FormatOwner(ownerId, _characters)}";

            _detailStatsText.text = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(null, descCard, _definitions)
                .Replace("<b>", "").Replace("</b>", "");
            _detailKeywordText.text = string.IsNullOrWhiteSpace(keywords)
                ? "（无关键词说明）"
                : keywords;

            if (_cardPrefab != null)
            {
                var view = Instantiate(_cardPrefab, _detailCardAnchor);
                CardView.ApplyHandPresentationScaleCentered(view, DetailCardScale);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                view.BindWithCard(
                    preview,
                    visual,
                    false,
                    false,
                    false,
                    "",
                    statsLine,
                    _uiIcons,
                    _characterVisuals,
                    null,
                    null,
                    null);
            }

            _removeButton.interactable = true;
            HideConfirmPanel();
        }

        void ClearDetailCard()
        {
            if (_detailCardAnchor == null)
                return;

            foreach (Transform child in _detailCardAnchor)
                Destroy(child.gameObject);
        }

        void TryRemoveCurrent()
        {
            if (_collection == null || _detailEntryIndex < 0 || _detailEntryIndex >= _collection.Count)
                return;

            _definitions.TryGetValue(_collection.Entries[_detailEntryIndex], out var definition);
            var cardName = definition != null ? definition.DisplayName : _collection.Entries[_detailEntryIndex];
            ShowConfirmPanel($"确定永久移除「{cardName}」？\n此操作不可撤销。");
        }

        void ConfirmRemoveCurrent()
        {
            if (_collection == null || _detailEntryIndex < 0)
                return;

            if (!CampCollectionRules.TryRemoveCollectionEntry(_collection, _detailEntryIndex, out var message))
            {
                HideConfirmPanel();
                return;
            }

            if (_roster != null)
                CampRosterLoadoutRules.OnCollectionEntryRemoved(_roster, _detailEntryIndex);

            HideConfirmPanel();
            _onCollectionChanged?.Invoke();
            ShowListPanel();
            RebuildList();
            _statusText.text = message;
        }

        void ShowConfirmPanel(string body)
        {
            if (_confirmPanel == null)
                return;

            _confirmBodyText.text = body;
            _confirmPanel.SetActive(true);
            _confirmPanel.transform.SetAsLastSibling();
        }

        void HideConfirmPanel()
        {
            if (_confirmPanel != null)
                _confirmPanel.SetActive(false);
        }

        void CycleCostFilter()
        {
            var current = Array.IndexOf(CostFilters, _filterCost);
            _filterCost = CostFilters[(current + 1) % CostFilters.Length];
            RebuildList();
        }

        void CycleRarityFilter()
        {
            var current = Array.IndexOf(RarityFilters, _filterRarity);
            _filterRarity = RarityFilters[(current + 1) % RarityFilters.Length];
            RebuildList();
        }

        void CycleOwnerFilter()
        {
            if (_characters.Count == 0)
            {
                _filterOwnerId = "";
                RebuildList();
                return;
            }

            if (string.IsNullOrEmpty(_filterOwnerId))
            {
                _filterOwnerId = _characters[0].CharacterId;
                RebuildList();
                return;
            }

            var index = _characters.FindIndex(c => c.CharacterId == _filterOwnerId);
            if (index < 0 || index >= _characters.Count - 1)
            {
                _filterOwnerId = "";
                RebuildList();
                return;
            }

            _filterOwnerId = _characters[index + 1].CharacterId;
            RebuildList();
        }

        void UpdateFilterLabels()
        {
            if (_filterCostButton != null)
            {
                var label = _filterCostButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"费用：{CampCardUiLabels.FormatCostFilter(_filterCost)}";
            }

            if (_filterRarityButton != null)
            {
                var label = _filterRarityButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"稀有度：{CampCardUiLabels.FormatRarityFilter(_filterRarity)}";
            }

            if (_filterOwnerButton != null)
            {
                var label = _filterOwnerButton.GetComponentInChildren<Text>();
                if (label != null)
                    label.text = $"角色：{CampCardUiLabels.FormatOwnerFilter(_filterOwnerId, _characters)}";
            }
        }

        void ClearDynamic()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
        }

        static List<CharacterDefinitionSO> CollectCharacters(BattleSetupSO battleSetup)
        {
            var list = new List<CharacterDefinitionSO>();
            var seen = new HashSet<string>();
            if (battleSetup?.Combatants == null)
                return list;

            foreach (var character in battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                if (!seen.Add(character.CharacterId))
                    continue;

                list.Add(character);
            }

            return list;
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            _panel = CampUiRuntime.CreateRect("CollectionManagePanel", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_panel);
            _panel.gameObject.SetActive(false);

            BuildListPanel();
            BuildDetailPanel();
        }

        void BuildListPanel()
        {
            _listPanel = CampUiRuntime.CreateImage("List", _panel, new Color(0.07f, 0.08f, 0.11f, 0.98f))
                .rectTransform;
            CampUiRuntime.StretchFull(_listPanel);

            _titleText = CampUiRuntime.CreateText(_listPanel, "管理卡牌", 28, FontStyle.Bold, TextAnchor.UpperCenter);
            _titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            _titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _titleText.rectTransform.offsetMin = new Vector2(0f, -52f);
            _titleText.rectTransform.offsetMax = new Vector2(0f, -8f);
            _titleText.color = new Color(0.95f, 0.88f, 0.62f, 1f);

            var backBtn = CampUiRuntime.CreateButton(_listPanel, "返回军营", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 42f));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1f, 1f);
            backRt.anchorMax = new Vector2(1f, 1f);
            backRt.pivot = new Vector2(1f, 1f);
            backRt.anchoredPosition = new Vector2(-8f, -8f);
            backBtn.onClick.AddListener(() => _onBack?.Invoke());

            _summaryText = CampUiRuntime.CreateText(_listPanel, "", 16, FontStyle.Normal, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(_summaryText.rectTransform, 0.03f, 0.9f, 0.55f, 0.94f);
            _summaryText.color = new Color(0.82f, 0.86f, 0.95f, 1f);

            _statusText = CampUiRuntime.CreateText(_listPanel, "", 15, FontStyle.Italic, TextAnchor.UpperRight);
            CampUiRuntime.SetAnchored(_statusText.rectTransform, 0.55f, 0.9f, 0.97f, 0.94f);
            _statusText.color = new Color(0.72f, 0.76f, 0.84f, 1f);

            var filterRow = CampUiRuntime.CreateRect("Filters", _listPanel);
            var filterRt = filterRow.GetComponent<RectTransform>();
            filterRt.anchorMin = new Vector2(0f, 1f);
            filterRt.anchorMax = new Vector2(1f, 1f);
            filterRt.offsetMin = new Vector2(20f, -132f);
            filterRt.offsetMax = new Vector2(-20f, -96f);
            var filterLayout = filterRow.AddComponent<HorizontalLayoutGroup>();
            filterLayout.spacing = 12f;
            filterLayout.childAlignment = TextAnchor.MiddleLeft;
            filterLayout.childControlWidth = false;
            filterLayout.childControlHeight = true;
            filterLayout.childForceExpandWidth = false;
            filterLayout.childForceExpandHeight = true;

            _filterCostButton = CreateFilterButton(filterRow.transform, CycleCostFilter);
            _filterRarityButton = CreateFilterButton(filterRow.transform, CycleRarityFilter);
            _filterOwnerButton = CreateFilterButton(filterRow.transform, CycleOwnerFilter);

            var scrollGo = CampUiRuntime.CreateRect("Scroll", _listPanel);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(20f, 20f);
            scrollRt.offsetMax = new Vector2(-20f, -140f);
            scrollGo.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.65f);

            _cardScroll = scrollGo.AddComponent<ScrollRect>();
            _cardScroll.horizontal = false;
            _cardScroll.vertical = true;
            _cardScroll.movementType = ScrollRect.MovementType.Clamped;
            _cardScroll.scrollSensitivity = 36f;

            var viewport = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            _cardScroll.viewport = viewportRt;

            _cardGrid = CampUiRuntime.CreateRect("Grid", viewport.transform).GetComponent<RectTransform>();
            _cardGrid.anchorMin = new Vector2(0f, 1f);
            _cardGrid.anchorMax = new Vector2(1f, 1f);
            _cardGrid.pivot = new Vector2(0.5f, 1f);
            _cardGrid.offsetMin = Vector2.zero;
            _cardGrid.offsetMax = Vector2.zero;
            _gridLayout = _cardGrid.gameObject.AddComponent<GridLayoutGroup>();
            _gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _gridLayout.childAlignment = TextAnchor.UpperCenter;
            var fitter = _cardGrid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cardScroll.content = _cardGrid;
        }

        void BuildDetailPanel()
        {
            _detailPanel = CampUiRuntime.CreateImage("Detail", _panel, new Color(0.08f, 0.09f, 0.12f, 0.99f))
                .rectTransform;
            CampUiRuntime.Stretch(_detailPanel, 24f, 24f, -24f, -24f);
            _detailPanel.gameObject.SetActive(false);

            var backBtn = CampUiRuntime.CreateButton(_detailPanel, "返回列表", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 42f));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1f, 1f);
            backRt.anchorMax = new Vector2(1f, 1f);
            backRt.pivot = new Vector2(1f, 1f);
            backRt.anchoredPosition = new Vector2(-8f, -8f);
            backBtn.onClick.AddListener(ShowListPanel);

            _detailTitleText = CampUiRuntime.CreateText(_detailPanel, "", 34, FontStyle.Bold, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(_detailTitleText.rectTransform, 0.04f, 0.9f, 0.96f, 0.98f);
            _detailTitleText.color = new Color(0.95f, 0.88f, 0.62f, 1f);

            _detailMetaText = CampUiRuntime.CreateText(_detailPanel, "", 20, FontStyle.Normal, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(_detailMetaText.rectTransform, 0.44f, 0.84f, 0.96f, 0.9f);
            _detailMetaText.color = new Color(0.82f, 0.86f, 0.95f, 1f);

            _detailCardAnchor = CampUiRuntime.CreateRect("CardAnchor", _detailPanel).GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(_detailCardAnchor, 0.03f, 0.1f, 0.4f, 0.9f);
            var cardAnchorBlocker = _detailCardAnchor.gameObject.AddComponent<Image>();
            cardAnchorBlocker.color = new Color(0f, 0f, 0f, 0f);
            cardAnchorBlocker.raycastTarget = false;

            var infoBox = CampUiRuntime.CreateImage("InfoBox", _detailPanel, new Color(0.12f, 0.14f, 0.19f, 0.95f));
            var infoRt = infoBox.rectTransform;
            CampUiRuntime.SetAnchored(infoRt, 0.42f, 0.1f, 0.96f, 0.82f);

            var statsLabel = CampUiRuntime.CreateText(infoBox.transform, "属性", 22, FontStyle.Bold, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(statsLabel.rectTransform, 0.04f, 0.88f, 0.96f, 0.98f);

            _detailStatsText = CampUiRuntime.CreateText(infoBox.transform, "", 20, FontStyle.Normal, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(_detailStatsText.rectTransform, 0.04f, 0.68f, 0.96f, 0.88f);

            var keywordLabel = CampUiRuntime.CreateText(infoBox.transform, "关键词说明", 22, FontStyle.Bold,
                TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(keywordLabel.rectTransform, 0.04f, 0.62f, 0.96f, 0.7f);

            var keywordScrollGo = CampUiRuntime.CreateRect("KeywordScroll", infoBox.transform);
            var keywordScrollRt = keywordScrollGo.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(keywordScrollRt, 0.04f, 0.04f, 0.96f, 0.64f);
            keywordScrollGo.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.55f);
            var keywordScroll = keywordScrollGo.AddComponent<ScrollRect>();
            keywordScroll.horizontal = false;
            keywordScroll.vertical = true;
            keywordScroll.movementType = ScrollRect.MovementType.Clamped;

            var keywordViewport = CampUiRuntime.CreateRect("Viewport", keywordScrollGo.transform);
            var keywordViewportRt = keywordViewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(keywordViewportRt);
            keywordViewport.AddComponent<Mask>().showMaskGraphic = false;
            keywordViewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            keywordScroll.viewport = keywordViewportRt;

            var keywordContent = CampUiRuntime.CreateRect("Content", keywordViewport.transform).GetComponent<RectTransform>();
            keywordContent.anchorMin = new Vector2(0f, 1f);
            keywordContent.anchorMax = new Vector2(1f, 1f);
            keywordContent.pivot = new Vector2(0.5f, 1f);
            _detailKeywordText = CampUiRuntime.CreateText(keywordContent, "", 18, FontStyle.Normal, TextAnchor.UpperLeft);
            CampUiRuntime.Stretch(_detailKeywordText.rectTransform, 8f, 8f, -8f, -8f);
            _detailKeywordText.color = new Color(0.86f, 0.9f, 0.98f, 1f);
            _detailKeywordText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailKeywordText.verticalOverflow = VerticalWrapMode.Overflow;
            var keywordFitter = keywordContent.gameObject.AddComponent<ContentSizeFitter>();
            keywordFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            keywordScroll.content = keywordContent;

            _removeButton = CampUiRuntime.CreateButton(_detailPanel, "永久移除", new Color(0.62f, 0.22f, 0.22f, 1f),
                new Vector2(200f, 52f));
            var removeRt = _removeButton.GetComponent<RectTransform>();
            removeRt.anchorMin = new Vector2(0.03f, 0.03f);
            removeRt.anchorMax = new Vector2(0.03f, 0.03f);
            removeRt.pivot = new Vector2(0f, 0f);
            removeRt.anchoredPosition = Vector2.zero;
            _removeButton.onClick.AddListener(TryRemoveCurrent);
            _removeButton.transform.SetAsLastSibling();

            BuildConfirmPanel();
        }

        void BuildConfirmPanel()
        {
            _confirmPanel = CampUiRuntime.CreateImage("ConfirmRemove", _panel, new Color(0f, 0f, 0f, 0.72f)).gameObject;
            CampUiRuntime.StretchFull(_confirmPanel.GetComponent<RectTransform>());

            var dialog = CampUiRuntime.CreateImage("Dialog", _confirmPanel.transform, new Color(0.1f, 0.11f, 0.15f, 0.98f));
            var dialogRt = dialog.rectTransform;
            dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(520f, 260f);

            var title = CampUiRuntime.CreateText(dialog.transform, "确认移除", 24, FontStyle.Bold, TextAnchor.UpperCenter);
            CampUiRuntime.SetAnchored(title.rectTransform, 0.08f, 0.78f, 0.92f, 0.94f);
            title.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _confirmBodyText = CampUiRuntime.CreateText(dialog.transform, "", 18, FontStyle.Normal, TextAnchor.UpperCenter);
            CampUiRuntime.SetAnchored(_confirmBodyText.rectTransform, 0.08f, 0.34f, 0.92f, 0.76f);
            _confirmBodyText.color = new Color(0.88f, 0.9f, 0.96f, 1f);
            _confirmBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var cancelBtn = CampUiRuntime.CreateButton(dialog.transform, "取消", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(160f, 44f));
            var cancelRt = cancelBtn.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.12f, 0.1f);
            cancelRt.anchorMax = new Vector2(0.12f, 0.1f);
            cancelRt.pivot = new Vector2(0f, 0f);
            cancelRt.anchoredPosition = Vector2.zero;
            cancelBtn.onClick.AddListener(HideConfirmPanel);

            var confirmBtn = CampUiRuntime.CreateButton(dialog.transform, "确认移除", new Color(0.62f, 0.22f, 0.22f, 1f),
                new Vector2(160f, 44f));
            var confirmRt = confirmBtn.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.88f, 0.1f);
            confirmRt.anchorMax = new Vector2(0.88f, 0.1f);
            confirmRt.pivot = new Vector2(1f, 0f);
            confirmRt.anchoredPosition = Vector2.zero;
            confirmBtn.onClick.AddListener(ConfirmRemoveCurrent);

            _confirmPanel.SetActive(false);
        }

        static Button CreateFilterButton(Transform parent, Action onClick)
        {
            var btn = CampUiRuntime.CreateButton(parent, "筛选", new Color(0.2f, 0.24f, 0.32f, 0.98f),
                new Vector2(220f, 36f));
            btn.onClick.AddListener(() => onClick?.Invoke());
            var text = btn.GetComponentInChildren<Text>();
            if (text != null)
            {
                text.fontSize = 15;
                text.alignment = TextAnchor.MiddleCenter;
            }

            return btn;
        }
    }

    static class CampCardUiLabels
    {
        public static string FormatCostFilter(int? cost) =>
            cost switch
            {
                null => "全部",
                5 => "5+",
                _ => cost.Value.ToString()
            };

        public static string FormatRarityFilter(CardRarity? rarity) =>
            rarity.HasValue ? FormatRarity(rarity.Value) : "全部";

        public static string FormatRarity(string cardId) => FormatRarity(CardRarityTable.GetOrDefault(cardId));

        public static string FormatRarity(CardRarity rarity) =>
            rarity switch
            {
                CardRarity.Common => "普通",
                CardRarity.Rare => "稀有",
                CardRarity.SuperRare => "超稀有",
                CardRarity.Epic => "史诗",
                CardRarity.Legendary => "传说",
                _ => "未知"
            };

        public static string FormatType(CardType type) =>
            type switch
            {
                CardType.Attack => "攻击",
                CardType.Defense => "防御",
                CardType.Status => "状态",
                _ => type.ToString()
            };

        public static string FormatOwnerFilter(string ownerId, List<CharacterDefinitionSO> characters)
        {
            if (string.IsNullOrEmpty(ownerId))
                return "全部";

            foreach (var character in characters)
            {
                if (character != null && character.CharacterId == ownerId)
                    return character.DisplayName;
            }

            return ownerId;
        }

        public static string FormatOwner(string ownerId, List<CharacterDefinitionSO> characters) =>
            FormatOwnerFilter(ownerId, characters);
    }
}
