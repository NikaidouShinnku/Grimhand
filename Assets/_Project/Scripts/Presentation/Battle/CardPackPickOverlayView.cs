using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>卡包三选一：event_plate 底板 + 三张卡直接摆放 + button6 放弃。</summary>
    public sealed class CardPackPickOverlayView : MonoBehaviour
    {
        const int LayoutVersion = 4;
        const float CardScale = 1.12f;
        const float SkipButtonWidth = 280f;
        const float Button6Aspect = 512f / 216f;
        static readonly Color HeaderGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color ButtonLabel = new(0.96f, 0.92f, 0.78f, 1f);

        BattleSession _session;
        BattleUiIconCatalogSO _uiIcons;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        Image _panelImage;
        Text _headerText;
        Text _hintText;
        RectTransform _choiceRow;
        Button _skipButton;
        InventoryTooltipView _tooltip;
        bool _built;
        int _builtVersion = -1;
        readonly List<GameObject> _dynamicObjects = new();

        public void Initialize(
            BattleSession session,
            Transform parent,
            BattleUiIconCatalogSO uiIcons,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _uiIcons = uiIcons;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var packOffer = _session.Expedition.Run.PendingCardPackOffer;
            if (packOffer != null)
            {
                _session.Expedition.ReconcileAfterResume();
                packOffer = _session.Expedition.Run.PendingCardPackOffer;
            }

            if (packOffer == null || packOffer.Choices.Count == 0 || _session.Expedition.Run.PendingCardOffer != null)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _root.SetAsLastSibling();
            _tooltip?.Hide();
            ClearDynamic();
            ApplyPanelBackground();

            _headerText.text = CardPackIds.GetDisplayName(packOffer.PackId);
            _hintText.text = "选择一张卡牌加入卡组，或放弃本卡包。";

            for (var i = 0; i < packOffer.Choices.Count; i++)
                AddChoiceCard(packOffer.Choices[i], i);
        }

        void AddChoiceCard(CardPackChoice choice, int choiceIndex)
        {
            if (choice?.Template == null || _cardPrefab == null)
                return;

            var holder = new GameObject($"Choice_{choiceIndex}", typeof(RectTransform));
            holder.transform.SetParent(_choiceRow, false);
            var rt = holder.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 320f);

            var cardGo = Instantiate(_cardPrefab.gameObject, holder.transform);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.localScale = Vector3.one * CardScale;

            _definitions.TryGetValue(choice.Template.DefinitionId, out var definition);
            var preview = CardVisualResolver.CreatePreviewInstance(
                choice.Template.DefinitionId,
                choice.OwnerCharacterId,
                choice.Template.DisplayName,
                definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);

            var cardView = cardGo.GetComponent<CardView>();
            CardView.ConfigureForRewardPresentation(cardView, CardScale);
            cardView.BindWithCard(
                preview,
                visual,
                selected: false,
                polluted: false,
                interactable: true,
                orderBadge: "",
                statsLine: statsLine,
                uiIcons: _uiIcons,
                characterVisuals: _characterVisuals,
                onClick: _ => _session.PickCardFromPack(choiceIndex),
                onHoverEnter: null,
                onHoverExit: null);

            _dynamicObjects.Add(holder);
        }

        void ClearDynamic()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);

            if (!visible)
                _tooltip?.Hide();
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
            _dynamicObjects.Clear();

            var go = new GameObject("CardPackPickOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.14f, 0.16f);
            panelRt.anchorMax = new Vector2(0.86f, 0.86f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            _panelImage = panelGo.GetComponent<Image>();
            ApplyPanelBackground();

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt, _uiIcons);

            _headerText = CreateText(panelGo.transform, "卡包", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(_headerText.rectTransform, 0.88f, 0.96f);
            _headerText.color = HeaderGold;

            _hintText = CreateText(panelGo.transform, "", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(_hintText.rectTransform, 0.80f, 0.87f);

            var rowGo = new GameObject("Choices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            _choiceRow = rowGo.GetComponent<RectTransform>();
            AnchorBand(_choiceRow, 0.16f, 0.78f);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 28f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            _skipButton = CreateButton6(
                panelGo.transform,
                "放弃卡包",
                new Vector2(0.5f, 0.05f),
                () => _session.SkipCardPack());

            go.SetActive(false);
        }

        void ApplyPanelBackground()
        {
            if (_panelImage == null)
                return;

            _panelImage.preserveAspect = false;
            _panelImage.type = Image.Type.Simple;
            if (_uiIcons != null && _uiIcons.UiEventPlate != null)
            {
                _panelImage.sprite = _uiIcons.UiEventPlate;
                _panelImage.color = Color.white;
                return;
            }

            _panelImage.sprite = null;
            _panelImage.color = new Color(0.1f, 0.11f, 0.15f, 0.98f);
        }

        static Text CreateText(Transform parent, string text, int size, FontStyle style, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = anchor;
            label.color = Color.white;
            label.text = text;
            label.raycastTarget = false;
            return label;
        }

        Button CreateButton6(Transform parent, string label, Vector2 anchorY, System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, anchorY.y);
            rt.anchorMax = new Vector2(0.5f, anchorY.y);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(SkipButtonWidth, SkipButtonWidth / Button6Aspect);

            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton6 != null)
                img.sprite = _uiIcons.UiButton6;
            else
                img.color = new Color(0.24f, 0.26f, 0.32f, 1f);

            var text = CreateText(go.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(16f, 8f);
            text.rectTransform.offsetMax = new Vector2(-16f, -12f);
            text.color = ButtonLabel;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            BattleButtonPressFeedback.Apply(btn);
            UiAudioHooks.WireButton(btn);
            return btn;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AnchorBand(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0.06f, yMin);
            rt.anchorMax = new Vector2(0.94f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
