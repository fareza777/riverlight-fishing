using System.Collections;
using System.IO;
using CozyFishing.Core;
using CozyFishing.Fishing;
using CozyFishing.Player;
using CozyFishing.Story;
using CozyFishing.UI;
using CozyFishing.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Runtime self-test, only active with "-selftest-tour". Walks the whole game in the real player build and
    /// screenshots each step to persistentDataPath/selftest_tour: title, new game (Lena), intro cutscene, every
    /// location by day, Mira's shop, map, journal, a memory cutscene and the Indonesian UI. The player's own save is
    /// backed up first and restored at the end.
    /// </summary>
    public sealed class TourSelfTest : MonoBehaviour
    {
        string dir;
        int shot;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!DevArgs.Has("-selftest-tour")) return;
            var go = new GameObject("TourSelfTest");
            DontDestroyOnLoad(go);
            go.AddComponent<TourSelfTest>();
        }

        IEnumerator Start()
        {
            dir = Path.Combine(Application.persistentDataPath, "selftest_tour");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            string backup = GameState.SavePath + ".tourbak";
            if (GameState.HasSave) File.Copy(GameState.SavePath, backup, true);
            Loc.Preview(Language.English);

            yield return Wait(1.2f);
            yield return Shot("splash");
            yield return Wait(4f);
            yield return Shot("title");

            GameState.Instance.NewGame("FisherF", "Lena");
            SceneManager.LoadScene("River");
            yield return Wait(4f);
            yield return Shot("intro_title");
            yield return WaitUntilSlide(6f);
            yield return Shot("intro_slide");
            CutscenePlayer.SkipAll();
            yield return Wait(3f);

            foreach (var scene in new[] { "River", "Lake", "Gorge", "Estuary" })
            {
                if (SceneManager.GetActiveScene().name != scene)
                {
                    GameState.Instance.Data.chapter = 5;
                    GameState.Instance.Travel(GameState.Db.Location(scene.ToLowerInvariant()));
                    yield return Wait(4f);
                }
                FindAnyObjectByType<TimeOfDay>()?.SetHour(10f);
                yield return Wait(1.5f);
                yield return Shot($"{scene}_day");
                yield return MeasureFps(scene);
                yield return FishOnce(scene);
                if (scene == "Lake")
                {
                    FindAnyObjectByType<TimeOfDay>()?.SetHour(21f);
                    yield return Wait(1.5f);
                    yield return Shot("Lake_night");
                }
            }

            ShopUI.Open();
            VoicePlayer();
            yield return Wait(1f);
            yield return Shot("shop");
            ShopClose();
            PauseMenu.Open(PauseMenu.Tab.Map);
            yield return Wait(0.8f);
            yield return Shot("map");
            PauseMenu.Open(PauseMenu.Tab.Journal);
            yield return Wait(0.8f);
            yield return Shot("journal");
            PauseMenu.Open(PauseMenu.Tab.Settings);
            yield return Wait(0.8f);
            yield return Shot("settings");
            PauseMenu.Close();

            CutscenePlayer.Play(GameState.Db.Cutscene("memory_2"));
            yield return WaitUntilSlide(6f);
            yield return Shot("memory_2");
            CutscenePlayer.SkipAll();
            yield return Wait(2f);

            Loc.Preview(Language.Indonesian);
            PauseMenu.Open(PauseMenu.Tab.Journal);
            yield return Wait(0.8f);
            yield return Shot("journal_id");
            PauseMenu.Close();
            Loc.EndPreview();

            if (File.Exists(backup)) { File.Copy(backup, GameState.SavePath, true); File.Delete(backup); }
            else File.Delete(GameState.SavePath);
            Debug.Log($"[TourTest] done, {shot} shots -> {dir}");
            yield return Wait(0.5f);
            Application.Quit();
        }

        /// <summary>Boards, moves into a fishing spot and plays one full catch through the touch inputs.</summary>
        IEnumerator FishOnce(string scene)
        {
            var modes = FindAnyObjectByType<PlayerModes>();
            var fishing = FindAnyObjectByType<FishingController>();
            string result = "none";
            fishing.Caught += (f, isNew) => result = $"CAUGHT {f.Species.Id} ({f.Species.Location}) {f.SizeCm:0}cm";
            fishing.Lost += r => result = "LOST " + r;
            modes.Board();
            yield return Wait(1.6f);
            var spots = FindObjectsByType<FishingSpot>(FindObjectsSortMode.InstanceID);
            var spot = spots[shot % spots.Length];
            var body = fishing.GetComponent<Rigidbody>();
            body.position = spot.transform.position + new Vector3(0f, -0.14f, 0f);
            body.linearVelocity = Vector3.zero;
            FindAnyObjectByType<CameraSystem.CameraRig>()?.Snap();
            yield return Wait(0.8f);
            var input = GameInput.Instance;
            input.SetTouchAction(true);
            yield return Wait(0.55f);
            input.SetTouchAction(false);
            float timeout = Time.time + 20f;
            while (fishing.State != FishingState.Bite && Time.time < timeout) yield return null;
            input.SetTouchAction(true);
            yield return null;
            input.SetTouchAction(false);
            float reelShot = Time.time + 2f;
            bool shotTaken = false;
            while (fishing.State == FishingState.Reeling)
            {
                float mid = (fishing.SafeBand.x + fishing.SafeBand.y) * 0.5f;
                input.SetTouchAction(!fishing.FishRunning && fishing.Tension < mid);   // ease off while the fish runs
                if (!shotTaken && Time.time > reelShot) { shotTaken = true; yield return Shot($"{scene}_reel"); }
                yield return null;
            }
            input.SetTouchAction(false);
            yield return Wait(1.5f);
            yield return Shot($"{scene}_catch");
            Debug.Log($"[TourTest] {scene} spot '{spot.SpotName}': {result}");
            if (fishing.State == FishingState.Landing) fishing.ConfirmCatch();
            yield return Wait(1f);
        }

        /// <summary>Average frame rate over 5 s of normal play (camera following the hero).</summary>
        static IEnumerator MeasureFps(string scene)
        {
            int frames = 0;
            float t = 0f, worst = 0f;
            while (t < 5f)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
                frames++;
            }
            Debug.Log($"[TourTest] fps {scene} avg={frames / t:0.0} worstMs={worst * 1000f:0} level={GraphicsQuality.Level} res={Screen.width}x{Screen.height}");
        }

        static void VoicePlayer() => Audio.VoicePlayer.Say("mira_0");

        static void ShopClose()
        {
            var shop = FindAnyObjectByType<ShopUI>();
            if (shop != null) shop.SendMessage("Close");
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        static IEnumerator WaitUntilSlide(float s) => Wait(s);

        IEnumerator Shot(string label)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"{shot++:00}_{label}.png"));
            Debug.Log($"[TourTest] shot {label} scene={SceneManager.GetActiveScene().name}");
            yield return null;
        }
    }
}
