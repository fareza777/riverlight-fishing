using CozyFishing.Player;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>On the boat: tells <see cref="PlayerModes"/> when the boat is inside a <see cref="DockZone"/>.</summary>
    public sealed class BoatDockSensor : MonoBehaviour
    {
        [SerializeField] PlayerModes modes;

        void OnTriggerEnter(Collider other)
        {
            if (other.GetComponent<DockZone>() != null) modes.SetNearDock(true);
        }

        void OnTriggerExit(Collider other)
        {
            if (other.GetComponent<DockZone>() != null) modes.SetNearDock(false);
        }
    }
}
