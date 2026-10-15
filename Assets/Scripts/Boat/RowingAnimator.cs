using UnityEngine;

namespace CozyFishing.Boat
{
    /// <summary>
    /// The rowing stroke engine. One phase drives the oars, the rower's lean (<see cref="Lean"/>) and the boat's
    /// thrust pulses (<see cref="DriveFactor"/>), so oars, body and boat always agree.
    /// Stroke (rower faces the stern): catch -> drive (blades in the water, pulled toward the bow) -> finish (blades
    /// lift) -> recovery (blades feathered flat, carried back above the water).
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class RowingAnimator : MonoBehaviour
    {
        [SerializeField] BoatController boat;
        [SerializeField] Transform oarLeft;
        [SerializeField] Transform oarRight;
        [SerializeField] float sweepAngle = 34f;
        [SerializeField] float bladeDepth = 4f;
        [SerializeField] float bladeLift = 9f;
        [SerializeField] float featherAngle = 80f;
        [SerializeField] float strokesPerSecondMin = 0.55f;
        [SerializeField] float strokesPerSecondMax = 0.85f;
        [Range(0.3f, 0.6f)] [SerializeField] float driveFraction = 0.42f;
        [Header("Blade height relative to the water surface (world Y)")]
        [SerializeField] float waterLevel;
        [SerializeField] float bladeDepthInWater = 0.15f;
        [SerializeField] float bladeClearance = 0.12f;
        [SerializeField] float idleClearance = 0.1f;

        sealed class Oar
        {
            public Transform transform;
            public Quaternion rest;
            public Quaternion shipped;
            public Quaternion current;
            public Vector3 shaftLocal;      // pivot -> blade, oar space
            public float sweepSign = 1f;    // + moves the handle toward the stern
            public float dipSign = 1f;      // + lifts the handle (puts the blade in the water)
            public float side;              // -1 port, +1 starboard (boat space)
            public Transform grip;
            public Vector3 tipLocal;        // blade tip in the oar's local space
        }

        Oar left, right;
        float phase = 0.5f;     // 0 = catch; starts at "finish" (blades out, handles near the body)
        float activity;         // 0 idle .. 1 rowing
        float turnBias;

        public Animator Rower { get; set; }
        public bool Resting { get; set; }
        public Transform GripLeft => left?.grip;
        public Transform GripRight => right?.grip;
        /// <summary>Rower lean in degrees: + forward (toward the stern) at the catch, - back at the finish.</summary>
        public float Lean { get; private set; }
        /// <summary>0..1, peaks mid-drive; scales the boat's thrust so it surges with each stroke.</summary>
        public float DriveFactor { get; private set; }
        /// <summary>True while hands should hold the grips (rowing or idle with oars out).</summary>
        public bool HandsOnOars => Rower != null && !Resting;

        void Awake()
        {
            left = Make(oarLeft);
            right = Make(oarRight);
        }

        /// <summary>
        /// Editor-time: finds the oar's real handle end and blade tip from its vertices (only readable in the editor)
        /// and stores them as "Grip" and "BladeTip" children on the shaft axis, so player builds never read mesh data.
        /// The shaft passes through the oar's origin (the oarlock pivot).
        /// </summary>
        public static void BakeOarPoints(Transform oar, Transform boat)
        {
            var mf = oar.GetComponentInChildren<MeshFilter>();
            var verts = mf.sharedMesh.vertices;
            Vector3 handle = Vector3.zero;
            float nearest = float.MaxValue;
            foreach (var v in verts)
            {
                Vector3 w = mf.transform.TransformPoint(v);
                float d = Mathf.Abs(boat.InverseTransformPoint(w).x);
                if (d < nearest) { nearest = d; handle = oar.InverseTransformPoint(w); }
            }
            Vector3 dir = (-handle).normalized;                       // outboard along the shaft
            float tip = float.MinValue;
            foreach (var v in verts) tip = Mathf.Max(tip, Vector3.Dot(oar.InverseTransformPoint(mf.transform.TransformPoint(v)), dir));
            float handleEnd = Vector3.Dot(handle, dir);                // negative: inboard of the pivot
            Point(oar, "Grip", dir * (handleEnd + 0.2f), dir);
            Point(oar, "BladeTip", dir * tip, dir);
        }

        static void Point(Transform parent, string name, Vector3 local, Vector3 dir)
        {
            var t = parent.Find(name);
            if (t == null) { t = new GameObject(name).transform; t.SetParent(parent, false); }
            t.localPosition = local;
            t.localRotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        Oar Make(Transform t)
        {
            if (t == null) return null;
            if (t.Find("Grip") == null || t.Find("BladeTip") == null)
            {
                if (Application.isEditor) BakeOarPoints(t, transform);
                else { Debug.LogError($"[RowingAnimator] {t.name} has no baked Grip/BladeTip; rebuild the scene."); return null; }
            }
            Transform grip = t.Find("Grip");
            Vector3 handleW = grip.position;
            Vector3 bladeW = t.Find("BladeTip").position;
            var oar = new Oar
            {
                transform = t, rest = t.localRotation, current = t.localRotation, grip = grip,
                shaftLocal = t.InverseTransformDirection(bladeW - handleW).normalized,
                side = Mathf.Sign(transform.InverseTransformPoint(t.position).x),
                tipLocal = t.InverseTransformPoint(bladeW),
            };
            // Work out which way positive sweep/dip move the handle, whatever the oar's export axes are.
            Vector3 gripLocalBoat0 = GripInBoat(oar, oar.rest);
            Vector3 swept = GripInBoat(oar, Quaternion.Euler(0f, 10f, 0f) * oar.rest);
            oar.sweepSign = swept.z < gripLocalBoat0.z ? 1f : -1f;
            Vector3 dipped = GripInBoat(oar, oar.rest * Quaternion.AngleAxis(10f, Vector3.Cross(oar.shaftLocal, Vector3.up)));
            oar.dipSign = dipped.y > gripLocalBoat0.y ? 1f : -1f;

            // Shipped: shaft swung to lie along the hull (handle toward the bow), slightly raised.
            Vector3 handleDirParent = -(oar.rest * oar.shaftLocal);
            Vector3 boatFwd = t.parent.InverseTransformDirection(transform.forward);
            oar.shipped = Quaternion.FromToRotation(handleDirParent, (boatFwd * 0.96f + Vector3.up * 0.28f).normalized) * oar.rest;
            return oar;
        }

        Vector3 GripInBoat(Oar oar, Quaternion localRot)
        {
            Vector3 local = oar.transform.InverseTransformPoint(oar.grip.position);
            Vector3 parentSpace = oar.transform.localPosition + localRot * local;
            return transform.InverseTransformPoint(oar.transform.parent.TransformPoint(parentSpace));
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float intensity = Resting || boat == null ? 0f : boat.RowIntensity;
            activity = Mathf.MoveTowards(activity, intensity > 0.05f ? 1f : 0f, dt * 1.5f);
            turnBias = boat != null ? Mathf.Clamp(boat.TurnInput, -1f, 1f) : 0f;

            // Advance the stroke while rowing; when the input stops, finish the stroke and park at the finish.
            float rate = Mathf.Lerp(strokesPerSecondMin, strokesPerSecondMax, intensity);
            bool parked = activity <= 0f && Mathf.Abs(Mathf.DeltaAngle(phase * 360f, driveFraction * 360f + 18f)) < 8f;
            if (!parked)
            {
                float dir = boat != null && boat.ForwardInput < -0.1f ? -1f : 1f;
                phase = Mathf.Repeat(phase + dt * rate * (activity > 0f ? dir : 1f), 1f);
            }

            float drive = phase < driveFraction ? phase / driveFraction : 0f;
            DriveFactor = drive > 0f ? Mathf.Sin(drive * Mathf.PI) * activity : 0f;
        }

        /// <summary>Stroke curve for phase p: handle sweep (-1 at the finish .. +1 at the catch), blade in water (0/1), feather (0..1).</summary>
        void Stroke(float p, out float sweep, out float inWater, out float feather)
        {
            if (p < driveFraction)
            {
                float k = p / driveFraction;                                   // drive: catch -> finish
                sweep = Mathf.Cos(k * Mathf.PI);                               // +1 -> -1
                inWater = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 8f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - k) * 8f));
                feather = 0f;
            }
            else
            {
                float k = (p - driveFraction) / (1f - driveFraction);         // recovery: finish -> catch
                sweep = -Mathf.Cos(k * Mathf.PI);                              // -1 -> +1
                inWater = 0f;
                feather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k * 5f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - k) * 5f));
            }
        }

        void LateUpdate()
        {
            Stroke(phase, out float sweep, out float inWater, out float feather);
            Lean = sweep * 14f * Mathf.Max(activity, 0.25f);
            Pose(left, sweep, inWater, feather);
            Pose(right, sweep, inWater, feather);
        }

        void Pose(Oar oar, float sweep, float inWater, float feather)
        {
            if (oar == null) return;
            Quaternion target;
            if (Resting && Rower != null) target = oar.shipped;
            else if (Rower == null) target = oar.rest;
            else
            {
                // Both oars are always identical (same rhythm, same reach). Steering is done by the boat's yaw only.
                float a = activity;
                float yaw = sweep * sweepAngle * oar.sweepSign * Mathf.Lerp(0.15f, 1f, a);
                Quaternion swept = Quaternion.Euler(0f, yaw, 0f) * oar.rest;

                // Tilt so the blade tip sits at an exact height relative to the water: buried during the drive,
                // just clear during the recovery. Being water-relative, both blades match even when the boat rolls.
                Transform parent = oar.transform.parent;
                Vector3 pivotW = parent.TransformPoint(oar.transform.localPosition);
                Vector3 tipW = parent.TransformPoint(oar.transform.localPosition + swept * oar.tipLocal);
                float len = Vector3.Distance(tipW, pivotW);
                float wanted = waterLevel + Mathf.Lerp(idleClearance, Mathf.Lerp(bladeClearance, -bladeDepthInWater, inWater), a);
                float now = Mathf.Asin(Mathf.Clamp((tipW.y - pivotW.y) / len, -1f, 1f));
                float goal = Mathf.Asin(Mathf.Clamp((wanted - pivotW.y) / len, -1f, 1f));
                Vector3 dirW = (tipW - pivotW).normalized;
                Vector3 axisW = Vector3.Cross(dirW, Vector3.up).normalized;      // +angle raises the tip
                Quaternion tiltW = Quaternion.AngleAxis((goal - now) * Mathf.Rad2Deg, axisW);
                target = Quaternion.Inverse(parent.rotation) * tiltW * parent.rotation * swept
                         * Quaternion.AngleAxis(feather * featherAngle * a * oar.side, oar.shaftLocal);
            }
            oar.current = Quaternion.Slerp(oar.current, target, 1f - Mathf.Exp(-(Resting ? 5f : 25f) * Time.deltaTime));
            oar.transform.localRotation = oar.current;
        }
    }
}
