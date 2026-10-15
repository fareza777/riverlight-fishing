using CozyFishing.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>
    /// Frame-rate meter. Shown in the corner when "Show FPS" is on (Settings); always writes a short
    /// "[Perf]" line to the log every 5 s so performance can be read from a connected phone (adb logcat).
    /// </summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        const string PrefKey = "show_fps";
        static FpsCounter instance;

        Text label;
        GameObject panel;
        int frames;
        float elapsed, worst, logTimer;
        int logFrames;

        public static bool Visible
        {
            get => PlayerPrefs.GetInt(PrefKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                if (instance != null) instance.panel.SetActive(value);
            }
        }

        /// <summary>Measured average frames per second over the last half second.</summary>
        public static float Current { get; private set; }

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("FpsCounter");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<FpsCounter>();
            instance.Build();
        }

        void Build()
        {
            var canvas = UIKit.Canvas(transform, "Fps Canvas", 200);
            var img = UIKit.Panel(canvas.transform, "Fps", new Color(0f, 0f, 0f, 0.45f));
            UIKit.Place(img.rectTransform, new Vector2(0f, 0f), new Vector2(110f, 30f), new Vector2(200f, 50f));
            img.raycastTarget = false;
            label = UIKit.Label(img.rectTransform, "Text", "", 26, Color.white, TextAnchor.MiddleCenter);
            UIKit.Fill(label);
            label.raycastTarget = false;
            panel = img.gameObject;
            panel.SetActive(Visible);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            frames++;
            elapsed += dt;
            worst = Mathf.Max(worst, dt);
            if (elapsed >= 0.5f)
            {
                Current = frames / elapsed;
                if (panel.activeSelf) label.text = $"{Current:0} FPS · {GraphicsQuality.Level}";
                frames = 0;
                elapsed = 0f;
            }
            logFrames++;
            logTimer += dt;
            if (logTimer >= 5f)
            {
                Debug.Log($"[Perf] fps={logFrames / logTimer:0.0} worstMs={worst * 1000f:0} level={GraphicsQuality.Level} scene={SceneManager.GetActiveScene().name}");
                logFrames = 0;
                logTimer = 0f;
                worst = 0f;
            }
        }
    }
}
