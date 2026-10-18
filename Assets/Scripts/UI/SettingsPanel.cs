using System;
using CozyFishing.Core;
using CozyFishing.Monetization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CozyFishing.UI
{
    /// <summary>
    /// Settings as white cards (language, sound, graphics, extras); every option is a visible pill button and every
    /// element is anchored to the page width, so it fits any phone. In-game it also offers Save &amp; Quit to the title.
    /// </summary>
    public static class SettingsPanel
    {
        const float Gap = 22f, Pad = 28f;

        public static void Build(RectTransform page, bool inGame)
        {
            var scrollContent = UIKit.ScrollList(page, "SettingsScroll", Gap, out var scroll);
            UIKit.Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, new Vector2(0, inGame ? 280 : 120), Vector2.zero);

            // Language
            var lang = Card(scrollContent, 200f);
            Heading(lang, Loc.T("language"));
            Segment(lang, new[] { "English", "Bahasa Indonesia" }, Loc.Current == Language.English ? 0 : 1, i =>
            {
                Loc.Current = i == 0 ? Language.English : Language.Indonesian;
                Rebuild(page, inGame);
            });

            // Sound
            var sound = Card(scrollContent, 330f);
            Heading(sound, Loc.T("sound_heading"));
            Slider(sound, 0, Loc.T("music"), Settings.MusicVolume, v => Settings.MusicVolume = v);
            Slider(sound, 1, Loc.T("sfx"), Settings.SfxVolume, v => { Settings.SfxVolume = v; Audio.MusicPlayer.ApplyVolume(); });
            Slider(sound, 2, Loc.T("voice"), Settings.VoiceVolume, v => Settings.VoiceVolume = v);

            // Graphics
            var gfx = Card(scrollContent, 250f);
            Heading(gfx, Loc.T("graphics"));
            var levels = new[] { GfxLevel.Low, GfxLevel.Medium, GfxLevel.High };
            Segment(gfx, new[] { Loc.T("gfx_low"), Loc.T("gfx_medium"), Loc.T("gfx_high") }, Array.IndexOf(levels, GraphicsQuality.Level), i =>
            {
                GraphicsQuality.Level = levels[i];
                Rebuild(page, inGame);
            });
            var hint = UIKit.Label(gfx, "Hint", Loc.T("gfx_hint"), 26, UIKit.InkSoft, TextAnchor.MiddleLeft);
            UIKit.Anchor(hint.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(Pad, 14), new Vector2(-Pad, 60));

            // Extras: vibration and the frame-rate meter
            var extras = Card(scrollContent, 230f);
            Toggle(extras, 0, Loc.T("vibration"), Settings.Vibration, v => { Settings.Vibration = v; Rebuild(page, inGame); });
            Toggle(extras, 1, Loc.T("show_fps"), FpsCounter.Visible, v => { FpsCounter.Visible = v; Rebuild(page, inGame); });

            var privacy = Card(scrollContent, 150f);
            UIKit.Button(privacy, Loc.T("ad_privacy"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700, 100),
                () => AdManager.Instance.ShowPrivacyOptions(), UIKit.Style.Secondary);
            privacy.gameObject.AddComponent<AdPrivacyEntry>().Bind();

            var removeAds = Card(scrollContent, 220f);
            var removeAdsButton = UIKit.Button(removeAds,
                RemoveAdsPurchase.IsOwned ? Loc.T("remove_ads_owned") : Loc.T("remove_ads_price"),
                new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(700, 100),
                () => RemoveAdsPurchase.Instance.BuyRemoveAds(),
                RemoveAdsPurchase.IsOwned ? UIKit.Style.Secondary : UIKit.Style.Primary);
            removeAdsButton.GetComponentInChildren<Text>().fontSize = 30;
            var removeAdsHint = UIKit.Label(removeAds, "RemoveAdsHint", Loc.T("remove_ads_hint"), 23, UIKit.InkSoft, TextAnchor.MiddleCenter);
            UIKit.Anchor(removeAdsHint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(Pad, -196), new Vector2(-Pad, -132));

            if (inGame)
            {
                var quit = UIKit.Button(page, Loc.T("save_quit"), new Vector2(0.5f, 0), new Vector2(0, 110), new Vector2(560, 104), () =>
                {
                    GameState.Instance.Save();
                    Time.timeScale = 1f;
                    ScreenFader.Transition(() => SceneManager.LoadScene("Title"));
                }, UIKit.Style.Primary);
                quit.GetComponentInChildren<Text>().fontSize = 36;
            }
            var credit = UIKit.Label(page, "Credit", Loc.T("credits_line"), 22, UIKit.InkSoft, TextAnchor.MiddleCenter);
            UIKit.Anchor(credit.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(10, inGame ? 30 : 20), new Vector2(-10, inGame ? 90 : 100));
        }

        static void Rebuild(RectTransform page, bool inGame)
        {
            foreach (Transform c in page) Object.Destroy(c.gameObject);
            Build(page, inGame);
            Object.FindAnyObjectByType<TitleScreen>()?.RefreshTexts();
        }

        /// <summary>A white rounded card spanning the page width.</summary>
        static RectTransform Card(RectTransform content, float height)
        {
            var card = UIKit.Panel(content, "Card", Color.white);
            card.rectTransform.sizeDelta = new Vector2(0, height);
            card.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return card.rectTransform;
        }

        static void Heading(RectTransform card, string text)
        {
            var t = UIKit.Label(card, "Heading", $"<b>{text}</b>", 32, UIKit.Teal, TextAnchor.MiddleLeft);
            UIKit.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(Pad, -76), new Vector2(-Pad, -16));
        }

        /// <summary>A row of equal pill buttons across the card; the selected one is filled teal.</summary>
        static void Segment(RectTransform card, string[] labels, int selected, Action<int> pick)
        {
            int n = labels.Length;
            for (int i = 0; i < n; i++)
            {
                int index = i;
                var b = UIKit.Button(card, labels[i], new Vector2(0, 1), Vector2.zero, Vector2.zero, () => pick(index),
                    i == selected ? UIKit.Style.Primary : UIKit.Style.Secondary);
                var rt = (RectTransform)b.transform;
                rt.pivot = new Vector2(0.5f, 1f);
                UIKit.Anchor(rt, new Vector2((float)i / n, 1), new Vector2((float)(i + 1) / n, 1),
                    new Vector2(i == 0 ? Pad : 8, -176), new Vector2(i == n - 1 ? -Pad : -8, -90));
                var label = b.GetComponentInChildren<Text>();
                label.fontSize = 30;
                label.resizeTextForBestFit = true; label.resizeTextMinSize = 20; label.resizeTextMaxSize = 30;
            }
        }

        /// <summary>Label on the left, On/Off pill on the right.</summary>
        static void Toggle(RectTransform card, int row, string label, bool value, Action<bool> set)
        {
            float top = -24f - row * 100f;
            var t = UIKit.Label(card, "Label", $"<b>{label}</b>", 32, UIKit.Teal, TextAnchor.MiddleLeft);
            UIKit.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(0.6f, 1), new Vector2(Pad, top - 80), new Vector2(0, top));
            var b = UIKit.Button(card, value ? Loc.T("on") : Loc.T("off"), new Vector2(1, 1), new Vector2(-Pad, top - 4), new Vector2(200, 76),
                () => set(!value), value ? UIKit.Style.Primary : UIKit.Style.Secondary);
            b.GetComponentInChildren<Text>().fontSize = 30;
        }

        static void Slider(RectTransform card, int row, string label, float value, UnityEngine.Events.UnityAction<float> onChange)
        {
            float top = -92f - row * 76f;
            var t = UIKit.Label(card, "L", label, 30, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(0.32f, 1), new Vector2(Pad, top - 60), new Vector2(0, top));
            var bg = UIKit.Panel(card, "Track", UIKit.PaperDark);
            UIKit.Anchor(bg.rectTransform, new Vector2(0.34f, 1), new Vector2(1, 1), new Vector2(0, top - 45), new Vector2(-Pad - 20, top - 15));
            var fill = UIKit.Panel(bg.rectTransform, "Fill", UIKit.Teal);
            var handle = UIKit.Image(bg.rectTransform, "Handle", UIKit.Accent, UIKit.Circle);
            handle.rectTransform.sizeDelta = new Vector2(56, 56);
            var s = bg.gameObject.AddComponent<UnityEngine.UI.Slider>();
            UIKit.Anchor(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0f; s.maxValue = 1f;
            s.value = value;
            s.onValueChanged.AddListener(onChange);
            bg.raycastTarget = true;
        }
    }
}
