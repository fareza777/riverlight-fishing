using System;
using System.Collections.Generic;
using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Story
{
    [Serializable]
    public sealed class CutsceneSlide
    {
        public Sprite image;
        [TextArea] public string textEN;
        [TextArea] public string textID;
        public AudioClip voice;
        [Tooltip("Indonesian narration (falls back to the English clip).")] public AudioClip voiceID;
        [Tooltip("The player's own lines voiced for Kai (the plain clips are Lena's).")] public AudioClip voiceM, voiceMID;
        [Tooltip("Seconds on screen (voice length is used when longer).")] public float seconds = 6f;
        [Tooltip("Ken Burns move: start/end zoom and normalized pan offset.")]
        public float zoomFrom = 1.05f, zoomTo = 1.15f;
        public Vector2 panFrom, panTo = new(0.02f, -0.02f);

        public string Text => Loc.Current == Language.Indonesian ? textID : textEN;
        public AudioClip Voice
        {
            get
            {
                bool id = Loc.Current == Language.Indonesian;
                bool arka = GameState.Instance != null && GameState.Instance.Data.character == "FisherM";
                if (arka && voiceM != null) return id && voiceMID != null ? voiceMID : voiceM;
                return id && voiceID != null ? voiceID : voice;
            }
        }
    }

    /// <summary>An illustrated, narrated story sequence (intro, memories, ending).</summary>
    [CreateAssetMenu(menuName = "CozyFishing/Cutscene", fileName = "Cutscene_")]
    public sealed class CutsceneDef : ScriptableObject
    {
        public string id;
        public string titleEN, titleID;
        [Tooltip("Memories are shown in warm sepia with soft vignette.")] public bool memory;
        public AudioClip music;
        public List<CutsceneSlide> slides = new();

        public string Title => Loc.Current == Language.Indonesian ? titleID : titleEN;
    }
}
