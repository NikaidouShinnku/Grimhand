using System;
using Grimhand.Battle.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>魔焰颅骨：战斗开始二选一（基础 UI）。</summary>
    public sealed class FelskullBattleChoiceView : MonoBehaviour
    {
        const int SortOrder = 500;

        GameObject _root;
        Action<int> _onPick;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void EnsureBuilt(Transform parent)
        {
            if (_root != null)
                return;

            _root = new GameObject("FelskullChoiceOverlay", typeof(RectTransform), typeof(Image), typeof(Canvas), typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _root.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            _root.GetComponent<Image>().raycastTarget = true;

            var canvas = _root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortOrder;

            var panel = CreatePanel(_root.transform, new Vector2(720f, 480f));
            CreateStretchText(
                panel,
                "Title",
                "魔焰颅骨",
                28,
                TextAnchor.UpperCenter,
                new Vector2(24f, -20f),
                new Vector2(-24f, -64f));
            CreateStretchText(
                panel,
                "Subtitle",
                "战斗开始前，你必须选择其一：",
                18,
                TextAnchor.UpperCenter,
                new Vector2(32f, -72f),
                new Vector2(-32f, -108f));

            CreateChoiceButton(
                panel,
                "A · 血祭换能",
                "所有我方角色失去 5% HP，本场战斗获得 1 额外能量上限。",
                new Vector2(0f, 72f),
                0);
            CreateChoiceButton(
                panel,
                "B · 抑能狂怒",
                "本场战斗失去 1 点能量上限，所有我方角色攻击牌增加 10% 伤害。",
                new Vector2(0f, -88f),
                1);

            _root.SetActive(false);
        }

        void CreateChoiceButton(Transform parent, string title, string body, Vector2 anchoredY, int choiceIndex)
        {
            var btnGo = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(640f, 128f);
            rt.anchoredPosition = anchoredY;
            btnGo.GetComponent<Image>().color = new Color(0.12f, 0.08f, 0.14f, 0.96f);

            CreateStretchText(
                btnGo.transform,
                "Title",
                title,
                20,
                TextAnchor.UpperLeft,
                new Vector2(20f, -12f),
                new Vector2(-20f, -44f));
            CreateStretchText(
                btnGo.transform,
                "Body",
                body,
                15,
                TextAnchor.UpperLeft,
                new Vector2(20f, -48f),
                new Vector2(-20f, -16f));

            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(() => _onPick?.Invoke(choiceIndex));
        }

        static Transform CreatePanel(Transform parent, Vector2 size)
        {
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.1f, 0.98f);
            return panel.transform;
        }

        static Text CreateStretchText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            TextAnchor anchor,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public void Show(Action<int> onPick)
        {
            _onPick = onPick;
            if (_root != null)
                _root.SetActive(true);
        }

        public void Hide()
        {
            _onPick = null;
            if (_root != null)
                _root.SetActive(false);
        }
    }
}
