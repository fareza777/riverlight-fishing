using CozyFishing.Core;
using CozyFishing.Fishing;
using CozyFishing.World;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>
    /// Builds and drives the whole HUD at runtime: creel counter, clock, contextual button hints,
    /// cast power / tension meters, "new fishing spot" popup, catch card and toasts.
    /// </summary>
    public sealed class HUD : MonoBehaviour
    {
        [SerializeField] FishingController fishing;
        [SerializeField] TimeOfDay timeOfDay;
        [SerializeField] Player.PlayerModes modes;
        [SerializeField] Font font;

        static readonly Color Ink = new(0.18f, 0.15f, 0.13f);
        static readonly Color Paper = new(0.98f, 0.95f, 0.88f, 0.96f);
        static readonly Color Accent = new(0.85f, 0.42f, 0.29f);
        static readonly Color Teal = new(0.25f, 0.56f, 0.53f);

        Sprite rounded;
        Text coinText;
        Text creelText, clockText, hintText, popupTitle, popupSub, toastText;
        RectTransform popup, toast, card, meterRoot, safeBandRect, needle, progressFill, chargeFill;
        CanvasGroup popupGroup, toastGroup, cardGroup, meterGroup;
        Text cardName, cardInfo, cardDesc, cardBadge, cardHint;
        Image cardArt;
        Text cardHeader;
        Text cardTap;
        CanvasGroup hintGroup;
        float popupTimer, toastTimer;

        void Awake()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = MakeRoundedSprite(64, 18);
            Build();
        }

        void OnEnable()
        {
            FishingSpot.Discovered += OnSpotDiscovered;
            if (fishing == null) return;
            fishing.Caught += OnCaught;
            fishing.Lost += OnLost;
            fishing.CreelFull += OnCreelFull;
            fishing.StateChanged += OnState;
        }

        void OnDisable()
        {
            FishingSpot.Discovered -= OnSpotDiscovered;
            if (fishing == null) return;
            fishing.Caught -= OnCaught;
            fishing.Lost -= OnLost;
            fishing.CreelFull -= OnCreelFull;
            fishing.StateChanged -= OnState;
        }

        // ---------------------------------------------------------------- build
        void Build()
        {
            var canvasGo = new GameObject("HUD Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;   // above the touch look-zone so the catch card can be tapped
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.GetComponent<RectTransform>();

            // Creel pill (top-left)
            var creelPill = Panel(root, "Creel", new Vector2(0, 1), new Vector2(30, -36), new Vector2(220, 76), Paper);
            var icon = Panel(creelPill, "Icon", new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(50, 50), new Color(0.9f, 0.74f, 0.38f));
            Label(icon, "Basket", "▤", 30, Ink, TextAnchor.MiddleCenter).rectTransform.anchoredPosition = Vector2.zero;
            creelText = Label(creelPill, "Count", "0 / 8", 34, Ink, TextAnchor.MiddleLeft);
            creelText.rectTransform.offsetMin = new Vector2(80, 0);
            creelText.fontStyle = FontStyle.Bold;

            // Coins
            var coinPill = Panel(root, "Coins", new Vector2(0, 1), new Vector2(262, -36), new Vector2(220, 76), Paper);
            coinText = Label(coinPill, "Coins", "", 34, Ink, TextAnchor.MiddleCenter);
            coinText.fontStyle = FontStyle.Bold;

            // Clock + menu button (top-right)
            var clock = Panel(root, "Clock", new Vector2(1, 1), new Vector2(-138, -36), new Vector2(300, 76), Paper);
            clockText = Label(clock, "Time", "07:30  Morning", 28, Ink, TextAnchor.MiddleCenter);
            var menu = UIKit.Button(root, "≡", new Vector2(1, 1), new Vector2(-30, -36), new Vector2(92, 76), () => PauseMenu.Open(), UIKit.Style.Primary);
            menu.GetComponentInChildren<Text>().fontSize = 48;

            // Hint bar (bottom-center)
            var hint = Panel(root, "Hints", new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 64), new Color(0.12f, 0.1f, 0.09f, 0.6f));
            hintText = Label(hint, "Text", "", 28, Color.white, TextAnchor.MiddleCenter);
            hintGroup = hint.gameObject.AddComponent<CanvasGroup>();

            // Popup (top-center)
            popup = Panel(root, "SpotPopup", new Vector2(0.5f, 1), new Vector2(0, -240), new Vector2(720, 130), Paper);
            popupGroup = popup.gameObject.AddComponent<CanvasGroup>();
            var stripe = Panel(popup, "Stripe", new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(560, 8), Teal);
            stripe.GetComponent<Image>().sprite = null;
            popupTitle = Label(popup, "Title", "New fishing spot", 26, Teal, TextAnchor.UpperCenter);
            popupTitle.rectTransform.offsetMax = new Vector2(0, -22);
            popupTitle.fontStyle = FontStyle.Bold;
            popupSub = Label(popup, "Name", "", 40, Ink, TextAnchor.LowerCenter);
            popupSub.rectTransform.offsetMin = new Vector2(0, 16);
            popupGroup.alpha = 0f;

            // Toast (center, above hint)
            toast = Panel(root, "Toast", new Vector2(0.5f, 0.5f), new Vector2(0, 360), new Vector2(760, 72), new Color(0.12f, 0.1f, 0.09f, 0.8f));
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
            toastText = Label(toast, "Text", "", 26, Color.white, TextAnchor.MiddleCenter);
            toastGroup.alpha = 0f;

            // Meter (right-center): cast power / tension
            meterRoot = Panel(root, "Meter", new Vector2(0.5f, 0), new Vector2(0, 520), new Vector2(560, 92), Paper);
            meterGroup = meterRoot.gameObject.AddComponent<CanvasGroup>();
            var track = Panel(meterRoot, "Track", new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(500, 26), new Color(0.82f, 0.77f, 0.68f));
            safeBandRect = Panel(track, "Safe", new Vector2(0, 0.5f), Vector2.zero, new Vector2(100, 26), new Color(0.49f, 0.66f, 0.33f));
            chargeFill = Panel(track, "Charge", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 26), Accent);
            needle = Panel(track, "Needle", new Vector2(0, 0.5f), Vector2.zero, new Vector2(10, 44), Ink);
            var progressTrack = Panel(meterRoot, "ProgressTrack", new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(500, 10), new Color(0.82f, 0.77f, 0.68f));
            progressFill = Panel(progressTrack, "Fill", new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 10), Teal);
            meterGroup.alpha = 0f;

            // Catch card: compact, at the top of the screen so the hero and the boat stay visible.
            card = Panel(root, "CatchCard", new Vector2(0.5f, 1), new Vector2(0, -128), new Vector2(980, 380), Paper);
            cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            var art = new GameObject("FishArt", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            art.SetParent(card, false);
            art.anchorMin = art.anchorMax = new Vector2(0f, 0.5f);
            art.pivot = new Vector2(0f, 0.5f);
            art.anchoredPosition = new Vector2(24f, 6f);
            art.sizeDelta = new Vector2(420f, 300f);
            cardArt = art.GetComponent<Image>();
            cardArt.preserveAspect = true;
            cardArt.raycastTarget = false;
            RectTransform Column(Text t, float top, float bottom)
            {
                var rt = t.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(460f, bottom); rt.offsetMax = new Vector2(-30f, -top);
                return rt;
            }
            var header = cardHeader = Label(card, "Header", Loc.T("you_caught"), 24, Teal, TextAnchor.UpperLeft);
            Column(header, 34, 0);
            header.fontStyle = FontStyle.Bold;
            cardName = Label(card, "Name", "Silver Bream", 54, Ink, TextAnchor.UpperLeft);
            Column(cardName, 68, 0);
            cardName.fontStyle = FontStyle.Bold;
            cardInfo = Label(card, "Info", "", 28, new Color(0.45f, 0.38f, 0.32f), TextAnchor.UpperLeft);
            Column(cardInfo, 138, 0);
            cardDesc = Label(card, "Desc", "", 25, Ink, TextAnchor.UpperLeft);
            Column(cardDesc, 186, 56);
            var badge = Panel(card, "Badge", new Vector2(1, 1), new Vector2(14, 14), new Vector2(130, 52), Accent);
            cardBadge = Label(badge, "Text", Loc.T("new"), 26, Color.white, TextAnchor.MiddleCenter);
            cardBadge.fontStyle = FontStyle.Bold;
            cardHint = Label(card, "Hint", "", 23, new Color(0.45f, 0.38f, 0.32f), TextAnchor.LowerLeft);
            Column(cardHint, 0, 22);
            cardTap = Label(card, "Tap", Loc.T("tap_continue"), 24, Teal, TextAnchor.LowerRight);
            cardTap.rectTransform.offsetMin = new Vector2(0f, 22f);
            cardTap.rectTransform.offsetMax = new Vector2(-30f, 0f);
            cardTap.fontStyle = FontStyle.Bold;
            var cardImage = card.GetComponent<Image>();
            cardImage.raycastTarget = true;
            card.gameObject.AddComponent<Button>().onClick.AddListener(() => { if (fishing != null) fishing.ConfirmCatch(); });
            cardGroup.alpha = 0f;
            cardGroup.blocksRaycasts = false;
            card.localScale = Vector3.one * 0.8f;
        }

        RectTransform Panel(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        Text Label(RectTransform parent, string name, string text, int size, Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        static Sprite MakeRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        // ---------------------------------------------------------------- update
        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (fishing != null && fishing.Creel != null)
                creelText.text = $"{fishing.Creel.Count} / {fishing.Creel.Capacity}";
            coinText.text = UIKit.Coins(GameState.Instance.Data.coins);
            if (timeOfDay != null) clockText.text = ClockLabel(timeOfDay.Hour);

            hintText.text = HintFor(fishing);
            UpdateMeter(dt);

            popupTimer -= dt;
            Fade(popupGroup, popupTimer > 0f, dt, 4f);
            popup.anchoredPosition = new Vector2(0, Mathf.Lerp(-160f, -240f, popupGroup.alpha));

            toastTimer -= dt;
            Fade(toastGroup, toastTimer > 0f, dt, 5f);

            bool showCard = fishing != null && fishing.State == FishingState.Landing;
            Fade(cardGroup, showCard, dt, 6f);
            cardGroup.blocksRaycasts = showCard;
            cardGroup.interactable = showCard;
            if (showCard) cardTap.color = new Color(Teal.r, Teal.g, Teal.b, 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f)));
            card.localScale = Vector3.one * Mathf.Lerp(0.85f, 1f, EaseOutBack(cardGroup.alpha));
            card.anchoredPosition = new Vector2(0f, Mathf.Lerp(-40f, -128f, EaseOutBack(cardGroup.alpha)));
            if (cardArt.enabled) cardArt.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 2.2f) * 3f);
            Fade(hintGroup, !string.IsNullOrEmpty(hintText.text) && !showCard, dt, 6f);
        }

        void UpdateMeter(float dt)
        {
            if (fishing == null) return;
            var s = fishing.State;
            bool show = s is FishingState.Charging or FishingState.Reeling;
            Fade(meterGroup, show, dt, 8f);
            const float width = 500f;
            if (s == FishingState.Charging)
            {
                safeBandRect.gameObject.SetActive(false);
                progressFill.parent.gameObject.SetActive(false);
                chargeFill.gameObject.SetActive(true);
                chargeFill.sizeDelta = new Vector2(width * fishing.Charge, 26);
                needle.anchoredPosition = new Vector2(width * fishing.Charge - 5f, 0);
            }
            else if (s == FishingState.Reeling)
            {
                safeBandRect.gameObject.SetActive(true);
                progressFill.parent.gameObject.SetActive(true);
                chargeFill.gameObject.SetActive(false);
                var band = fishing.SafeBand;
                safeBandRect.anchoredPosition = new Vector2(width * band.x, 0);
                safeBandRect.sizeDelta = new Vector2(width * (band.y - band.x), 26);
                float tension = Mathf.Clamp01(fishing.Tension);
                needle.anchoredPosition = new Vector2(width * tension - 5f + Mathf.Sin(Time.time * 40f) * 2f * tension, 0);
                needle.GetComponent<Image>().color = tension > band.y || fishing.FishRunning ? Accent : Ink;
                // The band flashes orange while the fish runs, telling the player to ease off.
                bool flash = fishing.FishRunning && Mathf.Repeat(Time.time * 6f, 1f) > 0.5f;
                safeBandRect.GetComponent<Image>().color = flash ? new Color(0.95f, 0.55f, 0.35f) : new Color(0.49f, 0.66f, 0.33f);
                progressFill.sizeDelta = new Vector2(width * fishing.Progress, 10);
            }
        }

        static void Fade(CanvasGroup g, bool visible, float dt, float speed) =>
            g.alpha = Mathf.MoveTowards(g.alpha, visible ? 1f : 0f, dt * speed);

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }


        string HintFor(FishingController f)
        {
            if (modes != null && !modes.InBoat) return Loc.T("walk_hint");
            if (f == null) return "";
            return f.State switch
            {
                FishingState.Rowing when f.Creel != null && f.Creel.IsFull => Loc.T("creel_full"),
                FishingState.Rowing => Loc.T("row_hint"),
                FishingState.Casting or FishingState.Waiting => Loc.T("waiting"),
                FishingState.Bite => Loc.T("bite"),
                FishingState.Reeling when f.FishRunning => Loc.T("let_it_run"),
                FishingState.Reeling => Loc.T("reel_hint"),
                _ => "",
            };
        }

        static string ClockLabel(float hour)
        {
            int h = Mathf.FloorToInt(hour);
            int m = Mathf.FloorToInt((hour - h) * 60f / 10f) * 10;
            string part = Loc.T(hour < 5f ? "night" : hour < 11f ? "morning" : hour < 14f ? "noon" : hour < 18f ? "afternoon" : hour < 21f ? "evening" : "night");
            return $"{h:00}:{m:00}   {part}";
        }

        // ---------------------------------------------------------------- events
        void OnSpotDiscovered(FishingSpot spot)
        {
            popupTitle.text = Loc.T("new_spot");
            popupSub.text = spot.SpotName;
            popupTimer = 3.2f;
        }

        void OnCaught(CaughtFish fish, bool isNew)
        {
            cardName.text = fish.Species.DisplayName;
            cardInfo.text = $"{fish.SizeCm:0.0} cm   ·   {fish.Species.RarityLabel}   ·   {UIKit.Coins(fish.Species.PriceFor(fish.SizeCm))}";
            cardHeader.text = fish.Species.IsLegend ? Loc.T("legend_caught") : Loc.T("you_caught");
            cardHeader.color = fish.Species.IsLegend ? UIKit.Accent : Teal;
            cardDesc.text = fish.Species.Description;
            cardArt.sprite = fish.Species.Illustration;
            cardArt.enabled = fish.Species.Illustration != null;
            cardBadge.transform.parent.gameObject.SetActive(isNew);
            cardHint.text = $"Creel {fishing.Creel.Count}/{fishing.Creel.Capacity}";
        }

        void OnState(FishingState s)
        {
            if (s == FishingState.Bite) ShowToast(Loc.T("bite"));
        }

        void OnLost(string reason) => ShowToast(reason);
        void OnCreelFull() => ShowToast(Loc.T("creel_full"));

        void ShowToast(string message)
        {
            toastText.text = message;
            toastTimer = 2.6f;
        }
    }
}
