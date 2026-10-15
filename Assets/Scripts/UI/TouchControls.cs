using CozyFishing.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>
    /// Portrait touch layout: a floating joystick that appears where the thumb lands on the lower-left,
    /// and a big context button on the lower-right whose label/icon is supplied by <see cref="ContextPrompt"/>.
    /// Feeds <see cref="GameInput"/>; hidden automatically when keyboard / gamepad is used.
    /// </summary>
    public sealed class TouchControls : MonoBehaviour
    {
        [SerializeField] Font font;
        [SerializeField] float stickRadius = 110f;

        RectTransform stickBase, stickKnob, actionButton;
        Text actionLabel;
        CanvasGroup group;
        Image actionImage;

        public static string ActionText { get; set; } = "";
        public static bool ActionVisible { get; set; }
        /// <summary>World position the floating prompt bubble points at (the boat, the door...).</summary>
        public static Vector3 ActionAnchor { get; set; }
        public static bool HasAnchor { get; set; }

        public static void Publish(string text, Vector3 anchor)
        {
            ActionText = text ?? "";
            ActionVisible = ActionText.Length > 0;
            ActionAnchor = anchor;
            HasAnchor = ActionVisible;
        }

        void Awake()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        void Build()
        {
            var canvasGo = new GameObject("Touch Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            group = canvasGo.GetComponent<CanvasGroup>();
            var root = canvasGo.GetComponent<RectTransform>();

            // Look zone: everywhere else — horizontal drags rotate the camera.
            var look = new GameObject("LookZone", typeof(RectTransform), typeof(Image), typeof(LookZone)).GetComponent<RectTransform>();
            look.SetParent(root, false);
            look.anchorMin = Vector2.zero; look.anchorMax = Vector2.one;
            look.offsetMin = look.offsetMax = Vector2.zero;
            look.GetComponent<Image>().color = new Color(0, 0, 0, 0);

            // Joystick zone: lower-left 55% x 45% of the screen.
            var zone = new GameObject("StickZone", typeof(RectTransform), typeof(Image), typeof(StickZone)).GetComponent<RectTransform>();
            zone.SetParent(root, false);
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = new Vector2(0.55f, 0.45f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            zone.GetComponent<Image>().color = new Color(0, 0, 0, 0);
            var stickZone = zone.GetComponent<StickZone>();
            stickZone.owner = this;

            stickBase = Circle(zone, "Base", 2.1f * stickRadius, new Color(1f, 1f, 1f, 0.18f));
            stickKnob = Circle(stickBase, "Knob", 0.9f * stickRadius, new Color(1f, 0.97f, 0.9f, 0.85f));
            stickBase.gameObject.SetActive(false);

            actionButton = Circle(root, "Action", 230f, new Color(0.98f, 0.95f, 0.88f, 0.95f));
            actionButton.anchorMin = actionButton.anchorMax = new Vector2(1f, 0f);
            actionButton.anchoredPosition = new Vector2(-190f, 260f);
            actionImage = actionButton.GetComponent<Image>();
            actionImage.raycastTarget = true;
            actionButton.gameObject.AddComponent<ActionButton>().owner = this;
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<RectTransform>();
            label.SetParent(actionButton, false);
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(12, 12);
            label.offsetMax = new Vector2(-12, -12);
            actionLabel = label.GetComponent<Text>();
            actionLabel.font = font;
            actionLabel.fontSize = 40;
            actionLabel.fontStyle = FontStyle.Bold;
            actionLabel.alignment = TextAnchor.MiddleCenter;
            actionLabel.color = new Color(0.2f, 0.16f, 0.13f);
            actionLabel.raycastTarget = false;
        }

        RectTransform Circle(RectTransform parent, string name, float size, Color color)
        {
            var rt = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.sizeDelta = new Vector2(size, size);
            var img = rt.GetComponent<Image>();
            img.sprite = CircleSprite();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        static Sprite circle;
        static Sprite CircleSprite()
        {
            if (circle != null) return circle;
            const int s = 128;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(s / 2f, s / 2f));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(s / 2f - d)));
            }
            tex.Apply();
            circle = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
            return circle;
        }

        void Update()
        {
            var input = GameInput.Instance;
            bool show = input == null || input.UsingTouch || Application.isMobilePlatform;
            group.alpha = Mathf.MoveTowards(group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            group.blocksRaycasts = show;

            bool visible = ActionVisible && !string.IsNullOrEmpty(ActionText);
            actionButton.gameObject.SetActive(visible);
            actionLabel.text = ActionText;
        }

        // ------------------------------------------------------------ joystick
        internal void StickDown(Vector2 screenPos, Camera cam, RectTransform zone)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(zone, screenPos, cam, out var local);
            stickBase.gameObject.SetActive(true);
            stickBase.localPosition = local;
            stickKnob.anchoredPosition = Vector2.zero;
        }

        internal void StickDrag(Vector2 screenPos, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(stickBase, screenPos, cam, out var local);
            Vector2 clamped = Vector2.ClampMagnitude(local, stickRadius);
            stickKnob.anchoredPosition = clamped;
            Vector2 value = clamped / stickRadius;
            // Small dead zone, then a gentle response curve for fine control.
            float m = value.magnitude;
            value = m < 0.12f ? Vector2.zero : value.normalized * Mathf.Pow((m - 0.12f) / 0.88f, 1.3f);
            GameInput.Instance?.SetTouchMove(value);
        }

        internal void StickUp()
        {
            stickBase.gameObject.SetActive(false);
            GameInput.Instance?.SetTouchMove(Vector2.zero);
        }

        internal void ActionDown()
        {
            actionButton.localScale = Vector3.one * 0.92f;
            GameInput.Instance?.SetTouchAction(true);
        }

        internal void ActionUp()
        {
            actionButton.localScale = Vector3.one;
            GameInput.Instance?.SetTouchAction(false);
        }

        sealed class StickZone : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            internal TouchControls owner;
            public void OnPointerDown(PointerEventData e) { owner.StickDown(e.position, e.pressEventCamera, (RectTransform)transform); owner.StickDrag(e.position, e.pressEventCamera); }
            public void OnDrag(PointerEventData e) => owner.StickDrag(e.position, e.pressEventCamera);
            public void OnPointerUp(PointerEventData e) => owner.StickUp();
        }

        sealed class LookZone : MonoBehaviour, IDragHandler
        {
            public void OnDrag(PointerEventData e) => GameInput.Instance?.AddTouchLook(e.delta.x * 0.18f);
        }

        sealed class ActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
        {
            internal TouchControls owner;
            public void OnPointerDown(PointerEventData e) => owner.ActionDown();
            public void OnPointerUp(PointerEventData e) => owner.ActionUp();
        }
    }
}
