using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>Camera framing for an area (outdoors, cabin interior): XZ bounds plus walking distance/pitch.</summary>
    public sealed class CameraZone : MonoBehaviour
    {
        [SerializeField] Vector2 boundsMin = new(-26f, -52f);
        [SerializeField] Vector2 boundsMax = new(26f, 39f);
        [SerializeField] float footDistance = 11f;
        [SerializeField] float footPitch = 50f;
        [SerializeField] bool indoor;

        public Vector2 BoundsMin => boundsMin;
        public Vector2 BoundsMax => boundsMax;
        public float FootDistance => footDistance;
        public float FootPitch => footPitch;
        public bool Indoor => indoor;

#if UNITY_EDITOR
        public void EditorSetup(Vector2 min, Vector2 max, float distance, float pitch, bool isIndoor)
        {
            boundsMin = min; boundsMax = max; footDistance = distance; footPitch = pitch; indoor = isIndoor;
        }
#endif
    }
}
