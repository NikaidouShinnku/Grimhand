using System;
using System.Collections;
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
        const int LayoutVersion = 10;
        const float DoorWidth = 286f;
        const float DoorHeight = 364f;
        const float DoorLabelHeight = 36f;
        // 门框中心距加大，整组仍居中
        const float DoorSpacing = 400f;
        const float RewardCardScale = 0.68f;
        const float RewardCardSpacing = 200f;
        const float RewardIconSpacing = 200f;
        const float RewardPlateWidth = 168f;
        const float RewardPlateHeight = 236f;
        const float SkipButtonWidth = 280f;
        // button6 原生 512×216
        const float Button6Aspect = 512f / 216f;
        const float DoorHoverScale = 1.16f;
        const float ChestRevealDelaySeconds = 1f;
        const float ChestArtWidth = 280f;
        const float ChestArtHeight = 240f;
        static readonly Color LocationTitleColor = new(0.93f, 0.86f, 0.68f, 1f);
        static readonly Color LocationFloorColor = new(0.82f, 0.78f, 0.72f, 1f);
        static readonly Color PathTitleColor = new(0.90f, 0.78f, 0.42f, 1f);
        static readonly Color HeaderGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color ButtonLabel = new(0.96f, 0.92f, 0.78f, 1f);

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        ConsumableVisualCatalogSO _consumableCatalog;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        CardView _cardPrefab;
        RectTransform _root;
        Image _dimImage;
        RectTransform _rewardRow;
        RectTransform _doorRow;
        RectTransform _locationPlate;
        Text _locationTitle;
        Text _locationFloor;
        RectTransform _chestPanel;
        RectTransform _chestClosedLayer;
        Image _closedChestImage;
        Button _closedChestButton;
        Text _headerText;
        Button _skipVictoryButton;
        InventoryTooltipView _tooltip;
        bool _chestRevealed;
        bool _chestOpening;
        string _chestRewardKey = "";
        Coroutine _chestOpenRoutine;
        readonly List<Button> _rewardButtons = new();
        readonly List<Button> _doorButtons = new();
        RectTransform _tutorialGoldClaimRect;
        RectTransform _tutorialPackClaimRect;
        bool _built;
        int _builtVersion = -1;

        public RectTransform RewardRowRect =>
            _rewardRow != null && _rewardRow.gameObject.activeInHierarchy ? _rewardRow : null;

        public RectTransform TutorialGoldClaimRect =>
            _tutorialGoldClaimRect != null && _tutorialGoldClaimRect.gameObject.activeInHierarchy
                ? _tutorialGoldClaimRect
                : null;

        public RectTransform TutorialPackClaimRect =>
            _tutorialPackClaimRect != null && _tutorialPackClaimRect.gameObject.activeInHierarchy
                ? _tutorialPackClaimRect
                : null;

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
            var showChestStage = isChest && !_chestRevealed;
            var showRewardClaims = phase == ExpeditionPhase.RewardPickup
                                   && !packPickActive
                                   && (!isChest || _chestRevealed);

            if (_chestPanel != null)
                _chestPanel.gameObject.SetActive(showChestStage);
            _rewardRow.gameObject.SetActive(showRewardClaims);
            _doorRow.gameObject.SetActive(phase == ExpeditionPhase.RouteSelect);
            if (_locationPlate != null)
                _locationPlate.gameObject.SetActive(phase == ExpeditionPhase.RouteSelect);
            if (_headerText != null)
            {
                // 开箱阶段不显示顶部黄字；领取奖励时与战斗胜利一致
                _headerText.gameObject.SetActive(showRewardClaims);
            }

            if (_dimImage != null)
            {
                _dimImage.color = phase == ExpeditionPhase.RouteSelect
                    ? new Color(0f, 0f, 0f, 0f)
                    : new Color(0f, 0f, 0f, 0.42f);
            }

            if (_skipVictoryButton != null)
            {
                // 属性/强固等强制增益不可放弃：仅当仍有可跳过奖励时显示放弃钮
                var showSkip = showRewardClaims && HasSkippableRemainingRewards(rewards);
                _skipVictoryButton.gameObject.SetActive(showSkip);
            }

            if (isChest)
            {
                var rewardKey = BuildChestRewardKey(rewards);
                if (_chestRewardKey != rewardKey)
                {
                    _chestRewardKey = rewardKey;
                    StopChestOpenRoutine();
                    _chestOpening = false;
                }

                // 新宝箱或同局刷新：始终与跑局标志同步，避免沿用上一只宝箱的开启态。
                _chestRevealed = _session.Expedition.Run.ChestRewardRevealed;

                if (_chestRevealed)
                {
                    StopChestOpenRoutine();
                    _chestOpening = false;
                    SetClosedChestVisual(open: false, interactable: false);
                }
                else if (_chestOpening)
                {
                    SetClosedChestVisual(open: true, interactable: false);
                }
                else
                {
                    SetClosedChestVisual(open: false, interactable: true);
                }
            }
            else
            {
                StopChestOpenRoutine();
                _chestRevealed = false;
                _chestOpening = false;
                _chestRewardKey = "";
                SetClosedChestVisual(open: false, interactable: false);
            }

            if (_chestClosedLayer != null)
                _chestClosedLayer.gameObject.SetActive(showChestStage);

            if (showChestStage && _chestPanel != null)
                _chestPanel.SetAsLastSibling();

            if (phase == ExpeditionPhase.RewardPickup)
                RefreshRewardPickup(isChest);
            else if (phase == ExpeditionPhase.RouteSelect)
                RefreshDoors();
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && _builtVersion == LayoutVersion)
                return;

            if (_root != null)
                Destroy(_root.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;
            _rewardButtons.Clear();
            _doorButtons.Clear();

            var overlayGo = new GameObject("ExpeditionPostBattleOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(parent, false);
            overlayGo.transform.SetAsLastSibling();
            _root = overlayGo.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            _dimImage = overlayGo.GetComponent<Image>();
            _dimImage.color = new Color(0f, 0f, 0f, 0.42f);
            _dimImage.raycastTarget = true;

            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(Text));
            headerGo.transform.SetParent(_root, false);
            var headerRt = headerGo.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0.5f, 1f);
            headerRt.anchorMax = new Vector2(0.5f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.anchoredPosition = new Vector2(0f, -36f);
            headerRt.sizeDelta = new Vector2(900f, 48f);
            _headerText = headerGo.GetComponent<Text>();
            StyleText(_headerText, 28, TextAnchor.MiddleCenter);
            _headerText.color = HeaderGold;

            BuildLocationPlate(_root);

            var rewardGo = new GameObject("RewardRow", typeof(RectTransform));
            rewardGo.transform.SetParent(_root, false);
            _rewardRow = rewardGo.GetComponent<RectTransform>();
            _rewardRow.anchorMin = new Vector2(0.5f, 0.54f);
            _rewardRow.anchorMax = new Vector2(0.5f, 0.54f);
            _rewardRow.pivot = new Vector2(0.5f, 0.5f);
            _rewardRow.sizeDelta = new Vector2(920f, RewardPlateHeight + 24f);

            _skipVictoryButton = CreateSkipVictoryButton(_root, new Vector2(0.5f, 0.12f));

            var doorGo = new GameObject("DoorRow", typeof(RectTransform));
            doorGo.transform.SetParent(_root, false);
            _doorRow = doorGo.GetComponent<RectTransform>();
            _doorRow.anchorMin = new Vector2(0.5f, 0.46f);
            _doorRow.anchorMax = new Vector2(0.5f, 0.46f);
            _doorRow.pivot = new Vector2(0.5f, 0.5f);
            _doorRow.sizeDelta = new Vector2(1180f, DoorHeight + DoorLabelHeight + 40f);

            _chestPanel = BuildChestStage(_root);
            overlayGo.SetActive(false);

            _tooltip = overlayGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_root, _icons);
        }

        void BuildLocationPlate(RectTransform parent)
        {
            var plateGo = new GameObject("LocationPlate", typeof(RectTransform), typeof(Image));
            plateGo.transform.SetParent(parent, false);
            _locationPlate = plateGo.GetComponent<RectTransform>();
            _locationPlate.anchorMin = new Vector2(0.5f, 1f);
            _locationPlate.anchorMax = new Vector2(0.5f, 1f);
            _locationPlate.pivot = new Vector2(0.5f, 1f);
            // 框位保持原位；只把字往上提
            _locationPlate.anchoredPosition = new Vector2(0f, -18f);
            _locationPlate.sizeDelta = new Vector2(620f, 320f);

            var plateImage = plateGo.GetComponent<Image>();
            plateImage.sprite = _icons?.UiChoosingPathLocationPlate;
            plateImage.preserveAspect = true;
            plateImage.type = Image.Type.Simple;
            plateImage.color = plateImage.sprite != null ? Color.white : new Color(0.12f, 0.1f, 0.14f, 0.92f);
            plateImage.raycastTarget = false;

            var titleGo = new GameObject("RegionTitle", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(plateGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.18f, 0.62f);
            titleRt.anchorMax = new Vector2(0.82f, 0.90f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _locationTitle = titleGo.GetComponent<Text>();
            StyleText(_locationTitle, 40, TextAnchor.MiddleCenter);
            _locationTitle.color = LocationTitleColor;
            foreach (var fx in _locationTitle.GetComponents<Shadow>())
                Destroy(fx);

            var floorGo = new GameObject("FloorText", typeof(RectTransform), typeof(Text));
            floorGo.transform.SetParent(plateGo.transform, false);
            var floorRt = floorGo.GetComponent<RectTransform>();
            // 层数上移，落入下方小框内（不动「洞窟」等地图名）
            floorRt.anchorMin = new Vector2(0.22f, 0.40f);
            floorRt.anchorMax = new Vector2(0.78f, 0.58f);
            floorRt.offsetMin = Vector2.zero;
            floorRt.offsetMax = Vector2.zero;
            _locationFloor = floorGo.GetComponent<Text>();
            StyleText(_locationFloor, 22, TextAnchor.MiddleCenter);
            _locationFloor.fontStyle = FontStyle.Normal;
            _locationFloor.color = LocationFloorColor;
            foreach (var fx in _locationFloor.GetComponents<Shadow>())
                Destroy(fx);

            plateGo.SetActive(false);
        }

        /// <summary>
        /// 开箱舞台：与战斗胜利奖励行同锚点/尺寸，仅展示可点击宝箱立绘。
        /// 开启后切到共用的 RewardRow + 放弃按钮，布局与战后奖励一致。
        /// </summary>
        RectTransform BuildChestStage(RectTransform parent)
        {
            var panelGo = new GameObject("ChestStage", typeof(RectTransform));
            panelGo.transform.SetParent(parent, false);
            var rt = panelGo.GetComponent<RectTransform>();
            // 与 _rewardRow 对齐，避免独立大框「又小又歪」
            rt.anchorMin = new Vector2(0.5f, 0.54f);
            rt.anchorMax = new Vector2(0.5f, 0.54f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(920f, RewardPlateHeight + 24f);

            _chestClosedLayer = BuildChestClosedLayer(rt);
            panelGo.SetActive(false);
            return rt;
        }

        RectTransform BuildChestClosedLayer(Transform parent)
        {
            var go = new GameObject("ChestClosedLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var chestGo = new GameObject("ClosedChest", typeof(RectTransform), typeof(Image), typeof(Button));
            chestGo.transform.SetParent(go.transform, false);
            var chestRt = chestGo.GetComponent<RectTransform>();
            chestRt.anchorMin = new Vector2(0.5f, 0.5f);
            chestRt.anchorMax = new Vector2(0.5f, 0.5f);
            chestRt.pivot = new Vector2(0.5f, 0.5f);
            chestRt.anchoredPosition = Vector2.zero;
            chestRt.sizeDelta = new Vector2(ChestArtWidth, ChestArtHeight);
            _closedChestImage = chestGo.GetComponent<Image>();
            _closedChestImage.sprite = _icons?.TreasureChestClosed;
            _closedChestImage.preserveAspect = true;
            _closedChestImage.color = Color.white;
            _closedChestImage.raycastTarget = true;

            _closedChestButton = chestGo.GetComponent<Button>();
            _closedChestButton.targetGraphic = _closedChestImage;
            _closedChestButton.transition = Selectable.Transition.None;
            _closedChestButton.onClick.AddListener(RevealChest);
            WireHoverScale(chestRt, _closedChestImage, DoorHoverScale, addOutline: true);
            return rt;
        }

        void RevealChest()
        {
            if (_chestRevealed || _chestOpening)
                return;

            if (_session?.Expedition?.Run == null
                || _session.Expedition.Run.PendingRewardPickup?.Kind != RewardPickupKind.Chest)
                return;

            _chestOpening = true;
            SetClosedChestVisual(open: true, interactable: false);
            GameAudioService.Instance.PlayUiChestOpen();

            StopChestOpenRoutine();
            _chestOpenRoutine = StartCoroutine(RevealChestAfterDelay());
        }

        IEnumerator RevealChestAfterDelay()
        {
            yield return new WaitForSecondsRealtime(ChestRevealDelaySeconds);

            _chestOpenRoutine = null;
            _chestOpening = false;
            _chestRevealed = true;
            if (_session?.Expedition?.Run != null)
                _session.Expedition.Run.ChestRewardRevealed = true;

            if (_chestPanel != null)
                _chestPanel.gameObject.SetActive(false);
            if (_chestClosedLayer != null)
                _chestClosedLayer.gameObject.SetActive(false);

            RefreshRewardPickup(isChest: true);
            _session?.RequestRefresh();
        }

        void StopChestOpenRoutine()
        {
            if (_chestOpenRoutine == null)
                return;

            StopCoroutine(_chestOpenRoutine);
            _chestOpenRoutine = null;
        }

        void SetClosedChestVisual(bool open, bool interactable)
        {
            if (_closedChestImage != null)
            {
                var sprite = open
                    ? _icons?.TreasureChestOpen
                    : _icons?.TreasureChestClosed;
                if (sprite != null)
                    _closedChestImage.sprite = sprite;
                _closedChestImage.color = Color.white;

                if (!open)
                    _closedChestImage.rectTransform.localScale = Vector3.one;
            }

            if (_closedChestButton != null)
                _closedChestButton.interactable = interactable;
        }

        static string BuildChestRewardKey(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return "";

            // 含对象身份：两只宝箱可能滚出相同金币/卡包，不能只靠内容判“是否新宝箱”。
            var key =
                $"{System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(rewards)}|{rewards.Gold}|{rewards.GoldClaimed}|{rewards.RelicId}|{rewards.RelicClaimed}|{rewards.CardDefinitionId}|{rewards.ConsumableId}|{rewards.ConsumableClaimed}";
            foreach (var pack in rewards.CardPacks)
                key += $"|{pack.PackId}:{pack.Claimed}:{pack.Skipped}";
            return key;
        }

        void RefreshRewardPickup(bool isChest)
        {
            ClearButtons(_rewardButtons);
            _tutorialGoldClaimRect = null;
            _tutorialPackClaimRect = null;

            var run = _session.Expedition.Run;
            var rewards = run.PendingRewardPickup;
            if (rewards == null)
                return;

            if (isChest && !_chestRevealed)
                return;

            // 战斗胜利 / 宝箱开启后共用同一套领取 UI
            _headerText.text = "点击领取奖励";
            _headerText.color = HeaderGold;
            if (_headerText != null)
                _headerText.gameObject.SetActive(true);
            if (_rewardRow != null)
                _rewardRow.gameObject.SetActive(true);

            var parent = (Transform)_rewardRow;
            if (parent == null)
                return;

            var x = -220f;
            var spacing = RewardIconSpacing;
            var cardScale = RewardCardScale;

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
                if (_rewardButtons.Count > 0)
                    _tutorialGoldClaimRect = _rewardButtons[_rewardButtons.Count - 1].transform as RectTransform;
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
                if (_tutorialPackClaimRect == null && _rewardButtons.Count > 0)
                    _tutorialPackClaimRect = _rewardButtons[_rewardButtons.Count - 1].transform as RectTransform;
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

            if (_skipVictoryButton != null)
                _skipVictoryButton.gameObject.SetActive(HasSkippableRemainingRewards(rewards));
        }

        static bool IsGrantXpOnlyStatReward(ExpeditionRewardPickup rewards)
        {
            if (rewards == null || rewards.GrantXp <= 0)
                return false;

            return rewards.TeamAttackBonus == 0
                   && rewards.TeamDefenseBonus == 0
                   && rewards.TeamBlockGainBonusPercent == 0f
                   && rewards.EnergyCapBonus == 0
                   && rewards.PersonalAttackBonus == 0
                   && !rewards.EnableSoulRiftBattleStartRandomHpLoss
                   && !rewards.EnableDivinePunishment;
        }

        static bool HasRemainingRewards(ExpeditionRewardPickup rewards)
        {
            if (rewards == null)
                return false;

            if (HasSkippableRemainingRewards(rewards))
                return true;

            return rewards.HasStatBonus && !rewards.StatClaimed && !rewards.StatSkipped;
        }

        /// <summary>可放弃的奖励（金币/遗物/卡牌/消耗品等）；属性与强固等强制增益不算。</summary>
        static bool HasSkippableRemainingRewards(ExpeditionRewardPickup rewards)
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

            return false;
        }

        void AddClaimStatReward(
            Transform parent,
            ref float x,
            float spacing,
            ExpeditionRewardPickup rewards,
            Action onClaim)
        {
            // 与金币/遗物等同规格：reward_plate 小框领取，不可放弃
            AddClaimReward(
                parent,
                ref x,
                spacing,
                BuildStatRewardLabel(rewards),
                null,
                onClaim,
                BuildStatRewardTitle(rewards));
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
            if (rewards.TeamBlockGainBonusPercent != 0f)
                lines.Add($"全队强固 +{rewards.TeamBlockGainBonusPercent:0.#}%");
            if (rewards.EnergyCapBonus != 0)
                lines.Add($"能量上限 +{rewards.EnergyCapBonus}");
            if (rewards.GrantXp > 0)
                lines.Add($"经验 +{rewards.GrantXp}");
            if (rewards.EnableSoulRiftBattleStartRandomHpLoss)
                lines.Add("每场战斗开始随机 1 人 -3% HP");
            if (rewards.EnableDivinePunishment)
                lines.Add("下场战斗敌人 +20% 增伤（永久）");

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
            var container = CreateRewardContainer(parent, new Vector2(x, 0f));
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

        static RectTransform CreateRewardContainer(Transform parent, Vector2 pos)
        {
            var go = new GameObject("RewardSlot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(RewardPlateWidth, RewardPlateHeight);
            return rt;
        }

        void RefreshDoors()
        {
            ClearDoorRow();
            var run = _session.Expedition.Run;
            var layer = ExpeditionPathArt.ResolvePresentationLayer(run);
            var total = run.Map?.ChapterLayerCount ?? run.TargetBattleCount;
            if (_locationTitle != null)
                _locationTitle.text = ExpeditionPathArt.ResolveRegionDisplayName(layer);
            if (_locationFloor != null)
                _locationFloor.text = $"第 {layer} 层 / {total} 层";

            var routes = run.PendingRoutes;
            var spacing = DoorSpacing;
            var startX = -(routes.Count - 1) * spacing * 0.5f;

            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var index = i;
                var isTreasure = route.NodeType == ExpeditionNodeType.Treasure;
                var nodeSprite = isTreasure
                    ? _icons?.TreasureChestClosed
                    : PickPathSprite(route.PathSpriteIndex, route.LayerNumber);
                var frameSprite = ExpeditionPathArt.ResolvePathFrame(_icons, route.NodeType);
                var typeLabel = BattleUiFormatters.DescribeNodeType(route.NodeType);

                var btn = CreateDoorButton(
                    _doorRow,
                    new Vector2(startX + i * spacing, 0f),
                    frameSprite,
                    nodeSprite,
                    typeLabel,
                    () => _session.SelectRoute(index));
                _doorButtons.Add(btn);
            }
        }

        void RefreshChest()
        {
            RefreshRewardPickup(isChest: true);
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
            rt.sizeDelta = new Vector2(SkipButtonWidth, SkipButtonWidth / Button6Aspect);

            var image = go.GetComponent<Image>();
            image.color = Color.white;
            image.preserveAspect = false;
            if (_icons != null && _icons.UiButton6 != null)
                image.sprite = _icons.UiButton6;
            else
                image.color = new Color(0.22f, 0.24f, 0.32f, 0.96f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(18f, 10f);
            labelRt.offsetMax = new Vector2(-18f, -14f);
            var label = labelGo.GetComponent<Text>();
            StyleText(label, 22, TextAnchor.MiddleCenter);
            label.color = ButtonLabel;
            label.text = "放弃剩余奖励";

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = image;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => _session?.SkipAllRemainingRewards());
            BattleButtonPressFeedback.Apply(btn);
            UiAudioHooks.WireButton(btn);
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
            var go = new GameObject("RelicReward", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(RewardPlateWidth, RewardPlateHeight);

            var rootImage = go.GetComponent<Image>();
            ApplyRewardPlate(rootImage);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.14f, 0.32f);
            iconRt.anchorMax = new Vector2(0.86f, 0.88f);
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
            nameRt.anchorMin = new Vector2(0.08f, 0.08f);
            nameRt.anchorMax = new Vector2(0.92f, 0.28f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<Text>();
            StyleText(nameText, 16, TextAnchor.MiddleCenter);
            nameText.text = relic?.DisplayName ?? relicId ?? "遗物";

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = rootImage;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            BattleButtonPressFeedback.Apply(btn);
            UiAudioHooks.WireButton(btn);
            BindRewardTooltip(
                go,
                relic?.DisplayName ?? relicId ?? "遗物",
                relic != null ? RelicDescriptionFormatter.Format(relic, 0) : null);
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
            Sprite frameSprite,
            Sprite nodeSprite,
            string typeLabel,
            Action onClick)
        {
            var totalHeight = DoorHeight + DoorLabelHeight;
            var go = new GameObject("Door", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(DoorWidth, totalHeight);

            // 全透明点击层：不可见，仅接收射线
            var hit = go.GetComponent<Image>();
            hit.color = Color.clear;
            hit.raycastTarget = true;

            var visualGo = new GameObject("Visual", typeof(RectTransform));
            visualGo.transform.SetParent(go.transform, false);
            var visualRt = visualGo.GetComponent<RectTransform>();
            visualRt.anchorMin = new Vector2(0.5f, 1f);
            visualRt.anchorMax = new Vector2(0.5f, 1f);
            visualRt.pivot = new Vector2(0.5f, 1f);
            visualRt.anchoredPosition = Vector2.zero;
            visualRt.sizeDelta = new Vector2(DoorWidth, DoorHeight);

            var nodeGo = new GameObject("NodeArt", typeof(RectTransform), typeof(Image));
            nodeGo.transform.SetParent(visualGo.transform, false);
            var nodeRt = nodeGo.GetComponent<RectTransform>();
            nodeRt.anchorMin = new Vector2(0.16f, 0.08f);
            nodeRt.anchorMax = new Vector2(0.84f, 0.72f);
            nodeRt.offsetMin = Vector2.zero;
            nodeRt.offsetMax = Vector2.zero;
            var nodeImg = nodeGo.GetComponent<Image>();
            nodeImg.sprite = nodeSprite;
            nodeImg.preserveAspect = true;
            nodeImg.type = Image.Type.Simple;
            nodeImg.color = nodeSprite != null ? Color.white : new Color(0.55f, 0.45f, 0.32f, 1f);
            nodeImg.raycastTarget = false;

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(visualGo.transform, false);
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = Vector2.zero;
            frameRt.anchorMax = Vector2.one;
            frameRt.offsetMin = Vector2.zero;
            frameRt.offsetMax = Vector2.zero;
            var frame = frameGo.GetComponent<Image>();
            frame.sprite = frameSprite;
            frame.preserveAspect = true;
            frame.type = Image.Type.Simple;
            frame.color = frameSprite != null ? Color.white : new Color(0.18f, 0.16f, 0.2f, 0.95f);
            frame.raycastTarget = false;

            var typeGo = new GameObject("TypeLabel", typeof(RectTransform), typeof(Text));
            typeGo.transform.SetParent(go.transform, false);
            var typeRt = typeGo.GetComponent<RectTransform>();
            typeRt.anchorMin = new Vector2(0f, 0f);
            typeRt.anchorMax = new Vector2(1f, 0f);
            typeRt.pivot = new Vector2(0.5f, 0f);
            typeRt.anchoredPosition = Vector2.zero;
            typeRt.sizeDelta = new Vector2(0f, DoorLabelHeight);
            var typeText = typeGo.GetComponent<Text>();
            StyleText(typeText, 18, TextAnchor.MiddleCenter);
            typeText.color = PathTitleColor;
            typeText.text = typeLabel ?? "";

            WireHoverScale(rt, nodeRt, hit, DoorHoverScale, addOutline: false);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
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
            rt.sizeDelta = new Vector2(RewardPlateWidth, RewardPlateHeight);

            var img = go.GetComponent<Image>();
            ApplyRewardPlate(img);

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.14f, 0.32f);
                iconRt.anchorMax = new Vector2(0.86f, 0.88f);
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
                labelRt.anchorMin = new Vector2(0.08f, 0.08f);
                labelRt.anchorMax = new Vector2(0.92f, 0.28f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
            }
            else
            {
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(10f, 12f);
                labelRt.offsetMax = new Vector2(-10f, -12f);
            }

            var text = labelGo.GetComponent<Text>();
            StyleText(text, icon != null ? 16 : 18, TextAnchor.MiddleCenter);
            text.text = label;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            BattleButtonPressFeedback.Apply(btn);
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        void ApplyRewardPlate(Image image)
        {
            if (image == null)
                return;

            image.raycastTarget = true;
            image.preserveAspect = false;
            image.type = Image.Type.Simple;
            if (_icons != null && _icons.UiRewardPlate != null)
            {
                image.sprite = _icons.UiRewardPlate;
                image.color = Color.white;
                return;
            }

            image.sprite = null;
            image.color = new Color(0.12f, 0.13f, 0.16f, 0.96f);
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
                if (btn == null)
                    continue;

                var go = btn.gameObject;
                var parent = go.transform.parent;
                if (parent != null && parent.name == "RewardSlot")
                    Destroy(parent.gameObject);
                else
                    Destroy(go);
            }

            buttons.Clear();
        }

        static void WireHoverScale(RectTransform rt, Graphic graphic, float hoverScale, bool addOutline = false) =>
            WireHoverScale(rt, rt, graphic, hoverScale, addOutline);

        static void WireHoverScale(
            RectTransform eventHost,
            RectTransform scaleTarget,
            Graphic graphic,
            float hoverScale,
            bool addOutline = false)
        {
            if (eventHost == null || scaleTarget == null)
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

            var trigger = eventHost.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = eventHost.gameObject.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                scaleTarget.localScale = Vector3.one * hoverScale;
                if (outline != null)
                    outline.effectColor = new Color(1f, 0.88f, 0.35f, 0.95f);
            });
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ =>
            {
                scaleTarget.localScale = Vector3.one;
                if (outline != null)
                    outline.effectColor = new Color(1f, 0.88f, 0.35f, 0f);
            });
            trigger.triggers.Add(exit);
        }

        void SetVisible(bool visible)
        {
            if (!visible)
            {
                StopChestOpenRoutine();
                _chestOpening = false;
                if (_tooltip != null)
                    _tooltip.Hide();
            }

            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        void OnDestroy()
        {
            StopChestOpenRoutine();
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
            foreach (var fx in text.GetComponents<Shadow>())
                UnityEngine.Object.Destroy(fx);
        }
    }
}
