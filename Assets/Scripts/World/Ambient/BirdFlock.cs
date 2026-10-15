using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>
    /// Birds wheeling in loose loops over the valley (songbirds) or the bay (gulls): flapping bursts and glides,
    /// each bird on its own slightly offset orbit. Daytime only for songbirds.
    /// </summary>
    public sealed class BirdFlock : Critter
    {
        [SerializeField] Transform[] birds;
        [SerializeField] Transform[] wingsL, wingsR;
        [SerializeField] float radius = 14f;
        [SerializeField] float height = 11f;
        [SerializeField] float speed = 4f;
        [SerializeField] bool dayOnly = true;
        [SerializeField] float flapRate = 9f;

        Vector3 centre;
        float[] offsets;

        protected override void Awake()
        {
            base.Awake();
            centre = transform.position;
            offsets = new float[birds.Length];
            for (int i = 0; i < offsets.Length; i++) offsets[i] = Random.Range(0f, 100f);
        }

        void Update()
        {
            bool show = !dayOnly || IsDay;
            float t = Time.time * speed / radius;
            for (int i = 0; i < birds.Length; i++)
            {
                if (birds[i].gameObject.activeSelf != show) birds[i].gameObject.SetActive(show);
                if (!show) continue;
                float o = offsets[i];
                float a = t * (0.85f + 0.3f * Mathf.PerlinNoise(o, 0.3f)) + o;
                float r = radius * (0.7f + 0.5f * Mathf.PerlinNoise(o, Time.time * 0.05f));
                Vector3 p = centre + new Vector3(Mathf.Cos(a) * r, height + Mathf.Sin(Time.time * 0.6f + o) * 1.5f, Mathf.Sin(a * 1.3f) * r * 0.7f);
                Vector3 dir = p - birds[i].position;
                birds[i].position = p;
                if (dir.sqrMagnitude > 1e-5f)
                {
                    var look = Quaternion.LookRotation(dir.normalized);
                    float bank = Mathf.Clamp(Vector3.SignedAngle(birds[i].forward, dir, Vector3.up) * 8f, -35f, 35f);
                    birds[i].rotation = Quaternion.Slerp(birds[i].rotation, look * Quaternion.Euler(0f, 0f, -bank), Time.deltaTime * 4f);
                }
                // Flap in bursts, then glide with wings held out.
                float burst = Mathf.PerlinNoise(o * 3f, Time.time * 0.4f) > 0.45f ? 1f : 0.15f;
                float flap = Mathf.Sin(Time.time * flapRate + o) * 50f * burst;
                if (i < wingsL.Length) wingsL[i].localRotation = Quaternion.Euler(0f, 0f, -flap);
                if (i < wingsR.Length) wingsR[i].localRotation = Quaternion.Euler(0f, 0f, flap);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform[] b, Transform[] l, Transform[] r, float rad, float h, float spd, bool day, float flap)
        { birds = b; wingsL = l; wingsR = r; radius = rad; height = h; speed = spd; dayOnly = day; flapRate = flap; }
#endif
    }
}
