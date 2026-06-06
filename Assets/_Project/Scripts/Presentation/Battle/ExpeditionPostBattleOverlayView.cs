using System;
using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
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
        const float RewardCardSpacing = 180f;
        const float RewardIconSpacing = 140f;

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
        Text _headerText;
        Button _skipVictoryButton;
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

            var rewards = _session.Expedition.Run.PendingRewardPickup;
            var isChest = phase == ExpeditionPhase.RewardPickup && rewards?.Kind == RewardPickupKind.Chest;

            _chestPanel.gameObject.SetActive(isChest);
            _rewardRow.gameObject.SetActive(phase == ExpeditionPhase.RewardPickup && !isChest);
            _doorRow.gameObject.SetActive(phase == ExpeditionPhase.RouteSelect);
            if (_skipVictoryButton != null)
                _skipVictoryButton.gameObject.SetActive(
                    phase == ExpeditionPhase.RewardPickup && HasRemainingRewards(rewards));

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

            _skipVictoryButton = CreateSkipVictoryButton(_root);

            var doorGo = new GameObject("DoorRow", typeof(RectTransform));
            doorGo.transform.SetParent(_root, false);
            _doorRow = doorGo.GetComponent<RectTransform>();
            _doorRow.anchorMin = new Vector2(0.5f, 0.48f);
            _doorRow.anchorMax = new Vector2(0.5f, 0.48f);
            _doorRow.pivot = new Vector2(0.5f, 0.5f);
            _doorRow.sizeDelta = new Vector2(980f, DoorHeight + 120f);

            _chestPanel = BuildChestPanel(_root);
            overlayGo.SetActive(false);
        }

        RectTransform BuildChestPanel(RectTransform parent)
        {
            var panelGo = new GameObject("ChestPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(parent, false);
            var rt = panelGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 280f);
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.16f, 0.96f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -16f);
            titleRt.sizeDelta = new Vector2(-24f, 40f);
            var title = titleGo.GetComponent<Text>();
            StyleText(title, 24, TextAnchor.UpperCenter);
            title.text = "宝箱";

            var rowGo = new GameObject("RewardRow", typeof(RectTransform));
            rowGo.transform.SetParent(panelGo.transform, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(420f, 120f);
            rowGo.name = "ChestRewardRow";

            panelGo.SetActive(false);
            return rt;
        }

        void RefreshRewardPickup(bool useChestPanel)
        {
            ClearButtons(_rewardButtons);

            var run = _session.Expedition.Run;
            var rewards = run.PendingRewardPickup;
            if (rewards == null)
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

            var x = useChestPanel ? -110f : -220f;
            var spacing = useChestPanel ? RewardIconSpacing : RewardIconSpacing;

            if (rewards.HasGold && !rewards.GoldClaimed && !rewards.GoldSkipped)
            {
                AddClaimReward(
                    parent,
                    ref x,
                    spacing,
                    BuildGoldLabel(rewards.Gold),
                    _icons?.GoldIcon,
                    () => _session.ClaimRewardGold());
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
                    () => _session.ClaimRewardRelic());
            }

            if (rewards.HasCard && !rewards.CardClaimed && !rewards.CardSkipped)
            {
                _definitions.TryGetValue(rewards.CardDefinitionId, out var definition);
                AddClaimCardReward(
                    parent,
                    ref x,
                    spacing,
                    rewards.CardDefinitionId,
                    rewards.CardOwnerCharacterId,
                    rewards.CardDisplayName,
                    definition,
                    () => _session.ClaimRewardCard());
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
                    () => _session.ClaimRewardConsumable());
            }
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
            if (rewards.HasConsumable && !rewards.ConsumableClaimed && !rewards.ConsumableSkipped)
                return true;

            return false;
        }

        void AddClaimConsumableReward(
            Transform parent,
            ref float x,
            float spacing,
            ConsumableDefinition consumable,
            string consumableId,
            Action onClaim)
        {
            var label = consumable?.DisplayName ?? consumableId ?? "消耗品";
            var icon = _consumableCatalog?.GetIcon(consumableId);
            AddClaimReward(parent, ref x, spacing, label, icon, onClaim);
        }

        void AddClaimReward(
            Transform parent,
            ref float x,
            float spacing,
            string label,
            Sprite icon,
            Action onClaim)
        {
            var container = CreateRewardContainer(parent, new Vector2(x, 0f));
            var btn = CreateIconButton(container, Vector2.zero, 112f, icon, label, onClaim);
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
            string definitionId,
            string ownerCharacterId,
            string displayName,
            CardDefinitionSO definition,
            Action onClaim)
        {
            var container = CreateRewardContainer(parent, new Vector2(x, 0f));
            var btn = CreateCardRewardButton(container, Vector2.zero, definitionId, ownerCharacterId, displayName, definition, onClaim);
            _rewardButtons.Add(btn);
            x += spacing;
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
            rt.sizeDelta = new Vector2(148f, 160f);
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
                var sprite = PickPathSprite(route.PathSpriteIndex);
                var label = ExpeditionRoutePresentation.BuildDoorLabel(route);

                var btn = CreateDoorButton(_doorRow, new Vector2(startX + i * spacing, 0f), sprite, label,
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
            Action onClick)
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
            rt.sizeDelta = new Vector2(168f * RewardCardScale, 236f * RewardCardScale);

            var cardView = Instantiate(_cardPrefab, rt);
            var cardRt = cardView.transform as RectTransform;
            if (cardRt != null)
            {
                cardRt.anchorMin = new Vector2(0.5f, 0.5f);
                cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
            }

            CardView.ApplyHandPresentationScaleCentered(cardView, RewardCardScale);

            var preview = CardVisualResolver.CreatePreviewInstance(
                definitionId,
                ownerCharacterId,
                displayName,
                definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview);

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

        Button CreateSkipVictoryButton(RectTransform parent)
        {
            var go = new GameObject("SkipVictoryRewards", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.42f);
            rt.anchorMax = new Vector2(0.5f, 0.42f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(320f, 44f);

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
            return btn;
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

        Button CreateDoorButton(Transform parent, Vector2 pos, Sprite sprite, string label, Action onClick)
        {
            var go = new GameObject("Door", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(DoorWidth, DoorHeight);

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            img.color = sprite != null ? Color.white : new Color(0.55f, 0.45f, 0.32f, 1f);

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

        Sprite PickPathSprite(int index)
        {
            var paths = _icons?.CavePathVariants;
            if (paths == null || paths.Length == 0)
                return null;

            if (index < 0)
                index = 0;

            return paths[index % paths.Length];
        }

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

        void SetVisible(bool visible)
        {
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
