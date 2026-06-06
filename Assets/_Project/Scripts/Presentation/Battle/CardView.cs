using System;
using System.Collections;
using Grimhand.Battle.Model;
using Grimhand.Content;
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
        const int DescriptionFontNormal = 15;
        const int DescriptionFontHover = 18;

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

        int _instanceId;
        bool _selected;
        bool _hovered;
        bool _interactable = true;
        Coroutine _scaleRoutine;
        RectTransform _scaleRoot;
        Action<int> _onClick;
        Action<CardInstanceState, RectTransform> _onHoverEnter;
        Action _onHoverExit;
        string _statsBaseLine = "";

        public int InstanceId => _instanceId;
        public CardInstanceState CurrentCard { get; private set; }

        void Awake()
        {
            RemoveStaleHoverCanvas();
            EnsureScaleRoot();
            EnsureCardVisuals();
            EnsureNameLayout();
            EnsureDescriptionLayout();
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
            Action onHoverExit)
        {
            EnsureScaleRoot();
            EnsureCardVisuals();

            var wasHovered = _hovered && _instanceId == card.InstanceId;

            CurrentCard = card;
            _instanceId = card.InstanceId;
            _selected = selected;
            _interactable = interactable;
            _onClick = onClick;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;
            _statsBaseLine = statsLine ?? "";

            if (frameImage != null)
            {
                frameImage.enabled = true;
                frameImage.sprite = visual.Frame;
                frameImage.preserveAspect = true;
                frameImage.color = visual.Frame != null ? Color.white : new Color(0.18f, 0.2f, 0.28f, 1f);
            }

            if (artImage != null)
            {
                var portrait = characterVisuals != null
                    ? characterVisuals.GetPortrait(card.OwnerCharacterId)
                    : null;
                var art = visual.Art ?? portrait;
                artImage.enabled = true;
                artImage.sprite = art;
                artImage.preserveAspect = true;
                artImage.color = art != null ? Color.white : new Color(0.25f, 0.27f, 0.35f, 1f);
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
                costText.text = card.Cost.ToString();

            if (nameText != null)
                nameText.text = polluted ? "[污] " + card.DisplayName : card.DisplayName;

            if (statsText != null)
            {
                statsText.text = _statsBaseLine;
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
                    if (interactable)
                        _onClick?.Invoke(_instanceId);
                });
            }

            _hovered = wasHovered && interactable;
            ApplyVisualState(immediate: true);
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
                selectedHighlight.transform.SetAsLastSibling();
                var rt = selectedHighlight.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (selectedOutline != null)
                selectedOutline.raycastTarget = false;

            if (artImage != null)
            {
                var artRt = artImage.rectTransform;
                artRt.anchorMin = new Vector2(0.06f, 0.30f);
                artRt.anchorMax = new Vector2(0.94f, 0.90f);
                artRt.offsetMin = Vector2.zero;
                artRt.offsetMax = Vector2.zero;
            }
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
        }

        const float CardBaseLayoutWidth = 168f;
        const float CardBaseLayoutHeight = 236f;

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
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(8f, -56f);
            rt.offsetMax = new Vector2(-8f, -6f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameText.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void EnsureDescriptionLayout()
        {
            if (statsText == null)
                return;

            var rt = statsText.rectTransform;
            if (rt.anchorMin.y <= 0.1f)
                return;

            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(10f, 12f);
            rt.offsetMax = new Vector2(-10f, 108f);
            statsText.alignment = TextAnchor.UpperCenter;
            statsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statsText.verticalOverflow = VerticalWrapMode.Overflow;
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

            if (!IsInteractable())
                return;

            _onClick?.Invoke(_instanceId);
        }

        bool IsInteractable() => _interactable && (button == null || button.interactable);

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!IsInteractable() || _hovered)
                return;

            _hovered = true;
            ApplyVisualState();

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
