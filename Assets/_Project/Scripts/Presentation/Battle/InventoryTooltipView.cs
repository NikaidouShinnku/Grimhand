using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class InventoryTooltipView : MonoBehaviour
    {
        const float HideDelaySeconds = 0.05f;
        const float MaxWidth = 360f;
        const float Padding = 12f;

        RectTransform _panel;
        RectTransform _content;
        Text _title;
        Text _body;
        bool _built;
        GameObject _activeTarget;
        GameObject _pendingHideTarget;
        Coroutine _hideRoutine;

        public void Initialize(RectTransform parent)
        {
            if (_built)
                return;

            _built = true;
            var go = new GameObject("InventoryTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(MaxWidth, 120f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.04f, 0.05f, 0.08f, 0.98f);
            bg.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            go.SetActive(false);

            _content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter))
                .GetComponent<RectTransform>();
            _content.SetParent(go.transform, false);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(Padding, Padding);
            _content.offsetMax = new Vector2(-Padding, -Padding);
            var layout = _content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = _content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(_content, false);
            titleGo.GetComponent<LayoutElement>().preferredWidth = MaxWidth - Padding * 2f;
            _title = titleGo.GetComponent<Text>();
            Style(_title, 16, TextAnchor.UpperLeft);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            bodyGo.transform.SetParent(_content, false);
            bodyGo.GetComponent<LayoutElement>().preferredWidth = MaxWidth - Padding * 2f;
            _body = bodyGo.GetComponent<Text>();
            Style(_body, 14, TextAnchor.UpperLeft);
            _body.fontStyle = FontStyle.Normal;
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
            exit.callback.AddListener(_ => ScheduleHide(target));
            trigger.triggers.Add(exit);
        }

        void ShowAt(GameObject target, RectTransform anchor, string title, string body, bool showTitle)
        {
            if (_panel == null || anchor == null)
                return;

            CancelHide();
            _activeTarget = target;

            var hasTitle = showTitle && !string.IsNullOrWhiteSpace(title);
            _title.gameObject.SetActive(hasTitle);
            _title.text = hasTitle ? title : "";

            _body.text = body ?? "";
            _body.gameObject.SetActive(!string.IsNullOrWhiteSpace(_body.text));

            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

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

        void ScheduleHide(GameObject target)
        {
            if (_activeTarget != target)
                return;

            _pendingHideTarget = target;
            CancelHide();
            _hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            var target = _pendingHideTarget;
            yield return new WaitForSecondsRealtime(HideDelaySeconds);
            _hideRoutine = null;
            if (_activeTarget != target)
                yield break;

            HideImmediate();
        }

        void HideImmediate()
        {
            if (_panel != null)
                _panel.gameObject.SetActive(false);
            _activeTarget = null;
            _pendingHideTarget = null;
        }

        void CancelHide()
        {
            if (_hideRoutine != null)
            {
                StopCoroutine(_hideRoutine);
                _hideRoutine = null;
            }

            _pendingHideTarget = null;
        }

        public void Hide()
        {
            CancelHide();
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
