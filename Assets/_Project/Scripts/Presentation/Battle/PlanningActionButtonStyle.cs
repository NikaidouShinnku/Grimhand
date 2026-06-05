using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>规划阶段 Icon 按钮：Icon 即点击区，文字仅标注、不参与判定。</summary>
    public static class PlanningActionButtonStyle
    {
        const int IconSize = 96;
        const int LabelFontSize = 16;
        const float AlphaHitThreshold = 0.25f;

        public static void Apply(Button button, Sprite icon, string label)
        {
            if (button == null)
                return;

            var root = button.transform;
            EnsureButtonBackground(button);
            EnsureVerticalLayout(root);
            EnsureLabel(root, label);
            var iconImage = EnsureIcon(root, icon);
            button.targetGraphic = iconImage;

            var le = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = IconSize;
            le.preferredHeight = IconSize + 24f;
            le.minWidth = IconSize;
            le.minHeight = IconSize;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
        }

        static void EnsureButtonBackground(Button button)
        {
            var bg = button.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = Color.clear;
                bg.raycastTarget = false;
            }
        }

        static void EnsureVerticalLayout(Transform root)
        {
            var horizontal = root.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
                Object.Destroy(horizontal);

            var layout = root.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = root.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.spacing = 4;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        static Image EnsureIcon(Transform root, Sprite icon)
        {
            var iconTr = root.Find("Icon");
            if (iconTr == null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(root, false);
                iconTr = iconGo.transform;
            }

            iconTr.SetAsLastSibling();

            var img = iconTr.GetComponent<Image>();
            img.sprite = icon;
            img.enabled = icon != null;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = true;
            img.type = Image.Type.Simple;
            img.alphaHitTestMinimumThreshold = AlphaHitThreshold;

            var le = img.GetComponent<LayoutElement>() ?? img.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = IconSize;
            le.preferredHeight = IconSize;
            le.minWidth = IconSize;
            le.minHeight = IconSize;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(IconSize, IconSize);
            return img;
        }

        static void EnsureLabel(Transform root, string label)
        {
            var labelTr = root.Find("Label");
            if (labelTr == null)
                return;

            labelTr.SetAsFirstSibling();

            var labelRt = labelTr as RectTransform;
            if (labelRt != null)
            {
                labelRt.anchorMin = new Vector2(0.5f, 0.5f);
                labelRt.anchorMax = new Vector2(0.5f, 0.5f);
                labelRt.offsetMin = Vector2.zero;
                labelRt.offsetMax = Vector2.zero;
            }

            var le = labelTr.GetComponent<LayoutElement>() ?? labelTr.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth = 0f;
            le.minWidth = 0f;
            le.preferredHeight = 20f;

            var text = labelTr.GetComponent<Text>();
            if (text == null)
                return;

            text.raycastTarget = false;

            if (string.IsNullOrEmpty(label))
                return;

            text.text = label;
            text.fontSize = LabelFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
