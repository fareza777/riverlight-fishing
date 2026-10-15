using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>
    /// Base for the procedural ambient animals. Gives them the ground/water helpers, a shared "threat" check
    /// (the player on foot or the boat) and skips some animals on the Low graphics level.
    /// </summary>
    public abstract class Critter : MonoBehaviour
    {
        [Tooltip("Hidden on the Low graphics level to keep old phones smooth.")]
        [SerializeField] protected bool skipOnLow;

        static Transform player, boat;
        protected float seed;

        protected virtual void Awake()
        {
            seed = Random.Range(0f, 100f);
            if (skipOnLow && GraphicsQuality.Level == GfxLevel.Low) gameObject.SetActive(false);
        }

        /// <summary>Distance on the ground plane to the closest of player / boat.</summary>
        protected static float ThreatDistance(Vector3 from, out Vector3 threatPos)
        {
            if (player == null) { var p = FindAnyObjectByType<Player.PlayerController>(); if (p) player = p.transform; }
            if (boat == null) { var b = FindAnyObjectByType<Boat.BoatController>(); if (b) boat = b.transform; }
            float best = float.MaxValue; threatPos = from + Vector3.forward * 999f;
            foreach (var t in new[] { player, boat })
            {
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                Vector3 d = t.position - from; d.y = 0f;
                if (d.magnitude < best) { best = d.magnitude; threatPos = t.position; }
            }
            return best;
        }

        /// <summary>Terrain/prop height below a point (ignores water, which has no collider).</summary>
        protected static float Ground(Vector3 p, float fallback = 0f) =>
            Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : fallback;

        protected static bool IsDay
        {
            get
            {
                var tod = TimeCache;
                return tod == null || (tod.Hour > 6f && tod.Hour < 19f);
            }
        }

        static TimeOfDay timeCache;
        protected static TimeOfDay TimeCache => timeCache != null ? timeCache : timeCache = FindAnyObjectByType<TimeOfDay>();

        protected static Quaternion Face(Vector3 dir, Quaternion current, float degPerSec)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return current;
            return Quaternion.RotateTowards(current, Quaternion.LookRotation(dir), degPerSec * Time.deltaTime);
        }
    }
}
