using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>Shared look &amp; builders for every code-built screen (portrait 1080x1920 reference).</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new(0.2f, 0.16f, 0.13f);
        public static readonly Color InkSoft = new(0.45f, 0.38f, 0.32f);
        public static readonly Color Paper = new(0.98f, 0.95f, 0.88f, 0.98f);
        public static readonly Color PaperDark = new(0.92f, 0.86f, 0.75f, 1f);
        public static readonly Color Accent = new(0.85f, 0.42f, 0.29f);
        public static readonly Color Teal = new(0.25f, 0.56f, 0.53f);
        public static readonly Color Gold = new(0.93f, 0.74f, 0.32f);
        public static readonly Color Dim = new(0.06f, 0.05f, 0.05f, 0.6f);

        public enum Style { Primary, Secondary, Ghost }
        public const Style Ghost = Style.Ghost;

        static Font font;
        public static Font Font => font != null ? font : font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Sprite rounded, roundedSmall, circle, vignette;
        public static Sprite Rounded => rounded != null ? rounded : rounded = MakeRounded(96, 34);
        public static Sprite RoundedSmall => roundedSmall != null ? roundedSmall : roundedSmall = MakeRounded(64, 18);
        public static Sprite Circle => circle != null ? circle : circle = MakeRounded(128, 64);

        public static Canvas Canvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            var s = go.GetComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1080, 1920);
            // Match the dimension that is relatively shorter so the 1080x1920 layout always fits:
            // tall phones (20:9) use the full width, wide screens (tablets) the full height.
            s.matchWidthOrHeight = Screen.height * 1080f >= Screen.width * 1920f ? 0f : 1f;
            return c;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Sprite sprite = null, bool sliced = false)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.rectTransform.SetParent(parent, false);
            img.color = color;
            img.sprite = sprite;
            if (sliced) img.type = UnityEngine.UI.Image.Type.Sliced;
            img.raycastTarget = false;
            return img;
        }

        public static Image Panel(Transform parent, string name, Color color) => Image(parent, name, color, Rounded, true);

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.rectTransform.SetParent(parent, false);
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static void Fill(Graphic g, float inset = 0f) => Anchor(g.rectTransform, Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));

        public static void Anchor(RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        public static Button Button(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, UnityAction onClick, Style style = Style.Primary)
        {
            Color bg = style switch { Style.Primary => Teal, Style.Secondary => Paper, _ => new Color(0f, 0f, 0f, 0.35f) };
            Color fg = style == Style.Secondary ? Ink : Color.white;
            var img = Image(parent, "Btn " + label, bg, Rounded, true);
            img.raycastTarget = true;
            Place(img.rectTransform, anchor, pos, size);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = new Color(0.7f, 0.7f, 0.7f, 0.6f);
            b.colors = colors;
            if (onClick != null) b.onClick.AddListener(onClick);
            b.onClick.AddListener(Audio.SfxPlayer.Click);
            var t = Label(img.rectTransform, "Label", label, 40, fg, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            // Long labels (e.g. Indonesian) shrink to stay on one line inside the button.
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 18;
            t.resizeTextMaxSize = 40;
            Fill(t, 8f);
            return b;
        }

        public static void SetLabel(Button b, string text) => b.GetComponentInChildren<Text>().text = text;

        /// <summary>Vertical scroll list; returns the content rect (add children, it auto-sizes).</summary>
        public static RectTransform ScrollList(Transform parent, string name, float spacing, out ScrollRect scroll, int columns = 1, Vector2 cell = default)
        {
            var viewport = Image(parent, name, new Color(1, 1, 1, 0.01f));
            viewport.raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;
            var content = Rect(viewport.rectTransform, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            if (columns > 1)
            {
                var grid = content.gameObject.AddComponent<GridLayoutGroup>();
                grid.cellSize = cell;
                grid.spacing = new Vector2(spacing, spacing);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = columns;
                grid.childAlignment = TextAnchor.UpperCenter;
                grid.padding = new RectOffset(0, 0, 10, 40);
            }
            else
            {
                var v = content.gameObject.AddComponent<VerticalLayoutGroup>();
                v.spacing = spacing;
                v.childControlHeight = false; v.childControlWidth = true;
                v.childForceExpandHeight = false; v.childForceExpandWidth = true;
                v.padding = new RectOffset(0, 0, 10, 40);
            }
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport.rectTransform;
            scroll.content = content;
            return content;
        }

        public static string Coins(int n) => $"<color=#D9A23A>●</color> {n}";

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();
            float border = Mathf.Min(radius, size / 2f - 1);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        }

        public static Sprite VignetteSprite()
        {
            if (vignette != null) return vignette;
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y) / (s - 1), new Vector2(0.5f, 0.5f)) * 1.41f;
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0.35f, 1f, d)));
            }
            tex.Apply();
            return vignette = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Fades a CanvasGroup toward visible/hidden.</summary>
        public static void Fade(CanvasGroup g, bool visible, float speed = 6f)
        {
            g.alpha = Mathf.MoveTowards(g.alpha, visible ? 1f : 0f, Time.unscaledDeltaTime * speed);
            g.blocksRaycasts = g.interactable = visible;
        }
    }
}
