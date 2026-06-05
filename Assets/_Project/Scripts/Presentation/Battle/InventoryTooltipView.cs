using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class InventoryTooltipView : MonoBehaviour
    {
        const float HideDelaySeconds = 0.04f;

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
            var go = new GameObject("InventoryTooltip", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _panel = go.GetComponent<RectTransform>();
            _panel.sizeDelta = new Vector2(280f, 120f);
            var bg = go.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.07f, 0.1f, 0.97f);
            bg.raycastTarget = false;
            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            go.SetActive(false);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.offsetMin = new Vector2(10f, -34f);
            titleRt.offsetMax = new Vector2(-10f, -6f);
            _title = titleGo.GetComponent<Text>();
            Style(_title, 16, TextAnchor.UpperLeft);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(go.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(10f, 8f);
            bodyRt.offsetMax = new Vector2(-10f, -38f);
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
            _panel.position = rightCenter + new Vector3(_panel.rect.width * 0.5f + 10f, 0f, 0f);
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
            _activeTarget = null;
            if (_panel != null)
                _panel.gameObject.SetActive(false);
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
