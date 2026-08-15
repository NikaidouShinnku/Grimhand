using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Expedition.Shop;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>局内流浪商人：模板底图 + goodsimageplate 商品格 + event plate 商品信息。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionShopOverlayView : MonoBehaviour
    {
        const int LayoutVersion = 3;
        const int Columns = 3;
        const int Rows = 2;
        const float ButtonHoverScale = 1.08f;

        // 相对 Panel（原点左下）——对照概念图
        // 金币略上，但仍在标题装饰线下方
        static readonly Vector4 ZoneGold = new(0.38f, 0.805f, 0.62f, 0.865f);
        // 商品 2×3：左侧区域；信息框在右侧
        const float GoodsX0 = 0.05f;
        const float GoodsY0 = 0.255f;
        const float GoodsW = 0.168f;
        const float GoodsH = 0.250f;
        const float GoodsStepX = 0.182f;
        const float GoodsStepY = 0.280f;
        // 商品信息 event plate；离开/刷新同大，分居左下/右下
        static readonly Vector4 ZoneInfo = new(0.62f, 0.255f, 0.94f, 0.545f);
        static readonly Vector4 ZoneLeave = new(0.03f, 0.04f, 0.20f, 0.13f);
        static readonly Vector4 ZoneRefresh = new(0.80f, 0.04f, 0.97f, 0.13f);

        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color BodyText = new(0.88f, 0.90f, 0.94f, 1f);
        static readonly Color AffordablePlateTint = new(1f, 0.97f, 0.82f, 1f);
        static readonly Color AffordableOutline = new(1f, 0.86f, 0.32f, 1f);

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        ConsumableVisualCatalogSO _consumableCatalog;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        CardView _cardPrefab;

        RectTransform _root;
        RectTransform _panel;
        Image _panelBackground;
        Text _goldText;
        Image _goldIcon;
        Text _infoTitle;
        Text _infoBody;
        Text _refreshLabel;
        Image _refreshImage;
        Button _refreshButton;
        CanvasGroup _refreshDim;
        Button _leaveButton;
        InventoryTooltipView _tooltip;
        readonly List<GameObject> _slotObjects = new();
        int _layoutVersion;
        bool _built;
        bool _wasVisible;

        public void Initialize(
            BattleSession session,
            Transform parent,
            BattleUiIconCatalogSO icons,
            CardView cardPrefab,
            CardVisualCatalogSO cardCatalog,
            CharacterVisualCatalogSO characterVisuals,
            RelicVisualCatalogSO relicCatalog,
            ConsumableVisualCatalogSO consumableCatalog,
            Dictionary<string, CardDefinitionSO> definitions)
        {
            _session = session;
            _icons = icons;
            _cardPrefab = cardPrefab;
            _cardCatalog = cardCatalog;
            _characterVisuals = characterVisuals;
            _relicCatalog = relicCatalog;
            _consumableCatalog = consumableCatalog;
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

            var show = _session.Expedition.Run.Phase == ExpeditionPhase.ShopVisit
                       && _session.Expedition.Run.PendingCardPackOffer == null;
            if (show && !_wasVisible)
                GameAudioService.Instance.PlayUiShopEnter();
            _wasVisible = show;
            SetVisible(show);
            if (!show)
                return;

            _root.SetAsLastSibling();
            FitPanelToScreen();
            RefreshContent();
        }

        void RefreshContent()
        {
            ClearSlots();
            ResetInfoPanel();

            var run = _session.Expedition.Run;
            if (_goldText != null)
                _goldText.text = run.Gold.ToString();
            if (_goldIcon != null && _icons != null)
            {
                _goldIcon.sprite = _icons.GoldIcon != null ? _icons.GoldIcon : _icons.CampGoldIcon;
                _goldIcon.color = _goldIcon.sprite != null ? Color.white : Color.clear;
            }

            var refreshCost = run.Shop.NextRefreshCost;
            if (_refreshLabel != null)
                _refreshLabel.text = refreshCost <= 0 ? "刷新 免费" : $"刷新  {refreshCost}";
            ApplyRefreshAffordable(run.Gold >= refreshCost);

            var offers = run.Shop.Offers;
            for (var i = 0; i < offers.Count; i++)
            {
                var col = i % Columns;
                var rowFromTop = i / Columns;
                var rowFromBottom = Rows - 1 - rowFromTop;
                var zone = new Vector4(
                    GoodsX0 + col * GoodsStepX,
                    GoodsY0 + rowFromBottom * GoodsStepY,
                    GoodsX0 + col * GoodsStepX + GoodsW,
                    GoodsY0 + rowFromBottom * GoodsStepY + GoodsH);
                BuildOfferSlot(i, offers[i], zone, run.Gold);
            }
        }

        void BuildOfferSlot(int index, ShopOffer offer, Vector4 zone, int gold)
        {
            var slotGo = new GameObject($"ShopSlot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            slotGo.transform.SetParent(_panel, false);
            var rt = slotGo.GetComponent<RectTransform>();
            SetZone(rt, zone);

            var plate = slotGo.GetComponent<Image>();
            plate.color = Color.white;
            plate.preserveAspect = false;
            if (_icons != null && _icons.UiMerchantGoodsImagePlate != null)
            {
                plate.sprite = _icons.UiMerchantGoodsImagePlate;
                plate.type = Image.Type.Simple;
            }
            else
            {
                plate.sprite = null;
                plate.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);
            }

            var kindLabel = CreateText(slotGo.transform, "Kind",
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f), 15, TextAnchor.UpperCenter);
            kindLabel.text = OfferKindLabel(offer);
            kindLabel.color = TitleGold;
            kindLabel.raycastTarget = false;

            var contentRoot = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRoot.SetParent(slotGo.transform, false);
            contentRoot.anchorMin = new Vector2(0.12f, 0.28f);
            contentRoot.anchorMax = new Vector2(0.88f, 0.80f);
            contentRoot.offsetMin = Vector2.zero;
            contentRoot.offsetMax = Vector2.zero;

            switch (offer.Kind)
            {
                case ShopOfferKind.CardPack:
                    BuildCardPackPreview(contentRoot, offer);
                    break;
                case ShopOfferKind.Relic:
                    BuildRelicPreview(contentRoot, offer);
                    break;
                default:
                    BuildConsumablePreview(contentRoot, offer);
                    break;
            }

            var priceRow = new GameObject("Price", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            priceRow.transform.SetParent(slotGo.transform, false);
            var priceRt = priceRow.GetComponent<RectTransform>();
            priceRt.anchorMin = new Vector2(0.08f, 0.04f);
            priceRt.anchorMax = new Vector2(0.92f, 0.22f);
            priceRt.offsetMin = Vector2.zero;
            priceRt.offsetMax = Vector2.zero;
            var priceLayout = priceRow.GetComponent<HorizontalLayoutGroup>();
            priceLayout.childAlignment = TextAnchor.MiddleCenter;
            priceLayout.spacing = 6f;
            priceLayout.childControlWidth = false;
            priceLayout.childControlHeight = true;
            priceLayout.childForceExpandWidth = false;
            priceLayout.childForceExpandHeight = true;
            // 「已售出」略偏右（相对正中）
            if (offer.Sold)
                priceLayout.padding = new RectOffset(12, 0, 0, 0);

            if (!offer.Sold)
            {
                var iconGo = new GameObject("GoldIcon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                iconGo.transform.SetParent(priceRow.transform, false);
                iconGo.GetComponent<LayoutElement>().preferredWidth = 22f;
                iconGo.GetComponent<LayoutElement>().preferredHeight = 22f;
                var gIcon = iconGo.GetComponent<Image>();
                gIcon.preserveAspect = true;
                gIcon.raycastTarget = false;
                gIcon.sprite = _icons != null
                    ? (_icons.GoldIcon != null ? _icons.GoldIcon : _icons.CampGoldIcon)
                    : null;
                gIcon.color = gIcon.sprite != null ? Color.white : Color.clear;
            }

            var priceTextGo = new GameObject("PriceText", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            priceTextGo.transform.SetParent(priceRow.transform, false);
            priceTextGo.GetComponent<LayoutElement>().preferredWidth = offer.Sold ? 72f : 100f;
            var priceText = priceTextGo.GetComponent<Text>();
            StyleText(priceText, 18, offer.Sold ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
            priceText.text = offer.Sold ? "已售出" : offer.Price.ToString();
            priceText.raycastTarget = false;

            var btn = slotGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var canBuy = !offer.Sold && gold >= offer.Price
                         && _session.Expedition.Run.PendingCardPackOffer == null
                         && _session.Expedition.Run.PendingCardOffer == null;
            btn.interactable = canBuy;
            var slotIndex = index;
            var offerKind = offer.Kind;
            btn.onClick.AddListener(() =>
            {
                if (!_session.BuyShopOffer(slotIndex))
                    return;
                PlayShopPurchaseSfx(offerKind);
            });
            UiAudioHooks.WireButton(btn);

            if (canBuy)
            {
                plate.color = AffordablePlateTint;
                var outline = slotGo.AddComponent<Outline>();
                outline.effectColor = AffordableOutline;
                outline.effectDistance = new Vector2(5f, -5f);
                outline.useGraphicAlpha = true;
            }
            else
            {
                var dim = slotGo.AddComponent<CanvasGroup>();
                dim.alpha = offer.Sold ? 0.42f : 0.72f;
            }

            WireOfferInfo(slotGo, offer);
            _slotObjects.Add(slotGo);
        }

        void WireOfferInfo(GameObject slotGo, ShopOffer offer)
        {
            ResolveOfferInfo(offer, out var title, out var body);
            _tooltip?.BindHover(slotGo, title, body);

            var trigger = slotGo.GetComponent<EventTrigger>() ?? slotGo.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowInfo(title, body));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => ResetInfoPanel());
            trigger.triggers.Add(exit);
        }

        void ResolveOfferInfo(ShopOffer offer, out string title, out string body)
        {
            switch (offer.Kind)
            {
                case ShopOfferKind.CardPack:
                    title = CardPackIds.GetDisplayName(offer.CardPackId);
                    body = "购买后开启三选一，选一张加入卡组或放弃。";
                    return;
                case ShopOfferKind.Relic:
                    if (RelicDatabase.TryGet(offer.RelicId, out var relic))
                    {
                        title = relic.DisplayName;
                        body = RelicDescriptionFormatter.Format(relic, 0);
                        return;
                    }

                    title = offer.RelicDisplayName;
                    body = "";
                    return;
                default:
                    if (ConsumableDatabase.TryGet(offer.ConsumableId, out var consumable))
                    {
                        title = consumable.DisplayName;
                        body = consumable.Description ?? "";
                        return;
                    }

                    title = offer.ConsumableDisplayName;
                    body = "";
                    return;
            }
        }

        void ShowInfo(string title, string body)
        {
            if (_infoTitle != null)
                _infoTitle.text = string.IsNullOrEmpty(title) ? "商品信息" : title;
            if (_infoBody != null)
                _infoBody.text = body ?? "";
        }

        void ResetInfoPanel()
        {
            if (_infoTitle != null)
                _infoTitle.text = "商品信息";
            if (_infoBody != null)
                _infoBody.text = "将鼠标移至商品上\n查看详细信息。";
        }

        static void PlayShopPurchaseSfx(ShopOfferKind kind)
        {
            switch (kind)
            {
                case ShopOfferKind.CardPack:
                    GameAudioService.Instance.PlayUiCardPackOpen();
                    break;
                case ShopOfferKind.Relic:
                    GameAudioService.Instance.PlayUiRelicsAcquire();
                    break;
                default:
                    GameAudioService.Instance.PlayUiConsumableAcquire();
                    break;
            }
        }

        void BuildRelicPreview(RectTransform parent, ShopOffer offer)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            Stretch(iconGo.GetComponent<RectTransform>(), 0.1f, 0.2f, 0.9f, 1f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = _relicCatalog?.GetIcon(offer.RelicId);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = icon.sprite != null ? Color.white : new Color(0.72f, 0.86f, 1f, 1f);

            var name = CreateText(parent, "Name", new Vector2(0.02f, 0f), new Vector2(0.98f, 0.22f), 15,
                TextAnchor.MiddleCenter);
            name.text = offer.RelicDisplayName;
            name.raycastTarget = false;
        }

        void BuildConsumablePreview(RectTransform parent, ShopOffer offer)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            Stretch(iconGo.GetComponent<RectTransform>(), 0.1f, 0.2f, 0.9f, 1f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = _consumableCatalog?.GetIcon(offer.ConsumableId);
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            icon.raycastTarget = false;
            icon.color = icon.sprite != null ? Color.white : new Color(0.85f, 0.9f, 0.95f, 1f);

            var name = CreateText(parent, "Name", new Vector2(0.02f, 0f), new Vector2(0.98f, 0.22f), 15,
                TextAnchor.MiddleCenter);
            name.text = offer.ConsumableDisplayName;
            name.raycastTarget = false;
        }

        void BuildCardPackPreview(RectTransform parent, ShopOffer offer)
        {
            var iconGo = new GameObject("PackIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            Stretch(iconGo.GetComponent<RectTransform>(), 0.12f, 0.18f, 0.88f, 1f);
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = CardPackVisuals.GetPackIcon(offer.CardPackId, _icons);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = icon.sprite != null ? Color.white : new Color(0.85f, 0.75f, 0.45f, 1f);

            var name = CreateText(parent, "Name", new Vector2(0.02f, 0f), new Vector2(0.98f, 0.2f), 15,
                TextAnchor.MiddleCenter);
            name.text = CardPackIds.GetDisplayName(offer.CardPackId);
            name.raycastTarget = false;
        }

        static string OfferKindLabel(ShopOffer offer) =>
            offer.Kind switch
            {
                ShopOfferKind.CardPack => CardPackIds.GetDisplayName(offer.CardPackId),
                ShopOfferKind.Relic => $"遗物·{RelicRarityLabel(offer.RelicRarity)}",
                ShopOfferKind.Consumable => "消耗品",
                _ => ""
            };

        static string RelicRarityLabel(RelicRarity rarity) =>
            rarity switch
            {
                RelicRarity.Epic => "史诗",
                RelicRarity.Rare => "稀有",
                _ => "普通"
            };

        void ClearSlots()
        {
            foreach (var go in _slotObjects)
            {
                if (go != null)
                    Destroy(go);
            }

            _slotObjects.Clear();
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
            if (_built && _layoutVersion == LayoutVersion && _root != null)
            {
                ApplyPanelBackground();
                return;
            }

            if (_root != null)
                Destroy(_root.gameObject);

            _built = true;
            _layoutVersion = LayoutVersion;

            var overlayGo = new GameObject("ExpeditionShopOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(parent, false);
            _root = overlayGo.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            _panel = panelGo.GetComponent<RectTransform>();
            // 模板 1619×971，居中铺满可视区
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            FitPanelToScreen();
            _panelBackground = panelGo.GetComponent<Image>();
            ApplyPanelBackground();

            BuildGoldRow();
            BuildInfoPanel();
            BuildLeaveButton();
            BuildRefreshButton();

            overlayGo.SetActive(false);

            _tooltip = overlayGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_root, _icons);
        }

        void FitPanelToScreen()
        {
            if (_panel == null)
                return;

            var parentRt = _panel.parent as RectTransform;
            var parentW = parentRt != null ? parentRt.rect.width : Screen.width;
            var parentH = parentRt != null ? parentRt.rect.height : Screen.height;
            if (parentW < 2f) parentW = 1920f;
            if (parentH < 2f) parentH = 1080f;

            const float aspect = 1619f / 971f;
            var w = parentW * 0.96f;
            var h = w / aspect;
            if (h > parentH * 0.96f)
            {
                h = parentH * 0.96f;
                w = h * aspect;
            }

            _panel.sizeDelta = new Vector2(w, h);
            _panel.anchoredPosition = Vector2.zero;
        }

        void BuildGoldRow()
        {
            var go = new GameObject("GoldRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(_panel, false);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, ZoneGold);
            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8f;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            iconGo.transform.SetParent(go.transform, false);
            iconGo.GetComponent<LayoutElement>().preferredWidth = 28f;
            iconGo.GetComponent<LayoutElement>().preferredHeight = 28f;
            _goldIcon = iconGo.GetComponent<Image>();
            _goldIcon.preserveAspect = true;
            _goldIcon.raycastTarget = false;

            var textGo = new GameObject("Amount", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            textGo.transform.SetParent(go.transform, false);
            textGo.GetComponent<LayoutElement>().preferredWidth = 120f;
            _goldText = textGo.GetComponent<Text>();
            StyleText(_goldText, 26, TextAnchor.MiddleLeft);
            _goldText.color = TitleGold;
            _goldText.raycastTarget = false;
        }

        void BuildInfoPanel()
        {
            var go = new GameObject("InfoPlate", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_panel, false);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, ZoneInfo);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = false;
            if (_icons != null && _icons.UiEventPlate != null)
            {
                img.sprite = _icons.UiEventPlate;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
            }
            else
            {
                img.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
            }

            _infoTitle = CreateText(go.transform, "Title",
                new Vector2(0.1f, 0.72f), new Vector2(0.9f, 0.92f), 22, TextAnchor.MiddleCenter);
            _infoTitle.color = TitleGold;
            _infoTitle.raycastTarget = false;

            _infoBody = CreateText(go.transform, "Body",
                new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.68f), 16, TextAnchor.UpperCenter);
            _infoBody.color = BodyText;
            _infoBody.raycastTarget = false;
            _infoBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            _infoBody.verticalOverflow = VerticalWrapMode.Truncate;
            ResetInfoPanel();
        }

        void BuildLeaveButton()
        {
            _leaveButton = CreateSpriteButton(_panel, "Leave", ZoneLeave, _icons != null ? _icons.UiButton3 : null, "离开");
            _leaveButton.onClick.AddListener(() => _session.LeaveShop());
            UiAudioHooks.WireButton(_leaveButton);
            WireHoverScale(_leaveButton.transform as RectTransform);
        }

        void BuildRefreshButton()
        {
            _refreshButton = CreateSpriteButton(_panel, "Refresh", ZoneRefresh,
                _icons != null ? _icons.UiButton2 : null, "");
            _refreshImage = _refreshButton.GetComponent<Image>();
            _refreshDim = _refreshButton.gameObject.GetComponent<CanvasGroup>()
                          ?? _refreshButton.gameObject.AddComponent<CanvasGroup>();
            _refreshLabel = CreateText(_refreshButton.transform, "Label",
                Vector2.zero, Vector2.one, 20, TextAnchor.MiddleCenter);
            _refreshLabel.raycastTarget = false;
            var labelRt = _refreshLabel.rectTransform;
            labelRt.offsetMin = new Vector2(10f, 6f);
            labelRt.offsetMax = new Vector2(-10f, -8f);
            _refreshButton.onClick.AddListener(() => _session.RefreshShop());
            UiAudioHooks.WireButton(_refreshButton);
            WireHoverScale(_refreshButton.transform as RectTransform);
        }

        void ApplyRefreshAffordable(bool canRefresh)
        {
            if (_refreshButton != null)
                _refreshButton.interactable = canRefresh;

            if (_refreshDim != null)
            {
                _refreshDim.alpha = canRefresh ? 1f : 0.38f;
                _refreshDim.interactable = canRefresh;
                _refreshDim.blocksRaycasts = canRefresh;
            }

            if (_refreshImage != null)
                _refreshImage.color = canRefresh
                    ? Color.white
                    : new Color(0.45f, 0.45f, 0.48f, 1f);

            if (_refreshLabel != null)
                _refreshLabel.color = canRefresh
                    ? Color.white
                    : new Color(0.55f, 0.55f, 0.58f, 1f);

            if (!canRefresh && _refreshButton != null)
                _refreshButton.transform.localScale = Vector3.one;
        }

        void ApplyPanelBackground()
        {
            if (_panelBackground == null)
                return;

            var sprite = _icons != null ? _icons.UiExpeditionShopBackground : null;
            if (sprite == null && _icons != null)
                sprite = _icons.ShopBackground;

            if (sprite != null)
            {
                _panelBackground.sprite = sprite;
                _panelBackground.color = Color.white;
                _panelBackground.type = Image.Type.Simple;
                _panelBackground.preserveAspect = false;
                return;
            }

            _panelBackground.sprite = null;
            _panelBackground.color = new Color(0.09f, 0.1f, 0.14f, 0.98f);
        }

        static Button CreateSpriteButton(Transform parent, string name, Vector4 zone, Sprite sprite, string label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            SetZone(rt, zone);
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Simple;
                img.preserveAspect = false;
            }
            else
            {
                img.color = new Color(0.16f, 0.18f, 0.24f, 0.98f);
            }

            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;

            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText(go.transform, "Label", Vector2.zero, Vector2.one, 22, TextAnchor.MiddleCenter);
                text.text = label;
                text.raycastTarget = false;
                text.rectTransform.offsetMin = new Vector2(8f, 4f);
                text.rectTransform.offsetMax = new Vector2(-8f, -6f);
            }

            return btn;
        }

        static void WireHoverScale(RectTransform rt)
        {
            if (rt == null)
                return;

            var trigger = rt.gameObject.GetComponent<EventTrigger>() ?? rt.gameObject.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => rt.localScale = Vector3.one * ButtonHoverScale);
            trigger.triggers.Add(enter);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => rt.localScale = Vector3.one);
            trigger.triggers.Add(exit);
        }

        static void SetZone(RectTransform rt, Vector4 zone)
        {
            rt.anchorMin = new Vector2(zone.x, zone.y);
            rt.anchorMax = new Vector2(zone.z, zone.w);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void Stretch(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static Text CreateText(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            int fontSize,
            TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var text = go.GetComponent<Text>();
            StyleText(text, fontSize, alignment);
            return text;
        }

        static Text CreateText(Transform parent, string name, Vector4 zone, int fontSize, TextAnchor alignment)
        {
            var text = CreateText(parent, name, Vector2.zero, Vector2.one, fontSize, alignment);
            SetZone(text.rectTransform, zone);
            return text;
        }

        static void StyleText(Text text, int fontSize, TextAnchor alignment)
        {
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void OnRectTransformDimensionsChange()
        {
            if (_built)
                FitPanelToScreen();
        }
    }
}
