using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CozyFishing.EditorTools.SceneKit;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Makes the edge of the play area visible, so the hero and the boat never stop at an invisible wall:
    /// a split-rail fence where the edge crosses land and a rope of red floats where it crosses water.
    /// Everything is merged into one mesh per material (hundreds of posts would cost a draw call each on old phones).
    /// </summary>
    public static class BoundaryBuilder
    {
        const string Dir = Models + "/Bounds";
        // Just outside where PlayArea stops the hero (1.5 m) and the boat's centre (3 m), so they stop right at the marker.
        const float FenceInset = 1.15f, BuoyInset = 1.3f;
        const float PostSpacing = 2.4f, BuoySpacing = 3.2f;
        const float LandHeight = -0.15f;     // terrain above this is shore/land (the fence runs into the shallows, no gap)
        const float MaxRailStep = 0.9f;      // steeper than this between posts: posts only, no rails

        public static void Build(Rect extent, Transform level)
        {
            var root = new GameObject("Boundary").transform;
            root.SetParent(level, false);
            var parts = new List<(GameObject prefab, Matrix4x4 m)>();
            var post = Load("Fence_Post"); var rail = Load("Fence_Rail"); var buoy = Load("Buoy"); var rope = Load("Buoy_Rope");

            // Fence along the land, then floats along the water (each on its own inset rectangle).
            var fence = Loop(Inset(extent, FenceInset), PostSpacing).ConvertAll(Grounded);
            int n = fence.Count;
            // A section joins two posts on walkable ground; cliffs (too steep to walk anyway) get no fence at all.
            bool Section(int i) { Vector3 a = fence[i], b = fence[(i + 1) % n]; return a.y >= LandHeight && b.y >= LandHeight && Mathf.Abs(b.y - a.y) <= MaxRailStep; }
            for (int i = 0; i < n; i++)
            {
                Vector3 a = fence[i], b = fence[(i + 1) % n];
                if (Section(i) || Section((i + n - 1) % n))
                    parts.Add((post, Matrix4x4.TRS(a, Quaternion.Euler(0f, i * 37f, 0f), Vector3.one)));
                if (!Section(i)) continue;
                foreach (float h in new[] { 0.42f, 0.82f })
                    parts.Add((rail, Span(a + Vector3.up * h, b + Vector3.up * (h + 0.03f * Mathf.Sin(i)))));
            }
            var floats = Loop(Inset(extent, BuoyInset), BuoySpacing);
            for (int i = 0; i < floats.Count; i++)
            {
                Vector3 a = floats[i], b = floats[(i + 1) % floats.Count];
                if (TerrainHeight(a) > LandHeight) continue;
                a.y = 0f;
                parts.Add((buoy, Matrix4x4.TRS(a, Quaternion.Euler(0f, i * 53f, 0f), Vector3.one * 1.3f)));
                if (TerrainHeight(b) > LandHeight) continue;
                b.y = 0f;
                parts.Add((rope, Span(a + Vector3.up * 0.46f, b + Vector3.up * 0.46f)));
            }
            Merge(parts, root);
        }

        static GameObject Load(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{name}.fbx") ?? throw new System.IO.FileNotFoundException($"{Dir}/{name}.fbx");

        static Rect Inset(Rect r, float d) => Rect.MinMaxRect(r.xMin + d, r.yMin + d, r.xMax - d, r.yMax - d);

        /// <summary>Evenly spaced points around the rectangle, starting at a corner.</summary>
        static List<Vector3> Loop(Rect r, float spacing)
        {
            var corners = new[] { new Vector3(r.xMin, 0f, r.yMin), new Vector3(r.xMax, 0f, r.yMin), new Vector3(r.xMax, 0f, r.yMax), new Vector3(r.xMin, 0f, r.yMax) };
            var pts = new List<Vector3>();
            for (int c = 0; c < 4; c++)
            {
                Vector3 a = corners[c], b = corners[(c + 1) % 4];
                int n = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(a, b) / spacing));
                for (int k = 0; k < n; k++) pts.Add(Vector3.Lerp(a, b, k / (float)n));
            }
            return pts;
        }

        static Vector3 Grounded(Vector3 p) { p.y = TerrainHeight(p); return p; }

        /// <summary>A 1 m part along +Z stretched from a to b.</summary>
        static Matrix4x4 Span(Vector3 a, Vector3 b) =>
            Matrix4x4.TRS(a, Quaternion.LookRotation(b - a), new Vector3(1f, 1f, Vector3.Distance(a, b)));

        static void Merge(List<(GameObject prefab, Matrix4x4 m)> parts, Transform root)
        {
            var byMat = new Dictionary<Material, List<CombineInstance>>();
            foreach (var (prefab, m) in parts)
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
                {
                    var mats = mf.GetComponent<MeshRenderer>().sharedMaterials;
                    // The prefab's own transform chain carries the FBX axis conversion.
                    Matrix4x4 local = prefab.transform.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                    {
                        if (!byMat.TryGetValue(mats[s], out var list)) byMat[mats[s]] = list = new List<CombineInstance>();
                        list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = s, transform = m * local });
                    }
                }
            foreach (var (mat, list) in byMat)
            {
                var mesh = new Mesh { name = $"Boundary_{mat.name}", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(list.ToArray(), true, true);
                mesh.RecalculateBounds();
                var go = new GameObject($"Boundary_{mat.name}", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.GetComponent<MeshRenderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;   // thin parts: shadows cost more than they add on a phone
                go.isStatic = true;
            }
            Debug.Log($"[Boundary] {parts.Count} parts merged into {byMat.Count} meshes");
        }
    }
}
