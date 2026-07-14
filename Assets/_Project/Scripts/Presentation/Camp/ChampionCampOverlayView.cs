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
        const float CardScale = 0.68f;
        const int CardsPerRow = 5;
        const float DeckSlotWidth = 132f;
        const float DeckSlotHeight = 186f;

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

            _body = CampUiRuntime.CreateImage("Body", _overlayRoot, new Color(0.07f, 0.08f, 0.11f, 0.98f))
                .rectTransform;
            _body.anchorMin = new Vector2(0.04f, 0.05f);
            _body.anchorMax = new Vector2(0.96f, 0.93f);
            _body.offsetMin = Vector2.zero;
            _body.offsetMax = Vector2.zero;

            var header = CampUiRuntime.CreateRect("Header", _body);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.offsetMin = new Vector2(20f, -58f);
            headerRt.offsetMax = new Vector2(-20f, -8f);

            var title = CampUiRuntime.CreateText(header.transform, "配置队伍", 26, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            title.rectTransform.anchorMin = new Vector2(0f, 0f);
            title.rectTransform.anchorMax = new Vector2(0.42f, 1f);
            title.rectTransform.offsetMin = Vector2.zero;
            title.rectTransform.offsetMax = Vector2.zero;

            _metaSummaryText = CampUiRuntime.CreateText(header.transform, "", 15, FontStyle.Normal,
                TextAnchor.MiddleLeft);
            _metaSummaryText.rectTransform.anchorMin = new Vector2(0.52f, 0f);
            _metaSummaryText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _metaSummaryText.rectTransform.offsetMin = new Vector2(0f, 0f);
            _metaSummaryText.rectTransform.offsetMax = new Vector2(-280f, 0f);
            _metaSummaryText.color = new Color(0.88f, 0.84f, 0.68f, 1f);
            _metaSummaryText.supportRichText = false;

            var campGoldIconGo = CampUiRuntime.CreateImage("CampGoldIcon", header.transform, Color.white);
            _campGoldIcon = campGoldIconGo;
            _campGoldIcon.sprite = _uiIcons != null ? _uiIcons.CampGoldIcon : null;
            _campGoldIcon.preserveAspect = true;
            var campGoldIconRt = _campGoldIcon.rectTransform;
            campGoldIconRt.anchorMin = new Vector2(0.42f, 0.5f);
            campGoldIconRt.anchorMax = new Vector2(0.42f, 0.5f);
            campGoldIconRt.pivot = new Vector2(0f, 0.5f);
            campGoldIconRt.anchoredPosition = Vector2.zero;
            campGoldIconRt.sizeDelta = new Vector2(24f, 24f);

            _accountGoldAmountText = CampUiRuntime.CreateText(header.transform, "0", 15, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            _accountGoldAmountText.rectTransform.anchorMin = new Vector2(0.42f, 0f);
            _accountGoldAmountText.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            _accountGoldAmountText.rectTransform.offsetMin = new Vector2(28f, 0f);
            _accountGoldAmountText.rectTransform.offsetMax = Vector2.zero;
            _accountGoldAmountText.color = new Color(0.92f, 0.88f, 0.72f, 1f);

            _closeButton = CampUiRuntime.CreateButton(header.transform, "返回", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(120f, 40f));
            var closeRt = _closeButton.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 0.5f);
            closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(0f, 0f);
            _closeButton.onClick.AddListener(ShowHub);

            _confirmButton = CampUiRuntime.CreateButton(header.transform, "保存编队", new Color(0.55f, 0.42f, 0.15f, 1f),
                new Vector2(140f, 40f));
            var confirmRt = _confirmButton.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(1f, 0.5f);
            confirmRt.anchorMax = new Vector2(1f, 0.5f);
            confirmRt.pivot = new Vector2(1f, 0.5f);
            confirmRt.anchoredPosition = new Vector2(-132f, 0f);
            _confirmButton.onClick.AddListener(SaveAndReturnToHub);

            _memberRow = CampUiRuntime.CreateRect("MemberRow", _body).GetComponent<RectTransform>();
            _memberRow.anchorMin = new Vector2(0f, 1f);
            _memberRow.anchorMax = new Vector2(1f, 1f);
            _memberRow.offsetMin = new Vector2(20f, -200f);
            _memberRow.offsetMax = new Vector2(-20f, -68f);

            var deckColumn = CampUiRuntime.CreateRect("DeckColumn", _body);
            var deckColumnRt = deckColumn.GetComponent<RectTransform>();
            deckColumnRt.anchorMin = new Vector2(0f, 0f);
            deckColumnRt.anchorMax = new Vector2(0.48f, 1f);
            deckColumnRt.offsetMin = new Vector2(20f, 52f);
            deckColumnRt.offsetMax = new Vector2(-8f, -208f);

            var deckLabel = CampUiRuntime.CreateText(deckColumn.transform, "携带卡牌", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            deckLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            deckLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            deckLabel.rectTransform.offsetMin = new Vector2(0f, -32f);
            deckLabel.rectTransform.offsetMax = Vector2.zero;

            _deckGrid = CampUiRuntime.CreateRect("DeckGrid", deckColumn.transform).GetComponent<RectTransform>();
            _deckGrid.anchorMin = Vector2.zero;
            _deckGrid.anchorMax = Vector2.one;
            _deckGrid.offsetMin = Vector2.zero;
            _deckGrid.offsetMax = new Vector2(0f, -36f);
            var deckLayout = _deckGrid.gameObject.AddComponent<GridLayoutGroup>();
            deckLayout.cellSize = new Vector2(DeckSlotWidth, DeckSlotHeight);
            deckLayout.spacing = new Vector2(8f, 8f);
            deckLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            deckLayout.constraintCount = 5;
            deckLayout.childAlignment = TextAnchor.UpperLeft;

            var poolColumn = CampUiRuntime.CreateRect("PoolColumn", _body);
            var poolColumnRt = poolColumn.GetComponent<RectTransform>();
            poolColumnRt.anchorMin = new Vector2(0.52f, 0f);
            poolColumnRt.anchorMax = new Vector2(1f, 1f);
            poolColumnRt.offsetMin = new Vector2(0f, 52f);
            poolColumnRt.offsetMax = new Vector2(-20f, -208f);

            var poolLabel = CampUiRuntime.CreateText(poolColumn.transform, "军营收藏", 18, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            poolLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            poolLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            poolLabel.rectTransform.offsetMin = new Vector2(0f, -32f);
            poolLabel.rectTransform.offsetMax = Vector2.zero;

            var scrollGo = CampUiRuntime.CreateRect("PoolScroll", poolColumn.transform);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0f, -36f);
            scrollGo.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.14f, 0.65f);

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
            _poolGrid.offsetMin = new Vector2(4f, 0f);
            _poolGrid.offsetMax = new Vector2(-4f, 0f);
            var poolLayout = _poolGrid.gameObject.AddComponent<GridLayoutGroup>();
            poolLayout.cellSize = new Vector2(168f * CardScale + 8f, 236f * CardScale + 8f);
            poolLayout.spacing = new Vector2(6f, 6f);
            poolLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            poolLayout.constraintCount = CardsPerRow;
            poolLayout.childAlignment = TextAnchor.UpperLeft;
            var poolFitter = _poolGrid.gameObject.AddComponent<ContentSizeFitter>();
            poolFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            poolFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _poolScroll.content = _poolGrid;

            _hintText = CampUiRuntime.CreateText(_body, "", 15, FontStyle.Italic, TextAnchor.MiddleLeft);
            _hintText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _hintText.rectTransform.anchorMax = new Vector2(1f, 0f);
            _hintText.rectTransform.offsetMin = new Vector2(20f, 10f);
            _hintText.rectTransform.offsetMax = new Vector2(-20f, 42f);
            _hintText.color = new Color(0.75f, 0.78f, 0.85f, 1f);

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

        void BuildHubPanel()
        {
            _hubPanel = CampUiRuntime.CreateImage("HubPanel", _overlayRoot, new Color(0.07f, 0.08f, 0.11f, 0.98f))
                .rectTransform;
            CampUiRuntime.Stretch(_hubPanel, 48f, 48f, -48f, -48f);

            var title = CampUiRuntime.CreateText(_hubPanel, "军营", 32, FontStyle.Bold, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, -56f);
            title.rectTransform.offsetMax = new Vector2(0f, -8f);
            title.color = new Color(0.95f, 0.88f, 0.62f, 1f);

            var closeBtn = CampUiRuntime.CreateButton(_hubPanel, "返回营地", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 42f));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-8f, -8f);
            closeBtn.onClick.AddListener(CloseToCamp);

            var hint = CampUiRuntime.CreateText(_hubPanel,
                "管理出征队伍与永久收藏卡牌。局外商店获得的卡牌可在「管理卡牌」中查看。",
                16, FontStyle.Italic, TextAnchor.UpperCenter);
            hint.rectTransform.anchorMin = new Vector2(0f, 1f);
            hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.offsetMin = new Vector2(24f, -96f);
            hint.rectTransform.offsetMax = new Vector2(-24f, -60f);
            hint.color = new Color(0.72f, 0.76f, 0.84f, 1f);

            var buttonRow = CampUiRuntime.CreateRect("HubButtons", _hubPanel);
            var rowRt = buttonRow.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.08f, 0.18f);
            rowRt.anchorMax = new Vector2(0.92f, 0.78f);
            rowRt.offsetMin = Vector2.zero;
            rowRt.offsetMax = Vector2.zero;
            var rowLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 32f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = true;

            CreateHubActionButton(buttonRow.transform, "配置队伍",
                "编辑远征小队成员和每人10张可在祭坛提取的卡牌",
                new Color(0.18f, 0.28f, 0.42f, 0.98f),
                ShowTeamPanel);
            CreateHubActionButton(buttonRow.transform, "管理卡牌",
                "浏览收藏、筛选查看详情，或出售卡牌换取黄金",
                new Color(0.28f, 0.22f, 0.38f, 0.98f),
                ShowCollectionPanel);
        }

        static void CreateHubActionButton(
            Transform parent,
            string title,
            string subtitle,
            Color bg,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(title, parent);
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 280f;
            layout.preferredHeight = 320f;

            var bgImage = go.AddComponent<Image>();
            bgImage.color = bg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = bgImage;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);

            var titleText = CampUiRuntime.CreateText(go.transform, title, 28, FontStyle.Bold, TextAnchor.UpperCenter);
            CampUiRuntime.SetAnchored(titleText.rectTransform, 0.08f, 0.58f, 0.92f, 0.88f);
            titleText.color = new Color(0.95f, 0.9f, 0.72f, 1f);

            var subText = CampUiRuntime.CreateText(go.transform, subtitle, 16, FontStyle.Normal, TextAnchor.UpperCenter);
            CampUiRuntime.SetAnchored(subText.rectTransform, 0.1f, 0.18f, 0.9f, 0.52f);
            subText.color = new Color(0.78f, 0.82f, 0.92f, 1f);
            subText.horizontalOverflow = HorizontalWrapMode.Wrap;
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
            var layoutGo = CampUiRuntime.CreateRect("MemberLayout", _memberRow);
            CampUiRuntime.StretchFull(layoutGo.GetComponent<RectTransform>());
            var h = layoutGo.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            _dynamicObjects.Add(layoutGo);

            for (var vi = 0; vi < CampRosterState.PartySize; vi++)
            {
                var index = CampFormationDisplay.VisualOrderMemberIndices[vi];
                var member = _roster.Members[index];
                var card = CreateMemberCard(layoutGo.transform, member, index, index == _activeMemberIndex);
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
            bool active)
        {
            var go = CampUiRuntime.CreateImage("Member", parent,
                active ? new Color(0.35f, 0.48f, 0.72f, 0.95f) : new Color(0.16f, 0.18f, 0.24f, 0.95f)).gameObject;
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(320f, 140f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 320f;
            le.preferredHeight = 140f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            UiAudioHooks.WireButton(btn);

            var portrait = CampUiRuntime.CreateImage("Portrait", go.transform, Color.white);
            portrait.preserveAspect = true;
            portrait.sprite = _characterVisuals != null
                ? _characterVisuals.GetPortrait(member.CharacterDefinitionId)
                : null;
            var portraitRt = portrait.rectTransform;
            portraitRt.anchorMin = new Vector2(0f, 0.5f);
            portraitRt.anchorMax = new Vector2(0f, 0.5f);
            portraitRt.pivot = new Vector2(0f, 0.5f);
            portraitRt.sizeDelta = new Vector2(88f, 88f);
            portraitRt.anchoredPosition = new Vector2(12f, 0f);

            var slotLabel = CampUiRuntime.CreateText(go.transform, CampFormationDisplay.SlotLabel(memberIndex),
                13, FontStyle.Bold, TextAnchor.UpperLeft);
            slotLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            slotLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            slotLabel.rectTransform.offsetMin = new Vector2(108f, -52f);
            slotLabel.rectTransform.offsetMax = new Vector2(-8f, -36f);
            slotLabel.color = new Color(0.75f, 0.82f, 0.95f, 1f);

            var name = CampUiRuntime.CreateText(go.transform,
                string.IsNullOrEmpty(member.DisplayName) ? "未选择" : member.DisplayName,
                18, FontStyle.Bold, TextAnchor.UpperLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 1f);
            name.rectTransform.anchorMax = new Vector2(1f, 1f);
            name.rectTransform.offsetMin = new Vector2(108f, -36f);
            name.rectTransform.offsetMax = new Vector2(-8f, -8f);

            var filled = CountFilledSlots(member);
            var deckInfo = CampUiRuntime.CreateText(go.transform, $"祭坛携带 {filled}/{CampRosterState.DeckSize}",
                15, FontStyle.Normal, TextAnchor.UpperLeft);
            deckInfo.rectTransform.anchorMin = new Vector2(0f, 0f);
            deckInfo.rectTransform.anchorMax = new Vector2(1f, 0f);
            deckInfo.rectTransform.offsetMin = new Vector2(108f, 36f);
            deckInfo.rectTransform.offsetMax = new Vector2(-120f, 64f);
            deckInfo.color = filled == CampRosterState.DeckSize
                ? new Color(0.7f, 0.95f, 0.72f, 1f)
                : new Color(0.95f, 0.75f, 0.55f, 1f);

            if (_meta != null && !string.IsNullOrEmpty(member.CharacterDefinitionId))
            {
                var progress = _meta.GetOrCreate(member.CharacterDefinitionId);
                var metaLine = CampUiRuntime.CreateText(go.transform,
                    MetaProgressionRules.FormatXpProgress(progress),
                    14, FontStyle.Normal, TextAnchor.UpperLeft);
                metaLine.rectTransform.anchorMin = new Vector2(0f, 0f);
                metaLine.rectTransform.anchorMax = new Vector2(1f, 0f);
                metaLine.rectTransform.offsetMin = new Vector2(108f, 8f);
                metaLine.rectTransform.offsetMax = new Vector2(-120f, 32f);
                metaLine.color = new Color(0.78f, 0.82f, 0.95f, 1f);
            }

            var swapBtn = CampUiRuntime.CreateButton(go.transform, "换人", new Color(0.22f, 0.28f, 0.38f, 1f),
                new Vector2(88f, 32f));
            var swapRt = swapBtn.GetComponent<RectTransform>();
            swapRt.anchorMin = new Vector2(1f, 0f);
            swapRt.anchorMax = new Vector2(1f, 0f);
            swapRt.pivot = new Vector2(1f, 0f);
            swapRt.anchoredPosition = new Vector2(-8f, 10f);
            swapBtn.onClick.AddListener(() => ShowCharacterPicker(memberIndex));

            _dynamicObjects.Add(go);
            return go;
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
                        ? new Color(0.45f, 0.55f, 0.85f, 0.35f)
                        : new Color(0.12f, 0.13f, 0.17f, 0.9f)).gameObject;

                var btn = slotGo.AddComponent<Button>();
                btn.targetGraphic = slotGo.GetComponent<Image>();
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
                else
                {
                    var empty = CampUiRuntime.CreateText(slotGo.transform, $"槽 {slot + 1}", 14, FontStyle.Normal);
                    CampUiRuntime.StretchFull(empty.rectTransform);
                    empty.color = new Color(0.55f, 0.58f, 0.65f, 1f);
                }

                _dynamicObjects.Add(slotGo);
            }
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
