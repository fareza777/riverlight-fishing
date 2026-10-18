using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using CozyFishing.Core;
using CozyFishing.Player;
using CozyFishing.Fishing;
using CozyFishing.World;
using CozyFishing.UI;
using CozyFishing.Story;
using CozyFishing.CameraSystem;

// Run in memory with Unity Pipeline. This file is deliberately outside Assets.
// Captures real game content; it never modifies scenes or production scripts.
public static class RiverlightCampaignCapture
{
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../Marketing/Remotion/public/gameplay"));
    static string Backup => Path.GetFullPath(Path.Combine(Application.dataPath, "../.utmp/campaign-save-original.json"));
    static int previousGraphics;

    public static string Prepare()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.GetDirectoryName(Backup));
        if (File.Exists(GameState.SavePath) && !File.Exists(Backup)) File.Copy(GameState.SavePath, Backup);
        previousGraphics = PlayerPrefs.GetInt("gfx_quality", -1);
        File.WriteAllText(Path.Combine(Root, "previous-graphics.txt"), previousGraphics.ToString());
        CozyFishing.EditorTools.GameViewPortrait.Apply(1080, 1920);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        return "Prepared 1080 x 1920; original player save backed up.";
    }

    public static string Start()
    {
        Application.runInBackground = true;
        var gs = GameState.Instance;
        gs.StartCoroutine(Run(gs));
        return "Campaign capture started. Progress: Marketing/Remotion/public/gameplay/progress.txt";
    }

    static void Progress(string message)
    {
        File.WriteAllText(Path.Combine(Root, "progress.txt"), message);
        Debug.Log("[Campaign] " + message);
    }

    static IEnumerator Run(GameState gs)
    {
        Progress("Starting English gameplay capture");
        Loc.Preview(Language.English);
        GraphicsQuality.Level = GfxLevel.High;
        gs.NewGame("FisherM", "Kai");
        gs.Data.seenCutscenes.AddRange(GameState.Db.cutscenes.Where(c => c != null).Select(c => c.id));
        gs.Data.chapter = 1;
        gs.Data.coins = 2340;
        SceneManager.LoadScene("River");
        yield return new WaitForSecondsRealtime(5);
        Setup();
        SetHour(10.5f);
        FaceAway();
        yield return new WaitForSecondsRealtime(3);
        yield return Shot("river-home");

        // A real cabin decorated with the pieces available from the game's shop.
        foreach (var id in new[] { "decor_rug", "decor_aquarium", "decor_lamp", "decor_clock", "decor_fern", "decor_painting" })
        {
            var item = GameState.Db.Item(id);
            if (item != null) gs.Buy(item);
        }
        var entrance = UnityEngine.Object.FindObjectsByType<CozyFishing.Interaction.DoorPortal>(FindObjectsSortMode.None).FirstOrDefault(d => d.Prompt == Loc.T("enter"));
        if (entrance != null)
        {
            entrance.Interact(UnityEngine.Object.FindAnyObjectByType<CozyFishing.Interaction.PlayerInteractor>());
            yield return new WaitForSecondsRealtime(3);
            FaceAway();
            yield return Shot("cabin");
            yield return Clip("cabin", 50, Vector2.zero);
            var exit = UnityEngine.Object.FindObjectsByType<CozyFishing.Interaction.DoorPortal>(FindObjectsSortMode.None).FirstOrDefault(d => d.Prompt == Loc.T("exit"));
            if (exit != null) exit.Interact(UnityEngine.Object.FindAnyObjectByType<CozyFishing.Interaction.PlayerInteractor>());
            yield return new WaitForSecondsRealtime(3);
        }

        foreach (var scene in new[] { "River", "Lake", "Gorge", "Estuary" })
        {
            Progress("Capturing " + scene);
            if (SceneManager.GetActiveScene().name != scene)
            {
                gs.Data.chapter = 5;
                gs.Data.location = scene.ToLowerInvariant();
                SceneManager.LoadScene(scene);
                yield return new WaitForSecondsRealtime(5);
            }
            Setup();
            SetHour(scene == "Estuary" ? 17.5f : 10.5f);
            FaceAway();
            yield return new WaitForSecondsRealtime(2);
            yield return Shot(scene.ToLowerInvariant() + "-shore");
            var modes = UnityEngine.Object.FindAnyObjectByType<PlayerModes>();
            modes.Board();
            yield return new WaitForSecondsRealtime(2);
            var boat = UnityEngine.Object.FindAnyObjectByType<CozyFishing.Boat.BoatController>();
            var fishing = boat.GetComponent<FishingController>();
            var spots = UnityEngine.Object.FindObjectsByType<FishingSpot>(FindObjectsSortMode.InstanceID);
            var spot = spots.FirstOrDefault(s => s.SpotName.ToLowerInvariant().Contains(scene == "Gorge" ? "pool" : scene == "Estuary" ? "lighthouse" : "shallow")) ?? spots.FirstOrDefault();
            if (spot != null)
            {
                var rb = boat.GetComponent<Rigidbody>();
                rb.position = spot.transform.position + new Vector3(0, -0.14f, -1.8f);
                rb.rotation = Quaternion.Euler(0, 180, 0);
                rb.linearVelocity = Vector3.zero;
                UnityEngine.Object.FindAnyObjectByType<CameraRig>()?.Snap();
            }
            yield return new WaitForSecondsRealtime(4);
            yield return Shot(scene.ToLowerInvariant() + "-boat");
            yield return Clip(scene.ToLowerInvariant() + "-row", 70, new Vector2(0, -0.18f));
            if (scene == "River") yield return Fish(fishing);
            if (scene == "Lake")
            {
                SetHour(21.5f);
                yield return new WaitForSecondsRealtime(4);
                yield return Shot("lake-night");
                yield return Clip("lake-night", 50, Vector2.zero);
            }
        }

        Progress("Capturing collection, map and equipment");
        foreach (var species in GameState.Db.fish.Where(f => f != null && !f.IsLegend).Take(9))
            if (gs.Entry(species.Id) == null) gs.Data.journal.Add(new JournalEntry { fishId = species.Id, count = 1, bestCm = species.SizeRangeCm.x });
        gs.NotifyChanged();
        PauseMenu.Open(PauseMenu.Tab.Journal);
        yield return new WaitForSecondsRealtime(1.5f);
        yield return Shot("journal");
        PauseMenu.Open(PauseMenu.Tab.Map);
        yield return new WaitForSecondsRealtime(1);
        yield return Shot("map");
        PauseMenu.Open(PauseMenu.Tab.Bag);
        yield return new WaitForSecondsRealtime(1);
        yield return Shot("equipment");
        PauseMenu.Close();
        Progress("COMPLETE");
        Time.captureFramerate = 0;
        Loc.EndPreview();
    }

    static void Setup()
    {
        CutscenePlayer.SkipAll();
        if (GameInput.Instance != null) typeof(GameInput).GetProperty("UsingTouch").SetValue(GameInput.Instance, true);
        foreach (var f in UnityEngine.Object.FindObjectsByType<FpsCounter>(FindObjectsSortMode.None)) f.gameObject.SetActive(false);
    }

    static void FaceAway()
    {
        var p = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (p != null) p.transform.rotation = Quaternion.identity;
    }

    static void SetHour(float hour) => UnityEngine.Object.FindAnyObjectByType<TimeOfDay>()?.SetHour(hour);

    static IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Root, name + ".png"));
        yield return new WaitForSecondsRealtime(0.25f);
    }

    static IEnumerator Clip(string name, int frames, Vector2 movement)
    {
        var dir = Path.Combine(Root, "frames-" + name);
        Directory.CreateDirectory(dir);
        Time.captureFramerate = 20;
        GameInput.Instance.SetTouchMove(movement);
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, "f" + i.ToString("0000") + ".png"));
        }
        GameInput.Instance.SetTouchMove(Vector2.zero);
        Time.captureFramerate = 0;
        yield return new WaitForSecondsRealtime(0.5f);
    }

    static IEnumerator Fish(FishingController fishing)
    {
        var input = GameInput.Instance;
        input.SetTouchAction(true);
        yield return new WaitForSeconds(0.6f);
        input.SetTouchAction(false);
        float timeout = Time.realtimeSinceStartup + 40;
        while (fishing.State != FishingState.Bite && Time.realtimeSinceStartup < timeout) yield return null;
        if (fishing.State != FishingState.Bite) yield break;
        input.SetTouchAction(true);
        yield return null;
        input.SetTouchAction(false);
        var dir = Path.Combine(Root, "frames-river-reel");
        Directory.CreateDirectory(dir);
        int n = 0;
        Time.captureFramerate = 20;
        while (fishing.State == FishingState.Reeling && n < 320)
        {
            float mid = (fishing.SafeBand.x + fishing.SafeBand.y) * 0.5f;
            input.SetTouchAction(!fishing.FishRunning && fishing.Tension < mid);
            yield return new WaitForEndOfFrame();
            if (n < 110) ScreenCapture.CaptureScreenshot(Path.Combine(dir, "f" + n.ToString("0000") + ".png"));
            if (n == 22) ScreenCapture.CaptureScreenshot(Path.Combine(Root, "river-reel.png"));
            n++;
        }
        input.SetTouchAction(false);
        Time.captureFramerate = 0;
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Shot("river-catch");
        if (fishing.State == FishingState.Landing) fishing.ConfirmCatch();
        yield return new WaitForSecondsRealtime(1);
    }

    public static string Restore()
    {
        if (File.Exists(Backup)) File.Copy(Backup, GameState.SavePath, true);
        string p = Path.Combine(Root, "previous-graphics.txt");
        if (File.Exists(p))
        {
            int q = int.Parse(File.ReadAllText(p));
            if (q < 0) PlayerPrefs.DeleteKey("gfx_quality"); else PlayerPrefs.SetInt("gfx_quality", q);
            PlayerPrefs.Save();
        }
        Time.captureFramerate = 0;
        return "Original save and graphics preferences restored.";
    }

    public static string RefreshJournal()
    {
        GameState.Instance.StartCoroutine(JournalRoutine());
        return "Capturing the Riverlight River journal with discovered species.";
    }

    static IEnumerator JournalRoutine()
    {
        var gs = GameState.Instance;
        Loc.Preview(Language.English);
        gs.NewGame("FisherM", "Kai");
        gs.Data.seenCutscenes.AddRange(GameState.Db.cutscenes.Where(c => c != null).Select(c => c.id));
        gs.Data.chapter = 1;
        gs.Data.location = "river";
        gs.Data.coins = 1180;
        foreach (var fish in GameState.Db.fish.Where(s => s != null && s.Location == "river" && !s.IsLegend).Take(9))
            gs.Data.journal.Add(new JournalEntry { fishId = fish.Id, count = 1, bestCm = (fish.SizeRangeCm.x + fish.SizeRangeCm.y) * .5f });
        SceneManager.LoadScene("River");
        yield return new WaitForSecondsRealtime(4);
        Setup();
        PauseMenu.Open(PauseMenu.Tab.Journal);
        yield return new WaitForSecondsRealtime(1);
        yield return Shot("journal");
        Progress("COMPLETE — River journal refreshed");
    }
}
