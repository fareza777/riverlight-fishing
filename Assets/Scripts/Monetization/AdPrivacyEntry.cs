using UnityEngine;

namespace CozyFishing.Monetization
{
    /// <summary>Stays subscribed while hidden so consent gathered after Settings opens can reveal the entry.</summary>
    public sealed class AdPrivacyEntry : MonoBehaviour
    {
        AdManager manager;
        public void Bind()
        {
            manager = AdManager.Instance;
            manager.StateChanged += Refresh;
            Refresh();
        }
        void Refresh() => gameObject.SetActive(manager.PrivacyOptionsRequired);
        void OnDestroy() { if (manager != null) manager.StateChanged -= Refresh; }
    }
}
