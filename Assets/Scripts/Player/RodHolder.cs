using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>
    /// Keeps the fishing rod in the character's right hand. The rod points along the forearm blended with "up",
    /// so it follows the cast wind-up and throw of the animation without depending on the rig's bone axes.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class RodHolder : MonoBehaviour
    {
        [SerializeField] Animator character;
        [SerializeField, Range(0f, 1f)] float upBlend = 0.45f;
        [SerializeField] Vector3 gripOffset = new(0f, -0.03f, 0f);

        Transform hand;
        Transform forearm;

        void Start() { if (hand == null) Bind(character); }

        public void Bind(Animator animator)
        {
            character = animator;
            if (character == null) return;
            hand = character.GetBoneTransform(HumanBodyBones.RightHand);
            forearm = character.GetBoneTransform(HumanBodyBones.RightLowerArm);
        }

        void LateUpdate()
        {
            if (hand == null || forearm == null) return;
            Vector3 along = (hand.position - forearm.position).normalized;
            Vector3 dir = Vector3.Slerp(along, Vector3.up, upBlend).normalized;
            Vector3 side = Vector3.Cross(dir, character.transform.forward);
            Vector3 up = side.sqrMagnitude > 1e-4f ? Vector3.Cross(side, dir) : character.transform.forward;
            transform.SetPositionAndRotation(hand.position + hand.rotation * gripOffset, Quaternion.LookRotation(up, dir));
        }
    }
}
