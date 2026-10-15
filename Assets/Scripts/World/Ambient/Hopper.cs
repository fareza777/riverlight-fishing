using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>
    /// Small ground animals: rabbits hop about their patch of meadow, sniff and twitch their ears, and bolt when the
    /// player comes near. Ground birds do the same with little hops and pecks, and fly off instead of running.
    /// </summary>
    public sealed class Hopper : Critter
    {
        [SerializeField] Transform body, ears;
        [SerializeField] Transform wingL, wingR;          // birds only
        [SerializeField] float range = 4f;
        [SerializeField] float hopLength = 0.35f, hopHeight = 0.12f, hopTime = 0.28f;
        [SerializeField] float fleeDistance = 4.5f;

        Vector3 home, from, to;
        float hopT = 1f, rest, away;
        bool fleeing, gone;
        Vector3 flyVel;

        bool IsBird => wingL != null;

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            from = to = home;
            rest = Random.Range(0.5f, 3f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (gone)
            {
                away -= dt;
                if (away <= 0f && ThreatDistance(home, out _) > fleeDistance * 2f)
                {
                    gone = false; fleeing = false;
                    transform.position = from = to = home;
                    SetVisible(true);
                }
                return;
            }
            float threatDist = ThreatDistance(transform.position, out var threat);
            if (!fleeing && threatDist < fleeDistance)
            {
                fleeing = true;
                flyVel = (transform.position - threat).normalized * 3f + Vector3.up * 3f;
                flyVel.y = 3f;
                rest = 0f;
            }

            if (IsBird && fleeing)
            {
                // Birds take off and flutter away, then come back to the same spot later.
                transform.position += flyVel * dt;
                transform.rotation = Face(flyVel, transform.rotation, 400f);
                float flap = Mathf.Sin(Time.time * 28f) * 60f;
                wingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
                wingR.localRotation = Quaternion.Euler(0f, 0f, flap);
                if (transform.position.y > home.y + 8f) Leave();
                return;
            }

            if (hopT < 1f)
            {
                hopT += dt / (fleeing ? hopTime * 0.7f : hopTime);
                float k = Mathf.Clamp01(hopT);
                Vector3 p = Vector3.Lerp(from, to, k);
                p.y = Mathf.Lerp(from.y, to.y, k) + Mathf.Sin(k * Mathf.PI) * hopHeight * (fleeing ? 1.5f : 1f);
                transform.position = p;
                if (body) body.localScale = new Vector3(1f, 1f + Mathf.Sin(k * Mathf.PI) * 0.12f, 1f - Mathf.Sin(k * Mathf.PI) * 0.08f);
                return;
            }

            rest -= dt;
            Idle();
            if (rest > 0f) return;
            if (fleeing && (transform.position - home).magnitude > range * 2.5f) { Leave(); return; }

            // Next hop: away from the threat when fleeing, otherwise a random spot near home.
            Vector3 dir = fleeing ? (transform.position - threat) : (home + Random.insideUnitSphere * range - transform.position);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-3f) dir = transform.forward;
            dir.Normalize();
            from = transform.position;
            to = from + dir * hopLength * (fleeing ? 2.2f : Random.Range(0.6f, 1.2f));
            to.y = Ground(to, from.y);
            if (to.y < 0.15f) { to = from; rest = 0.5f; return; }   // never hop into the water
            transform.rotation = Quaternion.LookRotation(dir);
            hopT = 0f;
            rest = fleeing ? 0.05f : Random.Range(IsBird ? 0.4f : 0.8f, IsBird ? 1.6f : 3.5f);
        }

        void Idle()
        {
            if (body) body.localScale = Vector3.one;
            if (ears) ears.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * 2f + seed) * 6f + (Mathf.PerlinNoise(seed, Time.time * 0.8f) > 0.7f ? 20f : 0f), 0f, 0f);
            if (IsBird)
            {
                // Peck at the ground every so often.
                float peck = Mathf.PerlinNoise(seed, Time.time * 1.5f) > 0.6f ? 35f : 0f;
                body.localRotation = Quaternion.Euler(peck, 0f, 0f);
                wingL.localRotation = Quaternion.Euler(0f, -85f, 6f);
                wingR.localRotation = Quaternion.Euler(0f, 85f, -6f);
            }
        }

        void Leave()
        {
            gone = true;
            away = Random.Range(20f, 40f);
            SetVisible(false);
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform b, Transform e, Transform wl, Transform wr, float hopLen, float hopH, float flee)
        { body = b; ears = e; wingL = wl; wingR = wr; hopLength = hopLen; hopHeight = hopH; fleeDistance = flee; }
#endif
    }
}
