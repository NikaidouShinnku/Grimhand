using System.Collections;
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
        const float CardBaseWidth = CardPortraitLayout.CardWidth;
        const float CardBaseHeight = CardPortraitLayout.CardHeight;
        const float OfferCardScale = 0.95f;
        const float MinDeckGap = 14f;
        const float DeckSidePad = 28f;
        const int LayoutVersion = 3;

        BattleSession _session;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        BattleUiIconCatalogSO _uiIcons;
        Dictionary<string, CardDefinitionSO> _definitions = new();

        RectTransform _root;
        Text _headerText;
        Text _hintText;
        Text _offerJoinLabel;
        RectTransform _offerCardAnchor;
        RectTransform _deckRow;
        RectTransform _deckViewport;
        Button _abandonButton;
        InventoryTooltipView _tooltip;
        bool _built;
        int _layoutVersion;
        Coroutine _fitRoutine;
        readonly List<GameObject> _dynamicObjects = new();
        readonly List<RectTransform> _deckHolders = new();

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

            // 立即排一次 + 下一帧再排，避免视口宽度未就绪时挤在中间叠卡
            FitDeckCardsToRow();
            if (_fitRoutine != null)
                StopCoroutine(_fitRoutine);
            _fitRoutine = StartCoroutine(FitDeckCardsNextFrame());
        }

        IEnumerator FitDeckCardsNextFrame()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            FitDeckCardsToRow();
            _fitRoutine = null;
        }

        void SpawnOfferCardPreview(CardTemplate template, string ownerCharacterId)
        {
            if (_offerCardAnchor == null || _cardPrefab == null || template == null)
                return;

            if (_offerJoinLabel != null)
                _offerJoinLabel.gameObject.SetActive(true);

            var holder = CreatePanelImage("OfferCard", _offerCardAnchor, new Color(0.18f, 0.34f, 0.24f, 0.2f));
            var holderRt = holder.GetComponent<RectTransform>();
            holderRt.anchorMin = new Vector2(0.5f, 0.5f);
            holderRt.anchorMax = new Vector2(0.5f, 0.5f);
            holderRt.pivot = new Vector2(0.5f, 0.5f);
            holderRt.sizeDelta = new Vector2(CardBaseWidth * OfferCardScale, CardBaseHeight * OfferCardScale);
            holderRt.anchoredPosition = Vector2.zero;
            _dynamicObjects.Add(holder);

            SpawnReadOnlyCard(holder.transform, template, ownerCharacterId, OfferCardScale);
        }

        void AddDeckCardButton(CardTemplate template, string deckKey, string ownerCharacterId)
        {
            var capturedKey = deckKey;
            var holder = CreatePanelImage("DeckChoice", _deckRow, new Color(0.1f, 0.11f, 0.14f, 0.35f));
            var holderRt = holder.GetComponent<RectTransform>();
            holderRt.localScale = Vector3.one;
            holderRt.anchorMin = new Vector2(0.5f, 0.5f);
            holderRt.anchorMax = new Vector2(0.5f, 0.5f);
            holderRt.pivot = new Vector2(0.5f, 0.5f);
            holderRt.sizeDelta = new Vector2(CardBaseWidth, CardBaseHeight);
            _deckHolders.Add(holderRt);
            _dynamicObjects.Add(holder);

            var btn = holder.AddComponent<Button>();
            btn.targetGraphic = holder.GetComponent<Image>();
            btn.onClick.AddListener(() => _session.ReplaceDeckCardForOffer(capturedKey));

            SpawnReadOnlyCard(holder.transform, template, ownerCharacterId, 1f);
        }

        void FitDeckCardsToRow()
        {
            if (_deckRow == null || _deckViewport == null || _deckHolders.Count == 0)
                return;

            // 关掉自动布局，完全手动占位
            var layout = _deckRow.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
                layout.enabled = false;
            var fitter = _deckRow.GetComponent<ContentSizeFitter>();
            if (fitter != null)
                fitter.enabled = false;

            _deckRow.anchorMin = Vector2.zero;
            _deckRow.anchorMax = Vector2.one;
            _deckRow.pivot = new Vector2(0.5f, 0.5f);
            _deckRow.offsetMin = Vector2.zero;
            _deckRow.offsetMax = Vector2.zero;
            _deckRow.anchoredPosition = Vector2.zero;
            _deckRow.localScale = Vector3.one;

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_deckViewport);

            var n = _deckHolders.Count;
            var viewW = _deckViewport.rect.width;
            var viewH = _deckViewport.rect.height;
            if (viewW < 50f && _root != null)
                viewW = Mathf.Max(viewW, _root.rect.width * 0.92f);
            if (viewH < 50f && _root != null)
                viewH = Mathf.Max(viewH, _root.rect.height * 0.35f);

            var availW = Mathf.Max(200f, viewW - DeckSidePad * 2f);
            var availH = Mathf.Max(180f, viewH - 8f);

            // 卡宽按整行均分，保证互不重叠；高度不够再等比缩小，把多余宽度变成间隙
            var scale = (availW - MinDeckGap * Mathf.Max(0, n - 1)) / (n * CardBaseWidth);
            if (CardBaseHeight * scale > availH)
                scale = availH / CardBaseHeight;
            scale = Mathf.Clamp(scale, 0.7f, 2.4f);

            var cardW = CardBaseWidth * scale;
            var cardH = CardBaseHeight * scale;
            var gap = n > 1
                ? Mathf.Max(MinDeckGap, (availW - cardW * n) / (n - 1))
                : 0f;

            // 总宽度刚好铺满 availW
            var totalW = cardW * n + gap * Mathf.Max(0, n - 1);
            var startX = -totalW * 0.5f + cardW * 0.5f;

            for (var i = 0; i < n; i++)
            {
                var holderRt = _deckHolders[i];
                if (holderRt == null)
                    continue;

                holderRt.SetParent(_deckRow, false);
                holderRt.localScale = Vector3.one;
                holderRt.anchorMin = new Vector2(0.5f, 0.5f);
                holderRt.anchorMax = new Vector2(0.5f, 0.5f);
                holderRt.pivot = new Vector2(0.5f, 0.5f);
                holderRt.sizeDelta = new Vector2(cardW, cardH);
                holderRt.anchoredPosition = new Vector2(startX + i * (cardW + gap), 0f);

                var cardView = holderRt.GetComponentInChildren<CardView>(true);
                if (cardView == null)
                    continue;

                CardView.ApplyHandPresentationScaleCentered(cardView, scale);
                var cvRt = cardView.transform as RectTransform;
                if (cvRt != null)
                {
                    cvRt.sizeDelta = new Vector2(cardW, cardH);
                    cvRt.anchoredPosition = Vector2.zero;
                    cvRt.localScale = Vector3.one;
                }

                var scaleRoot = cardView.transform.Find("CardScaleRoot") as RectTransform;
                if (scaleRoot != null)
                    scaleRoot.localScale = Vector3.one;
            }
        }

        void SpawnReadOnlyCard(Transform parent, CardTemplate template, string ownerCharacterId, float scale)
        {
            if (_cardPrefab == null || template == null)
                return;

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

            // 点击靠 holder 的 Button；悬停提示挂在 holder 上即可
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
            if (_fitRoutine != null)
            {
                StopCoroutine(_fitRoutine);
                _fitRoutine = null;
            }

            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    DestroyImmediate(go);
            }

            _dynamicObjects.Clear();
            _deckHolders.Clear();
            ClearRowImmediate(_deckRow);
        }

        static void ClearRowImmediate(RectTransform row)
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
                _tooltip?.Hide();
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && (_layoutVersion != LayoutVersion
                           || _offerJoinLabel == null
                           || _offerCardAnchor == null
                           || _deckViewport == null))
            {
                if (_root != null)
                    DestroyImmediate(_root.gameObject);
                _built = false;
                _layoutVersion = 0;
                _root = null;
                _deckRow = null;
                _deckViewport = null;
                _offerCardAnchor = null;
                _offerJoinLabel = null;
                _dynamicObjects.Clear();
                _deckHolders.Clear();
            }

            if (_built)
                return;

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
            panelRt.anchorMin = new Vector2(0.02f, 0.03f);
            panelRt.anchorMax = new Vector2(0.98f, 0.97f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            _tooltip = panelGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(panelRt, _uiIcons);

            _headerText = CreateStaticText(panelGo.transform, "卡组替换", 30, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(_headerText.rectTransform, 0.925f, 0.985f);
            _headerText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _hintText = CreateStaticText(panelGo.transform, "", 20, FontStyle.Normal, TextAnchor.MiddleCenter);
            AnchorBand(_hintText.rectTransform, 0.86f, 0.92f);
            _hintText.color = new Color(0.82f, 0.86f, 0.94f, 1f);

            _offerJoinLabel = CreateStaticText(panelGo.transform, "即将加入", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorBand(_offerJoinLabel.rectTransform, 0.815f, 0.855f);
            _offerJoinLabel.color = new Color(0.55f, 0.9f, 0.65f, 1f);

            var offerGo = new GameObject("OfferAnchor", typeof(RectTransform));
            offerGo.transform.SetParent(panelGo.transform, false);
            _offerCardAnchor = offerGo.GetComponent<RectTransform>();
            AnchorBand(_offerCardAnchor, 0.56f, 0.81f);

            var deckLabel = CreateStaticText(panelGo.transform, "选择要替换的卡牌", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            AnchorBand(deckLabel.rectTransform, 0.505f, 0.55f);
            deckLabel.rectTransform.offsetMin = new Vector2(24f, 0f);

            // 给卡组行更大高度，保证卡能放大
            _deckRow = BuildDeckRow(panelGo.transform, 0.10f, 0.50f);

            _abandonButton = CreateFooterButton(
                panelGo.transform,
                "放弃新卡牌",
                new Vector2(0f, 24f),
                new Vector2(260f, 52f),
                new Color(0.24f, 0.26f, 0.32f, 1f),
                () => _session.AbandonCardOffer());

            go.SetActive(false);
        }

        RectTransform BuildDeckRow(Transform parent, float yMin, float yMax)
        {
            var scrollGo = new GameObject("DeckRowHost", typeof(RectTransform), typeof(Image));
            scrollGo.transform.SetParent(parent, false);
            var hostRt = scrollGo.GetComponent<RectTransform>();
            AnchorBand(hostRt, yMin, yMax);
            var hostImg = scrollGo.GetComponent<Image>();
            hostImg.color = new Color(0.08f, 0.09f, 0.12f, 0.35f);
            hostImg.raycastTarget = false;
            _deckViewport = hostRt;

            var rowGo = new GameObject("Cards", typeof(RectTransform));
            rowGo.transform.SetParent(scrollGo.transform, false);
            var rowRt = rowGo.GetComponent<RectTransform>();
            StretchFull(rowRt);
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
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            var text = CreateStaticText(go.transform, label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            StretchFull(text.rectTransform);
            text.color = Color.white;
            return btn;
        }

        static GameObject CreatePanelImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
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

        static void AnchorBand(RectTransform rt, float yMin, float yMax)
        {
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
