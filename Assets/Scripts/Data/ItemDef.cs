using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Data
{
    public enum ItemType { Rod, Bait, Creel }

    /// <summary>Something the player can buy and equip: rods (reel stats), bait (rare chance), creel upgrades (capacity).</summary>
    [CreateAssetMenu(menuName = "CozyFishing/Item", fileName = "Item_")]
    public sealed class ItemDef : ScriptableObject
    {
        public string id;
        public ItemType type;
        public string nameEN, nameID;
        [TextArea] public string descEN, descID;
        public int price;
        [Tooltip("Rod tier: legends need a minimum tier.")] public int tier;
        [Tooltip("Rod: widens the safe tension band.")] public float safeBandBonus;
        [Tooltip("Rod: faster reeling.")] public float reelBonus;
        [Tooltip("Bait: multiplies uncommon/rare bite weights.")] public float rareBonus;
        [Tooltip("Creel: total slots.")] public int capacity;
        public Sprite icon;

        public string Name => Loc.Current == Language.Indonesian ? nameID : nameEN;
        public string Desc => Loc.Current == Language.Indonesian ? descID : descEN;
    }
}
