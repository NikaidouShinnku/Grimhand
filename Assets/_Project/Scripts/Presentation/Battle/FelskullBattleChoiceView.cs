using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>魔焰颅骨：战斗开始二选一（event_plate + event_option_plate）。</summary>
    public sealed class FelskullBattleChoiceView : MonoBehaviour
    {
        const int SortOrder = 500;
        const int LayoutVersion = 4;
        const float PanelWidth = 720f;
        const float PanelHeight = 780f;
        const float OptionAspect = 2026f / 384f;
        const float OptionWidth = 580f;
        static float OptionHeight => OptionWidth / OptionAspect;

        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color BodyText = new(0.90f, 0.92f, 0.96f, 1f);
        static readonly Color OptionText = new(0.96f, 0.92f, 0.78f, 1f);

        GameObject _root;
        Image _panelImage;
        BattleUiIconCatalogSO _icons;
        int _layoutVersion;
        Action<int> _onPick;

        public bool IsVisible => _root != null && _root.activeSelf;

        public void EnsureBuilt(Transform parent, BattleUiIconCatalogSO icons = null)
        {
            if (icons != null)
                _icons = icons;

            if (_root != null && _layoutVersion == LayoutVersion)
            {
                ApplyPanelBackground();
                return;
            }

            if (_root != null)
                Destroy(_root);

            _layoutVersion = LayoutVersion;

            _root = new GameObject(
                "FelskullChoiceOverlay",
                typeof(RectTransform),
                typeof(Image),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            var rt = _root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var dim = _root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.5f);
            dim.raycastTarget = true;

            var canvas = _root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortOrder;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root.transform, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panelImage = panelGo.GetComponent<Image>();
            ApplyPanelBackground();

            CreateBandText(
                panelGo.transform,
                "Title",
                "魔焰颅骨",
                30,
                FontStyle.Bold,
                TitleGold,
                new Vector2(0.08f, 0.88f),
                new Vector2(0.92f, 0.96f));
            CreateBandText(
                panelGo.transform,
                "Subtitle",
                "战斗开始前，你必须选择其一：",
                20,
                FontStyle.Normal,
                BodyText,
                new Vector2(0.10f, 0.72f),
                new Vector2(0.90f, 0.86f));

            var rowGo = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            var choiceRow = rowGo.GetComponent<RectTransform>();
            choiceRow.anchorMin = new Vector2(0.08f, 0.13f);
            choiceRow.anchorMax = new Vector2(0.92f, 0.70f);
            choiceRow.offsetMin = Vector2.zero;
            choiceRow.offsetMax = Vector2.zero;
            var layout = rowGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(0, 0, 4, 8);

            CreateChoiceButton(
                choiceRow,
                "A · 血祭换能\n所有我方角色失去 5% HP，本场战斗获得 1 额外能量上限。",
                0);
            CreateChoiceButton(
                choiceRow,
                "B · 抑能增伤\n本场战斗失去 1 点能量上限，所有我方角色获得 10% 增伤（永久）。",
                1);

            _root.SetActive(false);
        }

        void CreateChoiceButton(Transform parent, string label, int choiceIndex)
        {
            var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(OptionWidth, OptionHeight);

            var le = go.GetComponent<LayoutElement>();
            le.preferredWidth = OptionWidth;
            le.preferredHeight = OptionHeight;
            le.minHeight = OptionHeight;
            le.flexibleWidth = 0f;

            var img = go.GetComponent<Image>();
            img.preserveAspect = false;
            img.type = Image.Type.Simple;
            if (_icons != null && _icons.UiEventOptionPlate != null)
            {
                img.sprite = _icons.UiEventOptionPlate;
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = new Color(0.22f, 0.20f, 0.18f, 0.96f);
            }

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(28f, 10f);
            textRt.offsetMax = new Vector2(-28f, -12f);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 18;
            text.fontStyle = FontStyle.Bold;
            text.lineSpacing = 1.05f;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = OptionText;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = label;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => _onPick?.Invoke(choiceIndex));
            BattleButtonPressFeedback.Apply(btn);
            UiAudioHooks.WireButton(btn);
        }

        static void CreateBandText(
            Transform parent,
            string objectName,
            string content,
            int fontSize,
            FontStyle style,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
        }

        void ApplyPanelBackground()
        {
            if (_panelImage == null)
                return;

            _panelImage.preserveAspect = false;
            _panelImage.type = Image.Type.Simple;
            if (_icons != null && _icons.UiEventPlate != null)
            {
                _panelImage.sprite = _icons.UiEventPlate;
                _panelImage.color = Color.white;
                return;
            }

            _panelImage.sprite = null;
            _panelImage.color = new Color(0.1f, 0.11f, 0.15f, 0.98f);
        }

        public void Show(Action<int> onPick)
        {
            _onPick = onPick;
            if (_root != null)
            {
                ApplyPanelBackground();
                _root.SetActive(true);
                _root.transform.SetAsLastSibling();
            }
        }

        public void Hide()
        {
            _onPick = null;
            if (_root != null)
                _root.SetActive(false);
        }
    }
}
