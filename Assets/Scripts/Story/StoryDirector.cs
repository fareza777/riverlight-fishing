using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.Data;
using CozyFishing.Fishing;
using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.Story
{
    /// <summary>
    /// Drives "Grandpa's Legacy": intro on a new game, first-catch prologue, one legend per chapter
    /// (each legend plays a memory and unlocks the next location), Moon Eel finale and ending.
    /// One per location scene.
    /// </summary>
    public sealed class StoryDirector : MonoBehaviour
    {
        public const int FinalChapter = 5;

        void Start()
        {
            var gs = GameState.Instance;
            gs.FishCaught += OnFishCaught;
            if (!gs.Data.seenCutscenes.Contains("intro"))
                Play("intro", () => { Toast.Show(Loc.T("obj_0"), 7f); VoicePlayer.Say("obj_0"); });
            else if (gs.Data.chapter <= FinalChapter)
                Toast.Show(Objective(gs.Data.chapter), 6f);
        }

        void OnDestroy()
        {
            if (GameState.Instance != null) GameState.Instance.FishCaught -= OnFishCaught;
        }

        public static string Objective(int chapter)
        {
            if (chapter > FinalChapter) return Loc.T("obj_done");
            string need = Progression.NeedText(chapter);
            return string.IsNullOrEmpty(need) ? Loc.T("obj_" + chapter) : Loc.T("obj_" + chapter) + "\n<size=28><i>" + need + "</i></size>";
        }

        void OnFishCaught(FishSpecies species, float size, bool isNew)
        {
            var gs = GameState.Instance;
            foreach (var message in Progression.ClaimCollections())
            {
                SfxPlayer.Play("coins", 0.9f, 0f);
                Toast.Show(message, 5f);
            }
            if (Progression.JustBecameReady(species, isNew))
            {
                // The water has been learned: Grandpa's journal says the legend can now be found.
                Toast.Show(Loc.T("ready_" + gs.Data.chapter), 7f);
                VoicePlayer.Say("ready_" + gs.Data.chapter);
            }
            if (gs.Data.chapter == 0)
            {
                // Prologue goal: the very first fish.
                gs.AdvanceChapter(1);
                Invoke(nameof(AnnounceChapter), 3.5f);
                return;
            }
            if (!species.IsLegend || species.Chapter != gs.Data.chapter) return;

            int chapter = gs.Data.chapter;
            MusicPlayer.PlayMusic(null, 1f);
            Play("memory_" + chapter, () =>
            {
                if (chapter >= FinalChapter)
                {
                    Play("ending", () =>
                    {
                        gs.AdvanceChapter(FinalChapter + 1);
                        Toast.Show(Loc.T("obj_done"), 7f);
                        VoicePlayer.Say("obj_done");
                    });
                    return;
                }
                gs.AdvanceChapter(chapter + 1);
                if (chapter + 1 == FinalChapter)
                {
                    // Grandpa's heirloom rod: the only rod strong enough for the Moon Eel.
                    gs.Grant("rod_grandpa");
                    Toast.Show(Loc.T("got_grandpa_rod"), 6f);
                }
                AnnounceChapter();
                foreach (var loc in GameState.Db.locations)
                    if (loc.unlockChapter == chapter + 1) Toast.Show(Loc.T("unlocked", loc.Name), 5f);
            });
        }

        void AnnounceChapter()
        {
            SfxPlayer.Play("chapter", 0.8f, 0f);
            int ch = GameState.Instance.Data.chapter;
            Toast.Show(Objective(ch), 8f);
            VoicePlayer.Say(ch > FinalChapter ? "obj_done" : "obj_" + ch);
        }

        static void Play(string id, System.Action done)
        {
            var def = GameState.Db.Cutscene(id);
            if (def == null) { done?.Invoke(); return; }
            CutscenePlayer.Play(def, () =>
            {
                var music = FindAnyObjectByType<World.LocationAmbience>();
                if (music != null) music.Resume();
                done?.Invoke();
            });
        }
    }
}
