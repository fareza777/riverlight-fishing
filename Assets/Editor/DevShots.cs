using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CozyFishing.EditorTools
{
    /// <summary>Dev check: renders an overview and a dock close-up of every location to Temp/Shots for visual review.</summary>
    public static class DevShots
    {
        const string OutDir = "Temp/Shots";

        [MenuItem("CozyFishing/Dev/Render Location Shots")]
        public static void Render()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var name in new[] { "River", "Lake", "Gorge", "Estuary" })
            {
                EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                var dock = GameObject.Find("Props").transform.Find("Dock");
                var player = GameObject.Find("Player").transform;
                var mid = Vector3.Lerp(player.position, dock.position, 0.5f);
                // Edit mode has no TimeOfDay update: light the scene like late morning.
                var sun = GameObject.Find("Sun").GetComponent<Light>();
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.intensity = 1.6f;
                sun.color = new Color(1f, 0.95f, 0.86f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.78f);
                RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
                RenderSettings.fogColor = new Color(0.79f, 0.88f, 0.92f);
                Shot(name, "over", new Vector3(0f, 75f, -80f), Vector3.zero, 50f);
                Shot(name, "dock", mid + new Vector3(-7f, 9f, -11f), mid, 45f);
                Shot(name, "game", player.position + Quaternion.Euler(0f, 0f, 0f) * new Vector3(0f, 8.4f, -7.1f), player.position + Vector3.up, 48f);
            }
            Debug.Log($"[DevShots] saved to {Path.GetFullPath(OutDir)}");
        }

        [MenuItem("CozyFishing/Dev/Render Life Shots")]
        public static void RenderLife()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var name in new[] { "River", "Lake", "Gorge", "Estuary" })
            {
                EditorSceneManager.OpenScene($"Assets/Scenes/{name}.unity");
                var sun = GameObject.Find("Sun").GetComponent<Light>();
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
                sun.intensity = 1.6f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(0.62f, 0.7f, 0.78f);
                RenderSettings.ambientEquatorColor = new Color(0.55f, 0.58f, 0.5f);
                RenderSettings.ambientGroundColor = new Color(0.3f, 0.28f, 0.22f);
                var life = GameObject.Find("Ambient Life").transform;
                int i = 0;
                foreach (Transform c in life)
                {
                    if (c.name.Contains("Flock") || c.name.Contains("Fireflies") || c.name.Contains("Splash") || c.name.Contains("Float")) continue;
                    var b = new Bounds(c.position, Vector3.one * 0.2f);
                    foreach (var r in c.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    float d = Mathf.Max(b.size.magnitude * 1.6f, 1.6f);
                    Shot(name, $"life{i++:00}_{c.name}", b.center + new Vector3(d * 0.6f, d * 0.55f, -d * 0.8f), b.center, 40f);
                    if (i >= 12) break;
                }
                // Villagers posed with their default animation (edit mode has no animator update).
                foreach (var v in Object.FindObjectsByType<CozyFishing.World.NpcVillager>(FindObjectsSortMode.None))
                {
                    var anim = v.GetComponent<Animator>();
                    if (anim.runtimeAnimatorController is UnityEditor.Animations.AnimatorController ac && ac.layers[0].stateMachine.defaultState.motion is AnimationClip clip)
                        clip.SampleAnimation(v.gameObject, clip.length * 0.3f);
                    var t = v.transform;
                    Vector3 focus = t.position + Vector3.up * 0.8f;
                    Shot(name, $"npc_{v.name}", focus + t.forward * 3.2f + t.right * 1.2f + Vector3.up * 1.4f, focus, 40f);
                }
            }
            Debug.Log("[DevShots] life shots done");
        }

        static void Shot(string scene, string tag, Vector3 pos, Vector3 look, float fov)
        {
            var go = new GameObject("ShotCam");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.79f, 0.88f, 0.92f);
            cam.farClipPlane = 300f;
            cam.fieldOfView = fov;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var rt = new RenderTexture(540, 540, 24);
            cam.targetTexture = rt;
            go.transform.position = pos;
            go.transform.LookAt(look);
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{OutDir}/{scene}_{tag}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }
}
