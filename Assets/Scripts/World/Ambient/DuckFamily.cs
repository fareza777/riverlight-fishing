using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>
    /// A mother duck paddling slow loops on open water with her ducklings in a little line behind.
    /// Heads bob, now and then she dips for food; they paddle away from the boat.
    /// </summary>
    public sealed class DuckFamily : Critter
    {
        [SerializeField] Transform[] ducks;     // [0] = mother, the rest follow
        [SerializeField] Transform[] heads;
        [SerializeField] float radius = 3.5f;
        [SerializeField] float speed = 0.35f;

        Vector3 centre;
        float angle, dipTimer, dip;
        readonly Vector3[] trail = new Vector3[64];
        int trailHead;
        float trailTimer;

        protected override void Awake()
        {
            base.Awake();
            centre = transform.position;
            angle = seed;
            for (int i = 0; i < trail.Length; i++) trail[i] = ducks[0].position;
            dipTimer = Random.Range(4f, 9f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float flee = ThreatDistance(ducks[0].position, out var threat) < 5f ? 2.2f : 1f;
            angle += speed * flee / radius * dt;
            // A wobbly loop rather than a perfect circle.
            float r = radius * (0.8f + 0.2f * Mathf.Sin(angle * 2.3f + seed));
            Vector3 target = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
            target.y = Mathf.Sin(Time.time * 1.7f + seed) * 0.012f;
            var mom = ducks[0];
            Vector3 vel = target - mom.position;
            mom.position = target;
            mom.rotation = Face(vel, mom.rotation, 240f);

            // Ducklings follow the mother's recent path at a fixed spacing.
            trailTimer += dt;
            if (trailTimer > 0.12f) { trailTimer = 0f; trailHead = (trailHead + 1) % trail.Length; trail[trailHead] = mom.position; }
            for (int i = 1; i < ducks.Length; i++)
            {
                var p = trail[(trailHead - i * 5 + trail.Length * 4) % trail.Length];
                p.y = Mathf.Sin(Time.time * 2.2f + i) * 0.008f;
                Vector3 v = p - ducks[i].position;
                ducks[i].position = Vector3.Lerp(ducks[i].position, p, dt * 4f);
                ducks[i].rotation = Face(v, ducks[i].rotation, 300f);
            }

            // Mother dips her head for food now and then; everyone bobs.
            dipTimer -= dt;
            if (dipTimer <= 0f) { dip = 1.4f; dipTimer = Random.Range(5f, 11f); }
            dip = Mathf.Max(0f, dip - dt);
            for (int i = 0; i < heads.Length; i++)
            {
                float bob = Mathf.Sin(Time.time * 4f + i * 1.3f) * 6f;
                float down = i == 0 ? Mathf.Sin(Mathf.Clamp01(dip / 1.4f) * Mathf.PI) * 70f : 0f;
                heads[i].localRotation = Quaternion.Euler(bob + down, 0f, 0f);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform[] d, Transform[] h, float r) { ducks = d; heads = h; radius = r; }
#endif
    }
}
