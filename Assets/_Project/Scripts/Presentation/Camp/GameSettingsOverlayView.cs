using System;
using UnityEngine;
using UnityEngine.UI;

namespace Grimhand.Presentation.Camp
{
    /// <summary>音量设置面板（音效未实装，先持久化数值）。</summary>
    [DisallowMultipleComponent]
    public sealed class GameSettingsOverlayView : MonoBehaviour
    {
        bool _built;
        RectTransform _root;
        Slider _masterSlider;
        Slider _musicSlider;
        Slider _sfxSlider;
        Action _onClose;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public void Initialize(Action onClose)
        {
            _onClose = onClose;
            EnsureBuilt();
        }

        public void Show()
        {
            EnsureBuilt();
            _masterSlider.SetValueWithoutNotify(GameSettings.MasterVolume);
            _musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            _sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            _root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
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

            _root = CampUiRuntime.CreateRect("SettingsRoot", transform).GetComponent<RectTransform>();
            CampUiRuntime.StretchFull(_root);

            var backdrop = CampUiRuntime.CreateImage("Backdrop", _root, new Color(0f, 0f, 0f, 0.72f));
            CampUiRuntime.StretchFull(backdrop.rectTransform);

            var panel = CampUiRuntime.CreateImage("Panel", _root, new Color(0.08f, 0.09f, 0.12f, 0.98f))
                .rectTransform;
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(420f, 340f);

            var title = CampUiRuntime.CreateText(panel, "设置", 28, FontStyle.Bold, TextAnchor.UpperCenter);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.offsetMin = new Vector2(0f, -56f);
            title.rectTransform.offsetMax = new Vector2(0f, -8f);

            _masterSlider = CreateVolumeRow(panel, "主音量", 0.72f, GameSettings.MasterVolume, v =>
            {
                GameSettings.MasterVolume = v;
                GameSettings.ApplyAudioVolumes();
                GameSettings.Save();
            });
            _musicSlider = CreateVolumeRow(panel, "音乐", 0.52f, GameSettings.MusicVolume, v =>
            {
                GameSettings.MusicVolume = v;
                GameSettings.Save();
            });
            _sfxSlider = CreateVolumeRow(panel, "音效", 0.32f, GameSettings.SfxVolume, v =>
            {
                GameSettings.SfxVolume = v;
                GameSettings.Save();
            });

            var hint = CampUiRuntime.CreateText(panel, "音效系统尚未实装，音量设置已保存。", 14, FontStyle.Italic,
                TextAnchor.LowerCenter);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.offsetMin = new Vector2(16f, 56f);
            hint.rectTransform.offsetMax = new Vector2(-16f, 96f);
            hint.color = new Color(0.7f, 0.72f, 0.78f, 1f);

            var closeBtn = CampUiRuntime.CreateButton(panel, "返回", new Color(0.28f, 0.3f, 0.36f, 1f),
                new Vector2(140f, 44f));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 16f);
            closeBtn.onClick.AddListener(() =>
            {
                Hide();
                _onClose?.Invoke();
            });

            _root.gameObject.SetActive(false);
        }

        static Slider CreateVolumeRow(RectTransform panel, string label, float anchorY, float initial, Action<float> onChanged)
        {
            var row = CampUiRuntime.CreateRect(label + "Row", panel).GetComponent<RectTransform>();
            row.anchorMin = new Vector2(0.08f, anchorY);
            row.anchorMax = new Vector2(0.92f, anchorY + 0.14f);
            row.offsetMin = Vector2.zero;
            row.offsetMax = Vector2.zero;

            var labelText = CampUiRuntime.CreateText(row, label, 18, FontStyle.Normal, TextAnchor.MiddleLeft);
            labelText.rectTransform.anchorMin = new Vector2(0f, 0f);
            labelText.rectTransform.anchorMax = new Vector2(0.32f, 1f);
            labelText.rectTransform.offsetMin = Vector2.zero;
            labelText.rectTransform.offsetMax = Vector2.zero;

            var sliderGo = CampUiRuntime.CreateRect("Slider", row);
            var sliderRt = sliderGo.GetComponent<RectTransform>();
            sliderRt.anchorMin = new Vector2(0.34f, 0.2f);
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
    }
}
