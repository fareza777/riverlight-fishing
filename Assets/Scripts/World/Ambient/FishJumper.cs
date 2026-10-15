using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>Every so often a fish jumps out of the water near a school: an arc, a splash and ripples.</summary>
    public sealed class FishJumper : MonoBehaviour
    {
        [SerializeField] GameObject[] fishModels;
        [SerializeField] Transform[] spots;
        [SerializeField] ParticleSystem splash;
        [SerializeField] Vector2 interval = new(6f, 14f);

        float timer;
        GameObject fish;

        void Start()
        {
            timer = Random.Range(interval.x, interval.y);
            if (GraphicsQuality.Level == GfxLevel.Low) interval *= 1.6f;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f || fishModels.Length == 0 || spots.Length == 0) return;
            timer = Random.Range(interval.x, interval.y);
            StartCoroutine(Jump());
        }

        System.Collections.IEnumerator Jump()
        {
            var spot = spots[Random.Range(0, spots.Length)];
            Vector3 start = spot.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
            start.y = 0f;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 end = start + dir * Random.Range(0.8f, 1.6f);
            if (fish != null) Destroy(fish);
            fish = Instantiate(fishModels[Random.Range(0, fishModels.Length)]);
            foreach (var c in fish.GetComponentsInChildren<Collider>()) Destroy(c);
            Splash(start);
            const float duration = 0.7f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                Vector3 p = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
                Vector3 v = (end - start) + Vector3.up * Mathf.Cos(k * Mathf.PI) * 2.2f;
                fish.transform.SetPositionAndRotation(p, Quaternion.LookRotation(v.normalized) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 25f) * 20f));
                yield return null;
            }
            Splash(end);
            Destroy(fish);
        }

        void Splash(Vector3 at)
        {
            if (splash == null) return;
            splash.transform.position = at;
            splash.Emit(14);
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject[] models, Transform[] s, ParticleSystem ps) { fishModels = models; spots = s; splash = ps; }
#endif
    }
}
