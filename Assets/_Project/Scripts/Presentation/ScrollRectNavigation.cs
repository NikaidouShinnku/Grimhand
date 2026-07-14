using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation
{
    /// <summary>
    /// ScrollRect 导航辅助：保留滚动位置，并把子节点上的滚轮/拖拽转发给父级 ScrollRect
    /// （解决点在 Button/卡牌上无法滑动、重建列表后跳回顶部）。
    /// </summary>
    public static class ScrollRectNavigation
    {
        public static ScrollRect FindInParents(Transform start)
        {
            if (start == null)
                return null;
            return start.GetComponentInParent<ScrollRect>();
        }

        public static float CaptureVertical(ScrollRect scroll)
        {
            if (scroll == null)
                return 1f;
            return scroll.verticalNormalizedPosition;
        }

        public static float CaptureHorizontal(ScrollRect scroll)
        {
            if (scroll == null)
                return 0f;
            return scroll.horizontalNormalizedPosition;
        }

        public static void RestoreVertical(ScrollRect scroll, float normalized)
        {
            if (scroll == null)
                return;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition = Mathf.Clamp01(normalized);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        public static void RestoreHorizontal(ScrollRect scroll, float normalized)
        {
            if (scroll == null)
                return;

            Canvas.ForceUpdateCanvases();
            if (scroll.content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(normalized);
            Canvas.ForceUpdateCanvases();
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(normalized);
        }

        /// <summary>在可点击子物体上挂转发器，使指针落在其上时仍可滚轮/拖动手势滑动。</summary>
        public static void WireForwarding(GameObject target, ScrollRect scroll = null)
        {
            if (target == null)
                return;

            var forwarder = target.GetComponent<ScrollRectEventForwarder>();
            if (forwarder == null)
                forwarder = target.AddComponent<ScrollRectEventForwarder>();
            forwarder.Bind(scroll != null ? scroll : FindInParents(target.transform));
        }

        public static void WireForwarding(Component target, ScrollRect scroll = null)
        {
            if (target != null)
                WireForwarding(target.gameObject, scroll);
        }
    }

    /// <summary>把自身收到的滚轮与拖拽转给指定 ScrollRect。</summary>
    public sealed class ScrollRectEventForwarder : MonoBehaviour,
        IInitializePotentialDragHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler,
        IScrollHandler
    {
        ScrollRect _scroll;
        bool _dragRouted;

        public void Bind(ScrollRect scroll) => _scroll = scroll;

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            EnsureScroll();
            _scroll?.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureScroll();
            if (_scroll == null)
                return;

            // 仅横向/纵向主导的拖动手势才交给 ScrollRect，避免抢走短点按。
            var delta = eventData.delta;
            var horizontal = _scroll.horizontal && Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
            var vertical = _scroll.vertical && Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
            _dragRouted = horizontal || vertical || delta.sqrMagnitude < 0.01f;
            if (_dragRouted)
                _scroll.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragRouted)
            {
                EnsureScroll();
                if (_scroll == null)
                    return;
                var delta = eventData.delta;
                var horizontal = _scroll.horizontal && Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
                var vertical = _scroll.vertical && Mathf.Abs(delta.y) > Mathf.Abs(delta.x);
                if (!horizontal && !vertical)
                    return;
                _dragRouted = true;
                _scroll.OnBeginDrag(eventData);
            }

            _scroll?.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragRouted)
                _scroll?.OnEndDrag(eventData);
            _dragRouted = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            EnsureScroll();
            _scroll?.OnScroll(eventData);
        }

        void EnsureScroll()
        {
            if (_scroll == null)
                _scroll = ScrollRectNavigation.FindInParents(transform);
        }
    }
}
