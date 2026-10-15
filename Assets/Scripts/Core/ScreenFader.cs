using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.Core
{
    /// <summary>Full-screen fade used for doors, sleeping and scene changes. Creates its own overlay canvas.</summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        static ScreenFader instance;
        CanvasGroup group;
        bool busy;

        public static bool Busy => instance != null && instance.busy;

        static ScreenFader Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("ScreenFader", typeof(Canvas), typeof(CanvasGroup));
                DontDestroyOnLoad(go);
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 1000;
                var img = new GameObject("Black", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(go.transform, false);
                img.color = new Color(0.06f, 0.05f, 0.07f);
                var rt = img.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                instance = go.AddComponent<ScreenFader>();
                instance.group = go.GetComponent<CanvasGroup>();
                instance.group.alpha = 0f;
                instance.group.blocksRaycasts = false;
                return instance;
            }
        }

        /// <summary>Fade to black, run <paramref name="whileBlack"/>, hold briefly, fade back in.</summary>
        public static void Transition(Action whileBlack, float fadeSeconds = 0.35f, float holdSeconds = 0.15f)
        {
            var f = Instance;
            if (f.busy) return;
            f.StartCoroutine(f.Run(whileBlack, fadeSeconds, holdSeconds));
        }

        IEnumerator Run(Action whileBlack, float fade, float hold)
        {
            busy = true;
            group.blocksRaycasts = true;
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fade) { group.alpha = Mathf.SmoothStep(0, 1, t); yield return null; }
            group.alpha = 1f;
            try { whileBlack?.Invoke(); }
            catch (Exception e) { Debug.LogException(e); }
            yield return new WaitForSecondsRealtime(hold);
            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / fade) { group.alpha = 1f - Mathf.SmoothStep(0, 1, t); yield return null; }
            group.alpha = 0f;
            group.blocksRaycasts = false;
            busy = false;
        }
    }
}
