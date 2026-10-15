using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>A butterfly fluttering in erratic little loops above the meadow flowers (daytime).</summary>
    public sealed class Butterfly : Critter
    {
        [SerializeField] Transform wingL, wingR;
        [SerializeField] float range = 2.5f;
        Vector3 home;

        protected override void Awake() { base.Awake(); home = transform.position; }

        void Update()
        {
            bool day = IsDay;
            if (wingL.gameObject.activeSelf != day) { foreach (Transform c in transform) c.gameObject.SetActive(day); }
            if (!day) return;
            float t = Time.time * 0.6f + seed;
            Vector3 p = home + new Vector3((Mathf.PerlinNoise(t, seed) - 0.5f) * 2f * range,
                0.35f + Mathf.PerlinNoise(seed, t * 1.7f) * 0.8f, (Mathf.PerlinNoise(seed * 2f, t) - 0.5f) * 2f * range);
            Vector3 v = p - transform.position;
            transform.position = p;
            transform.rotation = Face(v, transform.rotation, 600f);
            float flap = Mathf.Abs(Mathf.Sin(Time.time * 18f + seed)) * 70f;
            wingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
            wingR.localRotation = Quaternion.Euler(0f, 0f, flap);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform l, Transform r) { wingL = l; wingR = r; }
#endif
    }
}
