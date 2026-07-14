using Grimhand.Expedition;
using Grimhand.Expedition.Model;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>单个天赋符文石：rune_plate 图标、悬停描述、选中高亮（尺寸不变）。</summary>
    [DisallowMultipleComponent]
    public sealed class TalentRuneStoneView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        const float RuneSize = 108f;

        RectTransform _rt;
        Image _plateImage;
        Image _lockOverlay;
        Outline _outline;
        Text _levelLabel;
        Text _titleLabel;
        Button _button;

        TalentDefinition _talent;
        TalentCardState _state;
        System.Action<TalentDefinition> _onClick;
        System.Action<TalentDefinition, TalentCardState> _onHover;
        System.Action _onHoverExit;

        public void Bind(
            TalentDefinition talent,
            TalentCardState state,
            Sprite runePlateSprite,
            System.Action<TalentDefinition> onClick,
            System.Action<TalentDefinition, TalentCardState> onHover,
            System.Action onHoverExit)
        {
            _talent = talent;
            _state = state;
            _onClick = onClick;
            _onHover = onHover;
            _onHoverExit = onHoverExit;

            EnsureBuilt(runePlateSprite);
            RefreshVisual();
        }

        void EnsureBuilt(Sprite runePlateSprite)
        {
            if (_rt != null)
                return;

            _rt = GetComponent<RectTransform>();
            if (_rt == null)
                _rt = gameObject.AddComponent<RectTransform>();

            var le = gameObject.GetComponent<LayoutElement>();
            if (le == null)
                le = gameObject.AddComponent<LayoutElement>();

            le.preferredWidth = RuneSize;
            le.preferredHeight = RuneSize + 36f;
            le.minWidth = le.preferredWidth;
            le.minHeight = le.preferredHeight;
            _rt.sizeDelta = new Vector2(le.preferredWidth, le.preferredHeight);

            var rootBg = gameObject.GetComponent<Image>();
            if (rootBg == null)
                rootBg = gameObject.AddComponent<Image>();
            rootBg.color = new Color(0f, 0f, 0f, 0f);
            rootBg.raycastTarget = true;

            _plateImage = CampUiRuntime.CreateImage("RunePlate", transform, Color.white);
            _plateImage.sprite = runePlateSprite;
            _plateImage.preserveAspect = true;
            var plateRt = _plateImage.rectTransform;
            plateRt.anchorMin = new Vector2(0.5f, 1f);
            plateRt.anchorMax = new Vector2(0.5f, 1f);
            plateRt.pivot = new Vector2(0.5f, 1f);
            plateRt.anchoredPosition = new Vector2(0f, -4f);
            plateRt.sizeDelta = new Vector2(RuneSize, RuneSize);
            plateRt.localScale = Vector3.one;

            _outline = _plateImage.gameObject.AddComponent<Outline>();
            _outline.effectColor = new Color(1f, 0.84f, 0.35f, 0f);
            _outline.effectDistance = new Vector2(3f, -3f);
            _outline.useGraphicAlpha = true;

            _levelLabel = CampUiRuntime.CreateText(transform, "", 13, FontStyle.Bold, TextAnchor.UpperCenter);
            _levelLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            _levelLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            _levelLabel.rectTransform.offsetMin = new Vector2(0f, -18f);
            _levelLabel.rectTransform.offsetMax = new Vector2(0f, 0f);
            _levelLabel.color = new Color(0.9f, 0.92f, 0.98f, 1f);

            _titleLabel = CampUiRuntime.CreateText(transform, "", 14, FontStyle.Bold, TextAnchor.UpperCenter);
            _titleLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _titleLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            _titleLabel.rectTransform.offsetMin = new Vector2(0f, 0f);
            _titleLabel.rectTransform.offsetMax = new Vector2(0f, 32f);
            _titleLabel.color = new Color(0.88f, 0.9f, 0.96f, 1f);

            _lockOverlay = CampUiRuntime.CreateImage("LockOverlay", _plateImage.transform, new Color(0f, 0f, 0f, 0.55f));
            CampUiRuntime.StretchFull(_lockOverlay.rectTransform);
            _lockOverlay.raycastTarget = false;

            var lockText = CampUiRuntime.CreateText(_lockOverlay.transform, "", 12, FontStyle.Bold);
            CampUiRuntime.StretchFull(lockText.rectTransform);

            _button = gameObject.GetComponent<Button>();
            if (_button == null)
                _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = rootBg;
            _button.transition = Selectable.Transition.None;
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => _onClick?.Invoke(_talent));
            UiAudioHooks.WireButton(_button);
        }

        void RefreshVisual()
        {
            if (_talent == null || _plateImage == null)
                return;

            _levelLabel.text = $"Lv{_talent.UnlockLevel}";
            _titleLabel.text = _talent.ShortTitle;

            var isSelected = _state == TalentCardState.Selected;
            var isLocked = _state == TalentCardState.Locked;

            _plateImage.rectTransform.sizeDelta = new Vector2(RuneSize, RuneSize);
            _plateImage.rectTransform.localScale = Vector3.one;
            _plateImage.color = isLocked
                ? new Color(0.42f, 0.42f, 0.45f, 0.85f)
                : isSelected
                    ? new Color(1f, 0.96f, 0.78f, 1f)
                    : Color.white;

            _outline.effectColor = isSelected
                ? new Color(1f, 0.84f, 0.28f, 1f)
                : new Color(1f, 0.84f, 0.35f, 0f);

            _lockOverlay.gameObject.SetActive(isLocked);
            if (isLocked)
            {
                var lockText = _lockOverlay.GetComponentInChildren<Text>();
                if (lockText != null)
                    lockText.text = $"Lv{_talent.UnlockLevel}\n解锁";
            }

            if (isSelected)
            {
                var badge = transform.Find("SelectedBadge");
                if (badge == null)
                {
                    var badgeText = CampUiRuntime.CreateText(transform, "已装备", 11, FontStyle.Bold,
                        TextAnchor.LowerCenter);
                    badgeText.gameObject.name = "SelectedBadge";
                    badgeText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
                    badgeText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                    badgeText.rectTransform.pivot = new Vector2(0.5f, 1f);
                    badgeText.rectTransform.anchoredPosition = new Vector2(0f, -(RuneSize + 6f));
                    badgeText.color = new Color(1f, 0.88f, 0.38f, 1f);
                }
            }
            else
            {
                var badge = transform.Find("SelectedBadge");
                if (badge != null)
                    Destroy(badge.gameObject);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onHover?.Invoke(_talent, _state);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onHoverExit?.Invoke();
        }
    }
}
