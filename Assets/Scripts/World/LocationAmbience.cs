using CozyFishing.Audio;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>Per-location music and ambience bed; switches to the night track after dark.</summary>
    public sealed class LocationAmbience : MonoBehaviour
    {
        [SerializeField] string dayMusic = "river_day";
        [SerializeField] string nightMusic = "night";
        [SerializeField] string ambience = "river";
        [SerializeField] TimeOfDay timeOfDay;

        bool night;

        void Start()
        {
            if (timeOfDay == null) timeOfDay = FindAnyObjectByType<TimeOfDay>();
            Resume();
        }

        public void Resume()
        {
            night = IsNight;
            MusicPlayer.PlayMusic(night ? nightMusic : dayMusic, 2.5f);
            MusicPlayer.PlayAmbience(ambience);
        }

        bool IsNight => timeOfDay != null && (timeOfDay.Hour >= 19.5f || timeOfDay.Hour < 5.5f);

        void Update()
        {
            if (Story.CutscenePlayer.Playing) return;
            if (IsNight != night) Resume();
        }

#if UNITY_EDITOR
        public void EditorSetup(string day, string nightTrack, string amb, TimeOfDay tod)
        {
            dayMusic = day; nightMusic = nightTrack; ambience = amb; timeOfDay = tod;
        }
#endif
    }
}
