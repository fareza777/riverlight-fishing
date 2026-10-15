using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>Marker trigger at the end of the dock: rowing in empties the creel.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DockZone : MonoBehaviour
    {
        void Reset() => GetComponent<Collider>().isTrigger = true;
    }
}
