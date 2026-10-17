using System.IO;
using System.Linq;
using CozyFishing.Interaction;
using CozyFishing.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Builds Grandpa's cabin interior (room shell from Blender + Meshy props) at a far-away spot in the River scene,
    /// and wires the doors between the porch and the room.
    /// </summary>
    public static class InteriorBuilder
    {
        const string PropsDir = "Assets/Art/Props";
        public static readonly Vector3 Origin = new(200f, 0f, 0f);
        /// <summary>
        /// The room shell is stretched this much in x/z (furniture keeps its size, its slots move apart) so there is
        /// floor to walk around on; the cabin is a cutaway, so the bigger inside never shows from outside.
        /// </summary>
        public const float Spread = 1.4f;
        public static Vector3 Spaced(Vector3 local) => new(local.x * Spread, local.y, local.z * Spread);
        // Furniture colliders cover this much of each piece's footprint, so the hero can walk up close.
        const float Footprint = 0.8f;

        // name, target size (largest dimension, m), local position, yaw, height offset
        static readonly (string name, float size, Vector3 pos, float yaw)[] Layout =
        {
            ("Prop_Bed", 2.05f, new Vector3(-1.72f, 0f, 1.12f), 180f),
            ("Prop_Stove", 1.15f, new Vector3(1.95f, 0f, 1.65f), 200f),
            ("Prop_Table", 1.0f, new Vector3(0.95f, 0f, 0.05f), 0f),
            ("Prop_Chair", 0.95f, new Vector3(0.2f, 0f, 0.05f), 90f),
            ("Prop_Chair", 0.95f, new Vector3(1.7f, 0f, 0.05f), -90f),
            ("Prop_RockingChair", 1.05f, new Vector3(1.05f, 0f, 1.35f), 160f),
            ("Prop_Chest", 0.85f, new Vector3(-1.72f, 0f, -0.25f), 180f),
            ("Prop_RodRack", 1.6f, new Vector3(-2.32f, 0f, -1.35f), 90f),
            ("Prop_Shelf", 1.25f, new Vector3(1.45f, 1.05f, 2.05f), 180f),
            ("Prop_FishTrophy", 0.75f, new Vector3(-1.25f, 1.65f, 2.08f), 180f),
        };

        public struct Result
        {
            public Transform Root, InsideSpawn, ExitPortal;
            public CameraZone Zone;
            public Light[] Lamps;
        }

        public static Result Build(Transform parent, Material fallbackLit)
        {
            var root = new GameObject("CabinInterior").transform;
            root.SetParent(parent, false);
            root.position = Origin;

            var room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Interior.fbx"), root);
            room.transform.localScale = Vector3.Scale(room.transform.localScale, new Vector3(Spread, 1f, Spread));
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>())
            {
                // Open doorway: the swinging leaf from Blender pokes out of the cutaway, so it is not used.
                if (mf.name.StartsWith("Interior_Door")) { Object.DestroyImmediate(mf.gameObject); continue; }
                mf.gameObject.AddComponent<MeshCollider>();
            }

            // A dark, warm "night outside" ground so the cutaway room doesn't float in the sky colour.
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Plane);
            backdrop.name = "Backdrop";
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.transform.SetParent(root, false);
            backdrop.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            backdrop.transform.localScale = new Vector3(6f * Spread, 1f, 6f * Spread);
            var dark = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            dark.SetColor("_BaseColor", new Color(0.09f, 0.07f, 0.06f));
            AssetDatabase.CreateAsset(dark, "Assets/Art/Materials/Interior_Backdrop.mat");
            backdrop.GetComponent<MeshRenderer>().sharedMaterial = dark;
            backdrop.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            foreach (var (name, size, pos, yaw) in Layout)
            {
                var go = SpawnProp(name, root);
                if (go == null) continue;
                Fit(go, size);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localPosition = Spaced(pos);
                SitOnFloor(go, root.position.y + pos.y);
                AddFootprintCollider(go);
            }

            // Grandpa's journal on the table.
            var journal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            journal.name = "Journal";
            journal.transform.SetParent(root, false);
            journal.transform.localPosition = Spaced(new Vector3(0.95f, 0.79f, 0.05f));
            journal.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            journal.transform.localScale = new Vector3(0.24f, 0.04f, 0.32f);
            journal.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Pal_WoodDark.mat");

            // Warm lamps: a ceiling lantern glow and the stove.
            var lamp = NewLight(root, "RoomLight", Spaced(new Vector3(0f, 2.3f, 0.2f)), 7f * Spread, 1.2f, new Color(1f, 0.82f, 0.6f));
            var stove = NewLight(root, "StoveGlow", Spaced(new Vector3(1.85f, 0.45f, 1.3f)), 3f, 0.8f, new Color(1f, 0.55f, 0.25f));

            var spawn = new GameObject("InsideSpawn").transform;
            spawn.SetParent(root, false);
            spawn.localPosition = Spaced(new Vector3(0.95f, 0.02f, -1.45f));
            spawn.localRotation = Quaternion.identity;

            var exit = new GameObject("ExitDoor").transform;
            exit.SetParent(root, false);
            exit.localPosition = Spaced(new Vector3(0.95f, 0f, -1.95f));

            var zoneGo = new GameObject("InteriorZone");
            zoneGo.transform.SetParent(root, false);
            var zone = zoneGo.AddComponent<CameraZone>();
            // The camera follows the hero around the room (a little closer than outdoors), staying inside the walls.
            zone.EditorSetup(new Vector2(Origin.x - 1.7f, Origin.z - 1.9f), new Vector2(Origin.x + 1.7f, Origin.z + 1.1f), 5.8f, 64f, true);

            return new Result { Root = root, InsideSpawn = spawn, ExitPortal = exit, Zone = zone, Lamps = new[] { lamp, stove } };
        }

        static GameObject SpawnProp(string name, Transform parent)
        {
            string dir = $"{PropsDir}/{name}";
            string src = $"ArtSource/Meshy/{name}";
            if (!File.Exists($"{src}/{name}.fbx")) { Debug.LogWarning($"[Interior] missing {src}/{name}.fbx"); return null; }
            Directory.CreateDirectory(dir);
            string fbx = $"{dir}/{name}.fbx";
            if (!File.Exists(fbx))
            {
                File.Copy($"{src}/{name}.fbx", fbx, true);
                if (File.Exists($"{src}/{name}_BaseColor.png")) File.Copy($"{src}/{name}_BaseColor.png", $"{dir}/{name}_BaseColor.png", true);
                AssetDatabase.Refresh();
                var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.animationType = ModelImporterAnimationType.None;
                imp.SaveAndReimport();
            }
            string matPath = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{name}_BaseColor.png"));
                mat.SetFloat("_Smoothness", 0.18f);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(fbx), parent);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            return go;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>A solid box over the middle of a piece's footprint, from the floor to its top.</summary>
        public static void AddFootprintCollider(GameObject go)
        {
            var b = Bounds(go);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(b.center);
            var ls = go.transform.lossyScale;
            box.size = new Vector3(b.size.x * Footprint / ls.x, b.size.y / ls.y, b.size.z * Footprint / ls.z);
        }

        static void Fit(GameObject go, float size)
        {
            var b = Bounds(go);
            float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (largest > 1e-4f) go.transform.localScale *= size / largest;
        }

        static void SitOnFloor(GameObject go, float floorY)
        {
            var b = Bounds(go);
            go.transform.position += Vector3.up * (floorY - b.min.y);
        }

        static Light NewLight(Transform parent, string name, Vector3 local, float range, float intensity, Color color)
        {
            var l = new GameObject(name, typeof(Light)).GetComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = local;
            l.type = LightType.Point;
            l.range = range;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
            return l;
        }
    }
}
