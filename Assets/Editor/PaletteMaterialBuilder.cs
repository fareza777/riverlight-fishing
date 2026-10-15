using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Builds URP Lit materials from ArtSource/palette.json (exported from Blender)
    /// and remaps every model in Assets/Art/Models to them by material name.
    /// </summary>
    public static class PaletteMaterialBuilder
    {
        const string PalettePath = "ArtSource/palette.json";
        const string MaterialDir = "Assets/Art/Materials";
        const string ModelDir = "Assets/Art/Models";

        [Serializable] class Entry { public string name; public float[] color; public bool hasEmission; public float[] emission; public float smooth; }
        [Serializable] class Palette { public Entry[] items; }

        [MenuItem("CozyFishing/Build Palette Materials")]
        public static void Build()
        {
            if (!File.Exists(PalettePath))
            {
                Debug.LogError($"Palette not found at {PalettePath}");
                return;
            }
            var palette = JsonUtility.FromJson<Palette>(File.ReadAllText(PalettePath));
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("URP Lit shader not found");
                return;
            }
            Directory.CreateDirectory(MaterialDir);

            foreach (var e in palette.items)
            {
                string path = $"{MaterialDir}/{e.name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = shader;
                mat.SetColor("_BaseColor", new Color(e.color[0], e.color[1], e.color[2]));
                mat.SetFloat("_Smoothness", Mathf.Clamp(e.smooth, 0f, 0.6f));
                mat.SetFloat("_Metallic", 0f);
                if (e.hasEmission)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", new Color(e.emission[0], e.emission[1], e.emission[2]) * 2.5f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                }
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnMaterialName, ModelImporterMaterialSearch.Everywhere);
                importer.SaveAndReimport();
            }
            Debug.Log($"[Palette] {palette.items.Length} materials built and models remapped");
        }
    }
}
