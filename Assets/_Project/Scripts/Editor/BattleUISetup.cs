#if UNITY_EDITOR
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Presentation;
using Grimhand.Presentation.Battle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Grimhand.Editor
{
    public static class BattleUISetup
    {
        const string PrefabPath = "Assets/_Project/Prefabs/UI/CardView.prefab";
        const string CatalogPath = "Assets/_Project/Data/CardVisualCatalog_Demo.asset";
        const string CharCatalogPath = "Assets/_Project/Data/CharacterVisualCatalog_Demo.asset";
        const string BattleSetupPath = "Assets/_Project/Data/Setups/BattleSetup_Demo.asset";
        const string ExpeditionSetupPath = "Assets/_Project/Data/Setups/ExpeditionSetup_Demo.asset";
        const string ScenePath = "Assets/_Project/Scenes/BattleSandbox.unity";

        [MenuItem("Grimhand/Setup Battle UI in Scene", priority = 10)]
        public static void SetupBattleUI()
        {
            SetupBattleUIInternal(saveScene: false);
            EditorUtility.DisplayDialog(
                "战斗 UI 已搭建",
                "已在当前场景创建/更新 BattleCanvas。\n\n" +
                "新布局：左右大立绘对峙 + 手牌下方居中 + 能量栏在手牌上方。\n\n" +
                "推荐：菜单 Grimhand → Open Battle Test Scene，直接 Play 测试。\n\n" +
                "IMGUI Demo 已自动禁用（可重新启用 Battle Demo Controller）。",
                "好的");
        }

        public static void SetupBattleUIInternal(bool saveScene)
        {
            EnsureFolders();
            EnsureEventSystem();

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
            }

            var cardPrefab = LoadOrCreateCardPrefab();
            var root = canvas.transform.Find("BattleScreen");
            if (root == null)
            {
                var rootGo = CreateRect("BattleScreen", canvas.transform);
                StretchFull(rootGo);
                root = rootGo.transform;
            }

            var view = root.GetComponent<BattleScreenView>();
            if (view == null)
                view = root.gameObject.AddComponent<BattleScreenView>();

            BuildLayout(root, view, cardPrefab);
            WireController(view);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);

            if (saveScene)
            {
                System.IO.Directory.CreateDirectory("Assets/_Project/Scenes");
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            Selection.activeGameObject = root.gameObject;
        }

        static void EnsureEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
                return;
            }

            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null)
                Object.DestroyImmediate(legacy);

            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        static void BuildLayout(Transform root, BattleScreenView view, CardView cardPrefab)
        {
            ClearChildren(root);

            var bg = CreateImage("Background", root, new Color(0.06f, 0.07f, 0.1f, 1f));
            StretchFull(bg);

            // 左右对峙舞台（无框大立绘）
            var playerStage = CreateRect("PlayerStage", root);
            AnchorRect(playerStage, 0f, 0.36f, 0.5f, 1f);
            var enemyStage = CreateRect("EnemyStage", root);
            AnchorRect(enemyStage, 0.5f, 0.36f, 1f, 1f);

            var playerSlots = CreateHeroStage(playerStage.transform, TeamSide.Player);
            var enemySlots = CreateHeroStage(enemyStage.transform, TeamSide.Enemy);

            // 敌人意图（右下，与手牌同行、在操作栏下方）
            var intentPanel = CreatePanel("EnemyIntentPanel", root, new Color(0.2f, 0.12f, 0.12f, 0.92f));
            PinBottomRight(intentPanel, 12, 8, 360, 248);
            var intentText = CreateText("Text", intentPanel.transform, "", 15, FontStyle.Normal);
            StretchFull(intentText, 10, 8, -10, -8);
            intentText.alignment = TextAnchor.UpperLeft;

            // 选目标提示
            var targetPanel = CreatePanel("TargetPromptPanel", root, new Color(0.25f, 0.2f, 0.08f, 0.95f));
            PinTopHeight(targetPanel, 16, 16, 140, 48);
            var targetRt = targetPanel.GetComponent<RectTransform>();
            targetRt.anchorMin = new Vector2(0.52f, 1f);
            targetRt.anchorMax = new Vector2(1f, 1f);
            targetRt.offsetMin = new Vector2(0f, targetRt.offsetMin.y);
            var targetText = CreateText("Text", targetPanel.transform, "请选择目标", 19, FontStyle.Bold);
            StretchFull(targetText, 12, 8, -12, -8);

            // 左下：远征/回合/能量（堆叠）；右下：操作按钮（在出牌顺序/意图下方）
            var planningInfo = CreatePanel("PlanningInfoLeft", root, new Color(0.1f, 0.11f, 0.15f, 0.9f));
            PinBottomLeft(planningInfo, 12, 352, 320, 84);

            var title = CreateText("Title", planningInfo.transform, "", 22, FontStyle.Bold);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.offsetMin = new Vector2(12f, -28f);
            titleRt.offsetMax = new Vector2(-8f, -8f);
            title.alignment = TextAnchor.UpperLeft;

            var subtitle = CreateText("Subtitle", planningInfo.transform, "", 16, FontStyle.Normal);
            var subtitleRt = subtitle.GetComponent<RectTransform>();
            subtitleRt.anchorMin = new Vector2(0f, 1f);
            subtitleRt.anchorMax = new Vector2(1f, 1f);
            subtitleRt.pivot = new Vector2(0f, 1f);
            subtitleRt.offsetMin = new Vector2(12f, -52f);
            subtitleRt.offsetMax = new Vector2(-8f, -32f);
            subtitle.alignment = TextAnchor.UpperLeft;

            var energyRow = CreateRect("EnergyRow", planningInfo.transform);
            var energyRowRt = energyRow.GetComponent<RectTransform>();
            energyRowRt.anchorMin = new Vector2(0f, 0f);
            energyRowRt.anchorMax = new Vector2(1f, 0f);
            energyRowRt.pivot = new Vector2(0f, 0f);
            energyRowRt.offsetMin = new Vector2(12f, 4f);
            energyRowRt.offsetMax = new Vector2(-8f, 28f);
            var energyLayout = energyRow.AddComponent<HorizontalLayoutGroup>();
            energyLayout.spacing = 8;
            energyLayout.childAlignment = TextAnchor.MiddleLeft;
            energyLayout.childControlWidth = false;
            energyLayout.childControlHeight = false;
            energyLayout.childForceExpandWidth = false;
            energyLayout.childForceExpandHeight = false;

            var energyIcon = CreateImage("EnergyIcon", energyRow.transform, Color.white);
            var energyIconLe = energyIcon.AddComponent<LayoutElement>();
            energyIconLe.preferredWidth = 28f;
            energyIconLe.preferredHeight = 28f;
            energyIconLe.minWidth = 28f;
            energyIconLe.minHeight = 28f;
            energyIcon.GetComponent<Image>().preserveAspect = true;
            energyIcon.GetComponent<Image>().raycastTarget = false;

            var energyValue = CreateText("EnergyValue", energyRow.transform, "8/8", 24, FontStyle.Bold);
            energyValue.alignment = TextAnchor.MiddleLeft;
            var energyValueLe = energyValue.gameObject.AddComponent<LayoutElement>();
            energyValueLe.minWidth = 72f;

            var queuePanel = CreatePanel("SelectedQueuePanel", root, new Color(0.12f, 0.16f, 0.22f, 0.88f));
            PinBottomLeft(queuePanel, 12, 8, 320, 248);
            var queueText = CreateText("Text", queuePanel.transform, "", 14, FontStyle.Normal);
            StretchFull(queueText, 10, 6, -10, -6);
            queueText.alignment = TextAnchor.UpperLeft;

            var planningActions = CreatePanel("PlanningActionsRight", root, new Color(0.1f, 0.11f, 0.15f, 0.9f));
            PinBottomRight(planningActions, 12, 352, 660, 148);

            var actionBar = CreateRect("ActionBar", planningActions.transform);
            StretchFull(actionBar, 8, 8, -8, -8);
            var actionLayout = actionBar.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 10;
            actionLayout.padding = new RectOffset(4, 4, 4, 4);
            actionLayout.childAlignment = TextAnchor.MiddleRight;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;

            GrimhandUiVisualBootstrap.EnsureUiIconCatalog();
            var uiIcons = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(GrimhandUiVisualBootstrap.IconCatalogPath);

            var confirm = CreateActionIconButton("ConfirmButton", actionBar.transform, "出牌",
                uiIcons?.ConfirmPlayIcon);
            var skip = CreateActionIconButton("SkipButton", actionBar.transform, "空过",
                uiIcons?.SkipIcon);
            var restart = CreateLayoutButton("RestartButton", actionBar.transform, "重开远征",
                new Color(0.35f, 0.22f, 0.22f, 1f));

            // 手牌区（与左右侧栏同行，居中）
            var handArea = CreateRect("HandArea", root);
            PinBottomHeight(handArea, 348, 380, 8, 248);
            var handLabel = CreateText("HandCount", handArea.transform, "手牌", 17, FontStyle.Bold);
            var handLabelRt = handLabel.GetComponent<RectTransform>();
            handLabelRt.anchorMin = new Vector2(0.5f, 1f);
            handLabelRt.anchorMax = new Vector2(0.5f, 1f);
            handLabelRt.pivot = new Vector2(0.5f, 1f);
            handLabelRt.anchoredPosition = new Vector2(0f, -4f);
            handLabelRt.sizeDelta = new Vector2(280f, 24f);
            handLabel.alignment = TextAnchor.MiddleCenter;

            var scrollGo = CreateRect("HandScroll", handArea.transform);
            StretchFull(scrollGo, 0, 0, 0, -28);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            var viewport = CreateRect("Viewport", scrollGo.transform);
            StretchFull(viewport);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.35f);
            var content = CreateRect("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0.5f, 0f);
            contentRt.anchorMax = new Vector2(0.5f, 1f);
            contentRt.pivot = new Vector2(0.5f, 0.5f);
            contentRt.sizeDelta = new Vector2(1200, 0);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14;
            hlg.padding = new RectOffset(12, 12, 8, 8);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;

            var handPanel = handArea.AddComponent<HandPanelView>();
            SetHandPanel(handPanel, scroll, contentRt, handLabel, cardPrefab);

            var tooltip = CreatePanel("KeywordTooltip", root, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            tooltip.SetActive(false);
            var tooltipRt = tooltip.GetComponent<RectTransform>();
            tooltipRt.sizeDelta = new Vector2(320, 160);
            tooltip.GetComponent<Image>().raycastTarget = false;
            var tooltipText = CreateText("Text", tooltip.transform, "", 16, FontStyle.Normal);
            StretchFull(tooltipText, 10, 8, -10, -8);
            tooltipText.alignment = TextAnchor.UpperLeft;

            var overlay = CreateImage("ExpeditionOverlay", root, new Color(0, 0, 0, 0.65f));
            StretchFull(overlay);
            overlay.SetActive(false);

            var routePanel = CreatePanel("RouteSelectPanel", overlay.transform, new Color(0.14f, 0.15f, 0.2f, 0.98f));
            CenterPanel(routePanel, 900, 420);
            var routeHeader = CreateText("Header", routePanel.transform, "选择路线", 20, FontStyle.Bold);
            AnchorTop(routeHeader, 16, -50, -16, -16);
            var routeRoot = CreateRect("RouteButtons", routePanel.transform);
            AnchorTop(routeRoot, 16, 16, -16, 120);
            var routeLayout = routeRoot.AddComponent<HorizontalLayoutGroup>();
            routeLayout.spacing = 16;
            routeLayout.padding = new RectOffset(8, 8, 8, 8);
            routeLayout.childForceExpandWidth = true;
            routeLayout.childForceExpandHeight = true;
            var routeBtnPrefab = CreateButton("RouteButtonPrefab", routeRoot.transform, "路线", new Vector2(0.5f, 0.5f));
            routeBtnPrefab.gameObject.SetActive(false);

            var runEndPanel = CreatePanel("RunEndPanel", overlay.transform, new Color(0.14f, 0.15f, 0.2f, 0.98f));
            CenterPanel(runEndPanel, 520, 220);
            runEndPanel.SetActive(false);
            var runTitle = CreateText("Title", runEndPanel.transform, "远征完成", 24, FontStyle.Bold);
            AnchorTop(runTitle, 16, -48, -16, -16);
            var runBody = CreateText("Body", runEndPanel.transform, "", 16, FontStyle.Normal);
            AnchorTop(runBody, 16, 60, -16, 100);
            var runRestart = CreateButton("Restart", runEndPanel.transform, "重新开始远征", new Vector2(0.5f, 0.15f));

            AssignView(view, title, subtitle, energyIcon.GetComponent<Image>(), energyValue, playerSlots, enemySlots, handPanel, intentText, intentPanel,
                queueText, queuePanel, targetText, targetPanel, confirm, skip, restart,
                tooltip, tooltipText, overlay, routePanel, routeRoot, routeBtnPrefab, routeHeader,
                runEndPanel, runTitle, runBody, runRestart);
        }

        static CombatantSlotView[] CreateHeroStage(Transform parent, TeamSide team)
        {
            var slots = new CombatantSlotView[3];
            var formation = new[] { FormationSlot.Front, FormationSlot.Middle, FormationSlot.Back };
            // 原画已朝向场地中央：玩家朝右、敌人朝左，创建槽位时不镜像
            const bool mirror = false;

            Vector2[] anchorMins;
            Vector2[] anchorMaxs;
            if (team == TeamSide.Player)
            {
                anchorMins = new[]
                {
                    new Vector2(0.38f, 0.02f),
                    new Vector2(0.12f, 0.18f),
                    new Vector2(0.00f, 0.38f)
                };
                anchorMaxs = new[]
                {
                    new Vector2(0.98f, 0.72f),
                    new Vector2(0.78f, 0.92f),
                    new Vector2(0.52f, 1.00f)
                };
            }
            else
            {
                anchorMins = new[]
                {
                    new Vector2(0.02f, 0.02f),
                    new Vector2(0.22f, 0.18f),
                    new Vector2(0.48f, 0.38f)
                };
                anchorMaxs = new[]
                {
                    new Vector2(0.62f, 0.72f),
                    new Vector2(0.88f, 0.92f),
                    new Vector2(1.00f, 1.00f)
                };
            }

            for (var i = 0; i < 3; i++)
            {
                slots[i] = CreateHeroSlot(parent, formation[i], team, anchorMins[i], anchorMaxs[i], mirror);
            }

            ApplyStageDrawOrder(parent);

            return slots;
        }

        static void ApplyStageDrawOrder(Transform stage)
        {
            SetSlotSiblingIndex(stage, FormationSlot.Back, 0);
            SetSlotSiblingIndex(stage, FormationSlot.Middle, 1);
            SetSlotSiblingIndex(stage, FormationSlot.Front, 2);
        }

        static void SetSlotSiblingIndex(Transform stage, FormationSlot slot, int index)
        {
            var child = stage.Find($"Slot_{slot}");
            if (child != null)
                child.SetSiblingIndex(index);
        }

        static CombatantSlotView CreateHeroSlot(
            Transform parent,
            FormationSlot slot,
            TeamSide team,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool mirror)
        {
            var slotGo = CreateRect($"Slot_{slot}", parent);
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var bg = slotGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0f);
            bg.raycastTarget = false;

            var portraitRoot = CreateRect("PortraitRoot", slotGo.transform);
            StretchFull(portraitRoot, 0, 72, 0, 44);
            var portrait = CreateImage("Portrait", portraitRoot.transform, Color.white);
            StretchFull(portrait);
            var portraitImg = portrait.GetComponent<Image>();
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;
            portraitImg.enabled = false;

            var portraitHit = CreateImage("PortraitHit", portraitRoot.transform, new Color(1f, 1f, 1f, 0f));
            StretchFull(portraitHit);
            var portraitBtn = portraitHit.AddComponent<Button>();
            portraitBtn.transition = Selectable.Transition.None;
            portraitBtn.targetGraphic = portraitHit.GetComponent<Image>();

            var highlight = CreateImage("TargetHighlight", portraitRoot.transform, new Color(1f, 0.92f, 0.45f, 0.35f));
            StretchFull(highlight);
            highlight.GetComponent<Image>().raycastTarget = false;
            highlight.GetComponent<Image>().preserveAspect = true;
            highlight.SetActive(false);

            var statusGo = CreateText("Status", slotGo.transform, "", 14, FontStyle.Bold);
            var statusRt = statusGo.GetComponent<RectTransform>();
            statusRt.anchorMin = new Vector2(0.05f, 0.74f);
            statusRt.anchorMax = new Vector2(0.95f, 0.98f);
            statusRt.offsetMin = Vector2.zero;
            statusRt.offsetMax = Vector2.zero;
            statusGo.alignment = TextAnchor.UpperCenter;
            statusGo.color = new Color(0.95f, 0.97f, 1f, 1f);

            var slotLabel = CreateText("Label", slotGo.transform, "", 1, FontStyle.Bold);
            slotLabel.gameObject.SetActive(false);

            var nameText = CreateText("Name", slotGo.transform, "", 18, FontStyle.Bold);
            AnchorBottom(nameText, 8, 42, -8, 66);
            nameText.alignment = TextAnchor.MiddleCenter;

            var statsGo = CreateRect("Stats", slotGo.transform);
            AnchorBottom(statsGo, 4, 4, -4, 40);

            var statsRow = statsGo.AddComponent<UnitStatsRowView>();

            var view = slotGo.AddComponent<CombatantSlotView>();
            SetSlotView(view, bg, highlight.GetComponent<Image>(), portraitRoot.GetComponent<RectTransform>(),
                portraitImg, slotLabel, statusGo, nameText, statsRow, portraitBtn, slot, team, mirror);
            view.Configure(slot, team, team == TeamSide.Player ? "我方" : "敌方", mirror);
            return view;
        }

        static CardView LoadOrCreateCardPrefab()
        {
            var cardGo = CreateCardViewObject("CardView");
            var prefab = PrefabUtility.SaveAsPrefabAsset(cardGo, PrefabPath);
            Object.DestroyImmediate(cardGo);
            return prefab.GetComponent<CardView>();
        }

        static GameObject CreateCardViewObject(string name)
        {
            var go = CreateRect(name, null);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(168, 236);

            var scaleRoot = CreateRect("CardScaleRoot", go.transform);
            var scaleRt = scaleRoot.GetComponent<RectTransform>();
            scaleRt.anchorMin = Vector2.zero;
            scaleRt.anchorMax = Vector2.one;
            scaleRt.pivot = new Vector2(0.5f, 1f);
            scaleRt.offsetMin = Vector2.zero;
            scaleRt.offsetMax = Vector2.zero;

            var frame = CreateImage("Frame", scaleRoot.transform, new Color(0.18f, 0.2f, 0.28f, 1f));
            StretchFull(frame);

            var art = CreateImage("Art", scaleRoot.transform, new Color(0.25f, 0.27f, 0.35f, 1f));
            var artImg = art.GetComponent<Image>();
            var artRt = art.GetComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0.1f, 0.32f);
            artRt.anchorMax = new Vector2(0.9f, 0.86f);
            artRt.offsetMin = Vector2.zero;
            artRt.offsetMax = Vector2.zero;
            artImg.type = Image.Type.Simple;
            artImg.preserveAspect = true;

            var icon = CreateImage("Icon", scaleRoot.transform, Color.white);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.72f, 0.72f);
            iconRt.anchorMax = new Vector2(0.95f, 0.95f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            icon.gameObject.SetActive(false);

            var costBg = CreateImage("CostBadge", scaleRoot.transform, new Color(1f, 1f, 1f, 0f));
            var costRt = costBg.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0.02f, 0.86f);
            costRt.anchorMax = new Vector2(0.24f, 0.99f);
            costRt.offsetMin = Vector2.zero;
            costRt.offsetMax = Vector2.zero;
            var costIconImg = costBg.GetComponent<Image>();
            costIconImg.preserveAspect = true;
            costIconImg.raycastTarget = false;
            var costText = CreateText("Cost", costBg.transform, "1", 16, FontStyle.Bold);
            StretchFull(costText);
            costText.alignment = TextAnchor.MiddleCenter;

            var nameText = CreateText("Name", scaleRoot.transform, "卡牌", 15, FontStyle.Bold);
            AnchorTop(nameText, 8, -56, -8, -6);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;

            var statsText = CreateText("Stats", scaleRoot.transform, "", 11, FontStyle.Normal);
            AnchorBottom(statsText, 10, 10, -10, 78);
            statsText.alignment = TextAnchor.UpperCenter;
            statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statsText.verticalOverflow = VerticalWrapMode.Truncate;

            var ownerText = CreateText("Owner", scaleRoot.transform, "", 11, FontStyle.Italic);
            ownerText.gameObject.SetActive(false);

            var orderBadge = CreateText("OrderBadge", scaleRoot.transform, "#1", 13, FontStyle.Bold);
            var obRt = orderBadge.GetComponent<RectTransform>();
            obRt.anchorMin = new Vector2(0.72f, 0.88f);
            obRt.anchorMax = new Vector2(0.98f, 0.98f);
            obRt.offsetMin = Vector2.zero;
            obRt.offsetMax = Vector2.zero;

            var polluted = CreateImage("PollutedOverlay", scaleRoot.transform, new Color(0, 0, 0, 0.45f));
            StretchFull(polluted);
            polluted.GetComponent<Image>().raycastTarget = false;
            polluted.gameObject.SetActive(false);

            var selectedHighlight = CreateImage("SelectedHighlight", scaleRoot.transform, new Color(1f, 1f, 1f, 0.42f));
            StretchFull(selectedHighlight);
            selectedHighlight.GetComponent<Image>().raycastTarget = false;
            selectedHighlight.SetActive(false);

            var selected = CreateImage("SelectedOutline", scaleRoot.transform, new Color(1f, 0.85f, 0.2f, 0.85f));
            StretchFull(selected);
            selected.GetComponent<Image>().type = Image.Type.Sliced;
            selected.GetComponent<Image>().raycastTarget = false;
            selected.SetActive(false);

            var cg = go.AddComponent<CanvasGroup>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 168;
            le.preferredHeight = 236;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = frame.GetComponent<Image>();

            var view = go.AddComponent<CardView>();
            SetCardView(view, frame.GetComponent<Image>(), art.GetComponent<Image>(), icon.GetComponent<Image>(),
                polluted.GetComponent<Image>(), selectedHighlight.GetComponent<Image>(), selected.GetComponent<Image>(),
                costIconImg, costText, nameText, statsText, ownerText, orderBadge, cg, btn);
            return go;
        }

        static void WireController(BattleScreenView view)
        {
            var demoGo = GameObject.Find("BattleDemo");
            if (demoGo == null)
            {
                demoGo = new GameObject("BattleDemo");
                Undo.RegisterCreatedObjectUndo(demoGo, "Create BattleDemo");
            }

            var legacy = demoGo.GetComponent<BattleDemoController>();
            if (legacy != null)
                legacy.enabled = false;

            var controller = demoGo.GetComponent<BattleScreenController>();
            if (controller == null)
                controller = demoGo.AddComponent<BattleScreenController>();

            GrimhandUiVisualBootstrap.EnsureUiIconCatalog();
            GrimhandUiVisualBootstrap.EnsureCardVisualCatalog();
            GrimhandUiVisualBootstrap.AssignDemoCardRarities();

            var catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogSO>(CatalogPath);
            var charCatalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(CharCatalogPath);
            var iconCatalog = AssetDatabase.LoadAssetAtPath<BattleUiIconCatalogSO>(GrimhandUiVisualBootstrap.IconCatalogPath);

            var so = new SerializedObject(controller);
            so.FindProperty("battleSetup").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BattleSetupSO>(BattleSetupPath);
            so.FindProperty("expeditionSetup").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(ExpeditionSetupPath);
            so.FindProperty("cardVisualCatalog").objectReferenceValue = catalog;
            so.FindProperty("characterVisualCatalog").objectReferenceValue = charCatalog;
            so.FindProperty("uiIconCatalog").objectReferenceValue = iconCatalog;
            so.FindProperty("screenView").objectReferenceValue = view;
            so.FindProperty("disableLegacyImGui").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Project", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefabs/UI"))
                AssetDatabase.CreateFolder("Assets/_Project/Prefabs", "UI");
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject CreateImage(string name, Transform parent, Color color)
        {
            var go = CreateRect(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        static GameObject CreatePanel(string name, Transform parent, Color color)
        {
            var go = CreateImage(name, parent, color);
            return go;
        }

        static Text CreateText(string name, Transform parent, string text, int size, FontStyle style)
        {
            var go = CreateRect(name, parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Button CreateButton(string name, Transform parent, string label, Vector2 anchor)
        {
            var go = CreatePanel(name, parent, new Color(0.22f, 0.35f, 0.55f, 1f));
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(180, 48);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var text = CreateText("Label", go.transform, label, 16, FontStyle.Bold);
            StretchFull(text);
            return btn;
        }

        static Button CreateLayoutButton(string name, Transform parent, string label, Color bg)
        {
            var go = CreatePanel(name, parent, bg);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200, 52);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 200;
            le.preferredHeight = 52;
            le.minWidth = 140;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            var text = CreateText("Label", go.transform, label, 16, FontStyle.Bold);
            StretchFull(text);
            return btn;
        }

        static Button CreateActionIconButton(string name, Transform parent, string label, Sprite icon)
        {
            var go = CreateRect(name, parent);
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 120f;
            le.preferredHeight = 144f;
            le.minWidth = 120f;
            le.flexibleWidth = 0f;

            CreateText("Label", go.transform, label, 16, FontStyle.Bold);
            var iconGo = CreateImage("Icon", go.transform, Color.white);
            iconGo.GetComponent<Image>().preserveAspect = true;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = iconGo.GetComponent<Image>();
            PlanningActionButtonStyle.Apply(btn, icon, label);
            return btn;
        }

        static void StretchFull(Component component, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            if (component != null)
                StretchFull(component.gameObject, left, bottom, right, top);
        }

        static void StretchFull(GameObject go, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        static void PinTopHeight(GameObject go, float left, float right, float fromTop, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -fromTop - height);
            rt.offsetMax = new Vector2(-right, -fromTop);
        }

        static void PinBottomLeft(GameObject go, float left, float fromBottom, float width, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(left, fromBottom);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void PinBottomRight(GameObject go, float right, float fromBottom, float width, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-right, fromBottom);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void PinBottomHeight(GameObject go, float left, float right, float fromBottom, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, fromBottom);
            rt.offsetMax = new Vector2(-right, fromBottom + height);
        }

        static void FillVerticalBand(GameObject go, float left, float right, float yMin01, float yMax01)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, yMin01);
            rt.anchorMax = new Vector2(1f, yMax01);
            rt.offsetMin = new Vector2(left, 4f);
            rt.offsetMax = new Vector2(-right, -4f);
        }

        static void AnchorTop(Component component, float left, float bottom, float right, float top)
        {
            if (component != null)
                AnchorTop(component.gameObject, left, bottom, right, top);
        }

        static void AnchorTop(GameObject go, float left, float bottom, float right, float top)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        static void AnchorBottom(Component component, float left, float bottom, float right, float top)
        {
            if (component != null)
                AnchorBottom(component.gameObject, left, bottom, right, top);
        }

        static void AnchorBottom(GameObject go, float left, float bottom, float right, float top)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        static void CenterPanel(GameObject go, float width, float height)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }

        static void SetHandPanel(HandPanelView panel, ScrollRect scroll, RectTransform content, Text label, CardView prefab)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("contentRoot").objectReferenceValue = content;
            so.FindProperty("scrollRect").objectReferenceValue = scroll;
            so.FindProperty("cardPrefab").objectReferenceValue = prefab;
            so.FindProperty("handCountLabel").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AnchorRect(GameObject go, float xMin, float yMin, float xMax, float yMax)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void SetSlotView(CombatantSlotView view, Image bg, Image targetHighlight, RectTransform portraitRoot,
            Image portrait, Text slotLabel, Text body, Text nameText,
            UnitStatsRowView statsRow, Button btn, FormationSlot slot, TeamSide team, bool mirror)
        {
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("targetHighlight").objectReferenceValue = targetHighlight;
            so.FindProperty("portraitRoot").objectReferenceValue = portraitRoot;
            so.FindProperty("portraitImage").objectReferenceValue = portrait;
            so.FindProperty("slotLabel").objectReferenceValue = slotLabel;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("statsRow").objectReferenceValue = statsRow;
            so.FindProperty("selectButton").objectReferenceValue = btn;
            so.FindProperty("formationSlot").intValue = (int)slot;
            so.FindProperty("team").intValue = (int)team;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetCardView(CardView view, Image frame, Image art, Image icon, Image polluted, Image selectedHighlight,
            Image selectedOutline, Image costIcon, Text cost, Text name, Text stats, Text owner, Text order,
            CanvasGroup cg, Button btn)
        {
            var so = new SerializedObject(view);
            so.FindProperty("frameImage").objectReferenceValue = frame;
            so.FindProperty("artImage").objectReferenceValue = art;
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("pollutedOverlay").objectReferenceValue = polluted;
            so.FindProperty("selectedHighlight").objectReferenceValue = selectedHighlight;
            so.FindProperty("selectedOutline").objectReferenceValue = selectedOutline;
            so.FindProperty("costIconImage").objectReferenceValue = costIcon;
            so.FindProperty("costText").objectReferenceValue = cost;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("ownerText").objectReferenceValue = owner;
            so.FindProperty("orderBadgeText").objectReferenceValue = order;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignView(BattleScreenView view, Text title, Text subtitle, Image hudEnergyIcon, Text energyValueText,
            CombatantSlotView[] playerSlots, CombatantSlotView[] enemySlots,
            HandPanelView handPanel, Text intentText, GameObject intentPanel,
            Text queueText, GameObject queuePanel, Text targetText, GameObject targetPanel,
            Button confirm, Button skip, Button restart,
            GameObject tooltip, Text tooltipText, GameObject overlay,
            GameObject routePanel, GameObject routeRoot, Button routeBtnPrefab, Text routeHeader,
            GameObject runEndPanel, Text runTitle, Text runBody, Button runRestart)
        {
            var so = new SerializedObject(view);
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("subtitleText").objectReferenceValue = subtitle;
            so.FindProperty("hudEnergyIcon").objectReferenceValue = hudEnergyIcon;
            so.FindProperty("energyValueText").objectReferenceValue = energyValueText;
            so.FindProperty("playerSlots").arraySize = 3;
            for (var i = 0; i < 3; i++)
                so.FindProperty("playerSlots").GetArrayElementAtIndex(i).objectReferenceValue = playerSlots[i];
            so.FindProperty("enemySlots").arraySize = 3;
            for (var i = 0; i < 3; i++)
                so.FindProperty("enemySlots").GetArrayElementAtIndex(i).objectReferenceValue = enemySlots[i];
            so.FindProperty("handPanel").objectReferenceValue = handPanel;
            so.FindProperty("enemyIntentText").objectReferenceValue = intentText;
            so.FindProperty("enemyIntentPanel").objectReferenceValue = intentPanel;
            so.FindProperty("selectedQueueText").objectReferenceValue = queueText;
            so.FindProperty("selectedQueuePanel").objectReferenceValue = queuePanel;
            so.FindProperty("targetPromptText").objectReferenceValue = targetText;
            so.FindProperty("targetPromptPanel").objectReferenceValue = targetPanel;
            so.FindProperty("confirmButton").objectReferenceValue = confirm;
            so.FindProperty("skipButton").objectReferenceValue = skip;
            so.FindProperty("restartButton").objectReferenceValue = restart;
            so.FindProperty("restartButtonLabel").objectReferenceValue = restart.GetComponentInChildren<Text>();
            so.FindProperty("keywordTooltipPanel").objectReferenceValue = tooltip;
            so.FindProperty("keywordTooltipText").objectReferenceValue = tooltipText;
            so.FindProperty("expeditionOverlay").objectReferenceValue = overlay;
            so.FindProperty("routeSelectPanel").objectReferenceValue = routePanel;
            so.FindProperty("routeButtonRoot").objectReferenceValue = routeRoot.transform;
            so.FindProperty("routeButtonPrefab").objectReferenceValue = routeBtnPrefab;
            so.FindProperty("routeHeaderText").objectReferenceValue = routeHeader;
            so.FindProperty("runEndPanel").objectReferenceValue = runEndPanel;
            so.FindProperty("runEndTitleText").objectReferenceValue = runTitle;
            so.FindProperty("runEndBodyText").objectReferenceValue = runBody;
            so.FindProperty("runRestartButton").objectReferenceValue = runRestart;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
