using CozyFishing.Boat;
using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>
    /// Makes the seated character sit and row properly: feet planted flat on the boat floor (leg IK, no clipping
    /// through the hull), spine leaning with the stroke, and both arms (two-bone IK) gripping the oar handles.
    /// Runs after the oars are posed and before the rod is placed.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public sealed class RowerBody : MonoBehaviour
    {
        [SerializeField] float blendSpeed = 4f;
        [Header("Feet on the boat floor (seat space: x = side, y = up, z = toward the stern)")]
        [SerializeField] float floorHeight = 0.115f;
        [SerializeField] float footSpread = 0.14f;
        [SerializeField] float footForward = 0.5f;

        Animator animator;
        RowingAnimator oars;
        float weight;
        float legWeight;
        PlayerAvatar avatar;
        Transform spine, chest, lUpper, lLower, lHand, rUpper, rLower, rHand;
        Transform lThigh, lShin, lFoot, rThigh, rShin, rFoot;
        Quaternion lFootRest, rFootRest;   // foot rotation relative to the character when standing (sole flat)
        // Upright, symmetric upper body (bind pose) used while rowing so idle sway/twist from the sit clip
        // can't make the two arms row differently.
        Transform[] torso;
        Quaternion[] torsoBind;
        float ankleHeight;

        void Awake()
        {
            animator = GetComponent<Animator>();
            spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            lUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            lLower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            lHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            rUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            rLower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            rHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            lThigh = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            lShin = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            lFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rThigh = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rShin = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            avatar = GetComponent<PlayerAvatar>();
            var torsoBones = new[] { HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest, HumanBodyBones.Neck,
                HumanBodyBones.Head, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder };
            var list = new System.Collections.Generic.List<Transform>();
            foreach (var hb in torsoBones) { var tb = animator.GetBoneTransform(hb); if (tb != null) list.Add(tb); }
            torso = list.ToArray();
            torsoBind = System.Array.ConvertAll(torso, x => x.localRotation);
            // Bind pose is standing: remember flat-foot orientation and ankle height above the sole.
            Quaternion inv = Quaternion.Inverse(transform.rotation);
            if (lFoot) lFootRest = inv * lFoot.rotation;
            if (rFoot) rFootRest = inv * rFoot.rotation;
            if (lFoot) ankleHeight = Mathf.Max(0.05f, lFoot.position.y - transform.position.y);
        }

        public void Bind(RowingAnimator rowing) => oars = rowing;

        void LateUpdate()
        {
            PlantFeet();
            bool active = oars != null && oars.HandsOnOars && oars.Rower == animator;
            weight = Mathf.MoveTowards(weight, active ? 1f : 0f, Time.deltaTime * blendSpeed);
            if (weight <= 0f || oars == null) return;

            // Square, symmetric shoulders and spine first (bind pose), then the stroke lean.
            for (int i = 0; i < torso.Length; i++)
                torso[i].localRotation = Quaternion.Slerp(torso[i].localRotation, torsoBind[i], weight);

            // Lean toward the stern (character forward) at the catch, back at the finish.
            Vector3 right = transform.right;
            float lean = oars.Lean * weight;
            if (spine) spine.rotation = Quaternion.AngleAxis(lean * 0.55f, right) * spine.rotation;
            if (chest) chest.rotation = Quaternion.AngleAxis(lean * 0.45f, right) * chest.rotation;

            // Each hand takes the grip on its own side of the body.
            Transform gripA = oars.GripLeft, gripB = oars.GripRight;
            if (gripA == null || gripB == null) return;
            bool aIsLeft = Vector3.Dot(gripA.position - transform.position, -right) > Vector3.Dot(gripB.position - transform.position, -right);
            Transform leftGrip = aIsLeft ? gripA : gripB;
            Transform rightGrip = aIsLeft ? gripB : gripA;
            Solve(lUpper, lLower, lHand, leftGrip.position, -right);
            Solve(rUpper, rLower, rHand, rightGrip.position, right);
        }

        void PlantFeet()
        {
            bool seated = avatar != null && avatar.InBoat;
            legWeight = Mathf.MoveTowards(legWeight, seated ? 1f : 0f, Time.deltaTime * blendSpeed * 1.5f);
            if (legWeight <= 0f) return;
            // Seat space is the player root the character sits under (parented to the boat's rowing seat).
            Transform seat = transform.parent != null ? transform.parent : transform;
            Vector3 up = seat.up, fwd = seat.forward, side = seat.right;
            Vector3 floor = seat.position + up * (floorHeight + ankleHeight) + fwd * footForward;
            Vector3 kneeHint = seat.position + up * 0.9f + fwd * 0.9f;
            PlaceLeg(lThigh, lShin, lFoot, floor - side * footSpread, kneeHint - side * footSpread * 1.6f, lFootRest);
            PlaceLeg(rThigh, rShin, rFoot, floor + side * footSpread, kneeHint + side * footSpread * 1.6f, rFootRest);
        }

        void PlaceLeg(Transform thigh, Transform shin, Transform foot, Vector3 target, Vector3 hint, Quaternion footRest)
        {
            if (thigh == null || shin == null || foot == null) return;
            SolveTo(thigh, shin, foot, target, hint, legWeight);
            // Sole flat on the floor, toes pointing where the character faces.
            foot.rotation = Quaternion.Slerp(foot.rotation, transform.rotation * footRest, legWeight);
        }

        /// <summary>Analytic two-bone IK: shoulder-elbow-hand reach for <paramref name="target"/>, elbow bends out and down.</summary>
        void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 outward)
        {
            Vector3 hintArm = upper != null ? upper.position + outward * 0.35f - Vector3.up * 0.35f - transform.forward * 0.1f : Vector3.zero;
            SolveTo(upper, lower, hand, target, hintArm, weight);
        }

        static void SolveTo(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hint, float w)
        {
            if (upper == null || lower == null || hand == null) return;
            Vector3 a = upper.position, b = lower.position, c = hand.position;
            float ab = (b - a).magnitude, bc = (c - b).magnitude;
            Vector3 t = Vector3.Lerp(c, target, w);
            Vector3 at = t - a;
            float len = Mathf.Clamp(at.magnitude, 0.05f, ab + bc - 0.002f);
            Vector3 dir = at.normalized;

            Vector3 n = Vector3.Cross(dir, hint - a);
            if (n.sqrMagnitude < 1e-6f) n = Vector3.Cross(dir, Vector3.up);
            Vector3 bend = Vector3.Cross(n.normalized, dir).normalized;
            float cosA = Mathf.Clamp((ab * ab + len * len - bc * bc) / (2f * ab * len), -1f, 1f);
            Vector3 elbow = a + dir * (ab * cosA) + bend * (ab * Mathf.Sqrt(1f - cosA * cosA));

            upper.rotation = Quaternion.FromToRotation(b - a, elbow - a) * upper.rotation;
            b = lower.position; c = hand.position;
            lower.rotation = Quaternion.FromToRotation(c - b, a + dir * len - b) * lower.rotation;
        }
    }
}
