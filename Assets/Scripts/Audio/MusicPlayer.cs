using System.Collections;
using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Audio
{
    /// <summary>
    /// Background music with cross-fades plus a looping ambience bed (river, lake, wind, surf).
    /// Clips load from Resources/Audio/Music and Resources/Audio/Ambience.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        static MusicPlayer instance;
        AudioSource a, b, ambience;
        bool aActive = true;
        string current, currentAmb;

        static MusicPlayer Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("MusicPlayer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<MusicPlayer>();
                instance.a = Make(go); instance.b = Make(go); instance.ambience = Make(go);
                return instance;
            }
        }

        static AudioSource Make(GameObject go)
        {
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false; s.volume = 0f; s.ignoreListenerPause = true;
            return s;
        }

        public static void PlayMusic(string name, float fade = 2f)
        {
            var p = Instance;
            if (p.current == name) return;
            p.current = name;
            var clip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Audio/Music/" + name);
            var from = p.aActive ? p.a : p.b;
            var to = p.aActive ? p.b : p.a;
            p.aActive = !p.aActive;
            p.StopAllCoroutines();
            p.StartCoroutine(p.Crossfade(from, to, clip, fade));
            if (p.ambience.clip != null) p.StartCoroutine(p.FadeTo(p.ambience, AmbVolume, 1.5f));
        }

        public static void PlayAmbience(string name)
        {
            var p = Instance;
            if (p.currentAmb == name) return;
            p.currentAmb = name;
            var clip = string.IsNullOrEmpty(name) ? null : Resources.Load<AudioClip>("Audio/Ambience/" + name);
            p.ambience.clip = clip;
            if (clip != null) { p.ambience.Play(); p.StartCoroutine(p.FadeTo(p.ambience, AmbVolume, 2f)); }
            else p.ambience.Stop();
        }

        static float AmbVolume => 0.5f * Settings.SfxVolume;

        public static void ApplyVolume()
        {
            if (instance == null) return;
            (instance.aActive ? instance.a : instance.b).volume = 0.45f * Settings.MusicVolume;
            instance.ambience.volume = AmbVolume;
        }

        IEnumerator Crossfade(AudioSource from, AudioSource to, AudioClip clip, float seconds)
        {
            to.clip = clip;
            if (clip != null) to.Play();
            float target = 0.45f * Settings.MusicVolume;
            float fromStart = from.volume;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / Mathf.Max(seconds, 0.01f))
            {
                from.volume = Mathf.Lerp(fromStart, 0f, t);
                to.volume = clip != null ? Mathf.Lerp(0f, target, t) : 0f;
                yield return null;
            }
            from.volume = 0f; from.Stop();
            to.volume = clip != null ? target : 0f;
        }

        IEnumerator FadeTo(AudioSource s, float target, float seconds)
        {
            float start = s.volume;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / seconds) { s.volume = Mathf.Lerp(start, target, t); yield return null; }
            s.volume = target;
        }
    }
}
