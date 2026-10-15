using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.World.Ambient
{
    /// <summary>Grandpa's cat asleep on the porch: slow breathing, a lazy tail swish now and then.</summary>
    public sealed class SleepingCat : MonoBehaviour
    {
        [SerializeField] Transform body, tail;

        void Update()
        {
            float breathe = 1f + Mathf.Sin(Time.time * 1.6f) * 0.03f;
            body.localScale = new Vector3(1f, breathe, 1f);
            float swish = Mathf.PerlinNoise(Time.time * 0.3f, 5f) > 0.65f ? Mathf.Sin(Time.time * 4f) * 25f : 0f;
            tail.localRotation = Quaternion.Euler(0f, swish, 0f);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform b, Transform t) { body = b; tail = t; }
#endif
    }
}
