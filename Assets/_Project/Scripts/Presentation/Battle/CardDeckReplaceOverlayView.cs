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
    public sealed class CardDeckReplaceOverlayView : MonoBehaviour
    {
        const float CardBaseWidth = CardPortraitLayout.CardWidth;
        const float CardBaseHeight = CardPortraitLayout.CardHeight;
        const float OfferCardScale = 0.98f;
        const float DeckCardScale = 0.78f;
        const float DeckGap = 12f;
        const int DeckColumns = 5;
        const int LayoutVersion = 6;
        const float ConfirmButtonWidth = 200f;
        const float AbandonButtonWidth = 200f;

        BattleSession _session;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        Image _panelImage;
        Text _headerText;
        Text _offerJoinLabel;
        Text _deckLabel;
        RectTransform _offerCardAnchor;
        RectTransform _deckHost;
        Button _confirmButton;
        Button _abandonButton;
        InventoryTooltipView _tooltip;
        bool _built;
        int _layoutVersion;
        string _selectedDeckKey = "";
        readonly List<GameObject> _dynamicObjects = new();
        readonly Dictionary<string, CardView> _deckCardViews = new();

        public void Initialize(
            BattleSession session,
            Transform parent,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
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

            var offer = _session.Expedition.Run.PendingCardOffer;
            if (offer?.Template == null
                || offer.Context == ExpeditionCardOfferContext.Altar
                || _session.Expedition.Run.Phase == ExpeditionPhase.EventInteraction)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            _root.SetAsLastSibling();
            _tooltip?.Hide();
            ClearDynamic();
            _selectedDeckKey = "";
            RefreshConfirmInteractable();

            PartyMemberSnapshot targetMember = null;
            foreach (var member in _session.Expedition.Run.Party)
            {
                if (member.CharacterDefinitionId != offer.OwnerCharacterId)
                    continue;

                targetMember = member;
                break;
            }

            if (targetMember == null && _session.Expedition.Run.Party.Count > 0)
                targetMember = _session.Expedition.Run.Party[0];

            _headerText.text = "卡组已满 — 选择替换";

            SpawnOfferCardPreview(offer.Template, offer.OwnerCharacterId);

            if (targetMember == null || _cardPrefab == null || _deckHost == null)
                return;

            var config = _session.Expedition.Config;
            var entries = ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, targetMember);
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry?.Template == null)
                    continue;
                AddDeckCardButton(entry.Template, entry.Key, targetMember.CharacterDefinitionId, i);
            }
        }

        void SpawnOfferCardPreview(CardTemplate template, string ownerCharacterId)
        {
            if (_offerCardAnchor == null || _cardPrefab == null || template == null)
                return;

            if (_offerJoinLabel != null)
                _offerJoinLabel.gameObject.SetActive(true);

            var holder = CreatePanelImage("OfferCard", _offerCardAnchor, Color.clear);
            var holderRt = holder.GetComponent<RectTransform>();
            holderRt.anchorMin = new Vector2(0.5f, 0.5f);
            holderRt.anchorMax = new Vector2(0.5f, 0.5f);
            holderRt.pivot = new Vector2(0.5f, 0.5f);
            holderRt.sizeDelta = new Vector2(CardBaseWidth * OfferCardScale, CardBaseHeight * OfferCardScale);
            holderRt.anchoredPosition = Vector2.zero;
            _dynamicObjects.Add(holder);

            SpawnReadOnlyCard(holder.transform, template, ownerCharacterId, OfferCardScale, null);
        }

        void AddDeckCardButton(CardTemplate template, string deckKey, string ownerCharacterId, int index)
        {
            var capturedKey = deckKey;
            var holder = CreatePanelImage("DeckChoice", _deckHost, Color.clear);
            var holderRt = holder.GetComponent<RectTransform>();
            holderRt.localScale = Vector3.one;
            holderRt.anchorMin = new Vector2(0.5f, 0.5f);
            holderRt.anchorMax = new Vector2(0.5f, 0.5f);
            holderRt.pivot = new Vector2(0.5f, 0.5f);

            var cardW = CardBaseWidth * DeckCardScale;
            var cardH = CardBaseHeight * DeckCardScale;
            holderRt.sizeDelta = new Vector2(cardW, cardH);
            holderRt.anchoredPosition = ComputeDeckSlotPosition(index, cardW, cardH);
            _dynamicObjects.Add(holder);

            var btn = holder.AddComponent<Button>();
            btn.targetGraphic = holder.GetComponent<Image>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnDeckCardClicked(capturedKey));
            UiAudioHooks.WireButton(btn);

            var cardView = SpawnReadOnlyCard(holder.transform, template, ownerCharacterId, DeckCardScale, capturedKey);
            if (cardView != null)
                _deckCardViews[capturedKey] = cardView;
        }

        static Vector2 ComputeDeckSlotPosition(int index, float cardW, float cardH)
        {
            var col = index % DeckColumns;
            var row = index / DeckColumns;
            var totalW = DeckColumns * cardW + (DeckColumns - 1) * DeckGap;
            var rows = 2;
            var totalH = rows * cardH + (rows - 1) * DeckGap;
            var startX = -totalW * 0.5f + cardW * 0.5f;
            var startY = totalH * 0.5f - cardH * 0.5f;
            return new Vector2(startX + col * (cardW + DeckGap), startY - row * (cardH + DeckGap));
        }

        void OnDeckCardClicked(string deckKey)
        {
            if (string.IsNullOrEmpty(deckKey))
                return;

            _selectedDeckKey = _selectedDeckKey == deckKey ? "" : deckKey;
            RefreshDeckSelectionVisuals();
            RefreshConfirmInteractable();
        }

        void RefreshDeckSelectionVisuals()
        {
            foreach (var pair in _deckCardViews)
            {
                if (pair.Value != null)
                    pair.Value.SetSelected(pair.Key == _selectedDeckKey);
            }
        }

        void RefreshConfirmInteractable()
        {
            if (_confirmButton == null)
                return;

            var canConfirm = !string.IsNullOrEmpty(_selectedDeckKey);
            _confirmButton.interactable = canConfirm;
            var cg = _confirmButton.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = _confirmButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = canConfirm ? 1f : 0.45f;
            cg.interactable = canConfirm;
            cg.blocksRaycasts = canConfirm;
        }

        void OnConfirmReplace()
        {
            if (string.IsNullOrEmpty(_selectedDeckKey))
                return;

            _session.ReplaceDeckCardForOffer(_selectedDeckKey);
        }

        CardView SpawnReadOnlyCard(
            Transform parent,
            CardTemplate template,
            string ownerCharacterId,
            float scale,
            string deckKey)
        {
            if (_cardPrefab == null || template == null)
                return null;

            _definitions.TryGetValue(template.DefinitionId, out var definition);
            var ownerId = string.IsNullOrEmpty(ownerCharacterId) ? template.OwnerCharacterId : ownerCharacterId;
            var cardView = Instantiate(_cardPrefab, parent);
            CardView.ApplyHandPresentationScaleCentered(cardView, scale);
            var preview = CardVisualResolver.CreatePreviewInstance(
                template.DefinitionId,
                ownerId,
                template.DisplayName,
                definition);
            var visual = CardVisualResolver.Resolve(preview, _cardCatalog, _characterVisuals, _definitions);
            var statsLine = BattleUiFormatters.BuildCardStatsLinePreview(preview, _definitions);
            cardView.BindWithCard(
                preview,
                visual,
                selected: false,
                polluted: false,
                interactable: false,
                orderBadge: "",
                statsLine,
                _uiIcons,
                _characterVisuals,
                null,
                null,
                null);

            var cg = cardView.GetComponent<CanvasGroup>();
            if (cg != null)
                cg.blocksRaycasts = false;

            foreach (var graphic in cardView.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic != null)
                    graphic.raycastTarget = false;
            }

            BindCardTooltip(parent.gameObject, preview);
            return cardView;
        }

        void BindCardTooltip(GameObject target, CardInstanceState card)
        {
            if (_tooltip == null || target == null || card == null)
                return;

            var descCard = CardVisualResolver.ResolveForDescription(card, _definitions);
            var stats = BattleUiFormatters.BuildCardStatsLinePreview(descCard, _definitions);
            var keywords = StripRichText(BattleUiFormatters.BuildCardKeywordTooltip(
                _session?.Engine?.State,
                descCard,
                _definitions));
            var body = string.IsNullOrWhiteSpace(keywords) ? stats : $"{stats}\n\n{keywords}";
            _tooltip.BindHover(target, card.DisplayName, body, showTitle: false);
        }

        static string StripRichText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Replace("<b>", "").Replace("</b>", "");
        }

        void ClearDynamic()
        {
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    DestroyImmediate(go);
            }

            _dynamicObjects.Clear();
            _deckCardViews.Clear();
            ClearChildrenImmediate(_deckHost);
            ClearChildrenImmediate(_offerCardAnchor);
        }

        static void ClearChildrenImmediate(RectTransform row)
        {
            if (row == null)
                return;

            for (var i = row.childCount - 1; i >= 0; i--)
            {
                var child = row.GetChild(i);
                if (child != null)
                    DestroyImmediate(child.gameObject);
            }
        }

        void SetVisible(bool visible)
        {
            if (_root != null)
                _root.gameObject.SetActive(visible);

            if (!visible)
            {
                _tooltip?.Hide();
                _selectedDeckKey = "";
            }
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && (_layoutVersion != LayoutVersion
                           || _offerJoinLabel == null
                           || _deckLabel == null
                           || _offerCardAnchor == null
                           || _deckHost == null
                           || _confirmButton == null
                           || _abandonButton == null
                           || _panelImage == null))
            {
                if (_root != null)
                    DestroyImmediate(_root.gameObject);
                _built = false;
                _layoutVersion = 0;
                _root = null;
                _deckHost = null;
                _offerCardAnchor = null;
                _offerJoinLabel = null;
                _deckLabel = null;
                _confirmButton = null;
                _abandonButton = null;
                _panelImage = null;
                _dynamicObjects.Clear();
                _deckCardViews.Clear();
            }

            if (_built)
            {
                ApplyEventPlate();
                ApplyFooterButtons();
                return;
            }

            _built = true;
            _layoutVersion = LayoutVersion;
            var go = new GameObject("CardDeckReplaceOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            var dim = go.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);
            dim.raycastTarget = true;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.06f, 0.06f);
            panelRt.anchorMax = new Vector2(0.94f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            _panelImage = panelGo.GetComponent<Image>();
            _panelImage.raycastTarget = true;
            ApplyEventPlate();

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt, _uiIcons);

            _headerText = CreateStaticText(panelGo.transform, "卡组替换", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorRect(_headerText.rectTransform, 0.08f, 0.90f, 0.92f, 0.97f);
            _headerText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            // 左列：即将加入 + 新卡
            _offerJoinLabel = CreateStaticText(panelGo.transform, "即将加入", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorRect(_offerJoinLabel.rectTransform, 0.04f, 0.82f, 0.34f, 0.88f);
            _offerJoinLabel.color = new Color(0.55f, 0.9f, 0.65f, 1f);

            var offerGo = new GameObject("OfferAnchor", typeof(RectTransform));
            offerGo.transform.SetParent(panelGo.transform, false);
            _offerCardAnchor = offerGo.GetComponent<RectTransform>();
            AnchorRect(_offerCardAnchor, 0.04f, 0.24f, 0.34f, 0.80f);

            // 右列：选择替换 + 2×5 网格（标签与卡区分离，避免遮挡）
            _deckLabel = CreateStaticText(panelGo.transform, "选择要替换的卡牌", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorRect(_deckLabel.rectTransform, 0.36f, 0.82f, 0.96f, 0.88f);
            _deckLabel.color = new Color(0.78f, 0.72f, 0.95f, 1f);

            var deckGo = new GameObject("DeckHost", typeof(RectTransform));
            deckGo.transform.SetParent(panelGo.transform, false);
            _deckHost = deckGo.GetComponent<RectTransform>();
            AnchorRect(_deckHost, 0.36f, 0.24f, 0.96f, 0.80f);

            _abandonButton = CreateFooterButton(
                panelGo.transform,
                "放弃新卡牌",
                new Vector2(-120f, 36f));
            _abandonButton.onClick.AddListener(() => _session.AbandonCardOffer());

            _confirmButton = CreateFooterButton(
                panelGo.transform,
                "确认",
                new Vector2(120f, 36f));
            _confirmButton.onClick.AddListener(OnConfirmReplace);

            ApplyFooterButtons();
            RefreshConfirmInteractable();
            go.SetActive(false);
        }

        void ApplyEventPlate()
        {
            if (_panelImage == null)
                return;

            var plate = _uiIcons != null ? _uiIcons.UiEventPlate : null;
            if (plate != null)
            {
                _panelImage.sprite = plate;
                _panelImage.type = Image.Type.Simple;
                _panelImage.preserveAspect = false;
                _panelImage.color = Color.white;
            }
            else
            {
                _panelImage.sprite = null;
                _panelImage.color = new Color(0.1f, 0.11f, 0.15f, 0.98f);
            }

            foreach (var fx in _panelImage.GetComponents<Outline>())
                Destroy(fx);
        }

        void ApplyFooterButtons()
        {
            if (_uiIcons == null)
                return;

            if (_abandonButton != null)
                PlanningActionButtonStyle.Apply(_abandonButton, _uiIcons.UiButton3, "放弃新卡牌", AbandonButtonWidth);
            if (_confirmButton != null)
                PlanningActionButtonStyle.Apply(_confirmButton, _uiIcons.UiButton1, "确认", ConfirmButtonWidth);
        }

        static Button CreateFooterButton(Transform parent, string label, Vector2 anchoredPos)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(ConfirmButtonWidth, PlanningActionButtonStyle.HeightForWidth(ConfirmButtonWidth));
            go.GetComponent<Image>().color = Color.white;
            return go.GetComponent<Button>();
        }

        static GameObject CreatePanelImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            go.GetComponent<CanvasRenderer>().cullTransparentMesh = false;
            return go;
        }

        static Text CreateStaticText(Transform parent, string value, int size, FontStyle style, TextAnchor align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void AnchorRect(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
        {
            rt.anchorMin = new Vector2(xMin, yMin);
            rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
