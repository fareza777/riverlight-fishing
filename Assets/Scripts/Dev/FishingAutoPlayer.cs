#if UNITY_EDITOR
using System.Collections;
using CozyFishing.Fishing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Editor-only play-test bot: moves the boat into a fishing spot and plays the fishing loop
    /// through simulated keyboard input, logging every state change. Add it to PlayerBoat in Play Mode.
    /// </summary>
    public sealed class FishingAutoPlayer : MonoBehaviour
    {
        [SerializeField] int rounds = 2;
        FishingController fishing;
        bool spaceDown;

        IEnumerator Start()
        {
            fishing = GetComponent<FishingController>();
            fishing.StateChanged += s =>
            {
                Debug.Log($"[AutoPlayer] state -> {s} (t={Time.time:0.0})");
                if (s is FishingState.Landing or FishingState.Reeling or FishingState.Charging) StartCoroutine(Capture(s));
            };
            fishing.Caught += (f, isNew) => Debug.Log($"[AutoPlayer] CAUGHT {f.Species.DisplayName} {f.SizeCm}cm new={isNew} creel={fishing.Creel.Count}");
            fishing.Lost += r => Debug.Log($"[AutoPlayer] LOST: {r}");

            var modes = FindAnyObjectByType<CozyFishing.Player.PlayerModes>();
            if (modes != null && !modes.InBoat)
            {
                modes.Board();
                yield return new WaitForSeconds(1.2f);
            }
            var spot = FindAnyObjectByType<FishingSpot>();
            var body = GetComponent<Rigidbody>();
            body.position = spot.transform.position + new Vector3(0f, -0.14f, 0f);
            body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(0.5f);
            Debug.Log($"[AutoPlayer] in spot '{fishing.CurrentSpot?.SpotName}' canCast={fishing.CanCast}");

            for (int i = 0; i < rounds; i++)
            {
                SetSpace(true);
                yield return new WaitForSeconds(0.55f);
                SetSpace(false);
                float timeout = Time.time + 15f;
                while (fishing.State != FishingState.Bite && Time.time < timeout) yield return null;
                yield return new WaitForSeconds(0.25f);
                SetSpace(true);
                yield return null;
                SetSpace(false);
                while (fishing.State == FishingState.Reeling)
                {
                    float mid = (fishing.SafeBand.x + fishing.SafeBand.y) * 0.5f;
                    SetSpace(fishing.Tension < mid);
                    yield return null;
                }
                SetSpace(false);
                yield return new WaitForSeconds(5.5f);
                Debug.Log($"[AutoPlayer] 5.5s after catch, state={fishing.State} (card should still be open)");
                if (fishing.State == FishingState.Landing)
                {
                    SetSpace(true); yield return null; SetSpace(false);
                }
                yield return new WaitForSeconds(0.6f);
            }
            Debug.Log("[AutoPlayer] done");
        }

        IEnumerator Capture(FishingState s)
        {
            yield return new WaitForSeconds(s == FishingState.Reeling ? 2.5f : 0.45f);
            ScreenCapture.CaptureScreenshot($"Docs/Progress/autoplay_{s}_{Time.frameCount}.png");
        }

        void SetSpace(bool down)
        {
            if (Keyboard.current == null || down == spaceDown) return;
            spaceDown = down;
            InputSystem.QueueStateEvent(Keyboard.current, down ? new KeyboardState(Key.Space) : new KeyboardState());
        }
    }
}
#endif
