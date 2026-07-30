using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>远征局内 ESC 菜单：RETURN / SETTINGS / FORFEIT / QUIT。</summary>
    [DisallowMultipleComponent]
    public sealed class ExpeditionEscMenuView : MonoBehaviour
    {
        const float PrimaryButtonTargetWidth = 420f;
        const float ButtonSpacing = 10f;

        [SerializeField] BattleUiIconCatalogSO uiIcons;

        bool _built;
        RectTransform _root;
        Image _backgroundImage;
        CampConfirmPromptView _forfeitConfirm;
        Action _onReturnToGame;
        Action _onSettings;
        Action _onForfeitConfirmed;
        Action _onQuit;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;
        public bool IsForfeitConfirmOpen => _forfeitConfirm != null && _forfeitConfirm.IsOpen;

        /// <summary>ESC：有确认框则取消（否），否则关闭菜单。</summary>
        public bool TryHandleEscape()
        {
            if (!IsOpen)
                return false;

            if (_forfeitConfirm != null && _forfeitConfirm.TryCancelViaEscape())
                return true;

            Hide();
            _onReturnToGame?.Invoke();
            return true;
        }

        public void ConfigureArt(BattleUiIconCatalogSO icons) => uiIcons = icons;

        public void Initialize(
            Action onReturnToGame,
            Action onSettings,
            Action onForfeitConfirmed,
            Action onQuit,
            BattleUiIconCatalogSO icons = null)
        {
            if (icons != null)
                uiIcons = icons;

            _onReturnToGame = onReturnToGame;
            _onSettings = onSettings;
            _onForfeitConfirmed = onForfeitConfirmed;
            _onQuit = onQuit;
            EnsureBuilt();
        }

        public void Show(Sprite layerBackground)
        {
            EnsureBuilt();
            HideForfeitConfirm();
            if (_backgroundImage != null)
            {
                if (layerBackground != null)
                {
                    _backgroundImage.sprite = layerBackground;
                    _backgroundImage.color = Color.white;
                    _backgroundImage.type = Image.Type.Simple;
                    _backgroundImage.preserveAspect = false;
                }
                else
                {
                    _backgroundImage.sprite = null;
                    _backgroundImage.color = new Color(0.03f, 0.04f, 0.07f, 1f);
                }
            }

            _root.gameObject.SetActive(true);
            BringToFrontUnderCanvas();
        }

        public void Hide()
        {
            HideForfeitConfirm();
            if (_root != null)
                _root.gameObject.SetActive(false);
        }

        public void HideForfeitConfirm()
        {
            _forfeitConfirm?.Hide();
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

            _root = CampUiRuntime.CreateRect("EscMenuRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_root);

            // 独立排序层，压住战斗 HUD，只露出中间按钮。
            var overlayCanvas = _root.gameObject.GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = _root.gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 5000;
            if (_root.GetComponent<GraphicRaycaster>() == null)
                _root.gameObject.AddComponent<GraphicRaycaster>();

            _backgroundImage = CampUiRuntime.CreateImage(
                "Background", _root, new Color(0.04f, 0.05f, 0.08f, 1f));
            CampUiRuntime.StretchFull(_backgroundImage.rectTransform);
            _backgroundImage.raycastTarget = true;

            var dim = CampUiRuntime.CreateImage("Dim", _root, new Color(0f, 0f, 0f, 0.45f));
            CampUiRuntime.StretchFull(dim.rectTransform);
            dim.raycastTarget = true;

            var sprites = ResolveMenuSprites();
            var primaryWidth = sprites[0] != null ? sprites[0].rect.width : 178f;

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

            CreateSpriteButton(buttonColumn, sprites[0], primaryWidth, () =>
            {
                // 先恢复战斗 HUD，再关菜单，避免出现空战场
                _onReturnToGame?.Invoke();
            });
            CreateSpriteButton(buttonColumn, sprites[1], primaryWidth, () => _onSettings?.Invoke());
            CreateSpriteButton(buttonColumn, sprites[2], primaryWidth, ShowForfeitConfirm);
            CreateSpriteButton(buttonColumn, sprites[3], primaryWidth, () => _onQuit?.Invoke());

            LayoutRebuilder.ForceRebuildLayoutImmediate(buttonColumn);
            buttonColumn.sizeDelta = new Vector2(buttonColumn.sizeDelta.x, layout.preferredHeight);

            BuildForfeitConfirm();
            _root.gameObject.SetActive(false);
        }

        void BuildForfeitConfirm()
        {
            _forfeitConfirm = CampConfirmPromptView.Create(_root, uiIcons, "ForfeitConfirm");
        }

        void ShowForfeitConfirm()
        {
            if (_forfeitConfirm == null)
                return;

            _forfeitConfirm.Show(
                "放弃当前远征？",
                "放弃后将按攻略层数结算局外经验（×5），并同步尚未入账的局外金币。\n当前远征进度将丢失，无法继续本局。",
                "取消",
                "确认放弃",
                onCancel: null,
                onConfirm: () =>
                {
                    Hide();
                    _onForfeitConfirmed?.Invoke();
                });
        }

        void BringToFrontUnderCanvas()
        {
            var t = transform;
            while (t != null)
            {
                t.SetAsLastSibling();
                if (t.parent == null || t.parent.GetComponent<Canvas>() != null)
                    break;
                t = t.parent;
            }
        }

        Sprite[] ResolveMenuSprites()
        {
            if (uiIcons?.EscMenuButtons != null && uiIcons.EscMenuButtons.Length >= 4)
                return uiIcons.EscMenuButtons;

            Debug.LogWarning("[EscMenu] 未绑定 EscMenuButtons。请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            return new Sprite[4];
        }

        static Button CreateSpriteButton(Transform parent, Sprite sprite, float primarySpriteWidth, Action onClick)
        {
            var img = CampUiRuntime.CreateImage(sprite != null ? sprite.name : "EscMenuButton", parent, Color.white);
            img.sprite = sprite;
            img.preserveAspect = true;
            img.type = Image.Type.Simple;
            img.raycastTarget = true;

            var width = sprite != null
                ? PrimaryButtonTargetWidth * (sprite.rect.width / primarySpriteWidth)
                : PrimaryButtonTargetWidth;
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
