using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>营地建筑悬停：轻微放大 + 高亮描边。</summary>
    [DisallowMultipleComponent]
    public sealed class CampBuildingHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float HoverScale = 1.08f;
        const float ScaleLerpSpeed = 14f;

        static readonly Color HighlightOutlineColor = new(1f, 0.84f, 0.38f, 1f);
        static readonly Vector2 HighlightOutlineDistance = new(3f, 3f);

        RectTransform _scaleTarget;
        Outline _outline;
        float _targetScale = 1f;

        public void Bind(RectTransform scaleTarget)
        {
            _scaleTarget = scaleTarget;
            _outline = scaleTarget.GetComponent<Outline>();
            if (_outline == null)
                _outline = scaleTarget.gameObject.AddComponent<Outline>();

            _outline.effectColor = HighlightOutlineColor;
            _outline.effectDistance = HighlightOutlineDistance;
            _outline.useGraphicAlpha = true;
            _outline.enabled = false;

            _scaleTarget.localScale = Vector3.one;
        }

        void Update()
        {
            if (_scaleTarget == null)
                return;

            var current = _scaleTarget.localScale.x;
            if (Mathf.Approximately(current, _targetScale))
                return;

            var next = Mathf.Lerp(current, _targetScale, Time.unscaledDeltaTime * ScaleLerpSpeed);
            if (Mathf.Abs(next - _targetScale) < 0.001f)
                next = _targetScale;

            _scaleTarget.localScale = Vector3.one * next;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _targetScale = HoverScale;
            if (_outline != null)
                _outline.enabled = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _targetScale = 1f;
            if (_outline != null)
                _outline.enabled = false;
        }

        void OnDisable()
        {
            _targetScale = 1f;
            if (_scaleTarget != null)
                _scaleTarget.localScale = Vector3.one;
            if (_outline != null)
                _outline.enabled = false;
        }
    }
}
