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
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>流浪商人：6 格商品、购买、刷新、离开。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionShopOverlayView : MonoBehaviour
    {
        const float CardScale = 0.98f;
        const float CellWidth = 460f;
        const float CellHeight = 430f;
        const float CellGapX = 32f;
        const float CellGapY = 28f;

        BattleSession _session;
        BattleUiIconCatalogSO _icons;
        CardVisualCatalogSO _cardCatalog;
        CharacterVisualCatalogSO _characterVisuals;
        RelicVisualCatalogSO _relicCatalog;
        ConsumableVisualCatalogSO _consumableCatalog;
        Dictionary<string, CardDefinitionSO> _definitions = new();
        CardView _cardPrefab;

        RectTransform _root;
        RectTransform _grid;
        Image _panelBackground;
        Text _titleText;
        Text _goldText;
        Text _messageText;
        Text _refreshLabel;
        Button _refreshButton;
        Button _leaveButton;
        InventoryTooltipView _tooltip;
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
            RefreshContent();
        }

        void RefreshContent()
        {
            ClearGrid();

            var run = _session.Expedition.Run;
            _titleText.text = "流浪商人";
            _goldText.text = $"金币：{run.Gold}";
            _messageText.text = string.IsNullOrEmpty(run.LastEventMessage)
                ? "点击商品购买；刷新仅更换未售出的栏位（首次刷新免费）。"
                : run.LastEventMessage;

            var refreshCost = run.Shop.NextRefreshCost;
            _refreshLabel.text = $"刷新 {refreshCost}";
            _refreshButton.interactable = run.Gold >= refreshCost;

            var offers = run.Shop.Offers;
            var columns = 3;
            var rows = Mathf.Max(1, (offers.Count + columns - 1) / columns);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_grid);

            var gridWidth = _grid.rect.width;
            var gridHeight = _grid.rect.height;
            if (gridWidth <= 1f || gridHeight <= 1f)
            {
                gridWidth = columns * CellWidth + (columns - 1) * CellGapX;
                gridHeight = rows * CellHeight + (rows - 1) * CellGapY;
            }

            var cellWidth = (gridWidth - (columns - 1) * CellGapX) / columns;
            var cellHeight = (gridHeight - (rows - 1) * CellGapY) / rows;
            var cardScale = Mathf.Clamp(cellHeight / 480f * CardScale, 0.72f, 1.12f);
            var startX = -((columns - 1) * (cellWidth + CellGapX)) * 0.5f;
            var startY = ((rows - 1) * (cellHeight + CellGapY)) * 0.5f;

            for (var i = 0; i < offers.Count; i++)
            {
                var col = i % columns;
                var row = i / columns;
                var x = startX + col * (cellWidth + CellGapX);
                var y = startY - row * (cellHeight + CellGapY);
                BuildOfferSlot(i, offers[i], new Vector2(x, y), run.Gold, cellWidth, cellHeight, cardScale);
            }
        }

        void BuildOfferSlot(int index, ShopOffer offer, Vector2 pos, int gold, float cellWidth, float cellHeight, float cardScale)
        {
            var slotGo = new GameObject($"ShopSlot_{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            slotGo.transform.SetParent(_grid, false);
            var rt = slotGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(cellWidth, cellHeight);

            var bg = slotGo.GetComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.18f, 0.96f);

            var kindLabel = CreateText(slotGo.transform, "Kind", new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.98f), 20,
                TextAnchor.UpperLeft);
            kindLabel.text = OfferKindLabel(offer);

            var contentRoot = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            contentRoot.SetParent(slotGo.transform, false);
            contentRoot.anchorMin = new Vector2(0.08f, 0.22f);
            contentRoot.anchorMax = new Vector2(0.92f, 0.86f);
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

            var priceText = CreateText(slotGo.transform, "Price", new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.16f), 24,
                TextAnchor.MiddleCenter);
            priceText.text = offer.Sold ? "已售出" : $"{offer.Price} 金币";

            var btn = slotGo.GetComponent<Button>();
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
            ScrollRectNavigation.WireForwarding(slotGo);

            if (!canBuy)
            {
                var dim = slotGo.AddComponent<CanvasGroup>();
                dim.alpha = offer.Sold ? 0.42f : 0.72f;
            }

            BindOfferTooltip(slotGo, offer);
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

        void BindOfferTooltip(GameObject slotGo, ShopOffer offer)
        {
            if (_tooltip == null || slotGo == null)
                return;

            switch (offer.Kind)
            {
                case ShopOfferKind.CardPack:
                    _tooltip.BindHover(
                        slotGo,
                        CardPackIds.GetDisplayName(offer.CardPackId),
                        "购买后开启三选一，选一张加入卡组或放弃。",
                        showTitle: true);
                    break;
                case ShopOfferKind.Relic:
                    if (RelicDatabase.TryGet(offer.RelicId, out var relic))
                        _tooltip.BindHover(slotGo, relic.DisplayName, relic.Description);
                    break;
                default:
                    if (ConsumableDatabase.TryGet(offer.ConsumableId, out var consumable))
                        _tooltip.BindHover(slotGo, consumable.DisplayName, consumable.Description);
                    break;
            }
        }

        void BuildRelicPreview(RectTransform parent, ShopOffer offer)
        {
            RelicDatabase.TryGet(offer.RelicId, out var relic);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.15f, 0.28f);
            iconRt.anchorMax = new Vector2(0.85f, 0.95f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = _relicCatalog?.GetIcon(offer.RelicId);
            icon.preserveAspect = true;
            icon.color = icon.sprite != null ? Color.white : new Color(0.72f, 0.86f, 1f, 1f);

            var name = CreateText(parent, "Name", new Vector2(0.05f, 0f), new Vector2(0.95f, 0.22f), 20, TextAnchor.MiddleCenter);
            name.text = offer.RelicDisplayName;
        }

        void BuildConsumablePreview(RectTransform parent, ShopOffer offer)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.15f, 0.24f);
            iconRt.anchorMax = new Vector2(0.85f, 0.94f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = _consumableCatalog?.GetIcon(offer.ConsumableId);
            icon.preserveAspect = true;
            icon.type = Image.Type.Simple;
            icon.color = icon.sprite != null ? Color.white : new Color(0.85f, 0.9f, 0.95f, 1f);
            icon.raycastTarget = false;

            var name = CreateText(parent, "Name", new Vector2(0.05f, 0f), new Vector2(0.95f, 0.2f), 20, TextAnchor.MiddleCenter);
            name.text = offer.ConsumableDisplayName;
        }

        void BuildCardPackPreview(RectTransform parent, ShopOffer offer)
        {
            var iconGo = new GameObject("PackIcon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.2f, 0.28f);
            iconRt.anchorMax = new Vector2(0.8f, 0.88f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = CardPackVisuals.GetPackIcon(offer.CardPackId, _icons);
            icon.preserveAspect = true;
            icon.color = icon.sprite != null ? Color.white : new Color(0.85f, 0.75f, 0.45f, 1f);
            icon.raycastTarget = false;

            var name = CreateText(parent, "Name", new Vector2(0.05f, 0f), new Vector2(0.95f, 0.2f), 20, TextAnchor.MiddleCenter);
            name.text = CardPackIds.GetDisplayName(offer.CardPackId);
        }

        static string OfferKindLabel(ShopOffer offer) =>
            offer.Kind switch
            {
                ShopOfferKind.CardPack => CardPackIds.GetDisplayName(offer.CardPackId),
                ShopOfferKind.Relic => $"遗物 · {RelicRarityLabel(offer.RelicRarity)}",
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

        void ClearGrid()
        {
            if (_grid == null)
                return;

            foreach (Transform child in _grid)
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

            var overlayGo = new GameObject("ExpeditionShopOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(parent, false);
            _root = overlayGo.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            overlayGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.62f);

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.02f, 0.03f);
            panelRt.anchorMax = new Vector2(0.98f, 0.97f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            _panelBackground = panelGo.GetComponent<Image>();
            ApplyPanelBackground();

            _titleText = CreateText(panelGo.transform, "Title", new Vector2(0.04f, 0.91f), new Vector2(0.96f, 0.99f), 42,
                TextAnchor.MiddleCenter);
            _titleText.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            _goldText = CreateText(panelGo.transform, "Gold", new Vector2(0.04f, 0.85f), new Vector2(0.96f, 0.91f), 28,
                TextAnchor.MiddleCenter);

            _messageText = CreateText(panelGo.transform, "Message", new Vector2(0.06f, 0.79f), new Vector2(0.94f, 0.85f), 22,
                TextAnchor.MiddleCenter);
            _messageText.color = new Color(0.78f, 0.82f, 0.9f, 1f);

            var gridGo = new GameObject("Grid", typeof(RectTransform));
            gridGo.transform.SetParent(panelGo.transform, false);
            _grid = gridGo.GetComponent<RectTransform>();
            _grid.anchorMin = new Vector2(0.04f, 0.14f);
            _grid.anchorMax = new Vector2(0.96f, 0.78f);
            _grid.offsetMin = Vector2.zero;
            _grid.offsetMax = Vector2.zero;

            _leaveButton = CreateFooterButton(panelGo.transform, "Leave", new Vector2(-320f, 36f), "离开", null);
            _leaveButton.onClick.AddListener(() => _session.LeaveShop());
            UiAudioHooks.WireButton(_leaveButton);

            _refreshButton = CreateFooterButton(panelGo.transform, "Refresh", new Vector2(320f, 36f), "", _icons?.ShopRefreshIcon);
            _refreshLabel = CreateText(_refreshButton.transform, "Label", new Vector2(0.42f, 0.1f), new Vector2(0.94f, 0.9f), 22,
                TextAnchor.MiddleLeft);
            _refreshButton.onClick.AddListener(() => _session.RefreshShop());
            UiAudioHooks.WireButton(_refreshButton);

            overlayGo.SetActive(false);

            _tooltip = overlayGo.AddComponent<InventoryTooltipView>();
            _tooltip.Initialize(_root);
        }

        void ApplyPanelBackground()
        {
            if (_panelBackground == null)
                return;

            var sprite = _icons?.ShopBackground;
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

        static Button CreateFooterButton(Transform parent, string name, Vector2 pos, string fallbackLabel, Sprite icon)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280f, 64f);
            go.GetComponent<Image>().color = new Color(0.16f, 0.18f, 0.24f, 0.98f);

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var iconRt = iconGo.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.08f, 0.15f);
                iconRt.anchorMax = new Vector2(0.38f, 0.85f);
                iconRt.offsetMin = Vector2.zero;
                iconRt.offsetMax = Vector2.zero;
                var img = iconGo.GetComponent<Image>();
                img.sprite = icon;
                img.preserveAspect = true;
                img.color = Color.white;
            }
            else
            {
                var label = CreateText(go.transform, "Label", Vector2.zero, Vector2.one, 24, TextAnchor.MiddleCenter);
                label.text = fallbackLabel;
            }

            return go.GetComponent<Button>();
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
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
    }
}
