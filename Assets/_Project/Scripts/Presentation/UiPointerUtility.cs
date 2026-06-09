using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Grimhand.Presentation
{
    public static class UiPointerUtility
    {
        public static bool TryGetScreenPosition(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.isPressed)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }

            screenPosition = default;
            return false;
#else
            screenPosition = Input.mousePosition;
            return true;
#endif
        }

        public static bool IsOverRectTransform(RectTransform rectTransform, Camera eventCamera)
        {
            if (rectTransform == null || !TryGetScreenPosition(out var screenPosition))
                return false;

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
        }

        public static Camera GetEventCamera(RectTransform rectTransform)
        {
            if (rectTransform == null)
                return null;

            var canvas = rectTransform.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
        }
    }
}
