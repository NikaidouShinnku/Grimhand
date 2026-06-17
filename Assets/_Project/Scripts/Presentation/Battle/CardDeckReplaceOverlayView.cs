using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CardDeckReplaceOverlayView : MonoBehaviour
    {
        const float CardScale = 0.74f;
        const float CardHolderWidth = 188f;
        const float CardHolderHeight = 272f;

        BattleSession _session;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        Text _headerText;
        Text _hintText;
        RectTransform _offerCardAnchor;
        RectTransform _deckRow;
        Button _abandonButton;
        InventoryTooltipView _tooltip;
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

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

            var memberName = offer.OwnerCharacterId;
            PartyMemberSnapshot targetMember = null;
            foreach (var member in _session.Expedition.Run.Party)
            {
                if (member.CharacterDefinitionId != offer.OwnerCharacterId)
                    continue;

                memberName = member.DisplayName;
                targetMember = member;
                break;
            }

            if (targetMember == null && _session.Expedition.Run.Party.Count > 0)
                targetMember = _session.Expedition.Run.Party[0];

            _headerText.text = "卡组已满 — 选择替换";
            _hintText.text =
                $"获得 {offer.Template.DisplayName}（{memberName}）\n" +
                $"当前 {ExpeditionRunDeckRules.DeckSize}/{ExpeditionRunDeckRules.DeckSize} 张 · 点击下方卡组中的一张进行替换，或放弃新卡。";

            SpawnOfferCardPreview(offer.Template, offer.OwnerCharacterId);

            if (targetMember == null || _cardPrefab == null)
                return;

            var config = _session.Expedition.Config;
            foreach (var entry in ExpeditionRunDeckCatalog.CollectMemberDeckEntries(config, targetMember))
                AddDeckCardButton(entry.Template, entry.Key, targetMember.CharacterDefinitionId);
        }

        void SpawnOfferCardPreview(CardTemplate template, string ownerCharacterId)
        {
            if (_offerCardAnchor == null || _cardPrefab == null || template == null)
                return;

            var label = CreateStaticText(_offerCardAnchor, "即将加入", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 1f);
            labelRt.anchorMax = new Vector2(1f, 1f);
            labelRt.pivot = new Vector2(0.5f, 1f);
            labelRt.sizeDelta = new Vector2(0f, 28f);
            labelRt.anchoredPosition = new Vector2(0f, 0f);
            label.color = new Color(0.55f, 0.9f, 0.65f, 1f);
            _dynamicObjects.Add(label.gameObject);

            var holder = CreatePanelImage("OfferCard", _offerCardAnchor, new Color(0.18f, 0.34f, 0.24f, 0.55f));
            var holderRt = holder.GetComponent<RectTransform>();
            holderRt.anchorMin = new Vector2(0.5f, 0f);
            holderRt.anchorMax = new Vector2(0.5f, 1f);
            holderRt.pivot = new Vector2(0.5f, 0.5f);
            holderRt.sizeDelta = new Vector2(CardHolderWidth, 0f);
            holderRt.offsetMin = new Vector2(-CardHolderWidth * 0.5f, 8f);
            holderRt.offsetMax = new Vector2(CardHolderWidth * 0.5f, -32f);
            _dynamicObjects.Add(holder);

            SpawnReadOnlyCard(holder.transform, template, ownerCharacterId);
        }

        void AddDeckCardButton(CardTemplate template, string deckKey, string ownerCharacterId)
        {
            var capturedKey = deckKey;
            var holder = CreatePanelImage("DeckChoice", _deckRow, new Color(0.12f, 0.13f, 0.17f, 0.92f));
            var le = holder.AddComponent<LayoutElement>();
            le.preferredWidth = CardHolderWidth;
            le.preferredHeight = CardHolderHeight;
            _dynamicObjects.Add(holder);

            var btn = holder.AddComponent<Button>();
            btn.targetGraphic = holder.GetComponent<Image>();
            btn.onClick.AddListener(() => _session.ReplaceDeckCardForOffer(capturedKey));

            SpawnReadOnlyCard(holder.transform, template, ownerCharacterId);
        }

        void SpawnReadOnlyCard(Transform parent, CardTemplate template, string ownerCharacterId)
        {
            if (_cardPrefab == null || template == null)
                return;

            _definitions.TryGetValue(template.DefinitionId, out var definition);
            var ownerId = string.IsNullOrEmpty(ownerCharacterId) ? template.OwnerCharacterId : ownerCharacterId;
            var cardView = Instantiate(_cardPrefab, parent);
            CardView.ApplyHandPresentationScaleCentered(cardView, CardScale);
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
                    Destroy(go);
            }

            _dynamicObjects.Clear();
            ClearRow(_deckRow);
        }

        void ClearRow(RectTransform row)
        {
            if (row == null)
                return;

            foreach (Transform child in row)
                Destroy(child.gameObject);
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
            panelRt.anchorMin = new Vector2(0.02f, 0.03f);
            panelRt.anchorMax = new Vector2(0.98f, 0.97f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt);

            _headerText = CreateStaticText(panelGo.transform, "卡组替换", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(_headerText.rectTransform, 0.92f, 0.98f);
            _headerText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _hintText = CreateStaticText(panelGo.transform, "", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(_hintText.rectTransform, 0.84f, 0.91f);
            _hintText.color = new Color(0.82f, 0.86f, 0.94f, 1f);

            var offerGo = new GameObject("OfferAnchor", typeof(RectTransform));
            offerGo.transform.SetParent(panelGo.transform, false);
            _offerCardAnchor = offerGo.GetComponent<RectTransform>();
            AnchorBand(_offerCardAnchor, 0.68f, 0.83f);

            var deckLabel = CreateStaticText(panelGo.transform, "选择要替换的卡牌", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            AnchorBand(deckLabel.rectTransform, 0.62f, 0.66f);
            deckLabel.rectTransform.offsetMin = new Vector2(24f, 0f);

            _deckRow = BuildScrollRow(panelGo.transform, 0.12f, 0.60f);

            _abandonButton = CreateFooterButton(
                panelGo.transform,
                "放弃新卡牌",
                new Vector2(0f, 24f),
                new Vector2(260f, 52f),
                new Color(0.24f, 0.26f, 0.32f, 1f),
                () => _session.AbandonCardOffer());

            go.SetActive(false);
        }

        RectTransform BuildScrollRow(Transform parent, float yMin, float yMax)
        {
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            AnchorBand(scrollRt, yMin, yMax);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            StretchFull(viewportRt);
            viewportGo.GetComponent<Image>().color = new Color(0.08f, 0.09f, 0.12f, 0.45f);
            viewportGo.GetComponent<Mask>().showMaskGraphic = false;

            var rowGo = new GameObject("Cards", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
            rowGo.transform.SetParent(viewportGo.transform, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.5f);
            rowRt.anchorMax = new Vector2(0f, 0.5f);
            rowRt.pivot = new Vector2(0f, 0.5f);
            var layout = rowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            rowGo.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.viewport = viewportRt;
            scroll.content = rowRt;
            return rowRt;
        }

        Button CreateFooterButton(
            Transform parent,
            string label,
            Vector2 anchoredPos,
            Vector2 size,
            Color color,
            System.Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            go.GetComponent<Image>().color = color;

            var text = CreateStaticText(go.transform, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());
            return btn;
        }

        GameObject CreatePanelImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        Text CreateStaticText(Transform parent, string text, int size, FontStyle style, TextAnchor anchor)
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
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }

        static void AnchorBand(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(24f, 0f);
            rt.offsetMax = new Vector2(-24f, 0f);
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
