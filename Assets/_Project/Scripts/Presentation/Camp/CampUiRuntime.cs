using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    internal static class CampUiRuntime
    {
        internal static Font DefaultFont =>
            Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        internal static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = CreateRect(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        internal static Text CreateText(
            Transform parent,
            string text,
            int size,
            FontStyle style,
            TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var go = CreateRect("Text", parent);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.font = DefaultFont;
            t.color = Color.white;
            t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        internal static Button CreateButton(
            Transform parent,
            string label,
            Color bg,
            Vector2 size)
        {
            var img = CreateImage(label + "Button", parent, bg);
            var rt = img.rectTransform;
            rt.sizeDelta = size;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var text = CreateText(img.transform, label, 16, FontStyle.Bold);
            Stretch(text.rectTransform, 8f, 6f, -8f, -6f);
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        internal static void Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        internal static void StretchFull(RectTransform rt)
        {
            Stretch(rt, 0f, 0f, 0f, 0f);
        }

        internal static void SetAnchored(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
