using System;
using System.Collections.Generic;
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
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _icons = icons;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
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
            var show = phase is ExpeditionPhase.VictoryRewards
                or ExpeditionPhase.RouteSelect
                or ExpeditionPhase.TreasureLoot;

            SetVisible(show);
            if (!show)
                return;

            _chestPanel.gameObject.SetActive(phase == ExpeditionPhase.TreasureLoot);
            _rewardRow.gameObject.SetActive(phase == ExpeditionPhase.VictoryRewards);
            _doorRow.gameObject.SetActive(phase == ExpeditionPhase.RouteSelect);
            if (_skipVictoryButton != null && phase != ExpeditionPhase.VictoryRewards)
                _skipVictoryButton.gameObject.SetActive(false);

            if (phase == ExpeditionPhase.VictoryRewards)
                RefreshVictoryRewards();
            else if (phase == ExpeditionPhase.RouteSelect)
                RefreshDoors();
            else if (phase == ExpeditionPhase.TreasureLoot)
                RefreshChest();
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

        void RefreshVictoryRewards()
        {
            ClearButtons(_rewardButtons);
            var run = _session.Expedition.Run;
            var rewards = run.PendingVictoryRewards;
            _headerText.text =
                $"第 {run.BattlesWon}/{run.TargetBattleCount} 场胜利\n点击领取奖励";

            if (rewards == null)
                return;

            var x = -220f;
            if (!rewards.GoldClaimed)
            {
                AddRewardButton(_rewardRow, ref x, BuildGoldLabel(rewards.Gold), _icons?.GoldIcon,
                    () => _session.ClaimVictoryGold());
            }

            if (rewards.HasRelic && !rewards.RelicClaimed)
            {
                RelicDatabase.TryGet(rewards.RelicId, out var relic);
                AddRelicRewardButton(_rewardRow, ref x, relic, rewards.RelicId,
                    () => _session.ClaimVictoryRelic());
            }

            if (rewards.HasCard && !rewards.CardClaimed)
            {
                _definitions.TryGetValue(rewards.CardDefinitionId, out var definition);
                AddCardRewardButton(
                    _rewardRow,
                    ref x,
                    rewards.CardDefinitionId,
                    rewards.CardOwnerCharacterId,
                    rewards.CardDisplayName,
                    definition,
                    () => _session.ClaimVictoryCard());
            }

            RefreshSkipVictoryButton(rewards);
        }

        void RefreshSkipVictoryButton(ExpeditionVictoryRewards rewards)
        {
            if (_skipVictoryButton == null)
                return;

            var showOptionalSkip = rewards != null &&
                ((rewards.HasRelic && !rewards.RelicClaimed) ||
                 (rewards.HasCard && !rewards.CardClaimed));

            _skipVictoryButton.gameObject.SetActive(showOptionalSkip);
        }

        void RefreshDoors()
        {
            ClearDoorRow();
            var run = _session.Expedition.Run;
            _headerText.text =
                $"选择前进路线（{run.BattlesWon}/{run.TargetBattleCount}）\n" +
                BattleUiFormatters.FormatPartySummary(run.Party, run.Gold);

            var routes = run.PendingRoutes;
            var spacing = DoorSpacing;
            var startX = -(routes.Count - 1) * spacing * 0.5f;

            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var index = i;
                var sprite = PickPathSprite(route.PathSpriteIndex);
                var label =
                    $"{route.DisplayName}\n[{BattleUiFormatters.DescribeNodeType(route.NodeType)}]\n{route.Description}";

                var btn = CreateDoorButton(_doorRow, new Vector2(startX + i * spacing, 0f), sprite, label,
                    () => _session.SelectRoute(index));
                _doorButtons.Add(btn);
            }
        }

        void RefreshChest()
        {
            _headerText.text = "宝箱房间";
            ClearButtons(_rewardButtons);

            var chestRow = _chestPanel.Find("ChestRewardRow");
            if (chestRow == null)
                return;

            foreach (Transform child in chestRow)
                Destroy(child.gameObject);

            var reward = _session.Expedition.Run.PendingChestReward;
            if (reward == null)
                return;

            var x = -110f;
            if (!reward.GoldClaimed)
            {
                AddRewardButton(chestRow, ref x, BuildGoldLabel(reward.Gold), _icons?.GoldIcon,
                    () => _session.ClaimChestGold());
            }

            if (reward.HasRelic && !reward.RelicClaimed)
            {
                RelicDatabase.TryGet(reward.RelicId, out var relic);
                AddRelicRewardButton(chestRow, ref x, relic, reward.RelicId,
                    () => _session.ClaimChestRelic());
            }
        }

        static string BuildGoldLabel(int gold) => $"金币\n+{gold}";

        void AddRewardButton(Transform parent, ref float x, string label, Sprite icon, Action onClick)
        {
            var btn = CreateIconButton(parent, new Vector2(x, 0f), 112f, icon, label, onClick);
            _rewardButtons.Add(btn);
            x += RewardIconSpacing;
        }

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

            CardView.ApplyHandPresentationScale(cardView, RewardCardScale);

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
            rt.anchorMin = new Vector2(0.5f, 0.52f);
            rt.anchorMax = new Vector2(0.5f, 0.52f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(280f, 44f);

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
            label.text = "放弃奖励";

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = image;
            btn.onClick.AddListener(() => _session?.SkipVictoryOptionalRewards());
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
            const float width = 132f;
            const float height = 176f;

            var go = new GameObject("RelicReward", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            var rarity = relic?.Rarity ?? RelicRarity.Common;
            var cardRarity = MapRelicRarity(rarity);
            var frame = _cardCatalog != null
                ? _cardCatalog.GetFrame(CardType.Status, cardRarity)
                : null;

            var rootImage = go.GetComponent<Image>();
            rootImage.color = Color.white;
            rootImage.raycastTarget = true;
            if (frame != null)
            {
                rootImage.sprite = frame;
                rootImage.preserveAspect = true;
                rootImage.type = Image.Type.Simple;
            }
            else
            {
                rootImage.color = RelicFallbackColor(rarity);
            }

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(go.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.18f, 0.34f);
            iconRt.anchorMax = new Vector2(0.82f, 0.78f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var iconImage = iconGo.GetComponent<Image>();
            iconImage.sprite = _icons?.DefenseIcon ?? _icons?.SpeedIcon;
            iconImage.preserveAspect = true;
            iconImage.color = RelicAccentColor(rarity);
            iconImage.raycastTarget = false;

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(go.transform, false);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0.08f, 0.08f);
            nameRt.anchorMax = new Vector2(0.92f, 0.28f);
            nameRt.offsetMin = Vector2.zero;
            nameRt.offsetMax = Vector2.zero;
            var nameText = nameGo.GetComponent<Text>();
            StyleText(nameText, 15, TextAnchor.MiddleCenter);
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
                img.sprite = icon;
                img.preserveAspect = true;
                img.color = Color.white;
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(4f, 4f);
            labelRt.offsetMax = new Vector2(-4f, -4f);
            var text = labelGo.GetComponent<Text>();
            StyleText(text, 16, TextAnchor.MiddleCenter);
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
