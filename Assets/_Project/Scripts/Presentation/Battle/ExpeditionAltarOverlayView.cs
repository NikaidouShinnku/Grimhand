using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
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
        const float UpgradeCardScale = 0.74f;
        const float HubTileMinHeight = 280f;
        const float ActionButtonHeight = 56f;
        const float SummonCardWidth = 208f;
        const float SummonCardHeight = 292f;
        const float SummonReplaceCardWidth = 156f;
        const float SummonReplaceCardHeight = 220f;
        const int SummonCollectionColumns = 7;
        const float SummonCollectionGridSpacing = 10f;
        const float SummonReplaceCardSpacing = 18f;
        const float UpgradeCardWidth = 188f;
        const float UpgradeCardHeight = 272f;

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

            var layer = run.CardAltar?.SourceLayer ?? 1;
            _layerHeaderText.text = $"第 {layer} 层 · {ResolveRegionName(layer)}";
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

            ClearChildren(_contentHost);
            _upgradeCardScroll = null;
            _upgradeCardGrid = null;

            switch (_screen)
            {
                case AltarScreen.Hub:
                    BuildHub(_contentHost);
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

        void BuildHub(RectTransform parent)
        {
            AddTitle(parent, "祭坛", "选择一项祭坛服务", 0.86f, 1f, 0.76f, 0.86f);
            var grid = CreateGrid(parent, "HubGrid", 3, 1, 0.06f, 0.72f, 20f, new Vector2(340f, 300f));
            CreateHubTile(grid, "◫", "召唤卡牌", "从收藏取出卡牌，加入或替换卡组", () => _screen = AltarScreen.SummonCards);
            CreateHubTile(grid, "★", "分配经验", "花费经验强化角色与卡牌", () => _screen = AltarScreen.DistributeXp);
            CreateHubTile(grid, "♥", "休息回复", "花费金币或经验，恢复全队生命", () => _screen = AltarScreen.RestRecovery);
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
            var grid = CreateGrid(parent, "XpGrid", 2, 2, 0.18f, 0.83f, 16f, new Vector2(420f, 230f));
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
            var list = CreateVerticalList(parent, "HpList", 0.12f, 0.78f, 12f);
            var cost = ExpeditionAltarUpgradeRules.GetHpPlus5Cost(run.Modifiers);
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

                var afterMax = member.MaxHp + ExpeditionAltarUpgradeRules.HpPlus5Amount;
                var afterHp = System.Math.Min(afterMax, member.Hp + ExpeditionAltarUpgradeRules.HpPlus5Amount);
                var healNote = member.Hp < member.MaxHp ? $"（并回复 {ExpeditionAltarUpgradeRules.HpPlus5Amount}HP）" : "";
                var sub = $"{member.Hp} / {member.MaxHp} HP";
                var right = $"{cost} XP → HP +{ExpeditionAltarUpgradeRules.HpPlus5Amount}\n升级后：{afterHp} / {afterMax} HP{healNote}";
                var memberId = member.CharacterDefinitionId;
                CreateMemberRow(list, member, sub, right, run.SharedXpPool >= cost,
                    () => { _session.UpgradeAltarMemberHp(memberId); Refresh(); });
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

            var btn = CreateActionButton(center, $"花费 {cost} XP 升级", BtnGreen, AccentGreen,
                new Vector2(0f, -190f), new Vector2(380f, ActionButtonHeight), canBuy, onBuy);
            btn.gameObject.SetActive(cost > 0);

            var maxUpgrades = label.Contains("抽牌") || label.Contains("手牌")
                ? ExpeditionAltarUpgradeRules.MaxHandLimitUpgrades
                : ExpeditionAltarUpgradeRules.MaxEnergyCapUpgrades;
            CreateLabel(center, $"剩余可升级次数：{remaining} / {maxUpgrades}",
                18, TextMuted, new Vector2(0f, -250f), new Vector2(420f, 30f));
        }

        void BuildUpgradeCardsScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "强化卡牌", "选择一张可强化的卡牌查看详情");

            var body = CreateRect("CardUpgradeBody", parent);
            var bodyRt = body.GetComponent<RectTransform>();
            SetAnchoredBand(body, 0.04f, 0.80f);

            var gridHost = CreateRect("GridHost", bodyRt);
            var gridHostRt = gridHost.GetComponent<RectTransform>();
            gridHostRt.anchorMin = new Vector2(0f, 0f);
            gridHostRt.anchorMax = new Vector2(0.52f, 1f);
            gridHostRt.offsetMin = Vector2.zero;
            gridHostRt.offsetMax = Vector2.zero;
            _upgradeCardGrid = BuildScrollGrid(gridHost.transform, 3, new Vector2(UpgradeCardWidth, UpgradeCardHeight));
            _upgradeCardScroll = _upgradeCardGrid != null
                ? _upgradeCardGrid.GetComponentInParent<ScrollRect>()
                : null;

            _upgradeCardDetail = CreatePanel("Detail", bodyRt, CardBg, Border).GetComponent<RectTransform>();
            var detailRt = _upgradeCardDetail;
            detailRt.anchorMin = new Vector2(0.54f, 0f);
            detailRt.anchorMax = Vector2.one;
            detailRt.offsetMin = new Vector2(12f, 0f);
            detailRt.offsetMax = Vector2.zero;

            _upgradeCardDetailTitle = CreateStaticText(_upgradeCardDetail, "", 22, FontStyle.Bold, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardDetailTitle.rectTransform, 0.82f, 0.98f);
            _upgradeCardCurrentText = CreateStaticText(_upgradeCardDetail, "", 16, FontStyle.Normal, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardCurrentText.rectTransform, 0.52f, 0.8f);
            _upgradeCardNextText = CreateStaticText(_upgradeCardDetail, "", 16, FontStyle.Normal, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardNextText.rectTransform, 0.28f, 0.5f);
            _upgradeCardMetaText = CreateStaticText(_upgradeCardDetail, "", 16, FontStyle.Normal, TextAnchor.UpperLeft);
            AnchorDetailText(_upgradeCardMetaText.rectTransform, 0.14f, 0.26f);
            _upgradeCardButton = CreateActionButton(_upgradeCardDetail, "强化", BtnGreen, AccentGreen,
                new Vector2(0f, 48f), new Vector2(280f, ActionButtonHeight), false, ConfirmCardUpgrade);

            ClearChildren(_upgradeCardGrid);
            var config = _session.Expedition.Config;
            foreach (var member in run.Party)
            {
                if (member == null)
                    continue;

                foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
                {
                    var template = entry.Template;
                    if (template == null || !CardUpgradeRules.CanUpgrade(member, template.DeckInstanceId, template.DisplayName))
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
                }
            }

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
            var btnLabel = _upgradeCardButton.GetComponentInChildren<Text>();
            if (btnLabel != null)
                btnLabel.text = $"↑  强化（{cost} XP）";
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
            var go = CreatePanel("Tile", parent, CardBg, Border);
            var inGrid = parent.GetComponent<GridLayoutGroup>() != null;

            var le = go.gameObject.AddComponent<LayoutElement>();
            if (!inGrid)
            {
                le.flexibleWidth = 1f;
                le.flexibleHeight = 1f;
                le.minWidth = 420f;
                le.minHeight = HubTileMinHeight;
            }

            var iconText = CreateStaticText(go.transform, icon, 48, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(iconText.rectTransform, 0.66f, 0.92f);
            iconText.color = TitleGold;

            var titleText = CreateStaticText(go.transform, title, 28, FontStyle.Bold, TextAnchor.UpperCenter);
            StretchBand(titleText.rectTransform, 0.46f, 0.64f);
            titleText.color = TextMain;

            var descText = CreateStaticText(go.transform, desc, 17, FontStyle.Normal, TextAnchor.UpperCenter);
            StretchBand(descText.rectTransform, 0.08f, 0.44f);
            descText.color = TextMuted;
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descText.verticalOverflow = VerticalWrapMode.Truncate;

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
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

        void CreateMemberRow(
            RectTransform parent,
            PartyMemberSnapshot member,
            string sub,
            string right,
            bool canBuy,
            System.Action onClick)
        {
            var go = CreatePanel("MemberRow", parent, CardBg, Border);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 96f;

            var badgeColor = ResolveMemberColor(member.CharacterDefinitionId);
            var badge = CreatePanel("Badge", go.transform, badgeColor, badgeColor);
            var badgeRt = badge.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(0f, 0.5f);
            badgeRt.anchorMax = new Vector2(0f, 0.5f);
            badgeRt.pivot = new Vector2(0f, 0.5f);
            badgeRt.sizeDelta = new Vector2(56f, 56f);
            badgeRt.anchoredPosition = new Vector2(16f, 0f);
            CreateStaticText(badge.transform, ResolveMemberBadge(member.CharacterDefinitionId), 24, FontStyle.Bold, TextAnchor.MiddleCenter)
                .color = Color.white;

            var name = CreateStaticText(go.transform, member.DisplayName, 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            name.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            name.rectTransform.anchorMax = new Vector2(0.55f, 1f);
            name.rectTransform.offsetMin = new Vector2(84f, 8f);
            name.rectTransform.offsetMax = new Vector2(-8f, -8f);
            name.color = TextMain;
            name.alignment = TextAnchor.LowerLeft;

            var hp = CreateStaticText(go.transform, $"♥ {sub}", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            hp.rectTransform.anchorMin = new Vector2(0f, 0f);
            hp.rectTransform.anchorMax = new Vector2(0.55f, 0.5f);
            hp.rectTransform.offsetMin = new Vector2(84f, 8f);
            hp.rectTransform.offsetMax = new Vector2(-8f, -8f);
            hp.color = TextMuted;
            hp.alignment = TextAnchor.UpperLeft;

            var action = CreateStaticText(go.transform, right, 18, FontStyle.Normal, TextAnchor.MiddleRight);
            action.rectTransform.anchorMin = new Vector2(0.55f, 0f);
            action.rectTransform.anchorMax = new Vector2(1f, 1f);
            action.rectTransform.offsetMin = new Vector2(8f, 12f);
            action.rectTransform.offsetMax = new Vector2(-16f, -12f);
            action.color = canBuy ? AccentGreen : TextMuted;
            action.alignment = TextAnchor.MiddleRight;

            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
            btn.interactable = canBuy;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
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
            var holder = CreateCardHolder(_upgradeCardGrid, selected, UpgradeCardWidth, UpgradeCardHeight);
            var btn = holder.gameObject.AddComponent<Button>();
            btn.targetGraphic = holder;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            ScrollRectNavigation.WireForwarding(holder.gameObject, _upgradeCardScroll);
            SpawnCardVisual(holder.transform, template, ownerId, UpgradeCardScale);
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

        void SpawnCardVisual(Transform parent, CardTemplate template, string ownerId, float scale)
        {
            if (_cardPrefab == null || template == null)
                return;

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

        static string ResolveRegionName(int layer)
        {
            if (layer >= ExpeditionRegionRules.AbyssStartLayer)
                return "海渊";
            if (layer >= ExpeditionRegionRules.DungeonStartLayer)
                return "地牢";
            return "洞穴";
        }

        static Color ResolveMemberColor(string memberId) => memberId switch
        {
            "char_mage" => new Color(0.35f, 0.45f, 0.82f, 1f),
            "char_ranger" => new Color(0.55f, 0.35f, 0.72f, 1f),
            _ => new Color(0.78f, 0.32f, 0.28f, 1f)
        };

        static string ResolveMemberBadge(string memberId) => memberId switch
        {
            "char_mage" => "P",
            "char_ranger" => "D",
            _ => "W"
        };

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
            if (_built)
                return;

            _built = true;
            var go = new GameObject("ExpeditionAltarOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            go.GetComponent<Image>().color = BgOverlay;

            var panelGo = CreatePanel("Panel", go.transform, PanelBg, Border);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.06f, 0.05f);
            panelRt.anchorMax = new Vector2(0.94f, 0.95f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().raycastTarget = true;
            _tooltip = panelGo.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt, _uiIcons);

            BuildHeader(panelRt);
            _contentHost = CreateRect("Content", panelRt);
            SetAnchoredBand(_contentHost, 0.13f, 0.74f);
            BuildNavBar(panelRt);
            BuildFooter(panelRt);

            // 导航栏必须在内容区之后创建，确保返回按钮可点击。
            _navBar.SetAsLastSibling();

            go.SetActive(false);
        }

        void BuildNavBar(RectTransform panelRt)
        {
            _navBar = CreateRect("NavBar", panelRt);
            SetAnchoredBand(_navBar, 0.74f, 0.84f);

            _backButton = CreateAnchoredActionButton(
                _navBar, "← 返回祭坛", BtnNeutral, TextMain,
                new Vector2(0f, 0.5f), new Vector2(240f, ActionButtonHeight), true, NavigateBack);
            var backRt = _backButton.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.anchoredPosition = new Vector2(8f, 0f);
            _navBar.gameObject.SetActive(false);
        }

        void BuildHeader(RectTransform panelRt)
        {
            var left = CreateStaticText(panelRt, "🔥 祭坛", 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            left.rectTransform.anchorMin = new Vector2(0.03f, 0.86f);
            left.rectTransform.anchorMax = new Vector2(0.28f, 0.97f);
            left.color = TitleGold;

            var currencyHost = CreateRect("CurrencyHeader", panelRt);
            var currencyRt = currencyHost;
            currencyRt.anchorMin = new Vector2(0.5f, 0.865f);
            currencyRt.anchorMax = new Vector2(0.5f, 0.965f);
            currencyRt.pivot = new Vector2(0.5f, 0.5f);
            currencyRt.sizeDelta = new Vector2(520f, 44f);

            var currencyRowGo = new GameObject("CurrencyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            currencyRowGo.transform.SetParent(currencyHost, false);
            var currencyRowRt = currencyRowGo.GetComponent<RectTransform>();
            StretchFull(currencyRowRt);
            var currencyLayout = currencyRowGo.GetComponent<HorizontalLayoutGroup>();
            currencyLayout.spacing = 56f;
            currencyLayout.childAlignment = TextAnchor.MiddleCenter;
            currencyLayout.childControlWidth = false;
            currencyLayout.childControlHeight = true;
            currencyLayout.childForceExpandWidth = false;
            currencyLayout.childForceExpandHeight = true;

            CreateHeaderCurrencyBadge(currencyRowGo.transform, out _xpHeaderIcon, out _xpHeaderText, AccentGreen);
            CreateHeaderCurrencyBadge(currencyRowGo.transform, out _goldHeaderIcon, out _goldHeaderText, TitleGold);

            _layerHeaderText = CreateStaticText(panelRt, "", 18, FontStyle.Normal, TextAnchor.MiddleRight);
            _layerHeaderText.rectTransform.anchorMin = new Vector2(0.58f, 0.86f);
            _layerHeaderText.rectTransform.anchorMax = new Vector2(0.97f, 0.97f);
            _layerHeaderText.color = TextMuted;
        }

        static void CreateHeaderCurrencyBadge(
            Transform parent,
            out Image icon,
            out Text amount,
            Color textColor)
        {
            var groupGo = new GameObject("CurrencyBadge", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            groupGo.transform.SetParent(parent, false);
            var groupLayout = groupGo.GetComponent<HorizontalLayoutGroup>();
            groupLayout.spacing = 10f;
            groupLayout.childAlignment = TextAnchor.MiddleCenter;
            groupLayout.childControlWidth = false;
            groupLayout.childControlHeight = true;
            groupLayout.childForceExpandWidth = false;
            groupLayout.childForceExpandHeight = true;
            groupGo.AddComponent<LayoutElement>().preferredHeight = 40f;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(groupGo.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 34f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 34f;
            icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGo.transform.SetParent(groupGo.transform, false);
            textGo.GetComponent<LayoutElement>().preferredWidth = 96f;
            amount = textGo.GetComponent<Text>();
            amount.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            amount.fontSize = 26;
            amount.fontStyle = FontStyle.Bold;
            amount.alignment = TextAnchor.MiddleLeft;
            amount.color = textColor;
            amount.text = "0";
            amount.raycastTarget = false;
        }

        void BuildFooter(RectTransform panelRt)
        {
            _footerHintText = CreateStaticText(panelRt, "祭坛操作完成后点击离开", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            _footerHintText.rectTransform.anchorMin = new Vector2(0.03f, 0.03f);
            _footerHintText.rectTransform.anchorMax = new Vector2(0.5f, 0.1f);
            _footerHintText.color = TextMuted;

            _leaveButton = CreateAnchoredActionButton(
                panelRt, "离开祭坛", BtnGreen, AccentGreen,
                new Vector2(1f, 0.065f), new Vector2(240f, ActionButtonHeight), true,
                () => _session.LeaveAltar());
            var leaveRt = _leaveButton.GetComponent<RectTransform>();
            leaveRt.anchorMin = new Vector2(1f, 0.065f);
            leaveRt.anchorMax = new Vector2(1f, 0.065f);
            leaveRt.pivot = new Vector2(1f, 0.5f);
            leaveRt.anchoredPosition = new Vector2(-12f, 0f);
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

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.45f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

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
            gridGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewportRt;
            scroll.content = gridRt;
            return gridRt;
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
