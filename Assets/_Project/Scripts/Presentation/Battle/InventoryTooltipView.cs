using Grimhand.Content;
using Grimhand.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class InventoryTooltipView : MonoBehaviour
    {
        /// <summary>
        /// 仅作保险：PointerExit 丢失时，指针离开目标超过该秒数才强制隐藏。
        /// 正常离开物品应立刻消失，不走此延迟。
        /// </summary>
        const float StaleHideSeconds = 1f;
        const float TitleBodySpacing = 6f;

        RectTransform _panel;
        RectTransform _content;
        Image _panelBg;
        Text _title;
        Text _body;
        bool _built;
        GameObject _activeTarget;
        float _leftTargetAt = -1f;

        public void Initialize(RectTransform parent, BattleUiIconCatalogSO icons = null)
        {
            if (_built)
            {
                ApplyInformationPlate(icons);
                return;
            }

            _built = true;
            var go = new GameObject("InventoryTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(UiInfoPlateMetrics.MinWidth, 80f);
            _panelBg = go.GetComponent<Image>();
            _panelBg.raycastTarget = false;
            ApplyInformationPlate(icons);
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            go.SetActive(false);

            _content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            _content.SetParent(go.transform, false);
            UiInfoPlateMetrics.ApplyTextInsets(_content);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(_content, false);
            _title = titleGo.GetComponent<Text>();
            Style(_title, 16, TextAnchor.UpperLeft);
            StretchTop(_title.rectTransform);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(_content, false);
            _body = bodyGo.GetComponent<Text>();
            Style(_body, 14, TextAnchor.UpperLeft);
            _body.fontStyle = FontStyle.Normal;
            StretchTop(_body.rectTransform);
        }

        static void StretchTop(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 20f);
        }

        void ApplyInformationPlate(BattleUiIconCatalogSO icons)
        {
            if (_panelBg == null)
                return;

            var plate = icons != null ? icons.UiInformationPlate : null;
            if (plate != null)
            {
                _panelBg.sprite = plate;
                _panelBg.type = Image.Type.Simple;
                _panelBg.preserveAspect = false;
                _panelBg.color = Color.white;
            }
            else
            {
                _panelBg.sprite = null;
                _panelBg.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);
            }
        }

        public void BindHover(GameObject target, string title, string body, bool showTitle = true)
        {
            if (target == null)
                return;

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowAt(target, target.transform as RectTransform, title, body, showTitle));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            // 离开物品立刻隐藏；1 秒兜底仅用于 PointerExit 丢失（见 LateUpdate）
            exit.callback.AddListener(_ => HideIfTarget(target));
            trigger.triggers.Add(exit);
        }

        void ShowAt(GameObject target, RectTransform anchor, string title, string body, bool showTitle)
        {
            if (_panel == null || anchor == null)
                return;

            _leftTargetAt = -1f;
            _activeTarget = target;

            var hasTitle = showTitle && !string.IsNullOrWhiteSpace(title);
            _title.gameObject.SetActive(hasTitle);
            _title.text = hasTitle ? title : "";

            var hasBody = !string.IsNullOrWhiteSpace(body);
            _body.text = hasBody ? body : "";
            _body.gameObject.SetActive(hasBody);

            ResizePanelToFit(hasTitle, hasBody);

            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var topCenter = (corners[1] + corners[2]) * 0.5f;
            var bottomCenter = (corners[0] + corners[3]) * 0.5f;
            var canvas = _panel.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;

            var placeAbove = true;
            if (canvasRect != null)
            {
                var canvasCorners = new Vector3[4];
                canvasRect.GetWorldCorners(canvasCorners);
                var canvasTop = canvasCorners[1].y;
                var estimatedTop = topCenter.y + _panel.rect.height + 16f;
                if (estimatedTop > canvasTop - 8f)
                    placeAbove = false;
            }

            if (placeAbove)
                _panel.position = topCenter + new Vector3(0f, _panel.rect.height * 0.5f + 14f, 0f);
            else
                _panel.position = bottomCenter + new Vector3(0f, -_panel.rect.height * 0.5f - 14f, 0f);

            if (canvasRect != null)
                ClampToCanvas(canvasRect);
        }

        void ResizePanelToFit(bool hasTitle, bool hasBody)
        {
            // 宽度：尽量窄；超长才顶到 MaxWidth 并换行加高
            var maxInner = UiInfoPlateMetrics.InnerWidth(UiInfoPlateMetrics.MaxWidth);
            var contentW = 0f;
            var contentH = 0f;
            var y = 0f;

            if (hasTitle)
            {
                var rawW = UiInfoPlateMetrics.MeasureUnwrappedWidth(_title, _title.text, maxInner);
                var tw = rawW >= maxInner - 1f ? maxInner : Mathf.Max(rawW, 40f);
                var th = UiInfoPlateMetrics.MeasureHeight(_title, _title.text, tw);
                contentW = Mathf.Max(contentW, tw);
                contentH += th;
                PlaceBlock(_title.rectTransform, y, th);
                y += th + (hasBody ? TitleBodySpacing : 0f);
            }

            if (hasBody)
            {
                if (hasTitle)
                    contentH += TitleBodySpacing;

                var rawW = UiInfoPlateMetrics.MeasureUnwrappedWidth(_body, _body.text, maxInner);
                var bw = rawW >= maxInner - 1f ? maxInner : Mathf.Max(rawW, 40f);
                // 若与标题同框，宽度对齐到内容总宽（仍不超过 Max）
                bw = Mathf.Max(bw, contentW);
                if (bw > maxInner)
                    bw = maxInner;
                var bh = UiInfoPlateMetrics.MeasureHeight(_body, _body.text, bw);
                contentW = Mathf.Max(contentW, bw);
                contentH += bh;
                PlaceBlock(_body.rectTransform, y, bh);
            }

            // 最终宽度按内容，但二次用最终宽度复测高度（换行后可能更高）
            var panelW = Mathf.Clamp(contentW + UiInfoPlateMetrics.PadX * 2f,
                UiInfoPlateMetrics.MinWidth, UiInfoPlateMetrics.MaxWidth);
            var innerW = UiInfoPlateMetrics.InnerWidth(panelW);
            contentH = 0f;
            y = 0f;
            if (hasTitle)
            {
                var th = UiInfoPlateMetrics.MeasureHeight(_title, _title.text, innerW);
                contentH += th;
                PlaceBlock(_title.rectTransform, y, th);
                y += th + (hasBody ? TitleBodySpacing : 0f);
            }

            if (hasBody)
            {
                if (hasTitle)
                    contentH += TitleBodySpacing;
                var bh = UiInfoPlateMetrics.MeasureHeight(_body, _body.text, innerW);
                contentH += bh;
                PlaceBlock(_body.rectTransform, y, bh);
            }

            _panel.sizeDelta = UiInfoPlateMetrics.FitPanelSize(innerW, contentH);
            UiInfoPlateMetrics.ApplyTextInsets(_content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
        }

        static void PlaceBlock(RectTransform rt, float fromTop, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -fromTop);
            rt.sizeDelta = new Vector2(0f, height);
        }

        void ClampToCanvas(RectTransform canvasRect)
        {
            var canvasCorners = new Vector3[4];
            canvasRect.GetWorldCorners(canvasCorners);
            var panelCorners = new Vector3[4];
            _panel.GetWorldCorners(panelCorners);

            var shift = Vector3.zero;
            if (panelCorners[2].x > canvasCorners[2].x - 8f)
                shift.x = canvasCorners[2].x - 8f - panelCorners[2].x;
            else if (panelCorners[0].x < canvasCorners[0].x + 8f)
                shift.x = canvasCorners[0].x + 8f - panelCorners[0].x;

            if (panelCorners[1].y > canvasCorners[1].y - 8f)
                shift.y = canvasCorners[1].y - 8f - panelCorners[1].y;
            else if (panelCorners[0].y < canvasCorners[0].y + 8f)
                shift.y = canvasCorners[0].y + 8f - panelCorners[0].y;

            if (shift.sqrMagnitude > 0.0001f)
                _panel.position += shift;
        }

        void HideIfTarget(GameObject target)
        {
            if (_activeTarget != target)
                return;

            HideImmediate();
        }

        void HideImmediate()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            _activeTarget = null;
            _leftTargetAt = -1f;
        }

        public void Hide() => HideImmediate();

        void LateUpdate()
        {
            if (_panel == null || !_panel.gameObject.activeSelf)
                return;

            // Unity 已销毁对象 == null 为 true：必须清掉残留面板
            if (_activeTarget == null)
            {
                HideImmediate();
                return;
            }

            if (!_activeTarget.activeInHierarchy)
            {
                HideImmediate();
                return;
            }

            var rt = _activeTarget.transform as RectTransform;
            if (rt == null)
            {
                HideImmediate();
                return;
            }

            // 指针仍在目标上：清掉滞留计时
            if (UiPointerUtility.IsOverRectTransform(rt, UiPointerUtility.GetEventCamera(rt)))
            {
                _leftTargetAt = -1f;
                return;
            }

            // PointerExit 可能丢失（遮罩/切场景等）：离开满 1 秒再强制隐藏作保险
            if (_leftTargetAt < 0f)
                _leftTargetAt = Time.unscaledTime;

            if (Time.unscaledTime - _leftTargetAt >= StaleHideSeconds)
                HideImmediate();
        }

        static void Style(Text text, int size, TextAnchor anchor)
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
