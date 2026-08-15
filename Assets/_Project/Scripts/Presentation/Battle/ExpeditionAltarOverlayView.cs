using System.Collections;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Camp;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>祭坛节点：召唤卡牌 + 分配经验 + 休息回复。</summary>
    public sealed class ExpeditionAltarOverlayView : MonoBehaviour
    {
        enum AltarScreen
        {
            Hub,
            SummonCards,
            DistributeXp,
            RestRecovery,
            Engraving,
            UpgradeHp,
            UpgradeEnergy,
            UpgradeHand,
            UpgradeCards
        }

        static readonly Color BgOverlay = new(0f, 0f, 0f, 0.58f);
        static readonly Color PanelBg = new(0.1f, 0.11f, 0.15f, 0.96f);
        static readonly Color CardBg = new(0.14f, 0.16f, 0.22f, 0.94f);
        static readonly Color DisabledCardBg = new(0.08f, 0.09f, 0.12f, 0.55f);
        static readonly Color Border = new(0.32f, 0.36f, 0.44f, 0.85f);
        static readonly Color TextMain = new(0.92f, 0.94f, 0.98f, 1f);
        static readonly Color TextMuted = new(0.62f, 0.68f, 0.78f, 1f);
        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color AccentGreen = new(0.45f, 0.88f, 0.58f, 1f);
        static readonly Color AccentGreenBg = new(0.16f, 0.32f, 0.24f, 0.95f);
        static readonly Color BtnGreen = new(0.18f, 0.38f, 0.28f, 1f);
        static readonly Color BtnNeutral = new(0.22f, 0.24f, 0.3f, 1f);

        const float SummonCardScale = 0.78f;
        const float SummonReplaceCardScale = 0.72f;
        const float UpgradeCardScale = 0.92f;
        // 与背包 BattleInventoryPanelView.CardScale 一致
        const float EngraveCardScale = 0.98f;
        const float HubTileMinHeight = 280f;
        const float ActionButtonHeight = 56f;
        const int LayoutVersion = 29;
        const float HubButtonHoverScale = 1.08f;
        // button6 原生 512×216
        const float Button6Aspect = 512f / 216f;

        /// <summary>归一化热区：原点左下，相对祭坛一级大模板精灵裁切（1488×995）。</summary>
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

        // 一级四服务 2×2：压低高度，避开「选择一项祭坛服务」
        static readonly HubNormRect HubZoneSummon = new(0.120f, 0.340f, 0.480f, 0.575f);
        static readonly HubNormRect HubZoneDistribute = new(0.520f, 0.340f, 0.880f, 0.575f);
        static readonly HubNormRect HubZoneRest = new(0.120f, 0.080f, 0.480f, 0.315f);
        static readonly HubNormRect HubZoneEngraving = new(0.520f, 0.080f, 0.880f, 0.315f);
        static readonly HubNormRect HubZoneXpIcon = new(0.415f, 0.900f, 0.455f, 0.960f);
        static readonly HubNormRect HubZoneXpText = new(0.458f, 0.905f, 0.520f, 0.955f);
        static readonly HubNormRect HubZoneGoldIcon = new(0.530f, 0.895f, 0.575f, 0.960f);
        static readonly HubNormRect HubZoneGoldText = new(0.578f, 0.905f, 0.645f, 0.955f);
        static readonly HubNormRect HubZoneTitleLeft = new(0.03f, 0.90f, 0.20f, 0.97f);
        // 离开：保持此前已对齐位置，不再挪动
        static readonly HubNormRect HubZoneLeave = new(0.755f, 0.018f, 0.958f, 0.128f);

        const float SummonCardWidth = 158f;
        const float SummonCardHeight = 222f;
        const float SummonReplaceCardWidth = 150f;
        const float SummonReplaceCardHeight = 210f;
        const int SummonCollectionColumns = 5;
        const float SummonCollectionGridSpacing = 12f;
        const float SummonReplaceCardSpacing = 14f;
        const float UpgradeCardWidth = 210f;
        const float UpgradeCardHeight = 300f;
        const int UpgradeCardColumns = 3;
        const float UpgradeCardSpacing = 12f;
        // 168×236 × 0.98 + 8 内边距，与背包格子一致；一行 6 张铺满
        const float EngraveCardWidth = 168f * EngraveCardScale + 8f;
        const float EngraveCardHeight = 236f * EngraveCardScale + 8f;
        const int EngraveCardColumns = 6;
        const float EngraveCardSpacing = 12f;

        BattleSession _session;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        RectTransform _contentHost;
        RectTransform _navBar;
        Image _xpHeaderIcon;
        Text _xpHeaderText;
        Image _goldHeaderIcon;
        Text _goldHeaderText;
        Text _layerHeaderText;
        Button _backButton;
        Text _footerHintText;
        Button _leaveButton;
        Image _panelImage;
        RectTransform _panelRt;
        RectTransform _hubLayer;
        RectTransform _engravingHubButton;
        Text _titleLeftText;
        int _builtVersion = -1;


        RectTransform _summonMemberRow;
        RectTransform _summonCollectionHost;
        RectTransform _summonCollectionGrid;
        RectTransform _summonReplaceHost;
        RectTransform _summonReplaceRow;
        Text _summonReplaceLabel;
        Button _summonConfirmButton;

        RectTransform _upgradeCardGrid;
        ScrollRect _upgradeCardScroll;
        float _upgradeCardScrollY = 1f;
        Coroutine _upgradeCardLayoutRoutine;
        RectTransform _upgradeCardDetail;
        Button _upgradeCardButton;
        Text _upgradeCardDetailTitle;
        Text _upgradeCardCurrentText;
        Text _upgradeCardNextText;
        Text _upgradeCardMetaText;

        InventoryTooltipView _tooltip;
        AltarScreen _screen = AltarScreen.Hub;
        int _activeMemberIndex;
        string _selectedUpgradeMemberId;
        string _selectedUpgradeDeckInstanceId;
        string _selectedUpgradeDisplayName;
        bool _built;

        // 刻印
        string _engraveTargetKey = "";
        string _engraveTargetMemberId = "";
        CardEngravingRules.EngraveMethod? _engraveMethod;
        readonly List<string> _engraveSacrificeKeys = new();
        bool _engravePopupOpen;
        ScrollRect _engraveScroll;
        RectTransform _engraveGrid;
        float _engraveScrollY = 1f;

        Button _restGoldButton;
        Button _restXpButton;
        Text _restHintText;

        /// <summary>祭坛子界面切换时通知（用于教程收尾，避免只 Refresh 祭坛却不刷新教学层）。</summary>
        public event System.Action UiStateChanged;

        public void Initialize(
            BattleSession session,
            Transform parent,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var run = _session.Expedition.Run;
            if (run.Phase != ExpeditionPhase.ShrineChoice || run.CardAltar == null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _root.SetAsLastSibling();
            if (_panelRt != null)
                FitPanelToAspect(_panelRt, GetPanelAspect(), 0.92f, 0.90f);
            if (_summonConfirmButton != null && _screen == AltarScreen.SummonCards)
                _summonConfirmButton.transform.SetAsLastSibling();
            if (_leaveButton != null)
                _leaveButton.transform.SetAsLastSibling();
            if (_navBar != null && _screen != AltarScreen.Hub)
                _navBar.SetAsLastSibling();
            RefreshHeader(run);
            UpdateBackLabel();
            RebuildContent(run);
            UiStateChanged?.Invoke();
        }

        void UpdateBackLabel()
        {
            if (_backButton == null)
                return;

            var label = _backButton.GetComponentInChildren<Text>();
            if (label == null)
                return;

            label.text = _screen switch
            {
                AltarScreen.DistributeXp or AltarScreen.SummonCards or AltarScreen.RestRecovery
                    or AltarScreen.Engraving => "← 返回祭坛",
                AltarScreen.UpgradeHp or AltarScreen.UpgradeEnergy or AltarScreen.UpgradeHand or AltarScreen.UpgradeCards => "← 返回分配经验",
                _ => "← 返回"
            };
        }

        void RefreshHeader(ExpeditionRunState run)
        {
            ApplyChromeForScreen();

            if (_xpHeaderText != null)
                _xpHeaderText.text = run.SharedXpPool.ToString();
            if (_xpHeaderIcon != null && _uiIcons?.XpIcon != null)
            {
                _xpHeaderIcon.sprite = _uiIcons.XpIcon;
                _xpHeaderIcon.color = Color.white;
            }

            if (_goldHeaderText != null)
                _goldHeaderText.text = run.Gold.ToString();
            if (_goldHeaderIcon != null && _uiIcons?.GoldIcon != null)
            {
                _goldHeaderIcon.sprite = _uiIcons.GoldIcon;
                _goldHeaderIcon.color = Color.white;
            }

            if (_layerHeaderText != null)
                _layerHeaderText.gameObject.SetActive(false);

            if (_footerHintText != null)
                _footerHintText.gameObject.SetActive(false);

            // 召唤页：左上返回祭坛 + 右下离开；确认取出单独叠在离开上方
            if (_backButton != null)
                _backButton.gameObject.SetActive(_screen != AltarScreen.Hub);
            if (_navBar != null)
                _navBar.gameObject.SetActive(_screen != AltarScreen.Hub);
            if (_leaveButton != null)
                _leaveButton.gameObject.SetActive(true);
            if (_summonConfirmButton != null)
                _summonConfirmButton.gameObject.SetActive(_screen == AltarScreen.SummonCards);
        }

        void RebuildContent(ExpeditionRunState run)
        {
            _tooltip?.Hide();
            ClearRestRecoveryRefs();

            if (_screen == AltarScreen.UpgradeCards && _upgradeCardScroll != null)
                _upgradeCardScrollY = ScrollRectNavigation.CaptureVertical(_upgradeCardScroll);
            if (_screen == AltarScreen.Engraving && _engraveScroll != null)
                _engraveScrollY = ScrollRectNavigation.CaptureVertical(_engraveScroll);

            if (_upgradeCardLayoutRoutine != null)
            {
                StopCoroutine(_upgradeCardLayoutRoutine);
                _upgradeCardLayoutRoutine = null;
            }

            ClearChildren(_contentHost);
            _upgradeCardScroll = null;
            _upgradeCardGrid = null;
            _engraveScroll = null;
            _engraveGrid = null;

            if (_hubLayer != null)
                _hubLayer.gameObject.SetActive(_screen == AltarScreen.Hub);

            switch (_screen)
            {
                case AltarScreen.Hub:
                    // 一级热区在 _hubLayer，模板已含标题文案
                    break;
                case AltarScreen.SummonCards:
                    BuildSummonScreen(_contentHost, run);
                    break;
                case AltarScreen.DistributeXp:
                    BuildDistributeXpScreen(_contentHost, run);
                    break;
                case AltarScreen.RestRecovery:
                    BuildRestRecoveryScreen(_contentHost, run);
                    break;
                case AltarScreen.Engraving:
                    BuildEngravingScreen(_contentHost, run);
                    ScrollRectNavigation.RestoreVertical(_engraveScroll, _engraveScrollY);
                    break;
                case AltarScreen.UpgradeHp:
                    BuildUpgradeHpScreen(_contentHost, run);
                    break;
                case AltarScreen.UpgradeEnergy:
                    BuildUpgradeEnergyScreen(_contentHost, run);
                    break;
                case AltarScreen.UpgradeHand:
                    BuildUpgradeHandScreen(_contentHost, run);
                    break;
                case AltarScreen.UpgradeCards:
                    BuildUpgradeCardsScreen(_contentHost, run);
                    ScrollRectNavigation.RestoreVertical(_upgradeCardScroll, _upgradeCardScrollY);
                    break;
            }
        }

        void BuildHubLayer(RectTransform panelRt)
        {
            _hubLayer = CreateRect("HubLayer", panelRt);
            StretchFull(_hubLayer);

            CreateHubOptionButton(
                _hubLayer, "Summon", HubZoneSummon, "◫", "召唤卡牌",
                "从收藏取出卡牌，加入或替换卡组",
                () => { _screen = AltarScreen.SummonCards; Refresh(); });
            CreateHubOptionButton(
                _hubLayer, "Distribute", HubZoneDistribute, "★", "分配经验",
                "花费经验强化角色与卡牌",
                () => { _screen = AltarScreen.DistributeXp; Refresh(); });
            CreateHubOptionButton(
                _hubLayer, "Rest", HubZoneRest, "♥", "休息回复",
                "花费金币或经验，恢复全队生命",
                () => { _screen = AltarScreen.RestRecovery; Refresh(); });
            _engravingHubButton = CreateHubOptionButton(
                _hubLayer, "Engraving", HubZoneEngraving, "◆", "刻印",
                "将局内卡牌带出至军营收藏",
                () =>
                {
                    ClearEngravingSelection();
                    _screen = AltarScreen.Engraving;
                    Refresh();
                });
        }

        RectTransform CreateHubOptionButton(
            Transform parent,
            string id,
            HubNormRect zone,
            string icon,
            string title,
            string desc,
            System.Action onClick)
        {
            var go = CreateRect(id, parent);
            ApplyHubNormRect(go, zone);

            var hit = go.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0.01f);
            hit.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var visualGo = CreateRect("Visual", go);
            StretchFull(visualGo);
            visualGo.pivot = new Vector2(0.5f, 0.5f);

            var visualImg = visualGo.gameObject.AddComponent<Image>();
            visualImg.color = Color.white;
            visualImg.raycastTarget = false;
            visualImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton7 != null)
                visualImg.sprite = _uiIcons.UiButton7;
            else
            {
                visualImg.color = CardBg;
                Debug.LogWarning($"[ExpeditionAltar] 缺少 UiButton7，请执行 Grimhand → Content → Refresh UI Visual Catalogs。选项：{id}");
            }

            var iconText = CreateStaticText(visualGo, icon, 34, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(iconText.rectTransform, 0.62f, 0.92f);
            iconText.color = TitleGold;

            var titleText = CreateStaticText(visualGo, title, 22, FontStyle.Bold, TextAnchor.UpperCenter);
            StretchBand(titleText.rectTransform, 0.42f, 0.62f);
            titleText.color = TextMain;

            var descText = CreateStaticText(visualGo, desc, 14, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(descText.rectTransform, 0.06f, 0.40f);
            descText.color = TextMuted;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            var group = visualGo.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var hover = go.gameObject.AddComponent<CampBuildingHoverView>();
            hover.Bind(visualGo, group, HubButtonHoverScale, hideWhenIdle: false);

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            return go;
        }

        void BuildRestRecoveryScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(
                parent,
                "休息回复",
                $"选择回复方式，全队恢复 {ExpeditionAltarUpgradeRules.RestHealPercent}% 最大生命",
                titleMinY: 0.90f,
                titleMaxY: 0.995f,
                subtitleMinY: 0.845f,
                subtitleMaxY: 0.90f);

            // 与升级血量同款：event_plate 角色行（仅展示，按钮在底部）
            var list = CreateVerticalList(parent, "RestList", 0.16f, 0.82f, 14f);
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;
                CreateRestMemberRow(list, member, run);
            }

            var bottom = CreateRect("RestBottom", parent);
            SetAnchoredBand(bottom, 0.02f, 0.14f);

            var needsHeal = ExpeditionAltarUpgradeRules.PartyHasRestHealableMember(run);
            var canXp = needsHeal && run.SharedXpPool >= ExpeditionAltarUpgradeRules.RestHealXpCost;
            var canGold = needsHeal && run.Gold >= ExpeditionAltarUpgradeRules.RestHealGoldCost;

            _restXpButton = CreateButton1(
                bottom,
                $"经验回复（{ExpeditionAltarUpgradeRules.RestHealXpCost}）",
                new Vector2(0.28f, 0.5f),
                new Vector2(280f, 64f),
                canXp,
                OnRestHealWithXp);
            _restGoldButton = CreateButton1(
                bottom,
                $"金币回复（{ExpeditionAltarUpgradeRules.RestHealGoldCost}）",
                new Vector2(0.72f, 0.5f),
                new Vector2(280f, 64f),
                canGold,
                OnRestHealWithGold);

            _restHintText = CreateBandText(
                parent,
                needsHeal ? "点击后立即生效，不会离开祭坛" : "当前无需回复",
                16, 0.14f, 0.18f, TextMuted);
            _restHintText.alignment = TextAnchor.MiddleCenter;
        }

        void ClearEngravingSelection()
        {
            _engraveTargetKey = "";
            _engraveTargetMemberId = "";
            _engraveMethod = null;
            _engraveSacrificeKeys.Clear();
            _engravePopupOpen = false;
        }

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public bool IsHubScreen => IsOpen && _screen == AltarScreen.Hub;
        public bool IsEngravingScreen => IsOpen && _screen == AltarScreen.Engraving;

        public RectTransform EngravingHubButtonRect =>
            IsHubScreen
            && _engravingHubButton != null
            && _engravingHubButton.gameObject.activeInHierarchy
                ? _engravingHubButton
                : null;

        /// <summary>
        /// ESC：子界面返回上一层 / 取消弹层；一级 Hub 不消费（交给菜单）。
        /// </summary>
        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;
            if (_screen == AltarScreen.Hub)
                return false;

            if (_engravePopupOpen || IsEngraveSacrificePicking())
            {
                ClearEngravingSelection();
                Refresh();
                return true;
            }

            NavigateBack();
            return true;
        }

        void BuildEngravingScreen(RectTransform parent, ExpeditionRunState run)
        {
            var canOffer = CardEngravingRules.CanOfferEngraving(run, out var offerBlockReason);
            var subtitle = IsEngraveSacrificePicking()
                ? $"献祭刻印：再选 {_engraveSacrificeKeys.Count}/{CardEngravingRules.SacrificeCountRequired} 张同稀有度卡"
                : canOffer
                    ? "本祭坛仅可刻印一次 · 点击卡牌选择方式"
                    : offerBlockReason;
            AddTitle(
                parent,
                "刻印",
                subtitle,
                titleMinY: 0.90f,
                titleMaxY: 0.995f,
                subtitleMinY: 0.845f,
                subtitleMaxY: 0.90f);

            if (run.PendingCardEngravings.Count > 0)
            {
                var pendingLines = new List<string>();
                foreach (var p in run.PendingCardEngravings)
                {
                    if (p == null)
                        continue;
                    pendingLines.Add($"{p.DisplayName} {p.BattlesCompleted}/{p.BattlesRequired}");
                }

                if (pendingLines.Count > 0)
                {
                    var pendingText = CreateBandText(
                        parent,
                        "进行中：" + string.Join("；", pendingLines) + "（完成前不可再刻）",
                        14, 0.80f, 0.845f, AccentGreen);
                    pendingText.alignment = TextAnchor.MiddleCenter;
                }
            }

            var gridHost = CreateRect("EngraveGridHost", parent);
            SetAnchoredBand(gridHost, 0.06f, 0.82f);
            var gridHostRt = gridHost.GetComponent<RectTransform>();
            gridHostRt.offsetMin = new Vector2(10f, 0f);
            gridHostRt.offsetMax = new Vector2(-36f, 0f);

            _engraveGrid = BuildScrollGrid(
                gridHost,
                EngraveCardColumns,
                new Vector2(EngraveCardWidth, EngraveCardHeight),
                EngraveCardSpacing);
            _engraveScroll = _engraveGrid != null
                ? _engraveGrid.GetComponentInParent<ScrollRect>()
                : null;
            AttachEngraveScrollbar(parent, _engraveScroll);

            var cards = ExpeditionRunDeckMutations.ListSelectableCards(_session.Expedition.Config, run);
            var spawned = 0;
            foreach (var entry in cards)
            {
                if (entry?.Template == null)
                    continue;
                SpawnEngraveCard(_engraveGrid, run, entry);
                spawned++;
            }

            FinalizeScrollGridContent(_engraveGrid, _engraveScroll, spawned);

            if (IsEngraveSacrificePicking()
                && _engraveSacrificeKeys.Count == CardEngravingRules.SacrificeCountRequired)
            {
                const float confirmW = 300f;
                CreateButton6(
                    parent,
                    "确认献祭刻印",
                    new Vector2(0.5f, 0.04f),
                    new Vector2(confirmW, confirmW / (512f / 216f)),
                    true,
                    OnConfirmEngraving);
            }

            if (_engravePopupOpen && !string.IsNullOrEmpty(_engraveTargetKey))
            {
                var target = FindEngraveEntry(cards, _engraveTargetKey);
                if (target != null)
                    BuildEngraveMethodPopup(parent, run, target);
                else
                    _engravePopupOpen = false;
            }
        }

        void AttachEngraveScrollbar(RectTransform parent, ScrollRect scroll)
        {
            if (scroll == null || parent == null)
                return;

            var barGo = CreateRect("EngraveScrollbar", parent);
            var barRt = barGo.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.955f, 0.12f);
            barRt.anchorMax = new Vector2(0.985f, 0.78f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;

            var barImg = barGo.gameObject.AddComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = true;
            barImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSliderBar != null)
                barImg.sprite = _uiIcons.UiSliderBar;
            else
                barImg.color = new Color(0.12f, 0.11f, 0.1f, 0.95f);

            var slidingArea = CreateRect("Sliding Area", barGo);
            StretchFull(slidingArea);
            slidingArea.offsetMin = new Vector2(1f, 10f);
            slidingArea.offsetMax = new Vector2(-1f, -10f);

            var handleGo = CreateRect("Handle", slidingArea);
            StretchFull(handleGo);
            var handleImg = handleGo.gameObject.AddComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;
            handleImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSlider != null)
                handleImg.sprite = _uiIcons.UiSlider;
            else
                handleImg.color = new Color(0.42f, 0.34f, 0.28f, 1f);

            var scrollbar = barGo.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleGo;
            scrollbar.targetGraphic = handleImg;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.value = 1f;
            scrollbar.size = 1f;
            scrollbar.numberOfSteps = 0;

            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 0f;
        }

        void SpawnEngraveCard(
            RectTransform parent,
            ExpeditionRunState run,
            ExpeditionRunDeckMutations.DeckCardEntry entry)
        {
            var canTarget = CardEngravingRules.CanSelectAsEngraveTarget(run, entry);
            var pending = CardEngravingRules.FindPending(run, entry.Template.DeckInstanceId);
            var selectedTarget = _engraveTargetKey == entry.Key;
            var selectedSacrifice = _engraveSacrificeKeys.Contains(entry.Key);
            var sacrificePick = IsEngraveSacrificePicking() && entry.Key != _engraveTargetKey;
            var dimmed = !canTarget && !sacrificePick;

            System.Action onClick = null;
            if (canTarget || sacrificePick)
                onClick = () => OnEngraveCardClicked(run, entry);

            // 与背包同呈现：不走祭坛强化字号改写
            SpawnSummonCard(
                parent,
                entry.Template,
                entry.MemberId,
                selectedTarget || selectedSacrifice,
                onClick,
                EngraveCardWidth,
                EngraveCardHeight,
                EngraveCardScale,
                dimmed,
                applyAltarUpgradePresentation: false);

            if (_engraveScroll != null)
                ScrollRectNavigation.WireForwarding(parent.GetChild(parent.childCount - 1).gameObject, _engraveScroll);

            // 状态角标
            if (parent.childCount == 0)
                return;
            var holder = parent.GetChild(parent.childCount - 1) as RectTransform;
            if (holder == null)
                return;

            string badge = null;
            if (pending != null)
                badge = $"{pending.BattlesCompleted}/{pending.BattlesRequired}";
            else if (selectedTarget)
                badge = "目标";
            else if (selectedSacrifice)
                badge = "献祭";

            if (string.IsNullOrEmpty(badge))
                return;

            var badgeText = CreateStaticText(holder, badge, 14, FontStyle.Bold, TextAnchor.UpperRight);
            var badgeRt = badgeText.rectTransform;
            badgeRt.anchorMin = new Vector2(0.55f, 0.82f);
            badgeRt.anchorMax = new Vector2(0.98f, 0.98f);
            badgeRt.offsetMin = Vector2.zero;
            badgeRt.offsetMax = Vector2.zero;
            badgeText.color = TitleGold;
            badgeText.raycastTarget = false;
        }

        void BuildEngraveMethodPopup(
            RectTransform parent,
            ExpeditionRunState run,
            ExpeditionRunDeckMutations.DeckCardEntry target)
        {
            var dim = CreateRect("EngravePopupDim", parent);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            // prompt_plate：与确认框同比例
            const float plateW = 920f;
            const float plateAspect = 1356f / 1057f;
            var panel = CreateRect("EngravePopup", dim);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(plateW, plateW / plateAspect);
            var panelImg = panel.gameObject.AddComponent<Image>();
            panelImg.raycastTarget = true;
            panelImg.preserveAspect = true;
            if (_uiIcons != null && _uiIcons.UiPromptPlate != null)
            {
                panelImg.sprite = _uiIcons.UiPromptPlate;
                panelImg.color = Color.white;
                panelImg.type = Image.Type.Simple;
            }
            else
                panelImg.color = CardBg;

            var rarity = CardEngravingRules.ResolveRarity(target.Template);
            var rarityLabel = CardEngravingRules.DescribeRarity(rarity);
            var goldCost = CardEngravingRules.GetAccountGoldCost(rarity);
            var battles = CardEngravingRules.GetBattlesRequired(rarity);
            var accountGold = _session.Expedition.MetaProfile?.AccountGold ?? 0;

            var title = CreateStaticText(
                panel,
                $"{target.MemberName} · {target.Template.DisplayName}",
                26,
                FontStyle.Bold,
                TextAnchor.MiddleCenter);
            SetNormalizedZone(title.rectTransform, 0.14f, 0.82f, 0.86f, 0.92f);
            title.color = TitleGold;

            var sub = CreateStaticText(
                panel,
                $"{rarityLabel}稀有度 · 选择刻印方式",
                16,
                FontStyle.Normal,
                TextAnchor.MiddleCenter);
            SetNormalizedZone(sub.rectTransform, 0.14f, 0.76f, 0.86f, 0.82f);
            sub.color = TextMuted;

            var methods = CreateRect("Methods", panel);
            SetNormalizedZone(methods, 0.08f, 0.30f, 0.92f, 0.74f);
            var methodGrid = methods.gameObject.AddComponent<GridLayoutGroup>();
            methodGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            methodGrid.constraintCount = 3;
            methodGrid.spacing = new Vector2(12f, 12f);
            methodGrid.cellSize = new Vector2(250f, 180f);
            methodGrid.childAlignment = TextAnchor.MiddleCenter;
            methodGrid.padding = new RectOffset(8, 8, 4, 4);

            CreateEngraveOptionTile(
                methods,
                "局外金刻印",
                $"{goldCost} 局外金（现有 {accountGold}）\n立刻写入收藏",
                _engraveMethod == CardEngravingRules.EngraveMethod.AccountGold,
                () =>
                {
                    _engraveMethod = CardEngravingRules.EngraveMethod.AccountGold;
                    _engraveSacrificeKeys.Clear();
                    Refresh();
                });
            CreateEngraveOptionTile(
                methods,
                "战斗刻印",
                $"胜利 {battles} 场后自动刻印\n期间该牌无法使用",
                _engraveMethod == CardEngravingRules.EngraveMethod.BattleProgress,
                () =>
                {
                    _engraveMethod = CardEngravingRules.EngraveMethod.BattleProgress;
                    _engraveSacrificeKeys.Clear();
                    Refresh();
                });
            CreateEngraveOptionTile(
                methods,
                "献祭刻印",
                $"再选 2 张同为{rarityLabel}的卡摧毁",
                _engraveMethod == CardEngravingRules.EngraveMethod.SacrificeSameRarity,
                () =>
                {
                    _engraveMethod = CardEngravingRules.EngraveMethod.SacrificeSameRarity;
                    _engraveSacrificeKeys.Clear();
                    _engravePopupOpen = false;
                    Refresh();
                });

            var canConfirm = CanConfirmEngraving(run, target, rarity)
                && _engraveMethod is CardEngravingRules.EngraveMethod.AccountGold
                    or CardEngravingRules.EngraveMethod.BattleProgress;

            // 与 CampConfirmPromptView 同槽位：铺满盖住 prompt_plate 预绘钮
            CreateEngravePlateButton(
                panel,
                "取消",
                new Vector4(0.088f, 0.108f, 0.495f, 0.265f),
                _uiIcons != null ? _uiIcons.UiButton3 : null,
                true,
                () =>
                {
                    ClearEngravingSelection();
                    Refresh();
                });
            CreateEngravePlateButton(
                panel,
                "确认刻印",
                new Vector4(0.505f, 0.108f, 0.912f, 0.265f),
                _uiIcons != null ? _uiIcons.UiButton1 : null,
                canConfirm,
                OnConfirmEngraving);
        }

        void CreateEngravePlateButton(
            RectTransform parent,
            string label,
            Vector4 zone,
            Sprite sprite,
            bool interactable,
            System.Action onClick)
        {
            var go = CreateRect(label + "Btn", parent);
            SetNormalizedZone(go, zone.x, zone.y, zone.z, zone.w);

            var img = go.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = interactable ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            else
                img.color = interactable ? BtnGreen : DisabledCardBg;

            var text = CreateStaticText(go, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(6f, 4f);
            text.rectTransform.offsetMax = new Vector2(-6f, -8f);
            text.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            text.raycastTarget = false;

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.interactable = interactable;
            if (interactable)
                btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        void CreateEngraveOptionTile(
            RectTransform parent,
            string title,
            string desc,
            bool selected,
            System.Action onClick)
        {
            var go = CreateRect("EngraveOption", parent);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;

            var img = go.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton7 != null)
            {
                img.sprite = _uiIcons.UiButton7;
                img.color = selected
                    ? new Color(1.12f, 1.05f, 0.82f, 1f)
                    : Color.white;
            }
            else
                img.color = selected ? AccentGreenBg : CardBg;

            var titleText = CreateStaticText(go, title, 24, FontStyle.Bold, TextAnchor.UpperCenter);
            StretchBand(titleText.rectTransform, 0.58f, 0.88f);
            titleText.color = TextMain;

            var descText = CreateStaticText(go, desc, 16, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(descText.rectTransform, 0.10f, 0.56f);
            descText.color = TextMuted;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
        }

        static void SetNormalizedZone(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        bool IsEngraveSacrificePicking() =>
            _engraveMethod == CardEngravingRules.EngraveMethod.SacrificeSameRarity
            && !string.IsNullOrEmpty(_engraveTargetKey)
            && !_engravePopupOpen;

        void OnEngraveCardClicked(ExpeditionRunState run, ExpeditionRunDeckMutations.DeckCardEntry entry)
        {
            if (entry == null)
                return;

            if (IsEngraveSacrificePicking() && entry.Key != _engraveTargetKey)
            {
                var target = FindEngraveEntry(
                    ExpeditionRunDeckMutations.ListSelectableCards(_session.Expedition.Config, run),
                    _engraveTargetKey);
                if (target == null)
                    return;

                var targetRarity = CardEngravingRules.ResolveRarity(target.Template);
                var rarity = CardEngravingRules.ResolveRarity(entry.Template);
                if (rarity != targetRarity)
                {
                    _session.AppendSessionLog(
                        $"需选择同为{CardEngravingRules.DescribeRarity(targetRarity)}的卡作为献祭。");
                    return;
                }

                if (_engraveSacrificeKeys.Contains(entry.Key))
                    _engraveSacrificeKeys.Remove(entry.Key);
                else if (_engraveSacrificeKeys.Count < CardEngravingRules.SacrificeCountRequired)
                    _engraveSacrificeKeys.Add(entry.Key);

                Refresh();
                return;
            }

            if (!CardEngravingRules.CanOfferEngraving(run, out var offerReason))
            {
                _session.AppendSessionLog(offerReason);
                return;
            }

            if (!CardEngravingRules.CanSelectAsEngraveTarget(run, entry))
                return;

            _engraveTargetKey = entry.Key;
            _engraveTargetMemberId = entry.MemberId;
            _engraveMethod = null;
            _engraveSacrificeKeys.Clear();
            _engravePopupOpen = true;
            Refresh();
        }

        bool CanConfirmEngraving(
            ExpeditionRunState run,
            ExpeditionRunDeckMutations.DeckCardEntry target,
            CardRarity rarity)
        {
            if (target == null || _engraveMethod == null)
                return false;
            if (!CardEngravingRules.CanSelectAsEngraveTarget(run, target))
                return false;

            switch (_engraveMethod.Value)
            {
                case CardEngravingRules.EngraveMethod.AccountGold:
                    var profile = _session.Expedition.MetaProfile;
                    return profile != null
                        && profile.AccountGold >= CardEngravingRules.GetAccountGoldCost(rarity);
                case CardEngravingRules.EngraveMethod.BattleProgress:
                    return true;
                case CardEngravingRules.EngraveMethod.SacrificeSameRarity:
                    return _engraveSacrificeKeys.Count == CardEngravingRules.SacrificeCountRequired;
                default:
                    return false;
            }
        }

        void OnConfirmEngraving()
        {
            var engine = _session.Expedition;
            if (engine == null || _engraveMethod == null || string.IsNullOrEmpty(_engraveTargetKey))
                return;

            var ok = _engraveMethod.Value switch
            {
                CardEngravingRules.EngraveMethod.AccountGold =>
                    engine.TryEngraveCardWithAccountGold(
                        _engraveTargetMemberId, _engraveTargetKey),
                CardEngravingRules.EngraveMethod.BattleProgress =>
                    engine.TryStartEngraveCardByBattles(
                        _engraveTargetMemberId, _engraveTargetKey),
                CardEngravingRules.EngraveMethod.SacrificeSameRarity =>
                    _engraveSacrificeKeys.Count == 2
                    && engine.TryEngraveCardBySacrifice(
                        _engraveTargetMemberId,
                        _engraveTargetKey,
                        _engraveSacrificeKeys[0],
                        _engraveSacrificeKeys[1]),
                _ => false
            };

            if (!string.IsNullOrEmpty(engine.Run.LastEventMessage))
                _session.AppendSessionLog(engine.Run.LastEventMessage);

            if (ok)
            {
                ClearEngravingSelection();
                _session.NotifyMetaChanged();
            }

            Refresh();
        }

        static ExpeditionRunDeckMutations.DeckCardEntry FindEngraveEntry(
            List<ExpeditionRunDeckMutations.DeckCardEntry> cards,
            string key)
        {
            if (cards == null || string.IsNullOrEmpty(key))
                return null;
            foreach (var entry in cards)
            {
                if (entry != null && entry.Key == key)
                    return entry;
            }

            return null;
        }

        void CreateRestMemberRow(RectTransform parent, PartyMemberSnapshot member, ExpeditionRunState run)
        {
            ExpeditionPartyStatsRules.GetDisplayHp(
                member, run.Party, run.Relics, run.RelicGrowthTiers, out var hp, out var maxHp);
            var currentHp = System.Math.Max(0, hp);
            var healAmount = currentHp < maxHp
                ? ExpeditionAltarUpgradeRules.ComputeRestHealAmount(member, run)
                : 0;
            var afterHp = currentHp < maxHp ? System.Math.Min(maxHp, currentHp + healAmount) : currentHp;
            var memberId = member.CharacterDefinitionId;
            var canHeal = currentHp < maxHp;

            var go = CreateRect("RestRow", parent);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 148f;
            le.flexibleWidth = 1f;

            var bg = go.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiEventPlate != null)
            {
                bg.sprite = _uiIcons.UiEventPlate;
                bg.color = Color.white;
            }
            else
            {
                bg.color = CardBg;
            }

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(go, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0f, 0.5f);
            portraitRt.anchorMax = new Vector2(0f, 0.5f);
            portraitRt.pivot = new Vector2(0f, 0.5f);
            portraitRt.sizeDelta = new Vector2(112f, 112f);
            portraitRt.anchoredPosition = new Vector2(24f, 0f);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.sprite = _characterVisuals?.GetPortrait(memberId)
                ?? _characterVisuals?.GetPortraitReference(memberId);
            portrait.color = portrait.sprite != null ? Color.white : new Color(0.35f, 0.38f, 0.45f, 1f);

            var name = CreateStaticText(go, member.DisplayName ?? "", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 0.62f);
            name.rectTransform.anchorMax = new Vector2(0.72f, 0.92f);
            name.rectTransform.offsetMin = new Vector2(152f, 0f);
            name.rectTransform.offsetMax = new Vector2(-8f, 0f);
            name.color = TextMain;
            name.alignment = TextAnchor.MiddleLeft;

            var hpLabel = CreateStaticText(go, $"♥ 当前 HP  {hp} / {maxHp}", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            hpLabel.rectTransform.anchorMin = new Vector2(0f, 0.38f);
            hpLabel.rectTransform.anchorMax = new Vector2(0.72f, 0.62f);
            hpLabel.rectTransform.offsetMin = new Vector2(152f, 0f);
            hpLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
            hpLabel.color = hp <= 0 ? TextMuted : TextMain;
            hpLabel.alignment = TextAnchor.MiddleLeft;

            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(go, false);
            var barBgRt = barBgGo.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 0.22f);
            barBgRt.anchorMax = new Vector2(0.55f, 0.36f);
            barBgRt.offsetMin = new Vector2(152f, 0f);
            barBgRt.offsetMax = Vector2.zero;
            var barBg = barBgGo.GetComponent<Image>();
            barBg.color = new Color(0.12f, 0.1f, 0.1f, 0.95f);
            barBg.raycastTarget = false;

            var barFillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRt = barFillGo.GetComponent<RectTransform>();
            StretchFull(barFillRt);
            var fillRatio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            barFillRt.anchorMax = new Vector2(fillRatio, 1f);
            var barFill = barFillGo.GetComponent<Image>();
            barFill.color = new Color(0.78f, 0.22f, 0.22f, 1f);
            barFill.raycastTarget = false;

            var preview = CreateStaticText(
                go,
                canHeal
                    ? (currentHp <= 0
                        ? $"倒下中，回复 +{healAmount} → {afterHp} / {maxHp}"
                        : $"回复 +{healAmount} → {afterHp} / {maxHp}")
                    : "已满血",
                18,
                FontStyle.Normal,
                TextAnchor.MiddleRight);
            preview.rectTransform.anchorMin = new Vector2(0.55f, 0.18f);
            preview.rectTransform.anchorMax = new Vector2(0.98f, 0.82f);
            preview.rectTransform.offsetMin = Vector2.zero;
            preview.rectTransform.offsetMax = Vector2.zero;
            preview.color = canHeal ? AccentGreen : TextMuted;
            preview.alignment = TextAnchor.MiddleRight;
            preview.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        void OnRestHealWithGold()
        {
            _session.AltarRestHealWithGold();
            Refresh();
        }

        void OnRestHealWithXp()
        {
            _session.AltarRestHealWithXp();
            Refresh();
        }

        void ClearRestRecoveryRefs()
        {
            _restGoldButton = null;
            _restXpButton = null;
            _restHintText = null;
        }

        void BuildDistributeXpScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(
                parent,
                "分配经验",
                "选择一项升级服务",
                titleMinY: 0.90f,
                titleMaxY: 0.995f,
                subtitleMinY: 0.845f,
                subtitleMaxY: 0.90f);
            // 与改皮肤前同布局：2×2 大格铺满中部
            var grid = CreateGrid(parent, "XpGrid", 2, 2, 0.06f, 0.82f, 18f, new Vector2(460f, 250f));
            CreateHubTile(grid, "♥", "升级血量", "选择角色提升最大 HP", () => _screen = AltarScreen.UpgradeHp);
            CreateHubTile(grid, "⚡", "升级能量", $"提升能量上限（当前 {GetEffectiveEnergyCap()}）", () => _screen = AltarScreen.UpgradeEnergy);
            CreateHubTile(grid, "▤", "抽牌数量", $"提升每回合抽牌数（当前 {GetEffectiveDrawCount()}）", () => _screen = AltarScreen.UpgradeHand);
            CreateHubTile(grid, "↑", "强化卡牌", "提升卡牌数值", () =>
            {
                _upgradeCardScrollY = 1f;
                _screen = AltarScreen.UpgradeCards;
            });
        }

        void BuildUpgradeHpScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "升级血量", "选择角色，消耗经验提升其最大 HP");
            var list = CreateVerticalList(parent, "HpList", 0.06f, 0.78f, 14f);
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

                var cost = ExpeditionAltarUpgradeRules.GetHpPlus5Cost(member);
                CreateUpgradeHpMemberRow(list, member, cost, run);
            }
        }

        void CreateUpgradeHpMemberRow(
            RectTransform parent,
            PartyMemberSnapshot member,
            int cost,
            ExpeditionRunState run)
        {
            ExpeditionPartyStatsRules.GetDisplayHp(
                member, run.Party, run.Relics, run.RelicGrowthTiers, out var hp, out var maxHp);
            var afterMax = maxHp + ExpeditionAltarUpgradeRules.HpPlus5Amount;
            var afterHp = System.Math.Min(afterMax, hp + ExpeditionAltarUpgradeRules.HpPlus5Amount);
            var canBuy = run.SharedXpPool >= cost && hp > 0;
            var memberId = member.CharacterDefinitionId;

            var go = CreateRect("HpRow", parent);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 148f;
            le.flexibleWidth = 1f;

            var bg = go.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiEventPlate != null)
            {
                bg.sprite = _uiIcons.UiEventPlate;
                bg.color = Color.white;
            }
            else
            {
                bg.color = CardBg;
            }

            // 立绘
            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(go, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0f, 0.5f);
            portraitRt.anchorMax = new Vector2(0f, 0.5f);
            portraitRt.pivot = new Vector2(0f, 0.5f);
            portraitRt.sizeDelta = new Vector2(112f, 112f);
            portraitRt.anchoredPosition = new Vector2(24f, 0f);
            var portrait = portraitGo.GetComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.sprite = _characterVisuals?.GetPortrait(memberId)
                ?? _characterVisuals?.GetPortraitReference(memberId);
            portrait.color = portrait.sprite != null ? Color.white : new Color(0.35f, 0.38f, 0.45f, 1f);

            // 当前 HP 文案
            var hpLabel = CreateStaticText(go, $"♥ 当前 HP  {hp} / {maxHp}", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            hpLabel.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            hpLabel.rectTransform.anchorMax = new Vector2(0.52f, 0.92f);
            hpLabel.rectTransform.offsetMin = new Vector2(152f, 0f);
            hpLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);
            hpLabel.color = TextMain;
            hpLabel.alignment = TextAnchor.MiddleLeft;

            // HP 条
            var barBgGo = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image));
            barBgGo.transform.SetParent(go, false);
            var barBgRt = barBgGo.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 0.38f);
            barBgRt.anchorMax = new Vector2(0.48f, 0.52f);
            barBgRt.offsetMin = new Vector2(152f, 0f);
            barBgRt.offsetMax = new Vector2(0f, 0f);
            var barBg = barBgGo.GetComponent<Image>();
            barBg.color = new Color(0.12f, 0.1f, 0.1f, 0.95f);
            barBg.raycastTarget = false;

            var barFillGo = new GameObject("HpBarFill", typeof(RectTransform), typeof(Image));
            barFillGo.transform.SetParent(barBgGo.transform, false);
            var barFillRt = barFillGo.GetComponent<RectTransform>();
            StretchFull(barFillRt);
            var fillRatio = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            barFillRt.anchorMax = new Vector2(fillRatio, 1f);
            var barFill = barFillGo.GetComponent<Image>();
            barFill.color = new Color(0.78f, 0.22f, 0.22f, 1f);
            barFill.raycastTarget = false;

            // 升级预览
            var preview = CreateStaticText(
                go,
                hp <= 0
                    ? "角色已倒下，无法升级"
                    : $"{cost} XP → HP +{ExpeditionAltarUpgradeRules.HpPlus5Amount}\n升级后：{afterHp} / {afterMax}",
                18,
                FontStyle.Normal,
                TextAnchor.MiddleLeft);
            preview.rectTransform.anchorMin = new Vector2(0.50f, 0.18f);
            preview.rectTransform.anchorMax = new Vector2(0.78f, 0.82f);
            preview.rectTransform.offsetMin = Vector2.zero;
            preview.rectTransform.offsetMax = Vector2.zero;
            preview.color = canBuy ? AccentGreen : TextMuted;
            preview.alignment = TextAnchor.MiddleLeft;
            preview.horizontalOverflow = HorizontalWrapMode.Wrap;

            // button1 升级
            var btnGo = CreateRect("Upgrade", go);
            var btnRt = btnGo;
            btnRt.anchorMin = new Vector2(1f, 0.5f);
            btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.sizeDelta = new Vector2(168f, 64f);
            btnRt.anchoredPosition = new Vector2(-22f, 0f);

            var btnImg = btnGo.gameObject.AddComponent<Image>();
            btnImg.raycastTarget = true;
            btnImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton1 != null)
            {
                btnImg.sprite = _uiIcons.UiButton1;
                btnImg.color = canBuy ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            else
            {
                btnImg.color = canBuy ? BtnGreen : DisabledCardBg;
            }

            var btnLabel = CreateStaticText(btnGo, "升级", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(btnLabel.rectTransform);
            btnLabel.color = canBuy
                ? new Color(0.96f, 0.92f, 0.78f, 1f)
                : new Color(0.55f, 0.55f, 0.58f, 1f);

            var group = btnGo.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = canBuy;
            group.interactable = canBuy;
            if (canBuy)
            {
                var hover = btnGo.gameObject.AddComponent<CampBuildingHoverView>();
                hover.Bind(btnRt, group, HubButtonHoverScale, hideWhenIdle: false);
            }

            var btn = btnGo.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;
            btn.transition = Selectable.Transition.None;
            btn.interactable = canBuy;
            if (canBuy)
            {
                btn.onClick.AddListener(() =>
                {
                    _session.UpgradeAltarMemberHp(memberId);
                    Refresh();
                });
                UiAudioHooks.WireButton(btn);
            }
        }

        void BuildUpgradeEnergyScreen(RectTransform parent, ExpeditionRunState run)
        {
            var current = GetEffectiveEnergyCap();
            var next = current + 1;
            var cost = ExpeditionAltarUpgradeRules.GetEnergyCapUpgradeCost(run.Modifiers);
            var remaining = ExpeditionAltarUpgradeRules.GetRemainingEnergyUpgrades(run.Modifiers);
            BuildStatUpgradeScreen(parent, "能量上限", current, next, cost, remaining,
                ExpeditionAltarUpgradeRules.CanUpgradeEnergyCap(run.Modifiers) && run.SharedXpPool >= cost,
                () => { _session.UpgradeAltarEnergyCap(); Refresh(); });
        }

        void BuildUpgradeHandScreen(RectTransform parent, ExpeditionRunState run)
        {
            var current = GetEffectiveDrawCount();
            var next = current + 1;
            var cost = ExpeditionAltarUpgradeRules.GetHandLimitUpgradeCost(run.Modifiers);
            var remaining = ExpeditionAltarUpgradeRules.GetRemainingHandLimitUpgrades(run.Modifiers);
            BuildStatUpgradeScreen(parent, "抽牌数量", current, next, cost, remaining,
                ExpeditionAltarUpgradeRules.CanUpgradeHandLimit(run.Modifiers) && run.SharedXpPool >= cost,
                () => { _session.UpgradeAltarHandLimit(); Refresh(); });
        }

        void BuildStatUpgradeScreen(
            RectTransform parent,
            string label,
            int current,
            int next,
            int cost,
            int remaining,
            bool canBuy,
            System.Action onBuy)
        {
            AddTitle(parent, label, "");
            var center = CreateRect("StatCenter", parent);
            SetAnchoredBand(center, 0.22f, 0.72f);

            CreateLabel(center, label, 22, TextMuted, new Vector2(0f, 120f), new Vector2(600f, 36f));
            CreateLabel(center, current.ToString(), 72, TextMain, new Vector2(0f, 40f), new Vector2(200f, 90f));
            CreateLabel(center, "↓", 36, TextMuted, new Vector2(0f, -20f), new Vector2(80f, 48f));
            CreateLabel(center, next.ToString(), 72, AccentGreen, new Vector2(0f, -90f), new Vector2(200f, 90f));

            var btn = CreateButton1(
                center,
                $"花费 {cost} XP 升级",
                new Vector2(0.5f, 0.5f),
                new Vector2(360f, 72f),
                canBuy,
                onBuy,
                new Vector2(0f, -190f));
            btn.gameObject.SetActive(cost > 0);

            var maxUpgrades = label.Contains("抽牌") || label.Contains("手牌")
                ? ExpeditionAltarUpgradeRules.MaxHandLimitUpgrades
                : ExpeditionAltarUpgradeRules.MaxEnergyCapUpgrades;
            CreateLabel(center, $"剩余可升级次数：{remaining} / {maxUpgrades}",
                18, TextMuted, new Vector2(0f, -250f), new Vector2(420f, 30f));
        }

        void BuildUpgradeCardsScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "强化卡牌", "选择一张可强化的卡牌查看详情",
                titleMinY: 0.88f, titleMaxY: 0.995f, subtitleMinY: 0.80f, subtitleMaxY: 0.88f);

            var body = CreateRect("CardUpgradeBody", parent);
            var bodyRt = body.GetComponent<RectTransform>();
            SetAnchoredBand(body, 0.02f, 0.78f);

            var gridHost = CreateRect("GridHost", bodyRt);
            var gridHostRt = gridHost.GetComponent<RectTransform>();
            gridHostRt.anchorMin = new Vector2(0f, 0f);
            gridHostRt.anchorMax = new Vector2(0.62f, 1f);
            gridHostRt.offsetMin = Vector2.zero;
            gridHostRt.offsetMax = Vector2.zero;
            _upgradeCardGrid = BuildUpgradeCardScroll(gridHost.transform);
            _upgradeCardScroll = _upgradeCardGrid != null
                ? _upgradeCardGrid.GetComponentInParent<ScrollRect>()
                : null;

            _upgradeCardDetail = CreateRect("Detail", bodyRt);
            var detailRt = _upgradeCardDetail;
            detailRt.anchorMin = new Vector2(0.64f, 0f);
            detailRt.anchorMax = Vector2.one;
            detailRt.offsetMin = new Vector2(8f, 0f);
            detailRt.offsetMax = Vector2.zero;

            _upgradeCardDetailTitle = CreateStaticText(_upgradeCardDetail, "", 34, FontStyle.Bold, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardDetailTitle.rectTransform, 0.82f, 0.98f);
            _upgradeCardCurrentText = CreateStaticText(_upgradeCardDetail, "", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardCurrentText.rectTransform, 0.52f, 0.8f);
            _upgradeCardNextText = CreateStaticText(_upgradeCardDetail, "", 24, FontStyle.Normal, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardNextText.rectTransform, 0.28f, 0.5f);
            _upgradeCardMetaText = CreateStaticText(_upgradeCardDetail, "", 22, FontStyle.Normal, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardMetaText.rectTransform, 0.14f, 0.26f);
            _upgradeCardButton = CreateButton1(
                _upgradeCardDetail,
                "强化",
                new Vector2(0.5f, 0f),
                new Vector2(280f, 72f),
                false,
                ConfirmCardUpgrade,
                new Vector2(0f, 48f));

            ClearChildren(_upgradeCardGrid);
            var config = _session.Expedition.Config;
            var spawned = 0;
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

                foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
                {
                    var template = entry.Template;
                    if (template == null
                        || !CardUpgradeRules.CanUpgrade(member, template))
                        continue;

                    var memberId = member.CharacterDefinitionId;
                    var deckInstanceId = template.DeckInstanceId;
                    var displayName = template.DisplayName;
                    var selected = memberId == _selectedUpgradeMemberId
                                   && deckInstanceId == _selectedUpgradeDeckInstanceId;
                    SpawnUpgradeCardButton(template, memberId, selected, () =>
                    {
                        _selectedUpgradeMemberId = memberId;
                        _selectedUpgradeDeckInstanceId = deckInstanceId;
                        _selectedUpgradeDisplayName = displayName;
                        Refresh();
                    });
                    spawned++;
                }
            }

            ScheduleUpgradeCardGridLayout(spawned);
            RefreshCardUpgradeDetail(run);
        }

        void RefreshCardUpgradeDetail(ExpeditionRunState run)
        {
            if (string.IsNullOrEmpty(_selectedUpgradeMemberId) || string.IsNullOrEmpty(_selectedUpgradeDeckInstanceId))
            {
                _upgradeCardDetailTitle.text = "请选择卡牌";
                _upgradeCardCurrentText.text = "";
                _upgradeCardNextText.text = "";
                _upgradeCardMetaText.text = "";
                SetUpgradeCardButtonInteractable(false);
                var idleImg = _upgradeCardButton.targetGraphic as Image;
                if (idleImg != null)
                {
                    if (_uiIcons != null && _uiIcons.UiButton1 != null)
                        idleImg.color = new Color(0.45f, 0.45f, 0.48f, 1f);
                    else
                        idleImg.color = DisabledCardBg;
                }

                var idleLabel = _upgradeCardButton.GetComponentInChildren<Text>();
                if (idleLabel != null)
                {
                    idleLabel.text = "强化";
                    idleLabel.color = new Color(0.55f, 0.55f, 0.58f, 1f);
                }

                return;
            }

            var member = FindMember(run, _selectedUpgradeMemberId);
            if (member == null)
                return;

            var level = CardUpgradeRules.GetLevel(member, _selectedUpgradeDeckInstanceId);
            var max = CardUpgradeRules.GetMaxLevel(_selectedUpgradeDisplayName);
            var cost = ExpeditionAltarUpgradeRules.GetCardUpgradeCost(_selectedUpgradeDisplayName);
            var canBuy = cost > 0 && run.SharedXpPool >= cost
                         && CardUpgradeRules.CanUpgrade(member, _selectedUpgradeDeckInstanceId, _selectedUpgradeDisplayName);

            CardDefinitionSO def = null;
            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(_session.Expedition.Config, member))
            {
                if (entry.Template?.DeckInstanceId != _selectedUpgradeDeckInstanceId)
                    continue;

                _definitions.TryGetValue(entry.Template.DefinitionId, out def);
                break;
            }
            var currentText = BuildCardEffectDescription(def, level);
            var nextText = BuildCardEffectDescription(def, level + 1);

            _upgradeCardDetailTitle.text = $"{_selectedUpgradeDisplayName}\n{BuildCardMetaLine(def)}";
            _upgradeCardDetailTitle.color = TextMain;
            _upgradeCardCurrentText.text = $"当前效果\n{currentText}";
            _upgradeCardCurrentText.color = TextMuted;
            _upgradeCardNextText.text = $"升级后效果\n{nextText}";
            _upgradeCardNextText.color = AccentGreen;
            _upgradeCardMetaText.text = $"剩余次数: {max - level} / {max}\n{cost} XP";
            _upgradeCardMetaText.color = TextMuted;
            SetUpgradeCardButtonInteractable(canBuy);
            var btnImg = _upgradeCardButton.targetGraphic as Image;
            if (btnImg != null)
            {
                if (_uiIcons != null && _uiIcons.UiButton1 != null)
                    btnImg.color = canBuy ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
                else
                    btnImg.color = canBuy ? BtnGreen : DisabledCardBg;
            }

            var btnLabel = _upgradeCardButton.GetComponentInChildren<Text>();
            if (btnLabel != null)
            {
                btnLabel.text = $"↑  强化（{cost} XP）";
                btnLabel.color = canBuy
                    ? new Color(0.96f, 0.92f, 0.78f, 1f)
                    : new Color(0.55f, 0.55f, 0.58f, 1f);
            }
        }

        void ConfirmCardUpgrade()
        {
            if (string.IsNullOrEmpty(_selectedUpgradeMemberId) || string.IsNullOrEmpty(_selectedUpgradeDeckInstanceId))
                return;

            _session.UpgradeAltarCard(
                _selectedUpgradeMemberId,
                _selectedUpgradeDeckInstanceId,
                _selectedUpgradeDisplayName);
            Refresh();
        }

        /// <summary>
        /// CreateButton1 同时挂了 Button + CanvasGroup；仅改 Button.interactable 时
        /// CanvasGroup.interactable 仍为 false，会吞掉点击导致强化无效。
        /// </summary>
        void SetUpgradeCardButtonInteractable(bool canBuy)
        {
            if (_upgradeCardButton == null)
                return;

            _upgradeCardButton.interactable = canBuy;
            var group = _upgradeCardButton.GetComponent<CanvasGroup>();
            if (group != null)
                group.interactable = canBuy;
        }

        void BuildSummonScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "召唤卡牌", null, 0.90f, 0.98f);

            // 三角色靠上；卡牌在其下完整显示，绝不裁切
            var memberRowGo = new GameObject("MemberButtons", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            memberRowGo.transform.SetParent(parent, false);
            _summonMemberRow = memberRowGo.GetComponent<RectTransform>();
            SetAnchoredBand(_summonMemberRow, 0.78f, 0.88f);
            var memberLayout = memberRowGo.GetComponent<HorizontalLayoutGroup>();
            memberLayout.spacing = 14f;
            memberLayout.padding = new RectOffset(10, 10, 2, 2);
            memberLayout.childAlignment = TextAnchor.MiddleCenter;
            memberLayout.childControlWidth = true;
            memberLayout.childControlHeight = true;
            memberLayout.childForceExpandWidth = true;
            memberLayout.childForceExpandHeight = true;

            // 卡牌区：上方弹性空白把 5×2 网格顶到下方，保证整卡可见
            _summonCollectionHost = CreateRect("CollectionHost", parent);
            SetAnchoredBand(_summonCollectionHost, 0.02f, 0.76f);
            _summonCollectionGrid = BuildSummonCollectionGrid(_summonCollectionHost);

            _summonReplaceHost = CreateRect("ReplaceHost", parent);
            SetAnchoredBand(_summonReplaceHost, 0.0f, 0.14f);
            var replaceBg = CreatePanel("ReplaceBg", _summonReplaceHost, CardBg, Border);
            StretchFull(replaceBg.GetComponent<RectTransform>());
            _summonReplaceLabel = CreateStaticText(replaceBg.transform,
                "卡组已满，请选择要替换的卡牌", 16, FontStyle.Bold, TextAnchor.UpperLeft);
            var replaceLabelRt = _summonReplaceLabel.rectTransform;
            replaceLabelRt.anchorMin = new Vector2(0f, 0.78f);
            replaceLabelRt.anchorMax = new Vector2(1f, 1f);
            replaceLabelRt.offsetMin = new Vector2(12f, 0f);
            replaceLabelRt.offsetMax = new Vector2(-12f, -2f);
            _summonReplaceLabel.color = TextMuted;
            _summonReplaceLabel.alignment = TextAnchor.MiddleLeft;

            var replaceScrollHost = CreateRect("ReplaceScrollHost", replaceBg.transform);
            var replaceScrollRt = replaceScrollHost.GetComponent<RectTransform>();
            replaceScrollRt.anchorMin = Vector2.zero;
            replaceScrollRt.anchorMax = new Vector2(1f, 0.78f);
            replaceScrollRt.offsetMin = new Vector2(8f, 6f);
            replaceScrollRt.offsetMax = new Vector2(-8f, -2f);
            _summonReplaceRow = BuildScrollRowInternal(replaceScrollRt, 0f, 1f, horizontal: true,
                spacing: SummonReplaceCardSpacing, padding: new RectOffset(10, 10, 6, 6));
            _summonReplaceHost.gameObject.SetActive(false);

            if (_activeMemberIndex >= run.Party.Count)
                _activeMemberIndex = 0;

            RebuildSummonContent(run);
        }

        void RebuildSummonContent(ExpeditionRunState run)
        {
            if (_summonMemberRow == null)
                return;

            ClearChildren(_summonMemberRow);
            if (_summonCollectionGrid != null)
                ClearChildren(_summonCollectionGrid);
            if (_summonReplaceRow != null)
                ClearChildren(_summonReplaceRow);

            for (var i = 0; i < run.Party.Count; i++)
            {
                var index = i;
                var member = run.Party[i];
                if (member == null)
                    continue;

                CreateSummonMemberButton(member, index == _activeMemberIndex, () =>
                {
                    _activeMemberIndex = index;
                    RebuildSummonContent(run);
                });
            }

            if (run.Party.Count == 0)
                return;

            var activeMember = run.Party[Mathf.Clamp(_activeMemberIndex, 0, run.Party.Count - 1)];
            FillSummonCollectionCards(activeMember);
            RebuildSummonReplace(activeMember);
            RefreshSummonStatus(activeMember);
        }

        void CreateSummonMemberButton(PartyMemberSnapshot member, bool active, System.Action onFocus)
        {
            var go = CreateRect($"Member_{member.CharacterDefinitionId}", _summonMemberRow);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            le.minWidth = 200f;

            var bg = go.gameObject.AddComponent<Image>();
            bg.raycastTarget = true;
            bg.preserveAspect = false;
            bg.type = Image.Type.Simple;
            if (_uiIcons != null && _uiIcons.UiButton7 != null)
            {
                bg.sprite = _uiIcons.UiButton7;
                bg.color = active ? Color.white : new Color(0.72f, 0.72f, 0.76f, 1f);
            }
            else
            {
                bg.sprite = null;
                bg.color = active ? AccentGreenBg : CardBg;
            }

            var group = go.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.gameObject.AddComponent<CampBuildingHoverView>();
            hover.Bind(go, group, HubButtonHoverScale, hideWhenIdle: false);

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onFocus?.Invoke());
            UiAudioHooks.WireButton(btn);

            var portraitGo = CreateRect("Portrait", go);
            StretchBand(portraitGo, 0.22f, 0.92f);
            var portrait = portraitGo.gameObject.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;
            portrait.sprite = _characterVisuals?.GetPortrait(member.CharacterDefinitionId)
                              ?? _characterVisuals?.GetPortraitReference(member.CharacterDefinitionId);
            portrait.color = portrait.sprite != null
                ? Color.white
                : new Color(0.35f, 0.32f, 0.28f, 1f);

            var name = CreateStaticText(go, member.DisplayName ?? "", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchBand(name.rectTransform, 0.02f, 0.20f);
            name.color = active ? TitleGold : TextMuted;
            name.raycastTarget = false;
        }

        void FillSummonCollectionCards(PartyMemberSnapshot member)
        {
            if (_summonCollectionGrid == null || member == null)
                return;

            var run = _session.Expedition.Run;
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var locked = draft.Confirmed;
            var ids = ExpeditionRunDeckRules.GetCampCollectionCardIds(run, member);
            var slotCount = Mathf.Max(CampRosterState.DeckSize, ids?.Count ?? 0);

            for (var index = 0; index < slotCount; index++)
            {
                var cardId = ids != null && index < ids.Count ? ids[index] : "";
                if (string.IsNullOrEmpty(cardId))
                {
                    CreateEmptySummonSlot(_summonCollectionGrid);
                    continue;
                }

                var template = ExpeditionRunDeckCatalog.ResolveCampCollectionCard(
                    config, cardId, member.CharacterDefinitionId);
                if (template == null)
                {
                    CreateEmptySummonSlot(_summonCollectionGrid);
                    continue;
                }

                var extracted = CampCollectionProgress.IsExtracted(run, member.CharacterDefinitionId, index);
                var selected = !locked && draft.CollectionCardIndex == index;
                var capturedIndex = index;
                var capturedMemberId = member.CharacterDefinitionId;

                // 已取出：视觉上消失（留空槽保布局）
                if (extracted)
                {
                    CreateEmptySummonSlot(_summonCollectionGrid);
                    continue;
                }

                // 本角色本趟已确认取出：其余牌变暗不可再选
                if (locked)
                {
                    SpawnSummonCard(_summonCollectionGrid, template, member.CharacterDefinitionId,
                        selected: false, onClick: null, dimmed: true);
                    continue;
                }

                SpawnSummonCard(_summonCollectionGrid, template, member.CharacterDefinitionId, selected, () =>
                {
                    var party = _session.Expedition.Run.Party;
                    for (var i = 0; i < party.Count; i++)
                    {
                        if (party[i]?.CharacterDefinitionId == capturedMemberId)
                        {
                            _activeMemberIndex = i;
                            break;
                        }
                    }

                    var currentMember = party[Mathf.Clamp(_activeMemberIndex, 0, party.Count - 1)];
                    var current = GetDraft(currentMember);
                    if (current.Confirmed)
                        return;

                    var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, currentMember);
                    var collectionIndex = current.CollectionCardIndex == capturedIndex ? -1 : capturedIndex;
                    var replaceKey = needsReplace && collectionIndex >= 0 ? current.ReplaceDeckCardKey : "";
                    _session.SetCardAltarDraft(capturedMemberId, collectionIndex, replaceKey);
                    RebuildSummonContent(_session.Expedition.Run);
                });
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_summonCollectionGrid);
        }

        /// <summary>
        /// 召唤卡牌区：上方弹性空白 + 固定高度 5×2 网格贴底。
        /// 不裁切，确保十张卡完整可见。
        /// </summary>
        static RectTransform BuildSummonCollectionGrid(RectTransform host)
        {
            // 去掉可能残留的裁剪，避免上排被切掉
            var existingMask = host.gameObject.GetComponent<RectMask2D>();
            if (existingMask != null)
                UnityEngine.Object.Destroy(existingMask);

            var column = host.gameObject.GetComponent<VerticalLayoutGroup>();
            if (column == null)
                column = host.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 0f;
            column.padding = new RectOffset(8, 8, 4, 8);
            column.childAlignment = TextAnchor.LowerCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var spacerGo = new GameObject("TopSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGo.transform.SetParent(host, false);
            var spacerLe = spacerGo.GetComponent<LayoutElement>();
            spacerLe.flexibleHeight = 1f;
            spacerLe.minHeight = 0f;
            spacerLe.preferredHeight = 0f;

            var gridHeight = SummonCardHeight * 2f
                             + SummonCollectionGridSpacing
                             + 8f;

            var gridGo = new GameObject("CampDeckGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            gridGo.transform.SetParent(host, false);
            var gridLe = gridGo.GetComponent<LayoutElement>();
            gridLe.preferredHeight = gridHeight;
            gridLe.minHeight = gridHeight;
            gridLe.flexibleHeight = 0f;
            gridLe.flexibleWidth = 1f;

            var gridRt = gridGo.GetComponent<RectTransform>();
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(SummonCardWidth, SummonCardHeight);
            grid.spacing = new Vector2(SummonCollectionGridSpacing, SummonCollectionGridSpacing);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = SummonCollectionColumns;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(4, 4, 0, 0);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            return gridRt;
        }

        static void CreateEmptySummonSlot(Transform parent)
        {
            var go = new GameObject("EmptySlot", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = SummonCardWidth;
            le.preferredHeight = SummonCardHeight;
            var img = go.GetComponent<Image>();
            // 空槽：透明占位，不要蓝虚影底板
            img.color = Color.clear;
            img.raycastTarget = false;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
        }

        void ConfirmActiveMemberSummon()
        {
            var run = _session.Expedition?.Run;
            if (run?.Party == null || _activeMemberIndex < 0 || _activeMemberIndex >= run.Party.Count)
                return;

            var member = run.Party[_activeMemberIndex];
            if (!_session.ConfirmCardAltar(member.CharacterDefinitionId))
                return;

            RebuildSummonContent(run);
        }

        void RebuildSummonReplace(PartyMemberSnapshot member)
        {
            if (_summonReplaceHost == null || _summonReplaceRow == null)
                return;

            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var locked = draft.Confirmed;
            var needsReplace = !locked && ExpeditionRunDeckRules.NeedsReplace(config, member);
            var showReplace = needsReplace && draft.HasSelection;
            _summonReplaceHost.gameObject.SetActive(showReplace);
            if (_summonCollectionHost != null)
                SetAnchoredBand(_summonCollectionHost, showReplace ? 0.16f : 0.02f, 0.76f);
            if (_summonMemberRow != null)
                SetAnchoredBand(_summonMemberRow, 0.78f, 0.88f);

            if (!showReplace)
                return;

            var deckCount = ExpeditionRunDeckRules.CountMemberDeck(config, member);
            _summonReplaceLabel.text =
                $"卡组已满（{deckCount}/{ExpeditionRunDeckRules.DeckSize}），请选择要替换的卡牌";

            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
            {
                var capturedKey = entry.Key;
                var selected = draft.ReplaceDeckCardKey == capturedKey;
                SpawnSummonCard(_summonReplaceRow, entry.Template, member.CharacterDefinitionId, selected,
                    () =>
                    {
                        var current = GetDraft(member);
                        if (current.Confirmed)
                            return;
                        var replaceKey = current.ReplaceDeckCardKey == capturedKey ? "" : capturedKey;
                        _session.SetCardAltarDraft(
                            member.CharacterDefinitionId, current.CollectionCardIndex, replaceKey);
                        RebuildSummonContent(_session.Expedition.Run);
                    },
                    SummonReplaceCardWidth, SummonReplaceCardHeight, SummonReplaceCardScale);
            }
        }

        void RefreshSummonStatus(PartyMemberSnapshot member)
        {
            SetSummonConfirmInteractable(HasValidDraftForMember(member));
        }

        void SetSummonConfirmInteractable(bool canConfirm)
        {
            if (_summonConfirmButton == null)
                return;

            _summonConfirmButton.interactable = canConfirm;
            var group = _summonConfirmButton.GetComponent<CanvasGroup>();
            if (group != null)
                group.interactable = canConfirm;

            var img = _summonConfirmButton.targetGraphic as Image;
            if (img != null)
            {
                if (_uiIcons != null && _uiIcons.UiButton1 != null)
                    img.color = canConfirm ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
                else
                    img.color = canConfirm ? BtnGreen : DisabledCardBg;
            }

            var label = _summonConfirmButton.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = "确认取出";
                label.color = canConfirm
                    ? new Color(0.96f, 0.92f, 0.78f, 1f)
                    : new Color(0.55f, 0.55f, 0.58f, 1f);
            }
        }

        bool HasValidDraftForMember(PartyMemberSnapshot member)
        {
            if (member == null)
                return false;

            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            if (draft.Confirmed || !draft.HasSelection)
                return false;

            if (ExpeditionRunDeckRules.NeedsReplace(config, member) && string.IsNullOrEmpty(draft.ReplaceDeckCardKey))
                return false;

            return true;
        }

        ExpeditionCardAltarMemberDraft GetDraft(PartyMemberSnapshot member)
        {
            var altar = _session.Expedition.Run.CardAltar;
            if (altar == null)
                return new ExpeditionCardAltarMemberDraft();

            return altar.Drafts.TryGetValue(member.CharacterDefinitionId, out var draft)
                ? draft
                : new ExpeditionCardAltarMemberDraft();
        }

        void CreateHubTile(RectTransform parent, string icon, string title, string desc, System.Action onClick)
        {
            var go = CreateRect("Tile", parent);
            var inGrid = parent.GetComponent<GridLayoutGroup>() != null;

            var le = go.gameObject.AddComponent<LayoutElement>();
            if (!inGrid)
            {
                le.flexibleWidth = 1f;
                le.flexibleHeight = 1f;
                le.minWidth = 420f;
                le.minHeight = HubTileMinHeight;
            }

            var img = go.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton7 != null)
            {
                img.sprite = _uiIcons.UiButton7;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = CardBg;
            }

            var iconText = CreateStaticText(go, icon, 48, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(iconText.rectTransform, 0.66f, 0.92f);
            iconText.color = TitleGold;

            var titleText = CreateStaticText(go, title, 28, FontStyle.Bold, TextAnchor.UpperCenter);
            StretchBand(titleText.rectTransform, 0.46f, 0.64f);
            titleText.color = TextMain;

            var descText = CreateStaticText(go, desc, 17, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(descText.rectTransform, 0.08f, 0.44f);
            descText.color = TextMuted;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            var group = go.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.gameObject.AddComponent<CampBuildingHoverView>();
            hover.Bind(go, group, HubButtonHoverScale, hideWhenIdle: false);

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                onClick?.Invoke();
                Refresh();
            });
            UiAudioHooks.WireButton(btn);
        }

        void AddXpBadge(RectTransform parent, int amount, float minY, float maxY)
        {
            var host = CreateRect("XpBadge", parent);
            SetAnchoredBand(host, minY, maxY);
            var hostRt = host.GetComponent<RectTransform>();
            hostRt.offsetMin = new Vector2(0f, 0f);
            hostRt.offsetMax = new Vector2(0f, 0f);

            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(host, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(160f, 48f);
            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(rowGo.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 36f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 36f;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (_uiIcons?.XpIcon != null)
            {
                icon.sprite = _uiIcons.XpIcon;
                icon.color = Color.white;
            }

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGo.transform.SetParent(rowGo.transform, false);
            textGo.GetComponent<LayoutElement>().preferredWidth = 96f;
            var amountText = textGo.GetComponent<Text>();
            amountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            amountText.fontSize = 28;
            amountText.fontStyle = FontStyle.Bold;
            amountText.alignment = TextAnchor.MiddleLeft;
            amountText.color = AccentGreen;
            amountText.text = amount.ToString();
            amountText.raycastTarget = false;
        }

        void SpawnUpgradeCardButton(CardTemplate template, string ownerId, bool selected, System.Action onClick)
        {
            var holder = CreateUpgradeCardHolder(_upgradeCardGrid, UpgradeCardWidth, UpgradeCardHeight);
            var btn = holder.gameObject.AddComponent<Button>();
            btn.targetGraphic = holder;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            ScrollRectNavigation.WireForwarding(holder.gameObject, _upgradeCardScroll);
            var cardView = SpawnCardVisual(holder.transform, template, ownerId, UpgradeCardScale);
            CardView.ConfigureForAltarUpgradePresentation(cardView);
            if (cardView != null)
                cardView.SetSelected(selected);
        }

        void SpawnSummonCard(RectTransform parent, CardTemplate template, string ownerId, bool selected, System.Action onClick,
            float width = SummonCardWidth, float height = SummonCardHeight, float scale = SummonCardScale,
            bool dimmed = false, bool applyAltarUpgradePresentation = true)
        {
            // 透明点击区，无蓝虚影底板；选中只靠 CardView 高亮
            var holder = CreateSummonCardHolder(parent, width, height);
            if (onClick != null && !dimmed)
            {
                var btn = holder.gameObject.AddComponent<Button>();
                btn.targetGraphic = holder;
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => onClick.Invoke());
                UiAudioHooks.WireButton(btn);
            }

            ScrollRectNavigation.WireForwarding(holder.gameObject);
            var cardView = SpawnCardVisual(holder.transform, template, ownerId, scale);
            if (applyAltarUpgradePresentation)
                CardView.ConfigureForAltarUpgradePresentation(cardView);
            if (cardView != null)
                cardView.SetSelected(selected && !dimmed);

            if (dimmed)
            {
                var cg = holder.gameObject.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = holder.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.42f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
        }

        /// <summary>召唤/替换卡牌：透明点击区，无虚影底板；选中由 CardView 高亮。</summary>
        static Image CreateSummonCardHolder(Transform parent, float width, float height)
        {
            var go = new GameObject("CardHolder", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var holder = go.GetComponent<Image>();
            holder.color = Color.clear;
            holder.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            return holder;
        }

        /// <summary>强化卡牌列表：透明点击区，无虚影底板；选中由 CardView 高亮。</summary>
        static Image CreateUpgradeCardHolder(Transform parent, float width, float height)
        {
            var go = new GameObject("CardHolder", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var holder = go.GetComponent<Image>();
            holder.color = Color.clear;
            holder.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(width, height);
            return holder;
        }

        RectTransform BuildUpgradeCardScroll(Transform parent)
        {
            var scrollGo = new GameObject("UpgradeScroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(scrollRt);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt);
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = Color.clear;
            viewportImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 48f;
            scroll.viewport = viewportRt;
            scroll.content = contentRt;
            return contentRt;
        }

        void ScheduleUpgradeCardGridLayout(int itemCount)
        {
            LayoutUpgradeCardGrid(itemCount);
            if (!isActiveAndEnabled)
                return;

            if (_upgradeCardLayoutRoutine != null)
                StopCoroutine(_upgradeCardLayoutRoutine);
            _upgradeCardLayoutRoutine = StartCoroutine(LayoutUpgradeCardGridNextFrame(itemCount));
        }

        IEnumerator LayoutUpgradeCardGridNextFrame(int itemCount)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            LayoutUpgradeCardGrid(itemCount);
            if (_upgradeCardScroll != null)
                ScrollRectNavigation.RestoreVertical(_upgradeCardScroll, _upgradeCardScrollY);
            _upgradeCardLayoutRoutine = null;
        }

        void LayoutUpgradeCardGrid(int itemCount)
        {
            if (_upgradeCardGrid == null)
                return;

            Canvas.ForceUpdateCanvases();
            var viewW = 0f;
            if (_upgradeCardScroll != null && _upgradeCardScroll.viewport != null)
                viewW = _upgradeCardScroll.viewport.rect.width;
            if (viewW < 32f)
                viewW = (_upgradeCardGrid.parent as RectTransform)?.rect.width ?? 640f;
            if (viewW < 32f)
                viewW = 640f;

            const float pad = 8f;
            var cellW = Mathf.Min(
                UpgradeCardWidth,
                (viewW - pad * 2f - UpgradeCardSpacing * (UpgradeCardColumns - 1)) / UpgradeCardColumns);
            cellW = Mathf.Max(140f, cellW);
            var cellH = cellW * (UpgradeCardHeight / UpgradeCardWidth);

            var count = Mathf.Max(itemCount, _upgradeCardGrid.childCount);
            for (var i = 0; i < _upgradeCardGrid.childCount; i++)
            {
                var rt = _upgradeCardGrid.GetChild(i) as RectTransform;
                if (rt == null)
                    continue;

                var row = i / UpgradeCardColumns;
                var col = i % UpgradeCardColumns;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(cellW, cellH);
                rt.anchoredPosition = new Vector2(
                    pad + col * (cellW + UpgradeCardSpacing),
                    -(pad + row * (cellH + UpgradeCardSpacing)));

                var cardView = rt.GetComponentInChildren<CardView>(true);
                if (cardView != null)
                    CardView.CenterInParent(cardView);
            }

            var rows = count > 0 ? (count + UpgradeCardColumns - 1) / UpgradeCardColumns : 0;
            var height = pad * 2f
                         + rows * cellH
                         + Mathf.Max(0, rows - 1) * UpgradeCardSpacing;
            _upgradeCardGrid.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(height, 1f));

            if (_upgradeCardScroll != null)
                _upgradeCardScroll.verticalNormalizedPosition = 1f;
        }

        static Image CreateCardHolder(Transform parent, bool selected, float width, float height)
        {
            var holder = CreatePanel("CardHolder", parent, selected ? AccentGreenBg : CardBg, Border);
            var le = holder.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
            return holder;
        }

        CardView SpawnCardVisual(Transform parent, CardTemplate template, string ownerId, float scale)
        {
            if (_cardPrefab == null || template == null)
                return null;

            _definitions.TryGetValue(template.DefinitionId, out var definition);
            if (definition != null && string.IsNullOrWhiteSpace(template.DisplayName))
                template.DisplayName = definition.DisplayName;
            var cardView = Instantiate(_cardPrefab, parent);
            CardView.ApplyHandPresentationScaleCentered(cardView, scale);
            var preview = CardVisualResolver.CreatePreviewInstanceFromTemplate(template, definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
            cardView.BindWithCard(preview, visual, false, false, false, "", statsLine, _uiIcons, _characterVisuals, null, null, null);
            var cg = cardView.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.blocksRaycasts = false;
            return cardView;
        }

        string BuildCardEffectDescription(CardDefinitionSO def, int upgradeLevel)
        {
            if (def == null)
                return "—";

            var template = def.ToTemplate();
            CardUpgradeRules.ApplyToTemplate(template, upgradeLevel);
            var preview = CardVisualResolver.CreatePreviewInstanceFromTemplate(template, def);
            var text = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
            return string.IsNullOrWhiteSpace(text) ? "—" : text;
        }

        static string BuildCardMetaLine(CardDefinitionSO def)
        {
            if (def == null)
                return "";

            var type = def.CardType switch
            {
                CardType.Attack => "攻击",
                CardType.Defense => "防御",
                CardType.Status => "状态",
                _ => "卡牌"
            };
            var rarity = def.Rarity switch
            {
                CardRarity.Rare => "绿色",
                CardRarity.SuperRare => "蓝色",
                CardRarity.Epic => "紫色",
                CardRarity.Legendary => "橙色",
                _ => "白色"
            };
            return $"{def.Cost}费 · {type} · {rarity}";
        }

        int GetEffectiveEnergyCap() =>
            _session.Expedition.GetAltarBaseEnergyCap() + _session.Expedition.Run.Modifiers.EnergyCapBonus;

        int GetEffectiveDrawCount()
        {
            var mods = _session.Expedition.Run.Modifiers;
            return _session.Expedition.GetAltarBaseDrawCount()
                   + mods.DrawPerTurnBonus
                   + mods.HandLimitBonus;
        }

        static PartyMemberSnapshot FindMember(ExpeditionRunState run, string memberId)
        {
            foreach (var member in run.Party)
            {
                if (member != null && member.CharacterDefinitionId == memberId)
                    return member;
            }

            return null;
        }

        void NavigateBack()
        {
            if (_screen == AltarScreen.Engraving)
                ClearEngravingSelection();

            _screen = _screen switch
            {
                AltarScreen.SummonCards or AltarScreen.DistributeXp or AltarScreen.RestRecovery
                    or AltarScreen.Engraving => AltarScreen.Hub,
                AltarScreen.UpgradeHp or AltarScreen.UpgradeEnergy or AltarScreen.UpgradeHand or AltarScreen.UpgradeCards => AltarScreen.DistributeXp,
                _ => AltarScreen.Hub
            };
            Refresh();
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && _builtVersion == LayoutVersion)
                return;

            if (_built && _root != null)
                DestroyImmediate(_root.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;
            var go = new GameObject("ExpeditionAltarOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            go.GetComponent<Image>().color = BgOverlay;

            var panelGo = CreatePanel("Panel", go.transform, PanelBg, Border);
            _panelImage = panelGo;
            _panelRt = panelGo.GetComponent<RectTransform>();
            // 模板 1537×1023
            const float HubAspect = 1488f / 995f;
            FitPanelToAspect(_panelRt, HubAspect, 0.92f, 0.90f);
            _panelImage.raycastTarget = true;
            _tooltip = panelGo.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_panelRt, _uiIcons);

            BuildHeader(_panelRt);
            BuildHubLayer(_panelRt);
            _contentHost = CreateRect("Content", _panelRt);
            SetAnchoredBand(_contentHost, 0.13f, 0.74f);
            BuildNavBar(_panelRt);
            BuildFooter(_panelRt);

            // 导航栏必须在内容区之后创建，确保返回按钮可点击。
            _navBar.SetAsLastSibling();
            ApplyChromeForScreen();

            go.SetActive(false);
        }

        static void FitPanelToAspect(RectTransform rt, float aspect, float maxWidthFrac, float maxHeightFrac)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;

            var parent = rt.parent as RectTransform;
            var parentW = parent != null ? parent.rect.width : Screen.width;
            var parentH = parent != null ? parent.rect.height : Screen.height;
            if (parentW < 8f) parentW = 1920f;
            if (parentH < 8f) parentH = 1080f;

            var maxW = parentW * maxWidthFrac;
            var maxH = parentH * maxHeightFrac;
            var width = maxW;
            var height = width / aspect;
            if (height > maxH)
            {
                height = maxH;
                width = height * aspect;
            }

            rt.sizeDelta = new Vector2(width, height);
        }

        void BuildNavBar(RectTransform panelRt)
        {
            _navBar = CreateRect("NavBar", panelRt);
            SetAnchoredBand(_navBar, 0.74f, 0.84f);

            var backGo = CreateRect("Back", _navBar);
            var backRt = backGo;
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.sizeDelta = new Vector2(200f, 68f);
            backRt.anchoredPosition = new Vector2(28f, 0f);

            var backImg = backGo.gameObject.AddComponent<Image>();
            backImg.raycastTarget = true;
            backImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton2 != null)
            {
                backImg.sprite = _uiIcons.UiButton2;
                backImg.color = Color.white;
            }
            else
            {
                backImg.color = BtnNeutral;
            }

            var backLabel = CreateStaticText(backGo, "← 返回祭坛", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(backLabel.rectTransform);
            backLabel.color = new Color(0.96f, 0.92f, 0.78f, 1f);

            var backGroup = backGo.gameObject.AddComponent<CanvasGroup>();
            backGroup.alpha = 1f;
            backGroup.blocksRaycasts = true;
            backGroup.interactable = true;
            var backHover = backGo.gameObject.AddComponent<CampBuildingHoverView>();
            backHover.Bind(backRt, backGroup, HubButtonHoverScale, hideWhenIdle: false);

            _backButton = backGo.gameObject.AddComponent<Button>();
            _backButton.targetGraphic = backImg;
            _backButton.transition = Selectable.Transition.None;
            _backButton.onClick.AddListener(NavigateBack);
            UiAudioHooks.WireButton(_backButton);
            _navBar.gameObject.SetActive(false);
        }

        bool IsDistributeFamilyScreen() =>
            _screen is AltarScreen.DistributeXp
                or AltarScreen.UpgradeHp
                or AltarScreen.UpgradeEnergy
                or AltarScreen.UpgradeHand
                or AltarScreen.UpgradeCards;

        float GetPanelAspect()
        {
            // 分配经验等二级页也与一级 UI 同框尺寸；event_plate 拉伸铺满
            return 1488f / 995f;
        }

        void BuildHeader(RectTransform panelRt)
        {
            _titleLeftText = CreateStaticText(panelRt, "祭坛", 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            _titleLeftText.color = TitleGold;

            _xpHeaderIcon = CreateHeaderIcon(panelRt, "XpIcon");
            _xpHeaderText = CreateStaticText(panelRt, "0", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            _xpHeaderText.color = AccentGreen;

            _goldHeaderIcon = CreateHeaderIcon(panelRt, "GoldIcon");
            _goldHeaderText = CreateStaticText(panelRt, "0", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            _goldHeaderText.color = TitleGold;

            _layerHeaderText = CreateStaticText(panelRt, "", 18, FontStyle.Normal, TextAnchor.MiddleRight);
            _layerHeaderText.color = TextMuted;
            _layerHeaderText.gameObject.SetActive(false);
        }

        static Image CreateHeaderIcon(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = Color.white;
            return img;
        }

        void BuildFooter(RectTransform panelRt)
        {
            _footerHintText = CreateStaticText(panelRt, "祭坛操作完成后点击离开", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            _footerHintText.color = TextMuted;
            _footerHintText.gameObject.SetActive(false);

            var leaveGo = CreateRect("LeaveAltar", panelRt);
            var leaveImg = leaveGo.gameObject.AddComponent<Image>();
            leaveImg.color = Color.white;
            leaveImg.raycastTarget = true;
            leaveImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton6 != null)
                leaveImg.sprite = _uiIcons.UiButton6;
            else
                leaveImg.color = BtnGreen;

            var leaveLabel = CreateStaticText(leaveGo, "离开祭坛", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(leaveLabel.rectTransform);
            leaveLabel.color = new Color(0.96f, 0.92f, 0.78f, 1f);

            var leaveGroup = leaveGo.gameObject.AddComponent<CanvasGroup>();
            leaveGroup.alpha = 1f;
            leaveGroup.blocksRaycasts = true;
            leaveGroup.interactable = true;
            var leaveHover = leaveGo.gameObject.AddComponent<CampBuildingHoverView>();
            leaveHover.Bind(leaveGo, leaveGroup, HubButtonHoverScale, hideWhenIdle: false);

            _leaveButton = leaveGo.gameObject.AddComponent<Button>();
            _leaveButton.targetGraphic = leaveImg;
            _leaveButton.transition = Selectable.Transition.None;
            _leaveButton.onClick.AddListener(() => _session.LeaveAltar());
            UiAudioHooks.WireButton(_leaveButton);

            // 召唤页：确认取出叠在离开正上方；右缘与离开齐平，宽度略窄
            _summonConfirmButton = CreateButton1(
                panelRt,
                "确认取出",
                new Vector2(HubZoneLeave.XMax, 0.140f),
                new Vector2(220f, 64f),
                false,
                ConfirmActiveMemberSummon);
            var confirmRt = _summonConfirmButton.GetComponent<RectTransform>();
            confirmRt.pivot = new Vector2(1f, 0f);
            confirmRt.anchoredPosition = Vector2.zero;
            _summonConfirmButton.gameObject.SetActive(false);
        }

        void ApplyChromeForScreen()
        {
            var hub = _screen == AltarScreen.Hub;
            var distributeFamily = IsDistributeFamilyScreen();
            var summon = _screen == AltarScreen.SummonCards;
            var engraving = _screen == AltarScreen.Engraving;
            var eventPlateChrome = distributeFamily || summon || engraving
                || _screen == AltarScreen.RestRecovery;
            if (_hubLayer != null)
                _hubLayer.gameObject.SetActive(hub);

            if (_panelImage != null)
            {
                var outline = _panelImage.GetComponent<Outline>();
                if (hub && _uiIcons != null && _uiIcons.UiExpeditionAltarHubBackground != null)
                {
                    _panelImage.sprite = _uiIcons.UiExpeditionAltarHubBackground;
                    _panelImage.color = Color.white;
                    _panelImage.type = Image.Type.Simple;
                    _panelImage.preserveAspect = false;
                    if (outline != null)
                        outline.enabled = false;
                }
                else if (eventPlateChrome && _uiIcons != null && _uiIcons.UiEventPlate != null)
                {
                    _panelImage.sprite = _uiIcons.UiEventPlate;
                    _panelImage.color = Color.white;
                    _panelImage.type = Image.Type.Simple;
                    _panelImage.preserveAspect = false;
                    if (outline != null)
                        outline.enabled = false;
                }
                else
                {
                    _panelImage.sprite = null;
                    _panelImage.color = PanelBg;
                    if (outline != null)
                    {
                        outline.enabled = true;
                        outline.effectColor = Border;
                        outline.effectDistance = new Vector2(1f, -1f);
                    }
                }
            }

            var rest = _screen == AltarScreen.RestRecovery;
            // 二级页统一：隐藏左上「祭坛」，返回钮与召唤同位置
            var secondaryChrome = summon || engraving || distributeFamily || rest;

            // 全祭坛界面：XP/金币/离开与一级 UI 同热区
            if (_titleLeftText != null)
            {
                ApplyHubNormRect(_titleLeftText.rectTransform, HubZoneTitleLeft);
                _titleLeftText.gameObject.SetActive(!secondaryChrome);
            }

            if (_xpHeaderIcon != null)
                ApplyHubNormRect(_xpHeaderIcon.rectTransform, HubZoneXpIcon);
            if (_xpHeaderText != null)
                ApplyHubNormRect(_xpHeaderText.rectTransform, HubZoneXpText);
            if (_goldHeaderIcon != null)
                ApplyHubNormRect(_goldHeaderIcon.rectTransform, HubZoneGoldIcon);
            if (_goldHeaderText != null)
                ApplyHubNormRect(_goldHeaderText.rectTransform, HubZoneGoldText);

            if (_layerHeaderText != null)
                _layerHeaderText.gameObject.SetActive(false);
            if (_footerHintText != null)
                _footerHintText.gameObject.SetActive(false);

            if (_leaveButton != null)
                ApplyHubNormRect(_leaveButton.GetComponent<RectTransform>(), HubZoneLeave);
            if (_summonConfirmButton != null)
            {
                // 右缘对齐离开按钮（XMax），宽度 220 比离开略窄
                var confirmRt = _summonConfirmButton.GetComponent<RectTransform>();
                confirmRt.anchorMin = new Vector2(HubZoneLeave.XMax, 0.140f);
                confirmRt.anchorMax = new Vector2(HubZoneLeave.XMax, 0.140f);
                confirmRt.pivot = new Vector2(1f, 0f);
                confirmRt.sizeDelta = new Vector2(220f, 64f);
                confirmRt.anchoredPosition = Vector2.zero;
            }

            if (_navBar != null)
                SetAnchoredBand(_navBar, secondaryChrome ? 0.82f : 0.74f, secondaryChrome ? 0.92f : 0.84f);

            if (_contentHost != null)
            {
                if (hub)
                {
                    _contentHost.gameObject.SetActive(false);
                }
                else
                {
                    var contentMin = secondaryChrome ? 0.18f : 0.13f;
                    var contentMax = secondaryChrome ? 0.82f : 0.74f;
                    SetAnchoredBand(_contentHost, contentMin, contentMax);
                    _contentHost.gameObject.SetActive(true);
                }
            }
        }

        static void ApplyHubNormRect(RectTransform rt, HubNormRect zone)
        {
            rt.anchorMin = new Vector2(zone.XMin, zone.YMin);
            rt.anchorMax = new Vector2(zone.XMax, zone.YMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        void AddTitle(
            RectTransform parent,
            string title,
            string subtitle,
            float titleMinY = 0.84f,
            float titleMaxY = 0.98f,
            float subtitleMinY = 0.74f,
            float subtitleMaxY = 0.84f)
        {
            var titleText = CreateStaticText(parent, title, 34, FontStyle.Bold, TextAnchor.UpperCenter);
            StretchBand(titleText.rectTransform, titleMinY, titleMaxY);
            titleText.color = TitleGold;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = CreateStaticText(parent, subtitle, 18, FontStyle.Normal, TextAnchor.UpperCenter);
                StretchBand(sub.rectTransform, subtitleMinY, subtitleMaxY);
                sub.color = TextMuted;
                sub.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        static RectTransform CreateHorizontalRow(
            RectTransform parent,
            string name,
            float minY,
            float maxY,
            float spacing = 24f,
            bool expandChildren = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchoredBand(rt, minY, maxY);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(24, 24, 12, 12);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = expandChildren;
            layout.childControlHeight = expandChildren;
            layout.childForceExpandWidth = expandChildren;
            layout.childForceExpandHeight = expandChildren;
            if (!expandChildren)
                ConfigureHorizontalLayout(layout, spacing);
            return rt;
        }

        static RectTransform CreateGrid(
            RectTransform parent,
            string name,
            int cols,
            int rows,
            float minY,
            float maxY,
            float spacing,
            Vector2 cellSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(GridLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchoredBand(rt, minY, maxY);
            var grid = go.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = cols;
            grid.spacing = new Vector2(spacing, spacing);
            grid.cellSize = cellSize;
            grid.padding = new RectOffset(16, 16, 12, 12);
            grid.childAlignment = TextAnchor.UpperCenter;
            return rt;
        }

        static RectTransform CreateVerticalList(RectTransform parent, string name, float minY, float maxY, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetAnchoredBand(rt, minY, maxY);
            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.padding = new RectOffset(8, 8, 8, 8);
            return rt;
        }

        RectTransform BuildScrollRow(RectTransform parent, float minY, float maxY)
        {
            return BuildScrollRowInternal(parent, minY, maxY, horizontal: true);
        }

        RectTransform BuildScrollRowInternal(RectTransform parent, float minY, float maxY, bool horizontal,
            float spacing = 14f, RectOffset padding = null)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            SetAnchoredBand(scrollRt, minY, maxY);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.45f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(viewportGo.transform, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0f);
            rowRt.anchorMax = new Vector2(0f, 1f);
            rowRt.pivot = new Vector2(0f, 0.5f);
            rowRt.offsetMin = Vector2.zero;
            rowRt.offsetMax = Vector2.zero;
            var rowLayout = rowGo.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = spacing;
            rowLayout.padding = padding ?? new RectOffset(12, 12, 8, 8);
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowGo.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            rowGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = horizontal;
            scroll.vertical = !horizontal;
            scroll.viewport = viewportRt;
            scroll.content = rowRt;
            return rowRt;
        }

        RectTransform BuildScrollGrid(Transform parent, int columns, Vector2 cellSize, float spacing = 14f)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            StretchFull(scrollRt);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = Color.clear;
            viewportGo.GetComponent<Image>().raycastTarget = true;

            var gridGo = new GameObject("Grid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            gridGo.transform.SetParent(viewportGo.transform, false);
            var gridRt = gridGo.GetComponent<RectTransform>();
            gridRt.anchorMin = new Vector2(0f, 1f);
            gridRt.anchorMax = new Vector2(1f, 1f);
            gridRt.pivot = new Vector2(0.5f, 1f);
            gridRt.anchoredPosition = Vector2.zero;
            gridRt.sizeDelta = Vector2.zero;
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(spacing, spacing);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            var fitter = gridGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewportRt;
            scroll.content = gridRt;
            return gridRt;
        }

        static void FinalizeScrollGridContent(RectTransform gridRt, ScrollRect scroll, int itemCount)
        {
            if (gridRt == null)
                return;

            var grid = gridRt.GetComponent<GridLayoutGroup>();
            if (grid != null && itemCount > 0)
            {
                var cols = Mathf.Max(1, grid.constraintCount);
                var rows = (itemCount + cols - 1) / cols;
                var height = grid.padding.top + grid.padding.bottom
                             + rows * grid.cellSize.y
                             + Mathf.Max(0, rows - 1) * grid.spacing.y;
                gridRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridRt);
            if (scroll == null)
                return;

            Canvas.ForceUpdateCanvases();
            var viewH = scroll.viewport != null ? scroll.viewport.rect.height : 0f;
            var contentH = gridRt.rect.height;
            if (viewH > 1f && contentH > 1f && contentH < viewH - 2f)
            {
                // 内容未撑满视口时垂直居中
                gridRt.anchorMin = new Vector2(0f, 0.5f);
                gridRt.anchorMax = new Vector2(1f, 0.5f);
                gridRt.pivot = new Vector2(0.5f, 0.5f);
                gridRt.anchoredPosition = Vector2.zero;
            }
            else
            {
                gridRt.anchorMin = new Vector2(0f, 1f);
                gridRt.anchorMax = new Vector2(1f, 1f);
                gridRt.pivot = new Vector2(0.5f, 1f);
                gridRt.anchoredPosition = Vector2.zero;
                scroll.verticalNormalizedPosition = 1f;
            }
        }

        static void ConfigureHorizontalLayout(HorizontalLayoutGroup layout, float spacing)
        {
            layout.spacing = spacing;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(12, 12, 10, 10);
        }

        static Image CreatePanel(string name, Transform parent, Color bg, Color border)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            var outline = go.GetComponent<Outline>();
            outline.effectColor = border;
            outline.effectDistance = new Vector2(1f, -1f);
            return img;
        }

        static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static Button CreateActionButton(
            Transform parent,
            string label,
            Color bg,
            Color textColor,
            Vector2 anchoredPosition,
            Vector2 size,
            bool interactable,
            System.Action onClick)
        {
            return CreateAnchoredActionButton(
                parent, label, bg, textColor,
                new Vector2(0.5f, 0.5f), size, interactable, onClick,
                anchoredPosition);
        }

        Button CreateButton1(
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 size,
            bool interactable,
            System.Action onClick,
            Vector2? anchoredPosition = null)
        {
            return CreateCatalogActionButton(
                parent, label, anchor, size, interactable, onClick, anchoredPosition,
                _uiIcons != null ? _uiIcons.UiButton1 : null, BtnGreen);
        }

        Button CreateButton6(
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 size,
            bool interactable,
            System.Action onClick,
            Vector2? anchoredPosition = null)
        {
            return CreateCatalogActionButton(
                parent, label, anchor, size, interactable, onClick, anchoredPosition,
                _uiIcons != null ? _uiIcons.UiButton6 : null, BtnGreen);
        }

        Button CreateButton2(
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 size,
            bool interactable,
            System.Action onClick,
            Vector2? anchoredPosition = null)
        {
            return CreateCatalogActionButton(
                parent, label, anchor, size, interactable, onClick, anchoredPosition,
                _uiIcons != null ? _uiIcons.UiButton2 : null, BtnNeutral);
        }

        Button CreateButton3(
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 size,
            bool interactable,
            System.Action onClick,
            Vector2? anchoredPosition = null)
        {
            return CreateCatalogActionButton(
                parent, label, anchor, size, interactable, onClick, anchoredPosition,
                _uiIcons != null ? _uiIcons.UiButton3 : null, BtnNeutral);
        }

        Button CreateCatalogActionButton(
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 size,
            bool interactable,
            System.Action onClick,
            Vector2? anchoredPosition,
            Sprite sprite,
            Color fallbackColor)
        {
            var go = CreateRect(label + "Btn", parent);
            var rt = go;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            if (anchoredPosition.HasValue)
                rt.anchoredPosition = anchoredPosition.Value;

            var img = go.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = interactable ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            else
            {
                img.color = interactable ? fallbackColor : DisabledCardBg;
            }

            var text = CreateStaticText(go, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);
            text.color = interactable
                ? new Color(0.96f, 0.92f, 0.78f, 1f)
                : new Color(0.55f, 0.55f, 0.58f, 1f);

            var group = go.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = interactable;
            if (interactable)
            {
                var hover = go.gameObject.AddComponent<CampBuildingHoverView>();
                hover.Bind(rt, group, HubButtonHoverScale, hideWhenIdle: false);
            }

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.interactable = interactable;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        static Button CreateAnchoredActionButton(
            Transform parent,
            string label,
            Color bg,
            Color textColor,
            Vector2 anchor,
            Vector2 size,
            bool interactable,
            System.Action onClick,
            Vector2? anchoredPosition = null)
        {
            var go = CreatePanel(label + "Btn", parent, bg, Border);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            if (anchoredPosition.HasValue)
                rt.anchoredPosition = anchoredPosition.Value;
            var text = CreateStaticText(go.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);
            text.color = textColor;
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
            btn.interactable = interactable;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        static Text CreateLabel(RectTransform parent, string text, int size, Color color, Vector2 pos, Vector2 sizeDelta)
        {
            var label = CreateStaticText(parent, text, size, FontStyle.Bold, TextAnchor.MiddleCenter);
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            label.color = color;
            return label;
        }

        Text CreateBandText(RectTransform parent, string text, int size, float minY, float maxY, Color color)
        {
            var label = CreateStaticText(parent, text, size, FontStyle.Normal, TextAnchor.MiddleCenter);
            StretchBand(label.rectTransform, minY, maxY);
            label.rectTransform.offsetMin = new Vector2(32f, 2f);
            label.rectTransform.offsetMax = new Vector2(-32f, -2f);
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static Text CreateStaticText(Transform parent, string text, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = TextMain;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.supportRichText = true;
            label.raycastTarget = false;
            return label;
        }

        static void ClearChildren(RectTransform row)
        {
            if (row == null)
                return;

            for (var i = row.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(row.GetChild(i).gameObject);
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);

            if (!visible)
            {
                _tooltip?.Hide();
                _screen = AltarScreen.Hub;
            }
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void StretchWithMargin(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        static void SetAnchoredBand(RectTransform rt, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(0f, minY);
            rt.anchorMax = new Vector2(1f, maxY);
            rt.offsetMin = new Vector2(16f, 0f);
            rt.offsetMax = new Vector2(-16f, 0f);
        }

        static void StretchBand(RectTransform rt, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(0f, minY);
            rt.anchorMax = new Vector2(1f, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AnchorDetailText(RectTransform rt, float minY, float maxY)
        {
            rt.anchorMin = new Vector2(0.05f, minY);
            rt.anchorMax = new Vector2(0.95f, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
