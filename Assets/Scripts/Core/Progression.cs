using System;
using System.Collections.Generic;
using System.Linq;
using CozyFishing.Data;
using CozyFishing.Fishing;
using UnityEngine;

namespace CozyFishing.Core
{
    [Serializable]
    public sealed class MiraOrder
    {
        public string fishId;
        public int count;
        public int reward;
    }

    /// <summary>
    /// Longer-term goals on top of the story:
    ///  - each chapter's legend only bites once the player knows that water (N species found there);
    ///  - Mira posts two fish orders a day that pay well;
    ///  - collection rewards for 6 and 12 species per location and for all 48.
    /// </summary>
    public static class Progression
    {
        /// <summary>Per chapter: the location to learn and how many of its species must be in the journal.</summary>
        public static readonly (string location, int species)[] ChapterNeeds =
        {
            (null, 0), ("river", 5), ("lake", 5), ("gorge", 5), ("estuary", 5), ("river", 9),
        };

        public static readonly (int species, int coins)[] CollectionTiers = { (6, 150), (12, 500) };
        public const int MasterReward = 1500;
        public const int OrdersPerDay = 2;

        static SaveData Data => GameState.Instance.Data;
        static GameDatabase Db => GameState.Db;

        // ------------------------------------------------------------------ chapter requirements
        public static int Found(string location) =>
            Data.journal.Count(j => Db.Fish(j.fishId) is FishSpecies f && f.Location == location && !f.IsLegend);

        public static bool LegendReady(int chapter)
        {
            if (chapter <= 0 || chapter >= ChapterNeeds.Length) return true;
            var (location, need) = ChapterNeeds[chapter];
            return Found(location) >= need;
        }

        /// <summary>"Get to know Willow Lake first: 3/5 kinds found" — empty once the legend can bite.</summary>
        public static string NeedText(int chapter)
        {
            if (LegendReady(chapter)) return "";
            var (location, need) = ChapterNeeds[chapter];
            return Loc.T("obj_progress", Found(location), need, Db.Location(location)?.Name ?? location);
        }

        /// <summary>True when this catch is the one that met the current chapter's requirement.</summary>
        public static bool JustBecameReady(FishSpecies caught, bool isNew)
        {
            int chapter = Data.chapter;
            if (!isNew || caught.IsLegend || chapter <= 0 || chapter >= ChapterNeeds.Length) return false;
            var (location, need) = ChapterNeeds[chapter];
            return caught.Location == location && Found(location) == need;
        }

        // ------------------------------------------------------------------ collection rewards
        /// <summary>Pays out any collection tiers reached; returns the messages to show.</summary>
        public static List<string> ClaimCollections()
        {
            var messages = new List<string>();
            foreach (var loc in Db.locations)
            {
                int found = Found(loc.id);
                foreach (var (species, coins) in CollectionTiers)
                {
                    string key = $"{loc.id}_{species}";
                    if (found < species || Data.claimed.Contains(key)) continue;
                    Data.claimed.Add(key);
                    Data.coins += coins;
                    messages.Add(Loc.T("collection_reward", loc.Name, found, 12, coins));
                }
            }
            if (Data.journal.Count >= Db.fish.Count && !Data.claimed.Contains("all"))
            {
                Data.claimed.Add("all");
                Data.coins += MasterReward;
                messages.Add(Loc.T("master_angler", MasterReward));
            }
            if (messages.Count > 0) { GameState.Instance.Save(); GameState.Instance.NotifyChanged(); }
            return messages;
        }

        // ------------------------------------------------------------------ Mira's orders
        /// <summary>Tops the order list up once per in-game day. Returns true when new orders arrived.</summary>
        public static bool RefreshOrders()
        {
            if (Data.ordersDay == Data.day && Data.orders.Count > 0) return false;
            bool added = false;
            Data.ordersDay = Data.day;
            while (Data.orders.Count < OrdersPerDay)
            {
                var order = NewOrder();
                if (order == null) break;
                Data.orders.Add(order);
                added = true;
            }
            if (added) GameState.Instance.Save();
            return added;
        }

        static MiraOrder NewOrder()
        {
            var pool = Db.fish.Where(f => f != null && !f.IsLegend && GameState.Instance.LocationUnlocked(Db.Location(f.Location))
                                          && Data.orders.All(o => o.fishId != f.Id)).ToList();
            if (pool.Count == 0) return null;
            float Weight(FishSpecies f) => f.Rarity switch { FishRarity.Common => 6f, FishRarity.Uncommon => 3f, _ => 1f };
            float roll = UnityEngine.Random.value * pool.Sum(Weight);
            var pick = pool.First(f => (roll -= Weight(f)) <= 0f || f == pool[^1]);
            int count = pick.Rarity switch
            {
                FishRarity.Common => UnityEngine.Random.Range(2, 5),
                FishRarity.Uncommon => UnityEngine.Random.Range(1, 3),
                _ => 1,
            };
            float typical = pick.PriceFor((pick.SizeRangeCm.x + pick.SizeRangeCm.y) * 0.5f);
            int reward = Mathf.RoundToInt(typical * count * 2.2f / 5f) * 5 + (pick.Rarity == FishRarity.Rare ? 100 : 0);
            return new MiraOrder { fishId = pick.Id, count = count, reward = reward };
        }

        public static int InCreel(string fishId) => Data.creel.Count(c => c.fishId == fishId);

        public static bool CanDeliver(MiraOrder o) => InCreel(o.fishId) >= o.count;

        public static void Deliver(MiraOrder o)
        {
            if (!CanDeliver(o)) return;
            // Hand over the smallest fish first; the big ones are worth more at the market.
            var give = Data.creel.Where(c => c.fishId == o.fishId).OrderBy(c => c.sizeCm).Take(o.count).ToList();
            foreach (var c in give) Data.creel.Remove(c);
            Data.coins += o.reward;
            Data.totalEarned += o.reward;
            Data.orders.Remove(o);
            GameState.Instance.Save();
            GameState.Instance.NotifyChanged();
        }
    }
}
