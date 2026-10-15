using System.Collections.Generic;
using CozyFishing.Core;
using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>
    /// Finds the closest usable <see cref="Interactable"/> in front of the player, publishes its prompt to the
    /// context button, and triggers it on the action input.
    /// </summary>
    public sealed class PlayerInteractor : MonoBehaviour
    {
        static readonly List<Interactable> all = new();

        public Interactable Current { get; private set; }
        public bool Enabled { get; set; } = true;

        public static void Register(Interactable i) { if (!all.Contains(i)) all.Add(i); }
        public static void Unregister(Interactable i) => all.Remove(i);

        void Update()
        {
            Current = Enabled ? FindBest() : null;
            if (Enabled)
                TouchControls.Publish(Current != null ? Current.Prompt : "", Current != null ? Current.PromptPosition : Vector3.zero);
            if (Current != null && GameInput.Instance != null && GameInput.Instance.ActionPressed)
                Current.Interact(this);
        }

        Interactable FindBest()
        {
            Interactable best = null;
            float bestScore = float.MaxValue;
            Vector3 p = transform.position;
            Vector3 fwd = transform.forward;
            foreach (var i in all)
            {
                if (i == null || !i.isActiveAndEnabled || !i.CanInteract(this)) continue;
                Vector3 d = i.transform.position - p;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > i.Radius) continue;
                // Prefer what the player faces.
                float facing = dist > 0.01f ? Vector3.Dot(fwd, d / dist) : 1f;
                float score = dist * (1.6f - facing * 0.6f);
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }
    }
}
