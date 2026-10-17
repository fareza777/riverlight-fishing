using CozyFishing.CameraSystem;
using CozyFishing.Core;
using CozyFishing.Player;
using CozyFishing.World;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>A door that moves the player between two areas (e.g. porch &lt;-&gt; cabin interior) behind a fade.</summary>
    public sealed class DoorPortal : Interactable
    {
        [SerializeField] Transform destination;
        [SerializeField] CameraZone destinationZone;
        [SerializeField] bool isExit;
        [SerializeField] AudioClip openSound;

        public override string Prompt => Loc.T(isExit ? "exit" : "enter");

        public override bool CanInteract(PlayerInteractor who) => destination != null && !ScreenFader.Busy;

        public override void Interact(PlayerInteractor who)
        {
            var walker = who.GetComponent<PlayerController>();
            var avatar = who.GetComponentInChildren<PlayerAvatar>();
            if (avatar != null) avatar.Animator.SetTrigger("OpenDoor");
            walker.InputLocked = true;
            ScreenFader.Transition(() =>
            {
                walker.Teleport(destination.position, destination.rotation);
                walker.Stop();
                // The door is behind us now: back to standing, or the rest of the door clip plays out as a slide.
                if (avatar != null)
                {
                    avatar.Animator.ResetTrigger("OpenDoor");
                    avatar.Animator.Play("Locomotion", 0, 0f);
                }
                var rig = FindAnyObjectByType<CameraRig>();
                if (rig != null)
                {
                    if (destinationZone != null) rig.SetZone(destinationZone);
                    rig.Snap();
                }
                walker.InputLocked = false;
            }, 0.4f, 0.2f);
        }
    }
}
