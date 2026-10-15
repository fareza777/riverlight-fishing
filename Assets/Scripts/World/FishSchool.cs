using System.Collections.Generic;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>
    /// A small school of fish swimming under the surface: cohesion around a wandering leader,
    /// separation, a tail wiggle, and scattering away from the boat.
    /// </summary>
    public sealed class FishSchool : MonoBehaviour
    {
        [SerializeField] GameObject fishPrefab;
        [SerializeField] int count = 7;
        [SerializeField] float areaRadius = 5f;
        [SerializeField] float depth = 0.55f;
        [SerializeField] float speed = 0.9f;
        [SerializeField] Vector2 scaleRange = new(0.8f, 1.15f);
        [SerializeField] Transform threat;
        [SerializeField] float fleeRadius = 3.2f;

        sealed class Member
        {
            public Transform transform;
            public Vector3 velocity;
            public float phase;
        }

        readonly List<Member> members = new();
        Vector3 leader;
        Vector3 leaderTarget;

        void Start()
        {
            if (fishPrefab == null) return;
            leader = transform.position;
            PickTarget();
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(fishPrefab, transform);
                Vector2 r = Random.insideUnitCircle * areaRadius * 0.4f;
                go.transform.position = transform.position + new Vector3(r.x, -depth, r.y);
                go.transform.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Destroy(c);
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                members.Add(new Member { transform = go.transform, velocity = Random.insideUnitSphere * speed, phase = Random.value * 10f });
            }
        }

        void PickTarget()
        {
            Vector2 r = Random.insideUnitCircle * areaRadius;
            leaderTarget = transform.position + new Vector3(r.x, 0f, r.y);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            leader = Vector3.MoveTowards(leader, leaderTarget, speed * 0.8f * dt);
            if ((leader - leaderTarget).sqrMagnitude < 0.25f) PickTarget();

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                Vector3 pos = m.transform.position;
                Vector3 steer = (leader - pos) * 0.6f;
                for (int j = 0; j < members.Count; j++)
                {
                    if (i == j) continue;
                    Vector3 away = pos - members[j].transform.position;
                    float d = away.magnitude;
                    if (d < 0.45f && d > 1e-4f) steer += away / (d * d) * 0.08f;
                }
                if (threat != null)
                {
                    Vector3 fromThreat = pos - threat.position;
                    fromThreat.y = 0f;
                    float d = fromThreat.magnitude;
                    if (d < fleeRadius) steer += fromThreat.normalized * (fleeRadius - d) * 3f;
                }
                steer.y = (transform.position.y - depth - pos.y) * 2f;
                m.velocity = Vector3.ClampMagnitude(m.velocity + steer * dt, speed * (threat && (pos - threat.position).sqrMagnitude < fleeRadius * fleeRadius ? 3f : 1f));
                pos += m.velocity * dt;
                m.transform.position = pos;

                Vector3 flat = new(m.velocity.x, 0f, m.velocity.z);
                if (flat.sqrMagnitude > 1e-4f)
                {
                    m.phase += dt * (6f + m.velocity.magnitude * 6f);
                    Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                    Quaternion wiggle = Quaternion.Euler(0f, Mathf.Sin(m.phase) * 9f, 0f);
                    m.transform.rotation = Quaternion.Slerp(m.transform.rotation, look * wiggle, 1f - Mathf.Exp(-6f * dt));
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, areaRadius);
        }
    }
}
