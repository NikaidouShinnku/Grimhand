using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Events;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>局内事件：event_plate 大框 + event_option_plate 选项。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionNodeInteractOverlayView : MonoBehaviour
    {
        const int LayoutVersion = 3;
        const float PanelWidth = 720f;
        const float PanelHeight = 780f;
        // event_option_plate 原生 2026×384
        const float OptionAspect = 2026f / 384f;
        const float OptionWidth = 580f;
        static float OptionHeight => OptionWidth / OptionAspect;

        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color BodyText = new(0.90f, 0.92f, 0.96f, 1f);
        static readonly Color OptionText = new(0.96f, 0.92f, 0.78f, 1f);

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        RectTransform _root;
        Image _panelImage;
        Text _titleText;
        Text _bodyText;
        RectTransform _choiceRow;
        bool _built;
        int _builtVersion = -1;

        public void Initialize(BattleSession session, Transform parent, BattleUiIconCatalogSO icons = null)
        {
            _session = session;
            _icons = icons;
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
                or ExpeditionPhase.EventAftermath;

            SetVisible(show);
            if (!show)
                return;

            _root.SetAsLastSibling();
            ApplyPanelBackground();
            ClearChoices();

            switch (phase)
            {
                case ExpeditionPhase.EventChoice:
                    RefreshEvent();
                    break;
                case ExpeditionPhase.EventAftermath:
                    RefreshEventAftermath();
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
                    : $"{choice.Label} / {choice.Description}";
                var canAfford = choice.RequiredGold <= 0
                    || _session.Expedition.Run.Gold >= choice.RequiredGold;
                AddChoiceButton(label, () => _session.ResolveEventChoice(index), canAfford);
            }
        }

        void RefreshEventAftermath()
        {
            var pending = _session.Expedition.Run.PendingEventAftermath;
            if (pending == null || !ExpeditionEventCatalog.TryGet(pending.EventId, out var evt))
            {
                _titleText.text = "事件结果";
                _bodyText.text = "……";
                AddChoiceButton("确定", () => _session.ConfirmEventAftermath());
                return;
            }

            _titleText.text = evt.DisplayName;
            _bodyText.text = pending.AfterChoiceText;
            AddChoiceButton("确定", () => _session.ConfirmEventAftermath());
        }

        void AddChoiceButton(string label, System.Action onClick, bool interactable = true)
        {
            var go = new GameObject("Choice", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(_choiceRow, false);
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
                img.color = interactable
                    ? Color.white
                    : new Color(0.45f, 0.45f, 0.48f, 1f);
            }
            else
            {
                img.sprite = null;
                img.color = interactable
                    ? new Color(0.22f, 0.20f, 0.18f, 0.96f)
                    : new Color(0.12f, 0.12f, 0.14f, 0.75f);
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
            text.color = interactable
                ? OptionText
                : new Color(0.55f, 0.55f, 0.58f, 1f);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.text = label;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.interactable = interactable;
            if (interactable)
            {
                btn.onClick.AddListener(() => onClick?.Invoke());
                BattleButtonPressFeedback.Apply(btn);
                UiAudioHooks.WireButton(btn);
            }
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
            if (_built && _builtVersion == LayoutVersion && _root != null)
            {
                ApplyPanelBackground();
                return;
            }

            if (_root != null)
                Destroy(_root.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;

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
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panelImage = panelGo.GetComponent<Image>();
            ApplyPanelBackground();

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.08f, 0.88f);
            titleRt.anchorMax = new Vector2(0.92f, 0.96f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            _titleText = titleGo.GetComponent<Text>();
            _titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _titleText.fontSize = 30;
            _titleText.fontStyle = FontStyle.Bold;
            _titleText.alignment = TextAnchor.MiddleCenter;
            _titleText.color = TitleGold;
            _titleText.raycastTarget = false;

            var bodyGo = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyGo.transform.SetParent(panelGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0.10f, 0.52f);
            bodyRt.anchorMax = new Vector2(0.90f, 0.84f);
            bodyRt.offsetMin = Vector2.zero;
            bodyRt.offsetMax = Vector2.zero;
            _bodyText = bodyGo.GetComponent<Text>();
            _bodyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _bodyText.fontSize = 20;
            _bodyText.alignment = TextAnchor.UpperLeft;
            _bodyText.color = BodyText;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            _bodyText.raycastTarget = false;

            var rowGo = new GameObject("Choices", typeof(RectTransform), typeof(VerticalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            _choiceRow = rowGo.GetComponent<RectTransform>();
            _choiceRow.anchorMin = new Vector2(0.08f, 0.13f);
            _choiceRow.anchorMax = new Vector2(0.92f, 0.49f);
            _choiceRow.offsetMin = Vector2.zero;
            _choiceRow.offsetMax = Vector2.zero;
            var layout = rowGo.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.padding = new RectOffset(0, 0, 4, 8);

            _root.gameObject.SetActive(false);
            _root.SetAsLastSibling();
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
    }
}
