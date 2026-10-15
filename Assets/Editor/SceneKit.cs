using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CozyFishing.EditorTools
{
    /// <summary>Shared helpers for the editor scene and content builders.</summary>
    public static class SceneKit
    {
        public const string Models = "Assets/Art/Models";
        public const string Mats = "Assets/Art/Materials";
        public const string PropsDir = "Assets/Art/Props";

        public static GameObject Spawn(string assetPath, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) ?? throw new FileNotFoundException(assetPath);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        }

        /// <summary>Copies a Meshy prop (FBX + BaseColor) from ArtSource into the project once and instantiates it with a textured URP material.</summary>
        public static GameObject SpawnMeshyProp(string name, Transform parent)
        {
            string dir = $"{PropsDir}/{name}";
            string src = $"ArtSource/Meshy/{name}";
            if (!File.Exists($"{src}/{name}.fbx")) { Debug.LogWarning($"[SceneKit] missing {src}/{name}.fbx"); return null; }
            Directory.CreateDirectory(dir);
            string fbx = $"{dir}/{name}.fbx";
            if (!File.Exists(fbx))
            {
                File.Copy($"{src}/{name}.fbx", fbx, true);
                if (File.Exists($"{src}/{name}_BaseColor.png")) File.Copy($"{src}/{name}_BaseColor.png", $"{dir}/{name}_BaseColor.png", true);
                AssetDatabase.Refresh();
                if (AssetImporter.GetAtPath($"{dir}/{name}_BaseColor.png") is TextureImporter tex)
                {
                    tex.maxTextureSize = 1024;   // props are small on a phone screen
                    tex.SaveAndReimport();
                }
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

        public static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Uniformly scales so the largest dimension equals <paramref name="size"/>.</summary>
        public static void Fit(GameObject go, float size)
        {
            var b = Bounds(go);
            float largest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (largest > 1e-4f) go.transform.localScale *= size / largest;
        }

        /// <summary>Scales so the height equals <paramref name="height"/>.</summary>
        public static void FitHeight(GameObject go, float height)
        {
            var b = Bounds(go);
            if (b.size.y > 1e-4f) go.transform.localScale *= height / b.size.y;
        }

        public static void SitOn(GameObject go, float y, float sink = 0f)
        {
            var b = Bounds(go);
            go.transform.position += Vector3.up * (y - b.min.y - sink);
        }

        public static Material ShaderMaterial(string name, string shaderName)
        {
            string path = $"{Mats}/{name}.mat";
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException($"Shader '{shaderName}' not found");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        public static Material Palette(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{Mats}/{name}.mat");

        public static void MarkStatic(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        }

        public static Light PointLight(Transform parent, Vector3 localPos, float range)
        {
            var go = new GameObject("LanternLight", typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.4f);
            l.range = range;
            l.intensity = 0.5f;
            l.shadows = LightShadows.None;
            return l;
        }

        public static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        /// <summary>Highest collider surface under <paramref name="p"/> (terrain, docks, props).</summary>
        public static Vector3 Ground(Vector3 p, float lift = 0.02f)
        {
            return Physics.Raycast(p + Vector3.up * 40f, Vector3.down, out var hit, 80f) ? hit.point + Vector3.up * lift : p;
        }

        public static float TerrainHeight(Vector3 p)
        {
            foreach (var hit in Physics.RaycastAll(p + Vector3.up * 40f, Vector3.down, 80f).OrderBy(h => h.distance))
                if (hit.collider.name.StartsWith("Terrain")) return hit.point.y;
            return 0f;
        }

        public static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name || t.name.StartsWith(name + ".")) return t;
            Debug.LogWarning($"[SceneKit] '{name}' not found under {root.name}");
            return null;
        }

        public static Sprite Sprite(string path, int maxSize = 1024)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter ti &&
                (ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single || ti.maxTextureSize != maxSize))
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = maxSize;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // ------------------------------------------------------------------ serialized field setters
        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector2(Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            Prop(so, field).vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetList<T>(Object target, string field, IList<T> values) where T : Object
        {
            var so = new SerializedObject(target);
            var p = Prop(so, field);
            p.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static SerializedProperty Prop(SerializedObject so, string field) =>
            so.FindProperty(field) ?? throw new ArgumentException($"{so.targetObject.GetType().Name} has no serialized field '{field}'");
    }
}
