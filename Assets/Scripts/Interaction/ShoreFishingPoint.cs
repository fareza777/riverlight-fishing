using CozyFishing.Core;
using CozyFishing.Fishing;
using CozyFishing.Player;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>The end of the dock: "Fish here" stands the hero facing the water and fishes without the boat.</summary>
    public sealed class ShoreFishingPoint : Interactable
    {
        [SerializeField] Transform stand;
        [SerializeField] FishingController fishing;
        [SerializeField] FishingSpot spot;
        [SerializeField] PlayerModes modes;

        public Transform Stand => stand;
        public FishingController Fishing => fishing;
        public FishingSpot Spot => spot;

        public override string Prompt => Loc.T("fish_here");

        public override bool CanInteract(PlayerInteractor who) => modes != null && !modes.InBoat && !modes.OnShore;

        public override void Interact(PlayerInteractor who) => modes.StartShore(this);

#if UNITY_EDITOR
        public void EditorSetup(Transform standPoint, FishingController f, FishingSpot s, PlayerModes m)
        {
            stand = standPoint; fishing = f; spot = s; modes = m;
        }
#endif
    }
}
