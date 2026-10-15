using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>Fireflies drifting over the grass and water's edge after dusk (a pooled particle system).</summary>
    public sealed class Fireflies : MonoBehaviour
    {
        [SerializeField] ParticleSystem particles;

        void Update()
        {
            var tod = FindTime();
            bool night = tod != null && (tod.Hour > 19f || tod.Hour < 5f);
            var emission = particles.emission;
            emission.enabled = night;
            // Fireflies stay around the player so a handful of particles fills the screen.
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 p = cam.transform.position + cam.transform.forward * 12f;
                p.y = 1.2f;
                transform.position = p;
            }
        }

        static TimeOfDay cached;
        static TimeOfDay FindTime() => cached != null ? cached : cached = FindAnyObjectByType<TimeOfDay>();

#if UNITY_EDITOR
        public void EditorSetup(ParticleSystem ps) => particles = ps;
#endif
    }
}
