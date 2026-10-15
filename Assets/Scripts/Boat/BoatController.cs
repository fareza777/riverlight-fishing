using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Boat
{
    /// <summary>
    /// Physics rowboat: eased thrust, keel (lateral) resistance, water drag relative to the river current,
    /// and a purely visual bob / pitch / roll layer so motion always feels soft.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BoatController : MonoBehaviour
    {
        [Header("Rowing")]
        [Tooltip("Stroke engine: thrust pulses with each drive so the boat surges in time with the oars.")]
        [SerializeField] RowingAnimator rowing;
        [SerializeField] float forwardThrust = 3.2f;
        [SerializeField] float reverseThrust = 1.6f;
        [SerializeField] float turnAcceleration = 1.9f;
        [SerializeField] float maxSpeed = 3.6f;
        [SerializeField] float inputEaseTime = 0.35f;

        [Header("Water")]
        [SerializeField] float waterDrag = 0.55f;
        [SerializeField] float keelGrip = 2.8f;
        [SerializeField] float turnDrag = 2.4f;
        [SerializeField] Vector3 riverCurrent = new(0f, 0f, -0.18f);
        [SerializeField] float calmZoneStartZ = 22f;
        [SerializeField] float waterLevel;
        [SerializeField] float draft = 0.2f;

        [Header("Visual motion")]
        [SerializeField] Transform visual;
        [SerializeField] float bobAmplitude = 0.035f;
        [SerializeField] float bobFrequency = 0.55f;
        [SerializeField] float pitchFromAccel = 2.2f;
        [SerializeField] float rollFromTurn = 5f;

        Rigidbody body;
        Vector2 easedInput;
        Vector2 inputVelocity;
        Vector3 lastVelocity;
        float smoothedPitch;
        float smoothedRoll;

        public bool InputLocked { get; set; }

        /// <summary>While fishing: slowly yaw the boat so the rower (facing the stern) looks at this point.</summary>
        public Vector3? AimTarget { get; set; }
        [SerializeField] float aimTurnRate = 0.9f;

        /// <summary>Tied to the dock: held in place (kinematic) until the player boards.</summary>
        public bool Moored
        {
            get => body != null && body.isKinematic;
            set
            {
                if (body == null) body = GetComponent<Rigidbody>();
                if (!value && body.isKinematic) body.isKinematic = false;
                if (value)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
            }
        }
        public float RowIntensity { get; private set; }
        public float ForwardInput => easedInput.y;
        public float TurnInput => easedInput.x;
        public Vector3 Velocity => body.linearVelocity;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            var p = body.position;
            body.position = new Vector3(p.x, waterLevel - draft, p.z);
        }

        void FixedUpdate()
        {
            if (body.isKinematic) return;
            float dt = Time.fixedDeltaTime;
            Vector2 raw = InputLocked || GameInput.Instance == null ? Vector2.zero : Vector2.ClampMagnitude(GameInput.Instance.Move, 1f);
            easedInput = Vector2.SmoothDamp(easedInput, raw, ref inputVelocity, inputEaseTime, Mathf.Infinity, dt);
            RowIntensity = Mathf.Clamp01(Mathf.Max(Mathf.Abs(easedInput.y), Mathf.Abs(easedInput.x) * 0.8f));

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            float thrust = easedInput.y >= 0f ? forwardThrust : reverseThrust;
            float pulse = rowing != null ? 0.35f + 2.4f * rowing.DriveFactor : 1f;
            body.AddForce(forward * (easedInput.y * thrust * pulse), ForceMode.Acceleration);

            // Turning works while stationary (one oar pulls, the other pushes).
            body.AddTorque(Vector3.up * (easedInput.x * turnAcceleration), ForceMode.Acceleration);
            if (AimTarget.HasValue)
            {
                Vector3 toTarget = AimTarget.Value - body.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.25f)
                {
                    Vector3 facing = -Vector3.ProjectOnPlane(transform.forward, Vector3.up);   // rower faces the stern
                    float angle = Vector3.SignedAngle(facing, toTarget, Vector3.up) * Mathf.Deg2Rad;
                    Vector3 av = body.angularVelocity;
                    av.y = Mathf.Lerp(av.y, Mathf.Clamp(angle * 1.6f, -aimTurnRate, aimTurnRate), dt * 4f);
                    body.angularVelocity = av;
                }
            }
            Vector3 angular = body.angularVelocity;
            angular.y -= angular.y * turnDrag * dt;
            body.angularVelocity = angular;

            // Water drag pulls velocity towards the local current; the keel kills sideways slip.
            Vector3 current = CurrentAt(body.position);
            Vector3 v = body.linearVelocity;
            v += (current - v) * (waterDrag * dt);
            Vector3 local = transform.InverseTransformDirection(v - current);
            local.x -= local.x * Mathf.Clamp01(keelGrip * dt);
            v = transform.TransformDirection(local) + current;
            v.y = 0f;
            v = Vector3.ClampMagnitude(v, maxSpeed);
            // Stay inside the map: the ends of the river used to let the boat row off-screen into the void.
            Vector3 pos = body.position;
            if (World.PlayArea.Contain(ref pos, ref v, 3f)) body.position = pos;
            body.linearVelocity = v;
        }

        Vector3 CurrentAt(Vector3 position)
        {
            float calm = Mathf.InverseLerp(calmZoneStartZ - 6f, calmZoneStartZ, position.z);
            return riverCurrent * (1f - calm);
        }

        void LateUpdate()
        {
            if (visual == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 v = body.isKinematic ? Vector3.zero : body.linearVelocity;
            Vector3 accel = (v - lastVelocity) / dt;
            lastVelocity = v;

            float forwardAccel = Vector3.Dot(accel, transform.forward);
            float targetPitch = Mathf.Clamp(-forwardAccel * pitchFromAccel, -4f, 4f);
            float targetRoll = Mathf.Clamp(-body.angularVelocity.y * rollFromTurn * (0.3f + v.magnitude * 0.25f), -6f, 6f);
            smoothedPitch = Mathf.Lerp(smoothedPitch, targetPitch, 1f - Mathf.Exp(-3f * dt));
            smoothedRoll = Mathf.Lerp(smoothedRoll, targetRoll, 1f - Mathf.Exp(-2.5f * dt));

            float t = Time.time * bobFrequency * Mathf.PI * 2f;
            float bob = Mathf.Sin(t) * bobAmplitude + Mathf.Sin(t * 1.7f + 1.3f) * bobAmplitude * 0.4f;
            float idlePitch = Mathf.Sin(t * 0.8f + 0.4f) * 0.8f;
            float idleRoll = Mathf.Sin(t * 0.63f) * 1.2f;

            visual.localPosition = new Vector3(0f, bob, 0f);
            visual.localRotation = Quaternion.Euler(smoothedPitch + idlePitch, 0f, smoothedRoll + idleRoll);
        }
    }
}
