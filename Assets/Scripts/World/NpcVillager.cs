using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.Interaction;
using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>
    /// A villager: either strolls between waypoints (pausing, looking around) or sits on a stool fishing.
    /// Waves when the player comes close; "Talk" plays the next of their voiced lines.
    /// </summary>
    public sealed class NpcVillager : Interactable
    {
        static readonly int WalkingId = Animator.StringToHash("Walking");
        static readonly int TalkingId = Animator.StringToHash("Talking");
        static readonly int WaveId = Animator.StringToHash("Wave");

        [SerializeField] string npcId = "wati";
        [SerializeField] string displayName = "Martha";
        [SerializeField] Animator animator;
        [SerializeField] Vector3[] waypoints = new Vector3[0];
        [SerializeField] float walkSpeed = 1.05f;
        [Header("Fishing (sitters)")]
        [SerializeField] Transform rod, rodTip, bobber;
        [SerializeField] LineRenderer line;
        [SerializeField] Vector3 bobberRest;
        [Tooltip("One-shot animation triggers (animation pack) played now and then.")]
        [SerializeField] string[] extras = new string[0];
        float extraTimer = 20f;

        int next, lineIndex;
        float pause, talkTimer, waveCooldown, jigTimer, catchTimer, catchAnim;
        Transform player;
        Quaternion rodBase;

        public override string Prompt => Loc.T("talk");
        bool Sitter => rod != null;

        void Start()
        {
            var p = FindAnyObjectByType<Player.PlayerController>();
            if (p != null) player = p.transform;
            if (rod != null) rodBase = rod.localRotation;
            pause = Random.Range(1f, 4f);
            catchTimer = Random.Range(20f, 45f);
        }

        public override void Interact(PlayerInteractor who)
        {
            string key = $"{npcId}_{lineIndex}";
            lineIndex = (lineIndex + 1) % 3;
            Toast.Show($"<b>{displayName}:</b> {Loc.T(key)}", 5.5f);
            VoicePlayer.Say(key);
            talkTimer = 5f;
            animator.SetBool(TalkingId, true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 toPlayer = player != null ? player.position - transform.position : Vector3.forward * 99f;
            toPlayer.y = 0f;
            bool near = toPlayer.magnitude < 3.2f;

            waveCooldown -= dt;
            if (near && waveCooldown <= 0f)
            {
                animator.SetTrigger(WaveId);
                waveCooldown = 25f;
            }
            if (talkTimer > 0f)
            {
                talkTimer -= dt;
                if (talkTimer <= 0f) animator.SetBool(TalkingId, false);
            }

            if (Sitter)
            {
                UpdateFishing(dt);
                // Now and then a sitter dozes off for a moment.
                extraTimer -= dt;
                if (extraTimer <= 0f) { Play("SitDoze"); extraTimer = Random.Range(40f, 80f); }
                return;
            }

            // Walkers stop and turn to the player while they are close.
            if (near || talkTimer > 0f)
            {
                animator.SetBool(WalkingId, false);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(toPlayer.sqrMagnitude > 0.01f ? toPlayer : transform.forward), 240f * dt);
                return;
            }
            if (waypoints.Length < 2) return;
            if (pause > 0f)
            {
                pause -= dt;
                animator.SetBool(WalkingId, false);
                return;
            }
            Vector3 target = waypoints[next];
            Vector3 to = target - transform.position; to.y = 0f;
            if (to.magnitude < 0.25f)
            {
                next = (next + 1) % waypoints.Length;
                pause = Random.Range(1.5f, 5f);
                // Sometimes a little moment at the waypoint: a happy jump, a look around, a cheer.
                if (extras.Length > 0 && Random.value < 0.45f) { animator.SetTrigger(extras[Random.Range(0, extras.Length)]); pause += 2f; }
                return;
            }
            animator.SetBool(WalkingId, true);
            Vector3 step = to.normalized * walkSpeed * dt;
            Vector3 pos = transform.position + step;
            if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out var hit, 5f, ~0, QueryTriggerInteraction.Ignore)) pos.y = hit.point.y;
            transform.position = pos;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 180f * dt);
        }

        /// <summary>Sitters: the rod bobs gently, the float rides the water, and every so often a fish is landed.</summary>
        void UpdateFishing(float dt)
        {
            jigTimer += dt;
            catchTimer -= dt;
            float lift = 0f;
            if (catchTimer <= 0f) { catchAnim = 2.2f; catchTimer = Random.Range(30f, 60f); Play(Random.value < 0.6f ? "SitCheer" : "SitClap"); }
            if (catchAnim > 0f)
            {
                catchAnim -= dt;
                lift = Mathf.Sin(Mathf.Clamp01(1f - catchAnim / 2.2f) * Mathf.PI) * 35f;
            }
            float jig = Mathf.Sin(jigTimer * 1.3f) * 3f + (Mathf.PerlinNoise(jigTimer * 0.5f, 3f) > 0.75f ? 6f : 0f);
            rod.localRotation = rodBase * Quaternion.Euler(-(jig + lift), 0f, 0f);
            if (bobber != null)
            {
                Vector3 b = bobberRest;
                b.y = Mathf.Sin(Time.time * 2f) * 0.02f - (catchAnim > 1.2f ? 0.08f : 0f);
                if (catchAnim > 0f && catchAnim < 1.2f) b = Vector3.Lerp(rodTip.position, bobberRest, catchAnim / 1.2f);
                bobber.position = b;
            }
            if (line != null && rodTip != null && bobber != null)
            {
                line.positionCount = 8;
                Vector3 a = rodTip.position, c = bobber.position + Vector3.up * 0.04f;
                for (int i = 0; i < 8; i++)
                {
                    float t = i / 7f;
                    Vector3 p = Vector3.Lerp(a, c, t);
                    p.y -= Mathf.Sin(t * Mathf.PI) * 0.3f;
                    line.SetPosition(i, p);
                }
            }
        }

        void Play(string trigger)
        {
            foreach (var e in extras)
                if (e == trigger) { animator.SetTrigger(trigger); return; }
        }

#if UNITY_EDITOR
        public void EditorSetup(string id, string name, Animator a, Vector3[] path)
        {
            npcId = id; displayName = name; animator = a; waypoints = path;
        }

        public void EditorSetupExtras(string[] triggers, float speed) { extras = triggers; walkSpeed = speed; }

        public void EditorSetupFishing(Transform rodRoot, Transform tip, Transform floatT, LineRenderer lr, Vector3 rest)
        {
            rod = rodRoot; rodTip = tip; bobber = floatT; line = lr; bobberRest = rest;
        }
#endif
    }
}
