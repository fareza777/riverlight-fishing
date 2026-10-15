using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>
    /// A grey heron standing still in the shallows: slow head turns, a sudden strike at a fish now and then.
    /// When the player or the boat comes close it lifts off with big slow wingbeats and returns later.
    /// </summary>
    public sealed class Heron : Critter
    {
        [SerializeField] Transform neck, wingL, wingR;
        [SerializeField] float fleeDistance = 6f;

        Vector3 home;
        Quaternion homeRot;
        float strikeTimer, strike, away, flight;
        Vector3 flyDir;
        enum Mode { Standing, Flying, Away }
        Mode mode;

        protected override void Awake()
        {
            base.Awake();
            home = transform.position;
            homeRot = transform.rotation;
            strikeTimer = Random.Range(5f, 10f);
            Fold(1f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            switch (mode)
            {
                case Mode.Standing:
                    if (ThreatDistance(transform.position, out var threat) < fleeDistance)
                    {
                        mode = Mode.Flying; flight = 0f;
                        flyDir = (transform.position - threat); flyDir.y = 0f; flyDir = flyDir.normalized;
                        break;
                    }
                    strikeTimer -= dt;
                    if (strikeTimer <= 0f) { strike = 0.9f; strikeTimer = Random.Range(6f, 14f); }
                    strike = Mathf.Max(0f, strike - dt);
                    float look = Mathf.Sin(Time.time * 0.3f + seed) * 25f;
                    float jab = Mathf.Sin(Mathf.Clamp01(strike / 0.9f) * Mathf.PI) * 75f;
                    neck.localRotation = Quaternion.Euler(jab, look, 0f);
                    break;
                case Mode.Flying:
                    flight += dt;
                    Fold(0f);
                    float beat = Mathf.Sin(flight * 5f) * 45f;
                    wingL.localRotation = Quaternion.Euler(0f, 0f, -beat);
                    wingR.localRotation = Quaternion.Euler(0f, 0f, beat);
                    transform.position += (flyDir * 5f + Vector3.up * 2.2f) * dt;
                    transform.rotation = Face(flyDir, transform.rotation, 120f);
                    neck.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    if (flight > 6f) { mode = Mode.Away; away = Random.Range(25f, 45f); SetVisible(false); }
                    break;
                case Mode.Away:
                    away -= dt;
                    if (away <= 0f && ThreatDistance(home, out _) > fleeDistance * 2f)
                    {
                        transform.SetPositionAndRotation(home, homeRot);
                        Fold(1f);
                        SetVisible(true);
                        mode = Mode.Standing;
                    }
                    break;
            }
        }

        /// <summary>Wings folded along the back (1) or spread for flight (0).</summary>
        void Fold(float k)
        {
            // Folded: swept back along the body (yaw) and lowered slightly onto the back.
            wingL.localRotation = Quaternion.Euler(0f, -k * 88f, k * 8f);
            wingR.localRotation = Quaternion.Euler(0f, k * 88f, -k * 8f);
        }

        void SetVisible(bool v)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = v;
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform n, Transform l, Transform r) { neck = n; wingL = l; wingR = r; }
#endif
    }
}
