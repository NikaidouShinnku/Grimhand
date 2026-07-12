using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>局外商店：可滚动商品列表 + 开包全收。</summary>
    [DisallowMultipleComponent]
    public sealed class MetaShopOverlayView : MonoBehaviour
    {
        const float OfferCellHeight = 220f;
        const float CardScale = 1.05f;
        const string DefaultShopHint = "购买卡包后一次开出 3 张卡牌，过目并收下全部加入军营收藏。";

        ExpeditionConfig _config;
        List<string> _characterIds = new();
        PlayerProfileState _profile;
        MetaShopPendingPack _pendingPack;
        BattleRng _rng = new(90210);

        BattleUiIconCatalogSO _uiIcons;
        CardView _cardPrefab;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        Action _onProfileChanged;
        Action _onClose;

        RectTransform _overlayRoot;
        RectTransform _shopPanel;
        RectTransform _pickPanel;
        RectTransform _offerContent;
        RectTransform _goldRow;
        ScrollRect _offerScroll;
        Text _titleText;
        Image _campGoldIcon;
        Text _goldText;
        Text _collectionText;
        Text _messageText;
        Text _pickHeaderText;
        Text _pickHintText;
        RectTransform _pickChoiceRow;
        Button _closeButton;
        Button _pickCollectButton;
        InventoryTooltipView _tooltip;
        bool _built;
        readonly List<GameObject> _dynamicObjects = new();

        public bool IsOpen => _overlayRoot != null && _overlayRoot.gameObject.activeSelf;

        public void Initialize(
            ExpeditionSetupSO expeditionSetup,
            BattleSetupSO battleSetup,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            BattleUiIconCatalogSO uiIcons,
            Dictionary<string, CardDefinitionSO> definitions,
            Action onProfileChanged,
            Action onClose)
        {
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _uiIcons = uiIcons;
            _definitions = definitions ?? new Dictionary<string, CardDefinitionSO>();
            _onProfileChanged = onProfileChanged;
            _onClose = onClose;
            _config = expeditionSetup != null ? expeditionSetup.ToExpeditionConfig() : null;
            _characterIds = CollectPlayableCharacterIds(battleSetup);
            EnsureBuilt();
        }

        public void Show(PlayerProfileState profile)
        {
            _profile = profile;
            EnsureBuilt();
            _messageText.text = DefaultShopHint;
            _overlayRoot.gameObject.SetActive(true);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            RefreshShopPanel();
            RefreshPickPanel();
        }

        public void Refresh()
        {
            RefreshShopPanel();
            RefreshPickPanel();
        }

        public void Hide()
        {
            if (_pendingPack != null)
                return;

            _tooltip?.Hide();
            if (_overlayRoot != null)
                _overlayRoot.gameObject.SetActive(false);
        }

        void RefreshShopPanel()
        {
            if (_profile == null)
                return;

            if (_campGoldIcon != null && _uiIcons?.CampGoldIcon != null)
            {
                _campGoldIcon.sprite = _uiIcons.CampGoldIcon;
                _campGoldIcon.color = Color.white;
            }

            _goldText.text = _profile.AccountGold.ToString();
            _collectionText.text =
                $"军营收藏：{_profile.Collection.Count}/{_profile.CollectionCapacity}";

            var showShop = _pendingPack == null;
            _shopPanel.gameObject.SetActive(showShop);
            if (!showShop)
                return;

            ClearDynamic();
            foreach (var offer in MetaShopCatalog.DemoCardPacks)
                BuildOfferRow(offer);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_offerContent);
            _offerScroll.verticalNormalizedPosition = 1f;
        }

        void RefreshPickPanel()
        {
            var showPick = _pendingPack != null && _pendingPack.Choices.Count > 0;
            _pickPanel.gameObject.SetActive(showPick);
            if (!showPick)
                return;

            _shopPanel.gameObject.SetActive(false);
            _pickHeaderText.text = CardPackIds.GetDisplayName(_pendingPack.PackId);
            _pickHintText.text = "本次开包获得以下卡牌，确认后将全部加入军营收藏。";

            ClearPickChoices();
            for (var i = 0; i < _pendingPack.Choices.Count; i++)
                BuildPickChoice(_pendingPack.Choices[i], i);
        }

        void BuildOfferRow(MetaShopCatalog.Offer offer)
        {
            var rowGo = CampUiRuntime.CreateRect($"Offer_{offer.PackId}", _offerContent);
            var rowRt = rowGo.GetComponent<RectTransform>();
            var layout = rowGo.AddComponent<LayoutElement>();
            layout.minHeight = OfferCellHeight;
            layout.preferredHeight = OfferCellHeight;

            var bg = rowGo.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);

            var iconGo = CampUiRuntime.CreateRect("Icon", rowGo.transform);
            var iconRt = iconGo.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(iconRt, 0.03f, 0.12f, 0.22f, 0.88f);
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = CardPackVisuals.GetPackIcon(offer.PackId, _uiIcons);
            icon.preserveAspect = true;
            icon.color = icon.sprite != null ? Color.white : new Color(0.85f, 0.75f, 0.45f, 1f);

            var title = CampUiRuntime.CreateText(rowGo.transform, CardPackIds.GetDisplayName(offer.PackId), 28,
                FontStyle.Bold, TextAnchor.MiddleLeft);
            CampUiRuntime.SetAnchored(title.rectTransform, 0.24f, 0.58f, 0.72f, 0.88f);

            var hint = CampUiRuntime.CreateText(rowGo.transform, offer.Hint, 18, FontStyle.Normal, TextAnchor.UpperLeft);
            CampUiRuntime.SetAnchored(hint.rectTransform, 0.24f, 0.18f, 0.72f, 0.56f);
            hint.color = new Color(0.78f, 0.82f, 0.9f, 1f);

            var priceRow = CreateGoldAmountRow(rowGo.transform, offer.Price.ToString(), 24);
            CampUiRuntime.SetAnchored(priceRow, 0.74f, 0.52f, 0.97f, 0.86f);

            var canBuy = _profile.AccountGold >= offer.Price
                         && !CampCollectionRules.BlocksShopCardPack(_profile.Collection, _profile.CollectionCapacity);
            var buyBtn = CampUiRuntime.CreateButton(rowGo.transform, "购买",
                new Color(0.2f, 0.34f, 0.22f, 0.98f), new Vector2(140f, 48f));
            var buyRt = buyBtn.GetComponent<RectTransform>();
            buyRt.anchorMin = new Vector2(0.74f, 0.14f);
            buyRt.anchorMax = new Vector2(0.97f, 0.14f);
            buyRt.pivot = new Vector2(0.5f, 0f);
            buyRt.anchoredPosition = Vector2.zero;
            buyBtn.interactable = canBuy && _pendingPack == null;
            var packId = offer.PackId;
            buyBtn.onClick.AddListener(() => TryBuy(packId));

            if (_tooltip != null)
            {
                _tooltip.BindHover(
                    rowGo,
                    CardPackIds.GetDisplayName(offer.PackId),
                    $"{offer.Hint}\n价格：{offer.Price}",
                    showTitle: true);
            }

            _dynamicObjects.Add(rowGo);
        }

        void TryBuy(string packId)
        {
            if (_profile == null || _pendingPack != null)
                return;

            var accountGold = _profile.AccountGold;
            if (!MetaShopRules.TryBuyPack(
                    ref accountGold,
                    _profile.Collection,
                    _profile.CollectionCapacity,
                    _config,
                    _characterIds,
                    packId,
                    _rng,
                    out _pendingPack,
                    out var message))
            {
                _messageText.text = message;
                RefreshShopPanel();
                return;
            }

            _profile.AccountGold = accountGold;
            _messageText.text = message;
            _onProfileChanged?.Invoke();
            RefreshPickPanel();
        }

        void BuildPickChoice(CardPackChoice choice, int choiceIndex)
        {
            if (choice?.Template == null || _cardPrefab == null)
                return;

            var holder = new GameObject($"Choice_{choiceIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
            holder.transform.SetParent(_pickChoiceRow, false);
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
                interactable: false,
                orderBadge: "",
                statsLine: statsLine,
                uiIcons: _uiIcons,
                characterVisuals: _characterVisuals,
                onClick: null,
                onHoverEnter: null,
                onHoverExit: null);

            var btn = holder.GetComponent<Button>();
            btn.targetGraphic = holder.GetComponent<Image>();
            btn.interactable = false;
            _dynamicObjects.Add(holder);
        }

        void TryCollectAll()
        {
            if (_profile == null || _pendingPack == null)
                return;

            if (!MetaShopRules.TryCollectAllCards(
                    _profile.Collection,
                    _profile.CollectionCapacity,
                    _pendingPack,
                    out var message))
            {
                _messageText.text = message;
                return;
            }

            _messageText.text = message;
            _pendingPack = null;
            _onProfileChanged?.Invoke();
            RefreshShopPanel();
            RefreshPickPanel();
        }

        void TryClose()
        {
            if (_pendingPack != null)
            {
                _messageText.text = "请先过目并收下卡牌，才能离开商店。";
                return;
            }

            Hide();
            _onClose?.Invoke();
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

        void ClearPickChoices()
        {
            if (_pickChoiceRow == null)
                return;

            foreach (Transform child in _pickChoiceRow)
                Destroy(child.gameObject);
        }

        static List<string> CollectPlayableCharacterIds(BattleSetupSO battleSetup)
        {
            var ids = new List<string>();
            var seen = new HashSet<string>();
            if (battleSetup?.Combatants == null)
                return ids;

            foreach (var combatant in battleSetup.Combatants)
            {
                if (combatant == null || combatant.Team != TeamSide.Player)
                    continue;

                if (string.IsNullOrEmpty(combatant.CharacterId) || !seen.Add(combatant.CharacterId))
                    continue;

                ids.Add(combatant.CharacterId);
            }

            return ids;
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;

            _overlayRoot = CampUiRuntime.CreateRect("MetaShopOverlay", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);
            var dim = _overlayRoot.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.62f);

            BuildShopPanel();
            BuildPickPanel();
            _tooltip = _overlayRoot.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_overlayRoot);

            _overlayRoot.gameObject.SetActive(false);
        }

        void BuildShopPanel()
        {
            _shopPanel = CampUiRuntime.CreateRect("ShopPanel", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(_shopPanel, 0.06f, 0.04f, 0.94f, 0.96f);
            var panelBg = _shopPanel.gameObject.AddComponent<Image>();
            ApplyPanelBackground(panelBg);
            panelBg.raycastTarget = true;
            panelBg.gameObject.AddComponent<ScrollRectDragForwarder>();

            BuildShopScrollArea();

            _titleText = CampUiRuntime.CreateText(_shopPanel, "局外商店", 40, FontStyle.Bold);
            CampUiRuntime.SetAnchored(_titleText.rectTransform, 0.04f, 0.91f, 0.96f, 0.99f);
            _titleText.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            DisableRaycast(_titleText);

            _goldRow = CreateGoldAmountRow(_shopPanel, "0", 26, out _campGoldIcon, out _goldText);
            CampUiRuntime.SetAnchored(_goldRow, 0.04f, 0.865f, 0.28f, 0.905f);

            _collectionText = CampUiRuntime.CreateText(_shopPanel, "", 22, FontStyle.Normal);
            CampUiRuntime.SetAnchored(_collectionText.rectTransform, 0.5f, 0.865f, 0.96f, 0.905f);
            _collectionText.alignment = TextAnchor.MiddleRight;
            _collectionText.color = new Color(0.78f, 0.82f, 0.9f, 1f);
            DisableRaycast(_collectionText);

            _messageText = CampUiRuntime.CreateText(_shopPanel, DefaultShopHint, 20, FontStyle.Normal);
            CampUiRuntime.SetAnchored(_messageText.rectTransform, 0.06f, 0.795f, 0.94f, 0.855f);
            _messageText.color = new Color(0.78f, 0.82f, 0.9f, 1f);
            DisableRaycast(_messageText);

            var forwarder = panelBg.GetComponent<ScrollRectDragForwarder>();
            if (forwarder != null)
                forwarder.Bind(_offerScroll);

            _closeButton = CampUiRuntime.CreateButton(_shopPanel, "离开商店",
                new Color(0.16f, 0.18f, 0.24f, 0.98f), new Vector2(240f, 56f));
            var closeRt = _closeButton.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0.03f);
            closeRt.anchorMax = new Vector2(0.5f, 0.03f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = Vector2.zero;
            _closeButton.onClick.AddListener(TryClose);

            _titleText.transform.SetAsLastSibling();
            _goldRow.SetAsLastSibling();
            _collectionText.transform.SetAsLastSibling();
            _messageText.transform.SetAsLastSibling();
            _closeButton.transform.SetAsLastSibling();
        }

        void BuildShopScrollArea()
        {
            var scrollGo = CampUiRuntime.CreateRect("OfferScroll", _shopPanel);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(scrollRt, 0.04f, 0.1f, 0.96f, 0.72f);
            _offerScroll = scrollGo.AddComponent<ScrollRect>();
            _offerScroll.horizontal = false;
            _offerScroll.vertical = true;
            _offerScroll.movementType = ScrollRect.MovementType.Elastic;
            _offerScroll.scrollSensitivity = 80f;
            _offerScroll.inertia = true;
            _offerScroll.decelerationRate = 0.12f;

            var viewportGo = CampUiRuntime.CreateRect("Viewport", scrollGo.transform);
            var viewportRt = viewportGo.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(viewportRt);
            var viewportMask = viewportGo.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            var viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportImage.raycastTarget = true;
            _offerScroll.viewport = viewportRt;

            _offerContent = CampUiRuntime.CreateRect("Content", viewportGo.transform).GetComponent<RectTransform>();
            _offerContent.anchorMin = new Vector2(0f, 1f);
            _offerContent.anchorMax = new Vector2(1f, 1f);
            _offerContent.pivot = new Vector2(0.5f, 1f);
            _offerContent.offsetMin = new Vector2(0f, _offerContent.offsetMin.y);
            _offerContent.offsetMax = new Vector2(0f, _offerContent.offsetMax.y);

            var contentLayout = _offerContent.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 16f;
            contentLayout.padding = new RectOffset(12, 12, 12, 12);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = _offerContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _offerScroll.content = _offerContent;
        }

        void BuildPickPanel()
        {
            _pickPanel = CampUiRuntime.CreateRect("PickPanel", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(_pickPanel, 0.12f, 0.12f, 0.88f, 0.88f);
            var panelBg = _pickPanel.gameObject.AddComponent<Image>();
            panelBg.color = new Color(0.1f, 0.11f, 0.15f, 0.98f);

            _pickHeaderText = CampUiRuntime.CreateText(_pickPanel, "卡包", 30, FontStyle.Bold);
            CampUiRuntime.SetAnchored(_pickHeaderText.rectTransform, 0.04f, 0.9f, 0.96f, 0.98f);
            _pickHeaderText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _pickHintText = CampUiRuntime.CreateText(_pickPanel, "", 20, FontStyle.Normal);
            CampUiRuntime.SetAnchored(_pickHintText.rectTransform, 0.04f, 0.82f, 0.96f, 0.89f);

            var rowGo = CampUiRuntime.CreateRect("Choices", _pickPanel);
            _pickChoiceRow = rowGo.GetComponent<RectTransform>();
            CampUiRuntime.SetAnchored(_pickChoiceRow, 0.04f, 0.12f, 0.96f, 0.8f);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 32f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;

            _pickCollectButton = CampUiRuntime.CreateButton(_pickPanel, "收下全部",
                new Color(0.22f, 0.42f, 0.24f, 1f), new Vector2(260f, 56f));
            var collectRt = _pickCollectButton.GetComponent<RectTransform>();
            collectRt.anchorMin = new Vector2(0.5f, 0.04f);
            collectRt.anchorMax = new Vector2(0.5f, 0.04f);
            collectRt.pivot = new Vector2(0.5f, 0f);
            collectRt.anchoredPosition = Vector2.zero;
            _pickCollectButton.onClick.AddListener(TryCollectAll);

            _pickPanel.gameObject.SetActive(false);
        }

        void ApplyPanelBackground(Image panelBg)
        {
            var sprite = _uiIcons?.ShopBackground;
            if (sprite != null)
            {
                panelBg.sprite = sprite;
                panelBg.color = Color.white;
                panelBg.type = Image.Type.Simple;
                panelBg.preserveAspect = false;
                return;
            }

            panelBg.color = new Color(0.09f, 0.1f, 0.14f, 0.98f);
        }

        static void DisableRaycast(Text text)
        {
            if (text != null)
                text.raycastTarget = false;
        }

        RectTransform CreateGoldAmountRow(
            Transform parent,
            string amount,
            int fontSize,
            out Image icon,
            out Text amountText)
        {
            const float iconSize = 28f;
            var row = CampUiRuntime.CreateRect("GoldAmount", parent).GetComponent<RectTransform>();
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var iconGo = CampUiRuntime.CreateImage("CampGoldIcon", row, Color.white);
            icon = iconGo;
            icon.preserveAspect = true;
            icon.sprite = _uiIcons != null ? _uiIcons.CampGoldIcon : null;
            icon.raycastTarget = false;
            var iconLayout = iconGo.gameObject.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = iconSize;
            iconLayout.preferredHeight = iconSize;

            amountText = CampUiRuntime.CreateText(row, amount, fontSize, FontStyle.Bold, TextAnchor.MiddleLeft);
            amountText.alignment = TextAnchor.MiddleLeft;
            DisableRaycast(amountText);
            var textLayout = amountText.gameObject.AddComponent<LayoutElement>();
            textLayout.preferredHeight = iconSize;

            return row;
        }

        RectTransform CreateGoldAmountRow(Transform parent, string amount, int fontSize)
        {
            return CreateGoldAmountRow(parent, amount, fontSize, out _, out _);
        }
    }

    /// <summary>将面板背景的拖拽/滚轮转发给 ScrollRect，便于在背景上滑动浏览。</summary>
    sealed class ScrollRectDragForwarder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler
    {
        ScrollRect _scroll;

        public void Bind(ScrollRect scroll) => _scroll = scroll;

        public void OnBeginDrag(PointerEventData eventData) => _scroll?.OnBeginDrag(eventData);
        public void OnDrag(PointerEventData eventData) => _scroll?.OnDrag(eventData);
        public void OnEndDrag(PointerEventData eventData) => _scroll?.OnEndDrag(eventData);
        public void OnScroll(PointerEventData eventData) => _scroll?.OnScroll(eventData);
    }
}
