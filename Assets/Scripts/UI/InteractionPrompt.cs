using CozyFishing.Core;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>
    /// Floating "[E] Board" style bubble that follows the thing you can interact with (boat, door, bed, fishing spot).
    /// Reads the current context action published to <see cref="TouchControls"/>.
    /// </summary>
    public sealed class InteractionPrompt : MonoBehaviour
    {
        [SerializeField] Font font;

        RectTransform bubble, keyCap;
        Text label, keyText;
        CanvasGroup group;
        Canvas canvas;
        Camera cam;
        float pop;
        string lastText;

        void Awake()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cgo = new GameObject("Prompt Canvas", typeof(Canvas), typeof(CanvasScaler));
            cgo.transform.SetParent(transform, false);
            canvas = cgo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;
            var scaler = cgo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            bubble = new GameObject("Bubble", typeof(RectTransform), typeof(Image), typeof(CanvasGroup), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            bubble.SetParent(cgo.transform, false);
            bubble.pivot = new Vector2(0.5f, 0f);
            var img = bubble.GetComponent<Image>();
            img.sprite = Rounded(64, 30);
            img.type = Image.Type.Sliced;
            img.color = new Color(0.99f, 0.96f, 0.88f, 0.97f);
            var layout = bubble.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 28, 12, 12);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fit = bubble.GetComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            group = bubble.GetComponent<CanvasGroup>();
            group.alpha = 0f;

            keyCap = new GameObject("Key", typeof(RectTransform), typeof(Image), typeof(LayoutElement)).GetComponent<RectTransform>();
            keyCap.SetParent(bubble, false);
            var kimg = keyCap.GetComponent<Image>();
            kimg.sprite = Rounded(64, 18);
            kimg.type = Image.Type.Sliced;
            kimg.color = new Color(0.25f, 0.2f, 0.16f);
            var le = keyCap.GetComponent<LayoutElement>();
            le.preferredWidth = 58; le.preferredHeight = 58;
            keyText = NewText(keyCap, 34, new Color(1f, 0.85f, 0.45f));
            var kt = keyText.rectTransform; kt.anchorMin = Vector2.zero; kt.anchorMax = Vector2.one; kt.offsetMin = kt.offsetMax = Vector2.zero;

            label = NewText(bubble, 38, new Color(0.22f, 0.17f, 0.13f));
        }

        Text NewText(Transform parent, int size, Color color)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            bool show = TouchControls.ActionVisible && !string.IsNullOrEmpty(TouchControls.ActionText) && TouchControls.HasAnchor && cam != null && !ScreenFader.Busy;
            if (show && TouchControls.ActionText != lastText) { pop = 0f; lastText = TouchControls.ActionText; }
            pop = Mathf.MoveTowards(pop, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            group.alpha = pop;
            if (pop <= 0f) { lastText = null; return; }

            Vector3 sp = cam.WorldToScreenPoint(TouchControls.ActionAnchor);
            if (sp.z < 0f) { group.alpha = 0f; return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, sp, null, out var local);
            float bob = Mathf.Sin(Time.unscaledTime * 3f) * 6f;
            bubble.localPosition = local + new Vector2(0f, 30f + bob);
            float s = EaseOutBack(pop);
            bubble.localScale = new Vector3(s, s, 1f);

            var input = GameInput.Instance;
            bool touch = input != null && input.UsingTouch;
            keyCap.gameObject.SetActive(!touch);
            keyText.text = input != null && input.UsingGamepad ? "A" : "E";
            label.text = TouchControls.ActionText;
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        static Sprite Rounded(int size, int radius)
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
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
