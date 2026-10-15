using System.Collections;
using System.IO;
using CozyFishing.Core;
using CozyFishing.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Walks the hero into every porch and dock edge and up/down the porch stairs, and checks they neither fall off
    /// nor get stuck. Runs with "-selftest-edges" in a player, or in the editor when Temp/edge_test.flag exists.
    /// Results are logged as "[EdgeTest]".
    /// </summary>
    public sealed class EdgeTest : MonoBehaviour
    {
        PlayerController walker;
        Transform cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            bool flag = Application.isEditor && File.Exists("Temp/edge_test.flag");
            if (!DevArgs.Has("-selftest-edges") && !flag) return;
            if (flag) File.Delete("Temp/edge_test.flag");
            var go = new GameObject("EdgeTest");
            DontDestroyOnLoad(go);
            go.AddComponent<EdgeTest>();
        }

        IEnumerator Start()
        {
            GameState.Instance.Data.seenCutscenes.Add("intro");
            if (SceneManager.GetActiveScene().name != "River") SceneManager.LoadScene("River");
            yield return new WaitForSeconds(3f);
            walker = FindAnyObjectByType<PlayerController>();
            cam = Camera.main.transform;

            var cabin = GameObject.Find("Props").transform.Find("Cabin");
            var dock = GameObject.Find("Props").transform.Find("Dock");
            Vector3 C(float x, float y, float z) => cabin.TransformPoint(new Vector3(x, y, z));
            Vector3 D(float x, float y, float z) => dock.TransformPoint(new Vector3(x, y, z));

            // Porch: try to walk off every open edge.
            yield return Push("porch-front-left", C(-1.0f, 0.5f, -2.3f), -cabin.forward, 2.5f, minY: C(0, 0.35f, 0).y);
            yield return Push("porch-front-right", C(1.7f, 0.5f, -2.3f), -cabin.forward, 2.5f, minY: C(0, 0.35f, 0).y);
            yield return Push("porch-left-side", C(-1.5f, 0.5f, -2.2f), -cabin.right, 2.5f, minY: C(0, 0.35f, 0).y);
            yield return Push("porch-right-side", C(1.5f, 0.5f, -2.2f), cabin.right, 2.5f, minY: C(0, 0.35f, 0).y);
            // Stairs: down from the porch, then back up from the ground.
            yield return Travel("stairs-down", C(0.9f, 0.5f, -2.3f), -cabin.forward, 2.5f, minMove: 1.4f);
            yield return Travel("stairs-up", C(0.9f, 0.2f, -4.6f), cabin.forward, 3f, minMove: 1.8f);
            // Dock: both sides and the far end.
            yield return Push("dock-left", D(-0.4f, 0.65f, 3f), -dock.right, 2.5f, minY: D(0, 0.45f, 0).y);
            yield return Push("dock-right", D(0.4f, 0.65f, 3f), dock.right, 2.5f, minY: D(0, 0.45f, 0).y);
            yield return Push("dock-end", D(0f, 0.65f, 4.5f), dock.forward, 2.5f, minY: D(0, 0.45f, 0).y);
            yield return Push("dock-boat-side", D(-0.6f, 0.65f, 4.4f), -dock.right, 2.5f, minY: D(0, 0.45f, 0).y);
            // Boat: row hard toward both ends of the river; it must stay inside the play area.
            var modes = FindAnyObjectByType<PlayerModes>();
            modes.Board();
            yield return new WaitForSeconds(1.5f);
            var boat = GameObject.Find("PlayerBoat").GetComponent<Rigidbody>();
            foreach (var (label, z, yaw) in new[] { ("boat-south-end", -44f, 180f), ("boat-north-end", 32f, 0f) })
            {
                boat.position = new Vector3(0.5f, -0.14f, z);
                boat.rotation = Quaternion.Euler(0f, yaw, 0f);
                boat.linearVelocity = Vector3.zero;
                float extreme = z;
                for (float t = 0f; t < 8f; t += Time.deltaTime)
                {
                    GameInput.Instance.SetTouchMove(new Vector2(0f, 1f));
                    extreme = yaw > 90f ? Mathf.Min(extreme, boat.position.z) : Mathf.Max(extreme, boat.position.z);
                    yield return null;
                }
                GameInput.Instance.SetTouchMove(Vector2.zero);
                bool ok = extreme > -52f + 2.5f && extreme < 39f - 2.5f;
                Debug.Log($"[EdgeTest] {label}: {(ok ? "OK" : "LEFT MAP")} furthest z={extreme:0.0} (area -52..39)");
            }
            Debug.Log("[EdgeTest] done");
            if (!Application.isEditor) Application.Quit();
        }

        /// <summary>Hold the stick toward an edge; the hero must stay above <paramref name="minY"/>.</summary>
        IEnumerator Push(string label, Vector3 start, Vector3 dir, float seconds, float minY)
        {
            walker.Teleport(start, Quaternion.LookRotation(dir));
            yield return new WaitForSeconds(0.4f);
            float lowest = float.MaxValue;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Steer(dir);
                lowest = Mathf.Min(lowest, walker.transform.position.y);
                yield return null;
            }
            GameInput.Instance.SetTouchMove(Vector2.zero);
            bool ok = lowest >= minY;
            Debug.Log($"[EdgeTest] {label}: {(ok ? "OK" : "FELL")} lowest={lowest:0.00} limit={minY:0.00} end={walker.transform.position}");
        }

        /// <summary>Walk a route (e.g. the stairs): the hero must cover at least <paramref name="minMove"/> metres.</summary>
        IEnumerator Travel(string label, Vector3 start, Vector3 dir, float seconds, float minMove)
        {
            walker.Teleport(start, Quaternion.LookRotation(dir));
            yield return new WaitForSeconds(0.4f);
            Vector3 from = walker.transform.position;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Steer(dir);
                yield return null;
            }
            GameInput.Instance.SetTouchMove(Vector2.zero);
            Vector3 d = walker.transform.position - from; d.y = 0f;
            bool ok = d.magnitude >= minMove;
            Debug.Log($"[EdgeTest] {label}: {(ok ? "OK" : "STUCK")} moved={d.magnitude:0.00} dy={walker.transform.position.y - from.y:0.00}");
        }

        void Steer(Vector3 worldDir)
        {
            // The stick is camera-relative: convert the wanted world direction into stick space.
            Vector3 f = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
            Vector3 r = Vector3.Cross(Vector3.up, f);
            worldDir.y = 0f; worldDir.Normalize();
            GameInput.Instance.SetTouchMove(new Vector2(Vector3.Dot(worldDir, r), Vector3.Dot(worldDir, f)) * 0.6f);
        }
    }
}
