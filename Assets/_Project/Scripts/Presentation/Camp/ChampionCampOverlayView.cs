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

        // 配置队伍二级：保存/返回略左移并加宽，完全盖住模板底图按钮
        static readonly HubNormRect TeamZoneSave = new(0.7350f, 0.9000f, 0.8580f, 0.9920f);
        static readonly HubNormRect TeamZoneBack = new(0.8620f, 0.8950f, 0.9820f, 0.9920f);

        // 三角色整框（立绘红框 + 右侧名字/经验区）
        static readonly HubNormRect TeamZoneMemberBack = new(0.1650f, 0.6100f, 0.3850f, 0.8600f);
        static readonly HubNormRect TeamZoneMemberMid = new(0.3880f, 0.6100f, 0.6080f, 0.8620f);
        static readonly HubNormRect TeamZoneMemberFront = new(0.6100f, 0.6100f, 0.9000f, 0.8650f);

        // 立绘框：整体上移，减少被换人按钮遮挡，仍不越出模板框
        static readonly HubNormRect TeamPortraitBack = new(0.1670f, 0.6220f, 0.2793f, 0.8620f);
        static readonly HubNormRect TeamPortraitMid = new(0.3926f, 0.6250f, 0.5049f, 0.8650f);
        static readonly HubNormRect TeamPortraitFront = new(0.6152f, 0.6220f, 0.7344f, 0.8680f);

        // 换人：与立绘框水平居中，三槽统一高度
        static readonly HubNormRect TeamSwapBack = new(0.1870f, 0.6180f, 0.2593f, 0.6739f);
        static readonly HubNormRect TeamSwapMid = new(0.4126f, 0.6180f, 0.4849f, 0.6739f);
        static readonly HubNormRect TeamSwapFront = new(0.6387f, 0.6180f, 0.7110f, 0.6739f);

        // 蓝/绿/粉框：左缘与「后排/中排/前排」标签左缘对齐
        static readonly HubNormRect TeamNameBack = new(0.2885f, 0.7939f, 0.3600f, 0.8517f);
        static readonly HubNormRect TeamNameMid = new(0.5123f, 0.7939f, 0.5838f, 0.8517f);
        static readonly HubNormRect TeamNameFront = new(0.7389f, 0.7939f, 0.8104f, 0.8517f);
        static readonly HubNormRect TeamLevelBack = new(0.2885f, 0.7107f, 0.3500f, 0.7414f);
        static readonly HubNormRect TeamLevelMid = new(0.5123f, 0.7107f, 0.5738f, 0.7414f);
        static readonly HubNormRect TeamLevelFront = new(0.7389f, 0.7107f, 0.8004f, 0.7414f);
        static readonly HubNormRect TeamXpTextBack = new(0.2885f, 0.6700f, 0.3650f, 0.7050f);
        static readonly HubNormRect TeamXpTextMid = new(0.5123f, 0.6700f, 0.5888f, 0.7050f);
        static readonly HubNormRect TeamXpTextFront = new(0.7389f, 0.6700f, 0.8154f, 0.7050f);

        // 黄框：精确落入模板经验槽内（相对标注整体上移）
        static readonly HubNormRect TeamXpBarBack = new(0.2900f, 0.6550f, 0.3652f, 0.6690f);
        static readonly HubNormRect TeamXpBarMid = new(0.5138f, 0.6550f, 0.5891f, 0.6690f);
        static readonly HubNormRect TeamXpBarFront = new(0.7404f, 0.6550f, 0.8158f, 0.6690f);

        const float TeamPortraitInset = 0.05f;

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
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        /// <summary>ESC 逐级返回：收藏详情/确认 → 收藏列表 → 编队 → 枢纽 → 关闭回营地。</summary>
        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;

            if (_collectionManageView != null && _collectionManageView.IsOpen)
            {
                if (_collectionManageView.TryHandleEscape())
                    return true;

                ShowHub();
                return true;
            }

            if (_body != null && _body.gameObject.activeSelf)
            {
                ShowHub();
                return true;
            }

            CloseToCamp();
            return true;
        }

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
            _tooltip.Initialize(_overlayRoot, _uiIcons);

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

            var isEmpty = string.IsNullOrEmpty(member.CharacterDefinitionId);

            // 未配置：立绘区留空，只保留模板底框
            if (!isEmpty)
            {
                var portraitHost = CampUiRuntime.CreateRect("PortraitHost", go.transform);
                ApplyLocalNormRect(
                    portraitHost.GetComponent<RectTransform>(),
                    zone,
                    InsetNormRect(portraitZone, TeamPortraitInset));

                var portrait = CampUiRuntime.CreateImage("Portrait", portraitHost.transform, Color.white);
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
                var portraitRt = portrait.rectTransform;
                portraitRt.anchorMin = new Vector2(0f, 0.08f);
                portraitRt.anchorMax = new Vector2(1f, 1f);
                portraitRt.offsetMin = Vector2.zero;
                portraitRt.offsetMax = Vector2.zero;
                portraitRt.pivot = new Vector2(0.5f, 1f);

                var animator = portraitHost.AddComponent<CampIdlePortraitAnimator>();
                // 仅当前选中槽播放 idle；其余只显示静态立绘
                animator.Bind(portrait, _characterVisuals, member.CharacterDefinitionId, animate: active);

                if (active)
                {
                    var outline = portrait.gameObject.AddComponent<Outline>();
                    outline.effectColor = TeamSelectedOutline;
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.useGraphicAlpha = true;
                }
            }

            var name = CampUiRuntime.CreateText(go.transform,
                isEmpty
                    ? "未选择"
                    : (string.IsNullOrEmpty(member.DisplayName) ? "未选择" : member.DisplayName),
                22, FontStyle.Bold, TextAnchor.MiddleLeft);
            ApplyLocalNormRect(name.rectTransform, zone, nameZone);
            name.color = Color.white;
            name.raycastTarget = false;
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            name.verticalOverflow = VerticalWrapMode.Truncate;

            var levelLine = CampUiRuntime.CreateText(go.transform, "Lv.1", 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            ApplyLocalNormRect(levelLine.rectTransform, zone, levelZone);
            levelLine.color = new Color(0.92f, 0.88f, 0.72f, 1f);
            levelLine.raycastTarget = false;

            var xpLine = CampUiRuntime.CreateText(go.transform, "0/100 XP", 13, FontStyle.Normal, TextAnchor.MiddleLeft);
            ApplyLocalNormRect(xpLine.rectTransform, zone, xpTextZone);
            xpLine.color = new Color(0.78f, 0.82f, 0.90f, 1f);
            xpLine.raycastTarget = false;

            // 经验条：仅绿色填充精确叠在黄框/模板细槽内
            var xpFillHost = CampUiRuntime.CreateRect("XpBar", go.transform);
            ApplyLocalNormRect(xpFillHost.GetComponent<RectTransform>(), zone, xpBarZone);

            var xpFill = CampUiRuntime.CreateImage("XpFill", xpFillHost.transform, TeamXpBarFill);
            xpFill.raycastTarget = false;
            xpFill.type = Image.Type.Simple;
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

        // 角色选择弹窗：底板 sprite 裁切为 1316×978（非全图 1402×1122），坐标相对该裁切。
        // character_plate_0 = 162×288，与模板空槽模板匹配量取。
        const float PickDialogAspect = 978f / 1316f;
        const int PickColumns = 5;
        const int PickVisibleRows = 2;
        const float PickPlateW = 162f / 1316f;
        const float PickPlateH = 288f / 978f;
        const float PickPlateX0 = 112f / 1316f;
        const float PickPlateStepX = 230.5f / 1316f;
        const float PickPlateGapY = 43f / 978f;
        const float PickPlateTopRowYMin = (978f - 146f - 288f) / 978f;
        static readonly HubNormRect PickZoneViewport = new(
            PickPlateX0,
            PickPlateTopRowYMin - (PickPlateH + PickPlateGapY),
            PickPlateX0 + (PickColumns - 1) * PickPlateStepX + PickPlateW,
            PickPlateTopRowYMin + PickPlateH);
        // 盖住模板右侧装饰轨道，换上 UiSliderBar + UiSlider
        static readonly HubNormRect PickZoneScrollbar = new(0.9483f, 0.2055f, 0.9612f, 0.8446f);
        static readonly HubNormRect PickZoneCancel = new(0.390f, 0.100f, 0.610f, 0.170f);
        static readonly HubNormRect PickZoneClose = new(0.90f, 0.90f, 0.98f, 0.98f);

        void ShowCharacterPicker(int memberIndex)
        {
            _activeMemberIndex = memberIndex;

            var root = CampUiRuntime.CreateRect("CharacterPicker", _body);
            CampUiRuntime.StretchFull(root.GetComponent<RectTransform>());
            root.transform.SetAsLastSibling();
            _dynamicObjects.Add(root);

            var dim = CampUiRuntime.CreateImage("Dim", root.transform, new Color(0f, 0f, 0f, 0.55f));
            CampUiRuntime.StretchFull(dim.rectTransform);
            dim.raycastTarget = true;

            var dialog = CampUiRuntime.CreateImage("Dialog", root.transform, Color.white);
            var dialogRt = dialog.rectTransform;
            dialogRt.anchorMin = new Vector2(0.5f, 0.5f);
            dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            // 与底板 sprite 裁切同比例，避免空槽被非等比拉伸错位
            const float dialogW = 1000f;
            var dialogH = dialogW * PickDialogAspect;
            dialogRt.sizeDelta = new Vector2(dialogW, dialogH);
            dialog.preserveAspect = false;
            dialog.raycastTarget = true;
            if (_uiIcons != null && _uiIcons.ChampionCampCharacterSelectBackground != null)
                dialog.sprite = _uiIcons.ChampionCampCharacterSelectBackground;
            else
                dialog.color = new Color(0.07f, 0.08f, 0.1f, 0.98f);

            void ClosePicker()
            {
                if (root != null)
                    Destroy(root);
            }

            var closeGo = CampUiRuntime.CreateRect("Close", dialog.transform);
            ApplyHubNormRect(closeGo.GetComponent<RectTransform>(), PickZoneClose);
            var closeHit = closeGo.AddComponent<Image>();
            closeHit.color = new Color(1f, 1f, 1f, 0.001f);
            closeHit.raycastTarget = true;
            var closeBtn = closeGo.AddComponent<Button>();
            closeBtn.targetGraphic = closeHit;
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(ClosePicker);
            UiAudioHooks.WireButton(closeBtn);

            var characters = new List<CharacterDefinitionSO>();
            foreach (var character in _playableCharacters)
            {
                if (character != null)
                    characters.Add(character);
            }

            var cellW = PickPlateW * dialogW;
            var cellH = PickPlateH * dialogH;
            var spacingX = PickPlateStepX * dialogW - cellW;
            var spacingY = PickPlateGapY * dialogH;

            var scrollGo = CampUiRuntime.CreateRect("PlateScroll", dialog.transform);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            ApplyHubNormRect(scrollRt, PickZoneViewport);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.inertia = false;

            var viewport = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewport.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            var viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0.02f);
            viewportImg.raycastTarget = true;
            var mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = CampUiRuntime.CreateRect("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.offsetMin = Vector2.zero;
            contentRt.offsetMax = Vector2.zero;

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cellW, cellH);
            grid.spacing = new Vector2(spacingX, spacingY);
            grid.padding = new RectOffset(0, 0, 0, 0);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = PickColumns;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRt;
            scroll.content = contentRt;

            foreach (var character in characters)
            {
                var plate = CreateCharacterPickPlate(content.transform, character, memberIndex, root);
                var le = plate.AddComponent<LayoutElement>();
                le.minWidth = cellW;
                le.minHeight = cellH;
                le.preferredWidth = cellW;
                le.preferredHeight = cellH;
                ScrollRectNavigation.WireForwarding(plate, scroll);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);
            Canvas.ForceUpdateCanvases();
            // 内容不超过可视 2 行时 size=1，滑块铺满轨道、不可拖动
            var visibleH = PickVisibleRows * cellH + (PickVisibleRows - 1) * spacingY;
            var contentH = Mathf.Max(contentRt.rect.height, 1f);
            scroll.verticalNormalizedPosition = 1f;

            BuildCharacterPickerScrollbar(dialog.transform, scroll, contentH, visibleH);
            Canvas.ForceUpdateCanvases();

            // 取消：透明热区盖住模板按钮，不再叠字/叠图（避免双「取消」）
            var cancelGo = CampUiRuntime.CreateRect("Cancel", dialog.transform);
            ApplyHubNormRect(cancelGo.GetComponent<RectTransform>(), PickZoneCancel);
            var cancelHit = cancelGo.AddComponent<Image>();
            cancelHit.color = new Color(1f, 1f, 1f, 0.001f);
            cancelHit.raycastTarget = true;
            var cancelBtn = cancelGo.AddComponent<Button>();
            cancelBtn.targetGraphic = cancelHit;
            cancelBtn.transition = Selectable.Transition.None;
            cancelBtn.onClick.AddListener(ClosePicker);
            UiAudioHooks.WireButton(cancelBtn);
        }

        void BuildCharacterPickerScrollbar(
            Transform dialog,
            ScrollRect scroll,
            float contentHeight,
            float viewportHeight)
        {
            var barGo = CampUiRuntime.CreateRect("PickScrollbar", dialog);
            var barRt = barGo.GetComponent<RectTransform>();
            ApplyHubNormRect(barRt, PickZoneScrollbar);

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
            scrollbar.numberOfSteps = 0;
            var size = contentHeight <= viewportHeight + 0.5f
                ? 1f
                : Mathf.Clamp01(viewportHeight / contentHeight);
            scrollbar.size = size;
            scrollbar.value = 1f;

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 0f;
        }

        GameObject CreateCharacterPickPlate(
            Transform parent,
            CharacterDefinitionSO character,
            int memberIndex,
            GameObject pickerRoot)
        {
            var plate = CampUiRuntime.CreateRect(character.CharacterId, parent);

            var plateImg = plate.AddComponent<Image>();
            plateImg.color = Color.white;
            plateImg.raycastTarget = true;
            // 格子尺寸已按 plate 精灵比例设定；关闭 preserveAspect，铺满空槽
            plateImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiCharacterPlate != null)
                plateImg.sprite = _uiIcons.UiCharacterPlate;
            else
                plateImg.color = new Color(0.18f, 0.16f, 0.14f, 1f);

            // 立绘：贴合拱形内腔（相对 character_plate 量取）
            var portraitHost = CampUiRuntime.CreateRect("Portrait", plate.transform);
            var portraitHostRt = portraitHost.GetComponent<RectTransform>();
            portraitHostRt.anchorMin = new Vector2(0.10f, 0.30f);
            portraitHostRt.anchorMax = new Vector2(0.90f, 0.90f);
            portraitHostRt.offsetMin = Vector2.zero;
            portraitHostRt.offsetMax = Vector2.zero;

            var portrait = CampUiRuntime.CreateImage("Idle", portraitHost.transform, Color.white);
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            CampUiRuntime.StretchFull(portrait.rectTransform);
            var animator = portraitHost.AddComponent<CampIdlePortraitAnimator>();
            animator.Bind(portrait, _characterVisuals, character.CharacterId, animate: false);

            var isSwap = CampRosterValidation.FindMemberIndexWithCharacter(
                _roster, character.CharacterId, memberIndex) >= 0;
            var displayName = CharacterDisplayNames.GetOrFallback(character.CharacterId, character.DisplayName);
            if (isSwap)
                displayName += " (互换)";

            // 名/等级：落在底部铭牌盒内并水平居中
            var name = CampUiRuntime.CreateText(plate.transform, displayName, 14, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            var nameRt = name.rectTransform;
            nameRt.anchorMin = new Vector2(0.08f, 0.12f);
            nameRt.anchorMax = new Vector2(0.92f, 0.26f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            name.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            name.raycastTarget = false;
            name.horizontalOverflow = HorizontalWrapMode.Wrap;
            name.verticalOverflow = VerticalWrapMode.Truncate;

            var level = 1;
            if (_meta != null && !string.IsNullOrEmpty(character.CharacterId))
            {
                var progress = _meta.GetOrCreate(character.CharacterId);
                MetaProgressionRules.NormalizeProgress(progress);
                level = progress.OutOfRunLevel;
            }

            var levelText = CampUiRuntime.CreateText(plate.transform, $"Lv.{level}", 12, FontStyle.Normal,
                TextAnchor.MiddleCenter);
            var levelRt = levelText.rectTransform;
            levelRt.anchorMin = new Vector2(0.08f, 0.02f);
            levelRt.anchorMax = new Vector2(0.92f, 0.12f);
            levelRt.offsetMin = Vector2.zero;
            levelRt.offsetMax = Vector2.zero;
            levelText.color = new Color(0.78f, 0.82f, 0.90f, 1f);
            levelText.raycastTarget = false;

            var group = plate.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = plate.AddComponent<CampBuildingHoverView>();
            hover.Bind(plate.GetComponent<RectTransform>(), group, HubButtonHoverScale, hideWhenIdle: false);

            var btn = plate.AddComponent<Button>();
            btn.targetGraphic = plateImg;
            btn.transition = Selectable.Transition.None;
            var captured = character;
            btn.onClick.AddListener(() =>
            {
                ApplyCharacter(memberIndex, captured);
                if (pickerRoot != null)
                    Destroy(pickerRoot);
                Rebuild();
            });
            UiAudioHooks.WireButton(btn);
            return plate;
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
                // 空槽透明且不可点；有卡时由 CardView 响应单击卸下与悬停高亮（无蓝色选中框）
                var slotGo = CampUiRuntime.CreateImage($"DeckSlot{slot}", _deckGrid,
                    new Color(0f, 0f, 0f, 0f)).gameObject;
                var slotImg = slotGo.GetComponent<Image>();
                slotImg.raycastTarget = false;
                ApplyHubNormRect(slotGo.GetComponent<RectTransform>(), GetTeamDeckSlotRect(slot));

                if (!string.IsNullOrEmpty(cardId) && _cardPrefab != null)
                {
                    // 与右侧收藏使用相同宿主尺寸，保证卡牌视觉大小一致
                    var cardHost = CampUiRuntime.CreateRect("CardHost", slotGo.transform);
                    var cardHostRt = cardHost.GetComponent<RectTransform>();
                    cardHostRt.anchorMin = new Vector2(0.5f, 0.5f);
                    cardHostRt.anchorMax = new Vector2(0.5f, 0.5f);
                    cardHostRt.pivot = new Vector2(0.5f, 0.5f);
                    cardHostRt.sizeDelta = new Vector2(168f * CardScale + 8f, 236f * CardScale + 8f);

                    _definitions.TryGetValue(cardId, out var definition);
                    var view = Instantiate(_cardPrefab, cardHost.transform);
                    CardView.ApplyHandPresentationScaleCentered(view, CardScale);
                    var preview = CardVisualResolver.CreatePreviewInstance(
                        cardId,
                        member.CharacterDefinitionId,
                        definition?.DisplayName ?? cardId,
                        definition);
                    var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
                    var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
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
                        _ =>
                        {
                            CampRosterLoadoutRules.ClearSlot(member, slotIndex);
                            Rebuild();
                        },
                        null,
                        null);
                    BindCardTooltip(view.gameObject, preview);
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
                    _ => AssignCollectionEntryToFirstEmptySlot(capturedIndex),
                    null,
                    null);
                BindCardTooltip(view.gameObject, preview);
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

        void AssignCollectionEntryToFirstEmptySlot(int collectionEntryIndex)
        {
            var member = _roster.Members[_activeMemberIndex];
            var slot = FindFirstEmptyDeckSlot(member);
            if (slot < 0)
                return;

            if (!CampRosterLoadoutRules.TryAssignCollectionEntry(
                    _roster,
                    _collection,
                    CardOwnerLookup,
                    _activeMemberIndex,
                    slot,
                    collectionEntryIndex,
                    out _))
                return;

            Rebuild();
        }

        static int FindFirstEmptyDeckSlot(CampMemberLoadout member)
        {
            if (member?.DeckCardIds == null)
                return -1;

            for (var i = 0; i < CampRosterState.DeckSize; i++)
            {
                if (string.IsNullOrEmpty(member.DeckCardIds[i]))
                    return i;
            }

            return -1;
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
            if (_hintText == null)
                return;

            // 底部「正在编辑…」说明已移除，避免干扰编队界面
            _hintText.text = "";
            _hintText.gameObject.SetActive(false);
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
