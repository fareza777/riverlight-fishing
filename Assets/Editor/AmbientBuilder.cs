using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CozyFishing.World;
using CozyFishing.World.Ambient;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CozyFishing.EditorTools.SceneKit;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Puts life into a location: ducks, herons, birds, rabbits, deer, butterflies, fireflies, jumping fish, the cabin
    /// cat, and villagers (walkers and fishers). Positions are found from the terrain (water depth, shallows, meadows),
    /// so the same rules work for every location. Critters are assembled from the part FBXs made by
    /// Tools/blender_critters.py (joint offsets in ArtSource/critters.json, Blender (x, y, z) -> Unity (x, z, y)).
    /// </summary>
    public static class AmbientBuilder
    {
        sealed class Plan
        {
            public int ducks, herons, rabbits, groundBirds, deer, butterflies;
            public bool songFlock, gullFlock, cat;
            public (string npc, string name, bool sitter)[] villagers;
        }

        static readonly Dictionary<string, Plan> Plans = new()
        {
            ["river"] = new Plan { ducks = 2, herons = 1, rabbits = 3, groundBirds = 3, deer = 1, butterflies = 6, songFlock = true, cat = true,
                villagers = new[] { ("dimas", "Leo", false), ("wati", "Martha", false) } },
            ["lake"] = new Plan { ducks = 3, herons = 1, rabbits = 3, groundBirds = 3, deer = 2, butterflies = 6, songFlock = true,
                villagers = new[] { ("joko", "Hugo", true), ("wati", "Martha", false) } },
            ["gorge"] = new Plan { ducks = 1, herons = 0, rabbits = 1, groundBirds = 2, deer = 2, butterflies = 4, songFlock = true,
                villagers = new[] { ("joko", "Hugo", true), ("dimas", "Leo", false) } },
            ["estuary"] = new Plan { ducks = 1, herons = 2, rabbits = 0, groundBirds = 2, deer = 0, butterflies = 3, gullFlock = true,
                villagers = new[] { ("bayu", "Old Ben", true), ("wati", "Martha", false) } },
        };

        static Dictionary<string, Dictionary<string, float[]>> offsets;
        static Random rng;
        static Rect area;
        static Vector3 dock;

        public static void Build(string location, Rect extent, Transform dockT, Transform cabin, Transform[] schools)
        {
            if (!Plans.TryGetValue(location, out var plan)) return;
            offsets = LoadOffsets();
            rng = new Random(location.GetHashCode() & 0xffff);
            area = Rect.MinMaxRect(extent.xMin + 3f, extent.yMin + 3f, extent.xMax - 3f, extent.yMax - 3f);
            dock = dockT.position;
            Physics.SyncTransforms();
            var root = new GameObject("Ambient Life").transform;

            for (int i = 0; i < plan.ducks; i++)
                if (Find(p => Depth(p) > 0.6f && OpenWater(p, 3.5f), out var p)) DuckFamily(root, p, i >= 1);
            for (int i = 0; i < plan.herons; i++)
                if (Find(p => Height(p) is > -0.35f and < -0.08f && Far(p, 8f), out var p)) Heron(root, p);
            for (int i = 0; i < plan.rabbits; i++)
                if (Find(Meadow, out var p)) Hopper(root, "Rabbit", p, 1f, i >= 2);
            for (int i = 0; i < plan.groundBirds; i++)
                if (Find(Meadow, out var p)) Hopper(root, "Songbird", p, 1.4f, i >= 1);
            for (int i = 0; i < plan.deer; i++)
                if (Find(p => Meadow(p) && Far(p, 14f), out var p)) Deer(root, p, i >= 1);
            for (int i = 0; i < plan.butterflies; i++)
                if (Find(Meadow, out var p)) Butterfly(root, p, i % 3, i % 2 == 1);
            if (plan.songFlock) Flock(root, "Songbird", 6, 1.7f, new Vector3(dock.x, 0f, dock.z + 6f), 16f, 10f, 4.5f, true, 11f);
            if (plan.gullFlock) Flock(root, "Gull", 5, 1f, new Vector3(dock.x + 14f, 0f, dock.z + 10f), 18f, 9f, 5f, false, 4f);
            if (plan.cat && cabin != null) Cat(cabin);
            Fireflies(root);
            Jumper(root, schools);
            foreach (var (npc, name, sitter) in plan.villagers) Villager(root, npc, name, sitter, schools);
        }

        // ------------------------------------------------------------------ terrain queries
        static float Height(Vector3 p) => TerrainHeight(p);
        static float Depth(Vector3 p) => -TerrainHeight(p);
        static bool Far(Vector3 p, float min) => (new Vector3(p.x, 0f, p.z) - new Vector3(dock.x, 0f, dock.z)).magnitude > min;

        static bool OpenWater(Vector3 p, float r)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4f;
                if (Height(p + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r) > -0.3f) return false;
            }
            return Far(p, 6f);
        }

        static bool Meadow(Vector3 p)
        {
            float h = Height(p);
            if (h < 0.5f || h > 3.5f || !Far(p, 6f)) return false;
            if (Mathf.Abs(Height(p + Vector3.right) - h) > 0.35f || Mathf.Abs(Height(p + Vector3.forward) - h) > 0.35f) return false;
            return !Physics.OverlapSphere(new Vector3(p.x, h + 0.8f, p.z), 1.2f, ~0, QueryTriggerInteraction.Ignore).Any(c => !c.name.StartsWith("Terrain"));
        }

        static bool Find(Func<Vector3, bool> ok, out Vector3 pos)
        {
            for (int i = 0; i < 400; i++)
            {
                var p = new Vector3(Mathf.Lerp(area.xMin, area.xMax, (float)rng.NextDouble()), 0f, Mathf.Lerp(area.yMin, area.yMax, (float)rng.NextDouble()));
                if (!ok(p)) continue;
                pos = new Vector3(p.x, Height(p), p.z);
                return true;
            }
            pos = default;
            return false;
        }

        // ------------------------------------------------------------------ critter assembly
        static Dictionary<string, Dictionary<string, float[]>> LoadOffsets()
        {
            // critters.json: {"Duck": {"Body": [x,y,z], "Head": [...]}, ...} (tiny hand parser: JsonUtility can't read dictionaries)
            var result = new Dictionary<string, Dictionary<string, float[]>>();
            var text = File.ReadAllText("ArtSource/critters.json");
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "\"(\\w+)\":\\s*\\{([^{}]*)\\}"))
            {
                var parts = new Dictionary<string, float[]>();
                foreach (System.Text.RegularExpressions.Match pm in System.Text.RegularExpressions.Regex.Matches(m.Groups[2].Value, "\"(\\w+)\":\\s*\\[([^\\]]*)\\]"))
                    parts[pm.Groups[1].Value] = pm.Groups[2].Value.Split(',').Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                result[m.Groups[1].Value] = parts;
            }
            return result;
        }

        /// <summary>Builds a critter from its parts; returns the root and the part transforms by name.</summary>
        static (Transform root, Dictionary<string, Transform> parts) Assemble(string critter, Transform parent, Vector3 pos, float scale)
        {
            var root = new GameObject(critter).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(pos, Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
            root.localScale = Vector3.one * scale;
            var parts = new Dictionary<string, Transform>();
            foreach (var (part, o) in offsets[critter])
            {
                var go = Spawn($"{Models}/Critters/{critter}_{part}.fbx", root);
                go.name = part;
                go.transform.localPosition = new Vector3(o[0], o[2], o[1]);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
                {
                    r.shadowCastingMode = critter is "Deer" or "Heron" ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    foreach (var m in r.sharedMaterials) if (m != null) m.enableInstancing = true;
                }
                parts[part] = go.transform;
            }
            return (root, parts);
        }

        static void DuckFamily(Transform parent, Vector3 centre, bool skipOnLow)
        {
            var family = new GameObject("Duck Family").transform;
            family.SetParent(parent, false);
            family.position = new Vector3(centre.x, 0f, centre.z);
            var ducks = new List<Transform>(); var heads = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var (root, parts) = Assemble("Duck", family, family.position + new Vector3(i * 0.4f, 0f, 0f), i == 0 ? 1.15f : 0.5f);
                ducks.Add(root); heads.Add(parts["Head"]);
            }
            var c = family.gameObject.AddComponent<DuckFamily>();
            c.EditorSetup(ducks.ToArray(), heads.ToArray(), 2.5f + (float)rng.NextDouble() * 1.5f);
            SetBool(c, "skipOnLow", skipOnLow);
        }

        static void Heron(Transform parent, Vector3 p)
        {
            var (root, parts) = Assemble("Heron", parent, new Vector3(p.x, p.y, p.z), 1.1f);
            root.gameObject.AddComponent<Heron>().EditorSetup(parts["Neck"], parts["WingL"], parts["WingR"]);
        }

        static void Hopper(Transform parent, string critter, Vector3 p, float scale, bool skipOnLow)
        {
            var (root, parts) = Assemble(critter, parent, p, scale);
            var h = root.gameObject.AddComponent<Hopper>();
            bool bird = critter == "Songbird";
            h.EditorSetup(parts["Body"], parts.TryGetValue("Ears", out var e) ? e : null,
                bird ? parts["WingL"] : null, bird ? parts["WingR"] : null, bird ? 0.12f : 0.4f, bird ? 0.06f : 0.14f, bird ? 3.5f : 5f);
            if (bird) root.position += Vector3.up * 0.03f;
            SetBool(h, "skipOnLow", skipOnLow);
        }

        static void Deer(Transform parent, Vector3 p, bool skipOnLow)
        {
            var (root, parts) = Assemble("Deer", parent, p, 1f);
            var d = root.gameObject.AddComponent<Deer>();
            d.EditorSetup(parts["Neck"], new[] { parts["LegFL"], parts["LegFR"], parts["LegBL"], parts["LegBR"] });
            SetBool(d, "skipOnLow", skipOnLow);
        }

        static void Butterfly(Transform parent, Vector3 p, int colour, bool skipOnLow)
        {
            string name = new[] { "ButterflyOrange", "ButterflyBlue", "ButterflyYellow" }[colour];
            var (root, parts) = Assemble(name, parent, p + Vector3.up * 0.6f, 1.6f);
            var b = root.gameObject.AddComponent<Butterfly>();
            b.EditorSetup(parts["WingL"], parts["WingR"]);
            SetBool(b, "skipOnLow", skipOnLow);
        }

        static void Flock(Transform parent, string critter, int count, float scale, Vector3 centre, float radius, float height, float speed, bool dayOnly, float flap)
        {
            var flock = new GameObject($"{critter} Flock").transform;
            flock.SetParent(parent, false);
            flock.position = centre;
            var birds = new List<Transform>(); var l = new List<Transform>(); var r = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                var (root, parts) = Assemble(critter, flock, centre + Vector3.up * height, scale);
                birds.Add(root); l.Add(parts["WingL"]); r.Add(parts["WingR"]);
            }
            flock.gameObject.AddComponent<BirdFlock>().EditorSetup(birds.ToArray(), l.ToArray(), r.ToArray(), radius, height, speed, dayOnly, flap);
        }

        static void Cat(Transform cabin)
        {
            // Curled up on the left end of the porch (cabin space: floor 0.45, x -1.95..1.95, z -2.89..-1.55).
            var (root, parts) = Assemble("Cat", cabin, cabin.TransformPoint(new Vector3(-1.45f, 0.45f, -2.3f)), 1f);
            root.localScale = Vector3.one;
            root.gameObject.AddComponent<SleepingCat>().EditorSetup(parts["Body"], parts["Tail"]);
        }

        // ------------------------------------------------------------------ particles
        static Material GlowMaterial(string name, Color colour, bool additive)
        {
            var m = ShaderMaterial(name, "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", GlowTexture());
            m.SetColor("_BaseColor", colour);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            return m;
        }

        static Texture2D GlowTexture()
        {
            const string path = "Assets/Art/Materials/SoftGlow.png";
            if (!File.Exists(path))
            {
                var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                        float a = Mathf.Clamp01(1f - d); a *= a;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(path, tex.EncodeToPNG());
                AssetDatabase.ImportAsset(path);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.alphaIsTransparency = true; ti.mipmapEnabled = false; ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static ParticleSystem Particles(Transform parent, string name, Material mat)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            var ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return ps;
        }

        static void Fireflies(Transform parent)
        {
            var ps = Particles(parent, "Fireflies", GlowMaterial("Firefly", new Color(1f, 0.92f, 0.45f), true));
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 7f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(22f, 2f, 22f);
            var noise = ps.noise; noise.enabled = true; noise.strength = 0.4f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            ps.gameObject.AddComponent<Fireflies>().EditorSetup(ps);
            ps.Play();
        }

        static void Jumper(Transform parent, Transform[] schools)
        {
            var ps = Particles(parent, "Fish Splash", GlowMaterial("Splash", new Color(1f, 1f, 1f, 0.9f), false));
            var main = ps.main;
            main.startLifetime = 0.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 60;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var models = schools.Select(s => s.GetComponent<FishSchool>()).Where(s => s != null)
                .Select(s => new SerializedObject(s).FindProperty("fishPrefab").objectReferenceValue as GameObject).Where(g => g != null).Distinct().ToArray();
            ps.gameObject.AddComponent<FishJumper>().EditorSetup(models, schools, ps);
        }

        // ------------------------------------------------------------------ villagers
        static void Villager(Transform parent, string npc, string displayName, bool sitter, Transform[] schools)
        {
            string asset = "NPC_" + char.ToUpper(npc[0]) + npc.Substring(1);
            if (!File.Exists(NpcImporter.ModelPath(asset)) && !NpcImporter.Import(asset)) return;
            Vector3 pos; Quaternion rot; Vector3 waterPoint = default; Vector3[] path = new Vector3[0];
            if (sitter)
            {
                if (!FindShore(schools, out pos, out waterPoint)) return;
                var face = waterPoint - pos; face.y = 0f;
                rot = Quaternion.LookRotation(face);
            }
            else
            {
                if (!Find(p => Meadow(p) && Far(p, 7f), out pos)) return;
                path = WalkPath(pos);
                rot = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            }

            var go = Spawn(NpcImporter.ModelPath(asset), parent);
            go.name = asset;
            go.transform.SetPositionAndRotation(pos, rot);
            FitHeight(go, npc == "dimas" ? 1.45f : npc == "wati" ? 1.58f : 1.66f);
            SitOn(go, pos.y);
            if (!go.TryGetComponent<Animator>(out var anim)) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(NpcImporter.ControllerPath(asset));
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var col = go.AddComponent<CapsuleCollider>();
            float s = go.transform.lossyScale.y;
            col.center = new Vector3(0f, 0.8f / s, 0f); col.radius = 0.3f / s; col.height = 1.6f / s;
            var v = go.AddComponent<NpcVillager>();
            v.EditorSetup(npc, displayName, anim, path);
            SetFloat(v, "radius", 2.4f);

            if (sitter)
            {
                // A stool under the seat, a rod held over the water and a float bobbing out front.
                var stool = Assemble("Stool", parent, pos, 1f).root;
                stool.rotation = rot;
                go.transform.position = pos + rot * new Vector3(0f, 0f, -0.05f);
                var rodRoot = new GameObject("NpcRod").transform;
                rodRoot.SetParent(go.transform, true);
                rodRoot.position = pos + rot * new Vector3(0.14f, 0.62f, 0.32f);
                rodRoot.rotation = rot * Quaternion.Euler(55f, 0f, 0f);
                Part(rodRoot, PrimitiveType.Cylinder, new Vector3(0f, 0.9f, 0f), new Vector3(0.016f, 0.95f, 0.016f), Palette("Pal_WoodDark"));
                Part(rodRoot, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.035f, 0.12f, 0.035f), Palette("Pal_WoodLight"));
                var tip = new GameObject("Tip").transform; tip.SetParent(rodRoot, false); tip.localPosition = new Vector3(0f, 1.85f, 0f);
                var bobber = new GameObject("Float").transform; bobber.SetParent(parent, false);
                Part(bobber, PrimitiveType.Sphere, Vector3.zero, new Vector3(0.11f, 0.08f, 0.11f), Palette("Pal_Red"));
                Vector3 rest = Vector3.Lerp(pos, waterPoint, 0.85f); rest.y = 0f;
                bobber.position = rest;
                var lineGo = new GameObject("Line", typeof(LineRenderer));
                lineGo.transform.SetParent(go.transform, false);
                var lr = lineGo.GetComponent<LineRenderer>();
                lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Mats}/FishingLine.mat");
                lr.widthMultiplier = 0.01f;
                lr.shadowCastingMode = ShadowCastingMode.Off;
                v.EditorSetupFishing(rodRoot, tip, bobber, lr, rest);
            }
        }

        /// <summary>A dry shore point near one of the fish schools, with open water a few metres in front.</summary>
        static bool FindShore(Transform[] schools, out Vector3 seat, out Vector3 water)
        {
            foreach (var school in schools.OrderBy(_ => rng.Next()))
            {
                Vector3 c = school.position;
                for (int i = 0; i < 160; i++)
                {
                    float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 3f + (float)rng.NextDouble() * 12f;
                    var p = c + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    float h = Height(p);
                    if (h < 0.3f || h > 1.6f || !Far(p, 8f)) continue;
                    var dir = c - p; dir.y = 0f; dir.Normalize();
                    var w = p + dir * 4f;
                    if (Height(p + dir * 1.6f) > 0.0f || Height(w) > -0.3f) continue;
                    if (Physics.OverlapSphere(new Vector3(p.x, h + 0.8f, p.z), 0.8f, ~0, QueryTriggerInteraction.Ignore).Any(col => !col.name.StartsWith("Terrain"))) continue;
                    seat = new Vector3(p.x, h, p.z); water = w;
                    return true;
                }
            }
            seat = water = default;
            return false;
        }

        /// <summary>A loop of reachable meadow waypoints around a start point (clear straight lines over dry land).</summary>
        static Vector3[] WalkPath(Vector3 start)
        {
            var path = new List<Vector3> { start };
            for (int tries = 0; tries < 300 && path.Count < 5; tries++)
            {
                var last = path[^1];
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = 3f + (float)rng.NextDouble() * 6f;
                var p = last + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if ((p - start).magnitude > 11f || !Meadow(p) || !Clear(last, p)) continue;
                path.Add(new Vector3(p.x, Height(p), p.z));
            }
            return path.Count >= 3 && Clear(path[^1], start) ? path.ToArray() : path.Take(Math.Min(path.Count, 2)).ToArray();
        }

        static bool Clear(Vector3 a, Vector3 b)
        {
            for (float t = 0.1f; t < 1f; t += 0.1f)
            {
                var p = Vector3.Lerp(a, b, t);
                if (Height(p) < 0.4f) return false;
            }
            var from = new Vector3(a.x, Height(a) + 0.9f, a.z);
            var to = new Vector3(b.x, Height(b) + 0.9f, b.z);
            return !Physics.SphereCastAll(from, 0.35f, (to - from).normalized, (to - from).magnitude, ~0, QueryTriggerInteraction.Ignore)
                .Any(h => !h.collider.name.StartsWith("Terrain"));
        }
    }
}
