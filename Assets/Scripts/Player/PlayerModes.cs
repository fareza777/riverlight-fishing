using System.Collections;
using CozyFishing.Boat;
using CozyFishing.CameraSystem;
using CozyFishing.Core;
using CozyFishing.Fishing;
using CozyFishing.Interaction;
using CozyFishing.UI;
using CozyFishing.World;
using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>
    /// Switches the player between walking and sitting in the boat: hands control and the camera over,
    /// moves the character onto the rowing seat (and back onto the dock), and owns the context-button prompt while boating.
    /// </summary>
    public sealed class PlayerModes : MonoBehaviour
    {
        [SerializeField] PlayerController walker;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] PlayerAvatar avatar;
        [SerializeField] BoatController boat;
        [SerializeField] FishingController fishing;
        [SerializeField] RowingAnimator oars;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] Transform seat;
        [SerializeField] Transform dockStandPoint;
        [SerializeField] float transferSeconds = 0.55f;

        bool nearDock;
        bool busy;

        public bool InBoat { get; private set; }
        public PlayerAvatar Avatar => avatar;

        /// <summary>Used by <see cref="CharacterSelector"/> to swap in the chosen hero before play starts.</summary>
        public void SetAvatar(PlayerAvatar a) => avatar = a;

        void Start()
        {
            boat.InputLocked = true;
            boat.Moored = true;
            fishing.enabled = false;
            cameraRig.SetTarget(walker.transform, null);
            cameraRig.Snap();
            // Walking into the moored boat shoved it and let the player climb onto the hull; boarding is a button.
            foreach (var c in boat.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(walker.Body, c);
        }

        public void Board()
        {
            if (busy || InBoat) return;
            StartCoroutine(BoardRoutine());
        }

        IEnumerator BoardRoutine()
        {
            busy = true;
            interactor.Enabled = false;
            walker.InputLocked = true;
            walker.Stop();
            walker.Body.enabled = false;
            avatar.EnterBoat(boat, fishing, oars);
            oars.Rower = avatar.Animator;
            if (avatar.TryGetComponent<RowerBody>(out var rowerBody)) rowerBody.Bind(oars);

            Transform t = walker.transform;
            Vector3 fromP = t.position;
            Quaternion fromR = t.rotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / transferSeconds)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                Vector3 p = Vector3.Lerp(fromP, seat.position, e);
                p.y += Mathf.Sin(e * Mathf.PI) * 0.35f;   // small hop into the boat
                t.SetPositionAndRotation(p, Quaternion.Slerp(fromR, seat.rotation, e));
                yield return null;
            }
            t.SetParent(seat, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;

            InBoat = true;
            boat.Moored = false;
            boat.InputLocked = false;
            fishing.enabled = true;
            cameraRig.SetTarget(boat.transform, boat.GetComponent<Rigidbody>());
            busy = false;
        }

        public void Disembark()
        {
            if (busy || !InBoat) return;
            StartCoroutine(DisembarkRoutine());
        }

        IEnumerator DisembarkRoutine()
        {
            busy = true;
            fishing.enabled = false;
            boat.InputLocked = true;
            boat.Moored = true;
            InBoat = false;
            Transform t = walker.transform;
            t.SetParent(null, true);
            avatar.LeaveBoat();
            oars.Rower = null;

            Vector3 fromP = t.position;
            Quaternion fromR = t.rotation;
            Quaternion toR = dockStandPoint.rotation;
            for (float k = 0f; k < 1f; k += Time.deltaTime / transferSeconds)
            {
                float e = Mathf.SmoothStep(0f, 1f, k);
                Vector3 p = Vector3.Lerp(fromP, dockStandPoint.position, e);
                p.y += Mathf.Sin(e * Mathf.PI) * 0.35f;
                t.SetPositionAndRotation(p, Quaternion.Slerp(fromR, toR, e));
                yield return null;
            }
            walker.Teleport(dockStandPoint.position, toR);
            walker.Body.enabled = true;
            walker.InputLocked = false;
            interactor.Enabled = true;
            cameraRig.SetTarget(walker.transform, null);
            busy = false;
        }

        void Update()
        {
            if (!InBoat || busy) return;

            // Context button while boating: fishing actions first, then getting off at the dock.
            bool canLeave = nearDock && fishing.State == FishingState.Rowing && !fishing.CanCast;
            fishing.CastAllowed = true;
            string prompt = fishing.State switch
            {
                FishingState.Rowing when fishing.CanCast => Loc.T("cast"),
                FishingState.Rowing when canLeave => Loc.T("leave"),
                FishingState.Charging => Loc.T("release"),
                FishingState.Bite => Loc.T("hook"),
                FishingState.Reeling => Loc.T("reel"),
                FishingState.Landing => Loc.T("continue"),
                _ => "",
            };
            TouchControls.Publish(prompt, walker.transform.position + Vector3.up * 1.9f);

            if (canLeave && GameInput.Instance != null && GameInput.Instance.ActionPressed) Disembark();
        }

        // Boat trigger callbacks are forwarded by BoatDockSensor on the boat.
        public void SetNearDock(bool value) => nearDock = value;
    }
}
