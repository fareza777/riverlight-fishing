using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CozyFishing.CameraSystem
{
    /// <summary>
    /// Top-down follow camera: critically damped follow with velocity look-ahead,
    /// a gentle handheld drift, smooth zoom for fishing moments and a tilt-shift depth of field
    /// that keeps the boat in focus.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Rigidbody targetBody;
        [SerializeField] Camera cam;

        [Header("Framing")]
        [Tooltip("Horizontal field of view kept constant across aspect ratios (portrait phones stay wide enough).")]
        [SerializeField] float horizontalFov = 48f;
        [SerializeField] float footDistance = 11f;
        [SerializeField] float footPitch = 50f;
        [SerializeField] float distance = 17f;
        [SerializeField] float pitch = 52f;
        [SerializeField] float yaw;
        [SerializeField] float closeDistance = 10f;
        [SerializeField] float closePitch = 44f;

        [Header("Smoothing")]
        [SerializeField] float followTime = 0.45f;
        [SerializeField] float lookAheadSeconds = 1.1f;
        [SerializeField] float maxLookAhead = 4.5f;
        [SerializeField] float zoomTime = 0.8f;
        [SerializeField] float driftAmount = 0.12f;

        [Header("Bounds (world XZ)")]
        [SerializeField] Vector2 boundsMin = new(-26f, -52f);
        [SerializeField] Vector2 boundsMax = new(26f, 39f);

        [Header("Depth of field")]
        [SerializeField] Volume volume;

        Vector3 focus;
        Vector3 focusVelocity;
        Vector3 lookAhead;
        Vector3 lookAheadVelocity;
        float zoom;
        float zoomVelocity;
        DepthOfField dof;

        /// <summary>0 = exploring, 1 = close-up (casting / reeling).</summary>
        public float ZoomTarget { get; set; }
        /// <summary>Where the camera turns to (degrees about Y); the player can still swipe it round.</summary>
        public float YawTarget { get => yawTarget; set => yawTarget = value; }

        float baseDistance;
        float basePitch;
        float baseDistanceVelocity;

        /// <summary>Switch framing/bounds for an area (outdoors, cabin interior...).</summary>
        float yawTarget;
        float yawVelocity;
        float outdoorYaw;
        bool indoor;

        public void SetZone(World.CameraZone zone)
        {
            // Indoors the cutaway room must be seen from the front: remember the outdoor yaw and lock to 0.
            if (zone.Indoor && !indoor) outdoorYaw = yawTarget;
            yawTarget = zone.Indoor ? 0f : (indoor ? outdoorYaw : yawTarget);
            yaw = yawTarget;
            indoor = zone.Indoor;
            boundsMin = zone.BoundsMin;
            boundsMax = zone.BoundsMax;
            footDistance = zone.FootDistance;
            footPitch = zone.FootPitch;
        }

        public void SetTarget(Transform newTarget, Rigidbody body)
        {
            target = newTarget;
            targetBody = body;
            ZoomTarget = 0f;
        }

        void Start()
        {
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (target != null) focus = target.position;
            if (volume != null && volume.profile != null) volume.profile.TryGet(out dof);
            baseDistance = targetBody != null ? distance : footDistance;
            basePitch = targetBody != null ? pitch : footPitch;
            Apply(1f);
        }

        void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            Vector3 velocity = targetBody != null ? targetBody.linearVelocity : Vector3.zero;
            velocity.y = 0f;
            Vector3 desiredAhead = Vector3.ClampMagnitude(velocity * lookAheadSeconds, maxLookAhead) * (1f - zoom);
            lookAhead = Vector3.SmoothDamp(lookAhead, desiredAhead, ref lookAheadVelocity, 0.9f);

            Vector3 desired = target.position + lookAhead;
            desired.x = Mathf.Clamp(desired.x, boundsMin.x, boundsMax.x);
            desired.z = Mathf.Clamp(desired.z, boundsMin.y, boundsMax.y);
            focus = Vector3.SmoothDamp(focus, desired, ref focusVelocity, followTime);

            if (!indoor && Core.GameInput.Instance != null) yawTarget += Core.GameInput.Instance.LookYaw;
            yaw = Mathf.SmoothDampAngle(yaw, yawTarget, ref yawVelocity, 0.18f);
            zoom = Mathf.SmoothDamp(zoom, Mathf.Clamp01(ZoomTarget), ref zoomVelocity, zoomTime);
            // Walking frames the character closer; the boat pulls back to show the river.
            baseDistance = Mathf.SmoothDamp(baseDistance, targetBody != null ? distance : footDistance, ref baseDistanceVelocity, 0.9f);
            basePitch = Mathf.Lerp(footPitch, pitch, Mathf.InverseLerp(footDistance, distance, baseDistance));
            Apply(dt);
        }

        void Apply(float dt)
        {
            float t = Time.time;
            Vector3 drift = new Vector3(Mathf.PerlinNoise(t * 0.07f, 0.3f) - 0.5f, 0f, Mathf.PerlinNoise(0.7f, t * 0.06f) - 0.5f) * driftAmount * 2f;
            if (cam != null) cam.fieldOfView = Camera.HorizontalToVerticalFieldOfView(horizontalFov, Mathf.Max(cam.aspect, 0.1f));
            float d = Mathf.Lerp(baseDistance, closeDistance, zoom);
            float p = Mathf.Lerp(basePitch, closePitch, zoom);
            Quaternion rot = Quaternion.Euler(p, yaw, 0f);
            transform.SetPositionAndRotation(focus + drift - rot * Vector3.forward * d, rot);

            if (dof != null)
            {
                dof.gaussianStart.value = d + 6f;
                dof.gaussianEnd.value = d + 40f;
            }
        }

        public void Snap()
        {
            if (target == null) return;
            focus = target.position;
            focusVelocity = Vector3.zero;
            lookAhead = Vector3.zero;
            baseDistance = targetBody != null ? distance : footDistance;
            basePitch = targetBody != null ? pitch : footPitch;
            Apply(0f);
        }
    }
}
