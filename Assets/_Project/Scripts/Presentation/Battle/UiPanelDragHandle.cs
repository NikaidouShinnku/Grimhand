using UnityEngine;
using UnityEngine.EventSystems;

namespace Grimhand.Presentation.Battle
{
    /// <summary>按住标题栏拖动面板。</summary>
    [DisallowMultipleComponent]
    public sealed class UiPanelDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] RectTransform dragTarget;

        RectTransform _target;
        Vector2 _dragStartAnchored;
        Vector2 _pointerStartLocal;

        public void SetDragTarget(RectTransform target) => dragTarget = target;

        void Awake()
        {
            _target = dragTarget != null ? dragTarget : transform.parent as RectTransform;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_target == null)
                return;

            _dragStartAnchored = _target.anchoredPosition;
            var parent = _target.parent as RectTransform;
            if (parent == null)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                eventData.position,
                eventData.pressEventCamera,
                out _pointerStartLocal);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_target == null)
                return;

            var parent = _target.parent as RectTransform;
            if (parent == null)
                return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var local))
                return;

            _target.anchoredPosition = _dragStartAnchored + (local - _pointerStartLocal);
        }
    }
}
