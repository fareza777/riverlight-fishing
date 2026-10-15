using System.Collections.Generic;
using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Audio
{
    /// <summary>One-shot sound effects loaded by name from Resources/Audio/Sfx. Missing clips are silently skipped.</summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        static SfxPlayer instance;
        readonly Dictionary<string, AudioClip> cache = new();
        AudioSource source;

        static SfxPlayer Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("SfxPlayer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<SfxPlayer>();
                instance.source = go.AddComponent<AudioSource>();
                instance.source.playOnAwake = false;
                instance.source.ignoreListenerPause = true;
                return instance;
            }
        }

        public static void Play(string name, float volume = 1f, float pitchJitter = 0.05f)
        {
            var p = Instance;
            if (!p.cache.TryGetValue(name, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/Sfx/" + name);
                p.cache[name] = clip;
            }
            if (clip == null) return;
            p.source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            p.source.PlayOneShot(clip, volume * Settings.SfxVolume);
        }

        public static void Click() => Play("ui_click", 0.6f, 0.02f);
    }
}
