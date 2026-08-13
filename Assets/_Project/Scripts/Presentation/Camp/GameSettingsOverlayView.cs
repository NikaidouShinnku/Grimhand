using System;
using Grimhand.Content;
using Grimhand.Presentation.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>设置：音乐 / 音效 / 分辨率；底板 event_plate，返回 button6。</summary>
    [DisallowMultipleComponent]
    public sealed class GameSettingsOverlayView : MonoBehaviour
    {
        // event_plate 约 1203×1308，略竖长
        const float PanelWidth = 520f;
        const float PanelHeight = 580f;
        const float ButtonAspect = 512f / 216f;
        const float ButtonHoverScale = 1.06f;
        const int LayoutVersion = 5;

        static readonly Color TitleColor = new(0.95f, 0.85f, 0.55f, 1f);
        static readonly Color LabelColor = new(0.92f, 0.90f, 0.84f, 1f);
        static readonly Color ButtonLabelColor = new(0.96f, 0.92f, 0.78f, 1f);

        // 板内槽位（原点左下）；边距避开装饰框
        static readonly Vector4 ZoneTitle = new(0.12f, 0.86f, 0.88f, 0.94f);
        static readonly Vector4 ZoneBack = new(0.32f, 0.06f, 0.68f, 0.16f);

        [SerializeField] BattleUiIconCatalogSO uiIcons;

        bool _built;
        int _builtVersion = -1;
        RectTransform _root;
        Slider _musicSlider;
        Slider _sfxSlider;
        Text _resolutionLabel;
        Toggle _fullscreenToggle;
        Action _onClose;
        int _resolutionIndex;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void ConfigureArt(BattleUiIconCatalogSO icons) => uiIcons = icons;

        public void Initialize(Action onClose, BattleUiIconCatalogSO icons = null)
        {
            if (icons != null)
                uiIcons = icons;
            _onClose = onClose;
            EnsureBuilt();
        }

        public void Show()
        {
            // GameMenu 在 Canvas 上常盖在 CampOverlays 之上；需要把自身（及祖先）提到最前。
            gameObject.SetActive(true);
            EnsureBuilt();
            _musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            _sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            _resolutionIndex = GameSettings.FindClosestPresetIndex();
            RefreshResolutionLabel();
            if (_fullscreenToggle != null)
                _fullscreenToggle.SetIsOnWithoutNotify(GameSettings.Fullscreen);
            _root.gameObject.SetActive(true);
            BringToFrontUnderCanvas();
        }

        public void Hide()
        {
            if (_root != null)
                _root.gameObject.SetActive(false);
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

        void EnsureBuilt()
        {
            if (_built && _builtVersion == LayoutVersion)
                return;

            if (_root != null)
                Destroy(_root.gameObject);

            _built = true;
            _builtVersion = LayoutVersion;
            var hostRt = GetComponent<RectTransform>();
            if (hostRt == null)
                hostRt = gameObject.AddComponent<RectTransform>();
            CampUiRuntime.StretchFull(hostRt);

            _root = CampUiRuntime.CreateRect("SettingsRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_root);

            // 高于 ESC 菜单（5000），否则局内 ESC→设置会被挡住
            var overlayCanvas = _root.gameObject.GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = _root.gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 5200;
            if (_root.GetComponent<GraphicRaycaster>() == null)
                _root.gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = CampUiRuntime.CreateImage("Backdrop", _root, new Color(0f, 0f, 0f, 0.72f));
            CampUiRuntime.StretchFull(backdrop.rectTransform);

            var panelImg = CampUiRuntime.CreateImage("Panel", _root, Color.white);
            var panel = panelImg.rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelImg.preserveAspect = false;
            panelImg.raycastTarget = true;

            var plate = uiIcons != null ? uiIcons.UiEventPlate : null;
            if (plate != null)
            {
                panelImg.sprite = plate;
                panelImg.color = Color.white;
                panelImg.type = Image.Type.Simple;
            }
            else
            {
                panelImg.sprite = null;
                panelImg.color = new Color(0.08f, 0.09f, 0.12f, 0.98f);
                Debug.LogWarning("[Settings] 缺少 UiEventPlate，请执行 Grimhand → Content → Refresh UI Visual Catalogs。");
            }

            var title = CampUiRuntime.CreateText(panel, "设置", 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.SetAnchored(title.rectTransform, ZoneTitle.x, ZoneTitle.y, ZoneTitle.z, ZoneTitle.w);
            title.color = TitleColor;
            title.raycastTarget = false;

            _musicSlider = CreateVolumeRow(panel, "音乐", 0.72f, GameSettings.MusicVolume, v =>
            {
                GameSettings.MusicVolume = v;
                GameSettings.ApplyAudioVolumes();
                GameSettings.Save();
            });
            _sfxSlider = CreateVolumeRow(panel, "音效", 0.58f, GameSettings.SfxVolume, v =>
            {
                GameSettings.SfxVolume = v;
                GameSettings.ApplyAudioVolumes();
                GameSettings.Save();
            });

            BuildResolutionRow(panel);
            BuildFullscreenRow(panel);
            CreateBackButton(panel);

            _root.gameObject.SetActive(false);
        }

        void CreateBackButton(RectTransform panel)
        {
            var go = CampUiRuntime.CreateRect("Back", panel);
            var rt = go.GetComponent<RectTransform>();
            SetZoneCoverAspect(rt, ZoneBack, PanelWidth, PanelHeight, ButtonAspect);

            var img = go.AddComponent<Image>();
            img.color = Color.white;
            img.raycastTarget = true;
            img.preserveAspect = false;
            if (uiIcons != null && uiIcons.UiButton6 != null)
                img.sprite = uiIcons.UiButton6;
            else
                img.color = new Color(0.28f, 0.3f, 0.36f, 1f);

            var label = CampUiRuntime.CreateText(go.transform, "返回", 20, FontStyle.Bold, TextAnchor.MiddleCenter);
            CampUiRuntime.StretchFull(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(4f, 2f);
            label.rectTransform.offsetMax = new Vector2(-4f, -6f);
            label.color = ButtonLabelColor;
            label.raycastTarget = false;

            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            var hover = go.AddComponent<CampBuildingHoverView>();
            hover.Bind(rt, group, ButtonHoverScale, hideWhenIdle: false);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                Hide();
                _onClose?.Invoke();
            });
            UiAudioHooks.WireButton(btn);
        }

        void BuildResolutionRow(RectTransform panel)
        {
            var row = CampUiRuntime.CreateRect("ResolutionRow", panel).GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0.12f, 0.40f);
            row.anchorMax = new Vector2(0.88f, 0.52f);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;

            var label = CampUiRuntime.CreateText(row, "分辨率", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = LabelColor;
            label.raycastTarget = false;

            var prev = CampUiRuntime.CreateButton(row, "◀", new Color(0.22f, 0.26f, 0.34f, 1f),
                new Vector2(48f, 36f));
            var prevRt = prev.GetComponent<RectTransform>();
            prevRt.anchorMin = new Vector2(0.3f, 0.5f);
            prevRt.anchorMax = new Vector2(0.3f, 0.5f);
            prevRt.pivot = new Vector2(0.5f, 0.5f);
            prevRt.anchoredPosition = Vector2.zero;
            prev.onClick.AddListener(() =>
            {
                _resolutionIndex = (_resolutionIndex - 1 + GameSettings.ResolutionPresets.Length)
                                   % GameSettings.ResolutionPresets.Length;
                GameSettings.SetResolutionPreset(_resolutionIndex);
                RefreshResolutionLabel();
            });

            _resolutionLabel = CampUiRuntime.CreateText(row, "", 18, FontStyle.Bold, TextAnchor.MiddleCenter);
            _resolutionLabel.rectTransform.anchorMin = new Vector2(0.4f, 0f);
            _resolutionLabel.rectTransform.anchorMax = new Vector2(0.82f, 1f);
            _resolutionLabel.rectTransform.offsetMin = Vector2.zero;
            _resolutionLabel.rectTransform.offsetMax = Vector2.zero;
            _resolutionLabel.color = new Color(0.92f, 0.88f, 0.72f, 1f);
            _resolutionLabel.raycastTarget = false;

            var next = CampUiRuntime.CreateButton(row, "▶", new Color(0.22f, 0.26f, 0.34f, 1f),
                new Vector2(48f, 36f));
            var nextRt = next.GetComponent<RectTransform>();
            nextRt.anchorMin = new Vector2(0.9f, 0.5f);
            nextRt.anchorMax = new Vector2(0.9f, 0.5f);
            nextRt.pivot = new Vector2(0.5f, 0.5f);
            nextRt.anchoredPosition = Vector2.zero;
            next.onClick.AddListener(() =>
            {
                _resolutionIndex = (_resolutionIndex + 1) % GameSettings.ResolutionPresets.Length;
                GameSettings.SetResolutionPreset(_resolutionIndex);
                RefreshResolutionLabel();
            });
        }

        void BuildFullscreenRow(RectTransform panel)
        {
            var row = CampUiRuntime.CreateRect("FullscreenRow", panel).GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0.12f, 0.26f);
            row.anchorMax = new Vector2(0.88f, 0.38f);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;

            var label = CampUiRuntime.CreateText(row, "全屏", 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
            label.color = LabelColor;
            label.raycastTarget = false;

            var toggleGo = CampUiRuntime.CreateRect("FullscreenToggle", row);
            var toggleRt = toggleGo.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(0.32f, 0.5f);
            toggleRt.anchorMax = new Vector2(0.32f, 0.5f);
            toggleRt.pivot = new Vector2(0.5f, 0.5f);
            toggleRt.sizeDelta = new Vector2(36f, 36f);

            var bg = CampUiRuntime.CreateImage("Background", toggleGo.transform, new Color(0.18f, 0.2f, 0.24f, 1f));
            CampUiRuntime.StretchFull(bg.rectTransform);

            var check = CampUiRuntime.CreateImage("Checkmark", toggleGo.transform, new Color(0.92f, 0.88f, 0.72f, 1f));
            CampUiRuntime.Stretch(check.rectTransform, 6f, 6f, -6f, -6f);

            _fullscreenToggle = toggleGo.AddComponent<Toggle>();
            _fullscreenToggle.targetGraphic = bg;
            _fullscreenToggle.graphic = check;
            _fullscreenToggle.isOn = GameSettings.Fullscreen;
            _fullscreenToggle.onValueChanged.AddListener(v =>
            {
                GameSettings.Fullscreen = v;
                GameSettings.ApplyDisplaySettings();
                GameSettings.Save();
            });
        }

        void RefreshResolutionLabel()
        {
            if (_resolutionLabel == null)
                return;

            var preset = GameSettings.ResolutionPresets[
                Mathf.Clamp(_resolutionIndex, 0, GameSettings.ResolutionPresets.Length - 1)];
            _resolutionLabel.text = $"{preset.x} × {preset.y}";
        }

        static Slider CreateVolumeRow(RectTransform panel, string label, float anchorY, float initial, Action<float> onChanged)
        {
            var row = CampUiRuntime.CreateRect(label + "Row", panel).GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0.12f, anchorY);
            row.anchorMax = new Vector2(0.88f, anchorY + 0.10f);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;

            var labelText = CampUiRuntime.CreateText(row, label, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0.28f, 1f);
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;
            labelText.color = LabelColor;
            labelText.raycastTarget = false;

            var sliderGo = CampUiRuntime.CreateRect("Slider", row);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.3f, 0.2f);
            sliderRt.anchorMax = new Vector2(1f, 0.8f);
            sliderRt.offsetMin = Vector2.zero;
            sliderRt.offsetMax = Vector2.zero;

            var bg = CampUiRuntime.CreateImage("Background", sliderGo.transform, new Color(0.18f, 0.2f, 0.24f, 1f));
            CampUiRuntime.StretchFull(bg.rectTransform);

            var fillArea = CampUiRuntime.CreateRect("Fill Area", sliderGo.transform).GetComponent<RectTransform>();
            CampUiRuntime.Stretch(fillArea, 8f, 6f, -8f, -6f);

            var fill = CampUiRuntime.CreateImage("Fill", fillArea, new Color(0.45f, 0.55f, 0.35f, 1f));
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var handleArea = CampUiRuntime.CreateRect("Handle Slide Area", sliderGo.transform)
                .GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(handleArea);

            var handle = CampUiRuntime.CreateImage("Handle", handleArea, new Color(0.92f, 0.88f, 0.72f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 18f);

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRt;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = initial;
            slider.onValueChanged.AddListener(v => onChanged?.Invoke(v));

            return slider;
        }

        static void SetZoneCoverAspect(RectTransform rt, Vector4 zone, float plateW, float plateH, float aspect)
        {
            var cx = (zone.x + zone.z) * 0.5f;
            var cy = (zone.y + zone.w) * 0.5f;
            var rw = (zone.z - zone.x) * plateW;
            var rh = (zone.w - zone.y) * plateH;
            var bw = rw;
            var bh = bw / aspect;
            if (bh < rh)
            {
                bh = rh;
                bw = bh * aspect;
            }

            var nw = bw / plateW;
            var nh = bh / plateH;
            CampUiRuntime.SetAnchored(rt, cx - nw * 0.5f, cy - nh * 0.5f, cx + nw * 0.5f, cy + nh * 0.5f);
        }
    }
}
