using System;
using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Fishing
{
    public enum FishRarity { Common, Uncommon, Rare, Legendary }

    [Flags]
    public enum Habitat { None = 0, River = 1, Shallows = 2, Pool = 4, Dock = 8, Lake = 16, Reeds = 32, Rapids = 64, Deep = 128, Sea = 256 }

    [CreateAssetMenu(menuName = "CozyFishing/Fish Species", fileName = "Fish_")]
    public sealed class FishSpecies : ScriptableObject
    {
        [SerializeField] string id;
        [SerializeField] string displayName = "Fish";
        [SerializeField] string nameID;
        [SerializeField] GameObject model;
        [Tooltip("Storybook illustration shown on the catch card and in the journal.")]
        [SerializeField] Sprite illustration;
        [SerializeField] FishRarity rarity = FishRarity.Common;
        [SerializeField] string location = "river";
        [SerializeField] Habitat habitats = Habitat.River;
        [Tooltip("Active from this hour (0-24). Equal start/end = all day. Wraps past midnight.")]
        [SerializeField, Range(0f, 24f)] float activeFrom;
        [SerializeField, Range(0f, 24f)] float activeTo;
        [SerializeField] Vector2 sizeRangeCm = new(18f, 32f);
        [Tooltip("How hard the fish fights, 0 = calm, 1 = wild.")]
        [SerializeField, Range(0f, 1f)] float fight = 0.3f;
        [SerializeField] int basePrice = 10;
        [Tooltip("Legends: minimum rod tier and the story chapter in which they can appear.")]
        [SerializeField] int requiredRodTier;
        [SerializeField] int chapter;
        [SerializeField, TextArea] string description;
        [SerializeField, TextArea] string descriptionID;
        [SerializeField] string hintEN, hintID;

        public string Id => string.IsNullOrEmpty(id) ? name : id;
        public string DisplayName => Loc.Current == Language.Indonesian && !string.IsNullOrEmpty(nameID) ? nameID : displayName;
        public GameObject Model => model;
        public Sprite Illustration => illustration;
        public FishRarity Rarity => rarity;
        public bool IsLegend => rarity == FishRarity.Legendary;
        public string Location => location;
        public Habitat Habitats => habitats;
        public Vector2 SizeRangeCm => sizeRangeCm;
        public float Fight => fight;
        public int RequiredRodTier => requiredRodTier;
        public int Chapter => chapter;
        public string Description => Loc.Current == Language.Indonesian && !string.IsNullOrEmpty(descriptionID) ? descriptionID : description;
        public string Hint => Loc.Current == Language.Indonesian ? hintID : hintEN;

        /// <summary>Coins for a fish of this size: bigger than average pays more.</summary>
        public int PriceFor(float sizeCm)
        {
            float t = Mathf.InverseLerp(sizeRangeCm.x, sizeRangeCm.y, sizeCm);
            return Mathf.RoundToInt(basePrice * Mathf.Lerp(0.8f, 1.5f, t));
        }

        public float Weight => rarity switch
        {
            FishRarity.Common => 10f,
            FishRarity.Uncommon => 4f,
            FishRarity.Rare => 1.5f,
            _ => 1f,
        };

        public bool IsActiveAt(float hour)
        {
            if (Mathf.Approximately(activeFrom, activeTo)) return true;
            return activeFrom < activeTo ? hour >= activeFrom && hour < activeTo : hour >= activeFrom || hour < activeTo;
        }

        public string RarityLabel => Loc.T("rarity_" + rarity.ToString().ToLowerInvariant());

#if UNITY_EDITOR
        [Serializable]
        public struct Spec
        {
            public string id, name, nameID, location, desc, descID, hintEN, hintID;
            public FishRarity rarity;
            public Habitat habitats;
            public float from, to, fight;
            public Vector2 size;
            public int price, rodTier, chapter;
        }

        public void EditorSetup(Spec s, GameObject fishModel, Sprite art)
        {
            id = s.id; displayName = s.name; nameID = s.nameID; location = s.location; model = fishModel; illustration = art;
            rarity = s.rarity; habitats = s.habitats; activeFrom = s.from; activeTo = s.to; sizeRangeCm = s.size; fight = s.fight;
            basePrice = s.price; requiredRodTier = s.rodTier; chapter = s.chapter;
            description = s.desc; descriptionID = s.descID; hintEN = s.hintEN; hintID = s.hintID;
        }
#endif
    }
}
