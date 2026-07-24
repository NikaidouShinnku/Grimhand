using System;
using System.Collections.Generic;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Core;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Persistence;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using Grimhand.Presentation.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>局外商店：营地商店模板 + 商品板组装 + 开包全收。</summary>
    [DisallowMultipleComponent]
    public sealed class MetaShopOverlayView : MonoBehaviour
    {
        const int LayoutVersion = 9;
        const float CardScale = 1.05f;
        const float ButtonHoverScale = 1.08f;
        const float GoldIconSize = 20f;
        // goodsplate sprite 裁切 1634×228
        const float GoodsPlateAspect = 1634f / 228f;

        // 模板 1670×941 归一化
        static readonly Vector4[] ZoneResourceFrames =
        {
            new(0.1204f, 0.8200f, 0.2545f, 0.8620f),
            new(0.2719f, 0.8200f, 0.4060f, 0.8620f),
            new(0.4228f, 0.8200f, 0.5629f, 0.8620f),
            new(0.5808f, 0.8200f, 0.7174f, 0.8620f),
            new(0.7419f, 0.8200f, 0.8719f, 0.8620f)
        };
        // 右上独立「军营收藏：」框内、冒号之后写 拥有/上限
        static readonly Vector4 ZoneCollection = new(0.8700f, 0.8840f, 0.9320f, 0.9300f);
        // 商品区：略右移，并上下加高
        static readonly Vector4 ZoneOfferList = new(0.1500f, 0.1580f, 0.8200f, 0.7450f);
        static readonly Vector4 ZoneScrollbar = new(0.8280f, 0.1680f, 0.8460f, 0.7350f);
        // 离开钮：略放大，盖住底框
        static readonly Vector4 ZoneLeave = new(0.3880f, 0.0240f, 0.6120f, 0.1160f);
        static readonly Vector4 ZoneMessage = new(0.0600f, 0.1050f, 0.4000f, 0.1450f);

        // 相对单行 goodsplate（行内价格保持原正确位置，勿动）
        static readonly Vector4 RowImagePlate = new(0.018f, 0.10f, 0.165f, 0.90f);
        static readonly Vector4 RowIcon = new(0.032f, 0.18f, 0.152f, 0.82f);
        static readonly Vector4 RowTitle = new(0.190f, 0.56f, 0.680f, 0.90f);
        static readonly Vector4 RowDesc = new(0.190f, 0.14f, 0.680f, 0.50f);
        static readonly Vector4 RowPrice = new(0.700f, 0.28f, 0.770f, 0.72f);
        static readonly Vector4 RowBuy = new(0.800f, 0.20f, 0.975f, 0.80f);

        static readonly Color ValueText = new(0.96f, 0.92f, 0.78f, 1f);
        static readonly Color BodyText = new(0.78f, 0.82f, 0.88f, 1f);

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
        ScrollRect _offerScroll;
        Scrollbar _offerScrollbar;
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
        int _builtVersion;
        readonly List<GameObject> _dynamicObjects = new();
        readonly List<LayoutElement> _offerRowLayouts = new();

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
            _tooltip?.Hide();
            if (_messageText != null)
                _messageText.text = "";
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

            if (_goldText != null)
                _goldText.text = _profile.AccountGold.ToString();
            if (_collectionText != null)
                _collectionText.text = $"{_profile.Collection.Count}/{_profile.CollectionCapacity}";

            var showShop = _pendingPack == null;
            _shopPanel.gameObject.SetActive(showShop);
            if (!showShop)
                return;

            var scrollY = ScrollRectNavigation.CaptureVertical(_offerScroll);
            _tooltip?.Hide();
            ClearDynamic();
            foreach (var offer in MetaShopCatalog.DemoCardPacks)
                BuildOfferRow(offer);
            Canvas.ForceUpdateCanvases();
            SyncOfferRowHeights();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_offerContent);
            SyncScrollbarSize();
            ScrollRectNavigation.RestoreVertical(_offerScroll, scrollY);
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
            layout.minHeight = 96f;
            layout.preferredHeight = 120f;
            _offerRowLayouts.Add(layout);

            var plate = rowGo.AddComponent<Image>();
            plate.color = Color.white;
            plate.raycastTarget = true;
            plate.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiMerchantGoodsPlate != null)
                plate.sprite = _uiIcons.UiMerchantGoodsPlate;
            else
                plate.color = new Color(0.12f, 0.13f, 0.16f, 0.96f);

            // 图标框
            var imagePlate = CampUiRuntime.CreateImage("ImagePlate", rowGo.transform, Color.white);
            imagePlate.preserveAspect = false;
            imagePlate.raycastTarget = false;
            if (_uiIcons != null && _uiIcons.UiMerchantGoodsImagePlate != null)
                imagePlate.sprite = _uiIcons.UiMerchantGoodsImagePlate;
            else
                imagePlate.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            SetZone(imagePlate.rectTransform, RowImagePlate);

            var icon = CampUiRuntime.CreateImage("Icon", rowGo.transform, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = CardPackVisuals.GetPackIcon(offer.PackId, _uiIcons);
            if (icon.sprite == null)
                icon.color = new Color(0.85f, 0.75f, 0.45f, 1f);
            SetZone(icon.rectTransform, RowIcon);

            var title = CampUiRuntime.CreateText(
                rowGo.transform, CardPackIds.GetDisplayName(offer.PackId), 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetZone(title.rectTransform, RowTitle);
            title.color = ValueText;
            title.raycastTarget = false;

            var hint = CampUiRuntime.CreateText(rowGo.transform, offer.Hint, 16, FontStyle.Normal, TextAnchor.UpperLeft);
            SetZone(hint.rectTransform, RowDesc);
            hint.color = BodyText;
            hint.raycastTarget = false;
            hint.horizontalOverflow = HorizontalWrapMode.Wrap;
            hint.verticalOverflow = VerticalWrapMode.Truncate;

            BuildPriceOnRow(rowGo.transform, offer.Price);

            var canBuy = _profile.AccountGold >= offer.Price
                         && !CampCollectionRules.BlocksShopCardPack(_profile.Collection, _profile.CollectionCapacity);
            var buyBtn = CreateBuyButton(rowGo.transform, canBuy && _pendingPack == null, offer.PackId);

            if (_tooltip != null)
            {
                _tooltip.BindHover(
                    rowGo,
                    CardPackIds.GetDisplayName(offer.PackId),
                    $"{offer.Hint}\n价格：{offer.Price}",
                    showTitle: true);
            }

            _dynamicObjects.Add(rowGo);
            ScrollRectNavigation.WireForwarding(rowGo, _offerScroll);
            ScrollRectNavigation.WireForwarding(buyBtn.gameObject, _offerScroll);
        }

        void BuildPriceOnRow(Transform row, int price)
        {
            var priceGo = CampUiRuntime.CreateRect("Price", row);
            var priceRt = priceGo.GetComponent<RectTransform>();
            SetZone(priceRt, RowPrice);

            // 与行内其它元素对齐；禁止 flexibleWidth
            var layout = priceGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 3f;
            layout.padding = new RectOffset(4, 4, 2, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var icon = CampUiRuntime.CreateImage("Gold", priceGo.transform, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.sprite = _uiIcons != null ? _uiIcons.CampGoldIcon : null;
            icon.rectTransform.sizeDelta = new Vector2(15f, 15f);
            var iconLe = icon.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = 15f;
            iconLe.preferredHeight = 15f;
            iconLe.minWidth = 15f;
            iconLe.minHeight = 15f;

            var text = CampUiRuntime.CreateText(priceGo.transform, price.ToString(), 17, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            text.color = ValueText;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            var textLe = text.gameObject.AddComponent<LayoutElement>();
            textLe.preferredHeight = 17f;
            textLe.preferredWidth = 48f;
        }

        Button CreateBuyButton(Transform row, bool interactable, string packId)
        {
            var go = CampUiRuntime.CreateRect("Buy", row);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, RowBuy);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton2 != null)
                img.sprite = _uiIcons.UiButton2;
            else
                img.color = new Color(0.22f, 0.28f, 0.4f, 0.98f);

            var label = CampUiRuntime.CreateText(go.transform, "购买", 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -6f);
            label.color = ValueText;
            label.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = interactable ? 1f : 0.45f;
            group.blocksRaycasts = true;
            group.interactable = interactable;
            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.interactable = interactable;
            btn.onClick.AddListener(() => TryBuy(packId));
            UiAudioHooks.WireButton(btn);
            return btn;
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
                if (_messageText != null)
                    _messageText.text = message;
                RefreshShopPanel();
                return;
            }

            _profile.AccountGold = accountGold;
            if (_messageText != null)
                _messageText.text = message;
            GameAudioService.Instance.PlayUiCardPackOpen();
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
                if (_messageText != null)
                    _messageText.text = message;
                return;
            }

            if (_messageText != null)
                _messageText.text = message;
            _pendingPack = null;
            GameAudioService.Instance.PlayUiCardAcquire();
            _onProfileChanged?.Invoke();
            RefreshShopPanel();
            RefreshPickPanel();
        }

        void TryClose()
        {
            if (_pendingPack != null)
            {
                if (_messageText != null)
                    _messageText.text = "请先过目并收下卡牌，才能离开商店。";
                return;
            }

            Hide();
            _onClose?.Invoke();
        }

        void ClearDynamic()
        {
            _tooltip?.Hide();
            foreach (var go in _dynamicObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _dynamicObjects.Clear();
            _offerRowLayouts.Clear();
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
            if (_built && _builtVersion == LayoutVersion && _overlayRoot != null)
                return;

            if (_overlayRoot != null)
                Destroy(_overlayRoot.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;

            _overlayRoot = CampUiRuntime.CreateRect("MetaShopOverlay", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_overlayRoot);

            BuildShopPanel();
            BuildPickPanel();
            _tooltip = _overlayRoot.gameObject.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_overlayRoot);

            _overlayRoot.gameObject.SetActive(false);
        }

        void BuildShopPanel()
        {
            _shopPanel = CampUiRuntime.CreateRect("ShopPanel", _overlayRoot).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_shopPanel);
            var panelBg = _shopPanel.gameObject.AddComponent<Image>();
            ApplyPanelBackground(panelBg);
            panelBg.raycastTarget = true;

            BuildOfferScrollArea();
            BuildOfferScrollbar();

            // 资源框：第 1 格黄金；第 2–4 格空着；第 5 格收藏 拥有/上限
            BuildGoldResourceFrame(ZoneResourceFrames[0]);

            // 右上「军营收藏：」冒号后写 拥有/上限
            _collectionText = CampUiRuntime.CreateText(_shopPanel, "0/0", 20, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetZone(_collectionText.rectTransform, ZoneCollection);
            _collectionText.rectTransform.offsetMin = new Vector2(0f, 1f);
            _collectionText.rectTransform.offsetMax = new Vector2(-2f, -2f);
            _collectionText.color = ValueText;
            _collectionText.raycastTarget = false;

            _messageText = CampUiRuntime.CreateText(_shopPanel, "", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetZone(_messageText.rectTransform, ZoneMessage);
            _messageText.color = BodyText;
            _messageText.raycastTarget = false;

            BuildLeaveButton();

            var forwarder = panelBg.gameObject.AddComponent<ScrollRectEventForwarder>();
            forwarder.Bind(_offerScroll);
        }

        void BuildLeaveButton()
        {
            var leaveGo = CampUiRuntime.CreateRect("Leave", _shopPanel);
            var rt = leaveGo.GetComponent<RectTransform>();
            SetZone(rt, ZoneLeave);

            var img = leaveGo.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiButton6 != null)
                img.sprite = _uiIcons.UiButton6;
            else
                img.color = new Color(0.18f, 0.2f, 0.28f, 0.98f);

            var label = CampUiRuntime.CreateText(leaveGo.transform, "离开商店", 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(6f, 4f);
            label.rectTransform.offsetMax = new Vector2(-6f, -8f);
            label.color = ValueText;
            label.raycastTarget = false;

            var group = leaveGo.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = leaveGo.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            _closeButton = leaveGo.AddComponent<Button>();
            _closeButton.targetGraphic = img;
            _closeButton.transition = Selectable.Transition.None;
            _closeButton.onClick.AddListener(TryClose);
            UiAudioHooks.WireButton(_closeButton);
        }

        void BuildGoldResourceFrame(Vector4 zone)
        {
            var frame = CampUiRuntime.CreateRect("GoldFrame", _shopPanel).GetComponent<RectTransform>();
            SetZone(frame, zone);

            // 格内水平居中，垂直略偏上
            var layout = frame.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(8, 8, 3, 9);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _campGoldIcon = CampUiRuntime.CreateImage("Icon", frame, Color.white);
            _campGoldIcon.preserveAspect = true;
            _campGoldIcon.raycastTarget = false;
            _campGoldIcon.sprite = _uiIcons != null ? _uiIcons.CampGoldIcon : null;
            var iconLe = _campGoldIcon.gameObject.AddComponent<LayoutElement>();
            iconLe.preferredWidth = GoldIconSize;
            iconLe.preferredHeight = GoldIconSize;
            iconLe.minWidth = GoldIconSize;
            iconLe.minHeight = GoldIconSize;
            _campGoldIcon.rectTransform.sizeDelta = new Vector2(GoldIconSize, GoldIconSize);

            _goldText = CampUiRuntime.CreateText(frame, "0", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            _goldText.color = ValueText;
            _goldText.raycastTarget = false;
            _goldText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var textLe = _goldText.gameObject.AddComponent<LayoutElement>();
            textLe.preferredHeight = GoldIconSize + 2f;
            textLe.preferredWidth = 72f;
        }

        void BuildOfferScrollArea()
        {
            var scrollGo = CampUiRuntime.CreateRect("OfferScroll", _shopPanel);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            SetZone(scrollRt, ZoneOfferList);
            scrollGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            _offerScroll = scrollGo.AddComponent<ScrollRect>();
            _offerScroll.horizontal = false;
            _offerScroll.vertical = true;
            _offerScroll.movementType = ScrollRect.MovementType.Clamped;
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
            contentLayout.spacing = 10f;
            contentLayout.padding = new RectOffset(6, 6, 8, 8);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = _offerContent.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _offerScroll.content = _offerContent;
        }

        void BuildOfferScrollbar()
        {
            var barGo = CampUiRuntime.CreateRect("OfferScrollbar", _shopPanel);
            var barRt = barGo.GetComponent<RectTransform>();
            SetZone(barRt, ZoneScrollbar);

            var barImg = barGo.AddComponent<Image>();
            barImg.color = Color.white;
            barImg.raycastTarget = true;
            barImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSliderBar != null)
                barImg.sprite = _uiIcons.UiSliderBar;
            else
                barImg.color = new Color(0.12f, 0.11f, 0.1f, 0.95f);

            var slidingArea = CampUiRuntime.CreateRect("Sliding Area", barGo.transform);
            var slidingRt = slidingArea.GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(slidingRt);
            slidingRt.offsetMin = new Vector2(1f, 8f);
            slidingRt.offsetMax = new Vector2(-1f, -8f);

            var handleGo = CampUiRuntime.CreateRect("Handle", slidingArea.transform);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            var handleImg = handleGo.AddComponent<Image>();
            handleImg.color = Color.white;
            handleImg.raycastTarget = true;
            handleImg.preserveAspect = false;
            if (_uiIcons != null && _uiIcons.UiSlider != null)
                handleImg.sprite = _uiIcons.UiSlider;
            else
                handleImg.color = new Color(0.42f, 0.34f, 0.28f, 1f);

            _offerScrollbar = barGo.AddComponent<Scrollbar>();
            _offerScrollbar.handleRect = handleRt;
            _offerScrollbar.targetGraphic = handleImg;
            _offerScrollbar.direction = Scrollbar.Direction.BottomToTop;
            _offerScrollbar.numberOfSteps = 0;
            _offerScrollbar.size = 1f;
            _offerScrollbar.value = 1f;

            _offerScroll.verticalScrollbar = _offerScrollbar;
            _offerScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            _offerScroll.verticalScrollbarSpacing = 0f;
        }

        void SyncOfferRowHeights()
        {
            if (_offerContent == null || _offerRowLayouts.Count == 0)
                return;

            var width = Mathf.Max(1f, _offerContent.rect.width - 12f);
            var height = width / GoodsPlateAspect;
            foreach (var layout in _offerRowLayouts)
            {
                if (layout == null)
                    continue;
                layout.minHeight = height;
                layout.preferredHeight = height;
            }
        }

        void SyncScrollbarSize()
        {
            if (_offerScrollbar == null || _offerScroll == null || _offerScroll.viewport == null)
                return;

            Canvas.ForceUpdateCanvases();
            var viewH = Mathf.Max(1f, _offerScroll.viewport.rect.height);
            var contentH = Mathf.Max(viewH, _offerContent.rect.height);
            _offerScrollbar.size = contentH <= viewH + 0.5f ? 1f : Mathf.Clamp01(viewH / contentH);
            if (_offerScrollbar.size >= 0.999f)
                _offerScrollbar.value = 1f;
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
            var sprite = _uiIcons != null ? _uiIcons.UiCampShopBackground : null;
            if (sprite == null)
                sprite = _uiIcons != null ? _uiIcons.ShopBackground : null;

            if (sprite != null)
            {
                panelBg.sprite = sprite;
                panelBg.color = Color.white;
                panelBg.type = Image.Type.Simple;
                panelBg.preserveAspect = false;
                return;
            }

            panelBg.color = new Color(0.09f, 0.1f, 0.14f, 0.98f);
            Debug.LogWarning("[MetaShop] 缺少 UiCampShopBackground，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
        }

        static void SetZone(RectTransform rt, Vector4 zone)
        {
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);
        }
    }
}
