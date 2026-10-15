using System.Collections;
using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>Title scene: splash -> main menu (Continue / New Game / Settings) -> character select -> start.</summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        [SerializeField] Sprite background;
        [SerializeField] Sprite logo;

        RectTransform root, main, select, settings;
        CanvasGroup mainGroup, selectGroup, settingsGroup, splashGroup;
        Text tagline;
        Button continueBtn, newBtn, settingsBtn, startBtn;
        Image bg;
        string chosen = "FisherM";
        InputField nameField;
        Image cardM, cardF;

        void Start()
        {
            Time.timeScale = 1f;
            GameState.Instance.Load();
            Build();
            MusicPlayer.PlayMusic("title", 1.5f);
            MusicPlayer.PlayAmbience("river");
            StartCoroutine(Splash());
        }

        void Build()
        {
            var canvas = UIKit.Canvas(transform, "Title Canvas", 10);
            root = (RectTransform)canvas.transform;
            bg = UIKit.Image(root, "Background", Color.white, background);
            bg.preserveAspect = false;
            CoverFit(bg);

            var shade = UIKit.Image(root, "Shade", new Color(0.05f, 0.04f, 0.03f, 0.25f)); UIKit.Fill(shade);

            main = UIKit.Rect(root, "Main"); UIKit.Anchor(main, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            mainGroup = main.gameObject.AddComponent<CanvasGroup>();
            // Emblem (generated art) above the game name.
            if (logo != null)
            {
                var l = UIKit.Image(main, "Emblem", Color.white, logo);
                l.preserveAspect = true;
                UIKit.Place(l.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -120), new Vector2(300, 300));
            }
            var title = UIKit.Label(main, "Logo", "Riverlight", 140, new Color(1f, 0.96f, 0.86f), TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0, logo != null ? -420 : -220), new Vector2(1000, 220));
            title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -6);
            tagline = UIKit.Label(main, "Tagline", "", 40, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter);
            tagline.fontStyle = FontStyle.Italic;
            tagline.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -3);
            UIKit.Place(tagline.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -690), new Vector2(1000, 70));

            continueBtn = UIKit.Button(main, "", new Vector2(0.5f, 0), new Vector2(0, 560), new Vector2(620, 120), () => { SfxPlayer.Play("start", 0.8f, 0f); GameState.Instance.ContinueGame(); });
            newBtn = UIKit.Button(main, "", new Vector2(0.5f, 0), new Vector2(0, 410), new Vector2(620, 120), () => Swap(mainGroup, selectGroup), UIKit.Style.Secondary);
            settingsBtn = UIKit.Button(main, "", new Vector2(0.5f, 0), new Vector2(0, 260), new Vector2(620, 120), () => Swap(mainGroup, settingsGroup), UIKit.Style.Secondary);
            continueBtn.gameObject.SetActive(GameState.HasSave);
            var ver = UIKit.Label(main, "Version", "v0.9  ·  " + Application.version, 22, new Color(1, 1, 1, 0.7f), TextAnchor.LowerCenter);
            UIKit.Anchor(ver.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 80));

            BuildSelect();
            BuildSettings();

            var splash = UIKit.Image(root, "Splash", new Color(0.07f, 0.06f, 0.05f)); UIKit.Fill(splash);
            splashGroup = splash.gameObject.AddComponent<CanvasGroup>();
            var st = UIKit.Label(splash.rectTransform, "SplashText", "a cozy fishing story", 44, new Color(0.95f, 0.88f, 0.72f), TextAnchor.MiddleCenter);
            st.fontStyle = FontStyle.Italic;
            UIKit.Fill(st);
            RefreshTexts();
        }

        void BuildSelect()
        {
            select = UIKit.Rect(root, "Select"); UIKit.Anchor(select, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            selectGroup = select.gameObject.AddComponent<CanvasGroup>();
            Hide(selectGroup);
            var panel = UIKit.Panel(select, "Panel", UIKit.Paper);
            UIKit.Anchor(panel.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 120), new Vector2(-40, -120));
            panel.raycastTarget = true;
            var head = UIKit.Label(panel.rectTransform, "Head", "", 50, UIKit.Ink, TextAnchor.MiddleCenter);
            head.name = "SelectHead";
            head.fontStyle = FontStyle.Bold;
            UIKit.Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -30));
            var db = GameDatabase.Instance;
            cardM = Card(panel.rectTransform, db != null ? db.portraitArka : null, "Kai", new Vector2(0.27f, 0.6f), () => Choose("FisherM", "Kai"));
            cardF = Card(panel.rectTransform, db != null ? db.portraitLaras : null, "Lena", new Vector2(0.73f, 0.6f), () => Choose("FisherF", "Lena"));

            var nameBg = UIKit.Panel(panel.rectTransform, "NameField", Color.white);
            nameBg.raycastTarget = true;
            UIKit.Place(nameBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 330), new Vector2(700, 110));
            var nameText = UIKit.Label(nameBg.rectTransform, "Text", "", 44, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Fill(nameText, 10);
            nameText.supportRichText = false;
            nameField = nameBg.gameObject.AddComponent<InputField>();
            nameField.textComponent = nameText;
            nameField.characterLimit = 14;
            nameField.text = "Kai";

            startBtn = UIKit.Button(panel.rectTransform, "", new Vector2(0.5f, 0), new Vector2(160, 120), new Vector2(460, 120), StartNewGame);
            UIKit.Button(panel.rectTransform, "‹", new Vector2(0.5f, 0), new Vector2(-260, 120), new Vector2(150, 120), () => Swap(selectGroup, mainGroup), UIKit.Style.Secondary);
            Choose("FisherM", "Kai");
        }

        Image Card(RectTransform parent, Sprite portrait, string label, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
        {
            var card = UIKit.Panel(parent, "Card " + label, Color.white);
            card.raycastTarget = true;
            UIKit.Place(card.rectTransform, anchor, Vector2.zero, new Vector2(440, 760));
            card.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var img = UIKit.Image(card.rectTransform, "Portrait", Color.white, portrait);
            img.preserveAspect = true;
            UIKit.Anchor(img.rectTransform, new Vector2(0, 0.14f), Vector2.one, new Vector2(16, 0), new Vector2(-16, -16));
            var t = UIKit.Label(card.rectTransform, "Name", label, 46, UIKit.Ink, TextAnchor.MiddleCenter);
            t.fontStyle = FontStyle.Bold;
            UIKit.Anchor(t.rectTransform, Vector2.zero, new Vector2(1, 0.14f), Vector2.zero, Vector2.zero);
            card.gameObject.AddComponent<Button>().onClick.AddListener(onClick);
            return card;
        }

        void Choose(string id, string defaultName)
        {
            string previousDefault = chosen == "FisherM" ? "Kai" : "Lena";
            chosen = id;
            if (nameField != null && (string.IsNullOrWhiteSpace(nameField.text) || nameField.text == previousDefault)) nameField.text = defaultName;
            cardM.color = id == "FisherM" ? UIKit.Gold : Color.white;
            cardF.color = id == "FisherF" ? UIKit.Gold : Color.white;
            cardM.rectTransform.localScale = Vector3.one * (id == "FisherM" ? 1.04f : 0.96f);
            cardF.rectTransform.localScale = Vector3.one * (id == "FisherF" ? 1.04f : 0.96f);
        }

        void StartNewGame()
        {
            string n = string.IsNullOrWhiteSpace(nameField.text) ? (chosen == "FisherM" ? "Kai" : "Lena") : nameField.text.Trim();
            SfxPlayer.Play("start", 0.8f, 0f);
            GameState.Instance.NewGame(chosen, n);
            GameState.Instance.ContinueGame();
        }

        void BuildSettings()
        {
            settings = UIKit.Rect(root, "Settings"); UIKit.Anchor(settings, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            settingsGroup = settings.gameObject.AddComponent<CanvasGroup>();
            Hide(settingsGroup);
            var panel = UIKit.Panel(settings, "Panel", UIKit.Paper);
            UIKit.Anchor(panel.rectTransform, Vector2.zero, Vector2.one, new Vector2(40, 260), new Vector2(-40, -260));
            panel.raycastTarget = true;
            var page = UIKit.Rect(panel.rectTransform, "Page");
            UIKit.Anchor(page, Vector2.zero, Vector2.one, new Vector2(40, 40), new Vector2(-40, -40));
            SettingsPanel.Build(page, false);
            UIKit.Button(settings, "‹", new Vector2(0.5f, 0), new Vector2(0, 120), new Vector2(200, 110), () => Swap(settingsGroup, mainGroup), UIKit.Style.Secondary);
        }

        public void RefreshTexts()
        {
            tagline.text = Loc.T("tagline");
            UIKit.SetLabel(continueBtn, Loc.T("continue_game"));
            UIKit.SetLabel(newBtn, Loc.T("new_game"));
            UIKit.SetLabel(settingsBtn, Loc.T("settings"));
            UIKit.SetLabel(startBtn, Loc.T("begin"));
            foreach (var t in GetComponentsInChildren<Text>(true)) if (t.name == "SelectHead") t.text = Loc.T("choose_character");
        }

        IEnumerator Splash()
        {
            splashGroup.alpha = 1f;
            Hide(mainGroup);
            yield return new WaitForSeconds(1.6f);
            for (float t = 1; t > 0; t -= Time.deltaTime / 0.8f) { splashGroup.alpha = t; yield return null; }
            splashGroup.alpha = 0f; splashGroup.blocksRaycasts = false;
            for (float t = 0; t < 1; t += Time.deltaTime / 0.6f) { mainGroup.alpha = t; yield return null; }
            Show(mainGroup);
        }

        void Update()
        {
            // Slow drift over the painting.
            float t = Time.time * 0.03f;
            bg.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t) * 30f, Mathf.Cos(t * 0.7f) * 18f);
        }

        static void Swap(CanvasGroup from, CanvasGroup to) { Hide(from); Show(to); }
        static void Hide(CanvasGroup g) { g.alpha = 0f; g.blocksRaycasts = g.interactable = false; }
        static void Show(CanvasGroup g) { g.alpha = 1f; g.blocksRaycasts = g.interactable = true; }

        static void CoverFit(Image img)
        {
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float aspect = img.sprite != null ? img.sprite.rect.width / img.sprite.rect.height : 9f / 16f;
            rt.sizeDelta = aspect > 1080f / 1920f ? new Vector2(1980f * aspect, 1980f) : new Vector2(1140f, 1140f / aspect);
        }
    }
}
