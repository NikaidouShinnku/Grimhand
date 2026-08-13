using System.Collections.Generic;
using Grimhand.Battle.Consumables;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    /// <summary>消耗品栏已满：Event Plate 底板 + 栏位框选择 + button1 确认 / button3 放弃。</summary>
    public sealed class ConsumableReplaceOverlayView : MonoBehaviour
    {
        const float PanelWidth = 960f;
        const float PanelHeight = 780f;
        const float IconSize = 120f;
        const float IconGap = 24f;
        const float ConfirmButtonWidth = 220f;
        const float AbandonButtonWidth = 220f;
        const int LayoutVersion = 8;

        static readonly Color TitleGold = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color SlotLabel = new(0.86f, 0.88f, 0.94f, 1f);
        static readonly Color SelectedOutline = new(0.95f, 0.82f, 0.42f, 1f);

        BattleSession _session;
        Transform _battleRoot;
        ConsumableVisualCatalogSO _consumableCatalog;
        BattleUiIconCatalogSO _uiIcons;
        RectTransform _root;
        Image _panelImage;
        Image _offerFrame;
        Text _titleText;
        Text _replaceLabel;
        RectTransform _offerIconHost;
        Image _offerIcon;
        Text _offerNameText;
        RectTransform _iconRow;
        Button _confirmButton;
        Button _abandonButton;
        bool _built;
        int _layoutVersion;
        int _selectedSlotIndex = -1;
        readonly List<Image> _slotFrames = new();

        public bool IsOpen =>
            _root != null
            && _root.gameObject.activeSelf
            && _session?.Expedition?.Run != null
            && !string.IsNullOrEmpty(_session.Expedition.Run.PendingConsumableOfferId);

        public void Initialize(
            BattleSession session,
            Transform parent,
            ConsumableVisualCatalogSO consumableCatalog,
            BattleUiIconCatalogSO uiIcons = null)
        {
            _session = session;
            _battleRoot = parent;
            _consumableCatalog = consumableCatalog;
            _uiIcons = uiIcons;
            EnsureBuilt(parent);
        }

        public void Refresh()
        {
            if (!_built || _session == null || !_session.IsExpeditionMode)
            {
                SetVisible(false);
                return;
            }

            var offerId = _session.Expedition.Run.PendingConsumableOfferId;
            if (string.IsNullOrEmpty(offerId))
            {
                SetVisible(false);
                return;
            }

            ConsumableDatabase.TryGet(offerId, out var def);
            SetVisible(true);
            // 与地图浮层同级（sorting 500），避免嵌套 Canvas 抢射线；并盖过图鉴层(120)
            if (_battleRoot != null)
                ExpeditionMapOverlayLayer.MountToFront(_root, _battleRoot);

            ClearChildren(_iconRow);
            _slotFrames.Clear();
            _selectedSlotIndex = -1;

            if (_titleText != null)
                _titleText.text = "消耗品栏已满";

            BindOfferPreview(offerId, def?.DisplayName ?? offerId);

            ConsumableInventory.EnsureInitialized(_session.Expedition.Run.ConsumableSlots);
            var slots = _session.Expedition.Run.ConsumableSlots;
            for (var i = 0; i < ConsumableInventory.MaxSlots; i++)
            {
                var index = i;
                var slotId = i < slots.Count ? slots[i] : "";
                ConsumableDatabase.TryGet(slotId, out var occupied);
                AddIconChoice(
                    slotId,
                    occupied?.DisplayName ?? (string.IsNullOrEmpty(slotId) ? "空栏" : slotId),
                    index);
            }

            FitIconRow();
            ApplyFooterButtons();
            EnsureAbandonInteractable();
            RefreshSelectionVisuals();
            RefreshConfirmInteractable();
        }

        void BindOfferPreview(string consumableId, string displayName)
        {
            if (_offerIcon == null || _offerNameText == null)
                return;

            ApplySlotFrame(_offerFrame);
            _offerNameText.text = $"获得：{displayName}";
            if (!string.IsNullOrEmpty(consumableId))
            {
                _offerIcon.sprite = _consumableCatalog?.GetIcon(consumableId);
                _offerIcon.color = _offerIcon.sprite != null
                    ? Color.white
                    : new Color(0.45f, 0.48f, 0.55f, 1f);
            }
            else
            {
                _offerIcon.sprite = null;
                _offerIcon.color = new Color(0.25f, 0.27f, 0.32f, 0.9f);
            }
        }

        void AddIconChoice(string consumableId, string displayName, int slotIndex)
        {
            var go = new GameObject("SlotIcon", typeof(RectTransform));
            go.transform.SetParent(_iconRow, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(IconSize, IconSize + 32f);

            var frameGo = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(Button));
            frameGo.transform.SetParent(go.transform, false);
            var frameRt = frameGo.GetComponent<RectTransform>();
            frameRt.anchorMin = new Vector2(0.5f, 1f);
            frameRt.anchorMax = new Vector2(0.5f, 1f);
            frameRt.pivot = new Vector2(0.5f, 1f);
            frameRt.anchoredPosition = new Vector2(0f, -4f);
            frameRt.sizeDelta = new Vector2(IconSize - 8f, IconSize - 8f);
            var frame = frameGo.GetComponent<Image>();
            ApplySlotFrame(frame);
            frame.raycastTarget = true;
            _slotFrames.Add(frame);

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(frameGo.transform, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.14f, 0.14f);
            iconRt.anchorMax = new Vector2(0.86f, 0.86f);
            iconRt.offsetMin = Vector2.zero;
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            if (!string.IsNullOrEmpty(consumableId))
            {
                icon.sprite = _consumableCatalog?.GetIcon(consumableId);
                icon.color = icon.sprite != null ? Color.white : new Color(0.4f, 0.45f, 0.55f, 1f);
            }
            else
            {
                icon.sprite = null;
                icon.color = new Color(0.2f, 0.22f, 0.28f, 0.9f);
            }

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelGo.transform.SetParent(go.transform, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0f);
            labelRt.pivot = new Vector2(0.5f, 0f);
            labelRt.anchoredPosition = new Vector2(0f, 4f);
            labelRt.sizeDelta = new Vector2(0f, 28f);
            var label = labelGo.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = SlotLabel;
            label.text = displayName;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;

            var btn = frameGo.GetComponent<Button>();
            btn.targetGraphic = frame;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnSlotClicked(slotIndex));
            UiAudioHooks.WireButton(btn);
        }

        void OnSlotClicked(int slotIndex)
        {
            _selectedSlotIndex = _selectedSlotIndex == slotIndex ? -1 : slotIndex;
            RefreshSelectionVisuals();
            RefreshConfirmInteractable();
        }

        void RefreshSelectionVisuals()
        {
            for (var i = 0; i < _slotFrames.Count; i++)
            {
                var frame = _slotFrames[i];
                if (frame == null)
                    continue;

                var selected = i == _selectedSlotIndex;
                frame.color = selected
                    ? new Color(1f, 0.96f, 0.82f, 1f)
                    : Color.white;

                var outline = frame.GetComponent<Outline>();
                if (selected)
                {
                    if (outline == null)
                        outline = frame.gameObject.AddComponent<Outline>();
                    outline.effectColor = SelectedOutline;
                    outline.effectDistance = new Vector2(3f, 3f);
                    outline.enabled = true;
                }
                else if (outline != null)
                {
                    outline.enabled = false;
                }
            }
        }

        void RefreshConfirmInteractable()
        {
            if (_confirmButton == null)
                return;

            var canConfirm = _selectedSlotIndex >= 0;
            _confirmButton.interactable = canConfirm;
            var cg = _confirmButton.GetComponent<CanvasGroup>();
            if (cg == null)
                cg = _confirmButton.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = canConfirm ? 1f : 0.45f;
            cg.interactable = canConfirm;
            cg.blocksRaycasts = true;
        }

        void EnsureAbandonInteractable()
        {
            if (_abandonButton == null)
                return;

            _abandonButton.interactable = true;
            var cg = _abandonButton.GetComponent<CanvasGroup>();
            if (cg == null)
                return;

            cg.alpha = 1f;
            cg.interactable = true;
            cg.blocksRaycasts = true;
        }

        void OnConfirmReplace()
        {
            if (_selectedSlotIndex < 0 || _session == null)
                return;

            _session.ReplaceConsumableSlot(_selectedSlotIndex);
        }

        void FitIconRow()
        {
            if (_iconRow == null || _iconRow.childCount == 0)
                return;

            var n = _iconRow.childCount;
            var totalW = n * IconSize + Mathf.Max(0, n - 1) * IconGap;
            var startX = -totalW * 0.5f + IconSize * 0.5f;
            for (var i = 0; i < n; i++)
            {
                var child = _iconRow.GetChild(i) as RectTransform;
                if (child == null)
                    continue;
                child.anchoredPosition = new Vector2(startX + i * (IconSize + IconGap), 0f);
            }
        }

        void ApplyFooterButtons()
        {
            if (_abandonButton != null)
            {
                var plate = _uiIcons != null ? _uiIcons.UiButton3 : null;
                PlanningActionButtonStyle.Apply(_abandonButton, plate, "放弃新物品", AbandonButtonWidth);
                _abandonButton.transform.SetAsLastSibling();
            }

            if (_confirmButton != null)
            {
                var plate = _uiIcons != null ? _uiIcons.UiButton1 : null;
                PlanningActionButtonStyle.Apply(_confirmButton, plate, "确认", ConfirmButtonWidth);
                _confirmButton.transform.SetAsLastSibling();
            }
        }

        void ApplySlotFrame(Image image)
        {
            if (image == null)
                return;

            var plate = _uiIcons != null ? _uiIcons.UiEventPlate : null;
            if (plate != null)
            {
                image.enabled = true;
                image.sprite = plate;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                image.enabled = true;
                image.sprite = null;
                image.color = new Color(0.07f, 0.08f, 0.12f, 0.995f);
            }
        }

        static void ClearChildren(RectTransform row)
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
        }

        void EnsureBuilt(Transform parent)
        {
            if (_built && _layoutVersion == LayoutVersion && _root != null)
            {
                ApplyEventPlate();
                ApplyFooterButtons();
                return;
            }

            if (_root != null)
                DestroyImmediate(_root.gameObject);

            _built = true;
            _layoutVersion = LayoutVersion;

            // 不挂嵌套 Canvas：由 ExpeditionMapOverlayLayer 统一排序/射线（同卡组替换/地图）
            var go = new GameObject("ConsumableReplaceOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            _root = go.GetComponent<RectTransform>();
            StretchFull(_root);
            var dim = go.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;

            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(_root, false);
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            _panelImage = panelGo.GetComponent<Image>();
            _panelImage.raycastTarget = true;
            ApplyEventPlate();

            _titleText = CreateText(panelGo.transform, "消耗品栏已满", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorRect(_titleText.rectTransform, 0.12f, 0.88f, 0.88f, 0.95f);
            _titleText.color = TitleGold;

            var offerHostGo = new GameObject("OfferPreview", typeof(RectTransform));
            offerHostGo.transform.SetParent(panelGo.transform, false);
            _offerIconHost = offerHostGo.GetComponent<RectTransform>();
            AnchorRect(_offerIconHost, 0.30f, 0.62f, 0.70f, 0.86f);

            _offerFrame = CreateImage("OfferFrame", _offerIconHost, Color.white);
            var offerFrameRt = _offerFrame.rectTransform;
            offerFrameRt.anchorMin = new Vector2(0.5f, 1f);
            offerFrameRt.anchorMax = new Vector2(0.5f, 1f);
            offerFrameRt.pivot = new Vector2(0.5f, 1f);
            offerFrameRt.anchoredPosition = new Vector2(0f, -2f);
            offerFrameRt.sizeDelta = new Vector2(112f, 112f);
            _offerFrame.raycastTarget = false;
            ApplySlotFrame(_offerFrame);

            _offerIcon = CreateImage("OfferIcon", _offerFrame.rectTransform, Color.white);
            var offerIconRt = _offerIcon.rectTransform;
            offerIconRt.anchorMin = new Vector2(0.14f, 0.14f);
            offerIconRt.anchorMax = new Vector2(0.86f, 0.86f);
            offerIconRt.offsetMin = Vector2.zero;
            offerIconRt.offsetMax = Vector2.zero;
            _offerIcon.preserveAspect = true;
            _offerIcon.raycastTarget = false;

            _offerNameText = CreateText(_offerIconHost, "", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            var offerNameRt = _offerNameText.rectTransform;
            offerNameRt.anchorMin = new Vector2(0f, 0f);
            offerNameRt.anchorMax = new Vector2(1f, 0f);
            offerNameRt.pivot = new Vector2(0.5f, 0f);
            offerNameRt.anchoredPosition = new Vector2(0f, 2f);
            offerNameRt.sizeDelta = new Vector2(0f, 28f);
            _offerNameText.color = new Color(0.55f, 0.9f, 0.65f, 1f);

            // 标签在栏位行之上，避免被槽位框遮住
            _replaceLabel = CreateText(panelGo.transform, "选择要替换的栏位", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            AnchorRect(_replaceLabel.rectTransform, 0.12f, 0.52f, 0.88f, 0.58f);
            _replaceLabel.color = new Color(0.78f, 0.72f, 0.95f, 1f);

            var rowGo = new GameObject("Icons", typeof(RectTransform));
            rowGo.transform.SetParent(panelGo.transform, false);
            _iconRow = rowGo.GetComponent<RectTransform>();
            AnchorRect(_iconRow, 0.11f, 0.24f, 0.89f, 0.50f);

            _abandonButton = CreateFooterButton(panelGo.transform, "Abandon", new Vector2(-130f, 40f), AbandonButtonWidth);
            _abandonButton.onClick.AddListener(() => _session?.AbandonConsumableOffer());
            UiAudioHooks.WireButton(_abandonButton);

            _confirmButton = CreateFooterButton(panelGo.transform, "Confirm", new Vector2(130f, 40f), ConfirmButtonWidth);
            _confirmButton.onClick.AddListener(OnConfirmReplace);
            UiAudioHooks.WireButton(_confirmButton);

            ApplyFooterButtons();
            EnsureAbandonInteractable();
            RefreshConfirmInteractable();
            go.SetActive(false);
        }

        static Button CreateFooterButton(Transform parent, string name, Vector2 anchoredPos, float width)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, PlanningActionButtonStyle.HeightForWidth(width));
            var img = go.GetComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            var btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            return btn;
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
        }

        static Text CreateText(Transform parent, string value, int size, FontStyle style, TextAnchor align)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = Color.white;
            text.text = value ?? "";
            text.raycastTarget = false;
            return text;
        }

        static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
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
