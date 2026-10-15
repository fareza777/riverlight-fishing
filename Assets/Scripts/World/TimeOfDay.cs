using System.Collections.Generic;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>
    /// Morning -> noon -> evening -> night cycle. Drives the sun, ambient light, fog and the lanterns.
    /// </summary>
    public sealed class TimeOfDay : MonoBehaviour
    {
        [SerializeField] Light sun;
        [SerializeField] float dayLengthMinutes = 20f;
        [SerializeField, Range(0f, 24f)] float startHour = 7.5f;
        [SerializeField] bool running = true;

        [Header("Look")]
        [SerializeField] Gradient sunColor = DefaultSunColor();
        [SerializeField] AnimationCurve sunIntensity = new(new Keyframe(0f, 0.5f), new Keyframe(5.5f, 0.5f), new Keyframe(7f, 1.1f), new Keyframe(12f, 1.7f), new Keyframe(17.5f, 1.2f), new Keyframe(19.5f, 0.55f), new Keyframe(24f, 0.5f));
        [SerializeField] Gradient skyColor = DefaultSky();
        [SerializeField] Gradient fogColor = DefaultFog();
        [SerializeField] List<Light> lanterns = new();
        [SerializeField] float lanternMaxIntensity = 2.5f;

        public float Hour { get; private set; }

        void Start() => Hour = startHour;

        void Update()
        {
            if (running)
            {
                float next = Hour + Time.deltaTime * 24f / (dayLengthMinutes * 60f);
                if (next >= 24f && Core.GameState.Instance != null) Core.GameState.Instance.Data.day++;
                Hour = Mathf.Repeat(next, 24f);
            }
            Apply();
        }

        public void SetHour(float hour)
        {
            Hour = Mathf.Repeat(hour, 24f);
            Apply();
        }

        void Apply()
        {
            float t = Hour / 24f;
            if (sun)
            {
                // Sun arcs east -> west; at night it becomes a dim blue moon light from the same rig.
                float elevation = Mathf.Sin((Hour - 6f) / 12f * Mathf.PI) * 62f;
                bool night = elevation < 3f;
                float moonElevation = 40f;
                sun.transform.rotation = Quaternion.Euler(night ? moonElevation : Mathf.Max(elevation, 3f), Mathf.Lerp(-110f, 110f, Mathf.InverseLerp(6f, 18f, Hour)) + 20f, 0f);
                sun.color = sunColor.Evaluate(t);
                sun.intensity = sunIntensity.Evaluate(Hour);
            }

            Color sky = skyColor.Evaluate(t);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = Color.Lerp(sky, fogColor.Evaluate(t), 0.5f) * 0.85f;
            RenderSettings.ambientGroundColor = sky * 0.45f;
            RenderSettings.fogColor = fogColor.Evaluate(t);

            float dark = 1f - Mathf.Clamp01(sunIntensity.Evaluate(Hour) / 0.9f);
            foreach (var l in lanterns)
            {
                if (!l) continue;
                // Lanterns only light up from dusk: in daylight they were invisible but still cost a light per pixel.
                l.enabled = dark > 0.12f;
                l.intensity = Mathf.Lerp(0.25f, lanternMaxIntensity, dark);
            }
        }

        static Gradient Make(params (float t, string hex)[] keys)
        {
            var g = new Gradient();
            var c = new GradientColorKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ColorUtility.TryParseHtmlString(keys[i].hex, out var col);
                c[i] = new GradientColorKey(col, keys[i].t);
            }
            g.SetKeys(c, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }

        static Gradient DefaultSunColor() => Make((0f, "#9DB2EE"), (0.24f, "#FFB27A"), (0.32f, "#FFE6C4"), (0.5f, "#FFF6E8"), (0.72f, "#FFD6A0"), (0.8f, "#FF9A6A"), (0.86f, "#9DA8E0"), (1f, "#9DB2EE"));
        static Gradient DefaultSky() => Make((0f, "#56679C"), (0.23f, "#6A6F9A"), (0.3f, "#C9B9C9"), (0.5f, "#BFD6E6"), (0.72f, "#E6C7A6"), (0.8f, "#9C7A9C"), (0.88f, "#5A6698"), (1f, "#56679C"));
        static Gradient DefaultFog() => Make((0f, "#2C3A62"), (0.24f, "#9C94B5"), (0.32f, "#DCE3E6"), (0.5f, "#CFE3EC"), (0.72f, "#F0D2B0"), (0.8f, "#B98AA0"), (0.88f, "#34406A"), (1f, "#2C3A62"));
    }
}
