using System;
using System.Collections.Generic;
using CozyFishing.Audio;
using CozyFishing.Boat;
using CozyFishing.CameraSystem;
using CozyFishing.Core;
using CozyFishing.World;
using UnityEngine;

namespace CozyFishing.Fishing
{
    public enum FishingState { Rowing, Readying, Charging, Casting, Waiting, Bite, Reeling, Landing }

    /// <summary>
    /// Cast -> wait -> bite -> reel loop.
    /// Reeling is a tension game: hold to reel (tension rises, fish comes closer), release to let line out.
    /// Keep the needle in the safe band; above the band the line snaps, slack for too long and the fish escapes.
    /// </summary>
    // The boat is optional: the same controller also fishes from the dock (no BoatController on that stand).
    public sealed class FishingController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] Creel creel;
        [SerializeField] RowingAnimator rowing;
        [SerializeField] CameraRig cameraRig;
        [SerializeField] TimeOfDay timeOfDay;
        [SerializeField] Transform rodPivot;
        [Tooltip("Rod lying in the boat; hidden while the rod is in the hand.")]
        [SerializeField] GameObject stowedRod;
        [SerializeField] Transform rodTip;
        [SerializeField] Transform bobber;
        [SerializeField] LineRenderer line;
        [SerializeField] List<FishSpecies> species = new();

        [Header("Cast")]
        [SerializeField] float minCastDistance = 3f;
        [SerializeField] float maxCastDistance = 10f;
        [SerializeField] float chargeCycleSeconds = 1.6f;
        [SerializeField] float castFlightSeconds = 0.75f;

        [Header("Bite")]
        [SerializeField] Vector2 biteDelay = new(2.5f, 6.5f);
        [SerializeField] float biteWindow = 1.1f;

        [Header("Reel")]
        [SerializeField] float tensionRise = 0.55f;
        [SerializeField] float tensionFall = 0.45f;
        [SerializeField] float reelSpeed = 0.3f;
        [SerializeField] float slackEscapeSeconds = 2.2f;
        [SerializeField] Vector2 safeBand = new(0.22f, 0.78f);

        [Header("Challenge")]
        [Tooltip("Small false bites before the real one; striking on a nibble spooks the fish.")]
        [SerializeField] Vector2Int nibbles = new(0, 3);
        [Tooltip("Seconds to strike, per rarity: common, uncommon, rare, legendary.")]
        [SerializeField] float[] strikeWindow = { 1.0f, 0.85f, 0.7f, 0.6f };
        [SerializeField] float runWarningSeconds = 0.6f;
        [SerializeField] float overTensionGrace = 0.25f;
        [Tooltip("Dock fishing: cast straight ahead (the stand faces the water) instead of toward the spot.")]
        [SerializeField] bool castAhead;

        public event Action<FishingState> StateChanged;
        public event Action<CaughtFish, bool> Caught;
        public event Action<string> Lost;
        public event Action CreelFull;

        public FishingState State { get; private set; } = FishingState.Rowing;
        public FishingSpot CurrentSpot { get; private set; }
        public float Charge { get; private set; }
        public float Tension { get; private set; }
        public float Progress { get; private set; }
        /// <summary>Safe tension band, widened by the equipped rod.</summary>
        public Vector2 SafeBand => State == FishingState.Reeling ? liveBand : BaseBand(0f);
        /// <summary>The fish is about to run (warning) or running: the player should stop reeling.</summary>
        public bool FishRunning => runWarning > 0f || runTime > 0f;
        public bool Nibbling => nibbleTime > 0f;

        Vector2 liveBand;
        float strength, hookedSize, runWarning, runTime, runCooldown, overTension, nibbleTime, bandSeed;
        readonly List<float> nibbleAt = new();

        /// <summary>Safe tension band: narrower against strong fish, widened by the equipped rod.</summary>
        Vector2 BaseBand(float fishStrength)
        {
            float bonus = GameState.Instance.Rod != null ? GameState.Instance.Rod.safeBandBonus : 0f;
            float width = (safeBand.y - safeBand.x) * Mathf.Lerp(1f, 0.5f, fishStrength) + bonus;
            return new Vector2(0.5f - width * 0.5f, 0.5f + width * 0.5f);
        }
        public FishSpecies Hooked => hooked;
        public Creel Creel => creel;
        public bool CanCast => CastAllowed && State == FishingState.Rowing && CurrentSpot != null;
        public bool CastAllowed { get; set; } = true;
        /// <summary>0..1 through the cast animation (wind-up + throw).</summary>
        public float CastProgress { get; private set; }
        /// <summary>0..1..0 while the hand reaches for the stowed rod (drives the pick-up IK).</summary>
        public float PickupReach { get; private set; }
        public Transform StowedRod => stowedRod ? stowedRod.transform : null;
        public Transform Bobber => bobber;
        /// <summary>Where the rower should face while fishing (toward the spot).</summary>
        public Vector3 AimPoint { get; private set; }

        BoatController boat;
        float stateTime;
        float waitTarget;
        float slackTime;
        float fishPull;
        float pullTimer;
        FishSpecies hooked;
        Vector3 castStart;
        Vector3 castTarget;
        float castWindup = 0.28f;
        const float ReadySeconds = 0.9f;
        bool releasedEarly;
        float reelClick;

        void Awake()
        {
            boat = GetComponent<BoatController>();
            SetRodVisible(false);
        }

        void Start()
        {
            // Species come from the game database for this location unless the scene overrides them.
            if (species.Count == 0)
            {
                var loc = GameState.Db.LocationForScene(gameObject.scene.name);
                string id = loc != null ? loc.id : GameState.Instance.Data.location;
                species.AddRange(GameState.Db.FishAt(id));
            }
        }

        void Update()
        {
            stateTime += Time.deltaTime;
            var input = GameInput.Instance;
            if (input == null) return;

            if (State != FishingState.Rowing && State != FishingState.Landing && input.CancelPressed)
            {
                Fail(State == FishingState.Reeling ? Loc.T("let_go") : null);
                return;
            }

            switch (State)
            {
                case FishingState.Rowing:
                    if (input.ActionPressed && CanCast)
                    {
                        if (creel != null && creel.IsFull) { CreelFull?.Invoke(); break; }
                        releasedEarly = false;
                        SfxPlayer.Play("rod_pickup", 0.7f);
                        AimPoint = castAhead ? transform.position + transform.forward * 7f
                            : CurrentSpot != null ? CurrentSpot.transform.position : transform.position - transform.forward * 5f;
                        Enter(FishingState.Readying);
                    }
                    break;
                case FishingState.Readying:
                    // Ship the oars, reach for the rod (hand IK peaks mid-way), turn the boat toward the spot.
                    float k = stateTime / ReadySeconds;
                    PickupReach = Mathf.Sin(Mathf.Clamp01(k) * Mathf.PI);
                    if (k >= 0.5f && stowedRod && stowedRod.activeSelf) SetRodVisible(true);
                    if (input.ActionReleased) releasedEarly = true;
                    if (k >= 1f) { PickupReach = 0f; Enter(FishingState.Charging); }
                    break;
                case FishingState.Charging:
                    Charge = Mathf.PingPong(stateTime / chargeCycleSeconds * 2f, 1f);
                    if (releasedEarly && stateTime > 0.35f) BeginCast();
                    else if (!releasedEarly && input.ActionReleased) BeginCast();
                    break;
                case FishingState.Casting:
                    UpdateCast();
                    break;
                case FishingState.Waiting:
                    UpdateNibbles();
                    if (input.ActionPressed)
                    {
                        // Striking before the real bite (on a nibble or on nothing) scares the fish off.
                        Fail(Loc.T("spooked"));
                        break;
                    }
                    if (stateTime >= waitTarget) BeginBite();
                    break;
                case FishingState.Bite:
                    FloatBobber(Mathf.Sin(stateTime * 30f) * 0.08f - 0.12f);
                    if (input.ActionPressed) BeginReel();
                    else if (stateTime > StrikeWindow) Fail(Loc.T("slow"));
                    break;
                case FishingState.Reeling:
                    UpdateReel(input.ActionHeld);
                    break;
                case FishingState.Landing:
                    // The catch card stays until the player dismisses it (tap the card or press the action button).
                    if (stateTime > 0.5f && input.ActionPressed) Enter(FishingState.Rowing);
                    break;
            }
            UpdateLine();
        }

        void Enter(FishingState next)
        {
            State = next;
            stateTime = 0f;
            bool fishing = next != FishingState.Rowing;
            if (boat) boat.InputLocked = fishing;
            if (rowing) rowing.Resting = fishing;
            if (cameraRig) cameraRig.ZoomTarget = next is FishingState.Reeling or FishingState.Bite ? 1f : fishing ? 0.45f : 0f;
            // The rod changes hands mid-way through Readying (see Update); otherwise it is in hand while fishing.
            if (next != FishingState.Readying) SetRodVisible(fishing);
            if (boat) boat.AimTarget = fishing ? AimPoint : (Vector3?)null;
            if (next == FishingState.Rowing)
            {
                Charge = 0f;
                if (bobber) bobber.gameObject.SetActive(false);
            }
            StateChanged?.Invoke(next);
        }

        void BeginCast()
        {
            Vector3 origin = transform.position;
            Vector3 dir = castAhead ? transform.forward : CurrentSpot != null ? CurrentSpot.transform.position - origin : -transform.forward;
            dir.y = 0f;
            // Sitting right on top of the spot: cast off the stern. (Not for dock casts, whose direction is a unit vector.)
            if (!castAhead && dir.sqrMagnitude < 2.25f) dir = -transform.forward;
            dir.Normalize();
            float distance = Mathf.Lerp(minCastDistance, maxCastDistance, Charge);
            castStart = rodTip ? rodTip.position : origin + Vector3.up;
            CastProgress = 0f;
            castTarget = origin + dir * distance;
            castTarget.y = 0.02f;
            if (bobber) { bobber.gameObject.SetActive(true); bobber.position = castStart; }
            SfxPlayer.Play("cast", 0.9f);
            Enter(FishingState.Casting);
        }

        void UpdateCast()
        {
            CastProgress = Mathf.Clamp01(stateTime / (castWindup + castFlightSeconds));
            float t = Mathf.Clamp01((stateTime - castWindup) / castFlightSeconds);
            if (stateTime < castWindup)
            {
                if (bobber && rodTip) bobber.position = rodTip.position;
                return;
            }
            if (bobber)
            {
                if (rodTip && t < 0.05f) castStart = rodTip.position;
                Vector3 p = Vector3.Lerp(castStart, castTarget, t);
                p.y += Mathf.Sin(t * Mathf.PI) * (1.2f + Vector3.Distance(castStart, castTarget) * 0.25f);
                bobber.position = p;
            }
            if (t >= 1f)
            {
                SfxPlayer.Play("splash_small", 0.8f);
                GameState.Instance.ConsumeBait();
                waitTarget = UnityEngine.Random.Range(biteDelay.x, biteDelay.y);
                ScheduleNibbles();
                Enter(FishingState.Waiting);
            }
        }

        void BeginBite()
        {
            hooked = PickSpecies();
            if (hooked == null)
            {
                waitTarget = stateTime + UnityEngine.Random.Range(biteDelay.x, biteDelay.y);
                ScheduleNibbles(stateTime);
                return;
            }
            SfxPlayer.Play("bite", 1f);
            Settings.Vibrate();
            Enter(FishingState.Bite);
        }

        void BeginReel()
        {
            Tension = 0.45f;
            Progress = 0f;
            slackTime = 0f;
            fishPull = 0f;
            pullTimer = 0f;
            // Size is decided on the hook: big fish pull harder, narrow the safe band and make it wander more.
            var range = hooked.SizeRangeCm;
            hookedSize = Mathf.Round(UnityEngine.Random.Range(range.x, range.y) * 10f) / 10f;
            strength = Mathf.Clamp01(hooked.Fight * Mathf.Lerp(0.85f, 1.25f, Mathf.InverseLerp(range.x, range.y, hookedSize)));
            runWarning = runTime = overTension = 0f;
            runCooldown = UnityEngine.Random.Range(1.5f, 3f);
            bandSeed = UnityEngine.Random.Range(0f, 100f);
            liveBand = BaseBand(strength);
            SfxPlayer.Play("hook", 0.9f);
            Enter(FishingState.Reeling);
        }

        void UpdateReel(bool reeling)
        {
            float dt = Time.deltaTime;
            float fight = strength;

            // The fish surges in bursts; bigger fighters surge harder and more often.
            pullTimer -= dt;
            if (pullTimer <= 0f)
            {
                pullTimer = UnityEngine.Random.Range(0.6f, 1.8f) * Mathf.Lerp(1.3f, 0.6f, fight);
                fishPull = UnityEngine.Random.Range(0.1f, 0.55f) * Mathf.Lerp(0.6f, 1.4f, fight);
            }
            fishPull = Mathf.MoveTowards(fishPull, 0f, dt * 0.5f);
            UpdateRun(dt, fight);

            // During a run the fish takes line: reeling against it spikes the tension.
            float rise = runTime > 0f ? tensionRise * 2.2f : tensionRise;
            float runPull = runTime > 0f ? Mathf.Lerp(0.35f, 0.9f, fight) : 0f;
            // Letting go always eases the line (the drag gives line): the fish's pull then costs progress instead,
            // so releasing on a red/orange bar reliably saves it. Over the limit the drag slips faster.
            if (reeling) Tension += (rise + fishPull + runPull) * dt;
            else
            {
                Tension -= tensionFall * (Tension > liveBand.y ? 2.5f : 1f) * dt;
                Progress -= (fishPull + runPull) * 0.04f * dt;
            }
            Tension = Mathf.Max(0f, Tension);

            // The safe band drifts as the fish changes direction (more for strong fish).
            var baseBand = BaseBand(strength);
            float drift = (Mathf.PerlinNoise(bandSeed, Time.time * Mathf.Lerp(0.25f, 0.7f, fight)) - 0.5f) * 2f * Mathf.Lerp(0.04f, 0.2f, fight);
            float half = (baseBand.y - baseBand.x) * 0.5f;
            float centre = Mathf.Clamp(0.5f + drift, half + 0.04f, 0.96f - half);
            liveBand = new Vector2(centre - half, centre + half);

            var rod = GameState.Instance.Rod;
            float rodBonus = rod != null ? rod.reelBonus : 0f;
            var band = liveBand;
            bool inBand = Tension >= band.x && Tension <= band.y;
            if (runTime > 0f) Progress -= 0.035f * dt;
            else if (reeling && inBand) Progress += reelSpeed * (1f + rodBonus) * Mathf.Lerp(1.15f, 0.75f, fight) * dt;
            reelClick -= dt;
            if (reeling && reelClick <= 0f) { SfxPlayer.Play("reel_tick", 0.35f, 0.15f); reelClick = 0.12f; }
            else if (!reeling) Progress -= 0.02f * dt;
            Progress = Mathf.Clamp01(Progress);

            // A running fish keeps the line taut even when the player lets go, so that is never slack.
            slackTime = !FishRunning && Tension < band.x * 0.5f ? slackTime + dt : 0f;
            // A legend on a rod that is too weak: the line gives way once the fight gets serious.
            if (hooked != null && hooked.IsLegend && (rod == null || rod.tier < hooked.RequiredRodTier) && Progress > 0.35f)
            {
                SfxPlayer.Play("snap", 1f);
                Fail(Loc.T("rod_weak"));
                return;
            }

            if (bobber)
            {
                // The fish comes in to the stern of the boat, or to the edge of the dock in front of the angler.
                Vector3 landing = transform.position + transform.forward * (castAhead ? 2.6f : -1.2f);
                Vector3 target = Vector3.Lerp(castTarget, landing, Progress);
                target += new Vector3(Mathf.Sin(Time.time * 3.1f), 0f, Mathf.Cos(Time.time * 2.3f)) * 0.4f * (1f - Progress);
                bobber.position = Vector3.Lerp(bobber.position, new Vector3(target.x, -0.05f, target.z), dt * 4f);
            }

            // Only reeling over the limit snaps the line; the moment the player lets go it is safe.
            overTension = reeling && Tension > 1f ? overTension + dt : 0f;
            Tension = Mathf.Min(Tension, 1.15f);
            if (overTension > overTensionGrace) { SfxPlayer.Play("snap", 1f); Fail(Loc.T("snap")); }
            else if (slackTime > slackEscapeSeconds) Fail(Loc.T("slack"));
            else if (Progress >= 1f) Land();
        }

        void Land()
        {
            var fish = new CaughtFish(hooked, hookedSize);
            bool isNew = creel != null && creel.Add(fish);
            SfxPlayer.Play(hooked.IsLegend ? "catch_legend" : "catch", 1f, 0f);
            SfxPlayer.Play("splash_big", 0.8f);
            Settings.Vibrate();
            if (hooked.Model != null && bobber != null) StartCoroutine(FishLeap(hooked.Model, bobber.position, fish.SizeCm));
            Enter(FishingState.Landing);
            Caught?.Invoke(fish, isNew);
            if (creel != null && creel.IsFull) CreelFull?.Invoke();
        }

        /// <summary>The landed fish leaps out of the water in an arc and drops into the boat.</summary>
        System.Collections.IEnumerator FishLeap(GameObject model, Vector3 from, float sizeCm)
        {
            var fish = Instantiate(model);
            foreach (var c in fish.GetComponentsInChildren<Collider>()) Destroy(c);
            float scale = Mathf.Clamp(sizeCm / 30f, 0.7f, 1.8f);
            fish.transform.localScale = Vector3.one * scale;
            const float duration = 0.9f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                Vector3 to = transform.position + Vector3.up * 0.6f;
                Vector3 p = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, k));
                p.y += Mathf.Sin(k * Mathf.PI) * 1.6f;
                Vector3 vel = (to - from).normalized + Vector3.up * Mathf.Cos(k * Mathf.PI) * 1.5f;
                fish.transform.SetPositionAndRotation(p, Quaternion.LookRotation(vel.normalized, Vector3.up) * Quaternion.Euler(0f, Mathf.Sin(t * 30f) * 25f, 0f));
                yield return null;
            }
            Destroy(fish);
        }

        /// <summary>Called by the catch card when tapped.</summary>
        public void ConfirmCatch()
        {
            if (State == FishingState.Landing && stateTime > 0.5f) Enter(FishingState.Rowing);
        }

        void Fail(string reason)
        {
            hooked = null;
            Enter(FishingState.Rowing);
            if (!string.IsNullOrEmpty(reason)) Lost?.Invoke(reason);
        }

        FishSpecies PickSpecies()
        {
            float hour = timeOfDay != null ? timeOfDay.Hour : 12f;
            Habitat habitat = CurrentSpot != null ? CurrentSpot.Habitat : Habitat.River;
            int chapter = GameState.Instance.Data.chapter;
            var bait = GameState.Instance.ActiveBait;
            float rare = 1f + (bait != null ? bait.rareBonus : 0f);
            float total = 0f;
            foreach (var s in species) total += WeightOf(s, hour, habitat, chapter, rare);
            if (total <= 0f) return null;
            float roll = UnityEngine.Random.value * total;
            foreach (var s in species)
            {
                float w = WeightOf(s, hour, habitat, chapter, rare);
                if (w <= 0f) continue;
                roll -= w;
                if (roll <= 0f) return s;
            }
            return null;
        }

        static float WeightOf(FishSpecies s, float hour, Habitat habitat, int chapter, float rare)
        {
            if (s == null || (s.Habitats & habitat) == 0 || !s.IsActiveAt(hour)) return 0f;
            if (s.IsLegend)
            {
                // Legends only rise for the chapter that seeks them (and stay catchable afterwards, rarely).
                if (chapter < s.Chapter) return 0f;
                if (chapter == s.Chapter && !Progression.LegendReady(chapter)) return 0f;
                return chapter == s.Chapter ? 2.5f * rare : 0.4f * rare;
            }
            return s.Rarity == FishRarity.Common ? s.Weight : s.Weight * rare;
        }

        float StrikeWindow => hooked != null ? strikeWindow[Mathf.Clamp((int)hooked.Rarity, 0, strikeWindow.Length - 1)] : biteWindow;

        void ScheduleNibbles(float from = 0f)
        {
            nibbleAt.Clear();
            int n = UnityEngine.Random.Range(nibbles.x, nibbles.y + 1);
            for (int i = 0; i < n; i++) nibbleAt.Add(Mathf.Lerp(from + 0.8f, waitTarget - 0.6f, (i + UnityEngine.Random.Range(0.2f, 0.8f)) / n));
        }

        /// <summary>Little false bites: a quick shallow dip and a soft plop. Only the real bite pulls the bobber under.</summary>
        void UpdateNibbles()
        {
            nibbleTime = Mathf.Max(0f, nibbleTime - Time.deltaTime);
            if (nibbleAt.Count > 0 && stateTime >= nibbleAt[0])
            {
                nibbleAt.RemoveAt(0);
                nibbleTime = 0.35f;
                SfxPlayer.Play("bite", 0.3f, 0.2f);
            }
            FloatBobber(nibbleTime > 0f ? -Mathf.Sin(nibbleTime / 0.35f * Mathf.PI) * 0.045f : 0f);
        }

        /// <summary>Strong fish make runs: a short warning, then they take line for a second or two.</summary>
        void UpdateRun(float dt, float fight)
        {
            if (runTime > 0f)
            {
                runTime -= dt;
                if (runTime <= 0f) runCooldown = UnityEngine.Random.Range(3f, 5f) * Mathf.Lerp(1.6f, 0.7f, fight);
                return;
            }
            if (runWarning > 0f)
            {
                runWarning -= dt;
                if (runWarning <= 0f) runTime = UnityEngine.Random.Range(0.8f, 1.8f) * Mathf.Lerp(0.8f, 1.2f, fight);
                return;
            }
            if (fight < 0.3f) return;   // small fish never run
            runCooldown -= dt;
            if (runCooldown <= 0f)
            {
                runWarning = runWarningSeconds;
                Settings.Vibrate();
                SfxPlayer.Play("reel_tick", 0.8f, 0.3f);
            }
        }

        /// <summary>Dock fishing has no boat to drift into a spot: the stand point names its spot directly.</summary>
        public void SetSpot(FishingSpot spot) => CurrentSpot = spot;

        void FloatBobber(float extraY)
        {
            if (!bobber) return;
            Vector3 p = bobber.position;
            p.y = Mathf.Sin(Time.time * 2.2f) * 0.025f + extraY;
            bobber.position = p;
        }

        void UpdateLine()
        {
            if (!line) return;
            bool show = State is FishingState.Casting or FishingState.Waiting or FishingState.Bite or FishingState.Reeling;
            line.enabled = show && rodTip && bobber;
            if (!line.enabled) return;
            const int points = 16;
            line.positionCount = points;
            Vector3 a = rodTip.position, b = bobber.position + Vector3.up * 0.05f;
            float sag = State == FishingState.Reeling ? Mathf.Lerp(0.6f, 0.02f, Tension) : 0.5f;
            for (int i = 0; i < points; i++)
            {
                float t = i / (points - 1f);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                line.SetPosition(i, p);
            }
        }

        void SetRodVisible(bool visible)
        {
            if (rodPivot) rodPivot.gameObject.SetActive(visible);
            if (stowedRod) stowedRod.SetActive(!visible);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out FishingSpot spot)) CurrentSpot = spot;
        }

        void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out FishingSpot spot) && spot == CurrentSpot) CurrentSpot = null;
        }
    }
}
