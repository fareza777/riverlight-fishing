using CozyFishing.Boat;
using CozyFishing.Fishing;
using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>
    /// Bridges gameplay state to the Meshy humanoid's Animator (the oars follow the hands, see RowingAnimator).
    /// Lives on the character model (Animator object).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class PlayerAvatar : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int SeatedId = Animator.StringToHash("Seated");
        static readonly int RowingId = Animator.StringToHash("Rowing");
        static readonly int RowSpeedId = Animator.StringToHash("RowSpeed");
        static readonly int FishingId = Animator.StringToHash("Fishing");
        static readonly int ReelSpeedId = Animator.StringToHash("ReelSpeed");
        static readonly int CastTimeId = Animator.StringToHash("CastTime");
        static readonly int CastId = Animator.StringToHash("Cast");
        static readonly int RowStateId = Animator.StringToHash("Rowing");

        [SerializeField] Transform rodSocketOffset;

        Animator animator;
        PlayerController walker;
        BoatController boat;
        FishingController fishing;
        RowingAnimator oars;
        float rowLayer;
        [Tooltip("Weight of the text-to-motion Row clip layer; 0 = rowing pose comes from RowerBody (procedural, synced to the oars).")]
        [SerializeField, Range(0f, 1f)] float rowStrokeWeight = 0f;
        Vector3 seatOffset;
        FishingState lastFishing = FishingState.Rowing;

        public Animator Animator => animator;
        public bool InBoat { get; private set; }
        public Transform RightHand => animator.GetBoneTransform(HumanBodyBones.RightHand);

        /// <summary>Hips height above the seat surface while sitting.</summary>
        public float SeatHipHeight { get; set; } = 0.12f;
        public float SeatSurfaceY { get; set; } = 0.42f;
        /// <summary>Hips this far behind the thwart centre (toward the bow, i.e. the rower's back), in seat space.</summary>
        public float SeatHipBack { get; set; } = -0.04f;

        void Awake()
        {
            animator = GetComponent<Animator>();
            animator.applyRootMotion = false;
            walker = GetComponentInParent<PlayerController>();
        }

        public void EnterBoat(BoatController b, FishingController f, RowingAnimator r)
        {
            boat = b; fishing = f; oars = r; InBoat = true;
            animator.SetBool(SeatedId, true);
        }

        public void LeaveBoat()
        {
            InBoat = false;
            animator.SetBool(SeatedId, false);
            animator.SetBool(RowingId, false);
            animator.SetBool(FishingId, false);
            animator.SetLayerWeight(1, 0f);
            rowLayer = 0f;
            transform.localPosition = Vector3.zero;
            seatOffset = Vector3.zero;
        }

        void Update()
        {
            if (!InBoat)
            {
                animator.SetFloat(SpeedId, walker ? walker.AnimSpeed : 0f);
                return;
            }

            var state = fishing ? fishing.State : FishingState.Rowing;
            bool isFishing = state != FishingState.Rowing;
            // The cast clip starts once the rod is in hand (after Readying), not the moment fishing begins.
            if (state == FishingState.Charging && lastFishing != FishingState.Charging) animator.SetTrigger(CastId);
            lastFishing = state;

            animator.SetBool(FishingId, isFishing && state != FishingState.Landing);
            float intensity = boat ? boat.RowIntensity : 0f;
            animator.SetBool(RowingId, !isFishing && intensity > 0.06f);
            float dir = boat && boat.ForwardInput < -0.1f ? -1f : 1f;
            animator.SetFloat(RowSpeedId, Mathf.Lerp(0.7f, 1.25f, intensity) * dir);

            float castTime = state switch
            {
                FishingState.Readying => 0.02f,
                FishingState.Charging => Mathf.Lerp(0.05f, 0.33f, fishing.Charge),
                FishingState.Casting => Mathf.Lerp(0.33f, 1f, fishing.CastProgress),
                _ => 1f,
            };
            animator.SetFloat(CastTimeId, castTime);
            float reel = state == FishingState.Reeling ? (Core.GameInput.Instance != null && Core.GameInput.Instance.ActionHeld ? 1.7f : 0.25f) : state == FishingState.Bite ? 0.6f : 0f;
            animator.SetFloat(ReelSpeedId, reel);

            // Upper-body stroke layer fades in while rowing.
            bool rowingNow = !isFishing && intensity > 0.06f;
            rowLayer = Mathf.MoveTowards(rowLayer, rowingNow ? rowStrokeWeight : 0f, Time.deltaTime * 2.5f);
            animator.SetLayerWeight(1, rowLayer);

        }

        void OnAnimatorIK(int layer)
        {
            // Reach down for the rod lying in the boat while getting ready to fish.
            if (!InBoat || fishing == null || layer != 0) return;
            float w = fishing.State == FishingState.Readying ? fishing.PickupReach : 0f;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, w);
            if (w > 0f && fishing.StowedRod != null) animator.SetIKPosition(AvatarIKGoal.RightHand, fishing.StowedRod.position);
            animator.SetLookAtWeight(fishing.State == FishingState.Rowing ? 0f : 0.6f, 0.2f, 0.7f);
            animator.SetLookAtPosition(fishing.State is FishingState.Waiting or FishingState.Bite or FishingState.Reeling && fishing.Bobber != null
                ? fishing.Bobber.position : fishing.AimPoint);
        }

        void LateUpdate()
        {
            if (!InBoat) return;
            // Put the hips on the centre of the thwart whatever the clip does: Meshy's sit clips sit on an invisible
            // chair with the pelvis well behind and beside the root, which pushed the legs through the hull.
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null || transform.parent == null) return;
            Vector3 hipsRaw = transform.parent.InverseTransformPoint(hips.position) - seatOffset;
            Vector3 wanted = new Vector3(0f, SeatSurfaceY + SeatHipHeight, SeatHipBack) - hipsRaw;
            seatOffset = Vector3.Lerp(seatOffset, wanted, 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.localPosition = seatOffset;
        }

    }
}
