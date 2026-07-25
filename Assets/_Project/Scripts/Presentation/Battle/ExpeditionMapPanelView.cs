using System.Collections;
using System.Text;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>横向远征地图：大框内每行 5 层，标注节点类型。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionMapPanelView : MonoBehaviour
    {
        const float PanelWidth = 1120f;
        const float PanelHeight = 720f;
        // 图标略收，间距加大，左右留白后整行居中（第 3 个对准中线）
        const float PathIconSize = 128f;
        const float CellGap = 56f;
        const float CellWidth = PathIconSize + 8f;
        const float CellHeight = PathIconSize + 72f;
        const float ScrollbarWidth = 32f;
        const float ScrollbarHandleNudgeX = 4f;
        const float ContentSidePad = 40f;
        const int LayersPerRow = 5;
        const int MapCanvasSortOrder = 90;

        const int LayoutVersion = 8;
        int _layoutVersion;

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        Transform _parent;
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
            _parent = parent;
            EnsureBuilt(parent);
        }

        public void Toggle()
        {
            if (_parent != null)
                EnsureBuilt(_parent);

            if (_open)
                CloseInternal(playSfx: true);
            else
                OpenInternal(playSfx: true);
        }

        public void Hide()
        {
            if (!_open)
                return;
            CloseInternal(playSfx: true);
        }

        void OpenInternal(bool playSfx)
        {
            _open = true;
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(true);

            if (playSfx)
                GameAudioService.Instance?.PlayUiMapOpen();

            BringToFront();
            Refresh();
            if (_scrollRect != null)
                _scrollRect.verticalNormalizedPosition = 0f;
            StartCoroutine(DeferredScrollAfterLayout());
        }

        void CloseInternal(bool playSfx)
        {
            _open = false;
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);

            if (playSfx)
                GameAudioService.Instance?.PlayUiMapClose();
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

            // 自上而下：高层（含 Boss）在上，每行 5 层，行内从左到右层号递增
            var total = map.Layers.Count;
            for (var start = ((total - 1) / LayersPerRow) * LayersPerRow; start >= 0; start -= LayersPerRow)
            {
                var end = Mathf.Min(start + LayersPerRow - 1, total - 1);
                BuildGridRow(map, start, end);
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
            KeepMapContentCentered();

            if (_open)
                StartCoroutine(DeferredScrollAfterLayout());
        }

        void KeepMapContentCentered()
        {
            if (_scrollContent == null)
                return;

            // 内容水平拉满视口：每行在全宽内居中，第 3 个节点对准大框中线
            _scrollContent.anchorMin = new Vector2(0f, 1f);
            _scrollContent.anchorMax = new Vector2(1f, 1f);
            _scrollContent.pivot = new Vector2(0.5f, 1f);
            _scrollContent.offsetMin = new Vector2(0f, _scrollContent.offsetMin.y);
            _scrollContent.offsetMax = new Vector2(0f, _scrollContent.offsetMax.y);
            var y = _scrollContent.anchoredPosition.y;
            _scrollContent.anchoredPosition = new Vector2(0f, y);
            var sd = _scrollContent.sizeDelta;
            _scrollContent.sizeDelta = new Vector2(0f, sd.y);
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
            KeepMapContentCentered();
            ScrollToCurrentLayer(_session.Expedition.Run.Map);
        }

        void ScrollToCurrentLayer(ExpeditionMapState map)
        {
            if (_scrollRect == null || _viewport == null || _scrollContent == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);

            var contentHeight = Mathf.Max(
                LayoutUtility.GetPreferredHeight(_scrollContent),
                _scrollContent.rect.height);
            var viewportHeight = _viewport.rect.height;
            var scrollRange = Mathf.Max(0f, contentHeight - viewportHeight);
            if (scrollRange <= 1f)
            {
                _scrollRect.verticalNormalizedPosition = 0f;
                return;
            }

            var total = Mathf.Max(1, map.Layers.Count);
            var currentLayer = Mathf.Clamp(map.NodesCompleted + 1, 1, total);
            var totalRows = (total + LayersPerRow - 1) / LayersPerRow;
            var rowFromBottom = (currentLayer - 1) / LayersPerRow;
            var rowFromTop = totalRows - 1 - rowFromBottom;
            var rowHeight = CellHeight + 12f;
            // 把当前行大致放到视口中下部，靠近「进度」位置
            var fromTop = rowFromTop * rowHeight;
            var targetOffset = Mathf.Clamp(fromTop - viewportHeight * 0.55f, 0f, scrollRange);
            _scrollRect.verticalNormalizedPosition = 1f - targetOffset / scrollRange;
        }

        void BuildGridRow(ExpeditionMapState map, int startIndex, int endIndex)
        {
            var rowGo = new GameObject($"Row_{startIndex}", typeof(RectTransform), typeof(LayoutElement));
            rowGo.transform.SetParent(_scrollContent, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.sizeDelta = new Vector2(0f, CellHeight);

            var rowLe = rowGo.GetComponent<LayoutElement>();
            rowLe.preferredHeight = CellHeight;
            rowLe.minHeight = CellHeight;
            rowLe.flexibleWidth = 1f;

            // 相对行中心排布：满行时第 3 格 x=0；不足 5 个时整组居中
            var count = endIndex - startIndex + 1;
            var blockW = count * CellWidth + Mathf.Max(0, count - 1) * CellGap;
            var startX = -blockW * 0.5f + CellWidth * 0.5f;
            var slot = 0;
            for (var i = startIndex; i <= endIndex; i++, slot++)
            {
                var x = startX + slot * (CellWidth + CellGap);
                BuildCell(rowRt, map.Layers[i], map, x);
            }
        }

        void BuildCell(Transform parent, ExpeditionMapLayer layer, ExpeditionMapState map, float centerX)
        {
            // 无底板：只保留路径图标 + 文字
            var cellGo = new GameObject($"Layer_{layer.LayerNumber}", typeof(RectTransform));
            cellGo.transform.SetParent(parent, false);
            var cellRt = cellGo.GetComponent<RectTransform>();
            cellRt.anchorMin = new Vector2(0.5f, 0.5f);
            cellRt.anchorMax = new Vector2(0.5f, 0.5f);
            cellRt.pivot = new Vector2(0.5f, 0.5f);
            cellRt.anchoredPosition = new Vector2(centerX, 0f);
            cellRt.sizeDelta = new Vector2(CellWidth, CellHeight);

            var isCurrent = layer.LayerNumber == map.NodesCompleted + 1;

            var pathGo = new GameObject("Path", typeof(RectTransform), typeof(Image));
            pathGo.transform.SetParent(cellGo.transform, false);
            var pathRt = pathGo.GetComponent<RectTransform>();
            pathRt.anchorMin = new Vector2(0.5f, 1f);
            pathRt.anchorMax = new Vector2(0.5f, 1f);
            pathRt.pivot = new Vector2(0.5f, 1f);
            pathRt.anchoredPosition = new Vector2(0f, -4f);
            pathRt.sizeDelta = new Vector2(PathIconSize, PathIconSize);
            ApplyPathImage(pathGo.GetComponent<Image>(), ResolvePathSprite(layer, map));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(cellGo.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0.02f, 0f);
            labelRt.anchorMax = new Vector2(0.98f, 0.34f);
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 17;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.UpperCenter;
            label.color = isCurrent
                ? new Color(1f, 0.92f, 0.55f, 1f)
                : Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            StripTextFx(label);
            label.text = BuildCellLabel(layer, map);
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

        static string BuildCellLabel(ExpeditionMapLayer layer, ExpeditionMapState map)
        {
            var typeLine = ResolveNodeTypeLine(layer, map);
            if (layer.IsBoss)
            {
                var state = layer.LayerNumber <= map.NodesCompleted ? "已通关" : "未知";
                return $"第 {layer.LayerNumber} 层\nBoss\n{state}";
            }

            if (layer.ChosenOptionIndex is int chosen && chosen >= 0 && chosen < layer.Options.Count)
            {
                var opt = layer.Options[chosen];
                return $"第 {layer.LayerNumber} 层\n{typeLine}\n{opt.DisplayName}";
            }

            if (layer.LayerNumber == map.NodesCompleted + 1)
                return $"第 {layer.LayerNumber} 层\n{typeLine}\n当前";

            if (layer.LayerNumber <= map.NodesCompleted)
                return $"第 {layer.LayerNumber} 层\n{typeLine}\n已通过";

            return $"第 {layer.LayerNumber} 层\n{typeLine}";
        }

        static string ResolveNodeTypeLine(ExpeditionMapLayer layer, ExpeditionMapState map)
        {
            if (layer.IsBoss)
                return "Boss";

            if (layer.ChosenOptionIndex is int chosen && chosen >= 0 && chosen < layer.Options.Count)
                return BattleUiFormatters.DescribeNodeType(layer.Options[chosen].NodeType);

            if (layer.IsRevealed && layer.Options.Count > 0)
            {
                var sb = new StringBuilder();
                for (var i = 0; i < layer.Options.Count; i++)
                {
                    if (i > 0)
                        sb.Append('/');
                    sb.Append(BattleUiFormatters.DescribeNodeType(layer.Options[i].NodeType));
                }

                return sb.ToString();
            }

            if (layer.LayerNumber <= map.NodesCompleted)
                return "已通过";

            return "？";
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && _layoutVersion == LayoutVersion && _overlayRoot != null)
                return;

            if (_overlayRoot != null)
                Destroy(_overlayRoot.gameObject);

            _open = false;
            _built = true;
            _layoutVersion = LayoutVersion;

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
            ApplyEventPlate(dim);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Image));
            titleGo.transform.SetParent(_root, false);
            var titleBarRt = titleGo.GetComponent<RectTransform>();
            titleBarRt.anchorMin = new Vector2(0f, 1f);
            titleBarRt.anchorMax = new Vector2(1f, 1f);
            titleBarRt.pivot = new Vector2(0.5f, 1f);
            titleBarRt.sizeDelta = new Vector2(0f, 48f);
            titleBarRt.anchoredPosition = Vector2.zero;
            var titleBg = titleGo.GetComponent<Image>();
            titleBg.color = Color.clear;
            titleBg.raycastTarget = true;
            var dragHandle = titleGo.AddComponent<UiPanelDragHandle>();
            dragHandle.SetDragTarget(_root);

            var titleTextGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
            titleTextGo.transform.SetParent(titleGo.transform, false);
            var titleRt = titleTextGo.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            // 标题略上移，落在框内
            titleRt.offsetMin = new Vector2(16f, 2f);
            titleRt.offsetMax = new Vector2(-48f, -4f);
            _titleText = titleTextGo.GetComponent<Text>();
            _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.fontSize = 22;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = new Color(0.92f, 0.88f, 0.72f, 1f);
            _titleText.raycastTarget = false;
            StripTextFx(_titleText);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(_root, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(ContentSidePad, 18f);
            scrollRt.offsetMax = new Vector2(-(ScrollbarWidth + 14f), -58f);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = Color.clear;
            scrollBg.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            _viewport = viewportGo.GetComponent<RectTransform>();
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = Vector2.zero;
            _viewport.offsetMax = Vector2.zero;
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = Color.clear;
            viewportImg.raycastTarget = true;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(_viewport.transform, false);
            _scrollContent = contentGo.GetComponent<RectTransform>();
            // 水平拉满视口，行内再居中五个节点
            _scrollContent.anchorMin = new Vector2(0f, 1f);
            _scrollContent.anchorMax = new Vector2(1f, 1f);
            _scrollContent.pivot = new Vector2(0.5f, 1f);
            _scrollContent.anchoredPosition = Vector2.zero;
            _scrollContent.offsetMin = Vector2.zero;
            _scrollContent.offsetMax = Vector2.zero;
            _scrollContent.sizeDelta = new Vector2(0f, 0f);

            var vLayout = contentGo.GetComponent<VerticalLayoutGroup>();
            vLayout.spacing = 14f;
            vLayout.padding = new RectOffset(8, 8, 10, 10);
            vLayout.childAlignment = TextAnchor.UpperCenter;
            vLayout.childControlWidth = true;
            vLayout.childControlHeight = true;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollbar = BuildStyledScrollbar(scrollGo.transform);
            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.viewport = _viewport;
            _scrollRect.content = _scrollContent;
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.movementType = ScrollRect.MovementType.Clamped;
            _scrollRect.scrollSensitivity = 36f;
            _scrollRect.verticalScrollbar = scrollbar;
            _scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            var closeGo = new GameObject("Close", typeof(RectTransform), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_root, false);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.sizeDelta = new Vector2(40f, 40f);
            closeRt.anchoredPosition = new Vector2(-10f, -8f);
            closeGo.GetComponent<Image>().color = new Color(0.25f, 0.12f, 0.12f, 0.9f);
            closeGo.GetComponent<Button>().onClick.AddListener(Toggle);

            var closeLabelGo = new GameObject("X", typeof(RectTransform), typeof(Text));
            closeLabelGo.transform.SetParent(closeGo.transform, false);
            var closeLabelRt = closeLabelGo.GetComponent<RectTransform>();
            closeLabelRt.anchorMin = Vector2.zero;
            closeLabelRt.anchorMax = Vector2.one;
            closeLabelRt.offsetMin = Vector2.zero;
            closeLabelRt.offsetMax = Vector2.zero;
            var closeLabel = closeLabelGo.GetComponent<Text>();
            closeLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            closeLabel.fontSize = 22;
            closeLabel.fontStyle = FontStyle.Bold;
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = Color.white;
            closeLabel.text = "×";
            closeLabel.raycastTarget = false;

            _overlayRoot.gameObject.SetActive(false);
        }

        Scrollbar BuildStyledScrollbar(Transform scrollParent)
        {
            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollParent, false);
            var scrollbarRt = scrollbarGo.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot = new Vector2(1f, 0.5f);
            scrollbarRt.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            scrollbarRt.anchoredPosition = Vector2.zero;

            var barImg = scrollbarGo.GetComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = true;
            barImg.preserveAspect = false;
            if (_icons != null && _icons.UiSliderBar != null)
                barImg.sprite = _icons.UiSliderBar;
            else
                barImg.color = new Color(0.12f, 0.11f, 0.1f, 0.95f);

            var handleAreaGo = new GameObject("Sliding Area", typeof(RectTransform));
            handleAreaGo.transform.SetParent(scrollbarGo.transform, false);
            var handleAreaRt = handleAreaGo.GetComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(2f + ScrollbarHandleNudgeX, 18f);
            handleAreaRt.offsetMax = new Vector2(-1f + ScrollbarHandleNudgeX, -18f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;
            handleImg.preserveAspect = false;
            if (_icons != null && _icons.UiSlider != null)
                handleImg.sprite = _icons.UiSlider;
            else
                handleImg.color = new Color(0.42f, 0.34f, 0.28f, 1f);

            var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImg;
            scrollbar.numberOfSteps = 0;
            return scrollbar;
        }

        static void StripTextFx(Text text)
        {
            if (text == null)
                return;
            foreach (var fx in text.GetComponents<Shadow>())
                UnityEngine.Object.Destroy(fx);
        }

        void ApplyEventPlate(Image image)
        {
            if (image == null)
                return;

            var plate = _icons != null ? _icons.UiEventPlate : null;
            if (plate != null)
            {
                image.sprite = plate;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = new Color(0.06f, 0.07f, 0.1f, 0.98f);
            }

            image.raycastTarget = true;
        }
    }
}
