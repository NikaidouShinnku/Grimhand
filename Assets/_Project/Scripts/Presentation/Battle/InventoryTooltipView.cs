using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class InventoryTooltipView : MonoBehaviour
    {
        const float HideDelaySeconds = 0.05f;
        const float MaxWidth = 340f;

        RectTransform _panel;
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
            var go = new GameObject("InventoryTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(ContentSizeFitter));
            go.transform.SetParent(parent, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(MaxWidth, 120f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.97f);
            bg.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            go.SetActive(false);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            titleGo.transform.SetParent(go.transform, false);
            var titleLe = titleGo.GetComponent<LayoutElement>();
            titleLe.preferredWidth = MaxWidth - 20f;
            _title = titleGo.GetComponent<Text>();
            Style(_title, 16, TextAnchor.UpperLeft);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            bodyGo.transform.SetParent(go.transform, false);
            var bodyLe = bodyGo.GetComponent<LayoutElement>();
            bodyLe.preferredWidth = MaxWidth - 20f;
            _body = bodyGo.GetComponent<Text>();
            Style(_body, 14, TextAnchor.UpperLeft);
            _body.fontStyle = FontStyle.Normal;
        }

        public void BindHover(GameObject target, string title, string body)
        {
            if (target == null)
                return;

            var trigger = target.GetComponent<EventTrigger>() ?? target.AddComponent<EventTrigger>();
            trigger.triggers.Clear();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowAt(target, target.transform as RectTransform, title, body));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => ScheduleHide(target));
            trigger.triggers.Add(exit);
        }

        void ShowAt(GameObject target, RectTransform anchor, string title, string body)
        {
            if (_panel == null || anchor == null)
                return;

            CancelHide();
            _activeTarget = target;
            _title.text = title ?? "";
            _body.text = body ?? "";
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            var rightCenter = (corners[2] + corners[3]) * 0.5f;
            var leftCenter = (corners[0] + corners[1]) * 0.5f;
            var canvas = _panel.GetComponentInParent<Canvas>();
            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;

            var placeOnRight = true;
            if (canvasRect != null)
            {
                var canvasCorners = new Vector3[4];
                canvasRect.GetWorldCorners(canvasCorners);
                var canvasRight = canvasCorners[2].x;
                var estimatedRightEdge = rightCenter.x + _panel.rect.width + 16f;
                if (estimatedRightEdge > canvasRight - 12f)
                    placeOnRight = false;
            }

            if (placeOnRight)
                _panel.position = rightCenter + new Vector3(_panel.rect.width * 0.5f + 12f, 0f, 0f);
            else
                _panel.position = leftCenter + new Vector3(-_panel.rect.width * 0.5f - 12f, 0f, 0f);

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
