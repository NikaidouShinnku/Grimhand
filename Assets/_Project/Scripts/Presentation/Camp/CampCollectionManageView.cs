using System;
using System.Collections.Generic;
using Grimhand.Battle;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>军营收藏管理：筛选、浏览、详情与出售。</summary>
    [DisallowMultipleComponent]
    public sealed class CampCollectionManageView : MonoBehaviour
    {
        const int CardsPerRow = 6;
        const float DetailCardScale = 1.72f;
        const float SellButtonWidth = 200f;
        const float ButtonHoverScale = 1.08f;

        // 对照用户红框（原点左下）：筛选 / 返回 / 卡区虚影 / 滑动条
        static readonly Vector4 ZoneFilterCost = new(0.1055f, 0.8084f, 0.2344f, 0.8554f);
        static readonly Vector4 ZoneFilterRarity = new(0.2383f, 0.8066f, 0.3682f, 0.8537f);
        static readonly Vector4 ZoneFilterOwner = new(0.3750f, 0.8049f, 0.5039f, 0.8571f);
        static readonly Vector4 ZoneBack = new(0.8584f, 0.8902f, 0.9756f, 0.9617f);
        static readonly Vector4 ZoneCards = new(0.1436f, 0.1948f, 0.8589f, 0.7561f);
        static readonly Vector4 ZoneScrollbar = new(0.8828f, 0.1934f, 0.8955f, 0.7439f);

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
        Action<int> _onGoldGained;
        Action _onBack;

        CampCollectionState _collection;
        CampRosterState _roster;
        int _pendingSellGold;

        RectTransform _panel;
        RectTransform _listPanel;
        RectTransform _detailPanel;
        RectTransform _cardGrid;
        ScrollRect _cardScroll;
        ScrollRect _keywordScroll;
        GridLayoutGroup _gridLayout;
        float _gridCardScale = 0.55f;
        GameObject _confirmPanel;
        Text _confirmTitleText;
        Text _confirmBodyText;
        Text _detailTitleText;
        Text _detailMetaText;
        Text _detailStatsText;
        Text _detailKeywordText;
        Text _sellGoldText;
        Image _sellGoldIcon;
        RectTransform _detailCardAnchor;
        Button _filterCostButton;
        Button _filterRarityButton;
        Button _filterOwnerButton;
        Button _sellButton;

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
            Action<int> onGoldGained,
            Action onBack)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onCollectionChanged = onCollectionChanged;
            _onGoldGained = onGoldGained;
            _onBack = onBack;
            _characters = CollectCharacters(battleSetup);
            EnsureBuilt();
        }

        public void Show(CampCollectionState collection, CampRosterState roster, int collectionCapacity)
        {
            _ = collectionCapacity;
            _collection = collection;
            _roster = roster;
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
            var scrollY = ScrollRectNavigation.CaptureVertical(_cardScroll);
            ClearDynamic();
            UpdateFilterLabels();
            if (_collection == null)
                return;

            var rows = BuildFilteredRows();
            RefreshGridLayout();
            foreach (var row in rows)
                BuildCardCell(row);

            LayoutRebuilder.ForceRebuildLayoutImmediate(_cardGrid);
            Canvas.ForceUpdateCanvases();
            ScrollRectNavigation.RestoreVertical(_cardScroll, scrollY);
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

            // 角色分类 → 稀有度升序 → 名称，方便浏览与测试
            rows.Sort(CompareCollectionRows);
            return rows;
        }

        int CompareCollectionRows(CollectionRow a, CollectionRow b)
        {
            var ownerCmp = OwnerSortKey(a.OwnerCharacterId).CompareTo(OwnerSortKey(b.OwnerCharacterId));
            if (ownerCmp != 0)
                return ownerCmp;

            var rarityA = a.Definition != null ? a.Definition.Rarity : CardRarityTable.GetOrDefault(a.CardId);
            var rarityB = b.Definition != null ? b.Definition.Rarity : CardRarityTable.GetOrDefault(b.CardId);
            var rarityCmp = rarityA.CompareTo(rarityB);
            if (rarityCmp != 0)
                return rarityCmp;

            var nameA = a.Definition != null ? a.Definition.DisplayName : a.CardId;
            var nameB = b.Definition != null ? b.Definition.DisplayName : b.CardId;
            var nameCmp = string.CompareOrdinal(nameA, nameB);
            return nameCmp != 0 ? nameCmp : a.Index.CompareTo(b.Index);
        }

        int OwnerSortKey(string ownerId)
        {
            if (string.IsNullOrEmpty(ownerId))
                return int.MaxValue;

            for (var i = 0; i < _characters.Count; i++)
            {
                if (_characters[i] != null && _characters[i].CharacterId == ownerId)
                    return i;
            }

            return int.MaxValue - 1;
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
            var viewportHeight = _cardScroll.viewport.rect.height;
            if (viewportWidth <= 1f)
                viewportWidth = 1600f;
            if (viewportHeight <= 1f)
                viewportHeight = 900f;

            // 6×2 虚影区归一化宽高：卡格与间距按视口比例对齐模板
            const float zoneW = 0.7153f;
            const float zoneH = 0.5613f;
            var cellWidth = viewportWidth * (0.1103f / zoneW);
            var cellHeight = viewportHeight * (0.2753f / zoneH);
            var spacingX = viewportWidth * (0.0107f / zoneW);
            var spacingY = viewportHeight * (0.0107f / zoneH);

            _gridLayout.padding = new RectOffset(0, 0, 0, 0);
            _gridLayout.spacing = new Vector2(spacingX, spacingY);
            _gridLayout.cellSize = new Vector2(cellWidth, cellHeight);
            _gridLayout.constraintCount = CardsPerRow;
            _gridLayout.childAlignment = TextAnchor.UpperLeft;
            _gridCardScale = Mathf.Clamp(cellWidth / 168f * 0.92f, 0.35f, 0.95f);
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
            RefreshKeywordScrollLayout();

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

            _sellButton.interactable = true;
            RefreshSellPriceDisplay(cardId, definition);
            HideConfirmPanel();
        }

        void RefreshSellPriceDisplay(string cardId, CardDefinitionSO definition)
        {
            var gold = CampCollectionRules.GetSellGold(ResolveRarity(cardId, definition));
            if (_sellGoldText != null)
                _sellGoldText.text = gold.ToString();
            if (_sellGoldIcon != null && _uiIcons?.CampGoldIcon != null)
                _sellGoldIcon.sprite = _uiIcons.CampGoldIcon;
        }

        static CardRarity ResolveRarity(string cardId, CardDefinitionSO definition)
        {
            if (definition != null)
                return definition.Rarity;
            return CardRarityTable.GetOrDefault(cardId);
        }

        void RefreshKeywordScrollLayout()
        {
            if (_detailKeywordText == null)
                return;

            Canvas.ForceUpdateCanvases();
            var textRt = _detailKeywordText.rectTransform;
            var content = textRt.parent as RectTransform;
            var viewport = content != null ? content.parent as RectTransform : null;

            // 先固定宽度，再按换行后的 preferredHeight 撑开内容；否则 ContentSizeFitter
            // 在高度为 0 时算不出滚动区，表现为拖一下立刻回弹。
            var width = viewport != null ? Mathf.Max(40f, viewport.rect.width - 16f) : 400f;
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            LayoutRebuilder.ForceRebuildLayoutImmediate(textRt);

            var preferred = Mathf.Max(_detailKeywordText.preferredHeight + 8f, 1f);
            textRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred);
            if (content != null)
            {
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport != null ? viewport.rect.width : width);
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred);
                LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            }

            if (_keywordScroll != null)
                _keywordScroll.verticalNormalizedPosition = 1f;
        }

        void ClearDetailCard()
        {
            if (_detailCardAnchor == null)
                return;

            foreach (Transform child in _detailCardAnchor)
                Destroy(child.gameObject);
        }

        void TrySellCurrent()
        {
            if (_collection == null || _detailEntryIndex < 0 || _detailEntryIndex >= _collection.Count)
                return;

            var cardId = _collection.Entries[_detailEntryIndex];
            _definitions.TryGetValue(cardId, out var definition);
            var cardName = definition != null ? definition.DisplayName : cardId;
            _pendingSellGold = CampCollectionRules.GetSellGold(ResolveRarity(cardId, definition));
            ShowConfirmPanel($"确定出售「{cardName}」？\n将获得 {_pendingSellGold} 黄金，并从收藏中移除。");
        }

        void ConfirmSellCurrent()
        {
            if (_collection == null || _detailEntryIndex < 0)
                return;

            var cardId = _collection.Entries[_detailEntryIndex];
            _definitions.TryGetValue(cardId, out var definition);
            var rarity = ResolveRarity(cardId, definition);

            if (!CampCollectionRules.TrySellCollectionEntry(
                    _collection, _detailEntryIndex, rarity, out var goldGained, out _))
            {
                HideConfirmPanel();
                return;
            }

            if (_roster != null)
                CampRosterLoadoutRules.OnCollectionEntryRemoved(_roster, _detailEntryIndex);

            HideConfirmPanel();
            if (goldGained > 0)
            {
                _onGoldGained?.Invoke(goldGained);
                GameAudioService.Instance.PlayUiGoldAcquire();
            }

            _onCollectionChanged?.Invoke();
            ShowListPanel();
            RebuildList();
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
            var listBg = CampUiRuntime.CreateImage("List", _panel, Color.white);
            _listPanel = listBg.rectTransform;
            CampUiRuntime.StretchFull(_listPanel);
            listBg.preserveAspect = false;
            listBg.raycastTarget = true;
            if (_uiIcons != null && _uiIcons.ChampionCampCollectionBackground != null)
            {
                listBg.sprite = _uiIcons.ChampionCampCollectionBackground;
            }
            else
            {
                listBg.color = new Color(0.07f, 0.08f, 0.11f, 0.98f);
                Debug.LogWarning("[CampCollection] 缺少 ChampionCampCollectionBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            // 标题 / 提示文案已在模板上

            _filterCostButton = CreateFilterButton("FilterCost", ZoneFilterCost, CycleCostFilter);
            _filterRarityButton = CreateFilterButton("FilterRarity", ZoneFilterRarity, CycleRarityFilter);
            _filterOwnerButton = CreateFilterButton("FilterOwner", ZoneFilterOwner, CycleOwnerFilter);

            CreateBackButton();

            var scrollGo = CampUiRuntime.CreateRect("Scroll", _listPanel);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(scrollRt, ZoneCards.x, ZoneCards.y, ZoneCards.z, ZoneCards.w);
            scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            _cardScroll = scrollGo.AddComponent<ScrollRect>();
            _cardScroll.horizontal = false;
            _cardScroll.vertical = true;
            _cardScroll.movementType = ScrollRect.MovementType.Clamped;
            _cardScroll.scrollSensitivity = 36f;

            var viewport = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImg.raycastTarget = true;
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
            _gridLayout.childAlignment = TextAnchor.UpperLeft;
            var fitter = _cardGrid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _cardScroll.content = _cardGrid;

            BuildCardScrollbar();
        }

        void CreateBackButton()
        {
            var go = CampUiRuntime.CreateRect("Back", _listPanel);
            var rt = go.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(rt, ZoneBack.x, ZoneBack.y, ZoneBack.z, ZoneBack.w);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton2 != null)
                img.sprite = _uiIcons.UiButton2;
            else
                img.color = new Color(0.35f, 0.28f, 0.18f, 0.95f);

            var label = CampUiRuntime.CreateText(go.transform, "返回", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            label.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => _onBack?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        void BuildCardScrollbar()
        {
            var barGo = CampUiRuntime.CreateRect("CardScrollbar", _listPanel);
            var barRt = barGo.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(barRt, ZoneScrollbar.x, ZoneScrollbar.y, ZoneScrollbar.z, ZoneScrollbar.w);

            var barImg = barGo.AddComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = true;
            barImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSliderBar != null)
                barImg.sprite = _uiIcons.UiSliderBar;
            else
                barImg.color = new Color(0.12f, 0.11f, 0.1f, 0.95f);

            var slidingArea = CampUiRuntime.CreateRect("Sliding Area", barGo.transform);
            var slidingRt = slidingArea.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(slidingRt);
            slidingRt.offsetMin = new Vector2(1f, 10f);
            slidingRt.offsetMax = new Vector2(-1f, -10f);

            var handleGo = CampUiRuntime.CreateRect("Handle", slidingArea.transform);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;
            handleImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSlider != null)
                handleImg.sprite = _uiIcons.UiSlider;
            else
                handleImg.color = new Color(0.42f, 0.34f, 0.28f, 1f);

            var scrollbar = barGo.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImg;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;
            scrollbar.size = 1f;
            scrollbar.numberOfSteps = 0;

            _cardScroll.verticalScrollbar = scrollbar;
            _cardScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _cardScroll.verticalScrollbarSpacing = 0f;
        }

        void BuildDetailPanel()
        {
            _detailPanel = CampUiRuntime.CreateImage("Detail", _panel, new Color(0.08f, 0.09f, 0.12f, 0.99f))
                .rectTransform;
            CampUiRuntime.Stretch(_detailPanel, 24f, 24f, -24f, -24f);
            _detailPanel.gameObject.SetActive(false);

            var backBtn = CampUiRuntime.CreateButton(_detailPanel, "返回收藏", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(160f, 42f));
            var backRt = backBtn.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1f, 1f);
            backRt.anchorMax = new Vector2(1f, 1f);
            backRt.pivot = new Vector2(1f, 1f);
            backRt.anchoredPosition = new Vector2(-8f, -8f);
            backBtn.onClick.AddListener(ShowListPanel);

            _detailTitleText = CampUiRuntime.CreateText(_detailPanel, "", 34, FontStyle.Bold, TextAnchor.UpperLeft);
            // 右侧留给「返回收藏」，避免标题全宽透明区域挡住按钮左半边点击。
            CampUiRuntime.SetAnchored(_detailTitleText.rectTransform, 0.04f, 0.9f, 0.72f, 0.98f);
            _detailTitleText.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            _detailTitleText.raycastTarget = false;

            _detailMetaText = CampUiRuntime.CreateText(_detailPanel, "", 20, FontStyle.Normal, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(_detailMetaText.rectTransform, 0.44f, 0.84f, 0.96f, 0.9f);
            _detailMetaText.color = new Color(0.82f, 0.86f, 0.95f, 1f);
            _detailMetaText.raycastTarget = false;

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
            _keywordScroll = keywordScrollGo.AddComponent<ScrollRect>();
            _keywordScroll.horizontal = false;
            _keywordScroll.vertical = true;
            _keywordScroll.movementType = ScrollRect.MovementType.Clamped;
            _keywordScroll.scrollSensitivity = 28f;
            _keywordScroll.inertia = true;
            var keywordScroll = _keywordScroll;

            var keywordViewport = CampUiRuntime.CreateRect("Viewport", keywordScrollGo.transform);
            var keywordViewportRt = keywordViewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(keywordViewportRt);
            keywordViewport.AddComponent<Mask>().showMaskGraphic = false;
            keywordViewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            keywordScroll.viewport = keywordViewportRt;

            var keywordContent = CampUiRuntime.CreateRect("Content", keywordViewport.transform)
                .GetComponent<RectTransform>();
            keywordContent.anchorMin = new Vector2(0f, 1f);
            keywordContent.anchorMax = new Vector2(1f, 1f);
            keywordContent.pivot = new Vector2(0.5f, 1f);
            keywordContent.anchoredPosition = Vector2.zero;
            keywordContent.sizeDelta = new Vector2(0f, 0f);
            var keywordFitter = keywordContent.gameObject.AddComponent<ContentSizeFitter>();
            keywordFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            keywordFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            keywordScroll.content = keywordContent;

            _detailKeywordText = CampUiRuntime.CreateText(keywordContent, "", 18, FontStyle.Normal, TextAnchor.UpperLeft);
            var keywordTextRt = _detailKeywordText.rectTransform;
            keywordTextRt.anchorMin = new Vector2(0f, 1f);
            keywordTextRt.anchorMax = new Vector2(1f, 1f);
            keywordTextRt.pivot = new Vector2(0.5f, 1f);
            keywordTextRt.anchoredPosition = Vector2.zero;
            keywordTextRt.sizeDelta = new Vector2(-16f, 0f);
            _detailKeywordText.color = new Color(0.86f, 0.9f, 0.98f, 1f);
            _detailKeywordText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _detailKeywordText.verticalOverflow = VerticalWrapMode.Overflow;
            var keywordTextFitter = _detailKeywordText.gameObject.AddComponent<ContentSizeFitter>();
            keywordTextFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            keywordTextFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _sellButton = CampUiRuntime.CreateButton(_detailPanel, "出售卡牌", new Color(0.62f, 0.22f, 0.22f, 1f),
                new Vector2(SellButtonWidth, 52f));
            var sellRt = _sellButton.GetComponent<RectTransform>();
            sellRt.anchorMin = new Vector2(0.03f, 0.03f);
            sellRt.anchorMax = new Vector2(0.03f, 0.03f);
            sellRt.pivot = new Vector2(0f, 0f);
            sellRt.anchoredPosition = Vector2.zero;
            _sellButton.onClick.AddListener(TrySellCurrent);

            var sellGoldRow = CampUiRuntime.CreateRect("SellGold", _detailPanel).GetComponent<RectTransform>();
            sellGoldRow.anchorMin = new Vector2(0.03f, 0.03f);
            sellGoldRow.anchorMax = new Vector2(0.03f, 0.03f);
            sellGoldRow.pivot = new Vector2(0f, 0f);
            sellGoldRow.anchoredPosition = new Vector2(SellButtonWidth + 16f, 8f);
            sellGoldRow.sizeDelta = new Vector2(180f, 36f);
            var sellGoldLayout = sellGoldRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            sellGoldLayout.spacing = 8f;
            sellGoldLayout.childAlignment = TextAnchor.MiddleLeft;
            sellGoldLayout.childControlWidth = false;
            sellGoldLayout.childControlHeight = true;
            sellGoldLayout.childForceExpandWidth = false;
            sellGoldLayout.childForceExpandHeight = false;

            _sellGoldIcon = CampUiRuntime.CreateImage("SellGoldIcon", sellGoldRow, Color.white);
            _sellGoldIcon.preserveAspect = true;
            _sellGoldIcon.sprite = _uiIcons != null ? _uiIcons.CampGoldIcon : null;
            _sellGoldIcon.raycastTarget = false;
            var sellIconLe = _sellGoldIcon.gameObject.AddComponent<LayoutElement>();
            sellIconLe.preferredWidth = 32f;
            sellIconLe.preferredHeight = 32f;

            _sellGoldText = CampUiRuntime.CreateText(sellGoldRow, "0", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            _sellGoldText.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            _sellGoldText.raycastTarget = false;
            var sellTextLe = _sellGoldText.gameObject.AddComponent<LayoutElement>();
            sellTextLe.preferredWidth = 120f;
            sellTextLe.preferredHeight = 32f;

            backBtn.transform.SetAsLastSibling();
            _sellButton.transform.SetAsLastSibling();
            sellGoldRow.SetAsLastSibling();

            BuildConfirmPanel();
        }

        void BuildConfirmPanel()
        {
            _confirmPanel = CampUiRuntime.CreateImage("ConfirmSell", _panel, new Color(0f, 0f, 0f, 0.72f)).gameObject;
            CampUiRuntime.StretchFull(_confirmPanel.GetComponent<RectTransform>());

            var dialog = CampUiRuntime.CreateImage("Dialog", _confirmPanel.transform, new Color(0.1f, 0.11f, 0.15f, 0.98f));
            var dialogRt = dialog.rectTransform;
            dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(520f, 260f);

            _confirmTitleText = CampUiRuntime.CreateText(dialog.transform, "确认出售", 24, FontStyle.Bold, TextAnchor.UpperCenter);
            CampUiRuntime.SetAnchored(_confirmTitleText.rectTransform, 0.08f, 0.78f, 0.92f, 0.94f);
            _confirmTitleText.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            _confirmTitleText.raycastTarget = false;

            _confirmBodyText = CampUiRuntime.CreateText(dialog.transform, "", 18, FontStyle.Normal, TextAnchor.UpperCenter);
            CampUiRuntime.SetAnchored(_confirmBodyText.rectTransform, 0.08f, 0.34f, 0.92f, 0.76f);
            _confirmBodyText.color = new Color(0.88f, 0.9f, 0.96f, 1f);
            _confirmBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _confirmBodyText.raycastTarget = false;

            var cancelBtn = CampUiRuntime.CreateButton(dialog.transform, "取消", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(160f, 44f));
            var cancelRt = cancelBtn.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.12f, 0.1f);
            cancelRt.anchorMax = new Vector2(0.12f, 0.1f);
            cancelRt.pivot = new Vector2(0f, 0f);
            cancelRt.anchoredPosition = Vector2.zero;
            cancelBtn.onClick.AddListener(HideConfirmPanel);

            var confirmBtn = CampUiRuntime.CreateButton(dialog.transform, "确认出售", new Color(0.62f, 0.22f, 0.22f, 1f),
                new Vector2(160f, 44f));
            var confirmRt = confirmBtn.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(0.88f, 0.1f);
            confirmRt.anchorMax = new Vector2(0.88f, 0.1f);
            confirmRt.pivot = new Vector2(1f, 0f);
            confirmRt.anchoredPosition = Vector2.zero;
            confirmBtn.onClick.AddListener(ConfirmSellCurrent);

            _confirmPanel.SetActive(false);
        }

        Button CreateFilterButton(string id, Vector4 zone, Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, _listPanel);
            var rt = go.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton4 != null)
                img.sprite = _uiIcons.UiButton4;
            else
                img.color = new Color(0.2f, 0.24f, 0.32f, 0.98f);

            var label = CampUiRuntime.CreateText(go.transform, "筛选", 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            label.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
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
