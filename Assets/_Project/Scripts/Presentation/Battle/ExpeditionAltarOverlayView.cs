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

        const float SummonCardScale = 1.02f;
        const float SummonReplaceCardScale = 0.78f;
        const float UpgradeCardScale = 0.92f;
        const float HubTileMinHeight = 280f;
        const float ActionButtonHeight = 56f;
        const int LayoutVersion = 13;
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

        // 三选项：竖直约 1.5× 盖住模板框；休息回复略左收，与左中间距一致
        static readonly HubNormRect HubZoneSummon = new(0.095f, 0.175f, 0.350f, 0.590f);
        static readonly HubNormRect HubZoneDistribute = new(0.375f, 0.175f, 0.630f, 0.590f);
        static readonly HubNormRect HubZoneRest = new(0.645f, 0.175f, 0.900f, 0.590f);
        static readonly HubNormRect HubZoneXpIcon = new(0.415f, 0.900f, 0.455f, 0.960f);
        static readonly HubNormRect HubZoneXpText = new(0.458f, 0.905f, 0.520f, 0.955f);
        static readonly HubNormRect HubZoneGoldIcon = new(0.530f, 0.895f, 0.575f, 0.960f);
        static readonly HubNormRect HubZoneGoldText = new(0.578f, 0.905f, 0.645f, 0.955f);
        static readonly HubNormRect HubZoneTitleLeft = new(0.03f, 0.90f, 0.20f, 0.97f);
        // 离开：保持此前已对齐位置，不再挪动
        static readonly HubNormRect HubZoneLeave = new(0.755f, 0.018f, 0.958f, 0.128f);

        const float SummonCardWidth = 208f;
        const float SummonCardHeight = 292f;
        const float SummonReplaceCardWidth = 156f;
        const float SummonReplaceCardHeight = 220f;
        const int SummonCollectionColumns = 7;
        const float SummonCollectionGridSpacing = 10f;
        const float SummonReplaceCardSpacing = 18f;
        const float UpgradeCardWidth = 210f;
        const float UpgradeCardHeight = 300f;
        const int UpgradeCardColumns = 3;
        const float UpgradeCardSpacing = 12f;

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
        Text _titleLeftText;
        int _builtVersion = -1;


        RectTransform _summonMemberRow;
        RectTransform _summonCollectionHost;
        RectTransform _summonCollectionGrid;
        RectTransform _summonReplaceHost;
        RectTransform _summonReplaceRow;
        Text _summonReplaceLabel;
        Text _summonPreviewText;
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

        struct RestOptionUi
        {
            public Image Row;
            public CanvasGroup Group;
            public Button Button;
            public Text Icon;
            public Text Title;
            public Text Cost;
            public Text Detail;
        }

        RestOptionUi _restGoldOption;
        RestOptionUi _restXpOption;
        RectTransform _restSummaryMembersRow;
        Text _restHintText;

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
            if (_navBar != null && _screen != AltarScreen.Hub)
                _navBar.SetAsLastSibling();
            RefreshHeader(run);
            UpdateBackLabel();
            if (_screen == AltarScreen.RestRecovery && _restGoldOption.Row != null)
            {
                ApplyRestRecoveryState(run);
                return;
            }

            RebuildContent(run);
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
                AltarScreen.DistributeXp or AltarScreen.SummonCards or AltarScreen.RestRecovery => "← 返回祭坛",
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

            if (_backButton != null)
                _backButton.gameObject.SetActive(_screen != AltarScreen.Hub);
            if (_navBar != null)
                _navBar.gameObject.SetActive(_screen != AltarScreen.Hub);
        }

        void RebuildContent(ExpeditionRunState run)
        {
            _tooltip?.Hide();
            ClearRestRecoveryRefs();

            if (_screen == AltarScreen.UpgradeCards && _upgradeCardScroll != null)
                _upgradeCardScrollY = ScrollRectNavigation.CaptureVertical(_upgradeCardScroll);

            if (_upgradeCardLayoutRoutine != null)
            {
                StopCoroutine(_upgradeCardLayoutRoutine);
                _upgradeCardLayoutRoutine = null;
            }

            ClearChildren(_contentHost);
            _upgradeCardScroll = null;
            _upgradeCardGrid = null;

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
        }

        void CreateHubOptionButton(
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

            var iconText = CreateStaticText(visualGo, icon, 42, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(iconText.rectTransform, 0.62f, 0.92f);
            iconText.color = TitleGold;

            var titleText = CreateStaticText(visualGo, title, 26, FontStyle.Bold, TextAnchor.UpperCenter);
            StretchBand(titleText.rectTransform, 0.42f, 0.62f);
            titleText.color = TextMain;

            var descText = CreateStaticText(visualGo, desc, 15, FontStyle.Normal, TextAnchor.UpperCenter);
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
        }

        void BuildRestRecoveryScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "休息回复", "选择一种方式，为全体角色恢复 25% 最大生命");
            BuildPartyRestSummaryShell(parent);

            var list = CreateVerticalList(parent, "RestOptions", 0.14f, 0.62f, 16f);
            _restGoldOption = CreateRestOptionRow(
                list,
                "◎",
                "金币休息",
                $"花费 {ExpeditionAltarUpgradeRules.RestHealGoldCost} 金币",
                "",
                false,
                OnRestHealWithGold);

            _restXpOption = CreateRestOptionRow(
                list,
                "★",
                "经验休息",
                $"花费 {ExpeditionAltarUpgradeRules.RestHealXpCost} 经验",
                "",
                false,
                OnRestHealWithXp);

            _restHintText = CreateBandText(parent, "", 16, 0.08f, 0.12f, TextMuted);
            _restHintText.alignment = TextAnchor.MiddleCenter;

            ApplyRestRecoveryState(run);
        }

        void OnRestHealWithGold()
        {
            _session.AltarRestHealWithGold();
            var run = _session.Expedition.Run;
            RefreshHeader(run);
            ApplyRestRecoveryState(run);
        }

        void OnRestHealWithXp()
        {
            _session.AltarRestHealWithXp();
            var run = _session.Expedition.Run;
            RefreshHeader(run);
            ApplyRestRecoveryState(run);
        }

        void ApplyRestRecoveryState(ExpeditionRunState run)
        {
            if (_restGoldOption.Row == null)
                return;

            RebuildPartyRestSummaryContent(run);

            var needsHeal = ExpeditionAltarUpgradeRules.PartyHasRestHealableMember(run);
            var canGold = needsHeal && run.Gold >= ExpeditionAltarUpgradeRules.RestHealGoldCost;
            var canXp = needsHeal && run.SharedXpPool >= ExpeditionAltarUpgradeRules.RestHealXpCost;

            SetRestOptionUiState(
                ref _restGoldOption,
                canGold,
                needsHeal
                    ? $"全队回复 {ExpeditionAltarUpgradeRules.RestHealPercent}% 最大生命\n当前金币：{run.Gold}"
                    : "全队已满血，无需回复");

            SetRestOptionUiState(
                ref _restXpOption,
                canXp,
                needsHeal
                    ? $"全队回复 {ExpeditionAltarUpgradeRules.RestHealPercent}% 最大生命\n当前经验：{run.SharedXpPool}"
                    : "全队已满血，无需回复");

            if (_restHintText != null)
                _restHintText.text = needsHeal ? "点击后立即生效，不会离开祭坛" : "当前无需回复";
        }

        static void SetRestOptionUiState(ref RestOptionUi ui, bool canBuy, string detail)
        {
            if (ui.Row == null)
                return;

            ui.Row.color = canBuy ? CardBg : DisabledCardBg;
            if (ui.Icon != null)
                ui.Icon.color = canBuy ? TitleGold : TextMuted;
            if (ui.Title != null)
                ui.Title.color = canBuy ? TextMain : TextMuted;
            if (ui.Detail != null)
            {
                ui.Detail.text = detail;
                ui.Detail.color = canBuy ? AccentGreen : TextMuted;
            }

            if (ui.Button != null)
                ui.Button.interactable = canBuy;
            if (ui.Group != null)
            {
                ui.Group.alpha = canBuy ? 1f : 0.42f;
                ui.Group.interactable = canBuy;
                ui.Group.blocksRaycasts = canBuy;
            }
        }

        void ClearRestRecoveryRefs()
        {
            _restGoldOption = default;
            _restXpOption = default;
            _restSummaryMembersRow = null;
            _restHintText = null;
        }

        void BuildPartyRestSummaryShell(RectTransform parent)
        {
            var summaryHost = CreateRect("RestSummary", parent);
            SetAnchoredBand(summaryHost, 0.62f, 0.74f);
            var summaryBg = CreatePanel("SummaryBg", summaryHost, CardBg, Border);
            StretchFull(summaryBg.GetComponent<RectTransform>());

            var rowGo = new GameObject("Members", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(summaryBg.transform, false);
            _restSummaryMembersRow = rowGo.GetComponent<RectTransform>();
            StretchFull(_restSummaryMembersRow);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
        }

        void RebuildPartyRestSummaryContent(ExpeditionRunState run)
        {
            if (_restSummaryMembersRow == null)
                return;

            ClearChildren(_restSummaryMembersRow);
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

                ExpeditionPartyStatsRules.GetDisplayHp(
                    member, run.Party, run.Relics, run.RelicGrowthTiers, out var hp, out var maxHp);
                var healAmount = hp > 0
                    ? ExpeditionAltarUpgradeRules.ComputeRestHealAmount(member, run)
                    : 0;
                var afterHp = hp > 0 ? System.Math.Min(maxHp, hp + healAmount) : 0;

                var tile = CreatePanel("MemberSummary", _restSummaryMembersRow, new Color(0.12f, 0.14f, 0.19f, 0.92f), Border);
                var name = CreateStaticText(tile.transform, member.DisplayName, 18, FontStyle.Bold, TextAnchor.UpperCenter);
                StretchBand(name.rectTransform, 0.58f, 0.92f);
                name.color = TextMain;

                var hpText = CreateStaticText(tile.transform,
                    hp <= 0 ? "已倒下" : $"♥ {hp} / {maxHp}",
                    16, FontStyle.Normal, TextAnchor.MiddleCenter);
                StretchBand(hpText.rectTransform, 0.28f, 0.58f);
                hpText.color = member.Hp <= 0 ? TextMuted : TextMain;

                var preview = CreateStaticText(tile.transform,
                    hp <= 0 ? "—" : $"回复后：{afterHp} / {maxHp}",
                    15, FontStyle.Normal, TextAnchor.LowerCenter);
                StretchBand(preview.rectTransform, 0.06f, 0.28f);
                preview.color = hp >= maxHp ? TextMuted : AccentGreen;
            }
        }

        RestOptionUi CreateRestOptionRow(
            RectTransform parent,
            string icon,
            string title,
            string costLine,
            string detail,
            bool canBuy,
            System.Action onClick)
        {
            var go = CreatePanel("RestOption", parent, canBuy ? CardBg : DisabledCardBg, Border);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 112f;

            var iconText = CreateStaticText(go.transform, icon, 34, FontStyle.Normal, TextAnchor.MiddleCenter);
            iconText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            iconText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            iconText.rectTransform.pivot = new Vector2(0f, 0.5f);
            iconText.rectTransform.sizeDelta = new Vector2(72f, 72f);
            iconText.rectTransform.anchoredPosition = new Vector2(12f, 0f);
            iconText.color = TitleGold;

            var titleText = CreateStaticText(go.transform, title, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            titleText.rectTransform.anchorMax = new Vector2(0.52f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(88f, 8f);
            titleText.rectTransform.offsetMax = new Vector2(-8f, -8f);
            titleText.color = TextMain;
            titleText.alignment = TextAnchor.LowerLeft;

            var costText = CreateStaticText(go.transform, costLine, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            costText.rectTransform.anchorMin = new Vector2(0f, 0f);
            costText.rectTransform.anchorMax = new Vector2(0.52f, 0.5f);
            costText.rectTransform.offsetMin = new Vector2(88f, 8f);
            costText.rectTransform.offsetMax = new Vector2(-8f, -8f);
            costText.color = TextMuted;
            costText.alignment = TextAnchor.UpperLeft;

            var detailText = CreateStaticText(go.transform, detail, 17, FontStyle.Normal, TextAnchor.MiddleRight);
            detailText.rectTransform.anchorMin = new Vector2(0.52f, 0f);
            detailText.rectTransform.anchorMax = new Vector2(1f, 1f);
            detailText.rectTransform.offsetMin = new Vector2(8f, 12f);
            detailText.rectTransform.offsetMax = new Vector2(-16f, -12f);
            detailText.color = AccentGreen;
            detailText.alignment = TextAnchor.MiddleRight;

            var ui = new RestOptionUi
            {
                Row = go,
                Icon = iconText,
                Title = titleText,
                Cost = costText,
                Detail = detailText,
                Group = go.gameObject.AddComponent<CanvasGroup>(),
                Button = go.gameObject.AddComponent<Button>()
            };
            ui.Button.targetGraphic = go;
            ui.Button.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(ui.Button);
            SetRestOptionUiState(ref ui, canBuy, detail);
            return ui;
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
            var cost = ExpeditionAltarUpgradeRules.GetHpPlus5Cost(run.Modifiers);
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

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
                        || !CardUpgradeRules.CanUpgrade(member, template.DeckInstanceId, template.DisplayName))
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
                _upgradeCardButton.interactable = false;
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
            _upgradeCardButton.interactable = canBuy;
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

        void BuildSummonScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "召唤卡牌", null, 0.90f, 0.98f);
            var intro = CreateBandText(
                parent,
                "从军营收藏中取出卡牌加入远征卡组。每个角色可单独确认取出一张；确认后该角色卡牌变暗不可再选。点「离开祭坛」才结束访问。",
                17, 0.82f, 0.90f, TextMuted);
            intro.alignment = TextAnchor.UpperCenter;
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;

            var memberRowGo = new GameObject("Members", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            memberRowGo.transform.SetParent(parent, false);
            _summonMemberRow = memberRowGo.GetComponent<RectTransform>();
            SetAnchoredBand(_summonMemberRow, 0.72f, 0.82f);
            var memberLayout = memberRowGo.GetComponent<HorizontalLayoutGroup>();
            memberLayout.spacing = 16f;
            memberLayout.padding = new RectOffset(24, 24, 0, 0);
            memberLayout.childAlignment = TextAnchor.MiddleCenter;
            memberLayout.childControlWidth = false;
            memberLayout.childControlHeight = false;

            var collectionLabel = CreateBandText(parent, "军营收藏 — 选择要取出的卡牌", 18, 0.68f, 0.72f, TextMuted);
            collectionLabel.alignment = TextAnchor.MiddleLeft;

            _summonCollectionHost = CreateRect("CollectionHost", parent);
            SetAnchoredBand(_summonCollectionHost, 0.10f, 0.68f);
            _summonCollectionGrid = BuildScrollGrid(_summonCollectionHost, SummonCollectionColumns,
                new Vector2(SummonCardWidth, SummonCardHeight), SummonCollectionGridSpacing);

            _summonReplaceHost = CreateRect("ReplaceHost", parent);
            SetAnchoredBand(_summonReplaceHost, 0.04f, 0.44f);
            var replaceBg = CreatePanel("ReplaceBg", _summonReplaceHost, CardBg, Border);
            StretchFull(replaceBg.GetComponent<RectTransform>());
            _summonReplaceLabel = CreateStaticText(replaceBg.transform,
                "卡组已满，请选择要替换的卡牌", 17, FontStyle.Bold, TextAnchor.UpperLeft);
            var replaceLabelRt = _summonReplaceLabel.rectTransform;
            replaceLabelRt.anchorMin = new Vector2(0f, 0.82f);
            replaceLabelRt.anchorMax = new Vector2(1f, 1f);
            replaceLabelRt.offsetMin = new Vector2(16f, 0f);
            replaceLabelRt.offsetMax = new Vector2(-16f, -4f);
            _summonReplaceLabel.color = TextMuted;
            _summonReplaceLabel.alignment = TextAnchor.MiddleLeft;

            var replaceScrollHost = CreateRect("ReplaceScrollHost", replaceBg.transform);
            var replaceScrollRt = replaceScrollHost.GetComponent<RectTransform>();
            replaceScrollRt.anchorMin = Vector2.zero;
            replaceScrollRt.anchorMax = new Vector2(1f, 0.82f);
            replaceScrollRt.offsetMin = new Vector2(10f, 12f);
            replaceScrollRt.offsetMax = new Vector2(-10f, -4f);
            _summonReplaceRow = BuildScrollRowInternal(replaceScrollRt, 0f, 1f, horizontal: true,
                spacing: SummonReplaceCardSpacing, padding: new RectOffset(16, 16, 12, 12));
            _summonReplaceHost.gameObject.SetActive(false);

            var bottomHost = CreateRect("SummonBottom", parent);
            SetAnchoredBand(bottomHost, 0.02f, 0.10f);

            _summonPreviewText = CreateStaticText(bottomHost, "从收藏中选择一张尚未取出的卡牌。", 17,
                FontStyle.Normal, TextAnchor.MiddleLeft);
            var previewRt = _summonPreviewText.rectTransform;
            previewRt.anchorMin = new Vector2(0f, 0f);
            previewRt.anchorMax = new Vector2(0.58f, 1f);
            previewRt.offsetMin = new Vector2(8f, 0f);
            previewRt.offsetMax = new Vector2(-8f, 0f);
            _summonPreviewText.color = TextMuted;
            _summonPreviewText.horizontalOverflow = HorizontalWrapMode.Wrap;

            _summonConfirmButton = CreateAnchoredActionButton(
                bottomHost, "确认取出", BtnGreen, AccentGreen,
                new Vector2(1f, 0.5f), new Vector2(300f, ActionButtonHeight), false,
                ConfirmActiveMemberSummon);
            var confirmRt = _summonConfirmButton.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(1f, 0.5f);
            confirmRt.anchorMax = new Vector2(1f, 0.5f);
            confirmRt.pivot = new Vector2(1f, 0.5f);
            confirmRt.anchoredPosition = new Vector2(-8f, 0f);

            if (_activeMemberIndex >= run.Party.Count)
                _activeMemberIndex = 0;

            RebuildSummonContent(run);
        }

        void RebuildSummonContent(ExpeditionRunState run)
        {
            if (_summonMemberRow == null)
                return;

            ClearChildren(_summonMemberRow);
            ClearChildren(_summonCollectionGrid);
            ClearChildren(_summonReplaceRow);

            for (var i = 0; i < run.Party.Count; i++)
            {
                var index = i;
                var member = run.Party[i];
                var active = index == _activeMemberIndex;
                CreateMemberTab(member, active, () =>
                {
                    _activeMemberIndex = index;
                    RebuildSummonContent(run);
                });
            }

            if (run.Party.Count == 0)
                return;

            var activeMember = run.Party[_activeMemberIndex];
            RebuildSummonCollection(activeMember);
            RebuildSummonReplace(activeMember);
            RefreshSummonStatus(activeMember);
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
            UpdateSummonCollectionBand(showReplace);

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

        void UpdateSummonCollectionBand(bool showReplace)
        {
            if (_summonCollectionHost == null)
                return;

            SetAnchoredBand(_summonCollectionHost, showReplace ? 0.46f : 0.10f, 0.68f);
        }

        void RebuildSummonCollection(PartyMemberSnapshot member)
        {
            var run = _session.Expedition.Run;
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var locked = draft.Confirmed;
            var needsReplace = !locked && ExpeditionRunDeckRules.NeedsReplace(config, member);
            var indices = ExpeditionRunDeckRules.GetAvailableCollectionIndices(run, member);

            if (locked)
            {
                // 已取出的卡消失；剩余收藏变暗不可再选。
                foreach (var index in indices)
                {
                    var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(config, run, member, index);
                    if (template == null)
                        continue;

                    SpawnSummonCard(_summonCollectionGrid, template, member.CharacterDefinitionId,
                        selected: false, onClick: null, dimmed: true);
                }

                if (_summonCollectionGrid.childCount == 0)
                {
                    var done = CreateStaticText(_summonCollectionGrid,
                        $"{member.DisplayName} 本趟已取出卡牌，可切换其他角色继续，或离开祭坛。",
                        18, FontStyle.Normal, TextAnchor.MiddleCenter);
                    StretchFull(done.rectTransform);
                    done.color = TextMuted;
                }

                return;
            }

            if (indices.Count == 0)
            {
                var empty = CreateStaticText(_summonCollectionGrid, "该角色军营收藏中已无可取出的卡牌。", 18,
                    FontStyle.Normal, TextAnchor.MiddleCenter);
                StretchFull(empty.rectTransform);
                empty.color = TextMuted;
                return;
            }

            foreach (var index in indices)
            {
                var cardId = ExpeditionRunDeckCatalog.GetCampCollectionCardId(run, member, index);
                if (string.IsNullOrEmpty(cardId))
                    continue;

                var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(config, run, member, index);
                var capturedIndex = index;
                var selected = draft.CollectionCardIndex == capturedIndex;
                SpawnSummonCard(_summonCollectionGrid, template, member.CharacterDefinitionId, selected, () =>
                {
                    var current = GetDraft(member);
                    if (current.Confirmed)
                        return;
                    var collectionIndex = current.CollectionCardIndex == capturedIndex ? -1 : capturedIndex;
                    var replaceKey = needsReplace && collectionIndex >= 0 ? current.ReplaceDeckCardKey : "";
                    _session.SetCardAltarDraft(member.CharacterDefinitionId, collectionIndex, replaceKey);
                    RebuildSummonContent(_session.Expedition.Run);
                });
            }

            var scroll = _summonCollectionGrid != null
                ? _summonCollectionGrid.GetComponentInParent<ScrollRect>()
                : null;
            FinalizeScrollGridContent(
                _summonCollectionGrid,
                scroll,
                _summonCollectionGrid != null ? _summonCollectionGrid.childCount : 0);
        }

        void RefreshSummonStatus(PartyMemberSnapshot member)
        {
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);

            if (draft.Confirmed)
            {
                _summonPreviewText.text =
                    $"{member.DisplayName} 本趟已取出卡牌。可切换其他角色继续取出，或点「离开祭坛」。";
            }
            else if (!draft.HasSelection)
            {
                _summonPreviewText.text = needsReplace
                    ? "从收藏中选择一张卡牌；卡组已满时将提示选择替换目标。"
                    : "从收藏中选择一张卡牌（将直接加入卡组）。";
            }
            else
            {
                var newTemplate = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(
                    config, _session.Expedition.Run, member, draft.CollectionCardIndex);
                var newName = newTemplate?.DisplayName ?? "未知卡牌";
                if (needsReplace)
                {
                    CardTemplate oldTemplate = null;
                    if (!string.IsNullOrEmpty(draft.ReplaceDeckCardKey)
                        && ExpeditionRunDeckRules.TryFindMemberDeckEntryByKey(config, member, draft.ReplaceDeckCardKey, out var oldEntry))
                    {
                        oldTemplate = oldEntry.Template;
                    }

                    var oldName = oldTemplate?.DisplayName ?? "（请选择要替换的卡牌）";
                    _summonPreviewText.text = string.IsNullOrEmpty(draft.ReplaceDeckCardKey)
                        ? $"将取出：{newName}\n请在下方选择卡组中要替换的卡牌。"
                        : $"替换：{oldName}  →  {newName}";
                }
                else
                {
                    _summonPreviewText.text = $"将加入卡组：{newName}";
                }
            }

            if (_summonConfirmButton != null)
                _summonConfirmButton.interactable = HasValidDraftForMember(member);
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

        void CreateMemberTab(PartyMemberSnapshot member, bool active, System.Action onClick)
        {
            var go = CreatePanel("MemberTab", _summonMemberRow, active ? AccentGreenBg : CardBg, Border);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 220f;
            le.preferredHeight = 56f;
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            var label = CreateStaticText(go.transform, member.DisplayName, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(label.rectTransform);
            label.color = TextMain;
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
            bool dimmed = false)
        {
            var holder = CreateCardHolder(parent, selected && !dimmed, width, height);
            if (onClick != null && !dimmed)
            {
                var btn = holder.gameObject.AddComponent<Button>();
                btn.targetGraphic = holder;
                btn.onClick.AddListener(() => onClick.Invoke());
                UiAudioHooks.WireButton(btn);
            }

            ScrollRectNavigation.WireForwarding(holder.gameObject);
            SpawnCardVisual(holder.transform, template, ownerId, scale);

            if (dimmed)
            {
                holder.color = new Color(0.12f, 0.13f, 0.16f, 0.92f);
                var cg = holder.gameObject.GetComponent<CanvasGroup>();
                if (cg == null)
                    cg = holder.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0.42f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }
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
            _screen = _screen switch
            {
                AltarScreen.SummonCards or AltarScreen.DistributeXp or AltarScreen.RestRecovery => AltarScreen.Hub,
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
        }

        void ApplyChromeForScreen()
        {
            var hub = _screen == AltarScreen.Hub;
            var distributeFamily = IsDistributeFamilyScreen();
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
                else if (distributeFamily && _uiIcons != null && _uiIcons.UiEventPlate != null)
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

            // 全祭坛界面：XP/金币/离开与一级 UI 同热区
            if (_titleLeftText != null)
            {
                ApplyHubNormRect(_titleLeftText.rectTransform, HubZoneTitleLeft);
                _titleLeftText.gameObject.SetActive(true);
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

            if (_contentHost != null)
            {
                if (hub)
                {
                    _contentHost.gameObject.SetActive(false);
                }
                else
                {
                    SetAnchoredBand(_contentHost, 0.13f, 0.74f);
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
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
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
            if (_uiIcons != null && _uiIcons.UiButton1 != null)
            {
                img.sprite = _uiIcons.UiButton1;
                img.color = interactable ? Color.white : new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            else
            {
                img.color = interactable ? BtnGreen : DisabledCardBg;
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
