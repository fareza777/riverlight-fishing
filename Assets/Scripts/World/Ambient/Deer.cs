using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>
    /// A deer grazing at the forest edge: head down to graze, head up to look around, a few slow steps.
    /// It watches the player approach, then bounds away and comes back much later.
    /// </summary>
    public sealed class Deer : Critter
    {
        [SerializeField] Transform neck;
        [SerializeField] Transform[] legs;   // FL, FR, BL, BR
        [SerializeField] float range = 5f;
        [SerializeField] float alertDistance = 12f, fleeDistance = 8f;

        Vector3 home, target;
        float phase, mood, away;
        bool walking, fleeing, gone;

        protected override void Awake()
        {
            base.Awake();
            home = target = transform.position;
            mood = Random.Range(2f, 6f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (gone)
            {
                away -= dt;
                if (away <= 0f && ThreatDistance(home, out _) > alertDistance * 1.5f)
                {
                    gone = fleeing = walking = false;
                    transform.position = target = home;
                    foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
                }
                return;
            }

            float threat = ThreatDistance(transform.position, out var threatPos);
            if (!fleeing && threat < fleeDistance)
            {
                fleeing = walking = true;
                Vector3 away3 = transform.position - threatPos; away3.y = 0f;
                target = transform.position + away3.normalized * 30f;
            }

            float speed = fleeing ? 5.5f : 0.6f;
            if (walking)
            {
                Vector3 to = target - transform.position; to.y = 0f;
                if (to.magnitude < 0.2f) { walking = false; mood = Random.Range(3f, 8f); }
                else
                {
                    Vector3 step = to.normalized * Mathf.Min(speed * dt, to.magnitude);
                    Vector3 next = transform.position + step;
                    next.y = Ground(next, transform.position.y);
                    if (next.y < 0.2f && !fleeing) { walking = false; }
                    else transform.position = next;
                    transform.rotation = Face(to, transform.rotation, fleeing ? 360f : 90f);
                    phase += dt * (fleeing ? 11f : 4f);
                }
            }
            else
            {
                mood -= dt;
                if (mood <= 0f)
                {
                    Vector2 r = Random.insideUnitCircle * range;
                    target = home + new Vector3(r.x, 0f, r.y);
                    walking = true;
                }
            }
            if (fleeing && (transform.position - home).magnitude > 28f)
            {
                gone = true;
                away = Random.Range(40f, 70f);
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
                return;
            }

            // Legs: diagonal pairs swing together; a bounding gait while fleeing.
            float swing = walking ? (fleeing ? 38f : 20f) : 0f;
            for (int i = 0; i < legs.Length; i++)
            {
                float off = (i == 0 || i == 3) ? 0f : Mathf.PI;
                legs[i].localRotation = Quaternion.Euler(Mathf.Sin(phase + off) * swing, 0f, 0f);
            }

            // Head: grazing (down) when calm and still, up and alert when the player is near.
            bool alert = threat < alertDistance || fleeing;
            float graze = !walking && !alert && Mathf.PerlinNoise(seed, Time.time * 0.15f) > 0.35f ? 1f : 0f;
            float pitch = Mathf.Lerp(-5f, 70f, graze) + Mathf.Sin(Time.time * 3f + seed) * 3f * graze;
            float yaw = alert && !fleeing ? Mathf.Clamp(Vector3.SignedAngle(transform.forward, threatPos - transform.position, Vector3.up), -60f, 60f) : 0f;
            neck.localRotation = Quaternion.Slerp(neck.localRotation, Quaternion.Euler(pitch, yaw, 0f), dt * 3f);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform n, Transform[] l) { neck = n; legs = l; }
#endif
    }
}
