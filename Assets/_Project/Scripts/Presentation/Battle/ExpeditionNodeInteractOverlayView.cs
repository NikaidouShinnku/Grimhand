using Grimhand.Expedition;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    [DisallowMultipleComponent]
    public sealed class ExpeditionNodeInteractOverlayView : MonoBehaviour
    {
        BattleSession _session;
        RectTransform _root;
        Text _titleText;
        Text _bodyText;
        RectTransform _choiceRow;
        bool _built;

        public void Initialize(BattleSession session, Transform parent)
        {
            _session = session;
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var phase = _session.Expedition.Run.Phase;
            var show = phase is ExpeditionPhase.EventChoice
                or ExpeditionPhase.ShrineChoice;

            SetVisible(show);
            if (!show)
                return;

            _root.SetAsLastSibling();
            ClearChoices();

            switch (phase)
            {
                case ExpeditionPhase.EventChoice:
                    RefreshEvent();
                    break;
                case ExpeditionPhase.ShrineChoice:
                    RefreshShrine();
                    break;
            }
        }

        void RefreshEvent()
        {
            var pending = _session.Expedition.Run.PendingEvent;
            if (pending == null || !ExpeditionEventCatalog.TryGet(pending.EventId, out var evt))
            {
                _titleText.text = "特殊事件";
                _bodyText.text = "……";
                return;
            }

            _titleText.text = evt.DisplayName;
            _bodyText.text = evt.SceneText;

            for (var i = 0; i < evt.Choices.Count; i++)
            {
                var index = i;
                var choice = evt.Choices[i];
                var label = string.IsNullOrEmpty(choice.Description)
                    ? choice.Label
                    : $"{choice.Label}\n{choice.Description}";
                AddChoiceButton(label, () => _session.ResolveEventChoice(index));
            }
        }

        void RefreshShrine()
        {
            var pending = _session.Expedition.Run.PendingShrine;
            if (pending == null || !ExpeditionShrineCatalog.TryGet(pending.ShrineId, out var shrine))
            {
                _titleText.text = "祭坛";
                _bodyText.text = "献祭换取奖励，或安全离开。";
                AddChoiceButton("离开", () => _session.ResolveShrineChoice(0));
                return;
            }

            _titleText.text = shrine.DisplayName;
            _bodyText.text = shrine.SceneText;

            for (var i = 0; i < shrine.Choices.Count; i++)
            {
                var index = i;
                var choice = shrine.Choices[i];
                var label = string.IsNullOrEmpty(choice.Label)
                    ? choice.Description
                    : $"{choice.Label}) {choice.Description}";
                AddChoiceButton(label, () => _session.ResolveShrineChoice(index));
            }
        }

        void AddChoiceButton(string label, System.Action onClick)
        {
            var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_choiceRow, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 72f);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = 96f;
            le.minHeight = 72f;
            le.flexibleWidth = 1f;
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.95f);

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(12f, 6f);
            textRt.offsetMax = new Vector2(-12f, -6f);
            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 17;
            text.lineSpacing = 1.05f;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.text = label;

            go.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
        }

        void ClearChoices()
        {
            if (_choiceRow == null)
                return;

            foreach (Transform child in _choiceRow)
                Destroy(child.gameObject);
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built)
                return;

            _built = true;

            var go = new GameObject("ExpeditionNodeInteractOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(760f, 520f);
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 48f);
            titleRt.anchoredPosition = new Vector2(0f, -8f);
            _titleText = titleGo.GetComponent<Text>();
            _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.fontSize = 28;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.06f, 0.42f);
            bodyRt.anchorMax = new Vector2(0.94f, 0.82f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 20;
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.color = new Color(0.9f, 0.92f, 0.96f, 1f);

            var rowGo = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            _choiceRow = rowGo.GetComponent<RectTransform>();
            _choiceRow.anchorMin = new Vector2(0.08f, 0.06f);
            _choiceRow.anchorMax = new Vector2(0.92f, 0.38f);
            _choiceRow.offsetMin = Vector2.zero;
            _choiceRow.offsetMax = Vector2.zero;
            var layout = rowGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;

            _root.gameObject.SetActive(false);
            _root.SetAsLastSibling();
        }
    }
}
