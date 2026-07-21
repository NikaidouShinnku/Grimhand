using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>开局主菜单：START / CONTINUE / SETTINGS / QUIT；有存档时 START 会确认放弃远征。</summary>
    [DisallowMultipleComponent]
    public sealed class GameMenuView : MonoBehaviour
    {
        const float StartButtonTargetWidth = 420f;
        const float ButtonSpacing = 10f;

        [SerializeField] BattleUiIconCatalogSO uiIcons;

        bool _built;
        RectTransform _root;
        GameObject _abandonConfirmPanel;
        Button _continueButton;
        Action _onStart;
        Action _onAbandonAndStart;
        Action _onContinue;
        Action _onSettings;
        Action _onQuit;

        public void ConfigureArt(BattleUiIconCatalogSO icons) => uiIcons = icons;

        public void Initialize(
            Action onStart,
            Action onAbandonAndStart,
            Action onContinue,
            Action onSettings,
            Action onQuit,
            BattleUiIconCatalogSO icons = null)
        {
            if (icons != null)
                uiIcons = icons;

            _onStart = onStart;
            _onAbandonAndStart = onAbandonAndStart;
            _onContinue = onContinue;
            _onSettings = onSettings;
            _onQuit = onQuit;
            EnsureBuilt();
        }

        public void Show(bool canContinue)
        {
            EnsureBuilt();
            HideAbandonConfirm();
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_continueButton != null)
                _continueButton.interactable = canContinue;
        }

        public void Hide()
        {
            HideAbandonConfirm();
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        void EnsureBuilt()
        {
            if (_built)
                return;

            _built = true;
            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _root = CampUiRuntime.CreateRect("GameMenuRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_root);

            var bg = CampUiRuntime.CreateImage("Background", _root, Color.white);
            CampUiRuntime.StretchFull(bg.rectTransform);
            bg.sprite = uiIcons != null ? uiIcons.MainMenuBackground : null;
            bg.preserveAspect = false;
            bg.raycastTarget = false;
            if (bg.sprite == null)
                bg.color = new Color(0.04f, 0.05f, 0.08f, 1f);

            var sprites = ResolveMenuSprites();
            var startWidth = sprites[0] != null ? sprites[0].rect.width : 179f;

            // 右侧留白竖排，避免挡住左侧角色与 Logo
            var buttonColumn = CampUiRuntime.CreateRect("MenuButtons", _root).GetComponent<RectTransform>();
            buttonColumn.anchorMin = new Vector2(0.78f, 0.42f);
            buttonColumn.anchorMax = new Vector2(0.78f, 0.42f);
            buttonColumn.pivot = new Vector2(0.5f, 0.5f);
            buttonColumn.anchoredPosition = Vector2.zero;

            var layout = buttonColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateSpriteButton(buttonColumn, sprites[0], startWidth, OnStartClicked);
            _continueButton = CreateSpriteButton(buttonColumn, sprites[1], startWidth, () =>
            {
                Hide();
                _onContinue?.Invoke();
            });
            CreateSpriteButton(buttonColumn, sprites[2], startWidth, () => _onSettings?.Invoke());
            CreateSpriteButton(buttonColumn, sprites[3], startWidth, () => _onQuit?.Invoke());

            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonColumn);
            buttonColumn.sizeDelta = new Vector2(buttonColumn.sizeDelta.x, layout.preferredHeight);

            BuildAbandonConfirm();
            _root.gameObject.SetActive(false);
        }

        void OnStartClicked()
        {
            if (_continueButton != null && _continueButton.interactable)
            {
                ShowAbandonConfirm();
                return;
            }

            Hide();
            _onStart?.Invoke();
        }

        void BuildAbandonConfirm()
        {
            _abandonConfirmPanel = CampUiRuntime.CreateImage(
                "AbandonConfirm", _root, new Color(0f, 0f, 0f, 0.72f)).gameObject;
            CampUiRuntime.StretchFull(_abandonConfirmPanel.GetComponent<RectTransform>());

            var dialog = CampUiRuntime.CreateImage(
                "Dialog", _abandonConfirmPanel.transform, new Color(0.09f, 0.1f, 0.14f, 0.98f)).rectTransform;
            dialog.anchorMin = new Vector2(0.5f, 0.5f);
            dialog.anchorMax = new Vector2(0.5f, 0.5f);
            dialog.pivot = new Vector2(0.5f, 0.5f);
            dialog.sizeDelta = new Vector2(560f, 280f);

            var title = CampUiRuntime.CreateText(dialog, "放弃当前远征？", 26, FontStyle.Bold, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(24f, -64f);
            title.rectTransform.offsetMax = new Vector2(-24f, -16f);
            title.color = new Color(0.95f, 0.85f, 0.55f, 1f);

            var body = CampUiRuntime.CreateText(dialog,
                "进入营地会自动放弃未完成的远征。\n将按攻略层数结算局外经验（×5），并同步尚未入账的局外金币。\n此操作后将无法继续该局。",
                16, FontStyle.Normal, TextAnchor.UpperCenter);
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(28f, 72f);
            body.rectTransform.offsetMax = new Vector2(-28f, -72f);
            body.color = new Color(0.82f, 0.86f, 0.94f, 1f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;

            var noBtn = CampUiRuntime.CreateButton(dialog, "取消", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 44f));
            var noRt = noBtn.GetComponent<RectTransform>();
            noRt.anchorMin = new Vector2(0.5f, 0f);
            noRt.anchorMax = new Vector2(0.5f, 0f);
            noRt.pivot = new Vector2(1f, 0f);
            noRt.anchoredPosition = new Vector2(-16f, 20f);
            noBtn.onClick.AddListener(HideAbandonConfirm);

            var yesBtn = CampUiRuntime.CreateButton(dialog, "确认放弃", new Color(0.55f, 0.28f, 0.18f, 1f),
                new Vector2(160f, 44f));
            var yesRt = yesBtn.GetComponent<RectTransform>();
            yesRt.anchorMin = new Vector2(0.5f, 0f);
            yesRt.anchorMax = new Vector2(0.5f, 0f);
            yesRt.pivot = new Vector2(0f, 0f);
            yesRt.anchoredPosition = new Vector2(16f, 20f);
            yesBtn.onClick.AddListener(() =>
            {
                HideAbandonConfirm();
                Hide();
                _onAbandonAndStart?.Invoke();
            });

            _abandonConfirmPanel.SetActive(false);
        }

        void ShowAbandonConfirm()
        {
            if (_abandonConfirmPanel == null)
                return;

            _abandonConfirmPanel.SetActive(true);
            _abandonConfirmPanel.transform.SetAsLastSibling();
        }

        void HideAbandonConfirm()
        {
            if (_abandonConfirmPanel != null)
                _abandonConfirmPanel.SetActive(false);
        }

        Sprite[] ResolveMenuSprites()
        {
            if (uiIcons?.GameMenuButtons != null && uiIcons.GameMenuButtons.Length >= 4)
                return uiIcons.GameMenuButtons;

            Debug.LogWarning("[GameMenu] 未绑定 GameMenuButtons。请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            return new Sprite[4];
        }

        static Button CreateSpriteButton(Transform parent, Sprite sprite, float startSpriteWidth, Action onClick)
        {
            var img = CampUiRuntime.CreateImage(sprite != null ? sprite.name : "MenuButton", parent, Color.white);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            img.raycastTarget = true;

            var width = sprite != null
                ? StartButtonTargetWidth * (sprite.rect.width / startSpriteWidth)
                : StartButtonTargetWidth;
            var height = sprite != null
                ? width * (sprite.rect.height / sprite.rect.width)
                : 52f;

            var rt = img.rectTransform;
            rt.sizeDelta = new Vector2(width, height);

            var layoutElement = img.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.disabledColor = new Color(0.42f, 0.42f, 0.42f, 0.65f);
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn, menuStyle: true);
            return btn;
        }
    }
}
