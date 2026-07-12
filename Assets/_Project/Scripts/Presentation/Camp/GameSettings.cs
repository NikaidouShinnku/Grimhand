using UnityEngine;

namespace Grimhand.Presentation.Camp
{
    /// <summary>音量等偏好（音效未实装，先持久化数值）。</summary>
    public static class GameSettings
    {
        const string MasterVolumeKey = "grimhand.master_volume";
        const string MusicVolumeKey = "grimhand.music_volume";
        const string SfxVolumeKey = "grimhand.sfx_volume";

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

        public static void Save() => PlayerPrefs.Save();

        public static void ApplyAudioVolumes() => AudioListener.volume = MasterVolume;
    }
}
