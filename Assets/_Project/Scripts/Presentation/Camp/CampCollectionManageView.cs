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
        const float ButtonHoverScale = 1.08f;

        // 模板空槽实测约 y=0.809–0.860；略放大并上扩以完全盖住底图框（勿大幅平移）
        static readonly Vector4 ZoneFilterCost = new(0.1000f, 0.8050f, 0.2400f, 0.8680f);
        static readonly Vector4 ZoneFilterRarity = new(0.2330f, 0.8050f, 0.3740f, 0.8680f);
        static readonly Vector4 ZoneFilterOwner = new(0.3660f, 0.8050f, 0.5080f, 0.8680f);
        // 返回：对齐概念图按钮区，略放大并稍向左，盖住底图框
        static readonly Vector4 ZoneBack = new(0.8480f, 0.8820f, 0.9820f, 0.9720f);
        static readonly Vector4 ZoneCards = new(0.1436f, 0.1948f, 0.8589f, 0.7561f);
        static readonly Vector4 ZoneScrollbar = new(0.8828f, 0.1934f, 0.8955f, 0.7439f);

        struct CollectionRow
        {
            public int Index;
            public string CardId;
            public CardDefinitionSO Definition;
            public string OwnerCharacterId;
            public bool IsEngraved;
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
        CampCardDetailView _detailView;
        RectTransform _cardGrid;
        ScrollRect _cardScroll;
        GridLayoutGroup _gridLayout;
        float _gridCardScale = 0.55f;
        CampConfirmPromptView _confirmPrompt;
        Button _filterCostButton;
        Button _filterRarityButton;
        Button _filterOwnerButton;

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

        public bool IsDetailOpen => _detailView != null && _detailView.IsOpen;
        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>ESC：确认框→否；卡牌详情→回列表；否则交由上层关闭收藏页。</summary>
        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;

            if (_confirmPrompt != null && _confirmPrompt.TryCancelViaEscape())
                return true;

            if (IsDetailOpen)
            {
                ShowListPanel();
                return true;
            }

            return false;
        }

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
            _detailView?.Hide();
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        void ShowListPanel()
        {
            _detailEntryIndex = -1;
            _listPanel.gameObject.SetActive(true);
            _detailView?.Hide();
        }

        void ShowDetailPanel(int entryIndex)
        {
            _detailEntryIndex = entryIndex;
            // 保持收藏列表可见，作为详情弹窗外围背景
            _listPanel.gameObject.SetActive(true);
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
                    OwnerCharacterId = ownerId,
                    IsEngraved = _collection.IsEngravedAt(i)
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
            if (_detailView == null || _collection == null
                || _detailEntryIndex < 0 || _detailEntryIndex >= _collection.Count)
                return;

            var cardId = _collection.Entries[_detailEntryIndex];
            _definitions.TryGetValue(cardId, out var definition);
            HideConfirmPanel();
            _detailView.Show(
                definition,
                cardId,
                showSell: true,
                onBack: ShowListPanel,
                onSell: TrySellCurrent,
                factionOverride: null,
                isEngraved: _collection.IsEngravedAt(_detailEntryIndex));
        }

        void TrySellCurrent()
        {
            if (_collection == null || _detailEntryIndex < 0 || _detailEntryIndex >= _collection.Count)
                return;

            var cardId = _collection.Entries[_detailEntryIndex];
            _definitions.TryGetValue(cardId, out var definition);
            var cardName = definition != null ? definition.DisplayName : cardId;
            var rarity = definition != null ? definition.Rarity : CardRarityTable.GetOrDefault(cardId);
            _pendingSellGold = CampCollectionRules.GetSellGold(rarity);
            ShowConfirmPanel($"确定出售「{cardName}」？\n将获得 {_pendingSellGold} 黄金，并从收藏中移除。");
        }

        void ConfirmSellCurrent()
        {
            if (_collection == null || _detailEntryIndex < 0)
                return;

            var cardId = _collection.Entries[_detailEntryIndex];
            _definitions.TryGetValue(cardId, out var definition);
            var rarity = definition != null ? definition.Rarity : CardRarityTable.GetOrDefault(cardId);

            if (!CampCollectionRules.TrySellCollectionEntry(
                    _collection, _detailEntryIndex, rarity, out var goldGained, out _))
                return;

            if (_roster != null)
                CampRosterLoadoutRules.OnCollectionEntryRemoved(_roster, _detailEntryIndex);

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
            if (_confirmPrompt == null)
                return;

            _confirmPrompt.Show(
                "确认出售",
                body,
                "取消",
                "确认出售",
                onCancel: null,
                onConfirm: ConfirmSellCurrent);
        }

        void HideConfirmPanel()
        {
            _confirmPrompt?.Hide();
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
            var host = CampUiRuntime.CreateRect("DetailHost", _panel);
            CampUiRuntime.StretchFull(host.GetComponent<RectTransform>());
            _detailView = host.AddComponent<CampCardDetailView>();
            _detailView.Initialize(
                _cardPrefab,
                _cardCatalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                _characters);
            _confirmPrompt = CampConfirmPromptView.Create(_panel, _uiIcons, "ConfirmSell");
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
