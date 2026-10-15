using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>Mira the market keeper: idles at her stall, turns to the player, waves when they come close and talks while the shop is open.</summary>
    public sealed class NpcMira : MonoBehaviour
    {
        static readonly int WaveId = Animator.StringToHash("Wave");
        static readonly int TalkId = Animator.StringToHash("Talking");

        [SerializeField] Animator animator;
        [SerializeField] float noticeRadius = 7f;
        [SerializeField] float turnSpeed = 120f;

        Transform player;
        Quaternion restRotation;
        bool near;

        void Start()
        {
            restRotation = transform.rotation;
            var p = FindAnyObjectByType<Player.PlayerController>();
            if (p != null) player = p.transform;
        }

        void Update()
        {
            if (player == null || animator == null) return;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            bool nowNear = to.magnitude < noticeRadius;
            if (nowNear && !near) animator.SetTrigger(WaveId);
            near = nowNear;
            animator.SetBool(TalkId, ShopUI.IsOpen);
            var want = near && to.sqrMagnitude > 0.01f ? Quaternion.LookRotation(to) : restRotation;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        }

        /// <summary>Mira cheers when an order is delivered.</summary>
        public void Cheer()
        {
            foreach (var p in animator.parameters)
                if (p.name == "Cheer") { animator.SetTrigger("Cheer"); return; }
        }

#if UNITY_EDITOR
        public void EditorSetup(Animator a) => animator = a;
#endif
    }
}
