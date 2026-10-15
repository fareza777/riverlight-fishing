using System;
using System.Linq;
using CozyFishing.Core;
using CozyFishing.Data;
using CozyFishing.Fishing;
using CozyFishing.Story;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CozyFishing.UI
{
    /// <summary>
    /// The in-game book: Journal (all 48 fish), Bag (character, stats, gear, creel), Map (travel), Settings.
    /// Pauses the game while open. Built in code like the rest of the UI.
    /// </summary>
    public sealed class PauseMenu : MonoBehaviour
    {
        public enum Tab { Journal, Bag, Map, Settings }
        static PauseMenu instance;
        public static bool IsOpen => instance != null && instance.open;

        CanvasGroup group;
        RectTransform page;
        readonly Button[] tabButtons = new Button[4];
        Tab tab;
        bool open;
        float oldTimeScale = 1f;
        string journalLocation;
        RectTransform detail;

        public static void Open(Tab t = Tab.Journal)
        {
            if (CutscenePlayer.Playing) return;
            if (instance == null)
            {
                var go = new GameObject("PauseMenu");
                instance = go.AddComponent<PauseMenu>();
                instance.Build();
            }
            instance.Show(t);
        }

        public static void Close() { if (instance != null) instance.Hide(); }

        void Build()
        {
            var canvas = UIKit.Canvas(transform, "Menu Canvas", 200);
            var root = (RectTransform)canvas.transform;
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            var dim = UIKit.Image(root, "Dim", UIKit.Dim); UIKit.Fill(dim); dim.raycastTarget = true;
            var book = UIKit.Panel(root, "Book", UIKit.Paper);
            UIKit.Anchor(book.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 60), new Vector2(-30, -60));
            book.raycastTarget = true;

            var tabs = new[] { ("tab_journal", Tab.Journal), ("tab_bag", Tab.Bag), ("tab_map", Tab.Map), ("tab_settings", Tab.Settings) };
            for (int i = 0; i < tabs.Length; i++)
            {
                var t = tabs[i].Item2;
                var b = UIKit.Button(book.rectTransform, Loc.T(tabs[i].Item1), new Vector2(0, 1), new Vector2(24 + i * 222, -24), new Vector2(210, 92), () => Switch(t), UIKit.Style.Secondary);
                b.GetComponentInChildren<Text>().fontSize = 30;
                tabButtons[i] = b;
            }
            UIKit.Button(book.rectTransform, "×", new Vector2(1, 1), new Vector2(-20, -24), new Vector2(92, 92), Hide, UIKit.Style.Primary).GetComponentInChildren<Text>().fontSize = 54;

            page = UIKit.Rect(book.rectTransform, "Page");
            UIKit.Anchor(page, Vector2.zero, Vector2.one, new Vector2(30, 30), new Vector2(-30, -140));
        }

        void Show(Tab t)
        {
            if (!open)
            {
                oldTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            open = true;
            group.alpha = 1f; group.blocksRaycasts = group.interactable = true;
            Audio.SfxPlayer.Play("book_open", 0.7f, 0f);
            Switch(t);
        }

        void Hide()
        {
            if (!open) return;
            open = false;
            group.alpha = 0f; group.blocksRaycasts = group.interactable = false;
            Time.timeScale = oldTimeScale <= 0f ? 1f : oldTimeScale;
            GameState.Instance.Save();
        }

        void Update()
        {
            if (open && GameInput.Instance != null && GameInput.Instance.CancelPressed)
            {
                if (detail != null) { Destroy(detail.gameObject); detail = null; }
                else Hide();
            }
        }

        void Switch(Tab t)
        {
            tab = t;
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var img = tabButtons[i].GetComponent<Image>();
                img.color = i == (int)t ? UIKit.Teal : UIKit.PaperDark;
                tabButtons[i].GetComponentInChildren<Text>().color = i == (int)t ? Color.white : UIKit.Ink;
            }
            foreach (Transform c in page) Destroy(c.gameObject);
            detail = null;
            switch (t)
            {
                case Tab.Journal: BuildJournal(); break;
                case Tab.Bag: BuildBag(); break;
                case Tab.Map: BuildMap(); break;
                case Tab.Settings: SettingsPanel.Build(page, true); break;
            }
        }

        // ------------------------------------------------------------------ journal
        void BuildJournal()
        {
            var gs = GameState.Instance;
            var db = GameState.Db;
            journalLocation ??= gs.Data.location;
            int known = gs.Data.journal.Count;
            var head = UIKit.Label(page, "Objective", $"<b>{Loc.T("objective")}</b>  {StoryDirector.Objective(gs.Data.chapter)}", 30, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), Vector2.zero);
            var count = UIKit.Label(page, "Count", Loc.T("journal_count", known, db.fish.Count), 28, UIKit.InkSoft, TextAnchor.UpperRight);
            UIKit.Anchor(count.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -160), new Vector2(0, -116));

            for (int i = 0; i < db.locations.Count; i++)
            {
                var loc = db.locations[i];
                bool sel = loc.id == journalLocation;
                var chip = UIKit.Button(page, loc.Name, new Vector2(0, 1), new Vector2(i * 246, -170), new Vector2(236, 70), () => { journalLocation = loc.id; Switch(Tab.Journal); }, sel ? UIKit.Style.Primary : UIKit.Style.Secondary);
                chip.GetComponentInChildren<Text>().fontSize = 24;
            }

            var content = UIKit.ScrollList(page, "Fish", 16f, out var scroll, 3, new Vector2(300, 360));
            UIKit.Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -260));
            foreach (var f in db.FishAt(journalLocation))
            {
                var entry = gs.Entry(f.Id);
                var card = UIKit.Panel(content, f.Id, entry != null ? Color.white : UIKit.PaperDark);
                card.raycastTarget = true;
                if (f.IsLegend) { var ring = UIKit.Panel(card.rectTransform, "Legend", UIKit.Gold); UIKit.Fill(ring, -4); ring.transform.SetAsFirstSibling(); }
                var art = UIKit.Image(card.rectTransform, "Art", entry != null ? Color.white : new Color(0.15f, 0.12f, 0.1f, 0.55f), f.Illustration);
                art.preserveAspect = true;
                UIKit.Anchor(art.rectTransform, new Vector2(0, 0.35f), Vector2.one, new Vector2(16, 0), new Vector2(-16, -16));
                var name = UIKit.Label(card.rectTransform, "Name", entry != null ? f.DisplayName : "???", 26, UIKit.Ink, TextAnchor.MiddleCenter);
                name.fontStyle = FontStyle.Bold;
                UIKit.Anchor(name.rectTransform, new Vector2(0, 0.14f), new Vector2(1, 0.35f), new Vector2(8, 0), new Vector2(-8, 0));
                var sub = UIKit.Label(card.rectTransform, "Sub", entry != null ? $"{entry.bestCm:0.0} cm · x{entry.count}" : f.RarityLabel, 22, UIKit.InkSoft, TextAnchor.MiddleCenter);
                UIKit.Anchor(sub.rectTransform, Vector2.zero, new Vector2(1, 0.14f), new Vector2(8, 6), new Vector2(-8, 0));
                var btn = card.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => ShowFish(f));
            }
        }

        void ShowFish(FishSpecies f)
        {
            var gs = GameState.Instance;
            var entry = gs.Entry(f.Id);
            detail = UIKit.Panel(page, "Detail", Color.white).rectTransform;
            UIKit.Fill(detail.GetComponent<Image>());
            detail.GetComponent<Image>().raycastTarget = true;
            var art = UIKit.Image(detail, "Art", entry != null ? Color.white : new Color(0.15f, 0.12f, 0.1f, 0.6f), f.Illustration);
            art.preserveAspect = true;
            UIKit.Anchor(art.rectTransform, new Vector2(0, 0.6f), Vector2.one, new Vector2(40, 0), new Vector2(-40, -40));
            string loc = GameState.Db.Location(f.Location)?.Name ?? "";
            string body = entry != null
                ? $"<size=60><b>{f.DisplayName}</b></size>\n<color=#{ColorUtility.ToHtmlStringRGB(f.IsLegend ? UIKit.Accent : UIKit.Teal)}>{f.RarityLabel}</color>  ·  {loc}\n\n{f.Description}\n\n" +
                  $"{Loc.T("best")}: <b>{entry.bestCm:0.0} cm</b>    {Loc.T("caught")}: <b>{entry.count}</b>\n<i>{f.Hint}</i>"
                : $"<size=60><b>???</b></size>\n{f.RarityLabel}  ·  {loc}\n\n<i>{f.Hint}</i>";
            var text = UIKit.Label(detail, "Body", body, 32, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Anchor(text.rectTransform, Vector2.zero, new Vector2(1, 0.6f), new Vector2(50, 150), new Vector2(-50, -20));
            UIKit.Button(detail, Loc.T("back"), new Vector2(0.5f, 0), new Vector2(f.IsLegend && entry != null ? -170 : 0, 40), new Vector2(300, 90), () => { Destroy(detail.gameObject); detail = null; }, UIKit.Style.Secondary);
            if (f.IsLegend && entry != null)
            {
                string id = "memory_" + f.Chapter;
                UIKit.Button(detail, Loc.T("replay_memory"), new Vector2(0.5f, 0), new Vector2(170, 40), new Vector2(300, 90), () =>
                {
                    Hide();
                    CutscenePlayer.Play(GameState.Db.Cutscene(id));
                });
            }
        }

        // ------------------------------------------------------------------ bag
        void BuildBag()
        {
            var gs = GameState.Instance;
            var d = gs.Data;
            var db = GameState.Db;
            var portrait = UIKit.Image(page, "Portrait", Color.white, d.character == "FisherF" ? db.portraitLaras : db.portraitArka);
            portrait.preserveAspect = true;
            UIKit.Place(portrait.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(330, 440));
            int legends = d.journal.Count(j => db.Fish(j.fishId) != null && db.Fish(j.fishId).IsLegend);
            var time = TimeSpan.FromSeconds(d.playSeconds);
            var stats = UIKit.Label(page, "Stats",
                $"<size=56><b>{d.playerName}</b></size>\n{Loc.T("chapter_n", Mathf.Min(d.chapter, StoryDirector.FinalChapter))}  ·  {Loc.T("day_n", d.day)}\n\n" +
                $"{UIKit.Coins(d.coins)}\n{Loc.T("stat_caught")}: <b>{d.totalCaught}</b>\n{Loc.T("stat_species")}: <b>{d.journal.Count}/{db.fish.Count}</b>\n" +
                $"{Loc.T("stat_legends")}: <b>{legends}/5</b>\n{Loc.T("stat_earned")}: <b>{d.totalEarned}</b>\n{Loc.T("stat_time")}: <b>{(int)time.TotalHours}h {time.Minutes:00}m</b>",
                30, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Anchor(stats.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(360, -460), Vector2.zero);

            var content = UIKit.ScrollList(page, "Gear", 14f, out var scroll);
            UIKit.Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -480));
            Header(content, Loc.T("gear"));
            foreach (var item in db.items.Where(i => (i.type == ItemType.Rod || i.type == ItemType.Creel) && gs.Owns(i.id)))
            {
                bool equipped = item.id == d.rod || item.id == d.creelItem;
                ItemRow(content, item, equipped ? Loc.T("equipped") : Loc.T("equip"), equipped ? null : () => { gs.Equip(item); Switch(Tab.Bag); });
            }
            // Outfits: tap to wear, tap the worn one again for the default clothes.
            var outfits = db.items.Where(i => i.type == ItemType.Outfit && i.forCharacter == d.character && gs.Owns(i.id)).ToList();
            if (outfits.Count > 0)
            {
                Header(content, Loc.T("outfits"));
                foreach (var item in outfits)
                {
                    bool worn = item.id == d.outfit;
                    ItemRow(content, item, worn ? Loc.T("take_off") : Loc.T("wear"), () => { gs.Equip(item); Switch(Tab.Bag); });
                }
            }
            int decor = db.items.Count(i => i.type == ItemType.Decor && gs.Owns(i.id));
            if (decor > 0) Row(content, $"{Loc.T("decor")}: <b>{decor}</b> {Loc.T("decor_in_cabin")}", 30);
            var bait = gs.ActiveBait;
            Row(content, $"{Loc.T("bait")}: <b>{(bait != null ? bait.Name + " ×" + d.baitCount : Loc.T("none"))}</b>", 30);
            Header(content, Loc.T("creel_contents", d.creel.Count, gs.CreelCapacity));
            if (d.creel.Count == 0) Row(content, Loc.T("creel_empty"), 28);
            foreach (var g in d.creel.GroupBy(c => c.fishId))
            {
                var f = db.Fish(g.Key);
                if (f == null) continue;
                Row(content, $"{f.DisplayName}  ×{g.Count()}   <color=#7A6A5A>{Loc.T("best")} {g.Max(x => x.sizeCm):0.0} cm</color>", 30);
            }
        }

        static void Header(RectTransform list, string text)
        {
            var t = UIKit.Label(list, "Header", $"<b>{text}</b>", 34, UIKit.Teal, TextAnchor.LowerLeft);
            t.rectTransform.sizeDelta = new Vector2(0, 70);
        }

        static void Row(RectTransform list, string text, int size)
        {
            var t = UIKit.Label(list, "Row", text, size, UIKit.Ink, TextAnchor.MiddleLeft);
            t.rectTransform.sizeDelta = new Vector2(0, 54);
        }

        public static void ItemRow(RectTransform list, ItemDef item, string action, UnityEngine.Events.UnityAction onClick, string price = null)
        {
            var row = UIKit.Panel(list, item.id, Color.white);
            row.rectTransform.sizeDelta = new Vector2(0, 150);
            if (item.icon != null)
            {
                var icon = UIKit.Image(row.rectTransform, "Icon", Color.white, item.icon);
                icon.preserveAspect = true;
                UIKit.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(120, 120));
            }
            var t = UIKit.Label(row.rectTransform, "Text", $"<b>{item.Name}</b>{(price != null ? "   " + price : "")}\n<size=24><color=#7A6A5A>{item.Desc}</color></size>", 30, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(item.icon != null ? 150 : 24, 0), new Vector2(-250, 0));
            var b = UIKit.Button(row.rectTransform, action, new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 90), onClick, onClick != null ? UIKit.Style.Primary : UIKit.Style.Secondary);
            b.GetComponentInChildren<Text>().fontSize = 28;
            b.interactable = onClick != null;
        }

        // ------------------------------------------------------------------ map
        void BuildMap()
        {
            var gs = GameState.Instance;
            var content = UIKit.ScrollList(page, "Locations", 20f, out var scroll);
            UIKit.Anchor((RectTransform)scroll.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            foreach (var loc in GameState.Db.locations)
            {
                bool unlocked = gs.LocationUnlocked(loc);
                bool here = loc.sceneName == SceneManager.GetActiveScene().name;
                var card = UIKit.Panel(content, loc.id, Color.white);
                card.rectTransform.sizeDelta = new Vector2(0, 360);
                var art = UIKit.Image(card.rectTransform, "Postcard", unlocked ? Color.white : new Color(0.35f, 0.33f, 0.3f), loc.postcard);
                art.preserveAspect = false;
                UIKit.Anchor(art.rectTransform, Vector2.zero, new Vector2(0.45f, 1), new Vector2(14, 14), new Vector2(0, -14));
                var t = UIKit.Label(card.rectTransform, "Text", unlocked ? $"<size=44><b>{loc.Name}</b></size>\n<size=26>{loc.Desc}</size>" : $"<size=44><b>🔒 {loc.Name}</b></size>\n<size=26>{Loc.T("locked_until", loc.unlockChapter)}</size>", 28, UIKit.Ink, TextAnchor.UpperLeft);
                UIKit.Anchor(t.rectTransform, new Vector2(0.45f, 0), Vector2.one, new Vector2(24, 110), new Vector2(-20, -20));
                var b = UIKit.Button(card.rectTransform, here ? Loc.T("you_are_here") : Loc.T("travel"), new Vector2(1, 0), new Vector2(-20, 20), new Vector2(280, 84),
                    unlocked && !here ? () => { Hide(); GameState.Instance.Travel(loc); } : null, unlocked && !here ? UIKit.Style.Primary : UIKit.Style.Secondary);
                b.interactable = unlocked && !here;
            }
        }
    }
}
