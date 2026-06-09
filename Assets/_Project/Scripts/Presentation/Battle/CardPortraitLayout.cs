using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>卡面预绘立绘（card_profile_*）覆盖上半椭圆区域，无需 Mask。</summary>
    public static class CardPortraitLayout
    {
        public const float CardWidth = 168f;
        public const float CardHeight = 236f;

        // 与 common_card_* 白环椭圆严丝合缝（128×128 贴图实测，勿用内棕区）
        public static readonly Vector2 ProfileAnchorMin = new(0.2266f, 0.4531f);
        public static readonly Vector2 ProfileAnchorMax = new(0.7656f, 0.8984f);
        /// <summary>整体上移（归一化卡高），贴齐框顶。</summary>
        const float ProfileVerticalNudge = 0.017f;

        public static void ApplyProfileOverlay(Image artImage, RectTransform cardRoot, Sprite sprite)
        {
            if (artImage == null || cardRoot == null)
                return;

            var artRt = artImage.rectTransform;
            if (artRt.parent != cardRoot)
                artRt.SetParent(cardRoot, false);

            artRt.localRotation = Quaternion.identity;
            artRt.localScale = Vector3.one;
            artRt.anchorMin = new Vector2(ProfileAnchorMin.x, ProfileAnchorMin.y + ProfileVerticalNudge);
            artRt.anchorMax = new Vector2(ProfileAnchorMax.x, ProfileAnchorMax.y + ProfileVerticalNudge);
            artRt.offsetMin = Vector2.zero;
            artRt.offsetMax = Vector2.zero;
            artRt.pivot = new Vector2(0.5f, 0.5f);
            artRt.anchoredPosition = Vector2.zero;

            artImage.sprite = sprite;
            artImage.type = Image.Type.Simple;
            artImage.preserveAspect = false;
            artImage.raycastTarget = false;
            artImage.color = sprite != null ? Color.white : new Color(0.25f, 0.27f, 0.35f, 1f);
        }
    }
}
