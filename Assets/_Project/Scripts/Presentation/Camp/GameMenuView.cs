using System;
using Grimhand.Content;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>开局主菜单：点击 gamemenu.png 切片按钮（START / CONTINUE / SETTINGS / QUIT GAME）。</summary>
    [DisallowMultipleComponent]
    public sealed class GameMenuView : MonoBehaviour
    {
        const float StartButtonTargetWidth = 420f;
        const float ButtonSpacing = 10f;

        [SerializeField] BattleUiIconCatalogSO uiIcons;

        bool _built;
        RectTransform _root;
        Button _continueButton;
        Action _onStart;
        Action _onContinue;
        Action _onSettings;
        Action _onQuit;

        public void ConfigureArt(BattleUiIconCatalogSO icons) => uiIcons = icons;

        public void Initialize(
            Action onStart,
            Action onContinue,
            Action onSettings,
            Action onQuit,
            BattleUiIconCatalogSO icons = null)
        {
            if (icons != null)
                uiIcons = icons;

            _onStart = onStart;
            _onContinue = onContinue;
            _onSettings = onSettings;
            _onQuit = onQuit;
            EnsureBuilt();
        }

        public void Show(bool canContinue)
        {
            EnsureBuilt();
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (_continueButton != null)
                _continueButton.interactable = canContinue;
        }

        public void Hide()
        {
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

            var bg = CampUiRuntime.CreateImage("Background", _root, new Color(0.04f, 0.05f, 0.08f, 1f));
            CampUiRuntime.StretchFull(bg.rectTransform);

            var sprites = ResolveMenuSprites();
            var startWidth = sprites[0] != null ? sprites[0].rect.width : 179f;

            var buttonColumn = CampUiRuntime.CreateRect("MenuButtons", _root).GetComponent<RectTransform>();
            buttonColumn.anchorMin = new Vector2(0.5f, 0.5f);
            buttonColumn.anchorMax = new Vector2(0.5f, 0.5f);
            buttonColumn.pivot = new Vector2(0.5f, 0.5f);
            buttonColumn.anchoredPosition = Vector2.zero;

            var layout = buttonColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = ButtonSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateSpriteButton(buttonColumn, sprites[0], startWidth, () => _onStart?.Invoke());
            _continueButton = CreateSpriteButton(buttonColumn, sprites[1], startWidth, () => _onContinue?.Invoke());
            CreateSpriteButton(buttonColumn, sprites[2], startWidth, () => _onSettings?.Invoke());
            CreateSpriteButton(buttonColumn, sprites[3], startWidth, () => _onQuit?.Invoke());

            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonColumn);
            buttonColumn.sizeDelta = new Vector2(buttonColumn.sizeDelta.x, layout.preferredHeight);

            _root.gameObject.SetActive(false);
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
            return btn;
        }
    }
}
