using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>information plate 内文字测量：优先加高，少加宽。</summary>
    public static class UiInfoPlateMetrics
    {
        public const float PadX = 34f;
        public const float PadY = 34f;
        public const float MinWidth = 240f;
        public const float MaxWidth = 400f;
        public const float HeightFudge = 24f;

        public static float InnerWidth(float panelWidth) =>
            Mathf.Max(40f, panelWidth - PadX * 2f);

        public static float MeasureHeight(Text text, string content, float innerWidth)
        {
            if (text == null || string.IsNullOrEmpty(content) || innerWidth <= 1f)
                return 0f;

            var prevH = text.horizontalOverflow;
            var prevV = text.verticalOverflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            var settings = text.GetGenerationSettings(new Vector2(innerWidth, 0f));
            settings.generationExtents = new Vector2(innerWidth, 0f);
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.richText = text.supportRichText;
            settings.scaleFactor = 1f;
            settings.fontSize = text.resizeTextForBestFit ? text.resizeTextMaxSize : text.fontSize;
            settings.fontStyle = text.fontStyle;
            settings.updateBounds = true;

            var generator = text.cachedTextGeneratorForLayout;
            var raw = generator.GetPreferredHeight(content, settings) / Mathf.Max(1f, text.pixelsPerUnit);

            // 再交叉验证 preferredHeight（部分富文本场景更准）
            var rt = text.rectTransform;
            var oldSize = rt.sizeDelta;
            var oldAnchorMin = rt.anchorMin;
            var oldAnchorMax = rt.anchorMax;
            var oldPivot = rt.pivot;
            var oldPos = rt.anchoredPosition;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(innerWidth, 0f);
            Canvas.ForceUpdateCanvases();
            var preferred = text.preferredHeight;
            rt.anchorMin = oldAnchorMin;
            rt.anchorMax = oldAnchorMax;
            rt.pivot = oldPivot;
            rt.anchoredPosition = oldPos;
            rt.sizeDelta = oldSize;

            text.horizontalOverflow = prevH;
            text.verticalOverflow = prevV;

            var h = Mathf.Max(raw, preferred, text.fontSize + 4f);
            return h + HeightFudge;
        }

        public static float MeasureUnwrappedWidth(Text text, string content, float maxInner)
        {
            if (text == null || string.IsNullOrEmpty(content))
                return 0f;

            var prev = text.horizontalOverflow;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            Canvas.ForceUpdateCanvases();
            var w = text.preferredWidth;
            text.horizontalOverflow = prev;
            return Mathf.Clamp(w, 0f, maxInner);
        }

        public static Vector2 FitPanelSize(float contentWidth, float contentHeight)
        {
            var w = Mathf.Clamp(contentWidth + PadX * 2f, MinWidth, MaxWidth);
            var h = Mathf.Max(72f, contentHeight + PadY * 2f);
            return new Vector2(w, h);
        }

        public static void ApplyTextInsets(RectTransform textRt)
        {
            if (textRt == null)
                return;

            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = Vector2.zero;
            textRt.offsetMin = new Vector2(PadX, PadY);
            textRt.offsetMax = new Vector2(-PadX, -PadY);
            textRt.localScale = Vector3.one;
        }
    }
}
