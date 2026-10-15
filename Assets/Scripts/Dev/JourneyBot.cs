#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using CozyFishing.Core;
using CozyFishing.Fishing;
using CozyFishing.Interaction;
using CozyFishing.Player;
using UnityEngine;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Editor-only play-test: walks the player to the boat with the virtual joystick, boards, rows, then fishes.
    /// Saves screenshots to Docs/Progress/journey_*.png. Add to any object in Play Mode.
    /// </summary>
    public sealed class JourneyBot : MonoBehaviour
    {
        IEnumerator Start()
        {
            var input = GameInput.Instance;
            var modes = FindAnyObjectByType<PlayerModes>();
            var player = modes.transform;
            var board = FindAnyObjectByType<BoardBoatPoint>().transform;

            Debug.Log("[Journey] walking to the boat");
            var dock = GameObject.Find("Props").transform.Cast<Transform>().First(t => t.name.StartsWith("Dock"));
            Vector3[] waypoints = { dock.position + dock.forward * 0.4f, dock.position + dock.forward * 3.2f, board.position };
            float timeout = Time.time + 30f;
            int shots = 0, wp = 0;
            while (Time.time < timeout && wp < waypoints.Length)
            {
                Vector3 d = waypoints[wp] - player.position;
                d.y = 0f;
                if (d.magnitude < (wp == waypoints.Length - 1 ? 1.0f : 0.5f)) { wp++; continue; }
                input.SetTouchMove(new Vector2(d.x, d.z).normalized * (d.magnitude > 2f ? 1f : 0.55f));
                if (shots == 0 && Time.time > timeout - 28.5f) { Shot("walk"); shots++; }
                yield return null;
            }
            input.SetTouchMove(Vector2.zero);
            yield return new WaitForSeconds(0.4f);
            var interactor = player.GetComponent<PlayerInteractor>();
            Debug.Log($"[Journey] at boat, prompt='{(interactor.Current ? interactor.Current.Prompt : "none")}'");
            Shot("dock");
            input.SetTouchAction(true);
            yield return null;
            yield return null;
            input.SetTouchAction(false);
            yield return new WaitForSeconds(1.5f);
            Debug.Log($"[Journey] inBoat={modes.InBoat}");
            Shot("seated");

            Debug.Log("[Journey] rowing");
            input.SetTouchMove(new Vector2(0.25f, 1f));
            for (int i = 0; i < 4; i++)
            {
                yield return new WaitForSeconds(0.55f);
                Shot($"row{i}");
            }
            input.SetTouchMove(Vector2.zero);
            yield return new WaitForSeconds(1f);

            Debug.Log("[Journey] fishing");
            var bot = FindAnyObjectByType<FishingController>().gameObject.AddComponent<FishingAutoPlayer>();
            yield return new WaitForSeconds(35f);
            Debug.Log("[Journey] done");
        }

        static void Shot(string label)
        {
            ScreenCapture.CaptureScreenshot($"Docs/Progress/journey_{label}.png");
            Debug.Log($"[Journey] shot {label}");
        }
    }
}
#endif
