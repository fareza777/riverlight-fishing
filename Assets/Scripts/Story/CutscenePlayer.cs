using System;
using System.Collections;
using CozyFishing.Core;
using CozyFishing.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.Story
{
    /// <summary>
    /// Plays a <see cref="CutsceneDef"/> full screen: slow Ken Burns pan over each illustration, letterbox, subtitles,
    /// narration, cross-fades. Tap advances a slide; "Skip" ends it. Gameplay is paused (timeScale 0) meanwhile.
    /// </summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        public static bool Playing { get; private set; }

        RawImage imageA, imageB;
        Text subtitle, title;
        CanvasGroup group, titleGroup, subtitleGroup;
        AudioSource voice, music;
        bool advance, skip;

        /// <summary>Skips whatever cutscene is playing (dev tools).</summary>
        public static void SkipAll()
        {
            foreach (var p in FindObjectsByType<CutscenePlayer>(FindObjectsSortMode.None)) p.skip = true;
        }

        public static void Play(CutsceneDef def, Action onDone = null)
        {
            if (def == null || def.slides.Count == 0) { onDone?.Invoke(); return; }
            var go = new GameObject("Cutscene " + def.id);
            DontDestroyOnLoad(go);
            go.AddComponent<CutscenePlayer>().StartCoroutine(go.GetComponent<CutscenePlayer>().Run(def, onDone));
        }

        void Build(bool memory)
        {
            var canvas = UIKit.Canvas(transform, "Cutscene Canvas", 900);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            var root = (RectTransform)canvas.transform;
            UIKit.Fill(UIKit.Image(root, "Black", new Color(0.04f, 0.035f, 0.03f)));

            imageA = Raw(root, memory); imageB = Raw(root, memory);
            if (memory)
            {
                var vignette = UIKit.Image(root, "Vignette", Color.white);
                vignette.sprite = UIKit.VignetteSprite();
                vignette.color = new Color(0.25f, 0.15f, 0.08f, 0.85f);
                UIKit.Fill(vignette);
            }
            // Letterbox
            var top = UIKit.Image(root, "BarTop", Color.black); UIKit.Anchor(top.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -150), Vector2.zero);
            var bottom = UIKit.Image(root, "BarBottom", Color.black); UIKit.Anchor(bottom.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 420));

            subtitle = UIKit.Label(root, "Subtitle", "", 40, new Color(0.98f, 0.95f, 0.88f), TextAnchor.MiddleCenter);
            UIKit.Anchor(subtitle.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(60, 60), new Vector2(-60, 400));
            subtitle.fontStyle = FontStyle.Italic;
            subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();

            title = UIKit.Label(root, "Title", "", 54, new Color(0.98f, 0.9f, 0.7f), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UIKit.Anchor(title.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(40, -60), new Vector2(-40, 60));
            titleGroup = title.gameObject.AddComponent<CanvasGroup>();

            var skipBtn = UIKit.Button(root, Loc.T("skip") + "  ›", new Vector2(1, 1), new Vector2(-40, -40), new Vector2(220, 80), () => skip = true, UIKit.Ghost);
            skipBtn.GetComponentInChildren<Text>().fontSize = 30;

            var tap = root.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => advance = true);
            root.GetComponentInChildren<Image>().raycastTarget = true;

            voice = gameObject.AddComponent<AudioSource>();
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.volume = 0f;
        }

        RawImage Raw(RectTransform root, bool memory)
        {
            var go = new GameObject("Slide", typeof(RectTransform), typeof(RawImage));
            var r = go.GetComponent<RawImage>();
            r.rectTransform.SetParent(root, false);
            r.color = memory ? new Color(1f, 0.9f, 0.76f, 0f) : new Color(1, 1, 1, 0);
            r.raycastTarget = false;
            return r;
        }

        IEnumerator Run(CutsceneDef def, Action onDone)
        {
            Playing = true;
            float oldScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = false;
            Build(def.memory);
            group.alpha = 0f;
            if (def.music != null) { music.clip = def.music; music.ignoreListenerPause = true; music.Play(); }

            for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.6f) { group.alpha = t; music.volume = t * 0.55f * Settings.MusicVolume; yield return null; }
            group.alpha = 1f;

            if (!string.IsNullOrEmpty(def.Title))
            {
                title.text = def.Title;
                for (float t = 0; t < 1f; t += Time.unscaledDeltaTime / 0.8f) { titleGroup.alpha = t; yield return null; }
                for (float t = 0; t < 1.6f && !skip && !Consume(); t += Time.unscaledDeltaTime) yield return null;
                for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 0.6f) { titleGroup.alpha = t; yield return null; }
                titleGroup.alpha = 0f;
            }

            bool useA = true;
            foreach (var slide in def.slides)
            {
                if (skip) break;
                var cur = useA ? imageA : imageB;
                var prev = useA ? imageB : imageA;
                useA = !useA;
                cur.texture = slide.image != null ? slide.image.texture : null;
                cur.transform.SetAsLastSibling();
                cur.transform.SetSiblingIndex(prev.transform.GetSiblingIndex() + 1);
                FitCover(cur);
                subtitle.text = slide.Text;
                subtitleGroup.alpha = 0f;
                if (slide.Voice != null) { voice.clip = slide.Voice; voice.volume = Settings.VoiceVolume; voice.ignoreListenerPause = true; voice.Play(); }
                float dur = Mathf.Max(slide.seconds, slide.Voice != null ? slide.Voice.length + 0.8f : 0f);
                advance = false;
                Color baseCol = cur.color;
                for (float t = 0; t < dur && !skip; t += Time.unscaledDeltaTime)
                {
                    float k = t / dur;
                    float fadeIn = Mathf.Clamp01(t / 0.9f);
                    cur.color = new Color(baseCol.r, baseCol.g, baseCol.b, fadeIn);
                    prev.color = new Color(prev.color.r, prev.color.g, prev.color.b, Mathf.Min(prev.color.a, 1f - fadeIn));
                    subtitleGroup.alpha = Mathf.Clamp01((t - 0.3f) / 0.6f);
                    float zoom = Mathf.Lerp(slide.zoomFrom, slide.zoomTo, Ease(k));
                    Vector2 pan = Vector2.Lerp(slide.panFrom, slide.panTo, Ease(k));
                    cur.rectTransform.localScale = Vector3.one * zoom;
                    cur.rectTransform.anchoredPosition = new Vector2(pan.x * 1080f, pan.y * 1920f);
                    if (t > 0.8f && Consume()) break;
                    yield return null;
                }
                voice.Stop();
            }

            for (float t = 1; t > 0f; t -= Time.unscaledDeltaTime / 0.8f) { group.alpha = t; music.volume = t * 0.55f * Settings.MusicVolume; yield return null; }
            Time.timeScale = oldScale <= 0f ? 1f : oldScale;
            Playing = false;
            GameState.Instance.MarkCutsceneSeen(def.id);
            Destroy(gameObject);
            onDone?.Invoke();
        }

        bool Consume()
        {
            bool a = advance || (GameInput.Instance != null && GameInput.Instance.ActionPressed);
            advance = false;
            return a;
        }

        static float Ease(float x) => x * x * (3f - 2f * x);

        static void FitCover(RawImage img)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float screenAspect = 1080f / 1920f;
            float texAspect = img.texture != null ? (float)img.texture.width / img.texture.height : 1f;
            Vector2 size = texAspect > screenAspect ? new Vector2(1920f * texAspect, 1920f) : new Vector2(1080f, 1080f / texAspect);
            rt.sizeDelta = size;
        }
    }
}
