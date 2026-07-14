using System;
using System.Collections;
using Grimhand.Battle.Model;
using Grimhand.Content;
using Grimhand.Expedition;
using Grimhand.Presentation;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        const float NormalScale = 1f;
        const float HoverScale = 1.14f;
        const float ScaleLerpDuration = 0.1f;

        static readonly Color SelectedHighlightColor = new(1f, 1f, 1f, 0.26f);

        [SerializeField] Image frameImage;
        [SerializeField] Image artImage;
        [SerializeField] Image iconImage;
        [SerializeField] Image pollutedOverlay;
        [SerializeField] Image selectedHighlight;
        [SerializeField] Image selectedOutline;
        [SerializeField] Image costIconImage;
        [SerializeField] Text costText;
        [SerializeField] Text nameText;
        [SerializeField] Text statsText;
        [SerializeField] Text ownerText;
        [SerializeField] Text orderBadgeText;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Button button;

        const string ScaleRootName = "CardScaleRoot";
        const string LegacyPortraitClipName = "PortraitClip";
        static readonly Vector2 NamePanelAnchorMin = new(0.10f, 0.36f);
        static readonly Vector2 NamePanelAnchorMax = new(0.90f, 0.47f);
        static readonly Vector2 StatsPanelAnchorMin = new(0.10f, 0.06f);
        static readonly Vector2 StatsPanelAnchorMax = new(0.90f, 0.36f);

        const int NameFontSize = 15;
        const int DescriptionFontNormal = 13;
        const int DescriptionFontHover = 15;

        int _instanceId;
        bool _selected;
        bool _hovered;
        bool _interactable = true;
        Coroutine _scaleRoutine;
        RectTransform _scaleRoot;
        Action<int> _onClick;
        Action<int> _onQuickStart;
        Action<CardInstanceState, RectTransform> _onHoverEnter;
        Action _onHoverExit;
        string _statsBaseLine = "";
        bool _isQuickStart;

        public int InstanceId => _instanceId;
        public CardInstanceState CurrentCard { get; private set; }

        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyVisualState(immediate: true);
        }

        void Awake()
        {
            RemoveStaleHoverCanvas();
            EnsureScaleRoot();
            EnsureCardVisuals();
            EnsurePortraitOverlay();
            EnsureLowerPanelLayout();
        }

        public void BindWithCard(
            CardInstanceState card,
            CardVisual visual,
            bool selected,
            bool polluted,
            bool interactable,
            string orderBadge,
            string statsLine,
            BattleUiIconCatalogSO uiIcons,
            CharacterVisualCatalogSO characterVisuals,
            Action<int> onClick,
            Action<CardInstanceState, RectTransform> onHoverEnter,
            Action onHoverExit,
            int? displayCost = null,
            Action<int> onQuickStart = null)
        {
            EnsureScaleRoot();
            EnsureCardVisuals();
            EnsurePortraitOverlay();
            EnsureLowerPanelLayout();

            var wasHovered = _hovered && _instanceId == card.InstanceId;

            CurrentCard = card;
            _instanceId = card.InstanceId;
            _selected = selected;
            _interactable = interactable;
            _onClick = onClick;
            _onQuickStart = onQuickStart;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;
            _statsBaseLine = statsLine ?? "";
            _isQuickStart = card != null && card.Keywords != null && card.Keywords.Contains("quick_start");

            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(HandlePrimaryClick);
                button.interactable = interactable;
            }

            if (frameImage != null)
            {
                frameImage.enabled = true;
                frameImage.sprite = visual.Frame;
                frameImage.preserveAspect = false;
                frameImage.color = visual.Frame != null ? Color.white : new Color(0.18f, 0.2f, 0.28f, 1f);
            }

            if (artImage != null)
            {
                var portrait = characterVisuals != null
                    ? characterVisuals.GetCardPortrait(card.OwnerCharacterId)
                    : null;
                var art = portrait ?? visual.Art;
                artImage.enabled = true;
                artImage.sprite = art;
                artImage.color = art != null ? Color.white : new Color(0.25f, 0.27f, 0.35f, 1f);
                ApplyPortraitPresentation();
            }

            if (iconImage != null)
            {
                iconImage.enabled = visual.Icon != null;
                iconImage.sprite = visual.Icon;
            }

            if (costIconImage != null)
            {
                var energyIcon = uiIcons != null ? uiIcons.EnergyIcon : null;
                costIconImage.sprite = energyIcon;
                costIconImage.enabled = energyIcon != null;
                costIconImage.preserveAspect = true;
                costIconImage.color = Color.white;
            }

            if (costText != null)
            {
                if (displayCost.HasValue)
                    costText.text = displayCost.Value.ToString();
                else if (card != null && card.Keywords != null && card.Keywords.Contains("x_cost"))
                    costText.text = "X";
                else
                    costText.text = card.Cost.ToString();
            }

            if (nameText != null)
            {
                var name = CardUpgradeRules.FormatDisplayName(card.DisplayName, card.UpgradeLevel);
                nameText.text = polluted ? "[污] " + name : name;
                nameText.color = Color.white;
                nameText.fontStyle = FontStyle.Bold;
                nameText.fontSize = NameFontSize;
            }

            if (statsText != null)
            {
                statsText.gameObject.SetActive(true);
                statsText.text = _statsBaseLine;
                statsText.color = new Color(0.95f, 0.92f, 0.86f, 1f);
                statsText.fontStyle = FontStyle.Normal;
                statsText.fontSize = wasHovered && interactable ? DescriptionFontHover : DescriptionFontNormal;
                statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
                statsText.verticalOverflow = VerticalWrapMode.Overflow;
            }

            if (ownerText != null)
                ownerText.text = "";

            if (orderBadgeText != null)
            {
                var showBadge = selected && !string.IsNullOrEmpty(orderBadge);
                orderBadgeText.gameObject.SetActive(showBadge);
                orderBadgeText.text = orderBadge;
            }

            if (pollutedOverlay != null)
                pollutedOverlay.enabled = polluted;

            if (canvasGroup != null)
                canvasGroup.alpha = polluted ? 0.55f : interactable ? 1f : 0.72f;

            if (button != null)
            {
                button.interactable = interactable;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    if (!interactable)
                        return;
                    if (_isQuickStart && _onQuickStart != null)
                        _onQuickStart?.Invoke(_instanceId);
                    else
                        _onClick?.Invoke(_instanceId);
                });
            }

            _hovered = wasHovered && interactable;
            ApplyCardDrawOrder();
            ApplyVisualState(immediate: true);
            SyncHoverWithPointer();
        }

        void SyncHoverWithPointer()
        {
            if (!_hovered)
                return;

            var rt = (_scaleRoot != null ? _scaleRoot : transform) as RectTransform;
            if (rt == null)
                return;

            if (UiPointerUtility.IsOverRectTransform(rt, UiPointerUtility.GetEventCamera(rt)))
                return;

            _hovered = false;
            ApplyVisualState(immediate: true);
            _onHoverExit?.Invoke();
        }

        void RemoveStaleHoverCanvas()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.overrideSorting)
                Destroy(canvas);
        }

        void EnsureCardVisuals()
        {
            EnsureScaleRoot();

            if (selectedHighlight == null)
            {
                foreach (var img in GetComponentsInChildren<Image>(true))
                {
                    if (img.gameObject.name == "SelectedHighlight")
                    {
                        selectedHighlight = img;
                        break;
                    }
                }
            }

            if (selectedHighlight == null)
            {
                var parent = _scaleRoot != null ? _scaleRoot : transform;
                var go = new GameObject("SelectedHighlight", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                selectedHighlight = go.GetComponent<Image>();
                selectedHighlight.color = SelectedHighlightColor;
                selectedHighlight.raycastTarget = false;
            }

            if (_scaleRoot != null)
            {
                selectedHighlight.transform.SetParent(_scaleRoot, false);
                var rt = selectedHighlight.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (selectedOutline != null)
                selectedOutline.raycastTarget = false;

            EnsurePortraitOverlay();
            EnsureLowerPanelLayout();
        }

        void EnsurePortraitOverlay()
        {
            if (artImage == null)
                return;

            EnsureScaleRoot();
            RemoveLegacyPortraitHierarchy();
            CardPortraitLayout.ApplyProfileOverlay(artImage, _scaleRoot, artImage.sprite);
            ApplyCardDrawOrder();
        }

        void RemoveLegacyPortraitHierarchy()
        {
            if (_scaleRoot == null)
                return;

            var clip = _scaleRoot.Find(LegacyPortraitClipName);
            if (clip == null)
                return;

            if (Application.isPlaying)
                Destroy(clip.gameObject);
            else
                DestroyImmediate(clip.gameObject);
        }

        void ApplyPortraitPresentation()
        {
            if (artImage == null)
                return;

            EnsurePortraitOverlay();
        }

        internal void RefreshPortraitLayout() => ApplyPortraitPresentation();

        void ApplyCardDrawOrder()
        {
            if (_scaleRoot == null)
                EnsureScaleRoot();
            if (_scaleRoot == null)
                return;

            var order = 0;

            if (frameImage != null)
                frameImage.transform.SetSiblingIndex(order++);

            if (artImage != null)
                artImage.transform.SetSiblingIndex(order++);

            if (iconImage != null)
                iconImage.transform.SetSiblingIndex(order++);

            if (costIconImage != null)
            {
                var costRoot = costIconImage.transform.parent;
                if (costRoot != null && costRoot != _scaleRoot)
                    costRoot.SetSiblingIndex(order++);
                else
                    costIconImage.transform.SetSiblingIndex(order++);
            }
            else if (costText != null)
                costText.transform.SetSiblingIndex(order++);

            if (nameText != null)
                nameText.transform.SetSiblingIndex(order++);

            if (statsText != null)
                statsText.transform.SetSiblingIndex(order++);

            if (ownerText != null)
                ownerText.transform.SetSiblingIndex(order++);

            if (orderBadgeText != null)
                orderBadgeText.transform.SetSiblingIndex(order++);

            if (pollutedOverlay != null)
                pollutedOverlay.transform.SetSiblingIndex(order++);

            if (selectedHighlight != null)
                selectedHighlight.transform.SetAsLastSibling();

            if (selectedOutline != null)
                selectedOutline.transform.SetAsLastSibling();
        }

        public static void ApplyHandPresentationScale(CardView view, float scale)
        {
            if (view == null)
                return;

            var width = CardBaseLayoutWidth * scale;
            var height = CardBaseLayoutHeight * scale;
            var rt = view.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(width, height);
            }

            view.EnsureScaleRootForLayout();
            ConfigureScaleRoot(view._scaleRoot);
            if (view._scaleRoot != null)
                view._scaleRoot.localScale = Vector3.one;

            var le = view.GetComponent<LayoutElement>() ?? view.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = height;
            le.minWidth = width;
            le.minHeight = height;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
            view.RefreshPortraitLayout();
        }

        public static void CenterInParent(CardView view)
        {
            if (view == null)
                return;

            var rt = view.transform as RectTransform;
            if (rt == null)
                return;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        public static void ConfigureForRewardPresentation(CardView view, float scale)
        {
            if (view == null)
                return;

            ApplyHandPresentationScaleCentered(view, scale);
            view.EnsureLowerPanelLayout();
            if (view.statsText == null)
                return;

            if (view.nameText != null)
                view.nameText.fontSize = 13;
            view.statsText.fontSize = 12;
            view.statsText.lineSpacing = 1f;
        }

        public static void ApplyHandPresentationScaleCentered(CardView view, float scale)
        {
            ApplyHandPresentationScale(view, scale);
            CenterInParent(view);
        }

        /// <summary>顶部顺序条：隐藏描述/归属，可选隐藏未知意图。</summary>
        public void SetOrderBarPresentation(bool compact, bool hiddenIntent = false)
        {
            if (statsText != null)
                statsText.gameObject.SetActive(!compact);

            if (ownerText != null)
                ownerText.gameObject.SetActive(!compact);

            if (nameText != null)
                nameText.gameObject.SetActive(!compact);

            if (orderBadgeText != null)
                orderBadgeText.gameObject.SetActive(false);

            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }

            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 1f;
            }

            _interactable = false;

            if (!hiddenIntent || artImage == null)
                return;

            artImage.color = new Color(0.12f, 0.12f, 0.14f, 1f);
            if (frameImage != null)
                frameImage.color = new Color(0.55f, 0.55f, 0.6f, 1f);
        }

        const float CardBaseLayoutWidth = CardPortraitLayout.CardWidth;
        const float CardBaseLayoutHeight = CardPortraitLayout.CardHeight;

        internal void EnsureScaleRootForLayout() => EnsureScaleRoot();

        void EnsureScaleRoot()
        {
            if (_scaleRoot != null)
                return;

            var existing = transform.Find(ScaleRootName) as RectTransform;
            if (existing != null)
            {
                _scaleRoot = existing;
                ConfigureScaleRoot(_scaleRoot);
                return;
            }

            var go = new GameObject(ScaleRootName, typeof(RectTransform));
            _scaleRoot = go.GetComponent<RectTransform>();
            _scaleRoot.SetParent(transform, false);
            ConfigureScaleRoot(_scaleRoot);

            var toMove = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
            {
                if (child != _scaleRoot)
                    toMove.Add(child);
            }

            foreach (var child in toMove)
                child.SetParent(_scaleRoot, false);

            transform.localScale = Vector3.one;
        }

        static void ConfigureScaleRoot(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        void EnsureNameLayout()
        {
            if (nameText == null)
                return;

            var rt = nameText.rectTransform;
            rt.anchorMin = NamePanelAnchorMin;
            rt.anchorMax = NamePanelAnchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
            nameText.fontStyle = FontStyle.Bold;
            nameText.fontSize = NameFontSize;
        }

        internal void EnsureDescriptionLayout()
        {
            if (statsText == null)
                return;

            var rt = statsText.rectTransform;
            rt.anchorMin = StatsPanelAnchorMin;
            rt.anchorMax = StatsPanelAnchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            statsText.alignment = TextAnchor.UpperCenter;
            statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statsText.verticalOverflow = VerticalWrapMode.Overflow;
            statsText.fontSize = DescriptionFontNormal;
            statsText.fontStyle = FontStyle.Normal;
            statsText.color = new Color(0.95f, 0.92f, 0.86f, 1f);
        }

        void EnsureLowerPanelLayout()
        {
            EnsureNameLayout();
            EnsureDescriptionLayout();
        }

        void ApplyVisualState(bool immediate = false)
        {
            if (selectedHighlight != null)
            {
                selectedHighlight.gameObject.SetActive(_selected);
                selectedHighlight.color = SelectedHighlightColor;
            }

            if (selectedOutline != null)
                selectedOutline.enabled = false;

            if (statsText != null)
            {
                statsText.fontSize = _hovered ? DescriptionFontHover : DescriptionFontNormal;
                statsText.text = _statsBaseLine;
            }

            var targetScale = _hovered ? HoverScale : NormalScale;
            if (_scaleRoutine != null)
            {
                StopCoroutine(_scaleRoutine);
                _scaleRoutine = null;
            }

            EnsureScaleRoot();
            var scaleTarget = _scaleRoot != null ? _scaleRoot : transform as RectTransform;

            if (immediate)
                scaleTarget.localScale = Vector3.one * targetScale;
            else
                _scaleRoutine = StartCoroutine(LerpScale(scaleTarget, targetScale));
        }

        IEnumerator LerpScale(RectTransform scaleTarget, float targetScale)
        {
            var start = scaleTarget.localScale.x;
            var elapsed = 0f;
            while (elapsed < ScaleLerpDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / ScaleLerpDuration);
                var s = Mathf.Lerp(start, targetScale, t);
                scaleTarget.localScale = new Vector3(s, s, 1f);
                yield return null;
            }

            scaleTarget.localScale = Vector3.one * targetScale;
            _scaleRoutine = null;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null)
                return;

            HandlePrimaryClick();
        }

        void HandlePrimaryClick()
        {
            if (!IsInteractable())
                return;

            GameAudioService.Instance.PlayBattleCardSelect();

            if (_isQuickStart && _onQuickStart != null)
                _onQuickStart?.Invoke(_instanceId);
            else
                _onClick?.Invoke(_instanceId);
        }

        bool IsInteractable() => _interactable && (button == null || button.interactable);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable() || _hovered)
                return;

            _hovered = true;
            ApplyVisualState();
            GameAudioService.Instance.PlayBattleCardHover();

            if (CurrentCard != null)
                _onHoverEnter?.Invoke(CurrentCard, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hovered)
                return;

            _hovered = false;
            ApplyVisualState();
            _onHoverExit?.Invoke();
        }
    }
}
