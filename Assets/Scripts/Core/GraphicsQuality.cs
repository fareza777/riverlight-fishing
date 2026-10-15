using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CozyFishing.Core
{
    public enum GfxLevel { Low, Medium, High }

    /// <summary>
    /// Graphics presets so older phones play smoothly. The first launch picks a level from the GPU and RAM;
    /// the player can change it in Settings (saved in PlayerPrefs). "-gfx low|medium|high" overrides it for tests.
    ///   Low:    60% resolution, 30 fps, short hard shadows, no post-processing/AA, simple terrain/water shading, sparse grass, half-res textures
    ///   Medium: 75% resolution, 60 fps, medium shadows, bloom + FXAA, medium grass
    ///   High:   full look (HDR, soft long shadows, bloom, depth of field, SMAA, dense grass)
    /// </summary>
    public static class GraphicsQuality
    {
        const string PrefKey = "gfx_quality";
        static GfxLevel? level;

        public static event Action Changed;

        public static GfxLevel Level
        {
            get
            {
                level ??= Load();
                return level.Value;
            }
            set
            {
                level = value;
                PlayerPrefs.SetInt(PrefKey, (int)value);
                PlayerPrefs.Save();
                Apply();
                Changed?.Invoke();
            }
        }

        public static float GrassDensity => Level switch { GfxLevel.Low => 0.3f, GfxLevel.Medium => 0.6f, _ => 1f };
        public static float GrassDistance => Level switch { GfxLevel.Low => 24f, GfxLevel.Medium => 34f, _ => 48f };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Apply();
            SceneManager.sceneLoaded += (_, _) => ApplyScene();
            UI.FpsCounter.Ensure();
        }

        static GfxLevel Load()
        {
            string arg = DevArgs.Value("-gfx");
            if (arg != null && Enum.TryParse(arg, true, out GfxLevel forced)) return forced;
            if (PlayerPrefs.HasKey(PrefKey)) return (GfxLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey), 0, 2);
            var detected = Detect();
            PlayerPrefs.SetInt(PrefKey, (int)detected);
            return detected;
        }

        /// <summary>Best guess from the GPU family and memory. Unknown phones get Medium.</summary>
        public static GfxLevel Detect()
        {
            if (!Application.isMobilePlatform) return GfxLevel.High;
            string gpu = SystemInfo.graphicsDeviceName ?? "";
            int ram = SystemInfo.systemMemorySize;
            if (ram > 0 && ram < 4500) return GfxLevel.Low;
            var adreno = Regex.Match(gpu, @"Adreno[^0-9]*(\d{3})");
            if (adreno.Success)
            {
                int n = int.Parse(adreno.Groups[1].Value);
                return n < 640 ? GfxLevel.Low : n < 730 ? GfxLevel.Medium : GfxLevel.High;
            }
            if (Regex.IsMatch(gpu, @"Mali-(T|4|G3|G5)|PowerVR|IMG|Vivante")) return GfxLevel.Low;
            if (Regex.IsMatch(gpu, @"Immortalis|Mali-G7[1-9]\d|Apple")) return GfxLevel.High;
            return GfxLevel.Medium;
        }

        /// <summary>Pipeline-wide settings (render scale, shadows, HDR, frame rate, texture resolution).</summary>
        public static void Apply()
        {
            var l = Level;
            bool mobile = Application.isMobilePlatform;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = l == GfxLevel.Low ? 30 : 60;
            QualitySettings.globalTextureMipmapLimit = l == GfxLevel.Low ? 1 : 0;
            // Cheaper terrain/water noise (fewer octaves) on Low.
            Shader.SetKeyword(GlobalKeyword.Create("CF_GFX_LOW"), l == GfxLevel.Low);
            // The editor would save these into the pipeline asset, so only players change it.
            var urp = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            if (urp != null && !Application.isEditor)
            {
                urp.renderScale = l switch { GfxLevel.Low => 0.6f, GfxLevel.Medium => 0.75f, _ => mobile ? 0.9f : 1f };
                urp.supportsHDR = l == GfxLevel.High;
                urp.msaaSampleCount = 1;
                urp.shadowDistance = l switch { GfxLevel.Low => 22f, GfxLevel.Medium => 40f, _ => 70f };
                urp.shadowCascadeCount = l == GfxLevel.High ? 2 : 1;
                urp.mainLightShadowmapResolution = l == GfxLevel.Low ? 512 : l == GfxLevel.Medium ? 1024 : 2048;
                urp.maxAdditionalLightsCount = l == GfxLevel.Low ? 2 : 4;
                urp.supportsCameraOpaqueTexture = l != GfxLevel.Low;   // the Low water shader doesn't refract
            }
            ApplyScene();
        }

        /// <summary>Per-scene settings: camera anti-aliasing and the post-processing stack.</summary>
        static void ApplyScene()
        {
            var l = Level;
            foreach (var cam in Camera.allCameras)
            {
                if (!cam.TryGetComponent<UniversalAdditionalCameraData>(out var data)) continue;
                data.antialiasing = l switch { GfxLevel.Low => AntialiasingMode.None, GfxLevel.Medium => AntialiasingMode.FastApproximateAntialiasing, _ => AntialiasingMode.SubpixelMorphologicalAntiAliasing };
                data.antialiasingQuality = l == GfxLevel.High ? AntialiasingQuality.High : AntialiasingQuality.Low;
                data.renderPostProcessing = l != GfxLevel.Low;   // the full-screen post pass alone costs ~20 ms on weak GPUs
            }
            foreach (var v in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                var p = v.profile;   // instance copy: never edits the shared asset
                if (p == null) continue;
                if (p.TryGet<DepthOfField>(out var dof)) dof.active = l == GfxLevel.High;
                if (p.TryGet<Bloom>(out var bloom)) bloom.active = l != GfxLevel.Low;
            }
        }
    }

    /// <summary>Command-line arguments, including the Android intent extra "unity" (adb shell am start ... -e unity "-gfx low").</summary>
    public static class DevArgs
    {
        static string[] all;

        public static string[] All
        {
            get
            {
                if (all != null) return all;
                var list = new System.Collections.Generic.List<string>(Environment.GetCommandLineArgs());
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                    using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                    using var intent = activity.Call<AndroidJavaObject>("getIntent");
                    string extra = intent.Call<string>("getStringExtra", "unity");
                    if (!string.IsNullOrEmpty(extra)) list.AddRange(extra.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DevArgs] intent extras unavailable: {e.Message}");
                }
#endif
                return all = list.ToArray();
            }
        }

        public static bool Has(string name) => Array.IndexOf(All, name) >= 0;

        public static string Value(string name)
        {
            int i = Array.IndexOf(All, name);
            return i >= 0 && i + 1 < All.Length ? All[i + 1] : null;
        }
    }
}
