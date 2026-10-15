using UnityEngine;

namespace CozyFishing.Core
{
    /// <summary>Player preferences (PlayerPrefs): volumes, vibration. Language lives in <see cref="Loc"/>.</summary>
    public static class Settings
    {
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat("vol_music", 0.7f);
            set { PlayerPrefs.SetFloat("vol_music", Mathf.Clamp01(value)); Audio.MusicPlayer.ApplyVolume(); }
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat("vol_sfx", 0.9f);
            set => PlayerPrefs.SetFloat("vol_sfx", Mathf.Clamp01(value));
        }

        public static float VoiceVolume
        {
            get => PlayerPrefs.GetFloat("vol_voice", 1f);
            set => PlayerPrefs.SetFloat("vol_voice", Mathf.Clamp01(value));
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("vibration", 1) == 1;
            set => PlayerPrefs.SetInt("vibration", value ? 1 : 0);
        }

        public static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Vibration) Handheld.Vibrate();
#endif
        }
    }
}
