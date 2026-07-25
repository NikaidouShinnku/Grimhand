using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>规划阶段出牌/空过：button1/button2 底板 + 居中文字。</summary>
    public static class PlanningActionButtonStyle
    {
        public const float DefaultWidth = 168f;
        public const float PlateAspect = 512f / 292f;
        const int LabelFontSize = 22;

        public static float HeightForWidth(float width) => width / PlateAspect;

        public static void Apply(Button button, Sprite plate, string label, float width = DefaultWidth)
        {
            if (button == null)
                return;

            var root = button.transform;
            var horizontal = root.GetComponent<HorizontalLayoutGroup>();
            if (horizontal != null)
                Object.Destroy(horizontal);
            var vertical = root.GetComponent<VerticalLayoutGroup>();
            if (vertical != null)
                Object.Destroy(vertical);

            var iconTr = root.Find("Icon");
            if (iconTr != null)
                iconTr.gameObject.SetActive(false);

            var bg = button.GetComponent<Image>();
            if (bg == null)
                bg = button.gameObject.AddComponent<Image>();
            bg.sprite = plate;
            bg.type = Image.Type.Simple;
            bg.preserveAspect = false;
            bg.raycastTarget = true;
            bg.color = plate != null ? Color.white : new Color(0.18f, 0.16f, 0.22f, 0.96f);

            var height = HeightForWidth(width);
            var rt = button.transform as RectTransform;
            if (rt != null)
                rt.sizeDelta = new Vector2(width, height);

            var le = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            EnsureLabel(root, label);
            button.targetGraphic = bg;
            BattleButtonPressFeedback.Apply(button);
        }

        static void EnsureLabel(Transform root, string label)
        {
            var labelTr = root.Find("Label");
            if (labelTr == null)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(root, false);
                labelTr = go.transform;
            }

            labelTr.SetAsLastSibling();
            var labelRt = labelTr as RectTransform;
            if (labelRt != null)
            {
                labelRt.anchorMin = Vector2.zero;
                labelRt.anchorMax = Vector2.one;
                labelRt.offsetMin = new Vector2(8f, 6f);
                labelRt.offsetMax = new Vector2(-8f, -8f);
            }

            var le = labelTr.GetComponent<LayoutElement>();
            if (le != null)
                Object.Destroy(le);

            var text = labelTr.GetComponent<Text>();
            if (text == null)
                return;

            text.raycastTarget = false;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = label ?? "";
            text.fontSize = LabelFontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.92f, 0.78f, 1f);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
        }
    }
}
