using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>卡包三选一：点击一张加入卡组，或放弃。</summary>
    public sealed class CardPackPickOverlayView : MonoBehaviour
    {
        const float CardScale = 1.12f;

        BattleSession _session;
        BattleUiIconCatalogSO _uiIcons;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        Text _headerText;
        Text _hintText;
        RectTransform _choiceRow;
        Button _skipButton;
        InventoryTooltipView _tooltip;
        bool _built;
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

            _headerText.text = CardPackIds.GetDisplayName(packOffer.PackId);
            _hintText.text = "选择一张卡牌加入卡组，或放弃本卡包。";

            for (var i = 0; i < packOffer.Choices.Count; i++)
                AddChoiceCard(packOffer.Choices[i], i);
        }

        void AddChoiceCard(CardPackChoice choice, int choiceIndex)
        {
            if (choice?.Template == null || _cardPrefab == null)
                return;

            var holder = new GameObject($"Choice_{choiceIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(_choiceRow, false);
            var rt = holder.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(280f, 420f);
            holder.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.92f);

            var cardGo = Instantiate(_cardPrefab.gameObject, holder.transform);
            var cardRt = cardGo.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.5f, 0.5f);
            cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = new Vector2(0f, 8f);
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

            var btn = holder.GetComponent<Button>();
            btn.targetGraphic = holder.GetComponent<Image>();
            btn.interactable = false;
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
            if (_built)
                return;

            _built = true;
            var go = new GameObject("CardPackPickOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.68f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.16f, 0.18f);
            panelRt.anchorMax = new Vector2(0.84f, 0.82f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt);

            _headerText = CreateText(panelGo.transform, "卡包", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(_headerText.rectTransform, 0.90f, 0.97f);
            _headerText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _hintText = CreateText(panelGo.transform, "", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(_hintText.rectTransform, 0.82f, 0.89f);

            var rowGo = new GameObject("Choices", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            rowGo.transform.SetParent(panelGo.transform, false);
            _choiceRow = rowGo.GetComponent<RectTransform>();
            AnchorBand(_choiceRow, 0.10f, 0.80f);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 32f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            _skipButton = CreateButton(
                panelGo.transform,
                "放弃卡包",
                new Vector2(0.5f, 0.06f),
                new Vector2(240f, 52f),
                () => _session.SkipCardPack());

            go.SetActive(false);
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
            return label;
        }

        static Button CreateButton(Transform parent, string label, Vector2 anchorY, Vector2 size, System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, anchorY.y);
            rt.anchorMax = new Vector2(0.5f, anchorY.y);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.24f, 0.26f, 0.32f, 1f);

            var text = CreateText(go.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);

            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());
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
            rt.anchorMin = new Vector2(0.04f, yMin);
            rt.anchorMax = new Vector2(0.96f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
