using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Audio
{
    /// <summary>
    /// Voiced story lines (objectives, NPC dialogue) from Resources/Audio/Voice/Lines/&lt;key&gt;[_id].
    /// Indonesian clips are used when that language is selected; otherwise (or if missing) the English one.
    /// </summary>
    public sealed class VoicePlayer : MonoBehaviour
    {
        static VoicePlayer instance;
        AudioSource source;

        static VoicePlayer Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("VoicePlayer");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<VoicePlayer>();
                instance.source = go.AddComponent<AudioSource>();
                instance.source.playOnAwake = false;
                instance.source.ignoreListenerPause = true;
                return instance;
            }
        }

        public static void Say(string key)
        {
            AudioClip clip = null;
            if (Loc.Current == Language.Indonesian) clip = Resources.Load<AudioClip>($"Audio/Voice/Lines/{key}_id");
            if (clip == null) clip = Resources.Load<AudioClip>($"Audio/Voice/Lines/{key}");
            if (clip == null) return;
            var p = Instance;
            p.source.Stop();
            p.source.clip = clip;
            p.source.volume = Settings.VoiceVolume;
            p.source.Play();
        }
    }
}
