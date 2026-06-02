using System;
using Grimhand.Battle.Model;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Battle
{
    public sealed class CardView : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image frameImage;
        [SerializeField] Image artImage;
        [SerializeField] Image iconImage;
        [SerializeField] Image pollutedOverlay;
        [SerializeField] Image selectedOutline;
        [SerializeField] Image costIconImage;
        [SerializeField] Text costText;
        [SerializeField] Text nameText;
        [SerializeField] Text statsText;
        [SerializeField] Text ownerText;
        [SerializeField] Text orderBadgeText;
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Button button;

        int _instanceId;
        Action<int> _onClick;
        Action<CardInstanceState, RectTransform> _onHoverEnter;
        Action _onHoverExit;

        public int InstanceId => _instanceId;
        public CardInstanceState CurrentCard { get; private set; }

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
            CurrentCard = card;
            _instanceId = card.InstanceId;
            _onClick = onClick;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;

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
                statsText.text = statsLine;

            if (ownerText != null)
                ownerText.text = "";

            if (orderBadgeText != null)
            {
                orderBadgeText.gameObject.SetActive(selected && !string.IsNullOrEmpty(orderBadge));
                orderBadgeText.text = orderBadge;
            }

            if (selectedOutline != null)
            {
                selectedOutline.enabled = selected;
                selectedOutline.color = new Color(1f, 0.85f, 0.15f, selected ? 1f : 0f);
            }

            transform.localScale = selected ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;

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
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (button != null)
                return;

            if (!IsInteractable())
                return;

            _onClick?.Invoke(_instanceId);
        }

        bool IsInteractable() => button == null || button.interactable;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (CurrentCard != null)
                _onHoverEnter?.Invoke(CurrentCard, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData) => _onHoverExit?.Invoke();
    }
}
