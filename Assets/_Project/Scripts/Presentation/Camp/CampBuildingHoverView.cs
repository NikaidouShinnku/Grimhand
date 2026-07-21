using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>
    /// 营地建筑：热区常驻接收悬停/点击；建筑贴图默认隐藏，悬停时放大弹出盖住背景绘制。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampBuildingHoverView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float HoverScale = 1.42f;
        const float ScaleLerpSpeed = 16f;

        static readonly Color HighlightOutlineColor = new(1f, 0.84f, 0.38f, 1f);
        static readonly Vector2 HighlightOutlineDistance = new(4f, 4f);

        RectTransform _visualRoot;
        CanvasGroup _visualGroup;
        Outline _outline;
        float _targetScale = 1f;
        bool _shown;

        public void Bind(RectTransform visualRoot, CanvasGroup visualGroup)
        {
            _visualRoot = visualRoot;
            _visualGroup = visualGroup;

            if (_visualRoot != null)
            {
                _outline = _visualRoot.GetComponent<Outline>();
                if (_outline == null)
                    _outline = _visualRoot.gameObject.AddComponent<Outline>();

                _outline.effectColor = HighlightOutlineColor;
                _outline.effectDistance = HighlightOutlineDistance;
                _outline.useGraphicAlpha = true;
                _outline.enabled = false;
                _visualRoot.localScale = Vector3.one;
            }

            SetShown(false, instant: true);
        }

        void Update()
        {
            if (_visualRoot == null || !_shown)
                return;

            var current = _visualRoot.localScale.x;
            if (Mathf.Approximately(current, _targetScale))
                return;

            var next = Mathf.Lerp(current, _targetScale, Time.unscaledDeltaTime * ScaleLerpSpeed);
            if (Mathf.Abs(next - _targetScale) < 0.001f)
                next = _targetScale;

            _visualRoot.localScale = Vector3.one * next;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetShown(true, instant: false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetShown(false, instant: false);
        }

        void OnDisable()
        {
            SetShown(false, instant: true);
        }

        void SetShown(bool shown, bool instant)
        {
            _shown = shown;
            _targetScale = shown ? HoverScale : 1f;

            if (_visualGroup != null)
            {
                _visualGroup.alpha = shown ? 1f : 0f;
                _visualGroup.blocksRaycasts = false;
                _visualGroup.interactable = false;
            }

            if (_outline != null)
                _outline.enabled = shown;

            if (_visualRoot != null && (instant || !shown))
                _visualRoot.localScale = Vector3.one * (shown ? HoverScale : 1f);
        }
    }
}
