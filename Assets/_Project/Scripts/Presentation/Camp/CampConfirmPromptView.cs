using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>
    /// 通用是/否确认框：prompt_plate + 标题/内容；左 button3 取消，右 button1 确认。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CampConfirmPromptView : MonoBehaviour
    {
        const float PlateWidth = 680f;
        const float PlateAspect = 1356f / 1057f;
        const float ButtonHoverScale = 1.06f;

        static readonly Color TitleColor = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color BodyColor = new(0.86f, 0.88f, 0.94f, 1f);
        static readonly Color LabelColor = new(0.96f, 0.92f, 0.78f, 1f);

        // 板内模板区（原点左下）；底钮区加宽压矮，盖住 prompt_plate 预绘按钮槽
        static readonly Vector4 ZoneTitle = new(0.14f, 0.78f, 0.86f, 0.90f);
        static readonly Vector4 ZoneBody = new(0.14f, 0.36f, 0.86f, 0.76f);
        // 底钮：只向下加高盖住槽位下沿，顶边勿上抬
        static readonly Vector4 ZoneCancel = new(0.088f, 0.108f, 0.495f, 0.265f);
        static readonly Vector4 ZoneConfirm = new(0.505f, 0.108f, 0.912f, 0.265f);

        BattleUiIconCatalogSO _icons;
        GameObject _root;
        Text _titleText;
        Text _bodyText;
        Text _cancelLabel;
        Text _confirmLabel;
        Action _onCancel;
        Action _onConfirm;
        bool _built;

        public bool IsOpen => _root != null && _root.activeSelf;

        /// <summary>ESC / 返回：等价于点「取消/否」。</summary>
        public bool TryCancelViaEscape()
        {
            if (!IsOpen)
                return false;

            var cb = _onCancel;
            Hide();
            cb?.Invoke();
            return true;
        }

        public static CampConfirmPromptView Create(
            Transform parent,
            BattleUiIconCatalogSO icons,
            string objectName = "ConfirmPrompt")
        {
            var go = CampUiRuntime.CreateRect(objectName, parent);
            var view = go.AddComponent<CampConfirmPromptView>();
            view.Build(icons);
            return view;
        }

        public void Show(
            string title,
            string body,
            string cancelLabel,
            string confirmLabel,
            Action onCancel,
            Action onConfirm)
        {
            if (!_built)
                Build(_icons);

            _onCancel = onCancel;
            _onConfirm = onConfirm;
            if (_titleText != null)
                _titleText.text = title ?? "";
            if (_bodyText != null)
                _bodyText.text = body ?? "";
            if (_cancelLabel != null)
                _cancelLabel.text = string.IsNullOrEmpty(cancelLabel) ? "取消" : cancelLabel;
            if (_confirmLabel != null)
                _confirmLabel.text = string.IsNullOrEmpty(confirmLabel) ? "确认" : confirmLabel;

            _root.SetActive(true);
            transform.SetAsLastSibling();
            _root.transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (_root != null)
                _root.SetActive(false);
            _onCancel = null;
            _onConfirm = null;
        }

        void Build(BattleUiIconCatalogSO icons)
        {
            if (_built)
                return;

            _built = true;
            _icons = icons;

            var hostRt = GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _root = CampUiRuntime.CreateImage("Dim", transform, new Color(0f, 0f, 0f, 0.72f)).gameObject;
            CampUiRuntime.StretchFull(_root.GetComponent<RectTransform>());

            var plateImg = CampUiRuntime.CreateImage("Plate", _root.transform, Color.white);
            var plateRt = plateImg.rectTransform;
            plateRt.anchorMin = new Vector2(0.5f, 0.5f);
            plateRt.anchorMax = new Vector2(0.5f, 0.5f);
            plateRt.pivot = new Vector2(0.5f, 0.5f);
            var plateH = PlateWidth / PlateAspect;
            plateRt.sizeDelta = new Vector2(PlateWidth, plateH);
            plateImg.preserveAspect = true;
            plateImg.raycastTarget = true;

            var plateSprite = icons != null ? icons.UiPromptPlate : null;
            if (plateSprite != null)
            {
                plateImg.sprite = plateSprite;
                plateImg.color = Color.white;
                plateImg.type = Image.Type.Simple;
            }
            else
            {
                plateImg.sprite = null;
                plateImg.color = new Color(0.09f, 0.1f, 0.14f, 0.98f);
                Debug.LogWarning("[ConfirmPrompt] 缺少 UiPromptPlate，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            _titleText = CampUiRuntime.CreateText(plateRt, "", 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetZone(_titleText.rectTransform, ZoneTitle);
            _titleText.color = TitleColor;
            _titleText.raycastTarget = false;

            _bodyText = CampUiRuntime.CreateText(plateRt, "", 17, FontStyle.Normal, TextAnchor.UpperCenter);
            SetZone(_bodyText.rectTransform, ZoneBody);
            _bodyText.color = BodyColor;
            _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _bodyText.verticalOverflow = VerticalWrapMode.Truncate;
            _bodyText.raycastTarget = false;

            _cancelLabel = CreateSpriteButton(
                plateRt,
                "Cancel",
                ZoneCancel,
                icons != null ? icons.UiButton3 : null,
                "取消",
                () =>
                {
                    var cb = _onCancel;
                    Hide();
                    cb?.Invoke();
                });

            _confirmLabel = CreateSpriteButton(
                plateRt,
                "Confirm",
                ZoneConfirm,
                icons != null ? icons.UiButton1 : null,
                "确认",
                () =>
                {
                    var cb = _onConfirm;
                    Hide();
                    cb?.Invoke();
                });

            _root.SetActive(false);
        }

        static Text CreateSpriteButton(
            Transform parent,
            string id,
            Vector4 zone,
            Sprite sprite,
            string label,
            Action onClick)
        {
            var go = CampUiRuntime.CreateRect(id, parent);
            var rt = go.GetComponent<RectTransform>();
            // 直接铺满槽位（略扁于原生 512×292），盖住模板预绘钮
            SetZone(rt, zone);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (sprite != null)
                img.sprite = sprite;
            else
                img.color = new Color(0.28f, 0.3f, 0.36f, 1f);

            var text = CampUiRuntime.CreateText(go.transform, label, 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(4f, 2f);
            text.rectTransform.offsetMax = new Vector2(-4f, -6f);
            text.color = LabelColor;
            text.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick?.Invoke());
            UiAudioHooks.WireButton(btn);
            return text;
        }

        static void SetZone(RectTransform rt, Vector4 zone)
        {
            CampUiRuntime.SetAnchored(rt, zone.x, zone.y, zone.z, zone.w);
        }
    }
}
