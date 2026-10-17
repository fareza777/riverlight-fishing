using System;
using System.IO;
using System.Linq;
using CozyFishing.Data;
using CozyFishing.Fishing;
using CozyFishing.Monetization;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyFishing.Core
{
    /// <summary>
    /// The persistent game session: save data, economy, journal, story chapter and scene travel.
    /// Survives scene loads; auto-saves on important events and when the app is paused.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GameState : MonoBehaviour
    {
        static GameState instance;
        public static GameState Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("GameState");
                    instance = go.AddComponent<GameState>();
                    DontDestroyOnLoad(go);
                    instance.Load();
                }
                return instance;
            }
        }

        public SaveData Data { get; private set; } = new();
        public static string SavePath => Path.Combine(Application.persistentDataPath, "riverlight_save.json");
        public static bool HasSave => File.Exists(SavePath);
        public static GameDatabase Db => GameDatabase.Instance;

        public event Action Changed;
        public event Action<int> ChapterChanged;
        public event Action<FishSpecies, float, bool> FishCaught;
        bool travelPending;
        public const int AdRewardCoins = 50;
        public bool CanClaimAdReward => (Data.adRewards ??= new DailyAdRewards()).CanClaim(Data.day);

        public bool ClaimAdReward()
        {
            if (!(Data.adRewards ??= new DailyAdRewards()).TryClaim(Data.day)) return false;
            Data.coins += AdRewardCoins;
            // Ad bonuses do not advance fishing-sale/story milestones.
            Save();
            Changed?.Invoke();
            return true;
        }

        void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update() => Data.playSeconds += Time.unscaledDeltaTime;

        // ------------------------------------------------------------------ save / load
        public void Load()
        {
            // A self-test that was stopped half-way leaves the player's real save in a backup: put it back.
            string backup = SavePath + ".tourbak";
            if (File.Exists(backup) && !DevArgs.Has("-selftest-tour"))
            {
                File.Copy(backup, SavePath, true);
                File.Delete(backup);
                Debug.Log("[GameState] restored the save backed up by an interrupted self-test");
            }
            try
            {
                Data = HasSave ? JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)) ?? new SaveData() : new SaveData();
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameState] Save could not be read, starting fresh: {e.Message}");
                Data = new SaveData();
            }
        }

        public void Save()
        {
            try
            {
                var w = FindAnyObjectByType<World.TimeOfDay>();
                if (w != null) Data.hour = w.Hour;
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameState] Save failed: {e.Message}");
            }
        }

        public void NewGame(string character, string playerName)
        {
            Data = new SaveData { character = character, playerName = playerName, coins = 40 };
            Save();
            Changed?.Invoke();
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        // ------------------------------------------------------------------ economy & items
        public ItemDef Rod => Db.Item(Data.rod);
        public int CreelCapacity => Db.Item(Data.creelItem)?.capacity ?? 8;
        public bool Owns(string itemId) => Data.owned.Contains(itemId);

        public bool Buy(ItemDef item)
        {
            if (item == null || Data.coins < item.price) return false;
            if (item.type != ItemType.Bait && Owns(item.id)) return false;
            Data.coins -= item.price;
            switch (item.type)
            {
                case ItemType.Rod: Data.owned.Add(item.id); Data.rod = item.id; break;
                case ItemType.Creel: Data.owned.Add(item.id); Data.creelItem = item.id; break;
                case ItemType.Decor: Data.owned.Add(item.id); break;
                case ItemType.Outfit: Data.owned.Add(item.id); Data.outfit = item.id; break;
                case ItemType.Bait:
                    if (Data.bait != item.id) { Data.bait = item.id; Data.baitCount = 0; }
                    Data.baitCount += 5;
                    break;
            }
            Save();
            Changed?.Invoke();
            return true;
        }

        public void Equip(ItemDef item)
        {
            if (item == null || !Owns(item.id)) return;
            if (item.type == ItemType.Rod) Data.rod = item.id;
            if (item.type == ItemType.Creel) Data.creelItem = item.id;
            if (item.type == ItemType.Outfit) Data.outfit = Data.outfit == item.id ? "" : item.id;   // tap again for default clothes
            Save();
            Changed?.Invoke();
        }

        /// <summary>Gives an item for free (story rewards) and equips it.</summary>
        public void Grant(string itemId)
        {
            var item = Db.Item(itemId);
            if (item == null || Owns(itemId)) return;
            Data.owned.Add(itemId);
            Equip(item);
            Save();
        }

        public ItemDef ActiveBait => Data.baitCount > 0 ? Db.Item(Data.bait) : null;

        public void ConsumeBait()
        {
            if (Data.baitCount <= 0) return;
            Data.baitCount--;
            Changed?.Invoke();
        }

        /// <summary>Sells everything in the creel. Returns coins earned.</summary>
        public int SellCreel()
        {
            int total = 0;
            foreach (var f in Data.creel)
            {
                var sp = Db.Fish(f.fishId);
                if (sp != null) total += sp.PriceFor(f.sizeCm);
            }
            Data.creel.Clear();
            Data.coins += total;
            Data.totalEarned += total;
            Save();
            Changed?.Invoke();
            return total;
        }

        // ------------------------------------------------------------------ catches & journal
        /// <returns>true when the species is new to the journal.</returns>
        public bool RecordCatch(FishSpecies species, float sizeCm)
        {
            var e = Data.journal.FirstOrDefault(j => j.fishId == species.Id);
            bool isNew = e == null;
            if (isNew) Data.journal.Add(e = new JournalEntry { fishId = species.Id });
            e.count++;
            e.bestCm = Mathf.Max(e.bestCm, sizeCm);
            Data.totalCaught++;
            if (!species.IsLegend) Data.creel.Add(new CreelFish { fishId = species.Id, sizeCm = sizeCm });
            FishCaught?.Invoke(species, sizeCm, isNew);
            Save();
            Changed?.Invoke();
            return isNew;
        }

        public JournalEntry Entry(string fishId) => Data.journal.FirstOrDefault(j => j.fishId == fishId);
        public bool CreelFull => Data.creel.Count >= CreelCapacity;

        // ------------------------------------------------------------------ story
        public void AdvanceChapter(int to)
        {
            if (to <= Data.chapter) return;
            Data.chapter = to;
            Save();
            ChapterChanged?.Invoke(to);
            Changed?.Invoke();
        }

        public bool LocationUnlocked(LocationDef loc) => loc != null && Data.chapter >= loc.unlockChapter;

        public void MarkCutsceneSeen(string id)
        {
            if (!Data.seenCutscenes.Contains(id)) Data.seenCutscenes.Add(id);
            Save();
        }

        // ------------------------------------------------------------------ travel
        public void Travel(LocationDef loc)
        {
            if (loc == null || loc.id == Data.location || !LocationUnlocked(loc)
                || travelPending || ScreenFader.Busy || AdManager.IsPresenting) return;
            travelPending = true;
            AdManager.Instance.AtTravelBreak(() =>
            {
                Data.location = loc.id;
                Save();
                ScreenFader.Transition(() =>
                {
                    try { SceneManager.LoadScene(loc.sceneName); }
                    finally { travelPending = false; }
                }, 0.5f, 0.2f);
            });
        }

        public void ContinueGame()
        {
            var loc = Db.Location(Data.location) ?? Db.locations.FirstOrDefault();
            ScreenFader.Transition(() => SceneManager.LoadScene(loc.sceneName), 0.5f, 0.2f);
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
