using System.Collections;
using System.IO;
using System.Text;
using CozyFishing.Boat;
using CozyFishing.Core;
using CozyFishing.Player;
using UnityEngine;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Runtime self-test, only active when the player is launched with "-selftest-rowing".
    /// Boards the boat, rows in open water, writes screenshots and a CSV of both oar grips/blade tips to
    /// Application.persistentDataPath/selftest, then quits. Lets rowing be verified in the real player build.
    /// </summary>
    public sealed class BuildSelfTest : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!DevArgs.Has("-selftest-rowing")) return;
            new GameObject("BuildSelfTest").AddComponent<BuildSelfTest>();
        }

        IEnumerator Start()
        {
            string dir = Path.Combine(Application.persistentDataPath, "selftest");
            Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            DontDestroyOnLoad(gameObject);
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "River")
            {
                // The player starts on the Title scene: go straight to the river with the intro already seen.
                GameState.Instance.Data.seenCutscenes.Add("intro");
                UnityEngine.SceneManagement.SceneManager.LoadScene("River");
                yield return null;
            }
            yield return new WaitForSeconds(2f);

            var modes = FindAnyObjectByType<PlayerModes>();
            modes.Board();
            yield return new WaitForSeconds(1.5f);
            var boat = GameObject.Find("PlayerBoat");
            var rb = boat.GetComponent<Rigidbody>();
            rb.position = new Vector3(-3.5f, -0.2f, -42f);
            rb.rotation = Quaternion.identity;
            boat.transform.SetPositionAndRotation(rb.position, rb.rotation);
            FindAnyObjectByType<CameraSystem.CameraRig>().Snap();

            var oars = FindAnyObjectByType<RowingAnimator>();
            Transform tipL = Tip(oars, oars.GripLeft), tipR = Tip(oars, oars.GripRight);
            var csv = new StringBuilder("t,gripLx,gripLy,gripLz,gripRx,gripRy,gripRz,tipLx,tipLy,tipLz,tipRx,tipRy,tipRz\n");
            GameInput.Instance.SetTouchMove(new Vector2(0f, 1f));
            yield return new WaitForSeconds(1.2f);
            float t0 = Time.time;
            int frame = 0, shot = 0;
            while (Time.time - t0 < 3f)
            {
                yield return new WaitForEndOfFrame();
                var b = oars.transform;
                Vector3 gl = b.InverseTransformPoint(oars.GripLeft.position), gr = b.InverseTransformPoint(oars.GripRight.position);
                Vector3 tl = b.InverseTransformPoint(tipL.position), tr = b.InverseTransformPoint(tipR.position);
                csv.AppendLine(System.FormattableString.Invariant($"{Time.time - t0:0.000},{gl.x:0.000},{gl.y:0.000},{gl.z:0.000},{gr.x:0.000},{gr.y:0.000},{gr.z:0.000},{tl.x:0.000},{tl.y:0.000},{tl.z:0.000},{tr.x:0.000},{tr.y:0.000},{tr.z:0.000},{tipL.position.y:0.000},{tipR.position.y:0.000}"));
                if (frame++ % 6 == 0) ScreenCapture.CaptureScreenshot(Path.Combine(dir, $"f{shot++:000}.png"));
            }
            File.WriteAllText(Path.Combine(dir, "oars_raw.csv"), csv.ToString());
            Debug.Log($"[SelfTest] rowing done, {shot} shots -> {dir}");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        /// <summary>Blade tip baked by the scene builder.</summary>
        static Transform Tip(RowingAnimator oars, Transform grip) => grip.parent.Find("BladeTip");
    }
}
