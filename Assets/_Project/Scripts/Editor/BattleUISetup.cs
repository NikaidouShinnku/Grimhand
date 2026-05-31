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

            var bg = CreateImage("Background", root, new Color(0.08f, 0.09f, 0.12f, 1f));
            StretchFull(bg);

            var hud = CreateRect("HUD", root);
            PinTopHeight(hud, 0, 0, 0, 72);
            var title = CreateText("Title", hud.transform, "", 22, FontStyle.Bold);
            StretchFull(title, 12, 36, -12, -8);
            var subtitle = CreateText("Subtitle", hud.transform, "", 16, FontStyle.Normal);
            StretchFull(subtitle, 12, 8, -12, -36);

            var battlefield = CreatePanel("Battlefield", root, new Color(0.12f, 0.13f, 0.18f, 0.95f));
            PinTopHeight(battlefield, 16, 16, 80, 420);

            var enemyRow = CreateRect("EnemyRow", battlefield.transform);
            FillVerticalBand(enemyRow, 8, 8, 0.54f, 1f);
            var playerRow = CreateRect("PlayerRow", battlefield.transform);
            FillVerticalBand(playerRow, 8, 8, 0f, 0.46f);

            var playerSlots = CreateSlotRow(playerRow.transform, TeamSide.Player);
            var enemySlots = CreateSlotRow(enemyRow.transform, TeamSide.Enemy);

            var intentPanel = CreatePanel("EnemyIntentPanel", root, new Color(0.2f, 0.12f, 0.12f, 0.9f));
            PinTopHeight(intentPanel, 16, 16, 508, 64);
            var intentText = CreateText("Text", intentPanel.transform, "", 15, FontStyle.Normal);
            StretchFull(intentText, 10, 8, -10, -8);
            intentText.alignment = TextAnchor.UpperLeft;

            var queuePanel = CreatePanel("SelectedQueuePanel", root, new Color(0.12f, 0.16f, 0.2f, 0.9f));
            PinTopHeight(queuePanel, 16, 16, 580, 64);
            var queueText = CreateText("Text", queuePanel.transform, "", 14, FontStyle.Normal);
            StretchFull(queueText, 10, 8, -10, -8);
            queueText.alignment = TextAnchor.UpperLeft;

            var targetPanel = CreatePanel("TargetPromptPanel", root, new Color(0.25f, 0.2f, 0.08f, 0.95f));
            PinTopHeight(targetPanel, 16, 16, 652, 48);
            var targetText = CreateText("Text", targetPanel.transform, "请选择目标", 18, FontStyle.Bold);
            StretchFull(targetText, 12, 8, -12, -8);

            var handArea = CreateRect("HandArea", root);
            PinBottomHeight(handArea, 16, 16, 96, 196);
            var handLabel = CreateText("HandCount", handArea.transform, "手牌", 16, FontStyle.Bold);
            var handLabelRt = handLabel.GetComponent<RectTransform>();
            handLabelRt.anchorMin = new Vector2(0f, 1f);
            handLabelRt.anchorMax = new Vector2(0f, 1f);
            handLabelRt.pivot = new Vector2(0f, 1f);
            handLabelRt.anchoredPosition = new Vector2(8f, -6f);
            handLabelRt.sizeDelta = new Vector2(260f, 24f);
            if (handLabel != null)
            {
                handLabel.fontStyle = FontStyle.Bold;
                handLabel.alignment = TextAnchor.MiddleLeft;
            }

            var scrollGo = CreateRect("HandScroll", handArea.transform);
            StretchFull(scrollGo, 0, 0, 0, -32);
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            var viewport = CreateRect("Viewport", scrollGo.transform);
            StretchFull(viewport);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.6f);
            var content = CreateRect("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 0);
            contentRt.anchorMax = new Vector2(0, 1);
            contentRt.pivot = new Vector2(0, 0.5f);
            contentRt.sizeDelta = new Vector2(1200, 0);
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            var hlg = content.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12;
            hlg.padding = new RectOffset(8, 8, 8, 8);
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;

            var handPanel = handArea.AddComponent<HandPanelView>();
            SetHandPanel(handPanel, scroll, contentRt, handLabel, cardPrefab);

            var actionBar = CreateRect("ActionBar", root);
            PinBottomHeight(actionBar, 16, 16, 8, 80);
            var actionLayout = actionBar.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 12;
            actionLayout.padding = new RectOffset(12, 12, 8, 8);
            actionLayout.childAlignment = TextAnchor.MiddleLeft;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = true;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = true;

            var confirm = CreateLayoutButton("ConfirmButton", actionBar.transform, "确认出牌",
                new Color(0.15f, 0.55f, 0.28f, 1f));
            var skip = CreateLayoutButton("SkipButton", actionBar.transform, "空过",
                new Color(0.22f, 0.35f, 0.55f, 1f));
            var restart = CreateLayoutButton("RestartButton", actionBar.transform, "重开远征",
                new Color(0.22f, 0.35f, 0.55f, 1f));

            var tooltip = CreatePanel("KeywordTooltip", root, new Color(0.05f, 0.05f, 0.08f, 0.95f));
            tooltip.SetActive(false);
            var tooltipRt = tooltip.GetComponent<RectTransform>();
            tooltipRt.sizeDelta = new Vector2(320, 160);
            tooltip.GetComponent<Image>().raycastTarget = false;
            var tooltipText = CreateText("Text", tooltip.transform, "", 14, FontStyle.Normal);
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

            AssignView(view, title, subtitle, playerSlots, enemySlots, handPanel, intentText, intentPanel,
                queueText, queuePanel, targetText, targetPanel, confirm, skip, restart,
                tooltip, tooltipText, overlay, routePanel, routeRoot, routeBtnPrefab, routeHeader,
                runEndPanel, runTitle, runBody, runRestart);
        }

        static CombatantSlotView[] CreateSlotRow(Transform parent, TeamSide team)
        {
            var slots = new CombatantSlotView[3];
            var formation = new[] { FormationSlot.Front, FormationSlot.Middle, FormationSlot.Back };
            var labels = team == TeamSide.Player ? "我方" : "敌方";

            for (var i = 0; i < 3; i++)
            {
                var slotGo = CreatePanel($"Slot_{formation[i]}", parent, new Color(0.2f, 0.22f, 0.3f, 1f));
                var rt = slotGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(i / 3f, 0);
                rt.anchorMax = new Vector2((i + 1) / 3f, 1);
                rt.offsetMin = new Vector2(4, 0);
                rt.offsetMax = new Vector2(-4, 0);

                var slotLabel = CreateText("Label", slotGo.transform, BattleUiFormatters.SlotLabel(formation[i]), 13, FontStyle.Bold);
                AnchorTop(slotLabel, 4, -22, -4, -4);

                var portrait = CreateImage("Portrait", slotGo.transform, Color.white);
                var portraitRt = portrait.GetComponent<RectTransform>();
                portraitRt.anchorMin = new Vector2(0.04f, 0.18f);
                portraitRt.anchorMax = new Vector2(0.96f, 0.98f);
                portraitRt.offsetMin = Vector2.zero;
                portraitRt.offsetMax = Vector2.zero;
                var portraitImg = portrait.GetComponent<Image>();
                portraitImg.preserveAspect = true;
                portraitImg.raycastTarget = false;
                portraitImg.enabled = false;

                var body = CreateText("Body", slotGo.transform, "—", 11, FontStyle.Normal);
                AnchorBottom(body, 6, 4, -6, 52);
                body.alignment = TextAnchor.LowerLeft;

                var btn = slotGo.AddComponent<Button>();
                btn.targetGraphic = slotGo.GetComponent<Image>();

                var view = slotGo.AddComponent<CombatantSlotView>();
                SetSlotView(view, slotGo.GetComponent<Image>(), portraitImg, slotLabel, body, btn, formation[i], team);
                view.Configure(formation[i], team, labels);
                slots[i] = view;
            }

            return slots;
        }

        static CardView LoadOrCreateCardPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
                return existing.GetComponent<CardView>();

            var cardGo = CreateCardViewObject("CardView");
            var prefab = PrefabUtility.SaveAsPrefabAsset(cardGo, PrefabPath);
            Object.DestroyImmediate(cardGo);
            return prefab.GetComponent<CardView>();
        }

        static GameObject CreateCardViewObject(string name)
        {
            var go = CreateRect(name, null);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 210);

            var frame = CreateImage("Frame", go.transform, new Color(0.18f, 0.2f, 0.28f, 1f));
            StretchFull(frame);

            var art = CreateImage("Art", go.transform, new Color(0.25f, 0.27f, 0.35f, 1f));
            var artImg = art.GetComponent<Image>();
            var artRt = art.GetComponent<RectTransform>();
            artRt.anchorMin = new Vector2(0.08f, 0.28f);
            artRt.anchorMax = new Vector2(0.92f, 0.88f);
            artRt.offsetMin = Vector2.zero;
            artRt.offsetMax = Vector2.zero;
            artImg.type = Image.Type.Simple;
            artImg.preserveAspect = true;

            var icon = CreateImage("Icon", go.transform, Color.white);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.72f, 0.72f);
            iconRt.anchorMax = new Vector2(0.95f, 0.95f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            icon.gameObject.SetActive(false);

            var costBg = CreateImage("CostBadge", go.transform, new Color(0.1f, 0.45f, 0.75f, 1f));
            var costRt = costBg.GetComponent<RectTransform>();
            costRt.anchorMin = new Vector2(0.02f, 0.88f);
            costRt.anchorMax = new Vector2(0.22f, 0.98f);
            costRt.offsetMin = Vector2.zero;
            costRt.offsetMax = Vector2.zero;
            var costText = CreateText("Cost", costBg.transform, "1", 18, FontStyle.Bold);
            StretchFull(costText);

            var nameText = CreateText("Name", go.transform, "卡牌", 15, FontStyle.Bold);
            AnchorTop(nameText, 8, -52, -8, -8);
            nameText.alignment = TextAnchor.UpperCenter;

            var statsText = CreateText("Stats", go.transform, "", 12, FontStyle.Normal);
            AnchorTop(statsText, 8, 52, -8, 88);
            statsText.alignment = TextAnchor.UpperCenter;

            var ownerText = CreateText("Owner", go.transform, "", 11, FontStyle.Italic);
            AnchorBottom(ownerText, 8, 8, -8, 28);

            var orderBadge = CreateText("OrderBadge", go.transform, "#1", 13, FontStyle.Bold);
            var obRt = orderBadge.GetComponent<RectTransform>();
            obRt.anchorMin = new Vector2(0.72f, 0.88f);
            obRt.anchorMax = new Vector2(0.98f, 0.98f);
            obRt.offsetMin = Vector2.zero;
            obRt.offsetMax = Vector2.zero;

            var polluted = CreateImage("PollutedOverlay", go.transform, new Color(0, 0, 0, 0.45f));
            StretchFull(polluted);
            polluted.gameObject.SetActive(false);

            var selected = CreateImage("SelectedOutline", go.transform, new Color(1f, 0.85f, 0.2f, 0.85f));
            StretchFull(selected);
            selected.GetComponent<Image>().type = Image.Type.Sliced;
            selected.gameObject.SetActive(false);

            var cg = go.AddComponent<CanvasGroup>();
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 150;
            le.preferredHeight = 210;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = frame.GetComponent<Image>();

            var view = go.AddComponent<CardView>();
            SetCardView(view, frame.GetComponent<Image>(), art.GetComponent<Image>(), icon.GetComponent<Image>(),
                polluted.GetComponent<Image>(), selected.GetComponent<Image>(),
                costText, nameText, statsText, ownerText, orderBadge, cg, btn);
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

            var catalog = AssetDatabase.LoadAssetAtPath<CardVisualCatalogSO>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CardVisualCatalogSO>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            var charCatalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(CharCatalogPath);
            if (charCatalog == null)
                GrimhandBattleSceneBootstrap.EnsureCharacterVisualCatalog();
            charCatalog = AssetDatabase.LoadAssetAtPath<CharacterVisualCatalogSO>(CharCatalogPath);

            var so = new SerializedObject(controller);
            so.FindProperty("battleSetup").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<BattleSetupSO>(BattleSetupPath);
            so.FindProperty("expeditionSetup").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<ExpeditionSetupSO>(ExpeditionSetupPath);
            so.FindProperty("cardVisualCatalog").objectReferenceValue = catalog;
            so.FindProperty("characterVisualCatalog").objectReferenceValue = charCatalog;
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

        static void SetSlotView(CombatantSlotView view, Image bg, Image portrait, Text slotLabel, Text body, Button btn,
            FormationSlot slot, TeamSide team)
        {
            var so = new SerializedObject(view);
            so.FindProperty("background").objectReferenceValue = bg;
            so.FindProperty("portraitImage").objectReferenceValue = portrait;
            so.FindProperty("slotLabel").objectReferenceValue = slotLabel;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("selectButton").objectReferenceValue = btn;
            so.FindProperty("formationSlot").enumValueIndex = (int)slot;
            so.FindProperty("team").enumValueIndex = (int)team;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetCardView(CardView view, Image frame, Image art, Image icon, Image polluted, Image selected,
            Text cost, Text name, Text stats, Text owner, Text order, CanvasGroup cg, Button btn)
        {
            var so = new SerializedObject(view);
            so.FindProperty("frameImage").objectReferenceValue = frame;
            so.FindProperty("artImage").objectReferenceValue = art;
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("pollutedOverlay").objectReferenceValue = polluted;
            so.FindProperty("selectedOutline").objectReferenceValue = selected;
            so.FindProperty("costText").objectReferenceValue = cost;
            so.FindProperty("nameText").objectReferenceValue = name;
            so.FindProperty("statsText").objectReferenceValue = stats;
            so.FindProperty("ownerText").objectReferenceValue = owner;
            so.FindProperty("orderBadgeText").objectReferenceValue = order;
            so.FindProperty("canvasGroup").objectReferenceValue = cg;
            so.FindProperty("button").objectReferenceValue = btn;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AssignView(BattleScreenView view, Text title, Text subtitle,
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
