#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Text;
using CozyFishing.Boat;
using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Dev
{
    /// <summary>
    /// Editor-only: rows forward with keyboard-like input and records the gameplay camera every few frames plus a
    /// per-frame CSV of both oar blade tips (boat space) so rowing rhythm/symmetry can be inspected.
    /// </summary>
    public sealed class RowRecorder : MonoBehaviour
    {
        public float seconds = 3f;
        public int everyNthFrame = 3;
        public Vector2 input = new(0f, 1f);

        IEnumerator Start()
        {
            var oars = FindAnyObjectByType<RowingAnimator>();
            var boat = oars.transform;
            Transform bladeL = Tip(oars, "Oar_L"), bladeR = Tip(oars, "Oar_R");
            Directory.CreateDirectory("Docs/Progress/rec");
            foreach (var f in Directory.GetFiles("Docs/Progress/rec")) File.Delete(f);
            var csv = new StringBuilder("t,lx,ly,lz,rx,ry,rz,drive,lean\n");
            GameInput.Instance.SetTouchMove(input);
            yield return new WaitForSeconds(1.5f);
            float t0 = Time.time;
            int frame = 0, shot = 0;
            while (Time.time - t0 < seconds)
            {
                yield return new WaitForEndOfFrame();
                Vector3 l = boat.InverseTransformPoint(bladeL.position), r = boat.InverseTransformPoint(bladeR.position);
                csv.AppendLine(string.Join(",", (Time.time - t0).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture),
                    F(l.x), F(l.y), F(l.z), F(r.x), F(r.y), F(r.z), F(oars.DriveFactor), F(oars.Lean)));
                if (frame++ % everyNthFrame == 0) ScreenCapture.CaptureScreenshot($"Docs/Progress/rec/f{shot++:000}.png");
            }
            GameInput.Instance.SetTouchMove(Vector2.zero);
            File.WriteAllText("Docs/Progress/rec/blades.csv", csv.ToString());
            Debug.Log($"[RowRecorder] {shot} frames");
        }

        static string F(float v) => v.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

        static Transform Tip(RowingAnimator oars, string name)
        {
            foreach (var mf in oars.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.name.StartsWith(name)) continue;
                Bounds b = mf.sharedMesh.bounds;
                Vector3 a = b.center + Vector3.right * b.extents.x, c = b.center - Vector3.right * b.extents.x;
                float da = Mathf.Abs(oars.transform.InverseTransformPoint(mf.transform.TransformPoint(a)).x);
                float dc = Mathf.Abs(oars.transform.InverseTransformPoint(mf.transform.TransformPoint(c)).x);
                var tip = new GameObject("BladeTip").transform;
                tip.SetParent(mf.transform, false);
                tip.localPosition = da > dc ? a : c;
                return tip;
            }
            return null;
        }
    }
}
#endif
