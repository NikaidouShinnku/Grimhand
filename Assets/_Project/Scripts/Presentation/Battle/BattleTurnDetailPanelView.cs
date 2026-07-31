using System.Text;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    [DisallowMultipleComponent]
    public sealed class BattleTurnDetailPanelView : MonoBehaviour
    {
        const float PanelWidth = 520f;
        const float PanelHeight = 560f;
        // 左下三按钮（约 8+96）右侧，避免盖住
        const float PanelLeft = 120f;
        const float PanelBottom = 12f;

        const int DetailLayoutVersion = 5;
        const float ScrollbarWidth = 18f;
        // 相对面板右缘内缩，避免贴住装饰边框
        const float ScrollbarInsetRight = 14f;
        int _layoutVersion;

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        Transform _battleRoot;
        RectTransform _panel;
        RectTransform _content;
        Text _bodyText;
        ScrollRect _scroll;
        bool _open;
        bool _built;

        public bool IsOpen => _open;

        public void Hide()
        {
            _open = false;
            if (_panel != null)
                _panel.gameObject.SetActive(false);
        }

        public void Initialize(BattleSession session, Transform battleRoot, BattleUiIconCatalogSO icons = null)
        {
            _session = session;
            _battleRoot = battleRoot;
            _icons = icons;
            EnsureBuilt(battleRoot);
        }

        public void Toggle()
        {
            if (_battleRoot != null)
                EnsureBuilt(_battleRoot);

            _open = !_open;
            if (_panel == null)
                return;

            if (_open)
            {
                CombatantTooltipLayer.MountToFront(_panel, _battleRoot != null ? _battleRoot : _panel.root);
                _panel.gameObject.SetActive(true);
                Refresh(resetScrollToTop: true);
            }
            else
            {
                _panel.gameObject.SetActive(false);
            }
        }

        public void Refresh() => Refresh(resetScrollToTop: false);

        void Refresh(bool resetScrollToTop)
        {
            if (!_built || _bodyText == null || _session == null)
                return;

            if (!_open)
                return;

            var sb = new StringBuilder();

            if (_session.IsExpeditionMode && _session.Expedition?.Run?.RunAcquisitionLog is { Count: > 0 } acquisitions)
            {
                sb.AppendLine("【远征获取】");
                for (var i = 0; i < acquisitions.Count; i++)
                    sb.AppendLine($"· {acquisitions[i]}");
                sb.AppendLine();
            }

            if (_session.ConsumablesUsedThisBattle is { Count: > 0 } consumables)
            {
                sb.AppendLine("【本战消耗品】");
                for (var i = 0; i < consumables.Count; i++)
                    sb.AppendLine($"· {consumables[i]}");
                sb.AppendLine();
            }

            var lines = _session.TurnLog.LastRound;
            if (lines == null || lines.Count == 0)
            {
                if (sb.Length == 0)
                    sb.AppendLine("暂无战斗明细。\n出牌结算后可在此查看（最多保留 40 条）。");
                else
                    sb.AppendLine("【战斗明细】\n（本回合暂无出牌记录）");
            }
            else
            {
                sb.AppendLine("【战斗明细】");
                for (var i = 0; i < lines.Count; i++)
                    sb.AppendLine($"{i + 1}. {lines[i]}");
            }

            _bodyText.text = sb.ToString();
            ResizeBody(resetScrollToTop);
        }

        void ResizeBody(bool resetScrollToTop)
        {
            if (_bodyText == null || _content == null || _scroll == null)
                return;

            // 战斗刷新很频繁：绝不能每次都强制回顶，否则滚到一半会被“弹回”。
            var savedScroll = _scroll.verticalNormalizedPosition;
            var viewportWidth = _scroll.viewport != null
                ? _scroll.viewport.rect.width
                : (_content.rect.width > 1f ? _content.rect.width : 460f);
            var usableWidth = Mathf.Max(viewportWidth - 16f, 80f);

            var bodyRt = _bodyText.rectTransform;
            bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, usableWidth);

            // 按实际换行高度撑开正文与 content，保证能滚到最底。
            var preferred = Mathf.Max(40f, _bodyText.preferredHeight + 8f);
            bodyRt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred);
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewportWidth);
            _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, preferred);

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(bodyRt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);

            _scroll.verticalNormalizedPosition = resetScrollToTop
                ? 1f
                : Mathf.Clamp01(savedScroll);
        }

        void EnsureBuilt(Transform battleRoot)
        {
            if (_built && _layoutVersion == DetailLayoutVersion && _panel != null)
                return;

            if (_panel != null)
                Destroy(_panel.gameObject);

            _built = true;
            _layoutVersion = DetailLayoutVersion;
            _open = false;

            var go = new GameObject("TurnDetailPanel", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(battleRoot, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0f, 0f);
            _panel.anchorMax = new Vector2(0f, 0f);
            _panel.pivot = new Vector2(0f, 0f);
            _panel.anchoredPosition = new Vector2(PanelLeft, PanelBottom);
            _panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var bg = go.GetComponent<Image>();
            ApplyEventPlate(bg);

            var titleGo = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            titleGo.transform.SetParent(go.transform, false);
            var titleBarRt = titleGo.GetComponent<RectTransform>();
            titleBarRt.anchorMin = new Vector2(0f, 1f);
            titleBarRt.anchorMax = new Vector2(1f, 1f);
            titleBarRt.pivot = new Vector2(0.5f, 1f);
            titleBarRt.anchoredPosition = Vector2.zero;
            titleBarRt.sizeDelta = new Vector2(0f, 44f);
            var titleBg = titleGo.GetComponent<Image>();
            titleBg.color = Color.clear;
            titleBg.raycastTarget = true;
            var drag = titleGo.AddComponent<UiPanelDragHandle>();
            drag.SetDragTarget(_panel);

            var titleTextGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleTextGo.transform.SetParent(titleGo.transform, false);
            var titleRt = titleTextGo.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(28f, -4f);
            titleRt.offsetMax = new Vector2(-12f, -8f);
            var title = titleTextGo.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = 20;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleLeft;
            title.color = Color.white;
            title.text = "明细";
            title.raycastTarget = false;
            foreach (var fx in title.GetComponents<Shadow>())
                Destroy(fx);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(go.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(16f, 16f);
            scrollRt.offsetMax = new Vector2(-(ScrollbarInsetRight + ScrollbarWidth + 6f), -52f);
            var scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = Color.clear;
            scrollBg.raycastTarget = true;

            // RectMask2D 裁剪内容；Image 仅作射线接收，完全透明避免灰框虚影
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            var viewportImage = viewportGo.GetComponent<Image>();
            viewportImage.color = Color.clear;
            viewportImage.raycastTarget = true;
            viewportImage.sprite = null;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = new Vector2(0f, 0f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(contentGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.anchoredPosition = Vector2.zero;
            bodyRt.sizeDelta = new Vector2(-16f, 0f);

            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 16;
            _bodyText.fontStyle = FontStyle.Normal;
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.color = new Color(0.92f, 0.94f, 0.98f, 1f);
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _bodyText.raycastTarget = true;
            foreach (var fx in _bodyText.GetComponents<Shadow>())
                Destroy(fx);

            _scroll = scrollGo.GetComponent<ScrollRect>();
            _scroll.viewport = viewportRt;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.inertia = true;
            _scroll.decelerationRate = 0.135f;
            _scroll.scrollSensitivity = 36f;

            var scrollbar = BuildStyledScrollbar(go.transform);
            _scroll.verticalScrollbar = scrollbar;
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _scroll.verticalScrollbarSpacing = 0f;

            ScrollRectNavigation.WireForwarding(bodyGo, _scroll);

            go.SetActive(false);
        }

        Scrollbar BuildStyledScrollbar(Transform panelParent)
        {
            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(panelParent, false);
            var scrollbarRt = scrollbarGo.GetComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1f, 0f);
            scrollbarRt.anchorMax = new Vector2(1f, 1f);
            scrollbarRt.pivot = new Vector2(1f, 0.5f);
            scrollbarRt.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            scrollbarRt.anchoredPosition = new Vector2(-ScrollbarInsetRight, 0f);
            scrollbarRt.offsetMin = new Vector2(scrollbarRt.offsetMin.x, 16f);
            scrollbarRt.offsetMax = new Vector2(scrollbarRt.offsetMax.x, -52f);

            var barImg = scrollbarGo.GetComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = true;
            barImg.preserveAspect = false;
            if (_icons != null && _icons.UiSliderBar != null)
                barImg.sprite = _icons.UiSliderBar;
            else
                barImg.color = new Color(0.12f, 0.11f, 0.1f, 0.95f);

            var slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
            slidingArea.transform.SetParent(scrollbarGo.transform, false);
            var slidingRt = slidingArea.GetComponent<RectTransform>();
            slidingRt.anchorMin = Vector2.zero;
            slidingRt.anchorMax = Vector2.one;
            slidingRt.offsetMin = new Vector2(1f, 10f);
            slidingRt.offsetMax = new Vector2(-1f, -10f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(slidingArea.transform, false);
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
            scrollbar.value = 1f;
            scrollbar.size = 1f;
            scrollbar.numberOfSteps = 0;
            return scrollbar;
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
                image.color = new Color(0.08f, 0.1f, 0.14f, 0.97f);
            }

            image.raycastTarget = true;
        }
    }
}
