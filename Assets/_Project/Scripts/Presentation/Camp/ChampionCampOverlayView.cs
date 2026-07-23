using System;
using System.Collections.Generic;
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
    /// <summary>军营：3 角色槽 + 从收藏选取每人 10 张祭坛携带牌。</summary>
    [DisallowMultipleComponent]
    public sealed class ChampionCampOverlayView : MonoBehaviour
    {
        const float CardScale = 0.72f;
        const int CardsPerRow = 5;
        const float DeckSlotWidth = 108f;
        const float DeckSlotHeight = 152f;
        const float HubButtonHoverScale = 1.08f;

        /// <summary>归一化热区：原点左下，相对军营一级全屏。</summary>
        readonly struct HubNormRect
        {
            public readonly float XMin;
            public readonly float YMin;
            public readonly float XMax;
            public readonly float YMax;

            public HubNormRect(float xMin, float yMin, float xMax, float yMax)
            {
                XMin = xMin;
                YMin = yMin;
                XMax = xMax;
                YMax = yMax;
            }
        }

        // 对照用户红框（原点左下）：配置队伍 / 管理卡牌 / 右上角返回
        static readonly HubNormRect HubZoneTeam = new(0.1660f, 0.0754f, 0.4424f, 0.3053f);
        static readonly HubNormRect HubZoneCards = new(0.5332f, 0.0754f, 0.8115f, 0.3053f);
        static readonly HubNormRect HubZoneClose = new(0.8975f, 0.7912f, 0.9736f, 0.8754f);

        // 配置队伍二级：对照用户指示红/蓝框（原点左下）
        static readonly HubNormRect TeamZoneSave = new(0.7480f, 0.9025f, 0.8613f, 0.9892f);
        static readonly HubNormRect TeamZoneBack = new(0.8750f, 0.8971f, 0.9834f, 0.9892f);

        // 三角色整框（立绘红框 + 右侧名字/经验区）
        static readonly HubNormRect TeamZoneMemberBack = new(0.1650f, 0.6100f, 0.3850f, 0.8600f);
        static readonly HubNormRect TeamZoneMemberMid = new(0.3880f, 0.6100f, 0.6080f, 0.8620f);
        static readonly HubNormRect TeamZoneMemberFront = new(0.6100f, 0.6100f, 0.9000f, 0.8650f);

        // 立绘红框（绝对坐标）；实际显示会再内缩以保比例不越界
        static readonly HubNormRect TeamPortraitBack = new(0.1670f, 0.6054f, 0.2793f, 0.8523f);
        static readonly HubNormRect TeamPortraitMid = new(0.3926f, 0.6090f, 0.5049f, 0.8559f);
        static readonly HubNormRect TeamPortraitFront = new(0.6152f, 0.6054f, 0.7344f, 0.8595f);

        // 换人蓝框
        static readonly HubNormRect TeamSwapBack = new(0.1855f, 0.6180f, 0.2578f, 0.6739f);
        static readonly HubNormRect TeamSwapMid = new(0.4111f, 0.6162f, 0.4854f, 0.6757f);
        static readonly HubNormRect TeamSwapFront = new(0.6396f, 0.6126f, 0.7168f, 0.6775f);

        // 绿框：名字 / 等级 / 经验文字
        static readonly HubNormRect TeamNameBack = new(0.2861f, 0.7856f, 0.3486f, 0.8378f);
        static readonly HubNormRect TeamNameMid = new(0.5117f, 0.7892f, 0.5693f, 0.8450f);
        static readonly HubNormRect TeamNameFront = new(0.7441f, 0.7892f, 0.7998f, 0.8450f);
        static readonly HubNormRect TeamLevelBack = new(0.2910f, 0.7000f, 0.3428f, 0.7315f);
        static readonly HubNormRect TeamLevelMid = new(0.5176f, 0.7000f, 0.5693f, 0.7333f);
        static readonly HubNormRect TeamLevelFront = new(0.7490f, 0.7045f, 0.7881f, 0.7333f);
        static readonly HubNormRect TeamXpTextBack = new(0.2910f, 0.6631f, 0.3428f, 0.6970f);
        static readonly HubNormRect TeamXpTextMid = new(0.5176f, 0.6649f, 0.5693f, 0.6970f);
        static readonly HubNormRect TeamXpTextFront = new(0.7480f, 0.6649f, 0.8018f, 0.6973f);

        // 紫框：经验条（填入模板已有细条内）
        static readonly HubNormRect TeamXpBarBack = new(0.2920f, 0.6360f, 0.3682f, 0.6541f);
        static readonly HubNormRect TeamXpBarMid = new(0.5146f, 0.6324f, 0.5928f, 0.6577f);
        static readonly HubNormRect TeamXpBarFront = new(0.7441f, 0.6342f, 0.8213f, 0.6559f);

        const float TeamPortraitInset = 0.07f;

        // 携带卡区（两行 5 列，按首行红框推导）
        static readonly HubNormRect TeamZoneDeck = new(0.0900f, 0.1157f, 0.4960f, 0.5150f);
        // 军营收藏卡区：右缘留给粉色滑动条
        static readonly HubNormRect TeamZonePool = new(0.5350f, 0.1157f, 0.9000f, 0.5150f);
        // 粉色标注：固定 slider_bar + 可拖动 slider
        static readonly HubNormRect TeamZonePoolScrollbar = new(0.9023f, 0.0868f, 0.9180f, 0.5278f);
        const float TeamDeckCardX0 = 0.0957f;
        const float TeamDeckCardStep = 0.0801f;
        const float TeamDeckCardW = 0.0752f;
        const float TeamDeckRow1YMin = 0.3177f;
        const float TeamDeckRow1YMax = 0.5090f;
        const float TeamDeckRow2YMin = 0.1157f;
        const float TeamDeckRow2YMax = 0.3070f;

        static readonly Color TeamXpBarFill = new(0.28f, 0.78f, 0.38f, 1f);
        static readonly Color TeamSelectedOutline = new(0.35f, 0.75f, 1f, 1f);

        BattleSetupSO _battleSetup;
        ExpeditionSetupSO _expeditionSetup;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        List<CardDefinitionSO> _cardPool = new();
        List<CharacterDefinitionSO> _playableCharacters = new();

        CampRosterState _roster;
        CampMetaState _meta;
        CampCollectionState _collection;
        int _accountGold;
        int _collectionCapacity;
        Action<CampRosterState> _onRosterChanged;
        Action<CampCollectionState> _onCollectionChanged;
        Action<int> _onAccountGoldChanged;
        Action _onClose;

        RectTransform _overlayRoot;
        RectTransform _hubPanel;
        RectTransform _body;
        CampCollectionManageView _collectionManageView;
        RectTransform _memberRow;
        RectTransform _deckGrid;
        RectTransform _poolGrid;
        ScrollRect _poolScroll;
        Text _hintText;
        Text _metaSummaryText;
        Text _accountGoldAmountText;
        Image _campGoldIcon;
        Button _closeButton;
        Button _confirmButton;
        InventoryTooltipView _tooltip;

        int _activeMemberIndex;
        int _selectedDeckSlot = -1;
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public void Initialize(
            BattleSetupSO battleSetup,
            ExpeditionSetupSO expeditionSetup,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action<CampRosterState> onRosterChanged,
            Action<CampCollectionState> onCollectionChanged,
            Action<int> onAccountGoldChanged,
            Action onClose)
        {
            _battleSetup = battleSetup;
            _expeditionSetup = expeditionSetup;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onRosterChanged = onRosterChanged;
            _onCollectionChanged = onCollectionChanged;
            _onAccountGoldChanged = onAccountGoldChanged;
            _onClose = onClose;

            _cardPool = CampRosterBuilder.BuildCardCatalog(expeditionSetup);
            EnsureCardPoolFallback();
            _playableCharacters = CollectPlayableCharacters();
            EnsureBuilt();
        }

        void EnsureCardPoolFallback()
        {
            if (_cardPool.Count > 0 || _battleSetup == null)
                return;

            var seen = new HashSet<string>();
            foreach (var character in _battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                foreach (var card in character.Deck)
                {
                    if (card == null || string.IsNullOrEmpty(card.CardId) || !seen.Add(card.CardId))
                        continue;

                    _cardPool.Add(card);
                }
            }

            foreach (var pair in _definitions)
            {
                var card = pair.Value;
                if (card == null || string.IsNullOrEmpty(card.CardId) || !seen.Add(card.CardId))
                    continue;

                _cardPool.Add(card);
            }
        }

        public void BindRoster(CampRosterState roster)
        {
            _roster = roster;
            if (IsOpen)
                Rebuild();
        }

        public void Show(CampRosterState roster)
        {
            Show(roster, null, 0);
        }

        public void Show(CampRosterState roster, CampMetaState meta, int accountGold)
        {
            Show(roster, meta, accountGold, null, CampCollectionState.DefaultCapacity);
        }

        public void Show(
            CampRosterState roster,
            CampMetaState meta,
            int accountGold,
            CampCollectionState collection,
            int collectionCapacity)
        {
            _roster = roster;
            _meta = meta;
            _accountGold = accountGold;
            _collection = collection;
            _collectionCapacity = collectionCapacity;
            EnsureBuilt();
            SanitizeDuplicateCharacters();
            CampRosterLoadoutRules.SanitizeRoster(_roster, _collection, CardOwnerLookup);
            _overlayRoot.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _activeMemberIndex = 0;
            _selectedDeckSlot = 0;
            ShowHub();
        }

        /// <summary>营地快捷入口：直接打开卡牌收藏管理。</summary>
        public void ShowCollection(
            CampRosterState roster,
            CampMetaState meta,
            int accountGold,
            CampCollectionState collection,
            int collectionCapacity)
        {
            Show(roster, meta, accountGold, collection, collectionCapacity);
            ShowCollectionPanel();
        }

        void ShowHub()
        {
            _tooltip?.Hide();
            _hubPanel.gameObject.SetActive(true);
            _body.gameObject.SetActive(false);
            _collectionManageView?.Hide();
        }

        void ShowTeamPanel()
        {
            _tooltip?.Hide();
            _hubPanel.gameObject.SetActive(false);
            _body.gameObject.SetActive(true);
            _collectionManageView?.Hide();
            Rebuild();
        }

        void ShowCollectionPanel()
        {
            _tooltip?.Hide();
            _hubPanel.gameObject.SetActive(false);
            _body.gameObject.SetActive(false);
            _collectionManageView?.Show(_collection, _roster, _collectionCapacity);
        }

        void CloseToCamp()
        {
            Hide();
            _onClose?.Invoke();
        }

        void NotifyCollectionChanged()
        {
            _onRosterChanged?.Invoke(_roster);
            _onCollectionChanged?.Invoke(_collection);
        }

        void OnCollectionCardSold(int goldGained)
        {
            if (goldGained <= 0)
                return;

            _accountGold += goldGained;
            RefreshMetaSummary();
            _onAccountGoldChanged?.Invoke(_accountGold);
        }

        public void Hide()
        {
            _collectionManageView?.Hide();
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        List<CharacterDefinitionSO> CollectPlayableCharacters()
        {
            var list = new List<CharacterDefinitionSO>();
            if (_battleSetup?.Combatants == null)
                return list;

            var seen = new HashSet<string>();
            foreach (var character in _battleSetup.Combatants)
            {
                if (character == null || character.Team != TeamSide.Player)
                    continue;

                if (!seen.Add(character.CharacterId))
                    continue;

                list.Add(character);
            }

            foreach (var id in CampRosterBuilder.PlayableCharacterIds)
            {
                if (seen.Contains(id))
                    continue;

                var fromSetup = FindCharacterInSetup(id);
                if (fromSetup != null && seen.Add(id))
                    list.Add(fromSetup);
            }

            list.Sort((a, b) => IndexOfPlayable(a.CharacterId).CompareTo(IndexOfPlayable(b.CharacterId)));
            return list;
        }

        CharacterDefinitionSO FindCharacterInSetup(string characterId)
        {
            if (_battleSetup?.Combatants == null)
                return null;

            foreach (var c in _battleSetup.Combatants)
            {
                if (c != null && c.CharacterId == characterId)
                    return c;
            }

            return null;
        }

        static int IndexOfPlayable(string characterId)
        {
            for (var i = 0; i < CampRosterBuilder.PlayableCharacterIds.Count; i++)
            {
                if (CampRosterBuilder.PlayableCharacterIds[i] == characterId)
                    return i;
            }

            return 99;
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _overlayRoot = CampUiRuntime.CreateRect("ChampionCampOverlayRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            _overlayRoot.gameObject.SetActive(false);

            var backdrop = CampUiRuntime.CreateImage("Backdrop", _overlayRoot, new Color(0.02f, 0.03f, 0.05f, 0.94f));
            CampUiRuntime.StretchFull(backdrop.rectTransform);

            _body = CampUiRuntime.CreateRect("Body", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_body);
            var bodyBg = _body.gameObject.AddComponent<Image>();
            bodyBg.color = Color.white;
            bodyBg.preserveAspect = false;
            bodyBg.raycastTarget = true;
            if (_uiIcons != null && _uiIcons.ChampionCampTeamBackground != null)
                bodyBg.sprite = _uiIcons.ChampionCampTeamBackground;
            else
            {
                bodyBg.color = new Color(0.07f, 0.08f, 0.11f, 0.98f);
                Debug.LogWarning("[ChampionCamp] 缺少 ChampionCampTeamBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            // 右上常显：保存编队 / 返回
            var teamButtons = CampUiRuntime.CreateRect("TeamActionButtons", _body);
            CampUiRuntime.StretchFull(teamButtons.GetComponent<RectTransform>());
            var teamBtnCanvas = teamButtons.AddComponent<Canvas>();
            teamBtnCanvas.overrideSorting = true;
            teamBtnCanvas.sortingOrder = 40;
            teamButtons.AddComponent<GraphicRaycaster>();
            _confirmButton = CreateTeamActionButton(
                teamButtons.transform,
                "SaveFormation",
                TeamZoneSave,
                _uiIcons != null ? _uiIcons.UiButton1 : null,
                "保存编队",
                20,
                SaveAndReturnToHub);
            _closeButton = CreateTeamActionButton(
                teamButtons.transform,
                "BackToHub",
                TeamZoneBack,
                _uiIcons != null ? _uiIcons.UiButton2 : null,
                "返回",
                22,
                ShowHub);

            _memberRow = CampUiRuntime.CreateRect("MemberRow", _body).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_memberRow);

            _deckGrid = CampUiRuntime.CreateRect("DeckGrid", _body).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_deckGrid);

            var scrollGo = CampUiRuntime.CreateRect("PoolScroll", _body);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            ApplyHubNormRect(scrollRt, TeamZonePool);
            // 透明滚动区，不盖住模板底
            scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            _poolScroll = scrollGo.AddComponent<ScrollRect>();
            _poolScroll.horizontal = false;
            _poolScroll.vertical = true;
            _poolScroll.movementType = ScrollRect.MovementType.Clamped;
            _poolScroll.scrollSensitivity = 36f;

            var viewport = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0.1f, 0.11f, 0.14f, 0.01f);
            viewportImg.raycastTarget = true;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            _poolScroll.viewport = viewportRt;

            _poolGrid = CampUiRuntime.CreateRect("PoolGrid", viewport.transform).GetComponent<RectTransform>();
            _poolGrid.anchorMin = new Vector2(0f, 1f);
            _poolGrid.anchorMax = new Vector2(1f, 1f);
            _poolGrid.pivot = new Vector2(0.5f, 1f);
            _poolGrid.offsetMin = new Vector2(2f, 0f);
            _poolGrid.offsetMax = new Vector2(-2f, 0f);
            var poolLayout = _poolGrid.gameObject.AddComponent<GridLayoutGroup>();
            poolLayout.cellSize = new Vector2(168f * CardScale + 6f, 236f * CardScale + 6f);
            poolLayout.spacing = new Vector2(4f, 4f);
            poolLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            poolLayout.constraintCount = CardsPerRow;
            poolLayout.childAlignment = TextAnchor.UpperLeft;
            var poolFitter = _poolGrid.gameObject.AddComponent<ContentSizeFitter>();
            poolFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            poolFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _poolScroll.content = _poolGrid;

            BuildPoolScrollbar();

            _hintText = CampUiRuntime.CreateText(_body, "", 14, FontStyle.Italic, TextAnchor.MiddleCenter);
            _hintText.rectTransform.anchorMin = new Vector2(0.2f, 0.01f);
            _hintText.rectTransform.anchorMax = new Vector2(0.8f, 0.055f);
            _hintText.rectTransform.offsetMin = Vector2.zero;
            _hintText.rectTransform.offsetMax = Vector2.zero;
            _hintText.color = new Color(0.75f, 0.78f, 0.85f, 0.85f);
            _hintText.raycastTarget = false;

            // 金币/等级摘要改由角色框展示，这里保留空引用以兼容 RefreshMetaSummary
            _metaSummaryText = null;
            _accountGoldAmountText = null;
            _campGoldIcon = null;

            _tooltip = _overlayRoot.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_overlayRoot);

            BuildHubPanel();
            var collectionHost = CampUiRuntime.CreateRect("CollectionManageHost", _overlayRoot);
            CampUiRuntime.StretchFull(collectionHost.GetComponent<RectTransform>());
            _collectionManageView = collectionHost.AddComponent<CampCollectionManageView>();
            _collectionManageView.Initialize(
                _battleSetup,
                _cardPrefab,
                _cardCatalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                NotifyCollectionChanged,
                OnCollectionCardSold,
                ShowHub);

            _body.gameObject.SetActive(false);
        }

        void BuildPoolScrollbar()
        {
            // 粉色区：固定轨道 sliderbar + 可拖动手柄 slider，长度由 ScrollRect 按内容自动缩放
            var barGo = CampUiRuntime.CreateRect("PoolScrollbar", _body);
            var barRt = barGo.GetComponent<RectTransform>();
            ApplyHubNormRect(barRt, TeamZonePoolScrollbar);

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
            // 轨道两端装饰略内缩，手柄在槽内滑动
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

            _poolScroll.verticalScrollbar = scrollbar;
            _poolScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _poolScroll.verticalScrollbarSpacing = 0f;
        }

        void BuildHubPanel()
        {
            // 军营一级：模板全屏背景 + 透明热区；悬停弹出按钮精灵，点击进二级。
            _hubPanel = CampUiRuntime.CreateRect("HubPanel", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_hubPanel);

            var bg = _hubPanel.gameObject.AddComponent<Image>();
            bg.color = Color.white;
            bg.preserveAspect = false;
            bg.raycastTarget = true;
            if (_uiIcons != null && _uiIcons.ChampionCampHubBackground != null)
            {
                bg.sprite = _uiIcons.ChampionCampHubBackground;
            }
            else
            {
                bg.color = new Color(0.07f, 0.08f, 0.11f, 0.98f);
                Debug.LogWarning("[ChampionCamp] 缺少 ChampionCampHubBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            var zones = CampUiRuntime.CreateRect("HubHotZones", _hubPanel);
            CampUiRuntime.StretchFull(zones.GetComponent<RectTransform>());

            CreateHubActionHotZone(
                zones.transform,
                "ConfigureTeam",
                HubZoneTeam,
                _uiIcons != null ? _uiIcons.ChampionCampButton1 : null,
                ShowTeamPanel);
            CreateHubActionHotZone(
                zones.transform,
                "ManageCards",
                HubZoneCards,
                _uiIcons != null ? _uiIcons.ChampionCampButton2 : null,
                ShowCollectionPanel);

            // 返回按钮常显置顶
            var closeBar = CampUiRuntime.CreateRect("HubCloseHotZones", _hubPanel);
            CampUiRuntime.StretchFull(closeBar.GetComponent<RectTransform>());
            var closeCanvas = closeBar.AddComponent<Canvas>();
            closeCanvas.overrideSorting = true;
            closeCanvas.sortingOrder = 90;
            closeBar.AddComponent<GraphicRaycaster>();
            CreateHubBackButton(closeBar.transform, HubZoneClose, CloseToCamp);
        }

        void CreateHubActionHotZone(
            Transform parent,
            string id,
            HubNormRect zone,
            Sprite visualSprite,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, parent);
            var rt = go.GetComponent<RectTransform>();
            ApplyHubNormRect(rt, zone);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var visualGo = CampUiRuntime.CreateRect("Visual", go.transform);
            var visualRt = visualGo.GetComponent<RectTransform>();
            visualRt.anchorMin = Vector2.zero;
            visualRt.anchorMax = Vector2.one;
            visualRt.offsetMin = Vector2.zero;
            visualRt.offsetMax = Vector2.zero;
            visualRt.pivot = new Vector2(0.5f, 0.5f);

            var visualImg = visualGo.AddComponent<Image>();
            visualImg.color = Color.white;
            visualImg.raycastTarget = false;
            // 热区已按红框量过，直接铺满以精确盖住模板静态按钮
            visualImg.preserveAspect = false;
            if (visualSprite != null)
                visualImg.sprite = visualSprite;
            else
                Debug.LogWarning($"[ChampionCamp] 缺少军营一级按钮贴图：{id}");

            var group = visualGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(visualRt, group, HubButtonHoverScale);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        void CreateHubBackButton(Transform parent, HubNormRect zone, Action onClick)
        {
            var go = CampUiRuntime.CreateRect("Back", parent);
            var rt = go.GetComponent<RectTransform>();
            ApplyHubNormRect(rt, zone);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton3 != null)
                img.sprite = _uiIcons.UiButton3;
            else
                img.color = new Color(0.45f, 0.12f, 0.12f, 0.95f);

            var label = CampUiRuntime.CreateText(go.transform, "返回", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.color = new Color(0.95f, 0.88f, 0.62f, 1f);
            label.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, HubButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        Button CreateTeamActionButton(
            Transform parent,
            string id,
            HubNormRect zone,
            Sprite sprite,
            string label,
            int fontSize,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, parent);
            var rt = go.GetComponent<RectTransform>();
            ApplyHubNormRect(rt, zone);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (sprite != null)
                img.sprite = sprite;
            else
                img.color = new Color(0.35f, 0.28f, 0.18f, 0.95f);

            var text = CampUiRuntime.CreateText(go.transform, label, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(text.rectTransform);
            text.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            text.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;

            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, HubButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        static void ApplyHubNormRect(RectTransform rt, HubNormRect zone)
        {
            rt.anchorMin = new Vector2(zone.XMin, zone.YMin);
            rt.anchorMax = new Vector2(zone.XMax, zone.YMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        void SaveAndReturnToHub()
        {
            EnsureRosterSize();
            CampRosterLoadoutRules.SanitizeRoster(_roster, _collection, CardOwnerLookup);
            SanitizeDuplicateCharacters();
            _onRosterChanged?.Invoke(_roster);
            ShowHub();
        }

        void SanitizeDuplicateCharacters()
        {
            if (_roster?.Members == null)
                return;

            var seen = new HashSet<string>();
            foreach (var member in _roster.Members)
            {
                if (member == null || string.IsNullOrEmpty(member.CharacterDefinitionId))
                    continue;

                if (seen.Add(member.CharacterDefinitionId))
                    continue;

                member.CharacterDefinitionId = "";
                member.DisplayName = "";
                CampRosterLoadoutRules.EnsureDeckStructure(member);
                for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
                    CampRosterLoadoutRules.ClearSlot(member, slot);
            }
        }

        void Rebuild()
        {
            _tooltip?.Hide();
            var poolScrollY = ScrollRectNavigation.CaptureVertical(_poolScroll);
            ClearDynamic();
            RefreshMetaSummary();
            if (_roster == null)
                return;

            EnsureRosterSize();
            RebuildMemberRow();
            RebuildDeckSlots();
            RebuildCardPool(poolScrollY);
            UpdateHint();
        }

        void EnsureRosterSize()
        {
            while (_roster.Members.Count < CampRosterState.PartySize)
            {
                var empty = new CampMemberLoadout();
                CampRosterLoadoutRules.EnsureDeckStructure(empty);
                _roster.Members.Add(empty);
            }

            while (_roster.Members.Count > CampRosterState.PartySize)
                _roster.Members.RemoveAt(_roster.Members.Count - 1);

            foreach (var member in _roster.Members)
                CampRosterLoadoutRules.EnsureDeckStructure(member);
        }

        void RebuildMemberRow()
        {
            HubNormRect[] zones =
            {
                TeamZoneMemberBack,
                TeamZoneMemberMid,
                TeamZoneMemberFront
            };
            HubNormRect[] portraits =
            {
                TeamPortraitBack,
                TeamPortraitMid,
                TeamPortraitFront
            };
            HubNormRect[] swaps =
            {
                TeamSwapBack,
                TeamSwapMid,
                TeamSwapFront
            };
            HubNormRect[] names =
            {
                TeamNameBack,
                TeamNameMid,
                TeamNameFront
            };
            HubNormRect[] levels =
            {
                TeamLevelBack,
                TeamLevelMid,
                TeamLevelFront
            };
            HubNormRect[] xpTexts =
            {
                TeamXpTextBack,
                TeamXpTextMid,
                TeamXpTextFront
            };
            HubNormRect[] xpBars =
            {
                TeamXpBarBack,
                TeamXpBarMid,
                TeamXpBarFront
            };

            for (var vi = 0; vi < CampRosterState.PartySize; vi++)
            {
                var index = CampFormationDisplay.VisualOrderMemberIndices[vi];
                var member = _roster.Members[index];
                var card = CreateMemberCard(
                    _memberRow,
                    member,
                    index,
                    index == _activeMemberIndex,
                    zones[vi],
                    portraits[vi],
                    swaps[vi],
                    names[vi],
                    levels[vi],
                    xpTexts[vi],
                    xpBars[vi]);
                card.GetComponent<Button>().onClick.AddListener(() =>
                {
                    _activeMemberIndex = index;
                    _selectedDeckSlot = 0;
                    Rebuild();
                });
            }
        }

        GameObject CreateMemberCard(
            Transform parent,
            CampMemberLoadout member,
            int memberIndex,
            bool active,
            HubNormRect zone,
            HubNormRect portraitZone,
            HubNormRect swapZone,
            HubNormRect nameZone,
            HubNormRect levelZone,
            HubNormRect xpTextZone,
            HubNormRect xpBarZone)
        {
            var go = CampUiRuntime.CreateRect($"Member{memberIndex}", parent);
            var rt = go.GetComponent<RectTransform>();
            ApplyHubNormRect(rt, zone);

            var hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.001f);
            hit.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            UiAudioHooks.WireButton(btn);

            // 动画立绘：保比例，略缩小放入红框
            var portraitHost = CampUiRuntime.CreateRect("PortraitHost", go.transform);
            ApplyLocalNormRect(portraitHost.GetComponent<RectTransform>(), zone, InsetNormRect(portraitZone, TeamPortraitInset));

            var portrait = CampUiRuntime.CreateImage("Portrait", portraitHost.transform, Color.white);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            CampUiRuntime.StretchFull(portrait.rectTransform);
            var animator = portraitHost.AddComponent<CampIdlePortraitAnimator>();
            animator.Bind(portrait, _characterVisuals, member.CharacterDefinitionId);

            if (active)
            {
                var outline = portrait.gameObject.AddComponent<Outline>();
                outline.effectColor = TeamSelectedOutline;
                outline.effectDistance = new Vector2(3f, 3f);
                outline.useGraphicAlpha = true;
            }

            var name = CampUiRuntime.CreateText(go.transform,
                string.IsNullOrEmpty(member.DisplayName) ? "未选择" : member.DisplayName,
                18, FontStyle.Bold, TextAnchor.MiddleCenter);
            ApplyLocalNormRect(name.rectTransform, zone, nameZone);
            name.color = Color.white;
            name.raycastTarget = false;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;

            var levelLine = CampUiRuntime.CreateText(go.transform, "Lv.1", 14, FontStyle.Bold, TextAnchor.MiddleCenter);
            ApplyLocalNormRect(levelLine.rectTransform, zone, levelZone);
            levelLine.color = new Color(0.92f, 0.88f, 0.72f, 1f);
            levelLine.raycastTarget = false;

            var xpLine = CampUiRuntime.CreateText(go.transform, "0/100 XP", 12, FontStyle.Normal, TextAnchor.MiddleCenter);
            ApplyLocalNormRect(xpLine.rectTransform, zone, xpTextZone);
            xpLine.color = new Color(0.78f, 0.82f, 0.90f, 1f);
            xpLine.raycastTarget = false;

            // 经验条：仅绿色填充叠在模板细条（紫框）上，不再画大黑底
            var xpFillHost = CampUiRuntime.CreateRect("XpBar", go.transform);
            ApplyLocalNormRect(xpFillHost.GetComponent<RectTransform>(), zone, xpBarZone);

            var xpFill = CampUiRuntime.CreateImage("XpFill", xpFillHost.transform, TeamXpBarFill);
            xpFill.raycastTarget = false;
            var xpFillRt = xpFill.rectTransform;
            xpFillRt.anchorMin = Vector2.zero;
            xpFillRt.anchorMax = new Vector2(0f, 1f);
            xpFillRt.offsetMin = Vector2.zero;
            xpFillRt.offsetMax = Vector2.zero;
            xpFillRt.pivot = new Vector2(0f, 0.5f);

            float ratio = 0f;
            if (_meta != null && !string.IsNullOrEmpty(member.CharacterDefinitionId))
            {
                var progress = _meta.GetOrCreate(member.CharacterDefinitionId);
                MetaProgressionRules.NormalizeProgress(progress);
                levelLine.text = $"Lv.{progress.OutOfRunLevel}";
                xpLine.text = MetaProgressionRules.FormatXpProgress(progress);

                if (MetaProgressionRules.IsMaxLevel(progress))
                    ratio = 1f;
                else
                {
                    var need = MetaProgressionRules.XpRequiredForNextLevel(progress);
                    ratio = need > 0 ? Mathf.Clamp01(progress.OutOfRunXp / (float)need) : 0f;
                }
            }
            else if (string.IsNullOrEmpty(member.CharacterDefinitionId))
            {
                levelLine.text = "";
                xpLine.text = "";
            }

            xpFillRt.anchorMax = new Vector2(ratio, 1f);

            var swapGo = CampUiRuntime.CreateRect("Swap", go.transform);
            ApplyLocalNormRect(swapGo.GetComponent<RectTransform>(), zone, swapZone);

            var swapImg = swapGo.AddComponent<Image>();
            swapImg.color = Color.white;
            swapImg.raycastTarget = true;
            swapImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton1 != null)
                swapImg.sprite = _uiIcons.UiButton1;
            else
                swapImg.color = new Color(0.45f, 0.28f, 0.12f, 0.95f);

            var swapLabel = CampUiRuntime.CreateText(swapGo.transform, "换人", 12, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(swapLabel.rectTransform);
            swapLabel.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            swapLabel.raycastTarget = false;

            var swapGroup = swapGo.AddComponent<CanvasGroup>();
            swapGroup.alpha = 1f;
            swapGroup.blocksRaycasts = true;
            swapGroup.interactable = true;

            var swapHover = swapGo.AddComponent<CampBuildingHoverView>();
            swapHover.Bind(swapGo.GetComponent<RectTransform>(), swapGroup, HubButtonHoverScale, hideWhenIdle: false);

            var swapBtn = swapGo.AddComponent<Button>();
            swapBtn.targetGraphic = swapImg;
            swapBtn.transition = Selectable.Transition.None;
            swapBtn.onClick.AddListener(() => ShowCharacterPicker(memberIndex));
            UiAudioHooks.WireButton(swapBtn);

            _dynamicObjects.Add(go);
            return go;
        }

        static HubNormRect InsetNormRect(HubNormRect zone, float inset01)
        {
            var w = zone.XMax - zone.XMin;
            var h = zone.YMax - zone.YMin;
            var ix = w * inset01;
            var iy = h * inset01;
            return new HubNormRect(zone.XMin + ix, zone.YMin + iy, zone.XMax - ix, zone.YMax - iy);
        }

        static void ApplyLocalNormRect(RectTransform child, HubNormRect parentZone, HubNormRect absoluteZone)
        {
            var pw = parentZone.XMax - parentZone.XMin;
            var ph = parentZone.YMax - parentZone.YMin;
            if (pw < 0.0001f || ph < 0.0001f)
            {
                child.anchorMin = Vector2.zero;
                child.anchorMax = Vector2.one;
                child.offsetMin = Vector2.zero;
                child.offsetMax = Vector2.zero;
                return;
            }

            child.anchorMin = new Vector2(
                (absoluteZone.XMin - parentZone.XMin) / pw,
                (absoluteZone.YMin - parentZone.YMin) / ph);
            child.anchorMax = new Vector2(
                (absoluteZone.XMax - parentZone.XMin) / pw,
                (absoluteZone.YMax - parentZone.YMin) / ph);
            child.offsetMin = Vector2.zero;
            child.offsetMax = Vector2.zero;
            child.pivot = new Vector2(0.5f, 0.5f);
        }

        void ShowCharacterPicker(int memberIndex)
        {
            _activeMemberIndex = memberIndex;
            var picker = CampUiRuntime.CreateImage("CharacterPicker", _body, new Color(0.05f, 0.06f, 0.09f, 0.98f))
                .gameObject;
            var rt = picker.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 360f);
            picker.transform.SetAsLastSibling();
            _dynamicObjects.Add(picker);

            var title = CampUiRuntime.CreateText(picker.transform, "选择角色", 22, FontStyle.Bold);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(16f, -48f);
            title.rectTransform.offsetMax = new Vector2(-16f, -8f);

            var row = CampUiRuntime.CreateRect("PickRow", picker.transform);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.15f);
            rowRt.anchorMax = new Vector2(1f, 0.85f);
            rowRt.offsetMin = new Vector2(16f, 0f);
            rowRt.offsetMax = new Vector2(-16f, 0f);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;

            foreach (var character in _playableCharacters)
            {
                var pickBtn = CampUiRuntime.CreateButton(row.transform, "",
                    new Color(0.2f, 0.32f, 0.48f, 1f), new Vector2(140f, 180f));
                var pickRt = pickBtn.GetComponent<RectTransform>();
                pickRt.sizeDelta = new Vector2(140f, 180f);

                var portrait = CampUiRuntime.CreateImage("Portrait", pickBtn.transform, Color.white);
                portrait.sprite = _characterVisuals?.GetPortrait(character.CharacterId);
                portrait.preserveAspect = true;
                var pRt = portrait.rectTransform;
                pRt.anchorMin = new Vector2(0.5f, 1f);
                pRt.anchorMax = new Vector2(0.5f, 1f);
                pRt.pivot = new Vector2(0.5f, 1f);
                pRt.anchoredPosition = new Vector2(0f, -12f);
                pRt.sizeDelta = new Vector2(96f, 96f);

                var nameLabel = CampUiRuntime.CreateText(pickBtn.transform,
                    CampRosterValidation.FindMemberIndexWithCharacter(_roster, character.CharacterId, memberIndex) >= 0
                        ? $"{character.DisplayName}\n(互换)"
                        : character.DisplayName,
                    16,
                    FontStyle.Bold, TextAnchor.UpperCenter);
                var nameRt = nameLabel.rectTransform;
                nameRt.anchorMin = new Vector2(0f, 0f);
                nameRt.anchorMax = new Vector2(1f, 0f);
                nameRt.offsetMin = new Vector2(4f, 28f);
                nameRt.offsetMax = new Vector2(-4f, 56f);

                if (_meta != null)
                {
                    var progress = _meta.GetOrCreate(character.CharacterId);
                    var metaLabel = CampUiRuntime.CreateText(pickBtn.transform,
                        MetaProgressionRules.FormatXpProgress(progress),
                        13, FontStyle.Normal, TextAnchor.UpperCenter);
                    var metaRt = metaLabel.rectTransform;
                    metaRt.anchorMin = new Vector2(0f, 0f);
                    metaRt.anchorMax = new Vector2(1f, 0f);
                    metaRt.offsetMin = new Vector2(4f, 8f);
                    metaRt.offsetMax = new Vector2(-4f, 26f);
                    metaLabel.color = new Color(0.75f, 0.8f, 0.92f, 1f);
                }

                var captured = character;
                pickBtn.onClick.AddListener(() =>
                {
                    ApplyCharacter(memberIndex, captured);
                    Destroy(picker);
                    Rebuild();
                });
            }

            var cancel = CampUiRuntime.CreateButton(picker.transform, "取消", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(100f, 36f));
            var cancelRt = cancel.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(1f, 0f);
            cancelRt.anchorMax = new Vector2(1f, 0f);
            cancelRt.pivot = new Vector2(1f, 0f);
            cancelRt.anchoredPosition = new Vector2(-12f, 12f);
            cancel.onClick.AddListener(() => Destroy(picker));
        }

        void ApplyCharacter(int memberIndex, CharacterDefinitionSO character)
        {
            var duplicateIndex = CampRosterValidation.FindMemberIndexWithCharacter(
                _roster, character.CharacterId, memberIndex);
            if (duplicateIndex >= 0)
                CampRosterValidation.SwapMembers(_roster, memberIndex, duplicateIndex);

            var member = _roster.Members[memberIndex];
            member.CharacterDefinitionId = character.CharacterId;
            member.DisplayName = CharacterDisplayNames.GetOrFallback(character.CharacterId, character.DisplayName);

            for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
                CampRosterLoadoutRules.ClearSlot(member, slot);
        }

        void RebuildDeckSlots()
        {
            var member = _roster.Members[_activeMemberIndex];
            for (var slot = 0; slot < CampRosterState.DeckSize; slot++)
            {
                var slotIndex = slot;
                var cardId = member.DeckCardIds[slot];
                var slotGo = CampUiRuntime.CreateImage($"DeckSlot{slot}", _deckGrid,
                    slotIndex == _selectedDeckSlot
                        ? new Color(0.35f, 0.55f, 0.90f, 0.22f)
                        : new Color(0.08f, 0.09f, 0.12f, 0.08f)).gameObject;
                ApplyHubNormRect(slotGo.GetComponent<RectTransform>(), GetTeamDeckSlotRect(slot));

                var btn = slotGo.AddComponent<Button>();
                btn.targetGraphic = slotGo.GetComponent<Image>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    if (_selectedDeckSlot == slotIndex && !string.IsNullOrEmpty(member.DeckCardIds[slotIndex]))
                        CampRosterLoadoutRules.ClearSlot(member, slotIndex);
                    else
                        _selectedDeckSlot = slotIndex;
                    Rebuild();
                });
                UiAudioHooks.WireButton(btn);

                if (!string.IsNullOrEmpty(cardId) && _cardPrefab != null)
                {
                    _definitions.TryGetValue(cardId, out var definition);
                    var view = Instantiate(_cardPrefab, slotGo.transform);
                    CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                    var preview = CardVisualResolver.CreatePreviewInstance(
                        cardId,
                        member.CharacterDefinitionId,
                        definition?.DisplayName ?? cardId,
                        definition);
                    var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                    var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                    view.BindWithCard(preview, visual, false, false, false, "", statsLine,
                        _uiIcons, _characterVisuals, null, null, null);
                    var cg = view.GetComponent<CanvasGroup>();
                    if (cg != null)
                        cg.blocksRaycasts = false;
                    BindCardTooltip(slotGo, preview);
                }

                _dynamicObjects.Add(slotGo);
            }
        }

        static HubNormRect GetTeamDeckSlotRect(int slot)
        {
            var col = slot % 5;
            var row = slot / 5;
            var xMin = TeamDeckCardX0 + col * TeamDeckCardStep;
            var xMax = xMin + TeamDeckCardW;
            var yMin = row == 0 ? TeamDeckRow1YMin : TeamDeckRow2YMin;
            var yMax = row == 0 ? TeamDeckRow1YMax : TeamDeckRow2YMax;
            return new HubNormRect(xMin, yMin, xMax, yMax);
        }

        void RebuildCardPool(float scrollY = 1f)
        {
            var member = _roster.Members[_activeMemberIndex];
            if (_collection == null || _collection.Count == 0)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_poolGrid);
                ScrollRectNavigation.RestoreVertical(_poolScroll, scrollY);
                UpdateHint();
                return;
            }

            var assigned = CampRosterLoadoutRules.CollectAssignedCollectionIndices(_roster);
            var poolEntries = new List<(int EntryIndex, string CardId, CardDefinitionSO Card)>();
            for (var entryIndex = 0; entryIndex < _collection.Count; entryIndex++)
            {
                if (assigned.Contains(entryIndex))
                    continue;

                var cardId = _collection.Entries[entryIndex];
                if (string.IsNullOrEmpty(cardId))
                    continue;

                if (!_definitions.TryGetValue(cardId, out var card))
                    continue;

                if (!CampRosterBuilder.IsCardOwnedByCharacter(card, member.CharacterDefinitionId))
                    continue;

                poolEntries.Add((entryIndex, cardId, card));
            }

            poolEntries.Sort((a, b) =>
            {
                var rarityCmp = a.Card.Rarity.CompareTo(b.Card.Rarity);
                if (rarityCmp != 0)
                    return rarityCmp;
                var nameCmp = string.CompareOrdinal(a.Card.DisplayName, b.Card.DisplayName);
                return nameCmp != 0 ? nameCmp : a.EntryIndex.CompareTo(b.EntryIndex);
            });

            foreach (var entry in poolEntries)
            {
                var entryIndex = entry.EntryIndex;
                var cardId = entry.CardId;
                var card = entry.Card;

                var holder = CampUiRuntime.CreateRect($"{cardId}_{entryIndex}", _poolGrid);
                var holderRt = holder.GetComponent<RectTransform>();
                holderRt.sizeDelta = new Vector2(168f * CardScale + 8f, 236f * CardScale + 8f);

                if (_cardPrefab == null)
                    continue;

                var view = Instantiate(_cardPrefab, holder.transform);
                CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                var preview = CardVisualResolver.CreatePreviewInstance(
                    cardId,
                    member.CharacterDefinitionId,
                    card.DisplayName,
                    card);
                var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
                var capturedIndex = entryIndex;
                view.BindWithCard(preview, visual, false, false, true, "", statsLine,
                    _uiIcons, _characterVisuals,
                    _ => AssignCollectionEntryToSelectedSlot(capturedIndex),
                    null,
                    null);
                BindCardTooltip(holder, preview);
                ScrollRectNavigation.WireForwarding(holder, _poolScroll);

                _dynamicObjects.Add(holder);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_poolGrid);
            ScrollRectNavigation.RestoreVertical(_poolScroll, scrollY);
            UpdateHint();
        }

        void BindCardTooltip(GameObject target, CardInstanceState card)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(null, descCard, _definitions)
                .Replace("<b>", "").Replace("</b>", "");
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            _tooltip.BindHover(target, card.DisplayName, body, showTitle: false);
        }

        void AssignCollectionEntryToSelectedSlot(int collectionEntryIndex)
        {
            if (_selectedDeckSlot < 0 || _selectedDeckSlot >= CampRosterState.DeckSize)
                return;

            if (!CampRosterLoadoutRules.TryAssignCollectionEntry(
                    _roster,
                    _collection,
                    CardOwnerLookup,
                    _activeMemberIndex,
                    _selectedDeckSlot,
                    collectionEntryIndex,
                    out _))
                return;

            if (_selectedDeckSlot < CampRosterState.DeckSize - 1)
                _selectedDeckSlot++;
            Rebuild();
        }

        static int CountFilledSlots(CampMemberLoadout member)
        {
            var count = 0;
            foreach (var id in member.DeckCardIds)
            {
                if (!string.IsNullOrEmpty(id))
                    count++;
            }

            return count;
        }

        void UpdateHint()
        {
            if (_hintText == null || _roster == null)
                return;

            if (!CampRosterValidation.HasUniqueCharacters(_roster))
            {
                _hintText.text = "编队中存在重复角色，请为每个槽位选择不同角色。";
                return;
            }

            var member = _roster.Members[_activeMemberIndex];
            var ready = _roster.IsReadyForExpedition;
            var assigned = CampRosterLoadoutRules.CollectAssignedCollectionIndices(_roster);
            var availableForMember = 0;
            if (_collection != null)
            {
                for (var i = 0; i < _collection.Count; i++)
                {
                    if (assigned.Contains(i))
                        continue;

                    var cardId = _collection.Entries[i];
                    if (!_definitions.TryGetValue(cardId, out var definition))
                        continue;

                    if (CampRosterBuilder.IsCardOwnedByCharacter(definition, member.CharacterDefinitionId))
                        availableForMember++;
                }
            }

            if (availableForMember == 0 && CountFilledSlots(member) == 0)
            {
                _hintText.text =
                    $"正在编辑：{member.DisplayName} — 左侧祭坛携带为空；请先在局外商店获得该角色卡牌，或从右侧收藏选取填入。";
                return;
            }

            _hintText.text = ready
                ? "编队已就绪（远征仍使用角色初始卡组；左侧为空表示祭坛暂无可提取收藏牌）。保存后可通过传送门开始远征。"
                : $"正在编辑：{member.DisplayName} — 选中槽位 {_selectedDeckSlot + 1}，点击右侧收藏卡牌填入祭坛携带；再次点击已填槽位可归还收藏。";
        }

        void RefreshMetaSummary()
        {
            if (_accountGoldAmountText != null)
                _accountGoldAmountText.text = _accountGold.ToString();

            if (_campGoldIcon != null && _uiIcons?.CampGoldIcon != null)
                _campGoldIcon.sprite = _uiIcons.CampGoldIcon;

            if (_metaSummaryText == null)
                return;

            if (_meta == null || _playableCharacters.Count == 0)
            {
                _metaSummaryText.text = "";
                return;
            }

            var levelParts = new List<string>();
            foreach (var character in _playableCharacters)
            {
                if (character == null || string.IsNullOrEmpty(character.CharacterId))
                    continue;

                var progress = _meta.GetOrCreate(character.CharacterId);
                var name = string.IsNullOrEmpty(character.DisplayName) ? character.CharacterId : character.DisplayName;
                levelParts.Add($"{name} {MetaProgressionRules.FormatLevelProgress(progress)}");
            }

            _metaSummaryText.text = levelParts.Count > 0
                ? string.Join("  ·  ", levelParts)
                : "";
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

        Dictionary<string, string> CardOwnerLookup => CampRosterBuilder.BuildCardOwnerLookup(_definitions);
    }
}
