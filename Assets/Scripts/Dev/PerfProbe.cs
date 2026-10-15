using System.Collections;
using CozyFishing.Core;
using CozyFishing.Player;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Performance attribution on a real device, only with "-selftest-perf". In the River scene it measures the frame
    /// time, then switches parts of the game off one at a time (cumulative) and measures again, on foot and in the boat.
    /// The difference between steps shows what each part costs. Results go to the log as "[PerfProbe]".
    /// </summary>
    public sealed class PerfProbe : MonoBehaviour
    {
        ProfilerRecorder batches, setPass, triangles, mainThread;
        readonly FrameTiming[] timings = new FrameTiming[1];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!DevArgs.Has("-selftest-perf")) return;
            var go = new GameObject("PerfProbe");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfProbe>();
        }

        IEnumerator Start()
        {
            batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);

            GameState.Instance.Data.seenCutscenes.Add("intro");
            SceneManager.LoadScene("River");
            yield return Wait(5f);
            FindAnyObjectByType<World.TimeOfDay>()?.SetHour(10f);
            yield return Wait(2f);

            yield return Sequence("foot");
            SceneManager.LoadScene("River");
            yield return Wait(5f);
            FindAnyObjectByType<World.TimeOfDay>()?.SetHour(10f);
            FindAnyObjectByType<PlayerModes>().Board();
            yield return Wait(2f);
            var body = GameObject.Find("PlayerBoat").GetComponent<Rigidbody>();
            body.position = new Vector3(0.5f, -0.14f, 8f);
            body.linearVelocity = Vector3.zero;
            yield return Wait(2f);
            yield return Sequence("boat");
            Debug.Log("[PerfProbe] done");
            Application.Quit();
        }

        IEnumerator Sequence(string tag)
        {
            yield return Measure(tag, "baseline");
            foreach (var g in FindObjectsByType<World.GrassField>(FindObjectsSortMode.None)) g.enabled = false;
            yield return Measure(tag, "-grass");
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.shadows = LightShadows.None;
            yield return Measure(tag, "-shadows");
            foreach (var c in Camera.allCameras)
                if (c.TryGetComponent<UniversalAdditionalCameraData>(out var d)) { d.renderPostProcessing = false; d.antialiasing = AntialiasingMode.None; }
            yield return Measure(tag, "-post");
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("Water") && !r.sharedMaterial.shader.name.Contains("Mask")) r.enabled = false;
            yield return Measure(tag, "-water");
            var props = GameObject.Find("Props");
            if (props != null) props.SetActive(false);
            yield return Measure(tag, "-props");
            foreach (var r in FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) r.enabled = false;
            yield return Measure(tag, "-characters");
            var level = GameObject.Find("Level");
            if (level != null) level.SetActive(false);
            yield return Measure(tag, "-terrain");
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            yield return Measure(tag, "-ui");
            foreach (var s in FindObjectsByType<World.FishSchool>(FindObjectsSortMode.None)) s.gameObject.SetActive(false);
            yield return Measure(tag, "-fish");
        }

        IEnumerator Measure(string tag, string step)
        {
            yield return Wait(1.5f);   // let the change settle
            int frames = 0;
            float t = 0f;
            double cpu = 0, render = 0, gpu = 0;
            int samples = 0;
            while (t < 4f)
            {
                yield return null;
                t += Time.unscaledDeltaTime;
                frames++;
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    cpu += timings[0].cpuMainThreadFrameTime;
                    render += timings[0].cpuRenderThreadFrameTime;
                    gpu += timings[0].gpuFrameTime;
                    samples++;
                }
            }
            int n = Mathf.Max(1, samples);
            Debug.Log(System.FormattableString.Invariant(
                $"[PerfProbe] {tag} {step,-12} fps={frames / t:0.0} cpuMain={cpu / n:0.0}ms cpuRender={render / n:0.0}ms gpu={gpu / n:0.0}ms batches={batches.LastValue} setpass={setPass.LastValue} tris={triangles.LastValue} mainRec={mainThread.LastValue / 1e6:0.0}ms level={GraphicsQuality.Level}"));
        }

        static IEnumerator Wait(float s)
        {
            float t = 0f;
            while (t < s) { t += Time.unscaledDeltaTime; yield return null; }
        }

        void OnDestroy()
        {
            batches.Dispose(); setPass.Dispose(); triangles.Dispose(); mainThread.Dispose();
        }
    }
}
