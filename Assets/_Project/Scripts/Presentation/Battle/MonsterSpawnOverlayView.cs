using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>训练场：选择怪物加入敌方队伍。</summary>
    public sealed class MonsterSpawnOverlayView : MonoBehaviour
    {
        RectTransform _panel;
        RectTransform _content;
        Text _title;
        Action<TrainingMonsterCatalog.Entry> _onSelect;
        readonly List<GameObject> _rows = new();

        public bool IsOpen => gameObject.activeSelf;

        public void Initialize(Transform parent)
        {
            if (_panel != null)
                return;

            transform.SetParent(parent, false);
            var root = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(transform, false);
            var backdropRt = backdrop.GetComponent<RectTransform>();
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;
            var backdropImg = backdrop.GetComponent<Image>();
            backdropImg.color = new Color(0f, 0f, 0f, 0.55f);
            backdrop.GetComponent<Button>().onClick.AddListener(Hide);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(transform, false);
            _panel = panelGo.GetComponent<RectTransform>();
            _panel.anchorMin = new Vector2(0.62f, 0.08f);
            _panel.anchorMax = new Vector2(0.98f, 0.92f);
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.14f, 0.97f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(_panel, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.04f, 0.92f);
            titleRt.anchorMax = new Vector2(0.96f, 0.99f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _title = titleGo.GetComponent<Text>();
            _title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _title.fontSize = 18;
            _title.fontStyle = FontStyle.Bold;
            _title.alignment = TextAnchor.MiddleLeft;
            _title.color = new Color(1f, 0.92f, 0.78f, 1f);
            _title.text = "测试怪物 — 点击加入敌方";

            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(_panel, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.03f, 0.03f);
            scrollRt.anchorMax = new Vector2(0.97f, 0.90f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0.06f, 0.06f, 0.09f, 1f);

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.spacing = 4f;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = _content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            gameObject.SetActive(false);
        }

        public void Show(Action<TrainingMonsterCatalog.Entry> onSelect)
        {
            _onSelect = onSelect;
            Rebuild();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        public void Toggle(Action<TrainingMonsterCatalog.Entry> onSelect)
        {
            if (IsOpen)
                Hide();
            else
                Show(onSelect);
        }

        void Rebuild()
        {
            foreach (var row in _rows)
            {
                if (row != null)
                    Destroy(row);
            }
            _rows.Clear();

            var entries = TrainingMonsterCatalog.BuildEntries();
            string lastCat = null;
            foreach (var entry in entries)
            {
                if (entry?.Template == null)
                    continue;

                if (entry.Category != lastCat)
                {
                    lastCat = entry.Category;
                    _rows.Add(CreateHeader(lastCat));
                }

                _rows.Add(CreateRow(entry));
            }
        }

        GameObject CreateHeader(string label)
        {
            var go = new GameObject("Header_" + label, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            go.transform.SetParent(_content, false);
            go.GetComponent<LayoutElement>().preferredHeight = 28f;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.75f, 0.85f, 1f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            text.text = "· " + label;
            return go;
        }

        GameObject CreateRow(TrainingMonsterCatalog.Entry entry)
        {
            var go = new GameObject("Row_" + entry.CharacterId, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
            go.transform.SetParent(_content, false);
            go.GetComponent<LayoutElement>().preferredHeight = 36f;
            var img = go.GetComponent<Image>();
            img.color = new Color(0.18f, 0.2f, 0.26f, 1f);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var captured = entry;
            btn.onClick.AddListener(() => _onSelect?.Invoke(captured));

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(10f, 0f);
            labelRt.offsetMax = new Vector2(-8f, 0f);
            var text = labelGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 15;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            var t = entry.Template;
            text.text = $"{entry.DisplayName}  HP{t.MaxHp} SPD{t.Speed}";
            return go;
        }
    }
}
