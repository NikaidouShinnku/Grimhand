using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Planning;
using Grimhand.Battle.Model;
using Grimhand.Battle.Rules;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class BattleScreenView : MonoBehaviour
    {
        [Header("HUD")]
        [SerializeField] Text titleText;
        [SerializeField] Text subtitleText;
        [SerializeField] Image hudEnergyIcon;
        [SerializeField] Text energyValueText;

        [Header("Battlefield")]
        [SerializeField] CombatantSlotView[] playerSlots = new CombatantSlotView[3];
        [SerializeField] CombatantSlotView[] enemySlots = new CombatantSlotView[3];

        [Header("Panels")]
        [SerializeField] HandPanelView handPanel;
        [SerializeField] Text enemyIntentText;
        [SerializeField] GameObject enemyIntentPanel;
        [SerializeField] Text selectedQueueText;
        [SerializeField] GameObject selectedQueuePanel;
        [SerializeField] Text targetPromptText;
        [SerializeField] GameObject targetPromptPanel;

        [Header("Actions")]
        [SerializeField] Button confirmButton;
        [SerializeField] Button skipButton;
        [SerializeField] Button restartButton;
        [SerializeField] Text restartButtonLabel;

        [Header("Tooltip")]
        [SerializeField] GameObject keywordTooltipPanel;
        [SerializeField] Text keywordTooltipText;
        RectTransform _keywordTooltipAnchor;
        float _keywordTooltipLeftAt = -1f;
        const float KeywordTooltipStaleHideSeconds = 1f;

        [Header("Expedition Overlay")]
        [SerializeField] GameObject expeditionOverlay;
        [SerializeField] GameObject routeSelectPanel;
        [SerializeField] Transform routeButtonRoot;
        [SerializeField] Button routeButtonPrefab;
        [SerializeField] Text routeHeaderText;
        [SerializeField] GameObject runEndPanel;
        [SerializeField] Text runEndTitleText;
        [SerializeField] Text runEndBodyText;
        [SerializeField] Button runRestartButton;

        readonly List<Button> _routeButtons = new();
        BattleActiveCardBanner _activeCardBanner;
        BattleInventoryPanelView _inventoryPanel;
        ConsumableReplaceOverlayView _consumableReplaceOverlay;
        CardDeckReplaceOverlayView _cardDeckReplaceOverlay;
        CardPackPickOverlayView _cardPackPickOverlay;
        ExpeditionAltarOverlayView _altarOverlay;
        ConsumableVisualCatalogSO _consumableCatalog;
        BattleTurnDetailPanelView _turnDetailPanel;
        ExpeditionMapPanelView _mapPanel;
        ExpeditionNodeInteractOverlayView _nodeInteractOverlay;
        ExpeditionEventInteractSequenceView _eventSequenceOverlay;
        BattleBackgroundView _backgroundView;
        ExpeditionPostBattleOverlayView _postBattleOverlay;
        ExpeditionShopOverlayView _shopOverlay;
        FelskullBattleChoiceView _felskullChoice;
        PsionicScryOverlayView _psionicScryOverlay;
        Button _inventoryButton;
        Button _turnLogButton;
        Button _mapButton;
        Button _codexButton;
        CardCodexOverlayView _codexOverlay;
        Button _dummyPlayButton;
        Button _monsterSpawnButton;
        MonsterSpawnOverlayView _monsterSpawnOverlay;
        BattleActionOrderBarView _actionOrderBar;
        BattlePresentationSpeedToggleView _presentationSpeedToggle;
        Button _settingsButton;
        Button _targetCancelBackdrop;
        Text _inventoryFallbackLabel;
        System.Action _onOpenSettings;

        BattleSession _session;
        System.Func<bool> _presentationBusy;
        string _damagePreviewCombatantId;
        CardVisualCatalogSO _catalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        bool _battleBgmActive;

        static readonly FormationSlot[] SlotOrder =
        {
            FormationSlot.Front,
            FormationSlot.Middle,
            FormationSlot.Back
        };

        public CardView HandCardPrefab => handPanel != null ? handPanel.CardPrefab : null;

        public void Initialize(
            BattleSession session,
            CardVisualCatalogSO catalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            RelicVisualCatalogSO relicCatalog = null,
            ConsumableVisualCatalogSO consumableCatalog = null)
        {
            _session = session;
            _catalog = catalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _relicCatalog = relicCatalog;
            _consumableCatalog = consumableCatalog;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();

            ConfigureBattlefieldSlots();

            confirmButton.onClick.AddListener(() => _session?.CommitPlan());
            skipButton.onClick.AddListener(() => _session?.SkipTurn());
            restartButton.onClick.AddListener(() => _session?.RestartRunOrBattle());
            runRestartButton.onClick.AddListener(() => _session?.ReturnToCampOrRestart());
            if (confirmButton != null)
                UiAudioHooks.WireButton(confirmButton);
            if (skipButton != null)
                UiAudioHooks.WireButton(skipButton);
            if (restartButton != null)
                UiAudioHooks.WireButton(restartButton);
            if (runRestartButton != null)
                UiAudioHooks.WireButton(runRestartButton);

            foreach (var slot in playerSlots)
            {
                slot?.SetSelectHandler(id => _session?.AssignTarget(id));
                slot?.SetHoverPreviewCallbacks(OnDamagePreviewEnter, OnDamagePreviewExit);
            }

            foreach (var slot in enemySlots)
            {
                slot?.SetSelectHandler(id => _session?.AssignTarget(id));
                slot?.SetHoverPreviewCallbacks(OnDamagePreviewEnter, OnDamagePreviewExit);
            }

            HideKeywordTooltip();
            ConfigureKeywordTooltipRaycast();
            ApplyTypographyPolish();
            ResolveHudReferences();
            BattleUiLayoutRuntimeFix.ApplyIfNeeded(transform);
            EnsurePlanningEnergyHud();
            EnsureInventoryHud();
            EnsureTurnLogHud();
            EnsureMapHud();
            EnsureCodexHud();
            EnsureDummyPlayHud();
            EnsurePresentationSpeedHud();
            EnsureSettingsHud();
            EnsureExpeditionPresentation();
            ApplyPlanningButtonIcons();
            CombatantTooltipLayer.GetOrCreate(transform);
            EnsureActiveCardBanner();
            EnsureActionOrderBar();

            if (GetComponent<BattleUiBootstrap>() == null)
                gameObject.AddComponent<BattleUiBootstrap>();
        }

        public void BindOpenSettings(System.Action onOpenSettings) => _onOpenSettings = onOpenSettings;

        public void SetPresentationBusyCheck(System.Func<bool> check) => _presentationBusy = check;

        void EnsureActiveCardBanner()
        {
            if (_activeCardBanner != null || handPanel == null)
                return;

            var prefab = handPanel.CardPrefab;
            if (prefab == null)
                return;

            _activeCardBanner = gameObject.AddComponent<BattleActiveCardBanner>();
            _activeCardBanner.Initialize(
                _session,
                prefab,
                _catalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                transform);
        }

        void EnsureActionOrderBar()
        {
            if (_actionOrderBar != null || handPanel == null)
                return;

            var prefab = handPanel.CardPrefab;
            if (prefab == null)
                return;

            var chromeRoot = BattleUiLayoutRuntimeFix.GetHudChromeRoot(transform) ?? transform;
            _actionOrderBar = gameObject.AddComponent<BattleActionOrderBarView>();
            _actionOrderBar.Initialize(
                chromeRoot,
                prefab,
                _catalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                ShowKeywordTooltip,
                HideKeywordTooltip);
            BattleUiLayoutRuntimeFix.EnsureInteractiveHudAboveActionOrderBar(chromeRoot);
        }

        public void ShowActiveCard(int cardInstanceId) =>
            _activeCardBanner?.Show(cardInstanceId);

        public void HideActiveCard() =>
            _activeCardBanner?.Hide();

        void ApplyPlanningButtonIcons()
        {
            if (restartButton != null)
                restartButton.gameObject.SetActive(false);

            if (_uiIcons == null)
                return;

            if (!ShouldShowBattlePlanningChrome())
                return;

            PlanningActionButtonStyle.Apply(confirmButton, _uiIcons.UiButton1, "出牌");
            PlanningActionButtonStyle.Apply(skipButton, _uiIcons.UiButton2, "空过");

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.transform.SetSiblingIndex(0);
            }

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.transform.SetSiblingIndex(1);
            }

            var actions = transform.Find("HudChromeRoot/PlanningActionsRight")
                ?? transform.Find("PlanningActionsRight");
            BattleUiLayoutRuntimeFix.FixActionBarPublic(actions?.Find("ActionBar"));
        }

        void ResolveHudReferences()
        {
            if (energyValueText == null)
            {
                energyValueText = transform.Find("HudChromeRoot/EnergyHud/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("EnergyHud/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("PlanningInfoLeft/EnergyRow/EnergyValue")?.GetComponent<Text>()
                    ?? transform.Find("PlanningBar/EnergyRow/EnergyValue")?.GetComponent<Text>();
            }

            if (hudEnergyIcon == null)
            {
                hudEnergyIcon = transform.Find("HudChromeRoot/EnergyHud/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("EnergyHud/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("PlanningInfoLeft/EnergyRow/EnergyIcon")?.GetComponent<Image>()
                    ?? transform.Find("PlanningBar/EnergyIcon")?.GetComponent<Image>();
            }

            if (titleText == null)
            {
                titleText = transform.Find("HudChromeRoot/PlanningInfoLeft/Title")?.GetComponent<Text>()
                    ?? transform.Find("PlanningInfoLeft/Title")?.GetComponent<Text>()
                    ?? transform.Find("PlanningBar/Title")?.GetComponent<Text>();
            }
        }

        Transform HudRoot => BattleUiLayoutRuntimeFix.GetHudChromeRoot(transform) ?? transform;

        void ApplyTypographyPolish()
        {
            if (titleText != null)
                titleText.fontSize = Mathf.Max(titleText.fontSize, 24);
            if (subtitleText != null)
                subtitleText.fontSize = Mathf.Max(subtitleText.fontSize, 16);
            if (enemyIntentText != null)
                enemyIntentText.fontSize = Mathf.Max(enemyIntentText.fontSize, 17);
            if (targetPromptText != null)
                targetPromptText.fontSize = Mathf.Max(targetPromptText.fontSize, 19);
            if (selectedQueueText != null)
                selectedQueueText.fontSize = Mathf.Max(selectedQueueText.fontSize, 14);
            if (keywordTooltipText != null)
                keywordTooltipText.fontSize = Mathf.Max(keywordTooltipText.fontSize, 16);
            if (energyValueText != null)
                energyValueText.fontSize = Mathf.Max(energyValueText.fontSize, 24);
            FixEnergyIconLayout(hudEnergyIcon);
        }

        public static void FixEnergyIconLayout(Image icon)
        {
            if (icon == null)
                return;

            const float size = 56f;
            var le = icon.GetComponent<LayoutElement>() ?? icon.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            le.minWidth = size;
            le.minHeight = size;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var rt = icon.rectTransform;
            rt.sizeDelta = new Vector2(size, size);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        void EnsurePlanningEnergyHud()
        {
            ResolveHudReferences();

            var info = transform.Find("PlanningInfoLeft") ?? transform.Find("PlanningBar");
            var legacyGoldRow = info?.Find("GoldRow");
            if (legacyGoldRow != null)
                Destroy(legacyGoldRow.gameObject);

            var legacyEnergyRow = info?.Find("EnergyRow") as RectTransform;

            var energyHud = HudRoot.Find("EnergyHud") as RectTransform;
            if (energyHud == null)
            {
                var hudGo = new GameObject("EnergyHud", typeof(RectTransform), typeof(Image));
                hudGo.transform.SetParent(HudRoot, false);
                energyHud = hudGo.GetComponent<RectTransform>();
            }

            ApplyEnergyHudPlate(energyHud.GetComponent<Image>());

            var energyRow = energyHud.Find("EnergyRow") as RectTransform;
            if (energyRow == null)
            {
                if (legacyEnergyRow != null)
                {
                    energyRow = legacyEnergyRow;
                    energyRow.SetParent(energyHud, false);
                }
                else
                {
                    var rowGo = new GameObject("EnergyRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                    rowGo.transform.SetParent(energyHud, false);
                    energyRow = rowGo.GetComponent<RectTransform>();
                }
            }

            energyRow.anchorMin = Vector2.zero;
            energyRow.anchorMax = Vector2.one;
            energyRow.pivot = new Vector2(0.5f, 0.5f);
            energyRow.offsetMin = new Vector2(10f, 8f);
            energyRow.offsetMax = new Vector2(-10f, -8f);
            energyRow.gameObject.SetActive(true);

            var layout = energyRow.GetComponent<HorizontalLayoutGroup>()
                ?? energyRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            if (hudEnergyIcon == null && legacyEnergyRow != null)
                hudEnergyIcon = legacyEnergyRow.Find("EnergyIcon")?.GetComponent<Image>();
            if (energyValueText == null && legacyEnergyRow != null)
                energyValueText = legacyEnergyRow.Find("EnergyValue")?.GetComponent<Text>();

            if (hudEnergyIcon == null)
            {
                var iconGo = new GameObject("EnergyIcon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(energyRow, false);
                hudEnergyIcon = iconGo.GetComponent<Image>();
            }

            if (hudEnergyIcon != null)
            {
                hudEnergyIcon.transform.SetParent(energyRow, false);
                hudEnergyIcon.transform.SetAsFirstSibling();
                hudEnergyIcon.gameObject.SetActive(true);
                ApplyEnergyIconSprite();
                FixEnergyIconLayout(hudEnergyIcon);
            }

            if (energyValueText == null)
            {
                var textGo = new GameObject("EnergyValue", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(energyRow, false);
                energyValueText = textGo.GetComponent<Text>();
                energyValueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                energyValueText.alignment = TextAnchor.MiddleLeft;
            }

            if (energyValueText != null)
            {
                if (energyValueText.transform.parent != energyRow)
                    energyValueText.transform.SetParent(energyRow, false);
                energyValueText.gameObject.SetActive(true);
                energyValueText.fontSize = Mathf.Max(energyValueText.fontSize, 28);
                energyValueText.fontStyle = FontStyle.Bold;
                energyValueText.color = Color.white;
                energyValueText.raycastTarget = false;
            }

            if (legacyEnergyRow != null && legacyEnergyRow != energyRow)
                legacyEnergyRow.gameObject.SetActive(false);

            BattleUiLayoutRuntimeFix.LayoutEnergyHud(energyHud);
            LayoutRebuilder.ForceRebuildLayoutImmediate(energyRow);
        }

        void ApplyEnergyHudPlate(Image hudBg)
        {
            if (hudBg == null)
                return;

            hudBg.raycastTarget = false;
            var plate = _uiIcons != null ? _uiIcons.UiButton5 : null;
            if (plate != null)
            {
                hudBg.sprite = plate;
                hudBg.type = Image.Type.Simple;
                hudBg.preserveAspect = false;
                hudBg.color = Color.white;
            }
            else
            {
                // 无美术底板时不加自制半透明虚影框，只留水晶+数字
                hudBg.sprite = null;
                hudBg.color = Color.clear;
            }
        }

        void EnsureInventoryHud()
        {
            if (_inventoryButton == null)
            {
                var go = new GameObject("InventoryButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(HudRoot, false);

                var img = go.GetComponent<Image>();
                img.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
                img.raycastTarget = true;

                var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(go.transform, false);
                _inventoryFallbackLabel = labelGo.GetComponent<Text>();
                _inventoryFallbackLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                _inventoryFallbackLabel.fontSize = 18;
                _inventoryFallbackLabel.fontStyle = FontStyle.Bold;
                _inventoryFallbackLabel.alignment = TextAnchor.MiddleCenter;
                _inventoryFallbackLabel.color = new Color(0.92f, 0.94f, 0.98f, 1f);
                _inventoryFallbackLabel.text = "包";
                _inventoryFallbackLabel.raycastTarget = false;
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;

                _inventoryButton = go.GetComponent<Button>();
                _inventoryButton.targetGraphic = img;
                _inventoryButton.onClick.AddListener(ToggleInventoryPanel);

                _inventoryPanel = gameObject.AddComponent<BattleInventoryPanelView>();
                _inventoryPanel.Initialize(
                    _session,
                    transform,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _relicCatalog,
                    _consumableCatalog,
                    _uiIcons,
                    _definitions);
                _inventoryPanel.OnConsumableUseStarted += () =>
                {
                    _inventoryPanel.Hide();
                    Refresh();
                };
            }

            if (_consumableReplaceOverlay == null)
            {
                _consumableReplaceOverlay = gameObject.AddComponent<ConsumableReplaceOverlayView>();
                _consumableReplaceOverlay.Initialize(_session, transform, _consumableCatalog);
            }

            if (_cardDeckReplaceOverlay == null)
            {
                _cardDeckReplaceOverlay = gameObject.AddComponent<CardDeckReplaceOverlayView>();
                _cardDeckReplaceOverlay.Initialize(
                    _session,
                    transform,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _uiIcons,
                    _definitions);
            }

            if (_cardPackPickOverlay == null)
            {
                _cardPackPickOverlay = gameObject.AddComponent<CardPackPickOverlayView>();
                _cardPackPickOverlay.Initialize(
                    _session,
                    transform,
                    _uiIcons,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _definitions);
            }

            if (_altarOverlay == null)
            {
                _altarOverlay = gameObject.AddComponent<ExpeditionAltarOverlayView>();
                _altarOverlay.Initialize(
                    _session,
                    transform,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _uiIcons,
                    _definitions);
            }

            ApplyInventoryButtonLayout();
        }

        void EnsureTurnLogHud()
        {
            if (_turnLogButton != null)
                return;

            var go = new GameObject("TurnLogButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(HudRoot, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
            img.raycastTarget = true;

            _turnLogButton = go.GetComponent<Button>();
            _turnLogButton.targetGraphic = img;
            _turnLogButton.onClick.AddListener(ToggleTurnDetailPanel);

            _turnDetailPanel = gameObject.AddComponent<BattleTurnDetailPanelView>();
            _turnDetailPanel.Initialize(_session, transform, _uiIcons);
            ApplyTurnLogButtonLayout();
        }

        void EnsureCodexHud()
        {
            if (_codexButton != null)
                return;

            var go = new GameObject("CodexButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(HudRoot, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.12f, 0.22f, 0.96f);
            img.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 16;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.95f, 0.88f, 1f, 1f);
            label.text = "图鉴";
            label.raycastTarget = false;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            _codexButton = go.GetComponent<Button>();
            _codexButton.targetGraphic = img;
            _codexButton.onClick.AddListener(ToggleCodexPanel);

            _codexOverlay = gameObject.GetComponent<CardCodexOverlayView>()
                            ?? gameObject.AddComponent<CardCodexOverlayView>();
            _codexOverlay.Initialize(
                transform,
                handPanel?.CardPrefab,
                _catalog,
                _characterVisuals,
                _uiIcons,
                _definitions,
                OnCodexCardAddToHand,
                titleHint: null,
                closeOnSelect: true,
                relicCatalog: _relicCatalog,
                onGrantRelic: OnCodexGrantRelic);

            ApplyCodexButtonLayout();
        }

        void EnsureDummyPlayHud()
        {
            if (_dummyPlayButton != null)
            {
                EnsureMonsterSpawnHud();
                return;
            }

            EnsureCodexHud();

            var parent = _codexButton != null ? _codexButton.transform.parent : HudRoot;
            var go = new GameObject("DummyPlayButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.22f, 0.14f, 0.1f, 0.96f);
            img.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 15;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 0.9f, 0.75f, 1f);
            label.text = "假人出牌";
            label.raycastTarget = false;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            _dummyPlayButton = go.GetComponent<Button>();
            _dummyPlayButton.targetGraphic = img;
            _dummyPlayButton.onClick.AddListener(ToggleDummyPlayPanel);

            ApplyDummyPlayButtonLayout();
            go.SetActive(false);

            EnsureMonsterSpawnHud();
        }

        void EnsureMonsterSpawnHud()
        {
            if (_monsterSpawnButton != null)
                return;

            var parent = _dummyPlayButton != null ? _dummyPlayButton.transform.parent : HudRoot;
            var go = new GameObject("MonsterSpawnButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.12f, 0.2f, 0.18f, 0.96f);
            img.raycastTarget = true;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 15;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.8f, 1f, 0.85f, 1f);
            label.text = "测试怪物";
            label.raycastTarget = false;
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            _monsterSpawnButton = go.GetComponent<Button>();
            _monsterSpawnButton.targetGraphic = img;
            _monsterSpawnButton.onClick.AddListener(ToggleMonsterSpawnPanel);

            if (_monsterSpawnOverlay == null)
            {
                var overlayGo = new GameObject("MonsterSpawnOverlay", typeof(RectTransform));
                overlayGo.transform.SetParent(transform, false);
                _monsterSpawnOverlay = overlayGo.AddComponent<MonsterSpawnOverlayView>();
                _monsterSpawnOverlay.Initialize(transform);
            }

            ApplyMonsterSpawnButtonLayout();
            go.SetActive(false);
        }

        void ToggleMonsterSpawnPanel()
        {
            if (_monsterSpawnOverlay == null)
                EnsureMonsterSpawnHud();
            if (_monsterSpawnOverlay == null)
                return;

            if (!_monsterSpawnOverlay.IsOpen)
                CloseOtherOverlays(_monsterSpawnOverlay);

            _monsterSpawnOverlay.Toggle(OnMonsterSpawnSelected);
        }

        void OnMonsterSpawnSelected(TrainingMonsterCatalog.Entry entry)
        {
            if (entry?.Template == null || _session == null)
                return;

            if (!_session.TrySpawnTrainingMonster(entry.Template, out var failReason))
            {
                if (!string.IsNullOrEmpty(failReason))
                    Debug.LogWarning("[训练场] " + failReason);
                return;
            }

            Refresh();
        }

        void OnCodexCardAddToHand(CardDefinitionSO def)
        {
            if (def == null || _session == null)
                return;

            if (!_session.CanInteractWithBattle())
                return;

            var template = def.ToTemplate();
            if (_session.TryAddCardToHand(template))
                Refresh();
        }

        void OnCodexGrantRelic(RelicDefinition relic)
        {
            if (relic == null || _session == null)
                return;

            _session.TryGrantRelic(relic.Id);
            Refresh();
        }

        void OnDummyPlayCardSelected(CardDefinitionSO def)
        {
            if (def == null || _session == null)
                return;

            if (!_session.CanInteractWithBattle())
                return;

            var template = def.ToTemplate();
            if (_session.TryEnqueueDummyIntent(template))
                Refresh();
        }

        void EnsurePresentationSpeedHud()
        {
            if (_presentationSpeedToggle == null)
                _presentationSpeedToggle = gameObject.AddComponent<BattlePresentationSpeedToggleView>();

            _presentationSpeedToggle.EnsureCreated(HudRoot, _uiIcons);
        }

        void EnsureSettingsHud()
        {
            if (_settingsButton == null)
            {
                var go = new GameObject("BattleSettingsButton", typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(HudRoot, false);

                var img = go.GetComponent<Image>();
                img.raycastTarget = true;
                img.preserveAspect = true;
                img.type = Image.Type.Simple;

                _settingsButton = go.GetComponent<Button>();
                _settingsButton.targetGraphic = img;
                _settingsButton.onClick.AddListener(() => _onOpenSettings?.Invoke());
                UiAudioHooks.WireButton(_settingsButton);
            }

            ApplySettingsButtonVisual();
            BattleButtonPressFeedback.Apply(_settingsButton);
            BattleUiLayoutRuntimeFix.LayoutSettingsButton(_settingsButton.transform as RectTransform);
            BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(_settingsButton.transform as RectTransform);
        }

        void ApplySettingsButtonVisual()
        {
            if (_settingsButton == null)
                return;

            var img = _settingsButton.GetComponent<Image>();
            var sprite = _uiIcons != null ? _uiIcons.UiSettingButton : null;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
            }
        }

        void ToggleCodexPanel()
        {
            var willOpen = _codexOverlay == null || !_codexOverlay.IsOpen;
            if (willOpen)
            {
                CloseOtherOverlays(_codexOverlay);
                _codexOverlay?.ConfigureSelection(OnCodexCardAddToHand, titleHint: null, closeOnSelect: true, showRelics: true);
            }

            _codexOverlay?.RefreshCardPrefab(handPanel?.CardPrefab);
            _codexOverlay?.Toggle();
        }

        void ToggleDummyPlayPanel()
        {
            if (_codexOverlay == null)
                EnsureCodexHud();
            if (_codexOverlay == null)
                return;

            var opening = !_codexOverlay.IsOpen;
            if (opening)
                CloseOtherOverlays(_codexOverlay);

            _codexOverlay.ConfigureSelection(
                OnDummyPlayCardSelected,
                titleHint: "假人出牌 — 点击按序加入意图　",
                closeOnSelect: false,
                showRelics: false);
            _codexOverlay.RefreshCardPrefab(handPanel?.CardPrefab);

            if (opening)
                _codexOverlay.Show();
            else
                _codexOverlay.Hide();
        }

        // 任意 UI 浮层（背包/图鉴/地图/明细）打开时，关闭其余已打开的浮层，避免叠层信息过载。
        void CloseOtherOverlays(object keepOpen)
        {
            if (!ReferenceEquals(keepOpen, _turnDetailPanel) && _turnDetailPanel != null && _turnDetailPanel.IsOpen)
                _turnDetailPanel.Hide();
            if (!ReferenceEquals(keepOpen, _codexOverlay) && _codexOverlay != null && _codexOverlay.IsOpen)
                _codexOverlay.Hide();
            if (!ReferenceEquals(keepOpen, _monsterSpawnOverlay) && _monsterSpawnOverlay != null && _monsterSpawnOverlay.IsOpen)
                _monsterSpawnOverlay.Hide();
            if (!ReferenceEquals(keepOpen, _mapPanel) && _mapPanel != null && _mapPanel.IsOpen)
                _mapPanel.Hide();
            if (!ReferenceEquals(keepOpen, _inventoryPanel) && _inventoryPanel != null && _inventoryPanel.IsOpen)
            {
                _inventoryPanel.Hide();
                ApplyInventoryBackdrop(false);
            }
        }

        void ApplyCodexButtonLayout()
        {
            if (_codexButton == null)
                return;

            BattleUiLayoutRuntimeFix.LayoutCodexButton(_codexButton.transform as RectTransform);
            ApplyFramedUtilityButton(_codexButton.GetComponent<Image>(), null, keepLabel: true);
        }

        void ApplyDummyPlayButtonLayout()
        {
            if (_dummyPlayButton == null)
                return;

            var codexRt = _codexButton != null ? _codexButton.transform as RectTransform : null;
            BattleUiLayoutRuntimeFix.LayoutDummyPlayButton(
                _dummyPlayButton.transform as RectTransform,
                codexRt);
            ApplyFramedUtilityButton(_dummyPlayButton.GetComponent<Image>(), null, keepLabel: true);
            ApplyMonsterSpawnButtonLayout();
        }

        void ApplyMonsterSpawnButtonLayout()
        {
            if (_monsterSpawnButton == null)
                return;

            BattleUiLayoutRuntimeFix.LayoutMonsterSpawnButton(
                _monsterSpawnButton.transform as RectTransform,
                _dummyPlayButton != null ? _dummyPlayButton.transform as RectTransform : null);
            ApplyFramedUtilityButton(_monsterSpawnButton.GetComponent<Image>(), null, keepLabel: true);
        }

        /// <summary>button5 外框 + 内嵌图标；无图标时仅底板（可保留 Label）。</summary>
        void ApplyFramedUtilityButton(Image frameImage, Sprite icon, bool keepLabel = false)
        {
            if (frameImage == null)
                return;

            var frame = _uiIcons != null ? _uiIcons.UiButton5 : null;
            if (frame != null)
            {
                frameImage.sprite = frame;
                frameImage.type = Image.Type.Simple;
                frameImage.preserveAspect = true;
                frameImage.color = Color.white;
            }
            else if (icon != null)
            {
                frameImage.sprite = icon;
                frameImage.type = Image.Type.Simple;
                frameImage.preserveAspect = true;
                frameImage.color = Color.white;
            }
            else
            {
                frameImage.sprite = null;
                frameImage.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
            }

            var host = frameImage.transform;
            var iconTr = host.Find("FrameIcon");
            if (icon != null && frame != null)
            {
                if (iconTr == null)
                {
                    var go = new GameObject("FrameIcon", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(host, false);
                    iconTr = go.transform;
                }

                var iconRt = iconTr as RectTransform;
                iconRt.anchorMin = new Vector2(0.18f, 0.18f);
                iconRt.anchorMax = new Vector2(0.82f, 0.82f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                var iconImg = iconTr.GetComponent<Image>();
                iconImg.sprite = icon;
                iconImg.preserveAspect = true;
                iconImg.type = Image.Type.Simple;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
                iconTr.gameObject.SetActive(true);

                if (!keepLabel)
                {
                    var label = host.Find("Label");
                    if (label != null)
                        label.gameObject.SetActive(false);
                }
            }
            else if (iconTr != null)
            {
                iconTr.gameObject.SetActive(false);
            }

            var button = frameImage.GetComponent<Button>();
            if (button != null)
                BattleButtonPressFeedback.Apply(button);
        }

        void EnsureMapHud()
        {
            if (_mapButton != null)
                return;

            var go = new GameObject("MapButton", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(HudRoot, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.14f, 0.15f, 0.2f, 0.96f);
            img.raycastTarget = true;

            _mapButton = go.GetComponent<Button>();
            _mapButton.targetGraphic = img;
            _mapButton.onClick.AddListener(ToggleMapPanel);

            _mapPanel = gameObject.AddComponent<ExpeditionMapPanelView>();
            _mapPanel.Initialize(_session, transform, _uiIcons);
            ApplyMapButtonLayout();
        }

        void ToggleMapPanel()
        {
            var willOpen = _mapPanel == null || !_mapPanel.IsOpen;
            if (willOpen)
                CloseOtherOverlays(_mapPanel);
            _mapPanel?.Toggle();
            if (_mapPanel != null && _mapPanel.IsOpen)
                _mapPanel.Refresh();
        }

        void ApplyMapButtonLayout()
        {
            if (_mapButton == null)
                return;

            BattleUiLayoutRuntimeFix.LayoutMapButton(_mapButton.transform as RectTransform);

            var img = _mapButton.GetComponent<Image>();
            ApplyFramedUtilityButton(img, _uiIcons != null ? _uiIcons.MapIcon : null);
            BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(_mapButton.transform as RectTransform);
        }

        void EnsureExpeditionPresentation()
        {
            if (_backgroundView == null)
            {
                _backgroundView = gameObject.AddComponent<BattleBackgroundView>();
                _backgroundView.EnsureBuilt(transform, _uiIcons?.CaveBackground);
            }

            if (_postBattleOverlay == null)
            {
                _postBattleOverlay = gameObject.AddComponent<ExpeditionPostBattleOverlayView>();
                _postBattleOverlay.Initialize(
                    _session,
                    transform,
                    _uiIcons,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _relicCatalog,
                    _consumableCatalog,
                    _definitions);
            }

            if (_shopOverlay == null)
            {
                _shopOverlay = gameObject.AddComponent<ExpeditionShopOverlayView>();
                _shopOverlay.Initialize(
                    _session,
                    transform,
                    _uiIcons,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _relicCatalog,
                    _consumableCatalog,
                    _definitions);
            }

            EnsureExpeditionOverlays();
        }

        void EnsureExpeditionOverlays()
        {
            if (_nodeInteractOverlay == null)
            {
                _nodeInteractOverlay = gameObject.AddComponent<ExpeditionNodeInteractOverlayView>();
                _nodeInteractOverlay.Initialize(_session, transform, _uiIcons);
            }

            if (_eventSequenceOverlay == null)
            {
                _eventSequenceOverlay = gameObject.AddComponent<ExpeditionEventInteractSequenceView>();
                _eventSequenceOverlay.Initialize(
                    _session,
                    transform,
                    handPanel?.CardPrefab,
                    _catalog,
                    _characterVisuals,
                    _uiIcons,
                    _definitions);
            }
        }

        void RefreshExpeditionOverlays()
        {
            if (!_session.IsExpeditionMode)
                return;

            _nodeInteractOverlay?.Refresh();
            _eventSequenceOverlay?.Refresh();
        }

        void ApplyTurnLogButtonLayout()
        {
            if (_turnLogButton == null)
                return;

            var rt = _turnLogButton.transform as RectTransform;
            BattleUiLayoutRuntimeFix.LayoutTurnLogButton(rt);

            var img = _turnLogButton.GetComponent<Image>();
            ApplyFramedUtilityButton(img, _uiIcons != null ? _uiIcons.NoteIcon : null);
            BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(rt);
        }

        void ToggleTurnDetailPanel()
        {
            var willOpen = _turnDetailPanel == null || !_turnDetailPanel.IsOpen;
            if (willOpen)
                CloseOtherOverlays(_turnDetailPanel);
            _turnDetailPanel?.Toggle();
            if (_turnDetailPanel != null && _turnDetailPanel.IsOpen)
                _turnDetailPanel.Refresh();
        }

        bool _applyingLateHudLayout;

        public void ApplyLateHudLayout()
        {
            if (_applyingLateHudLayout)
                return;

            _applyingLateHudLayout = true;
            try
            {
                EnsurePlanningEnergyHud();
                EnsureSettingsHud();
                EnsureInventoryHud();
                EnsureTurnLogHud();
                EnsureMapHud();
                if (ShouldShowBattlePlanningChrome())
                    EnsurePresentationSpeedHud();

                ApplyInventoryButtonLayout();
                ApplyTurnLogButtonLayout();
                ApplyMapButtonLayout();
                ApplyCodexButtonLayout();
                ApplyDummyPlayButtonLayout();
                ApplySettingsButtonVisual();
                if (_settingsButton != null)
                {
                    BattleUiLayoutRuntimeFix.LayoutSettingsButton(_settingsButton.transform as RectTransform);
                    BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(_settingsButton.transform as RectTransform);
                }

                BattleUiLayoutRuntimeFix.RefreshBottomHud(transform);
                if (ShouldShowBattlePlanningChrome())
                    ApplyPlanningButtonIcons();
                RefreshPlanningChromeVisibility();
            }
            finally
            {
                _applyingLateHudLayout = false;
            }
        }

        public void NotifyLayoutApplied()
        {
            handPanel?.ReapplyPoolLayout();
            _inventoryPanel?.Refresh();

            InvalidateAllEnemyHpBarLayouts();

            EnsurePlanningEnergyHud();
            BattleUiLayoutRuntimeFix.RefreshBottomHud(transform);
            if (ShouldShowBattlePlanningChrome())
                ApplyPlanningButtonIcons();
            RefreshPlanningChromeVisibility();
            _activeCardBanner?.Relayout();

            if (_session?.Engine?.State != null && ShouldShowBattlePlanningChrome())
                RefreshBattlefield(_session.Engine.State, _session.Engine.Draft);
        }

        bool _escUiSuppressed;

        public void SetEscUiSuppressed(bool suppressed)
        {
            if (_escUiSuppressed == suppressed)
                return;

            _escUiSuppressed = suppressed;
            if (!suppressed)
            {
                // 兜底：若曾误把 BattleScreen 根关掉，先恢复再 Refresh
                if (!gameObject.activeSelf)
                    gameObject.SetActive(true);
                Refresh();
                if (_session?.Engine?.State?.Phase == TurnPhase.Planning && !_session.PresentationLocked)
                    BeginPlanningIdleLoops();
                return;
            }

            RefreshPlanningChromeVisibility();
            handPanel?.gameObject.SetActive(false);
            if (_session?.Engine?.State != null)
                RefreshBattlefield(_session.Engine.State, _session.Engine.Draft);
        }

        void RefreshPlanningChromeVisibility()
        {
            var showBattleChrome = ShouldShowBattlePlanningChrome() && !_escUiSuppressed;

            var intent = enemyIntentPanel != null
                ? enemyIntentPanel.transform
                : transform.Find("HudChromeRoot/EnemyIntentPanel") ?? transform.Find("EnemyIntentPanel");
            var actions = transform.Find("HudChromeRoot/PlanningActionsRight")
                ?? transform.Find("PlanningActionsRight");
            var orderBar = transform.Find("HudChromeRoot/ActionOrderBar");
            var energyHud = HudRoot.Find("EnergyHud");
            var info = transform.Find("HudChromeRoot/PlanningInfoLeft") ?? transform.Find("PlanningInfoLeft");

            info?.gameObject.SetActive(false);
            // 旧意图灰框永不显示，避免挡住加速等按钮
            intent?.gameObject.SetActive(false);
            actions?.gameObject.SetActive(showBattleChrome);
            if (!showBattleChrome)
                orderBar?.gameObject.SetActive(false);
            energyHud?.gameObject.SetActive(showBattleChrome);
            selectedQueuePanel?.SetActive(false);

            if (!showBattleChrome)
            {
                targetPromptPanel?.SetActive(false);
                confirmButton?.gameObject.SetActive(false);
                skipButton?.gameObject.SetActive(false);
                _activeCardBanner?.Hide();
            }

            // Esc 会把图鉴/背包等 SetActive(false)；关闭后必须显式恢复，否则会永久消失。
            // 局外（选路/事件/商店等）也要能打开背包、明细、地图、设置。
            var showMetaUtility = !_escUiSuppressed;
            var showBattleOnlyUtility = showBattleChrome;

            _inventoryButton?.gameObject.SetActive(showMetaUtility);
            _mapButton?.gameObject.SetActive(showMetaUtility);
            _turnLogButton?.gameObject.SetActive(showMetaUtility);
            _settingsButton?.gameObject.SetActive(showMetaUtility);

            _codexButton?.gameObject.SetActive(showBattleOnlyUtility);
            if (_presentationSpeedToggle != null)
            {
                var speedGo = HudRoot.Find("PresentationSpeedButton");
                speedGo?.gameObject.SetActive(showBattleOnlyUtility);
            }

            var showDummyPlay = showBattleOnlyUtility && _session?.Expedition?.Run?.IsTrainingGround == true;
            if (showDummyPlay)
            {
                ApplyDummyPlayButtonLayout();
                _dummyPlayButton?.gameObject.SetActive(true);
                _monsterSpawnButton?.gameObject.SetActive(true);
            }
            else
            {
                _dummyPlayButton?.gameObject.SetActive(false);
                _monsterSpawnButton?.gameObject.SetActive(false);
                _monsterSpawnOverlay?.Hide();
            }

            if (_escUiSuppressed)
            {
                handPanel?.gameObject.SetActive(false);
                // 绝不能 _actionOrderBar.gameObject.SetActive(false)：
                // ActionOrderBar 组件挂在 BattleScreen 根上，会把整棵战斗 UI 关掉导致蓝屏。
                _actionOrderBar?.SetVisible(false);
            }
        }

        bool ShouldShowBattlePlanningChrome()
        {
            if (_escUiSuppressed)
                return false;

            return ShouldShowBattleSlots();
        }

        /// <summary>战场立绘/背景是否应显示（不受 ESC HUD 抑制影响）。</summary>
        bool ShouldShowBattleSlots()
        {
            if (_session == null || !_session.IsExpeditionMode)
                return true;

            return _session.Expedition.Run.Phase == ExpeditionPhase.InBattle;
        }

        void ApplyEnergyIconSprite()
        {
            if (hudEnergyIcon == null)
                return;

            var sprite = _uiIcons != null ? _uiIcons.EnergyIcon : null;
            hudEnergyIcon.sprite = sprite;
            hudEnergyIcon.type = Image.Type.Simple;
            hudEnergyIcon.enabled = true;
            hudEnergyIcon.preserveAspect = true;
            hudEnergyIcon.color = sprite != null ? Color.white : new Color(0.75f, 0.55f, 1f, 1f);
        }

        void ApplyInventoryButtonLayout()
        {
            if (_inventoryButton == null)
                return;

            var rt = _inventoryButton.transform as RectTransform;
            BattleUiLayoutRuntimeFix.LayoutInventoryButton(rt);

            var img = _inventoryButton.GetComponent<Image>();
            var icon = _uiIcons != null ? _uiIcons.InventoryIcon : null;
            ApplyFramedUtilityButton(img, icon);
            if (_inventoryFallbackLabel != null)
                _inventoryFallbackLabel.enabled = icon == null && (_uiIcons == null || _uiIcons.UiButton5 == null);
            BattleUiLayoutRuntimeFix.PromoteHudControlOverlay(rt);
        }

        void ToggleInventoryPanel()
        {
            var willOpen = _inventoryPanel == null || !_inventoryPanel.IsOpen;
            if (willOpen)
            {
                CloseOtherOverlays(_inventoryPanel);
                GameAudioService.Instance.PlayUiInventoryOpen();
            }
            else
            {
                GameAudioService.Instance.PlayUiInventoryClose();
            }

            _inventoryPanel?.Toggle();
            var open = _inventoryPanel != null && _inventoryPanel.IsOpen;
            if (open)
                _inventoryPanel.Refresh();
            ApplyInventoryBackdrop(open);
        }

        void ApplyInventoryBackdrop(bool inventoryOpen)
        {
            if (inventoryOpen)
            {
                _postBattleOverlay?.Hide();
                if (_mapPanel != null && _mapPanel.IsOpen)
                    _mapPanel.Toggle();
            }

            Refresh();
        }

        void ConfigureBattlefieldSlots()
        {
            for (var i = 0; i < playerSlots.Length && i < SlotOrder.Length; i++)
                playerSlots[i]?.Configure(SlotOrder[i], TeamSide.Player, "我方", mirror: false);
            for (var i = 0; i < enemySlots.Length && i < SlotOrder.Length; i++)
                // 玩家原画朝右、敌人原画朝左（均面向场地中央），禁止水平翻转
                enemySlots[i]?.Configure(SlotOrder[i], TeamSide.Enemy, "敌方", mirror: false);
        }

        void ConfigureKeywordTooltipRaycast()
        {
            if (keywordTooltipPanel == null)
                return;

            foreach (var graphic in keywordTooltipPanel.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            ApplyKeywordTooltipPlate();
        }

        void ApplyKeywordTooltipPlate()
        {
            if (keywordTooltipPanel == null)
                return;

            var img = keywordTooltipPanel.GetComponent<Image>();
            if (img == null)
                img = keywordTooltipPanel.AddComponent<Image>();

            var plate = _uiIcons != null ? _uiIcons.UiInformationPlate : null;
            if (plate != null)
            {
                img.sprite = plate;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
                img.color = Color.white;
            }

            foreach (var fx in keywordTooltipPanel.GetComponents<Outline>())
                Destroy(fx);
            img.raycastTarget = false;
        }

        public void Refresh()
        {
            if (_session == null)
                return;

            if (_session.Engine?.State != null)
            {
                var state = _session.Engine.State;
                var draft = _session.Engine.Draft;
                var expeditionBlocks = _session.ExpeditionBlocksInput;

                RefreshHud(state);
                RefreshBattlefield(state, draft);
                RefreshActionTimeline(state, draft);
                RefreshTargetPrompt(state, draft);
                RefreshTargetCancelBackdrop(draft);
                RefreshActions(state, expeditionBlocks);
                RefreshFelskullChoice(state);
                RefreshPsionicScry(state);
            }
            else
            {
                // 选路线等非战斗阶段 Engine 为空：必须关掉战场立绘，否则会残留上一场战斗的槽位
                SetBattlefieldVisible(false);
                ClearCombatantHoverDetails();
                RefreshPlanningChromeVisibility();
            }

            RefreshHand(_session.Engine?.State);

            RefreshExpeditionOverlay();
            RefreshExpeditionPresentation();
            _inventoryPanel?.Refresh();
            _consumableReplaceOverlay?.Refresh();
            _turnDetailPanel?.Refresh();
            if (_session.IsExpeditionMode && !(_inventoryPanel?.IsOpen ?? false))
            {
                _mapPanel?.Refresh();
                RefreshExpeditionOverlays();
                _shopOverlay?.Refresh();
            }

            _cardDeckReplaceOverlay?.Refresh();
            _cardPackPickOverlay?.Refresh();
            _altarOverlay?.Refresh();

            RefreshPlanningChromeVisibility();

            if (_session.Engine?.State != null
                && ShouldShowBattlePlanningChrome()
                && _session.Engine.State.Phase == TurnPhase.Planning
                && !_session.PresentationLocked)
            {
                BeginPlanningIdleLoops();
            }
        }

        void RefreshHud(BattleState state)
        {
            ResolveHudReferences();

            var showBattleChrome = ShouldShowBattlePlanningChrome();
            var energyHud = HudRoot.Find("EnergyHud");
            energyHud?.gameObject.SetActive(showBattleChrome);

            if (!showBattleChrome)
                return;

            ApplyEnergyIconSprite();
            if (hudEnergyIcon != null)
                FixEnergyIconLayout(hudEnergyIcon);

            if (energyValueText != null)
            {
                energyValueText.gameObject.SetActive(true);
                energyValueText.text = $"{state.EnergyCurrent}/{state.EnergyMax}";
                energyValueText.color = Color.white;
            }

            var energyRow = hudEnergyIcon != null ? hudEnergyIcon.transform.parent as RectTransform : null;
            if (energyRow != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(energyRow);

            if (_inventoryButton != null)
                ApplyInventoryButtonLayout();

            if (titleText != null)
                titleText.gameObject.SetActive(false);

            if (subtitleText != null)
                subtitleText.gameObject.SetActive(false);
        }

        void RefreshBattlefield(BattleState state, PlanningDraft draft)
        {
            // ESC 只隐藏 HUD，不隐藏立绘槽；否则关菜单后会出现空战场/蓝屏感
            if (!ShouldShowBattleSlots())
            {
                SetBattlefieldVisible(false);
                ClearCombatantHoverDetails();
                return;
            }

            SetBattlefieldVisible(true);

            var awaitingCard = draft.AwaitingTargetCardId;
            var awaitingConsumable = draft.IsAwaitingConsumableTarget;
            CardInstanceState awaitingCardState = null;
            CombatantState owner = null;
            List<CombatantState> validTargets = null;

            if (awaitingCard != null)
            {
                awaitingCardState = state.GetCard(awaitingCard.Value);
                if (awaitingCardState != null)
                {
                    var ownerId = PositionRules.GetOwnerCombatantId(state, awaitingCardState);
                    owner = ownerId != null ? state.GetCombatant(ownerId) : null;
                    validTargets = CardRules.GetValidTargetCandidates(state, awaitingCardState, owner);
                }
            }
            else if (awaitingConsumable &&
                     ConsumableDatabase.TryGet(draft.AwaitingConsumableId, out var consumableDef))
            {
                validTargets = ConsumableRules.GetValidTargets(state, consumableDef);
            }

            var targetMode = awaitingCardState != null || awaitingConsumable;
            if (!targetMode)
                ClearDamagePreviewTarget(silent: true);

            RefreshSlotRow(enemySlots, state, targetMode, validTargets, _session.PresentationSnapshot, showExpBar: false);
            // 战斗信息框只显示等级/生命/速度等，不显示经验
            RefreshSlotRow(playerSlots, state, targetMode, validTargets, _session.PresentationSnapshot,
                showExpBar: false);
            EnsureEnemyHpBarAlignment();
        }

        void EnsureEnemyHpBarAlignment()
        {
            if (playerSlots == null || enemySlots == null)
                return;

            float? referenceY = null;
            if (playerSlots.Length > 1 && playerSlots[1] != null
                && !string.IsNullOrEmpty(playerSlots[1].CombatantId))
            {
                referenceY = playerSlots[1].GetHpBarWorldY();
            }
            else
            {
                foreach (var slot in playerSlots)
                {
                    if (slot == null || string.IsNullOrEmpty(slot.CombatantId))
                        continue;

                    referenceY = slot.GetHpBarWorldY();
                    break;
                }
            }

            if (!referenceY.HasValue)
                return;

            foreach (var slot in enemySlots)
                slot?.EnsureFixedEnemyHpBarLayout(referenceY.Value);
        }

        public void SyncCombatantSlotLayout(string combatantId)
        {
            if (string.IsNullOrEmpty(combatantId))
                return;

            foreach (var slot in enemySlots)
            {
                if (slot != null && slot.CombatantId == combatantId)
                    slot.SyncEnemyLayoutAfterPresentation();
            }
        }

        public void InvalidateAllEnemyHpBarLayouts()
        {
            if (enemySlots == null)
                return;

            foreach (var slot in enemySlots)
                slot?.InvalidateEnemyHpBarLayout();
        }

        void RefreshSlotRow(
            CombatantSlotView[] slots,
            BattleState state,
            bool targetMode,
            List<CombatantState> validTargets,
            PresentationSnapshot presentation,
            bool showExpBar)
        {
            if (slots == null)
                return;

            foreach (var slot in slots)
                slot?.Refresh(state, targetMode, validTargets, _characterVisuals, _uiIcons, presentation, showExpBar, _session);
        }

        void ClearCombatantHoverDetails()
        {
            ClearSlotRowHoverDetails(enemySlots);
            ClearSlotRowHoverDetails(playerSlots);
        }

        void ClearSlotRowHoverDetails(CombatantSlotView[] slots)
        {
            if (slots == null)
                return;

            foreach (var slot in slots)
                slot?.DismissHoverDetail();
        }

        void RefreshActionTimeline(BattleState state, PlanningDraft draft)
        {
            selectedQueuePanel?.SetActive(false);
            RefreshActionOrderBar(state, draft);

            // 旧右下意图面板已废弃；永久关闭以免灰框挡 UI
            if (enemyIntentPanel != null)
                enemyIntentPanel.SetActive(false);
            var legacyIntent = transform.Find("HudChromeRoot/EnemyIntentPanel") ?? transform.Find("EnemyIntentPanel");
            legacyIntent?.gameObject.SetActive(false);
        }

        void RefreshActionOrderBar(BattleState state, PlanningDraft draft)
        {
            EnsureActionOrderBar();
            if (_actionOrderBar == null)
                return;

            if (!ShouldShowBattlePlanningChrome())
            {
                _actionOrderBar.SetVisible(false);
                return;
            }

            var presenting = _session.PresentationLocked
                && _session.PresentationSnapshot?.HasTurnPresentation == true;
            var planning = state.Phase == TurnPhase.Planning;
            if (!planning && !presenting)
            {
                _actionOrderBar.SetVisible(false);
                return;
            }

            List<ActionOrderVisualEntry> entries;
            if (presenting)
            {
                entries = BattleUiFormatters.BuildActionOrderVisualEntriesFromSnapshot(
                    state, _session.PresentationSnapshot);
            }
            else
            {
                var hasPlayerCards = draft != null && draft.SelectedQueue.Count > 0;
                var steps = _session.Engine.PreviewResolutionSteps();
                entries = BattleUiFormatters.BuildActionOrderVisualEntries(state, draft, steps);
                if (!hasPlayerCards && (entries == null || entries.Count == 0))
                {
                    entries = BattleUiFormatters.BuildActionOrderVisualEntriesFromEnemyIntents(state);
                }
            }

            var hasEntries = entries != null && entries.Count > 0;
            _actionOrderBar.SetVisible(hasEntries);
            if (hasEntries)
                _actionOrderBar.RefreshEntries(entries);
        }

        void RefreshSelectedQueue(BattleState state, PlanningDraft draft)
        {
            selectedQueuePanel?.SetActive(false);
        }

        void RefreshTargetPrompt(BattleState state, PlanningDraft draft)
        {
            if (targetPromptPanel == null)
                return;

            if (!ShouldShowBattlePlanningChrome())
            {
                targetPromptPanel.SetActive(false);
                return;
            }

            var awaiting = draft.AwaitingTargetCardId;
            var awaitingConsumable = draft.IsAwaitingConsumableTarget;
            var show = state.Phase == TurnPhase.Planning && (awaiting != null || awaitingConsumable);
            targetPromptPanel.SetActive(show);
            if (!show || targetPromptText == null)
                return;

            if (awaitingConsumable &&
                ConsumableDatabase.TryGet(draft.AwaitingConsumableId, out var consumableDef))
            {
                var consumableSideLabel = consumableDef.TargetKind switch
                {
                    ConsumableTargetKind.SingleAlly => "队友",
                    ConsumableTargetKind.SingleEnemy => "敌人",
                    _ => "目标"
                };
                targetPromptText.text =
                    $"使用「{consumableDef.DisplayName}」— 点击高亮的{consumableSideLabel}（再点消耗品或空白处取消）";
                return;
            }

            var card = state.GetCard(awaiting.Value);
            if (card == null)
            {
                targetPromptText.text = "请点击高亮单位选择目标";
                return;
            }

            var side = CardRules.GetRequiredTargetPick(card);
            var sideLabel = side switch
            {
                TargetPickSide.Ally => "队友",
                TargetPickSide.Enemy => "敌人",
                _ => "目标"
            };
            targetPromptText.text =
                $"已选「{card.DisplayName}」— 点击高亮的{sideLabel}（再点卡牌取消）";
        }

        void RefreshTargetCancelBackdrop(PlanningDraft draft)
        {
            var show = ShouldShowBattlePlanningChrome()
                && draft != null
                && (draft.AwaitingTargetCardId != null || draft.IsAwaitingConsumableTarget);

            if (!show)
            {
                if (_targetCancelBackdrop != null)
                    _targetCancelBackdrop.gameObject.SetActive(false);
                return;
            }

            EnsureTargetCancelBackdrop();
            _targetCancelBackdrop.gameObject.SetActive(true);
            _targetCancelBackdrop.transform.SetAsFirstSibling();
        }

        void EnsureTargetCancelBackdrop()
        {
            if (_targetCancelBackdrop != null)
                return;

            var go = new GameObject("TargetCancelBackdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.01f);
            img.raycastTarget = true;
            _targetCancelBackdrop = go.GetComponent<Button>();
            _targetCancelBackdrop.onClick.AddListener(() =>
            {
                _session?.CancelTargetSelection();
                Refresh();
            });
            go.SetActive(false);
        }

        void OnDamagePreviewEnter(CombatantState unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.Id))
                return;

            var draft = _session?.Engine?.Draft;
            if (draft?.AwaitingTargetCardId == null)
                return;

            if (_damagePreviewCombatantId == unit.Id)
                return;

            _damagePreviewCombatantId = unit.Id;
            RefreshHand(_session?.Engine?.State);
        }

        void OnDamagePreviewExit() => ClearDamagePreviewTarget();

        void ClearDamagePreviewTarget(bool silent = false)
        {
            if (string.IsNullOrEmpty(_damagePreviewCombatantId))
                return;

            _damagePreviewCombatantId = null;
            if (!silent)
                RefreshHand(_session?.Engine?.State);
        }

        void RefreshHand(BattleState state)
        {
            if (handPanel == null)
                return;

            var showHand = state != null
                           && ShouldShowBattlePlanningChrome()
                           && !state.AwaitingPsionicScry;
            handPanel.gameObject.SetActive(showHand);
            if (!showHand)
                return;

            CombatantState damagePreviewTarget = null;
            if (!string.IsNullOrEmpty(_damagePreviewCombatantId))
                damagePreviewTarget = state.GetCombatant(_damagePreviewCombatantId);

            handPanel.Refresh(
                state,
                _session,
                _catalog,
                _uiIcons,
                _characterVisuals,
                _definitions,
                id =>
                {
                    ClearDamagePreviewTarget(silent: true);
                    _session.ToggleCard(id);
                    Refresh();
                },
                id =>
                {
                    ClearDamagePreviewTarget(silent: true);
                    _session.TryQuickStartCard(id);
                    Refresh();
                },
                ShowKeywordTooltip,
                HideKeywordTooltip,
                damagePreviewTarget);
        }

        void RefreshActions(BattleState state, bool expeditionBlocks)
        {
            var actionsRoot = transform.Find("HudChromeRoot/PlanningActionsRight")
                ?? transform.Find("PlanningActionsRight");

            if (!ShouldShowBattlePlanningChrome())
            {
                actionsRoot?.gameObject.SetActive(false);
                confirmButton?.gameObject.SetActive(false);
                skipButton?.gameObject.SetActive(false);
                return;
            }

            var planning = state.Phase == TurnPhase.Planning
                && !expeditionBlocks
                && !(_presentationBusy?.Invoke() ?? false);

            if (actionsRoot != null)
                actionsRoot.gameObject.SetActive(true);

            if (confirmButton != null)
            {
                confirmButton.gameObject.SetActive(true);
                confirmButton.interactable = planning && _session.Engine.Draft.SelectedQueue.Count > 0;
            }

            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.interactable = planning;
            }

            ApplyPlanningButtonIcons();
        }

        void SetBattlefieldVisible(bool visible)
        {
            foreach (var slot in playerSlots)
            {
                if (slot != null)
                    slot.gameObject.SetActive(visible);
            }

            foreach (var slot in enemySlots)
            {
                if (slot != null)
                    slot.gameObject.SetActive(visible);
            }
        }

        /// <summary>离开战斗 / 非战斗阶段强制隐藏立绘槽。</summary>
        public void HideBattlefieldSlots()
        {
            SetBattlefieldVisible(false);
            ClearCombatantHoverDetails();
        }

        void RefreshExpeditionPresentation()
        {
            var expedition = _session.IsExpeditionMode;
            var training = _session.Expedition?.Run?.IsTrainingGround == true;
            var layer = _session.Expedition?.Run?.Map?.NodesCompleted + 1 ?? 1;
            var phase = _session.Expedition?.Run?.Phase;
            var bg = training
                ? _uiIcons?.TrainingGroundBackground
                : phase == ExpeditionPhase.RouteSelect
                    ? ExpeditionPathArt.ResolveRouteSelectBackground(_uiIcons, layer)
                    : ExpeditionPathArt.ResolveBackground(_uiIcons, layer);
            var bgAlpha = phase == ExpeditionPhase.RouteSelect ? 1f : 0.88f;
            _backgroundView?.EnsureBuilt(transform, bg ?? _uiIcons?.CaveBackground, bgAlpha);
            _backgroundView?.SetVisible(expedition);
            UpdateExpeditionAudio(expedition, layer);

            if (_postBattleOverlay == null)
                return;

            if (_inventoryPanel != null && _inventoryPanel.IsOpen)
            {
                _postBattleOverlay.Hide();
                return;
            }

            if (_presentationBusy?.Invoke() == true)
            {
                _postBattleOverlay.Hide();
                return;
            }

            _postBattleOverlay.Refresh();
            // 奖励/选路遮罩出现时清掉战斗悬停信息框，避免残留
            if (_session.Expedition?.Run?.Phase is ExpeditionPhase.RewardPickup
                or ExpeditionPhase.RouteSelect)
            {
                HideKeywordTooltip();
                ClearCombatantHoverDetails();
            }
        }

        void UpdateExpeditionAudio(bool expedition, int layer)
        {
            var audio = GameAudioService.Instance;
            var inBattle = expedition
                           && _session.Expedition?.Run != null
                           && _session.Expedition.Run.Phase == ExpeditionPhase.InBattle;
            var sandboxBattle = !expedition && _session.Engine?.State != null;

            if (!expedition)
            {
                if (sandboxBattle)
                {
                    if (!_battleBgmActive)
                        audio.PlayBattleBgm(reselect: true);
                    else
                        audio.PlayBattleBgm(reselect: false);
                    _battleBgmActive = true;
                }
                else
                {
                    _battleBgmActive = false;
                }

                return;
            }

            if (inBattle)
            {
                if (!_battleBgmActive)
                    audio.PlayBattleBgm(reselect: true);
                else
                    audio.PlayBattleBgm(reselect: false);
                _battleBgmActive = true;
            }
            else
            {
                _battleBgmActive = false;
                audio.PlayMapBgm(Mathf.Max(1, layer));
            }
        }

        void RefreshExpeditionOverlay()
        {
            if (!_session.IsExpeditionMode)
            {
                expeditionOverlay?.SetActive(false);
                return;
            }

            var phase = _session.Expedition.Run.Phase;
            var showRunEnd = phase is ExpeditionPhase.RunComplete or ExpeditionPhase.RunFailed;
            var presentationBusy = _presentationBusy?.Invoke() ?? false;

            if (expeditionOverlay != null)
            {
                expeditionOverlay.SetActive(showRunEnd && !presentationBusy);
                if (showRunEnd && !presentationBusy)
                {
                    routeSelectPanel?.SetActive(false);
                    runEndPanel?.SetActive(true);

                    if (phase == ExpeditionPhase.RunComplete)
                    {
                        var run = _session.Expedition.Run;
                        var completed = run.Map?.NodesCompleted ?? run.BattlesWon;
                        var total = run.Map?.ChapterLayerCount ?? ExpeditionRegionRules.FullLayerCount;
                        runEndTitleText.text = "远征完成";
                        runEndBodyText.text =
                            $"恭喜通关 {completed}/{total} 层！\n{BattleUiFormatters.FormatPartySummary(run.Party, run.Gold)}";
                        SetRunEndButtonLabel("返回营地");
                    }
                    else if (phase == ExpeditionPhase.RunFailed)
                    {
                        runEndTitleText.text = "远征失败";
                        runEndBodyText.text = string.IsNullOrEmpty(_session.Expedition.Run.LastEventMessage)
                            ? "队伍已无法继续作战。"
                            : _session.Expedition.Run.LastEventMessage;
                        SetRunEndButtonLabel("传送回营地");
                    }
                }
            }
        }

        void SetRunEndButtonLabel(string label)
        {
            if (runRestartButton == null)
                return;

            var text = runRestartButton.GetComponentInChildren<Text>();
            if (text != null)
                text.text = label;
        }

        void ShowKeywordTooltip(CardInstanceState card, RectTransform anchor)
        {
            if (keywordTooltipPanel == null || keywordTooltipText == null || card == null || anchor == null)
            {
                HideKeywordTooltip();
                return;
            }

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLine(
                _session?.Engine?.State, _session?.Engine?.Draft, descCard, definitions: _definitions);
            var keywords = BattleUiFormatters.BuildCardKeywordTooltip(_session?.Engine?.State, descCard, _definitions);
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            if (string.IsNullOrWhiteSpace(body))
            {
                HideKeywordTooltip();
                return;
            }

            keywordTooltipText.supportRichText = true;
            keywordTooltipText.fontStyle = FontStyle.Normal;
            keywordTooltipText.alignment = TextAnchor.UpperLeft;
            keywordTooltipText.horizontalOverflow = HorizontalWrapMode.Wrap;
            keywordTooltipText.verticalOverflow = VerticalWrapMode.Overflow;
            keywordTooltipText.text = body;

            ApplyKeywordTooltipPlate();

            var panel = keywordTooltipPanel.transform as RectTransform;
            if (panel == null)
                return;

            // 固定较窄宽度，高度按正文撑开（information plate 上下内边距）
            const float tooltipWidth = UiInfoPlateMetrics.MaxWidth;
            var innerW = UiInfoPlateMetrics.InnerWidth(tooltipWidth);

            keywordTooltipPanel.SetActive(true);
            var preferred = UiInfoPlateMetrics.MeasureHeight(keywordTooltipText, body, innerW);
            panel.sizeDelta = new Vector2(tooltipWidth, Mathf.Max(72f, preferred + UiInfoPlateMetrics.PadY * 2f));
            UiInfoPlateMetrics.ApplyTextInsets(keywordTooltipText.rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            // 二次测量：避免富文本首次 preferred 偏低
            preferred = UiInfoPlateMetrics.MeasureHeight(keywordTooltipText, body, innerW);
            panel.sizeDelta = new Vector2(tooltipWidth, Mathf.Max(72f, preferred + UiInfoPlateMetrics.PadY * 2f));
            UiInfoPlateMetrics.ApplyTextInsets(keywordTooltipText.rectTransform);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            PositionTooltipBesideCard(panel, anchor);

            var battleRoot = transform;
            CombatantTooltipLayer.MountToFront(panel, battleRoot);
            _keywordTooltipAnchor = anchor;
            _keywordTooltipLeftAt = -1f;
        }

        void PositionTooltipBesideCard(RectTransform panel, RectTransform anchor)
        {
            var canvasRt = panel.GetComponentInParent<Canvas>()?.transform as RectTransform;
            if (canvasRt == null)
                return;

            var anchorCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);
            var anchorRightX = anchorCorners[2].x;
            var anchorCenterY = (anchorCorners[0].y + anchorCorners[1].y) * 0.5f;

            var canvasCorners = new Vector3[4];
            canvasRt.GetWorldCorners(canvasCorners);
            var canvasLeft = canvasCorners[0].x;
            var canvasRight = canvasCorners[2].x;
            var canvasTop = canvasCorners[1].y;
            var canvasBottom = canvasCorners[0].y;

            panel.pivot = new Vector2(0f, 0.5f);
            panel.position = new Vector3(anchorRightX + 16f, anchorCenterY, panel.position.z);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            ClampTooltipX(panel, canvasLeft, canvasRight);

            var panelCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners);

            if (panelCorners[2].x > canvasRight - 12f)
            {
                var anchorLeftX = anchorCorners[0].x;
                panel.pivot = new Vector2(1f, 0.5f);
                panel.position = new Vector3(anchorLeftX - 16f, anchorCenterY, panel.position.z);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
                ClampTooltipX(panel, canvasLeft, canvasRight);
                panel.GetWorldCorners(panelCorners);
            }

            if (panelCorners[1].y > canvasTop - 8f)
            {
                var shift = canvasTop - 8f - panelCorners[1].y;
                panel.position += new Vector3(0f, shift, 0f);
            }
            else if (panelCorners[0].y < canvasBottom + 8f)
            {
                var shift = canvasBottom + 8f - panelCorners[0].y;
                panel.position += new Vector3(0f, shift, 0f);
            }
        }

        void PositionTooltipAboveCard(RectTransform panel, RectTransform anchor)
        {
            var canvasRt = panel.GetComponentInParent<Canvas>()?.transform as RectTransform;
            if (canvasRt == null)
                return;

            var anchorCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);
            var anchorCenterX = (anchorCorners[0].x + anchorCorners[2].x) * 0.5f;
            var anchorTopY = anchorCorners[1].y;
            var anchorBottomY = anchorCorners[0].y;

            var canvasCorners = new Vector3[4];
            canvasRt.GetWorldCorners(canvasCorners);
            var canvasLeft = canvasCorners[0].x;
            var canvasRight = canvasCorners[2].x;
            var canvasTop = canvasCorners[1].y;
            var canvasBottom = canvasCorners[0].y;
            const float handReserved = 300f;

            panel.pivot = new Vector2(0.5f, 0f);
            panel.position = new Vector3(anchorCenterX, anchorTopY + 14f, panel.position.z);
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            ClampTooltipX(panel, canvasLeft, canvasRight);

            var panelCorners = new Vector3[4];
            panel.GetWorldCorners(panelCorners);

            if (panelCorners[1].y > canvasTop - 8f)
            {
                panel.pivot = new Vector2(0.5f, 1f);
                panel.position = new Vector3(panel.position.x, anchorBottomY - 14f, panel.position.z);
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
                panel.GetWorldCorners(panelCorners);
            }

            if (panelCorners[0].y < canvasBottom + handReserved)
            {
                panel.pivot = new Vector2(0.5f, 0f);
                panel.position = new Vector3(panel.position.x, canvasBottom + handReserved + 8f, panel.position.z);
                ClampTooltipX(panel, canvasLeft, canvasRight);
            }
        }

        static void ClampTooltipX(RectTransform panel, float canvasLeft, float canvasRight)
        {
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            var shift = 0f;
            if (corners[2].x > canvasRight - 12f)
                shift = canvasRight - 12f - corners[2].x;
            else if (corners[0].x < canvasLeft + 12f)
                shift = canvasLeft + 12f - corners[0].x;

            if (Mathf.Abs(shift) > 0.01f)
                panel.position += new Vector3(shift, 0f, 0f);
        }

        public void HideKeywordTooltip()
        {
            if (keywordTooltipPanel != null)
                keywordTooltipPanel.SetActive(false);
            _keywordTooltipAnchor = null;
            _keywordTooltipLeftAt = -1f;
        }

        void LateUpdate()
        {
            if (keywordTooltipPanel == null || !keywordTooltipPanel.activeSelf)
                return;

            if (_keywordTooltipAnchor == null || !_keywordTooltipAnchor.gameObject.activeInHierarchy)
            {
                HideKeywordTooltip();
                return;
            }

            if (UiPointerUtility.IsOverRectTransform(
                    _keywordTooltipAnchor,
                    UiPointerUtility.GetEventCamera(_keywordTooltipAnchor)))
            {
                _keywordTooltipLeftAt = -1f;
                return;
            }

            if (_keywordTooltipLeftAt < 0f)
                _keywordTooltipLeftAt = Time.unscaledTime;

            if (Time.unscaledTime - _keywordTooltipLeftAt >= KeywordTooltipStaleHideSeconds)
                HideKeywordTooltip();
        }

        void RefreshFelskullChoice(BattleState state)
        {
            if (_felskullChoice == null)
            {
                _felskullChoice = gameObject.AddComponent<FelskullBattleChoiceView>();
            }

            _felskullChoice.EnsureBuilt(transform, _uiIcons);

            if (state != null && state.AwaitingFelskullChoice)
            {
                _felskullChoice.Show(choice =>
                {
                    _session?.ApplyFelskullChoice(choice);
                    Refresh();
                });
                return;
            }

            _felskullChoice.Hide();
        }

        void RefreshPsionicScry(BattleState state)
        {
            if (_psionicScryOverlay == null)
            {
                _psionicScryOverlay = gameObject.AddComponent<PsionicScryOverlayView>();
                _psionicScryOverlay.Initialize(
                    _session,
                    transform,
                    _uiIcons,
                    HandCardPrefab,
                    _catalog,
                    _characterVisuals,
                    _definitions);
            }

            _psionicScryOverlay.Refresh();
        }

        public System.Collections.Generic.IEnumerable<CombatantPortraitView> AllPortraitViews()
        {
            foreach (var slot in playerSlots)
            {
                if (slot?.PortraitView != null)
                    yield return slot.PortraitView;
            }

            foreach (var slot in enemySlots)
            {
                if (slot?.PortraitView != null)
                    yield return slot.PortraitView;
            }
        }

        public void BeginPlanningIdleLoops()
        {
            foreach (var slot in playerSlots)
                slot?.PortraitView?.BeginPlanningIdle();
            foreach (var slot in enemySlots)
                slot?.PortraitView?.BeginPlanningIdle();
        }

        public void StopAllPortraitIdleLoops()
        {
            foreach (var view in AllPortraitViews())
                view?.StopIdleLoop();
        }

        public Vector3 GetDuelCenterWorldPosition(string actorCombatantId = null)
        {
            var playerFeet = GetTeamFeetReference(playerSlots, preferFrontLine: true);
            var enemyFeet = GetTeamFeetReference(enemySlots, preferFrontLine: true);
            var blend = ResolveDuelCenterBlend(actorCombatantId);

            if (playerFeet.HasValue && enemyFeet.HasValue)
            {
                return new Vector3(
                    Mathf.Lerp(playerFeet.Value.x, enemyFeet.Value.x, blend),
                    (playerFeet.Value.y + enemyFeet.Value.y) * 0.5f,
                    (playerFeet.Value.z + enemyFeet.Value.z) * 0.5f);
            }

            var playerStage = transform.Find("PlayerStage");
            var enemyStage = transform.Find("EnemyStage");
            if (playerStage != null && enemyStage != null)
            {
                return new Vector3(
                    Mathf.Lerp(playerStage.position.x, enemyStage.position.x, blend),
                    (playerStage.position.y + enemyStage.position.y) * 0.5f,
                    (playerStage.position.z + enemyStage.position.z) * 0.5f);
            }

            return transform.position;
        }

        float ResolveDuelCenterBlend(string actorCombatantId)
        {
            if (string.IsNullOrEmpty(actorCombatantId))
                return 0.5f;

            var unit = _session?.Engine?.State?.GetCombatant(actorCombatantId);
            if (unit == null)
                return 0.5f;

            if (unit.Team == TeamSide.Enemy && BossCharacterRules.IsBoss(unit.CharacterDefinitionId))
                return 0.56f;

            return 0.5f;
        }

        static Vector3? GetTeamFeetReference(CombatantSlotView[] slots, bool preferFrontLine = false)
        {
            if (slots == null || slots.Length == 0)
                return null;

            var start = preferFrontLine ? 0 : (slots.Length > 1 ? 1 : 0);
            for (var i = 0; i < slots.Length; i++)
            {
                var pick = slots[(start + i) % slots.Length];
                if (pick == null || string.IsNullOrEmpty(pick.PortraitView?.CombatantId))
                    continue;

                return pick.GetFeetWorldPosition();
            }

            return slots[start]?.GetFeetWorldPosition();
        }

        static Vector3? GetTeamDuelReference(CombatantSlotView[] slots) =>
            GetTeamFeetReference(slots);
    }
}
