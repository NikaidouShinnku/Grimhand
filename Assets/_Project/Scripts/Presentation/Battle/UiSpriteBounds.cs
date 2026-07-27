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

        /// <summary>
        /// 点击判定框：先按立绘可视区域 letterbox，再收窄到身体宽度，避免邻槽误点。
        /// </summary>
        public static void FitJudgmentBox(
            RectTransform container,
            RectTransform target,
            Sprite sprite,
            float widthScale,
            float heightScale,
            float maxWidthRatioOfContainer,
            float padding = 0f)
        {
            if (container == null || target == null)
                return;

            FitCentered(container, target, sprite, padding);

            var size = target.sizeDelta;
            var containerWidth = Mathf.Max(1f, container.rect.width);
            size.x = Mathf.Max(20f, size.x * Mathf.Clamp01(widthScale));
            size.y = Mathf.Max(32f, size.y * Mathf.Clamp01(heightScale));
            size.x = Mathf.Min(size.x, containerWidth * Mathf.Clamp(maxWidthRatioOfContainer, 0.08f, 1f));
            StretchCenter(target, size.x, size.y);
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
