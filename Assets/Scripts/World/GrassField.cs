using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CozyFishing.World
{
    /// <summary>
    /// Scatters instanced grass blades (and optional flower heads) over the terrain's grassy faces and draws only the
    /// chunks the camera can see. Placement is deterministic (seeded) so it is identical every run.
    /// </summary>
    public sealed class GrassField : MonoBehaviour
    {
        [SerializeField] MeshFilter terrain;
        [SerializeField] Material grassMaterial;
        [SerializeField] Material[] flowerMaterials = new Material[0];
        [SerializeField] float bladesPerSquareMetre = 42f;
        [SerializeField] float flowersPerSquareMetre = 2.2f;
        [Tooltip("Flowers only grow in meadow patches where this noise is above the threshold.")]
        [SerializeField] float meadowThreshold = 0.6f;
        [SerializeField] Vector2 bladeHeight = new(0.16f, 0.36f);
        [SerializeField] float minHeightAboveWater = 0.45f;
        [SerializeField] float maxSlope = 0.3f;
        [SerializeField] float chunkSize = 12f;
        [SerializeField] float drawDistance = 48f;
        [SerializeField] int seed = 7;
        [SerializeField] List<Collider> exclusions = new();
        [SerializeField] Transform[] pushers = new Transform[0];

        sealed class Chunk
        {
            public Bounds bounds;
            public readonly List<Matrix4x4> blades = new();
            public readonly List<Matrix4x4>[] flowers = { new(), new(), new() };
            public List<Matrix4x4[]> bladeBatches;
            public List<Matrix4x4[]>[] flowerBatches;
        }

        readonly List<Chunk> chunks = new();
        Mesh blade, flower;
        Camera cam;
        readonly Plane[] planes = new Plane[6];
        readonly Vector4[] pusherData = new Vector4[4];
        static readonly int PushersId = Shader.PropertyToID("_GrassPushers");

        public void SetPushers(params Transform[] list) => pushers = list;

        readonly List<Bounds> blocked = new();

        void Start()
        {
            cam = Camera.main;
            blade = BuildBlade();
            flower = BuildFlower();
            foreach (var col in exclusions)
            {
                if (col == null) continue;
                var b = col.bounds;
                b.Expand(new Vector3(0.5f, 10f, 0.5f));
                blocked.Add(b);
            }
            StartCoroutine(Scatter());
            Core.GraphicsQuality.Changed += Rescatter;
        }

        void OnDestroy() => Core.GraphicsQuality.Changed -= Rescatter;

        /// <summary>Graphics level changed: scatter again at the new density.</summary>
        void Rescatter()
        {
            StopAllCoroutines();
            chunks.Clear();
            StartCoroutine(Scatter());
        }

        static Mesh BuildBlade()
        {
            // Three-segment tapered blade with a slight forward curve; uv.y = 0 at root, 1 at tip.
            var m = new Mesh { name = "GrassBlade" };
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            const int seg = 3;
            for (int s = 0; s <= seg; s++)
            {
                float k = s / (float)seg;
                float w = Mathf.Lerp(0.032f, 0.003f, k);
                float bend = k * k * 0.12f;
                v.Add(new Vector3(-w, k, bend)); uv.Add(new Vector2(0, k));
                v.Add(new Vector3(w, k, bend)); uv.Add(new Vector2(1, k));
            }
            for (int s = 0; s < seg; s++)
            {
                int b = s * 2;
                t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static Mesh BuildFlower()
        {
            // An upward-facing blossom quad just above the grass tips (petals are cut in the shader).
            const float s = 0.075f, h = 0.3f;
            var m = new Mesh { name = "Flower" };
            m.SetVertices(new List<Vector3> { new(-s, h, -s), new(s, h, -s), new(-s, h, s), new(s, h, s) });
            m.SetUVs(0, new List<Vector2> { new(0, 0), new(1, 0), new(0, 1), new(1, 1) });
            m.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        System.Collections.IEnumerator Scatter()
        {
            var mesh = terrain.sharedMesh;
            var verts = mesh.vertices;
            var colors = mesh.colors;
            var tris = mesh.triangles;
            var l2w = terrain.transform.localToWorldMatrix;
            var rng = new System.Random(seed);
            var map = new Dictionary<Vector2Int, Chunk>();

            for (int i = 0; i < tris.Length; i += 3)
            {
                if (i % 6000 == 0) yield return null;   // spread the work over a few frames
                Vector3 a = l2w.MultiplyPoint3x4(verts[tris[i]]);
                Vector3 b = l2w.MultiplyPoint3x4(verts[tris[i + 1]]);
                Vector3 c = l2w.MultiplyPoint3x4(verts[tris[i + 2]]);
                Vector3 n = Vector3.Cross(b - a, c - a);
                float area = n.magnitude * 0.5f;
                if (area < 1e-4f) continue;
                n /= area * 2f;
                if (n.y < 0) n = -n;
                if (1f - n.y > maxSlope) continue;
                float path = colors.Length > 0 ? (colors[tris[i]].r + colors[tris[i + 1]].r + colors[tris[i + 2]].r) / 3f : 0f;
                if (path > 0.35f) continue;

                float expected = area * bladesPerSquareMetre * Core.GraphicsQuality.GrassDensity * (1f - path);
                int count = (int)expected + (rng.NextDouble() < expected - (int)expected ? 1 : 0);
                float flowerExpected = area * flowersPerSquareMetre * Mathf.Max(0.5f, Core.GraphicsQuality.GrassDensity);
                int flowerCount = (int)flowerExpected + (rng.NextDouble() < flowerExpected - (int)flowerExpected ? 1 : 0);

                for (int k = 0; k < count + flowerCount; k++)
                {
                    float r1 = (float)rng.NextDouble(), r2 = (float)rng.NextDouble();
                    if (r1 + r2 > 1f) { r1 = 1f - r1; r2 = 1f - r2; }
                    Vector3 p = a + (b - a) * r1 + (c - a) * r2;
                    if (p.y < minHeightAboveWater + (float)rng.NextDouble() * 0.15f) continue;
                    if (Excluded(p)) continue;
                    var key = new Vector2Int(Mathf.FloorToInt(p.x / chunkSize), Mathf.FloorToInt(p.z / chunkSize));
                    if (!map.TryGetValue(key, out var chunk))
                    {
                        chunk = new Chunk { bounds = new Bounds(new Vector3((key.x + 0.5f) * chunkSize, p.y, (key.y + 0.5f) * chunkSize), new Vector3(chunkSize, 4f, chunkSize)) };
                        map[key] = chunk;
                    }
                    chunk.bounds.Encapsulate(p + Vector3.up);
                    var rot = Quaternion.Euler((float)rng.NextDouble() * 14f - 7f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 14f - 7f);
                    if (k < count)
                    {
                        float h = Mathf.Lerp(bladeHeight.x, bladeHeight.y, Mathf.Pow((float)rng.NextDouble(), 0.7f));
                        chunk.blades.Add(Matrix4x4.TRS(p, rot, new Vector3(1f, h, 1f)));
                    }
                    else if (Mathf.PerlinNoise(p.x * 0.09f + 13f, p.z * 0.09f + 7f) > meadowThreshold)
                    {
                        int kind = rng.Next(3);
                        var up = Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f - 10f);
                        chunk.flowers[kind].Add(Matrix4x4.TRS(p, up, Vector3.one * (0.8f + (float)rng.NextDouble() * 0.5f)));
                    }
                }
            }
            foreach (var c in map.Values)
            {
                c.bladeBatches = Batch(c.blades);
                c.flowerBatches = new List<Matrix4x4[]>[c.flowers.Length];
                for (int f = 0; f < c.flowers.Length; f++) { c.flowerBatches[f] = Batch(c.flowers[f]); c.flowers[f].Clear(); }
                c.blades.Clear();
                chunks.Add(c);
            }
        }

        // Instanced draws are capped at 1023 per call; keep batches safely below that.
        static List<Matrix4x4[]> Batch(List<Matrix4x4> all)
        {
            const int size = 1000;
            var list = new List<Matrix4x4[]>();
            for (int i = 0; i < all.Count; i += size) list.Add(all.GetRange(i, Mathf.Min(size, all.Count - i)).ToArray());
            return list;
        }

        bool Excluded(Vector3 p)
        {
            foreach (var b in blocked)
                if (b.Contains(p)) return true;
            return false;
        }

        void Update()
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; }
            for (int i = 0; i < 4; i++)
                pusherData[i] = i < pushers.Length && pushers[i] != null ? (Vector4)pushers[i].position + new Vector4(0, 0, 0, 1) : Vector4.zero;
            Shader.SetGlobalVectorArray(PushersId, pusherData);

            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            Vector3 camPos = cam.transform.position;
            float range = Mathf.Min(drawDistance, Core.GraphicsQuality.GrassDistance);
            var bladeParams = new RenderParams(grassMaterial) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
            foreach (var c in chunks)
            {
                if ((c.bounds.center - camPos).sqrMagnitude > range * range) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, c.bounds)) continue;
                foreach (var b in c.bladeBatches) Graphics.RenderMeshInstanced(bladeParams, blade, 0, b);
                for (int f = 0; f < c.flowerBatches.Length && f < flowerMaterials.Length; f++)
                {
                    if (flowerMaterials[f] == null) continue;
                    var fp = new RenderParams(flowerMaterials[f]) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
                    foreach (var batch in c.flowerBatches[f]) Graphics.RenderMeshInstanced(fp, flower, 0, batch);
                }
            }
        }
    }
}
