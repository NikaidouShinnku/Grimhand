using UnityEngine;

namespace Grimhand.Presentation.Battle
{
    public static class UiSpriteBounds
    {
        public static void FitCentered(RectTransform container, RectTransform target, Sprite sprite, float padding = 0f)
        {
            if (container == null || target == null)
                return;

            var rect = container.rect;
            if (sprite == null || rect.width <= 0f || rect.height <= 0f)
            {
                StretchCenter(target, rect.width, rect.height);
                return;
            }

            var spriteAspect = sprite.rect.width / sprite.rect.height;
            var rectAspect = rect.width / rect.height;

            float width;
            float height;
            if (spriteAspect > rectAspect)
            {
                width = rect.width;
                height = width / spriteAspect;
            }
            else
            {
                height = rect.height;
                width = height * spriteAspect;
            }

            width = Mathf.Max(8f, width - padding * 2f);
            height = Mathf.Max(8f, height - padding * 2f);
            StretchCenter(target, width, height);
        }

        static void StretchCenter(RectTransform target, float width, float height)
        {
            target.anchorMin = new Vector2(0.5f, 0.5f);
            target.anchorMax = new Vector2(0.5f, 0.5f);
            target.pivot = new Vector2(0.5f, 0.5f);
            target.sizeDelta = new Vector2(width, height);
            target.anchoredPosition = Vector2.zero;
            target.localScale = Vector3.one;
        }
    }
}
