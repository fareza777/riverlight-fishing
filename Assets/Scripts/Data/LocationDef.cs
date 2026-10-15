using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Data
{
    /// <summary>A fishing location (its own scene). Unlocked by story chapter.</summary>
    [CreateAssetMenu(menuName = "CozyFishing/Location", fileName = "Loc_")]
    public sealed class LocationDef : ScriptableObject
    {
        public string id;
        public string sceneName;
        public string nameEN, nameID;
        [TextArea] public string descEN, descID;
        public int unlockChapter;
        public Sprite postcard;

        public string Name => Loc.Current == Language.Indonesian ? nameID : nameEN;
        public string Desc => Loc.Current == Language.Indonesian ? descID : descEN;
    }
}
