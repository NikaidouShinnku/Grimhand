using System.Collections;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>竖向远征地图，底层为第 1 层，顶层为 Boss。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionMapPanelView : MonoBehaviour
    {
        const float PanelWidth = 400f;
        const float PanelHeight = 860f;
        const float RowHeight = 132f;
        const float PathIconWidth = 112f;
        const int MapCanvasSortOrder = 90;

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        RectTransform _overlayRoot;
        RectTransform _root;
        RectTransform _scrollContent;
        RectTransform _viewport;
        ScrollRect _scrollRect;
        Text _titleText;
        bool _built;
        bool _open;

        public bool IsOpen => _open;

        public void Initialize(BattleSession session, Transform parent, BattleUiIconCatalogSO icons)
        {
            _session = session;
            _icons = icons;
            EnsureBuilt(parent);
        }

        public void Toggle()
        {
            _open = !_open;
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(_open);

            if (!_open)
                return;

            BringToFront();
            Refresh();
            StartCoroutine(DeferredScrollAfterLayout());
        }

        public void Hide()
        {
            if (!_open)
                return;
            _open = false;
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode || _scrollContent == null)
                return;

            if (_open)
                BringToFront();

            foreach (Transform child in _scrollContent)
                Destroy(child.gameObject);

            var run = _session.Expedition.Run;
            var map = run.Map;
            if (map == null)
            {
                _titleText.text = "远征地图";
                return;
            }

            _titleText.text = $"远征地图 · 第 {map.NodesCompleted}/{map.ChapterLayerCount} 层";

            for (var i = map.Layers.Count - 1; i >= 0; i--)
                BuildRow(map.Layers[i], map, run);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);

            if (_open)
                StartCoroutine(DeferredScrollAfterLayout());
        }

        void BringToFront()
        {
            if (_overlayRoot == null)
                return;

            _overlayRoot.SetAsLastSibling();
            var canvas = _overlayRoot.GetComponent<Canvas>();
            if (canvas != null)
                canvas.sortingOrder = MapCanvasSortOrder;
        }

        IEnumerator DeferredScrollAfterLayout()
        {
            yield return null;
            yield return null;

            if (!_open || _session?.Expedition?.Run?.Map == null)
                yield break;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
            ScrollToCurrentLayer(_session.Expedition.Run.Map);
        }

        void ScrollToCurrentLayer(ExpeditionMapState map)
        {
            if (_scrollRect == null || _viewport == null || _scrollContent == null)
                return;

            var contentHeight = LayoutUtility.GetPreferredHeight(_scrollContent);
            if (contentHeight <= 1f)
                contentHeight = _scrollContent.rect.height;

            var viewportHeight = _viewport.rect.height;
            var scrollRange = Mathf.Max(0f, contentHeight - viewportHeight);
            if (scrollRange <= 1f)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            var currentLayer = Mathf.Clamp(map.NodesCompleted + 1, 1, map.Layers.Count);
            var fromTop = (map.Layers.Count - currentLayer) * RowHeight;
            var targetOffset = Mathf.Clamp(fromTop - viewportHeight * 0.35f, 0f, scrollRange);
            _scrollRect.verticalNormalizedPosition = 1f - targetOffset / scrollRange;
        }

        void BuildRow(ExpeditionMapLayer layer, ExpeditionMapState map, ExpeditionRunState run)
        {
            var rowGo = new GameObject($"Layer_{layer.LayerNumber}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            rowGo.transform.SetParent(_scrollContent, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.sizeDelta = new Vector2(0f, RowHeight);
            var rowLe = rowGo.GetComponent<LayoutElement>();
            rowLe.preferredHeight = RowHeight;
            rowLe.minHeight = RowHeight;

            var bg = rowGo.GetComponent<Image>();
            var isCurrent = layer.LayerNumber == map.NodesCompleted + 1;
            var isPast = layer.LayerNumber <= map.NodesCompleted;
            bg.color = isCurrent
                ? new Color(0.18f, 0.22f, 0.34f, 0.95f)
                : isPast
                    ? new Color(0.12f, 0.14f, 0.18f, 0.88f)
                    : new Color(0.08f, 0.09f, 0.12f, 0.82f);
            bg.raycastTarget = false;

            var pathGo = new GameObject("Path", typeof(RectTransform), typeof(Image));
            pathGo.transform.SetParent(rowGo.transform, false);
            var pathRt = pathGo.GetComponent<RectTransform>();
            pathRt.anchorMin = new Vector2(0f, 0.5f);
            pathRt.anchorMax = new Vector2(0f, 0.5f);
            pathRt.pivot = new Vector2(0f, 0.5f);
            pathRt.anchoredPosition = new Vector2(8f, 0f);
            pathRt.sizeDelta = new Vector2(PathIconWidth, RowHeight - 12f);
            ApplyPathImage(pathGo.GetComponent<Image>(), ResolvePathSprite(layer, map));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(rowGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.offsetMin = new Vector2(PathIconWidth + 16f, 8f);
            labelRt.offsetMax = new Vector2(-8f, -8f);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 17;
            label.lineSpacing = 1.05f;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.raycastTarget = false;
            label.text = BuildRowLabel(layer, map, run);
        }

        static void ApplyPathImage(Image pathImg, Sprite sprite)
        {
            pathImg.sprite = sprite;
            pathImg.type = Image.Type.Simple;
            pathImg.preserveAspect = true;
            pathImg.color = sprite != null ? Color.white : new Color(0.55f, 0.45f, 0.32f, 1f);
            pathImg.raycastTarget = false;
        }

        Sprite ResolvePathSprite(ExpeditionMapLayer layer, ExpeditionMapState map)
        {
            var showDetail = layer.LayerNumber <= map.NodesCompleted ||
                             layer.IsRevealed ||
                             layer.ChosenOptionIndex.HasValue;

            if (!showDetail)
                return GetUnknownPathIcon();

            if (layer.IsBoss)
                return ExpeditionPathArt.PickPathSprite(_icons, layer.LayerNumber, 0);

            var idx = layer.ChosenOptionIndex ?? 0;
            var option = layer.Options.Count > idx ? layer.Options[idx] : null;
            return ExpeditionPathArt.PickPathSprite(_icons, layer.LayerNumber, option?.PathSpriteIndex ?? 0);
        }

        Sprite GetUnknownPathIcon()
        {
            var sprite = _icons?.UnknownPathIcon;
            if (ExpeditionPathSpriteUtil.IsFullPathSprite(sprite))
                return sprite;

            return sprite;
        }

        static string BuildRowLabel(ExpeditionMapLayer layer, ExpeditionMapState map, ExpeditionRunState run)
        {
            if (layer.IsBoss)
            {
                if (layer.LayerNumber <= map.NodesCompleted)
                    return $"第 {layer.LayerNumber} 层 · Boss（已通关）";

                return $"第 {layer.LayerNumber} 层 · Boss（未知）";
            }

            if (layer.ChosenOptionIndex is int chosen && chosen >= 0 && chosen < layer.Options.Count)
            {
                var opt = layer.Options[chosen];
                return $"第 {layer.LayerNumber} 层 · {BattleUiFormatters.DescribeNodeType(opt.NodeType)}\n{opt.DisplayName}";
            }

            if (layer.IsRevealed)
            {
                var parts = new System.Text.StringBuilder();
                parts.Append($"第 {layer.LayerNumber} 层 · 已探明\n");
                for (var i = 0; i < layer.Options.Count; i++)
                {
                    if (i > 0)
                        parts.Append(" / ");
                    parts.Append(BattleUiFormatters.DescribeNodeType(layer.Options[i].NodeType));
                }

                return parts.ToString();
            }

            if (layer.LayerNumber <= map.NodesCompleted)
                return $"第 {layer.LayerNumber} 层 · 已通过";

            if (layer.LayerNumber == map.NodesCompleted + 1)
                return $"第 {layer.LayerNumber} 层 · 当前选择";

            return $"第 {layer.LayerNumber} 层 · 未知";
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;

            var overlayGo = new GameObject(
                "ExpeditionMapOverlay",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            overlayGo.transform.SetParent(parent, false);
            _overlayRoot = overlayGo.GetComponent<RectTransform>();
            _overlayRoot.anchorMin = Vector2.zero;
            _overlayRoot.anchorMax = Vector2.one;
            _overlayRoot.offsetMin = Vector2.zero;
            _overlayRoot.offsetMax = Vector2.zero;

            var canvas = overlayGo.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = MapCanvasSortOrder;

            var blockerGo = new GameObject("Blocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blockerGo.transform.SetParent(_overlayRoot, false);
            var blockerRt = blockerGo.GetComponent<RectTransform>();
            blockerRt.anchorMin = Vector2.zero;
            blockerRt.anchorMax = Vector2.one;
            blockerRt.offsetMin = Vector2.zero;
            blockerRt.offsetMax = Vector2.zero;
            blockerGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);
            blockerGo.GetComponent<Button>().onClick.AddListener(Toggle);

            var panelGo = new GameObject("ExpeditionMapPanel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_overlayRoot, false);
            _root = panelGo.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0.5f);
            _root.anchorMax = new Vector2(0.5f, 0.5f);
            _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _root.anchoredPosition = Vector2.zero;

            var dim = panelGo.GetComponent<Image>();
            dim.color = new Color(0.06f, 0.07f, 0.1f, 0.98f);
            dim.raycastTarget = true;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Image));
            titleGo.transform.SetParent(_root, false);
            var titleBarRt = titleGo.GetComponent<RectTransform>();
            titleBarRt.anchorMin = new Vector2(0f, 1f);
            titleBarRt.anchorMax = new Vector2(1f, 1f);
            titleBarRt.pivot = new Vector2(0.5f, 1f);
            titleBarRt.sizeDelta = new Vector2(0f, 44f);
            titleBarRt.anchoredPosition = Vector2.zero;
            var titleBg = titleGo.GetComponent<Image>();
            titleBg.color = new Color(0.12f, 0.13f, 0.18f, 0.95f);
            titleBg.raycastTarget = true;
            var dragHandle = titleGo.AddComponent<UiPanelDragHandle>();

            var titleTextGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleTextGo.transform.SetParent(titleGo.transform, false);
            var titleRt = titleTextGo.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = new Vector2(-40f, 0f);
            _titleText = titleTextGo.GetComponent<Text>();
            _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.fontSize = 22;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = new Color(0.92f, 0.88f, 0.72f, 1f);
            _titleText.raycastTarget = false;

            var dragHintGo = new GameObject("DragHint", typeof(RectTransform), typeof(Text));
            dragHintGo.transform.SetParent(titleGo.transform, false);
            var dragHintRt = dragHintGo.GetComponent<RectTransform>();
            dragHintRt.anchorMin = new Vector2(1f, 0.5f);
            dragHintRt.anchorMax = new Vector2(1f, 0.5f);
            dragHintRt.pivot = new Vector2(1f, 0.5f);
            dragHintRt.anchoredPosition = new Vector2(-40f, 0f);
            dragHintRt.sizeDelta = new Vector2(72f, 24f);
            var dragHint = dragHintGo.GetComponent<Text>();
            dragHint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            dragHint.fontSize = 13;
            dragHint.alignment = TextAnchor.MiddleRight;
            dragHint.color = new Color(0.72f, 0.76f, 0.82f, 0.85f);
            dragHint.text = "≡ 拖动";
            dragHint.raycastTarget = false;

            dragHandle.SetDragTarget(_root);

            var hintGo = new GameObject("ScrollHint", typeof(RectTransform), typeof(Text));
            hintGo.transform.SetParent(_root, false);
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0f, 0f);
            hintRt.anchorMax = new Vector2(1f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.sizeDelta = new Vector2(0f, 28f);
            hintRt.anchoredPosition = new Vector2(0f, 8f);
            var hint = hintGo.GetComponent<Text>();
            hint.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hint.fontSize = 14;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = new Color(0.72f, 0.76f, 0.82f, 0.9f);
            hint.text = "滚轮 / 拖拽 / 滚动条查看各层";

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_root, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(12f, 40f);
            scrollRt.offsetMax = new Vector2(-28f, -52f);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = new Color(0.04f, 0.05f, 0.08f, 0.65f);
            scrollBg.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            _viewport = viewportGo.GetComponent<RectTransform>();
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = Vector2.zero;
            _viewport.offsetMax = Vector2.zero;
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(_viewport.transform, false);
            _scrollContent = contentGo.GetComponent<RectTransform>();
            _scrollContent.anchorMin = new Vector2(0f, 1f);
            _scrollContent.anchorMax = new Vector2(1f, 1f);
            _scrollContent.pivot = new Vector2(0.5f, 1f);
            _scrollContent.anchoredPosition = Vector2.zero;
            _scrollContent.sizeDelta = new Vector2(0f, 0f);

            var layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 8, 8);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollGo.transform, false);
            var scrollbarRt = scrollbarGo.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot = new Vector2(1f, 0.5f);
            scrollbarRt.sizeDelta = new Vector2(14f, 0f);
            scrollbarRt.anchoredPosition = Vector2.zero;
            scrollbarGo.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.18f, 0.85f);

            var handleAreaGo = new GameObject("Sliding Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(scrollbarGo.transform, false);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(2f, 6f);
            handleAreaRt.offsetMax = new Vector2(-2f, -6f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;
            handleGo.GetComponent<Image>().color = new Color(0.45f, 0.5f, 0.62f, 0.95f);

            var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleGo.GetComponent<Image>();

            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = _viewport;
            _scrollRect.content = _scrollContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 32f;
            _scrollRect.verticalScrollbar = scrollbar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_root, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.sizeDelta = new Vector2(36f, 36f);
            closeRt.anchoredPosition = new Vector2(-6f, -6f);
            closeGo.GetComponent<Image>().color = new Color(0.25f, 0.12f, 0.12f, 0.9f);
            closeGo.GetComponent<Button>().onClick.AddListener(Toggle);

            _overlayRoot.gameObject.SetActive(false);
        }
    }
}
