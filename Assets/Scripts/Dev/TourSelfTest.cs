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
            bool flag = Application.isEditor && File.Exists("Temp/tour.flag");
            if (!DevArgs.Has("-selftest-tour") && !flag) return;
            if (flag) File.Delete("Temp/tour.flag");
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
                if (scene == "River") yield return Expansions();
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
            var fishing = FindAnyObjectByType<Boat.BoatController>().GetComponent<FishingController>();
            string result = "none";
            fishing.StateChanged += st => Debug.Log($"[TourTest] {scene} state -> {st} spot={fishing.CurrentSpot?.SpotName} t={Time.time:0.0}");
            fishing.Caught += (f, isNew) => result = $"CAUGHT {f.Species.Id} ({f.Species.Location}) {f.SizeCm:0}cm";
            fishing.Lost += r => result = "LOST " + r;
            modes.Board();
            // Let the boarding hop finish before moving the boat into the spot.
            float until = Time.unscaledTime + 5f;
            while (!modes.InBoat && Time.unscaledTime < until) yield return null;
            yield return Wait(0.8f);
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

        /// <summary>Dock fishing, buying decor and an outfit, the decorated cabin, Felix's wagon.</summary>
        IEnumerator Expansions()
        {
            var gs = GameState.Instance;
            gs.Data.coins += 3000;
            gs.Data.chapter = Mathf.Max(gs.Data.chapter, 1);

            // Fishing from the end of the dock.
            var modes = FindAnyObjectByType<PlayerModes>();
            var point = FindAnyObjectByType<Interaction.ShoreFishingPoint>();
            if (point != null)
            {
                modes.StartShore(point);
                yield return Wait(1f);
                yield return Shot("dock_ready");
                string result = "none";
                point.Fishing.Caught += (f, isNew) => result = $"CAUGHT {f.Species.Id} {f.SizeCm:0}cm";
                point.Fishing.Lost += r => result = "LOST " + r;
                yield return PlayCatch(point.Fishing, "River_dock");
                Debug.Log($"[TourTest] River dock fishing: {result} heroY={modes.transform.position.y:0.00} standY={point.Stand.position.y:0.00}");
                if (point.Fishing.State == FishingState.Landing) point.Fishing.ConfirmCatch();
                yield return Wait(1f);
                GameInput.Instance.SetTouchMove(new Vector2(0f, -1f));
                yield return Wait(0.4f);
                GameInput.Instance.SetTouchMove(Vector2.zero);
                Debug.Log($"[TourTest] stood up from the dock: onShore={modes.OnShore}");
            }

            // Shopping: decor for the cabin and an outfit for the hero.
            foreach (var id in new[] { "decor_rug", "decor_aquarium", "decor_lamp", "decor_clock", "decor_fern", "decor_painting", "outfit_lena_raincoat" })
                gs.Buy(GameState.Db.Item(id));
            Debug.Log($"[TourTest] outfit={gs.Data.outfit} owned={gs.Data.owned.Count}");
            yield return Wait(1f);
            yield return Shot("outfit_raincoat");

            // Inside the cabin.
            var door = FindObjectsByType<Interaction.DoorPortal>(FindObjectsSortMode.None);
            foreach (var d in door)
            {
                if (d.Prompt != Loc.T("enter")) continue;
                d.Interact(FindAnyObjectByType<Interaction.PlayerInteractor>());
                yield return Wait(2f);
                yield return Shot("cabin_decor");
                break;
            }
            foreach (var d in FindObjectsByType<Interaction.DoorPortal>(FindObjectsSortMode.None))
                if (d.Prompt == Loc.T("exit")) { d.Interact(FindAnyObjectByType<Interaction.PlayerInteractor>()); break; }
            yield return Wait(2f);

            // Felix visits on every third day.
            gs.Data.day = 3;
            yield return Wait(3f);
            var cart = GameObject.Find("Prop_MerchantCart");
            Debug.Log($"[TourTest] merchant in town={World.TravelingMerchant.InTown} cart visible={(cart != null && cart.activeInHierarchy)}");
            var walker = FindAnyObjectByType<PlayerController>();
            if (cart != null)
            {
                walker.Teleport(cart.transform.position + cart.transform.forward * 3.5f, Quaternion.LookRotation(-cart.transform.forward));
                yield return Wait(1.5f);
                yield return Shot("merchant");
                FindAnyObjectByType<Interaction.MerchantStall>()?.Interact(FindAnyObjectByType<Interaction.PlayerInteractor>());
                yield return Wait(1f);
                yield return Shot("merchant_shop");
                var shop = FindAnyObjectByType<ShopUI>();
                if (shop != null) shop.SendMessage("Close");
            }
            gs.Equip(GameState.Db.Item("outfit_lena_raincoat"));   // back to the default clothes
            gs.Data.day = 1;
        }

        /// <summary>Plays one cast / strike / reel through the touch inputs on the given controller.</summary>
        IEnumerator PlayCatch(FishingController fishing, string label)
        {
            var input = GameInput.Instance;
            input.SetTouchAction(true);
            yield return Wait(0.55f);
            input.SetTouchAction(false);
            float timeout = Time.time + 25f;
            while (fishing.State != FishingState.Bite && Time.time < timeout) yield return null;
            input.SetTouchAction(true);
            yield return null;
            input.SetTouchAction(false);
            bool shotTaken = false;
            float reelShot = Time.time + 2f;
            while (fishing.State == FishingState.Reeling)
            {
                float mid = (fishing.SafeBand.x + fishing.SafeBand.y) * 0.5f;
                input.SetTouchAction(!fishing.FishRunning && fishing.Tension < mid);
                if (!shotTaken && Time.time > reelShot) { shotTaken = true; yield return Shot($"{label}_reel"); }
                yield return null;
            }
            input.SetTouchAction(false);
            yield return Wait(1.5f);
            yield return Shot($"{label}_catch");
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
