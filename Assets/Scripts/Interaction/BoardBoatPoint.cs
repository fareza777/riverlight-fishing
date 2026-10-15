using CozyFishing.Core;
using CozyFishing.Player;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>Spot on the dock next to the moored boat: "Board". Only usable while the boat is actually close.</summary>
    public sealed class BoardBoatPoint : Interactable
    {
        [SerializeField] PlayerModes modes;
        [SerializeField] Transform boat;
        [SerializeField] float maxBoatDistance = 3.2f;

        public override string Prompt => Loc.T("board");

        public override bool CanInteract(PlayerInteractor who) =>
            modes != null && !modes.InBoat && boat != null && Vector3.Distance(boat.position, transform.position) < maxBoatDistance;

        public override void Interact(PlayerInteractor who) => modes.Board();
    }
}
