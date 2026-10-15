using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>
    /// The playable rectangle of a location (x/z). The boat and the hero are kept inside it so they can't row or walk
    /// off the end of the map, past where the camera follows.
    /// </summary>
    public sealed class PlayArea : MonoBehaviour
    {
        [SerializeField] Vector2 min = new(-26f, -52f);
        [SerializeField] Vector2 max = new(26f, 39f);

        static PlayArea instance;

        void OnEnable() => instance = this;
        void OnDisable() { if (instance == this) instance = null; }

        /// <summary>
        /// Keeps a position inside the area (shrunk by <paramref name="margin"/>) and zeroes the velocity pointing out.
        /// Returns true when it had to push back.
        /// </summary>
        public static bool Contain(ref Vector3 position, ref Vector3 velocity, float margin)
        {
            if (instance == null) return false;
            // Far-away spaces (the cabin interior is built 170 m off the map) are not part of the outdoor area.
            if (position.x < instance.min.x - 25f || position.x > instance.max.x + 25f || position.z < instance.min.y - 25f || position.z > instance.max.y + 25f)
                return false;
            bool pushed = false;
            float x0 = instance.min.x + margin, x1 = instance.max.x - margin;
            float z0 = instance.min.y + margin, z1 = instance.max.y - margin;
            if (position.x < x0) { position.x = x0; velocity.x = Mathf.Max(0f, velocity.x); pushed = true; }
            else if (position.x > x1) { position.x = x1; velocity.x = Mathf.Min(0f, velocity.x); pushed = true; }
            if (position.z < z0) { position.z = z0; velocity.z = Mathf.Max(0f, velocity.z); pushed = true; }
            else if (position.z > z1) { position.z = z1; velocity.z = Mathf.Min(0f, velocity.z); pushed = true; }
            return pushed;
        }

#if UNITY_EDITOR
        public void EditorSetup(Vector2 lo, Vector2 hi) { min = lo; max = hi; }
#endif
    }
}
