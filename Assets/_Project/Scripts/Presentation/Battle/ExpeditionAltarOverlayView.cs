using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>祭坛节点：召唤卡牌 + 分配经验（血量/能量/手牌/卡牌强化）。</summary>
    public sealed class ExpeditionAltarOverlayView : MonoBehaviour
    {
        enum AltarScreen
        {
            Hub,
            SummonCards,
            DistributeXp,
            UpgradeHp,
            UpgradeEnergy,
            UpgradeHand,
            UpgradeCards
        }

        static readonly Color BgOverlay = new(0f, 0f, 0f, 0.58f);
        static readonly Color PanelBg = new(0.1f, 0.11f, 0.15f, 0.96f);
        static readonly Color CardBg = new(0.14f, 0.16f, 0.22f, 0.94f);
        static readonly Color Border = new(0.32f, 0.36f, 0.44f, 0.85f);
        static readonly Color TextMain = new(0.92f, 0.94f, 0.98f, 1f);
        static readonly Color TextMuted = new(0.62f, 0.68f, 0.78f, 1f);
        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color AccentGreen = new(0.45f, 0.88f, 0.58f, 1f);
        static readonly Color AccentGreenBg = new(0.16f, 0.32f, 0.24f, 0.95f);
        static readonly Color BtnGreen = new(0.18f, 0.38f, 0.28f, 1f);
        static readonly Color BtnNeutral = new(0.22f, 0.24f, 0.3f, 1f);

        const float SummonCardScale = 0.74f;
        const float UpgradeCardScale = 0.74f;
        const float HubTileMinHeight = 280f;
        const float ActionButtonHeight = 56f;
        const float SummonCardWidth = 188f;
        const float SummonCardHeight = 272f;
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
        Text _layerHeaderText;
        Button _backButton;
        Text _footerHintText;
        Button _leaveButton;

        RectTransform _summonMemberRow;
        RectTransform _summonDeckRow;
        RectTransform _summonCollectionRow;
        Text _summonPreviewText;
        Text _summonStatusText;
        Button _summonConfirmButton;

        RectTransform _upgradeCardGrid;
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
                AltarScreen.DistributeXp or AltarScreen.SummonCards => "← 返回祭坛",
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

            var layer = run.CardAltar?.SourceLayer ?? 1;
            _layerHeaderText.text = $"第 {layer} 层 · {ResolveRegionName(layer)}";
            _backButton.gameObject.SetActive(_screen != AltarScreen.Hub);
            if (_navBar != null)
                _navBar.gameObject.SetActive(_screen != AltarScreen.Hub);
        }

        void RebuildContent(ExpeditionRunState run)
        {
            _tooltip?.Hide();
            ClearChildren(_contentHost);

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
                    break;
            }
        }

        void BuildHub(RectTransform parent)
        {
            AddTitle(parent, "祭坛", "选择一项祭坛服务", 0.86f, 1f, 0.76f, 0.86f);
            var row = CreateHorizontalRow(parent, "HubRow", 0.06f, 0.72f, 32f, expandChildren: true);
            CreateHubTile(row, "◫", "召唤卡牌", "从收藏中取出卡牌，加入或替换卡组", () => _screen = AltarScreen.SummonCards);
            CreateHubTile(row, "★", "分配经验", "花费经验强化角色与卡牌", () => _screen = AltarScreen.DistributeXp);
        }

        void BuildDistributeXpScreen(RectTransform parent, ExpeditionRunState run)
        {
            AddTitle(parent, "分配经验", "选择一项升级服务");
            var grid = CreateGrid(parent, "XpGrid", 2, 2, 0.10f, 0.76f, 24f, new Vector2(480f, 280f));
            CreateHubTile(grid, "♥", "升级血量", "选择角色提升最大 HP", () => _screen = AltarScreen.UpgradeHp);
            CreateHubTile(grid, "⚡", "升级能量", $"提升能量上限（当前 {GetEffectiveEnergyCap()}）", () => _screen = AltarScreen.UpgradeEnergy);
            CreateHubTile(grid, "▤", "升级手牌数", $"提升手牌上限（当前 {GetEffectiveHandLimit()}）", () => _screen = AltarScreen.UpgradeHand);
            CreateHubTile(grid, "↑", "强化卡牌", "提升卡牌数值", () => _screen = AltarScreen.UpgradeCards);
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
            var current = GetEffectiveHandLimit();
            var next = current + 1;
            var cost = ExpeditionAltarUpgradeRules.GetHandLimitUpgradeCost(run.Modifiers);
            var remaining = ExpeditionAltarUpgradeRules.GetRemainingHandLimitUpgrades(run.Modifiers);
            BuildStatUpgradeScreen(parent, "手牌上限", current, next, cost, remaining,
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

            var maxUpgrades = label.Contains("手牌")
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
            AddTitle(parent, "召唤卡牌", "从军营收藏中取出卡牌加入远征卡组");

            var memberRowGo = new GameObject("Members", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            memberRowGo.transform.SetParent(parent, false);
            _summonMemberRow = memberRowGo.GetComponent<RectTransform>();
            SetAnchoredBand(_summonMemberRow, 0.74f, 0.82f);
            var memberLayout = memberRowGo.GetComponent<HorizontalLayoutGroup>();
            memberLayout.spacing = 16f;
            memberLayout.padding = new RectOffset(24, 24, 0, 0);
            memberLayout.childAlignment = TextAnchor.MiddleLeft;
            memberLayout.childControlWidth = false;
            memberLayout.childControlHeight = false;

            var deckLabel = CreateBandText(parent, "当前卡组", 18, 0.69f, 0.73f, TextMuted);
            deckLabel.alignment = TextAnchor.MiddleLeft;
            _summonDeckRow = BuildScrollRow(parent, 0.43f, 0.69f);
            _summonPreviewText = CreateBandText(parent, "从下方收藏中选择一张卡牌。", 18, 0.39f, 0.43f, TextMuted);
            var collectionLabel = CreateBandText(parent, "军营收藏", 18, 0.35f, 0.39f, TextMuted);
            collectionLabel.alignment = TextAnchor.MiddleLeft;
            _summonCollectionRow = BuildScrollRow(parent, 0.08f, 0.35f);

            var footerHost = CreateRect("SummonFooter", parent);
            SetAnchoredBand(footerHost, 0.02f, 0.08f);
            _summonStatusText = CreateStaticText(footerHost, "", 16, FontStyle.Normal, TextAnchor.MiddleLeft);
            var statusRt = _summonStatusText.rectTransform;
            statusRt.anchorMin = new Vector2(0f, 0.5f);
            statusRt.anchorMax = new Vector2(0.45f, 1f);
            statusRt.offsetMin = new Vector2(8f, 0f);
            statusRt.offsetMax = Vector2.zero;
            _summonStatusText.color = TextMuted;

            _summonConfirmButton = CreateAnchoredActionButton(
                footerHost, "确认取出", BtnGreen, AccentGreen,
                new Vector2(1f, 0.5f), new Vector2(300f, ActionButtonHeight), false,
                () => _session.ConfirmCardAltar());
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
            ClearChildren(_summonDeckRow);
            ClearChildren(_summonCollectionRow);

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
            RebuildSummonDeck(activeMember);
            RebuildSummonCollection(activeMember);
            RefreshSummonStatus(activeMember);
        }

        void RebuildSummonDeck(PartyMemberSnapshot member)
        {
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);
            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, member))
            {
                var capturedKey = entry.Key;
                var selected = needsReplace && draft.ReplaceDeckCardKey == capturedKey;
                SpawnSummonCard(_summonDeckRow, entry.Template, member.CharacterDefinitionId, selected,
                    needsReplace
                        ? () =>
                        {
                            var current = GetDraft(member);
                            var replaceKey = current.ReplaceDeckCardKey == capturedKey ? "" : capturedKey;
                            _session.SetCardAltarDraft(member.CharacterDefinitionId, current.CollectionCardIndex, replaceKey);
                            RebuildSummonContent(_session.Expedition.Run);
                        }
                        : null);
            }
        }

        void RebuildSummonCollection(PartyMemberSnapshot member)
        {
            var run = _session.Expedition.Run;
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);
            foreach (var index in ExpeditionRunDeckRules.GetAvailableCollectionIndices(run, member))
            {
                var cardId = ExpeditionRunDeckCatalog.GetCampCollectionCardId(run, member, index);
                if (string.IsNullOrEmpty(cardId))
                    continue;

                var template = ExpeditionRunDeckCatalog.TryResolveCampCollectionCard(config, run, member, index);
                var capturedIndex = index;
                var selected = draft.CollectionCardIndex == capturedIndex;
                SpawnSummonCard(_summonCollectionRow, template, member.CharacterDefinitionId, selected, () =>
                {
                    var current = GetDraft(member);
                    var collectionIndex = current.CollectionCardIndex == capturedIndex ? -1 : capturedIndex;
                    var replaceKey = needsReplace ? current.ReplaceDeckCardKey : "";
                    _session.SetCardAltarDraft(member.CharacterDefinitionId, collectionIndex, replaceKey);
                    RebuildSummonContent(_session.Expedition.Run);
                });
            }
        }

        void RefreshSummonStatus(PartyMemberSnapshot member)
        {
            var config = _session.Expedition.Config;
            var draft = GetDraft(member);
            var deckCount = ExpeditionRunDeckRules.CountMemberDeck(config, member);
            var needsReplace = ExpeditionRunDeckRules.NeedsReplace(config, member);

            if (!draft.HasSelection)
            {
                _summonPreviewText.text = needsReplace
                    ? "从下方收藏中选择一张卡牌，并点选上方卡组中要替换的牌。"
                    : "从下方收藏中选择一张卡牌（将直接加入卡组）。";
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
                        ? $"将加入：{newName}\n请先选择卡组中要替换的卡牌。"
                        : $"{oldName}  →  {newName}";
                }
                else
                {
                    _summonPreviewText.text = $"将加入卡组：{newName}";
                }
            }

            _summonStatusText.text = $"当前卡组 {deckCount}/{ExpeditionRunDeckRules.DeckSize}";
            if (_summonConfirmButton != null)
                _summonConfirmButton.interactable = HasAnyValidDraft();
        }

        bool HasAnyValidDraft()
        {
            var run = _session.Expedition.Run;
            var config = _session.Expedition.Config;
            if (run.CardAltar == null)
                return false;

            var any = false;
            foreach (var member in run.Party)
            {
                if (!run.CardAltar.Drafts.TryGetValue(member.CharacterDefinitionId, out var draft) || !draft.HasSelection)
                    continue;

                any = true;
                if (ExpeditionRunDeckRules.NeedsReplace(config, member) && string.IsNullOrEmpty(draft.ReplaceDeckCardKey))
                    return false;
            }

            return any;
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
        }

        void CreateMemberTab(PartyMemberSnapshot member, bool active, System.Action onClick)
        {
            var go = CreatePanel("MemberTab", _summonMemberRow, active ? AccentGreenBg : CardBg, Border);
            var le = go.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 260f;
            le.preferredHeight = 88f;
            var btn = go.gameObject.AddComponent<Button>();
            btn.targetGraphic = go;
            btn.onClick.AddListener(() => onClick?.Invoke());
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
            SpawnCardVisual(holder.transform, template, ownerId, UpgradeCardScale);
        }

        void SpawnSummonCard(RectTransform parent, CardTemplate template, string ownerId, bool selected, System.Action onClick)
        {
            var holder = CreateCardHolder(parent, selected, SummonCardWidth, SummonCardHeight);
            if (onClick != null)
            {
                var btn = holder.gameObject.AddComponent<Button>();
                btn.targetGraphic = holder;
                btn.onClick.AddListener(() => onClick.Invoke());
            }

            SpawnCardVisual(holder.transform, template, ownerId, SummonCardScale);
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
            CardView.ConfigureForRewardPresentation(cardView, scale);
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

        int GetEffectiveHandLimit() =>
            _session.Expedition.GetAltarBaseHandLimit() + _session.Expedition.Run.Modifiers.HandLimitBonus;

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
                AltarScreen.SummonCards or AltarScreen.DistributeXp => AltarScreen.Hub,
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
            _tooltip.Initialize(panelRt);

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

            var xpHost = CreateRect("XpHeader", panelRt);
            var xpRt = xpHost.GetComponent<RectTransform>();
            xpRt.anchorMin = new Vector2(0.34f, 0.865f);
            xpRt.anchorMax = new Vector2(0.52f, 0.965f);
            xpRt.offsetMin = Vector2.zero;
            xpRt.offsetMax = Vector2.zero;

            var xpRowGo = new GameObject("XpRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            xpRowGo.transform.SetParent(xpHost, false);
            var xpRowRt = xpRowGo.GetComponent<RectTransform>();
            StretchFull(xpRowRt);
            var xpLayout = xpRowGo.GetComponent<HorizontalLayoutGroup>();
            xpLayout.spacing = 8f;
            xpLayout.childAlignment = TextAnchor.MiddleLeft;
            xpLayout.childControlWidth = false;
            xpLayout.childControlHeight = true;
            xpLayout.childForceExpandWidth = false;
            xpLayout.childForceExpandHeight = true;
            xpLayout.padding = new RectOffset(0, 0, 0, 0);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(xpRowGo.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 34f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 34f;
            _xpHeaderIcon = iconGo.GetComponent<Image>();
            _xpHeaderIcon.preserveAspect = true;
            _xpHeaderIcon.raycastTarget = false;

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGo.transform.SetParent(xpRowGo.transform, false);
            textGo.GetComponent<LayoutElement>().preferredWidth = 120f;
            _xpHeaderText = textGo.GetComponent<Text>();
            _xpHeaderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _xpHeaderText.fontSize = 26;
            _xpHeaderText.fontStyle = FontStyle.Bold;
            _xpHeaderText.alignment = TextAnchor.MiddleLeft;
            _xpHeaderText.color = AccentGreen;
            _xpHeaderText.text = "0";
            _xpHeaderText.raycastTarget = false;

            _layerHeaderText = CreateStaticText(panelRt, "", 18, FontStyle.Normal, TextAnchor.MiddleRight);
            _layerHeaderText.rectTransform.anchorMin = new Vector2(0.56f, 0.86f);
            _layerHeaderText.rectTransform.anchorMax = new Vector2(0.97f, 0.97f);
            _layerHeaderText.color = TextMuted;
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

        RectTransform BuildScrollRowInternal(RectTransform parent, float minY, float maxY, bool horizontal)
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
            rowLayout.spacing = 14f;
            rowLayout.padding = new RectOffset(12, 12, 8, 8);
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
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

        RectTransform BuildScrollGrid(Transform parent, int columns, Vector2 cellSize)
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
            gridRt.anchorMax = new Vector2(0f, 1f);
            gridRt.pivot = new Vector2(0f, 1f);
            gridRt.anchoredPosition = Vector2.zero;
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = cellSize;
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperLeft;
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

            foreach (Transform child in row)
                Destroy(child.gameObject);
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
