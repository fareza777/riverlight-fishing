using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>
    /// On-foot movement: camera-relative joystick walking/running with eased acceleration and smooth turning.
    /// Walk below ~70% stick, run above. Disabled while the player sits in the boat.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] float walkSpeed = 1.6f;
        [SerializeField] float runSpeed = 4.2f;
        [SerializeField] float runThreshold = 0.72f;
        [SerializeField] float acceleration = 9f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float gravity = -18f;
        [SerializeField] Transform cameraTransform;
        [SerializeField] float waterLevel;

        CharacterController body;
        Vector3 velocity;
        float verticalSpeed;

        /// <summary>0 idle, 1 walk, 2 run — feeds the locomotion blend tree.</summary>
        public float AnimSpeed { get; private set; }
        public bool InputLocked { get; set; }
        public CharacterController Body => body;

        void Awake() => body = GetComponent<CharacterController>();

        void Update()
        {
            if (!body.enabled) return;
            float dt = Time.deltaTime;
            Vector2 stick = InputLocked || GameInput.Instance == null ? Vector2.zero : GameInput.Instance.Move;

            // Camera-relative direction on the ground plane.
            Vector3 fwd = cameraTransform ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Vector3 dir = fwd * stick.y + right * stick.x;
            float amount = Mathf.Clamp01(dir.magnitude);
            if (amount > 1e-3f) dir /= Mathf.Max(amount, 1e-3f);

            float targetSpeed = amount < 0.05f ? 0f : amount < runThreshold ? Mathf.Lerp(walkSpeed * 0.5f, walkSpeed, amount / runThreshold) : runSpeed;
            Vector3 targetVel = dir * targetSpeed;
            velocity = Vector3.MoveTowards(velocity, targetVel, acceleration * dt);

            if (velocity.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(velocity.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);
            }

            // Don't wade into the river: stop at the waterline (the dock deck is solid ground above it).
            Vector3 ahead = transform.position + velocity.normalized * (body.radius + 0.35f);
            // A sphere (not a ray) so the small gaps between dock planks don't read as open water.
            // Floating things (the moored boat) are not ground: stepping onto them dropped the player into the water.
            if (velocity.sqrMagnitude > 0.01f && Physics.SphereCast(ahead + Vector3.up * 2f, 0.15f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && (hit.point.y < waterLevel + 0.08f || (hit.rigidbody != null && !hit.rigidbody.isKinematic)))
                velocity = Vector3.zero;

            // Don't walk off the edge of the map.
            Vector3 next = transform.position + velocity * (dt + 0.25f);
            Vector3 clamped = next;
            if (World.PlayArea.Contain(ref clamped, ref velocity, 1.5f) && velocity.sqrMagnitude < 1e-4f) AnimSpeed = Mathf.Lerp(AnimSpeed, 0f, dt * 8f);

            verticalSpeed = body.isGrounded ? -1f : verticalSpeed + gravity * dt;
            body.Move((velocity + Vector3.up * verticalSpeed) * dt);

            float speed = new Vector3(velocity.x, 0f, velocity.z).magnitude;
            float anim = speed <= walkSpeed ? speed / walkSpeed : 1f + (speed - walkSpeed) / (runSpeed - walkSpeed);
            AnimSpeed = Mathf.Lerp(AnimSpeed, anim, 1f - Mathf.Exp(-12f * dt));
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool was = body.enabled;
            body.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            velocity = Vector3.zero;
            body.enabled = was;
        }

        public void Stop()
        {
            velocity = Vector3.zero;
            AnimSpeed = 0f;
        }
    }
}
