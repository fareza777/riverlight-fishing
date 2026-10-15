using System;
using System.Collections.Generic;

namespace CozyFishing.Core
{
    [Serializable]
    public sealed class JournalEntry
    {
        public string fishId;
        public int count;
        public float bestCm;
    }

    [Serializable]
    public sealed class CreelFish
    {
        public string fishId;
        public float sizeCm;
    }

    /// <summary>Everything persisted between sessions (JSON in persistentDataPath).</summary>
    [Serializable]
    public sealed class SaveData
    {
        public int version = 1;
        public string character = "FisherM";
        public string playerName = "Kai";
        public int coins;
        public int day = 1;
        public float hour = 7.5f;
        public string location = "river";
        public int chapter;                          // 0 prologue .. 5 finale, 6 = story complete
        public List<string> seenCutscenes = new();
        public List<JournalEntry> journal = new();
        public List<CreelFish> creel = new();
        public List<string> owned = new() { "rod_willow", "creel_small" };
        public string rod = "rod_willow";
        public string bait = "";
        public int baitCount;
        public string creelItem = "creel_small";
        public string outfit = "";                    // "" = the hero's default clothes
        public int totalCaught;
        public int totalEarned;
        public float playSeconds;
        public List<MiraOrder> orders = new();
        public int ordersDay;
        public List<string> claimed = new();          // collection rewards already paid ("lake_6", "all")
    }
}
