using System.Collections.Generic;
using CozyFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>Queued banner messages near the top-centre of the screen (objectives, unlocks, warnings).</summary>
    public sealed class Toast : MonoBehaviour
    {
        static Toast instance;
        readonly Queue<(string text, float seconds)> queue = new();
        CanvasGroup group;
        Text label;
        RectTransform panel;
        float timer;

        static Toast Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("Toast");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Toast>();
                instance.Build();
                return instance;
            }
        }

        public static void Show(string text, float seconds = 3f)
        {
            if (string.IsNullOrEmpty(text)) return;
            Instance.queue.Enqueue((text, seconds));
        }

        void Build()
        {
            var canvas = UIKit.Canvas(transform, "Toast Canvas", 60);
            var img = UIKit.Panel(canvas.transform, "Panel", new Color(0.13f, 0.11f, 0.1f, 0.88f));
            panel = img.rectTransform;
            UIKit.Place(panel, new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(960f, 120f));
            group = img.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            label = UIKit.Label(panel, "Text", "", 34, new Color(1f, 0.96f, 0.88f), TextAnchor.MiddleCenter);
            UIKit.Fill(label, 24f);
            var fit = img.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (timer > 0f)
            {
                timer -= dt;
                group.alpha = Mathf.MoveTowards(group.alpha, timer > 0.3f ? 1f : 0f, dt * 5f);
                panel.anchoredPosition = new Vector2(0f, Mathf.Lerp(-200f, -250f, group.alpha));
            }
            else if (queue.Count > 0 && !Story.CutscenePlayer.Playing)
            {
                var (text, seconds) = queue.Dequeue();
                label.text = text;
                float lines = Mathf.Ceil(text.Length / 46f);
                panel.sizeDelta = new Vector2(960f, Mathf.Max(110f, 60f + lines * 42f));
                timer = seconds + 0.3f;
                Audio.SfxPlayer.Play("toast", 0.5f, 0f);
            }
            else group.alpha = Mathf.MoveTowards(group.alpha, 0f, dt * 5f);
        }
    }
}
