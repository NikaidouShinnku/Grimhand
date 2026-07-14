using Grimhand.Presentation.Audio;
using UnityEngine;

namespace Grimhand.Presentation.Camp
{
    /// <summary>主音量 / 音乐 / 音效 / 分辨率偏好。</summary>
    public static class GameSettings
    {
        const string MasterVolumeKey = "grimhand.master_volume";
        const string MusicVolumeKey = "grimhand.music_volume";
        const string SfxVolumeKey = "grimhand.sfx_volume";
        const string ResolutionWidthKey = "grimhand.resolution_width";
        const string ResolutionHeightKey = "grimhand.resolution_height";
        const string FullscreenKey = "grimhand.fullscreen";

        public static readonly Vector2Int[] ResolutionPresets =
        {
            new(1280, 720),
            new(1600, 900),
            new(1920, 1080),
            new(2560, 1440),
            new(3840, 2160)
        };

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MasterVolumeKey, 0.8f);
            set => PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f);
            set => PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f);
            set => PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        public static int ResolutionWidth
        {
            get => PlayerPrefs.GetInt(ResolutionWidthKey, Screen.width);
            set => PlayerPrefs.SetInt(ResolutionWidthKey, Mathf.Max(640, value));
        }

        public static int ResolutionHeight
        {
            get => PlayerPrefs.GetInt(ResolutionHeightKey, Screen.height);
            set => PlayerPrefs.SetInt(ResolutionHeightKey, Mathf.Max(480, value));
        }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
            set => PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        }

        public static void Save() => PlayerPrefs.Save();

        public static void ApplyAudioVolumes()
        {
            if (GameAudioService.Instance != null)
                GameAudioService.Instance.ApplyVolumes();
            else
                AudioListener.volume = MasterVolume;
        }

        public static void ApplyDisplaySettings()
        {
            var width = ResolutionWidth;
            var height = ResolutionHeight;
            if (width <= 0 || height <= 0)
            {
                width = Screen.width;
                height = Screen.height;
            }

            Screen.SetResolution(
                width,
                height,
                Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
        }

        public static int FindClosestPresetIndex()
        {
            var best = 0;
            var bestScore = int.MaxValue;
            for (var i = 0; i < ResolutionPresets.Length; i++)
            {
                var preset = ResolutionPresets[i];
                var score = Mathf.Abs(preset.x - ResolutionWidth) + Mathf.Abs(preset.y - ResolutionHeight);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                best = i;
            }

            return best;
        }

        public static void SetResolutionPreset(int index)
        {
            if (index < 0 || index >= ResolutionPresets.Length)
                return;

            var preset = ResolutionPresets[index];
            ResolutionWidth = preset.x;
            ResolutionHeight = preset.y;
            ApplyDisplaySettings();
            Save();
        }
    }
}
