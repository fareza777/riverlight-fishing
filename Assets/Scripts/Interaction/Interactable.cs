using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>Anything the player can use with the context button when standing close to it.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] float radius = 1.8f;
        [SerializeField] Transform promptAnchor;

        public float Radius => radius;
        public Vector3 PromptPosition => promptAnchor ? promptAnchor.position : transform.position + Vector3.up * 1.6f;

        /// <summary>Short, localized label for the context button (e.g. "Masuk", "Naik perahu").</summary>
        public abstract string Prompt { get; }

        public virtual bool CanInteract(PlayerInteractor who) => true;

        protected virtual void OnEnable() => PlayerInteractor.Register(this);
        protected virtual void OnDisable() => PlayerInteractor.Unregister(this);

        public abstract void Interact(PlayerInteractor who);

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
