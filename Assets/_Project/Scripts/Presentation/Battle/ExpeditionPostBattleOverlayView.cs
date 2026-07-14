using System;
using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>战后奖励（上）+ 路线门（下）+ 宝箱拾取。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionPostBattleOverlayView : MonoBehaviour
    {
        const float DoorWidth = 280f;
        const float DoorHeight = 360f;
        const float DoorSpacing = 300f;
        const float RewardCardScale = 0.68f;
        const float ChestRewardCardScale = 0.92f;
        const float RewardCardSpacing = 180f;
        const float RewardIconSpacing = 140f;
        const float ChestPanelWidth = 920f;
        const float ChestPanelHeight = 560f;
        const float DoorHoverScale = 1.12f;
        const float ChestOpenArtAlpha = 0.8f;

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        ConsumableVisualCatalogSO _consumableCatalog;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        CardView _cardPrefab;
        RectTransform _root;
        RectTransform _rewardRow;
        RectTransform _doorRow;
        RectTransform _chestPanel;
        RectTransform _chestClosedLayer;
        Image _chestPanelBackground;
        Image _chestOpenArtImage;
        Text _headerText;
        Button _skipVictoryButton;
        Button _chestSkipButton;
        InventoryTooltipView _tooltip;
        bool _chestRevealed;
        string _chestRewardKey = "";
        readonly List<Button> _rewardButtons = new();
        readonly List<Button> _doorButtons = new();
        bool _built;

        public void Initialize(
            BattleSession session,
            Transform parent,
            BattleUiIconCatalogSO icons,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            RelicVisualCatalogSO relicCatalog,
            ConsumableVisualCatalogSO consumableCatalog,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _icons = icons;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _relicCatalog = relicCatalog;
            _consumableCatalog = consumableCatalog;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(parent);
        }

        public void Hide() => SetVisible(false);

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var phase = _session.Expedition.Run.Phase;
            var show = phase is ExpeditionPhase.RewardPickup
                or ExpeditionPhase.RouteSelect;

            SetVisible(show);
            if (!show)
                return;

            _session.Expedition.ReconcileAfterResume();

            var offer = _session.Expedition.Run.PendingCardOffer;
            var cardReplaceActive = offer?.Template != null
                                    && offer.Context != ExpeditionCardOfferContext.Altar;
            var packPickActive = _session.Expedition.Run.PendingCardPackOffer != null;
            if (_root != null)
            {
                var dim = _root.GetComponent<Image>();
                if (dim != null)
                    dim.raycastTarget = !cardReplaceActive && !packPickActive;
            }

            var rewards = _session.Expedition.Run.PendingRewardPickup;
            var isChest = phase == ExpeditionPhase.RewardPickup && rewards?.Kind == RewardPickupKind.Chest;
            _chestRevealed = isChest && _session.Expedition.Run.ChestRewardRevealed;

            _chestPanel.gameObject.SetActive(isChest);
            _rewardRow.gameObject.SetActive(phase == ExpeditionPhase.RewardPickup && !isChest && !packPickActive);
            _doorRow.gameObject.SetActive(phase == ExpeditionPhase.RouteSelect);
            if (_skipVictoryButton != null)
            {
                var showSkip = phase == ExpeditionPhase.RewardPickup && HasRemainingRewards(rewards);
                _skipVictoryButton.gameObject.SetActive(showSkip && !isChest);
                if (_chestSkipButton != null)
                    _chestSkipButton.gameObject.SetActive(showSkip && isChest && _chestRevealed);
            }

            if (isChest)
            {
                var rewardKey = BuildChestRewardKey(rewards);
                if (_chestRewardKey != rewardKey)
                {
                    _chestRewardKey = rewardKey;
                    if (!_session.Expedition.Run.ChestRewardRevealed)
                        _chestRevealed = false;
                }

                if (!_chestRevealed)
                    ResetChestOpenArt();
                else
                    ShowChestOpenArt();
            }
            else
            {
                _chestRevealed = false;
                _chestRewardKey = "";
                ResetChestOpenArt();
            }

            if (_chestClosedLayer != null)
                _chestClosedLayer.gameObject.SetActive(isChest && !_chestRevealed);

            if (isChest && _chestPanel != null)
                _chestPanel.SetAsLastSibling();

            if (isChest && !_chestRevealed)
                _headerText.text = "宝箱\n点击宝箱开启";

            if (phase == ExpeditionPhase.RewardPickup)
                RefreshRewardPickup(isChest);
            else if (phase == ExpeditionPhase.RouteSelect)
                RefreshDoors();
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;

            var overlayGo = new GameObject("ExpeditionPostBattleOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(parent, false);
            overlayGo.transform.SetAsLastSibling();
            _root = overlayGo.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            var dim = overlayGo.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.42f);
            dim.raycastTarget = true;

            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Text));
            headerGo.transform.SetParent(_root, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -24f);
            headerRt.sizeDelta = new Vector2(900f, 72f);
            _headerText = headerGo.GetComponent<Text>();
            StyleText(_headerText, 22, TextAnchor.UpperCenter);

            var rewardGo = new GameObject("RewardRow", typeof(RectTransform));
            rewardGo.transform.SetParent(_root, false);
            _rewardRow = rewardGo.GetComponent<RectTransform>();
            _rewardRow.anchorMin = new Vector2(0.5f, 0.62f);
            _rewardRow.anchorMax = new Vector2(0.5f, 0.62f);
            _rewardRow.pivot = new Vector2(0.5f, 0.5f);
            _rewardRow.sizeDelta = new Vector2(720f, 280f);

            _skipVictoryButton = CreateSkipVictoryButton(_root, new Vector2(0.5f, 0.14f));

            var doorGo = new GameObject("DoorRow", typeof(RectTransform));
            doorGo.transform.SetParent(_root, false);
            _doorRow = doorGo.GetComponent<RectTransform>();
            _doorRow.anchorMin = new Vector2(0.5f, 0.48f);
            _doorRow.anchorMax = new Vector2(0.5f, 0.48f);
            _doorRow.pivot = new Vector2(0.5f, 0.5f);
            _doorRow.sizeDelta = new Vector2(980f, DoorHeight + 120f);

            _chestPanel = BuildChestPanel(_root);
            overlayGo.SetActive(false);

            _tooltip = overlayGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_root);
        }

        RectTransform BuildChestPanel(RectTransform parent)
        {
            var panelGo = new GameObject("ChestPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(ChestPanelWidth, ChestPanelHeight);
            _chestPanelBackground = panelGo.GetComponent<Image>();
            _chestPanelBackground.color = new Color(0.1f, 0.11f, 0.16f, 0.96f);
            _chestPanelBackground.raycastTarget = true;

            _chestOpenArtImage = CreateChestOpenArt(panelGo.transform);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);
            titleRt.sizeDelta = new Vector2(-24f, 40f);
            var title = titleGo.GetComponent<Text>();
            StyleText(title, 28, TextAnchor.UpperCenter);
            title.text = "宝箱";

            var rowGo = new GameObject("RewardRow", typeof(RectTransform));
            rowGo.transform.SetParent(panelGo.transform, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.56f);
            rowRt.anchorMax = new Vector2(0.5f, 0.56f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(760f, 320f);
            rowGo.name = "ChestRewardRow";

            _chestClosedLayer = BuildChestClosedLayer(rt);
            _chestSkipButton = CreateSkipVictoryButton(rt, new Vector2(0.5f, 0.08f), "ChestSkipVictoryRewards");

            panelGo.SetActive(false);
            return rt;
        }

        static Image CreateChestOpenArt(Transform parent)
        {
            var go = new GameObject("ChestOpenArt", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -12f);
            rt.sizeDelta = new Vector2(ChestPanelWidth - 72f, ChestPanelHeight - 72f);

            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            image.gameObject.SetActive(false);
            return image;
        }

        void ResetChestOpenArt()
        {
            if (_chestOpenArtImage != null)
                _chestOpenArtImage.gameObject.SetActive(false);

            if (_chestPanelBackground == null)
                return;

            _chestPanelBackground.sprite = null;
            _chestPanelBackground.preserveAspect = false;
            _chestPanelBackground.color = new Color(0.1f, 0.11f, 0.16f, 0.96f);
        }

        void ShowChestOpenArt()
        {
            if (_chestOpenArtImage == null)
                return;

            _chestOpenArtImage.sprite = _icons?.TreasureChestOpen;
            _chestOpenArtImage.color = new Color(1f, 1f, 1f, ChestOpenArtAlpha);
            _chestOpenArtImage.gameObject.SetActive(true);
            _chestOpenArtImage.transform.SetAsFirstSibling();
        }

        RectTransform BuildChestClosedLayer(Transform parent)
        {
            var go = new GameObject("ChestClosedLayer", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var dim = go.GetComponent<Image>();
            dim.color = new Color(0.04f, 0.05f, 0.08f, 0.72f);
            dim.raycastTarget = false;

            var chestGo = new GameObject("ClosedChest", typeof(RectTransform), typeof(Image), typeof(Button));
            chestGo.transform.SetParent(go.transform, false);
            var chestRt = chestGo.GetComponent<RectTransform>();
            chestRt.anchorMin = new Vector2(0.5f, 0.5f);
            chestRt.anchorMax = new Vector2(0.5f, 0.5f);
            chestRt.pivot = new Vector2(0.5f, 0.5f);
            chestRt.sizeDelta = new Vector2(280f, 240f);
            var chestImg = chestGo.GetComponent<Image>();
            chestImg.sprite = _icons?.TreasureChestClosed;
            chestImg.preserveAspect = true;
            chestImg.color = Color.white;
            chestImg.raycastTarget = true;

            var hintGo = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(go.transform, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0.18f);
            hintRt.anchorMax = new Vector2(0.5f, 0.18f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.sizeDelta = new Vector2(520f, 40f);
            var hint = hintGo.GetComponent<Text>();
            StyleText(hint, 20, TextAnchor.MiddleCenter);
            hint.text = "点击宝箱开启";

            var btn = chestGo.GetComponent<Button>();
            btn.targetGraphic = chestImg;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(RevealChest);
            WireHoverScale(chestRt, chestImg, DoorHoverScale, addOutline: true);
            return rt;
        }

        void RevealChest()
        {
            _chestRevealed = true;
            if (_session?.Expedition?.Run != null)
                _session.Expedition.Run.ChestRewardRevealed = true;
            ShowChestOpenArt();
            GameAudioService.Instance.PlayUiChestOpen();

            if (_chestClosedLayer != null)
                _chestClosedLayer.gameObject.SetActive(false);

            if (_chestSkipButton != null && _session?.Expedition?.Run?.PendingRewardPickup != null)
                _chestSkipButton.gameObject.SetActive(HasRemainingRewards(_session.Expedition.Run.PendingRewardPickup));

            RefreshRewardPickup(useChestPanel: true);
            _session?.RequestRefresh();
        }

        static string BuildChestRewardKey(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return "";

            var key = $"{rewards.Gold}|{rewards.RelicId}|{rewards.CardDefinitionId}|{rewards.ConsumableId}";
            foreach (var pack in rewards.CardPacks)
                key += $"|{pack.PackId}:{pack.Claimed}:{pack.Skipped}";
            return key;
        }

        void RefreshRewardPickup(bool useChestPanel)
        {
            ClearButtons(_rewardButtons);

            var run = _session.Expedition.Run;
            var rewards = run.PendingRewardPickup;
            if (rewards == null)
                return;

            if (useChestPanel && !_chestRevealed)
                return;

            if (rewards.Kind == RewardPickupKind.BattleVictory)
            {
                _headerText.text =
                    $"第 {run.BattlesWon}/{run.TargetBattleCount} 场胜利\n点击领取奖励";
            }
            else if (!string.IsNullOrEmpty(rewards.HeaderText))
            {
                _headerText.text = rewards.HeaderText + "\n点击领取奖励";
            }
            else
            {
                _headerText.text = "拾取奖励\n点击领取奖励";
            }

            var parent = useChestPanel
                ? _chestPanel.Find("ChestRewardRow")
                : (Transform)_rewardRow;

            if (parent == null)
                return;

            if (useChestPanel)
            {
                foreach (Transform child in parent)
                    Destroy(child.gameObject);
            }

            var x = useChestPanel ? -240f : -220f;
            var spacing = useChestPanel ? RewardCardSpacing : RewardIconSpacing;
            var cardScale = useChestPanel ? ChestRewardCardScale : RewardCardScale;

            if (rewards.HasGold && !rewards.GoldClaimed && !rewards.GoldSkipped)
            {
                AddClaimReward(
                    parent,
                    ref x,
                    spacing,
                    BuildGoldLabel(rewards.Gold),
                    _icons?.GoldIcon,
                    () =>
                    {
                        GameAudioService.Instance.PlayUiGoldAcquire();
                        _session.ClaimRewardGold();
                    });
            }

            if (rewards.HasRelic && !rewards.RelicClaimed && !rewards.RelicSkipped)
            {
                RelicDatabase.TryGet(rewards.RelicId, out var relic);
                AddClaimRelicReward(
                    parent,
                    ref x,
                    spacing,
                    relic,
                    rewards.RelicId,
                    () =>
                    {
                        GameAudioService.Instance.PlayUiRelicsAcquire();
                        _session.ClaimRewardRelic();
                    });
            }

            if (rewards.HasCard && !rewards.CardClaimed && !rewards.CardSkipped)
            {
                _definitions.TryGetValue(rewards.CardDefinitionId, out var definition);
                AddClaimCardReward(
                    parent,
                    ref x,
                    spacing,
                    cardScale,
                    rewards.CardDefinitionId,
                    rewards.CardOwnerCharacterId,
                    rewards.CardDisplayName,
                    definition,
                    () =>
                    {
                        GameAudioService.Instance.PlayUiCardAcquire();
                        _session.ClaimRewardCard();
                    });
            }

            for (var packIndex = 0; packIndex < rewards.CardPacks.Count; packIndex++)
            {
                var pack = rewards.CardPacks[packIndex];
                if (pack.IsResolved || !CardPackIds.IsValid(pack.PackId))
                    continue;

                var localIndex = packIndex;
                AddClaimCardPackReward(
                    parent,
                    ref x,
                    spacing,
                    pack.PackId,
                    () =>
                    {
                        GameAudioService.Instance.PlayUiCardPackOpen();
                        _session.OpenRewardCardPack(localIndex);
                    });
            }

            if (rewards.HasConsumable && !rewards.ConsumableClaimed && !rewards.ConsumableSkipped)
            {
                ConsumableDatabase.TryGet(rewards.ConsumableId, out var consumable);
                AddClaimConsumableReward(
                    parent,
                    ref x,
                    spacing,
                    consumable,
                    rewards.ConsumableId,
                    rewards.ConsumableCount,
                    () =>
                    {
                        GameAudioService.Instance.PlayUiConsumableAcquire();
                        _session.ClaimRewardConsumable();
                    });
            }

            if (rewards.HasStatBonus && !rewards.StatClaimed && !rewards.StatSkipped)
            {
                if (IsGrantXpOnlyStatReward(rewards))
                {
                    AddClaimReward(
                        parent,
                        ref x,
                        spacing,
                        $"经验\n+{rewards.GrantXp}",
                        _icons?.GoldIcon,
                        () => _session.ClaimRewardStat());
                }
                else
                {
                    AddClaimStatReward(
                        parent,
                        ref x,
                        spacing,
                        rewards,
                        () => _session.ClaimRewardStat());
                }
            }
        }

        static bool IsGrantXpOnlyStatReward(ExpeditionRewardPickup rewards)
        {
            if (rewards == null || rewards.GrantXp <= 0)
                return false;

            return rewards.TeamAttackBonus == 0
                   && rewards.TeamDefenseBonus == 0
                   && rewards.EnergyCapBonus == 0
                   && rewards.PersonalAttackBonus == 0
                   && !rewards.EnableSoulRiftBattleStartRandomHpLoss
                   && !rewards.EnableDivinePunishment;
        }

        static bool HasRemainingRewards(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return false;

            if (rewards.HasGold && !rewards.GoldClaimed && !rewards.GoldSkipped)
                return true;
            if (rewards.HasRelic && !rewards.RelicClaimed && !rewards.RelicSkipped)
                return true;
            if (rewards.HasCard && !rewards.CardClaimed && !rewards.CardSkipped)
                return true;
            if (rewards.HasCardPacks)
            {
                foreach (var pack in rewards.CardPacks)
                {
                    if (!pack.IsResolved)
                        return true;
                }
            }
            if (rewards.HasConsumable && !rewards.ConsumableClaimed && !rewards.ConsumableSkipped)
                return true;
            if (rewards.HasStatBonus && !rewards.StatClaimed && !rewards.StatSkipped)
                return true;

            return false;
        }

        void AddClaimStatReward(
            Transform parent,
            ref float x,
            float spacing,
            ExpeditionRewardPickup rewards,
            Action onClaim)
        {
            var container = CreateRewardContainer(parent, new Vector2(x, 0f), useChestPanel: true);
            var btnGo = new GameObject("StatReward", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(container, false);
            var btnRt = btnGo.GetComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax = new Vector2(0.5f, 0.5f);
            btnRt.pivot = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = Vector2.zero;
            btnRt.sizeDelta = new Vector2(180f, 240f);
            btnGo.GetComponent<Image>().color = new Color(0.14f, 0.16f, 0.22f, 0.98f);

            var portraitGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            portraitGo.transform.SetParent(btnGo.transform, false);
            var portraitRt = portraitGo.GetComponent<RectTransform>();
            portraitRt.anchorMin = new Vector2(0.5f, 1f);
            portraitRt.anchorMax = new Vector2(0.5f, 1f);
            portraitRt.pivot = new Vector2(0.5f, 1f);
            portraitRt.anchoredPosition = new Vector2(0f, -12f);
            portraitRt.sizeDelta = new Vector2(128f, 128f);
            var portrait = portraitGo.GetComponent<Image>();
            var characterId = rewards.StatCharacterId;
            if (string.IsNullOrEmpty(characterId) && _session.Expedition.Run.Party.Count > 0)
                characterId = _session.Expedition.Run.Party[0].CharacterDefinitionId;
            portrait.sprite = _characterVisuals?.GetPortraitReference(characterId)
                ?? _characterVisuals?.GetPortrait(characterId);
            portrait.preserveAspect = true;
            portrait.color = portrait.sprite != null ? Color.white : new Color(0.35f, 0.38f, 0.45f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.06f, 0f);
            labelRt.anchorMax = new Vector2(0.94f, 0.42f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            StyleText(label, 18, TextAnchor.MiddleCenter);
            label.text = BuildStatRewardLabel(rewards);

            var btn = btnGo.GetComponent<Button>();
            btn.targetGraphic = btnGo.GetComponent<Image>();
            btn.onClick.AddListener(() => onClaim?.Invoke());
            BindRewardTooltip(btnGo, BuildStatRewardTitle(rewards), label.text);
            _rewardButtons.Add(btn);
            x += spacing;
        }

        static string BuildStatRewardTitle(ExpeditionRewardPickup rewards)
        {
            if (!string.IsNullOrEmpty(rewards.StatCharacterName))
                return rewards.StatCharacterName;

            if (rewards.PersonalAttackBonus != 0)
                return "属性强化";

            return "队伍增益";
        }

        static string BuildStatRewardLabel(ExpeditionRewardPickup rewards)
        {
            var lines = new List<string>();
            if (rewards.PersonalAttackBonus != 0)
                lines.Add($"增伤 +{rewards.PersonalAttackBonus}");
            if (rewards.TeamAttackBonus != 0)
                lines.Add($"全队增伤 +{rewards.TeamAttackBonus}");
            if (rewards.TeamDefenseBonus != 0)
                lines.Add($"全队护甲获取 +{rewards.TeamDefenseBonus}");
            if (rewards.EnergyCapBonus != 0)
                lines.Add($"能量上限 +{rewards.EnergyCapBonus}");
            if (rewards.GrantXp > 0)
                lines.Add($"经验 +{rewards.GrantXp}");
            if (rewards.EnableSoulRiftBattleStartRandomHpLoss)
                lines.Add("战前随机失血");
            if (rewards.EnableDivinePunishment)
                lines.Add("神罚激活");

            return lines.Count > 0 ? string.Join("\n", lines) : "属性奖励";
        }

        void AddClaimConsumableReward(
            Transform parent,
            ref float x,
            float spacing,
            ConsumableDefinition consumable,
            string consumableId,
            int count,
            Action onClaim)
        {
            var baseName = consumable?.DisplayName ?? consumableId ?? "消耗品";
            var label = count > 1 ? $"{baseName} ×{count}" : baseName;
            var icon = _consumableCatalog?.GetIcon(consumableId);
            AddClaimReward(parent, ref x, spacing, label, icon, onClaim, consumable?.Description);
        }

        void AddClaimReward(
            Transform parent,
            ref float x,
            float spacing,
            string label,
            Sprite icon,
            Action onClaim,
            string tooltipBody = null)
        {
            var container = CreateRewardContainer(parent, new Vector2(x, 0f));
            var btn = CreateIconButton(container, Vector2.zero, 112f, icon, label, onClaim);
            BindRewardTooltip(btn.gameObject, label, tooltipBody);
            _rewardButtons.Add(btn);
            x += spacing;
        }

        void AddClaimRelicReward(
            Transform parent,
            ref float x,
            float spacing,
            RelicDefinition relic,
            string relicId,
            Action onClaim)
        {
            var container = CreateRewardContainer(parent, new Vector2(x, 0f));
            var btn = CreateRelicRewardButton(container, Vector2.zero, relic, relicId, onClaim);
            _rewardButtons.Add(btn);
            x += spacing;
        }

        void AddClaimCardReward(
            Transform parent,
            ref float x,
            float spacing,
            float cardScale,
            string definitionId,
            string ownerCharacterId,
            string displayName,
            CardDefinitionSO definition,
            Action onClaim)
        {
            var container = CreateRewardContainer(parent, new Vector2(x, 0f), useChestPanel: cardScale >= ChestRewardCardScale - 0.01f);
            var btn = CreateCardRewardButton(container, Vector2.zero, definitionId, ownerCharacterId, displayName, definition, onClaim, cardScale);
            _rewardButtons.Add(btn);
            x += spacing;
        }

        void AddClaimCardPackReward(
            Transform parent,
            ref float x,
            float spacing,
            string packId,
            Action onClaim)
        {
            var label = CardPackIds.GetDisplayName(packId);
            AddRewardButton(parent, ref x, label, CardPackVisuals.GetPackIcon(packId, _icons), onClaim);
        }

        static RectTransform CreateRewardContainer(Transform parent, Vector2 pos, bool useChestPanel = false)
        {
            var go = new GameObject("RewardSlot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = useChestPanel ? new Vector2(220f, 300f) : new Vector2(148f, 160f);
            return rt;
        }

        void RefreshDoors()
        {
            ClearDoorRow();
            var run = _session.Expedition.Run;
            _headerText.text =
                $"选择前进路线（第 {run.Map?.NodesCompleted + 1 ?? run.BattlesWon + 1}/{run.Map?.ChapterLayerCount ?? run.TargetBattleCount} 层）\n" +
                BattleUiFormatters.FormatPartySummary(run.Party, run.Gold);

            var routes = run.PendingRoutes;
            var spacing = DoorSpacing;
            var startX = -(routes.Count - 1) * spacing * 0.5f;

            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var index = i;
                var isTreasure = route.NodeType == ExpeditionNodeType.Treasure;
                var sprite = isTreasure
                    ? _icons?.TreasureChestClosed
                    : PickPathSprite(route.PathSpriteIndex, route.LayerNumber);
                var label = ExpeditionRoutePresentation.BuildDoorLabel(route);

                var btn = CreateDoorButton(_doorRow, new Vector2(startX + i * spacing, 0f), sprite, label, isTreasure,
                    () => _session.SelectRoute(index));
                _doorButtons.Add(btn);
            }
        }

        void RefreshChest()
        {
            RefreshRewardPickup(useChestPanel: true);
        }

        void AddRewardButton(Transform parent, ref float x, string label, Sprite icon, Action onClick, float localX = float.NaN)
        {
            var pos = float.IsNaN(localX) ? new Vector2(x, 0f) : new Vector2(localX, 0f);
            var btn = CreateIconButton(parent, pos, 112f, icon, label, onClick);
            if (float.IsNaN(localX))
            {
                _rewardButtons.Add(btn);
                x += RewardIconSpacing;
            }
        }

        static string BuildGoldLabel(int gold) => $"金币\n+{gold}";

        void AddCardRewardButton(
            Transform parent,
            ref float x,
            string definitionId,
            string ownerCharacterId,
            string displayName,
            CardDefinitionSO definition,
            Action onClick)
        {
            var btn = CreateCardRewardButton(parent, new Vector2(x, 0f), definitionId, ownerCharacterId, displayName, definition, onClick);
            _rewardButtons.Add(btn);
            x += RewardCardSpacing;
        }

        void AddRelicRewardButton(
            Transform parent,
            ref float x,
            RelicDefinition relic,
            string relicId,
            Action onClick)
        {
            var btn = CreateRelicRewardButton(parent, new Vector2(x, 0f), relic, relicId, onClick);
            _rewardButtons.Add(btn);
            x += RewardCardSpacing;
        }

        Button CreateCardRewardButton(
            Transform parent,
            Vector2 pos,
            string definitionId,
            string ownerCharacterId,
            string displayName,
            CardDefinitionSO definition,
            Action onClick,
            float cardScale = RewardCardScale)
        {
            if (_cardPrefab == null)
                return CreateIconButton(parent, pos, 112f, null, displayName ?? definitionId, onClick);

            var go = new GameObject("CardReward", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(168f * cardScale, 236f * cardScale);

            var cardView = Instantiate(_cardPrefab, rt);
            var cardRt = cardView.transform as RectTransform;
            if (cardRt != null)
            {
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
            }

            CardView.ConfigureForRewardPresentation(cardView, cardScale);

            var preview = CardVisualResolver.CreatePreviewInstance(
                definitionId,
                ownerCharacterId,
                displayName,
                definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);

            cardView.BindWithCard(
                preview,
                visual,
                selected: false,
                polluted: false,
                interactable: true,
                orderBadge: "",
                statsLine: statsLine,
                uiIcons: _icons,
                characterVisuals: _characterVisuals,
                onClick: _ => onClick?.Invoke(),
                onHoverEnter: null,
                onHoverExit: null);

            ForceRewardCardOpaque(cardView);
            AttachRewardBadge(rt);
            BindCardRewardTooltip(go, preview);

            var button = cardView.GetComponent<Button>();
            if (button == null)
            {
                button = cardView.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
            }

            return button;
        }

        static void ForceRewardCardOpaque(CardView cardView)
        {
            if (cardView == null)
                return;

            var canvasGroup = cardView.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        Button CreateSkipVictoryButton(RectTransform parent, Vector2 anchorY, string objectName = "SkipVictoryRewards")
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorY;
            rt.anchorMax = anchorY;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(360f, 52f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.24f, 0.32f, 0.96f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            StyleText(label, 18, TextAnchor.MiddleCenter);
            label.text = "放弃剩余奖励";

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(() => _session?.SkipAllRemainingRewards());
            go.SetActive(false);
            return btn;
        }

        Button CreateRelicRewardButton(
            Transform parent,
            Vector2 pos,
            RelicDefinition relic,
            string relicId,
            Action onClick)
        {
            const float width = 148f;
            const float height = 188f;

            var go = new GameObject("RelicReward", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            var rootImage = go.GetComponent<Image>();
            rootImage.color = new Color(0.08f, 0.09f, 0.12f, 0.92f);
            rootImage.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.12f, 0.22f);
            iconRt.anchorMax = new Vector2(0.88f, 0.92f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.sprite = _relicCatalog?.GetIcon(relicId);
            iconImage.preserveAspect = true;
            if (iconImage.sprite != null)
            {
                iconImage.color = Color.white;
                iconImage.type = Image.Type.Simple;
            }
            else
            {
                iconImage.color = RelicAccentColor(relic?.Rarity ?? RelicRarity.Common);
                var fallbackGo = new GameObject("Fallback", typeof(RectTransform), typeof(Text));
                fallbackGo.transform.SetParent(iconGo.transform, false);
                var fallbackRt = fallbackGo.GetComponent<RectTransform>();
                fallbackRt.anchorMin = Vector2.zero;
                fallbackRt.anchorMax = Vector2.one;
                fallbackRt.offsetMin = Vector2.zero;
                fallbackRt.offsetMax = Vector2.zero;
                var fallbackText = fallbackGo.GetComponent<Text>();
                StyleText(fallbackText, 28, TextAnchor.MiddleCenter);
                fallbackText.text = string.IsNullOrEmpty(relic?.DisplayName)
                    ? "?"
                    : relic.DisplayName.Substring(0, 1);
            }
            iconImage.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.08f, 0.04f);
            nameRt.anchorMax = new Vector2(0.92f, 0.20f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<Text>();
            StyleText(nameText, 14, TextAnchor.MiddleCenter);
            nameText.text = relic?.DisplayName ?? relicId ?? "遗物";

            AttachRewardBadge(rt);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = rootImage;
            btn.onClick.AddListener(() => onClick?.Invoke());
            BindRewardTooltip(go, relic?.DisplayName ?? relicId ?? "遗物", relic?.Description);
            return btn;
        }

        void BindRewardTooltip(GameObject target, string title, string body)
        {
            if (_tooltip == null || target == null || string.IsNullOrWhiteSpace(body))
                return;

            _tooltip.BindHover(target, title, body);
        }

        void BindCardRewardTooltip(GameObject target, CardInstanceState preview)
        {
            if (_tooltip == null || target == null || preview == null)
                return;

            var stats = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(null, preview, _definitions);
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            if (string.IsNullOrWhiteSpace(body))
                return;

            _tooltip.BindHover(target, preview.DisplayName, body, showTitle: false);
        }

        static void AttachRewardBadge(RectTransform parent)
        {
            var badgeGo = new GameObject("RewardBadge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(parent, false);
            var badgeRt = badgeGo.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(6f, 6f);
            badgeRt.sizeDelta = new Vector2(52f, 22f);
            var badgeImage = badgeGo.GetComponent<Image>();
            badgeImage.color = new Color(0.92f, 0.72f, 0.18f, 0.96f);
            badgeImage.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(badgeGo.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            StyleText(text, 13, TextAnchor.MiddleCenter);
            text.color = new Color(0.12f, 0.08f, 0.02f, 1f);
            text.raycastTarget = false;
            text.text = "奖励";
        }

        static CardRarity MapRelicRarity(RelicRarity rarity) =>
            rarity switch
            {
                RelicRarity.Rare => CardRarity.Rare,
                RelicRarity.Epic => CardRarity.Epic,
                _ => CardRarity.Common
            };

        static Color RelicFallbackColor(RelicRarity rarity) =>
            rarity switch
            {
                RelicRarity.Rare => new Color(0.18f, 0.24f, 0.42f, 0.98f),
                RelicRarity.Epic => new Color(0.34f, 0.16f, 0.44f, 0.98f),
                _ => new Color(0.28f, 0.22f, 0.16f, 0.98f)
            };

        static Color RelicAccentColor(RelicRarity rarity) =>
            rarity switch
            {
                RelicRarity.Rare => new Color(0.72f, 0.86f, 1f, 1f),
                RelicRarity.Epic => new Color(0.92f, 0.68f, 1f, 1f),
                _ => new Color(0.95f, 0.82f, 0.55f, 1f)
            };

        Button CreateDoorButton(
            Transform parent,
            Vector2 pos,
            Sprite sprite,
            string label,
            bool isTreasure,
            Action onClick)
        {
            var go = new GameObject("Door", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = isTreasure ? new Vector2(240f, 220f) : new Vector2(DoorWidth, DoorHeight);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            img.color = sprite != null ? Color.white : new Color(0.55f, 0.45f, 0.32f, 1f);

            WireHoverScale(rt, img, DoorHoverScale, addOutline: true);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(_doorRow, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.5f, 0.5f);
            labelRt.anchorMax = new Vector2(0.5f, 0.5f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.anchoredPosition = new Vector2(pos.x, pos.y - DoorHeight * 0.5f - 6f);
            labelRt.sizeDelta = new Vector2(DoorWidth + 24f, 88f);
            var text = labelGo.GetComponent<Text>();
            StyleText(text, 16, TextAnchor.UpperCenter);
            text.text = label;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        Button CreateIconButton(Transform parent, Vector2 pos, float size, Sprite icon, string label, Action onClick)
        {
            var go = new GameObject("Reward", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.14f, 0.16f, 0.22f, 0.96f);

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.12f, 0.22f);
                iconRt.anchorMax = new Vector2(0.88f, 0.92f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.type = Image.Type.Simple;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            if (icon != null)
            {
                labelRt.anchorMin = new Vector2(0.06f, 0.04f);
                labelRt.anchorMax = new Vector2(0.94f, 0.2f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
            }
            else
            {
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(4f, 4f);
                labelRt.offsetMax = new Vector2(-4f, -4f);
            }
            var text = labelGo.GetComponent<Text>();
            StyleText(text, icon != null ? 13 : 16, TextAnchor.MiddleCenter);
            text.text = label;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        Sprite PickPathSprite(int index, int layerNumber) =>
            ExpeditionPathArt.PickPathSprite(_icons, layerNumber, index);

        void ClearDoorRow()
        {
            ClearButtons(_doorButtons);
            if (_doorRow == null)
                return;

            for (var i = _doorRow.childCount - 1; i >= 0; i--)
                Destroy(_doorRow.GetChild(i).gameObject);
        }

        static void ClearButtons(List<Button> buttons)
        {
            foreach (var btn in buttons)
            {
                if (btn != null)
                    Destroy(btn.gameObject);
            }

            buttons.Clear();
        }

        static void WireHoverScale(RectTransform rt, Graphic graphic, float hoverScale, bool addOutline = false)
        {
            if (rt == null)
                return;

            Outline outline = null;
            if (addOutline && graphic != null)
            {
                outline = graphic.gameObject.GetComponent<Outline>();
                if (outline == null)
                    outline = graphic.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.88f, 0.35f, 0f);
                outline.effectDistance = new Vector2(2f, -2f);
            }

            var trigger = rt.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = rt.gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                rt.localScale = Vector3.one * hoverScale;
                if (outline != null)
                    outline.effectColor = new Color(1f, 0.88f, 0.35f, 0.95f);
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                rt.localScale = Vector3.one;
                if (outline != null)
                    outline.effectColor = new Color(1f, 0.88f, 0.35f, 0f);
            });
            trigger.triggers.Add(exit);
        }

        void SetVisible(bool visible)
        {
            if (!visible && _tooltip != null)
                _tooltip.Hide();

            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        static void StyleText(Text text, int size, TextAnchor anchor)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }
    }
}
