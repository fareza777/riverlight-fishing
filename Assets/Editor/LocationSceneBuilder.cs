using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CozyFishing.Boat;
using CozyFishing.CameraSystem;
using CozyFishing.Core;
using CozyFishing.Fishing;
using CozyFishing.Interaction;
using CozyFishing.Player;
using CozyFishing.Story;
using CozyFishing.UI;
using CozyFishing.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static CozyFishing.EditorTools.SceneKit;
using Object = UnityEngine.Object;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Builds one scene per fishing location from ArtSource/locations/&lt;id&gt;.json (exported by Tools/blender_locations.py;
    /// Blender (x, y, z) -> Unity (x, z, y), yaw = -rot). Every location gets the same gameplay rig: boat, both heroes,
    /// fishing, camera, story director, market (Mira), map board, music, grass and HUD. The river also has Grandpa's cabin.
    /// </summary>
    public static class LocationSceneBuilder
    {
        [Serializable] class Item { public string asset; public float x, y, z, rot, scale, size; }
        [Serializable] class P3 { public float x, y, z; }
        [Serializable] class SpotDef { public string name, nameID, habitat; public float x, y; }
        [Serializable] class SchoolDef { public string fish; public float x, y; public int count; public float radius, depth; }
        [Serializable] class Layout { public Item[] items; public P3 spawn; public SpotDef[] spots; public SchoolDef[] schools; }

        sealed class Look
        {
            public string scene, terrain, dayMusic;
            public Rect extent;                   // x/z bounds of the playable area
            public Color shallow, deep;
            public Color[] grass;                 // A, B, C, dry (null = shader defaults)
            public float slopeRock = 0.34f, slopeDirt = 0.2f, shore = 0.42f, grassDensity = 42f, seaMargin;
        }

        static readonly Dictionary<string, Look> Looks = new()
        {
            ["river"] = new Look
            {
                scene = "River", terrain = "Terrain", dayMusic = "river_day", extent = Rect.MinMaxRect(-26f, -52f, 26f, 39f),
                shallow = new Color(0.36f, 0.78f, 0.74f), deep = new Color(0.05f, 0.29f, 0.37f),
            },
            ["lake"] = new Look
            {
                scene = "Lake", terrain = "Terrain_lake", dayMusic = "lake_day", extent = Rect.MinMaxRect(-42f, -44f, 42f, 48f),
                shallow = new Color(0.42f, 0.76f, 0.70f), deep = new Color(0.06f, 0.24f, 0.33f),
                grass = new[] { new Color(0.30f, 0.50f, 0.24f), new Color(0.45f, 0.62f, 0.30f), new Color(0.20f, 0.36f, 0.20f), new Color(0.56f, 0.56f, 0.33f) },
            },
            ["gorge"] = new Look
            {
                scene = "Gorge", terrain = "Terrain_gorge", dayMusic = "gorge_day", extent = Rect.MinMaxRect(-34f, -44f, 34f, 40f),
                shallow = new Color(0.36f, 0.80f, 0.68f), deep = new Color(0.04f, 0.30f, 0.30f),
                grass = new[] { new Color(0.27f, 0.42f, 0.22f), new Color(0.40f, 0.53f, 0.27f), new Color(0.17f, 0.30f, 0.17f), new Color(0.52f, 0.52f, 0.33f) },
                slopeRock = 0.26f, slopeDirt = 0.16f, grassDensity = 30f,
            },
            ["estuary"] = new Look
            {
                scene = "Estuary", terrain = "Terrain_estuary", dayMusic = "estuary_day", extent = Rect.MinMaxRect(-34f, -44f, 34f, 40f),
                shallow = new Color(0.46f, 0.83f, 0.80f), deep = new Color(0.05f, 0.26f, 0.42f),
                grass = new[] { new Color(0.42f, 0.52f, 0.25f), new Color(0.56f, 0.60f, 0.32f), new Color(0.31f, 0.41f, 0.21f), new Color(0.72f, 0.66f, 0.42f) },
                shore = 0.55f, grassDensity = 18f, seaMargin = 70f,
            },
        };

        // Meshy props: height or largest-dimension fit and whether they get a solid collider.
        static readonly HashSet<string> FitByHeight = new() { "Prop_Willow", "Prop_Lighthouse" };

        [MenuItem("CozyFishing/Build Everything (Content + All Scenes)")]
        public static void BuildEverything()
        {
            ConfigurePipeline();
            CharacterImporter.ImportAll();
            ContentBuilder.Build();
            MiraImporter.Import();
            foreach (var npc in new[] { "NPC_Bayu", "NPC_Joko", "NPC_Wati", "NPC_Dimas", "NPC_Felix" }) NpcImporter.Import(npc);
            foreach (var id in Looks.Keys) Build(id);
            TitleSceneBuilder.Build();
            EditorBuildSettings.scenes = new[] { "Title", "River", "Lake", "Gorge", "Estuary" }
                .Select(s => new EditorBuildSettingsScene($"Assets/Scenes/{s}.unity", true)).ToArray();
            Debug.Log("[LocationSceneBuilder] all scenes built");
        }

        [MenuItem("CozyFishing/Scenes/Build River")] public static void BuildRiver() => Build("river");
        [MenuItem("CozyFishing/Scenes/Build Lake")] public static void BuildLake() => Build("lake");
        [MenuItem("CozyFishing/Scenes/Build Gorge")] public static void BuildGorge() => Build("gorge");
        [MenuItem("CozyFishing/Scenes/Build Estuary")] public static void BuildEstuary() => Build("estuary");

        public static void Build(string id)
        {
            var look = Looks[id];
            // Create the scene first: NewScene unloads unused assets, which would orphan anything loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText($"ArtSource/locations/{id}.json"));
            var ripple = ShaderMaterial("Ripple", "CozyFishing/Ripple");
            var lanterns = new List<Light>();

            // --- terrain & water -----------------------------------------------------------------
            var level = new GameObject("Level").transform;
            string terrainPath = $"{Models}/Level/{look.terrain}.fbx";
            var ti = (ModelImporter)AssetImporter.GetAtPath(terrainPath);
            if (!ti.isReadable) { ti.isReadable = true; ti.SaveAndReimport(); }
            var terrain = Spawn(terrainPath, level);
            terrain.name = "Terrain";
            foreach (var mf in terrain.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
            var terrainMat = TerrainMaterial(id, look);
            foreach (var r in terrain.GetComponentsInChildren<MeshRenderer>())
                r.sharedMaterials = Enumerable.Repeat(terrainMat, r.sharedMaterials.Length).ToArray();
            MarkStatic(terrain);
            BuildWater(id, look, level);
            Physics.SyncTransforms();

            // --- props ------------------------------------------------------------------------------
            var props = new GameObject("Props").transform;
            var dockItem = layout.items.First(i => i.asset == "Dock");
            var dockT = PlaceDock(dockItem, props, lanterns);
            Transform stall = null, cabinT = null;
            var spawnXZ = new Vector3(layout.spawn.x, 0f, layout.spawn.y);
            foreach (var item in layout.items)
            {
                if (item.asset == "Dock") continue;
                var go = PlaceItem(item, props, dockT, spawnXZ, lanterns);
                if (go == null) continue;
                if (item.asset == "Prop_MarketStall") stall = go.transform;
                if (item.asset == "Cabin") cabinT = go.transform;
            }
            Physics.SyncTransforms();

            // --- lighting & atmosphere -----------------------------------------------------------------
            var sun = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 30f;
            RenderSettings.fogEndDistance = 110f;
            RenderSettings.skybox = null;
            var volume = new GameObject("Post Processing", typeof(Volume)).GetComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = PostProfile();

            // --- boat, camera, systems --------------------------------------------------------------
            var boat = BoatRig.Build(dockT, lanterns);
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraRig));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 220f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.79f, 0.88f, 0.92f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            var rig = camGo.GetComponent<CameraRig>();
            Set(rig, "cam", cam);
            Set(rig, "volume", volume);
            SetVector2(rig, "boundsMin", look.extent.min);
            SetVector2(rig, "boundsMax", look.extent.max);
            var area = new GameObject("PlayArea").AddComponent<PlayArea>();
            area.transform.SetParent(level, false);
            area.EditorSetup(look.extent.min, look.extent.max);
            var outdoor = new GameObject("OutdoorZone").AddComponent<CameraZone>();
            outdoor.transform.SetParent(level, false);
            outdoor.EditorSetup(look.extent.min, look.extent.max, 11f, 50f, false);

            var systems = new GameObject("Systems");
            systems.AddComponent<GameInput>();
            var tod = systems.AddComponent<TimeOfDay>();
            Set(tod, "sun", sun);
            systems.AddComponent<StoryDirector>();
            systems.AddComponent<LocationAmbience>().EditorSetup(look.dayMusic, "night", id, tod);
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            var fishing = boat.Fishing;
            Set(fishing, "cameraRig", rig);
            Set(fishing, "timeOfDay", tod);

            // --- player (both heroes; CharacterSelector enables the chosen one) -------------------------
            var spawnPos = Ground(new Vector3(layout.spawn.x, 0f, layout.spawn.y));
            var face = dockT.position - spawnPos; face.y = 0f;
            var player = new GameObject("Player");
            player.transform.SetPositionAndRotation(spawnPos, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? face : Vector3.forward));
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.86f, 0f); cc.stepOffset = 0.45f; cc.slopeLimit = 50f;
            var walker = player.AddComponent<PlayerController>();
            Set(walker, "cameraTransform", camGo.transform);
            var interactor = player.AddComponent<PlayerInteractor>();
            var arka = Hero(player.transform, "FisherM", "Kai", ("outfit_kai_raincoat", "Raincoat"), ("outfit_kai_summer", "Summer"));
            var laras = Hero(player.transform, "FisherF", "Lena", ("outfit_lena_raincoat", "Raincoat"), ("outfit_lena_summer", "Summer"));
            laras.gameObject.SetActive(false);
            var rodHolder = boat.RodRoot.gameObject.AddComponent<RodHolder>();
            Set(rodHolder, "character", arka.GetComponent<Animator>());

            var modes = player.AddComponent<PlayerModes>();
            Set(modes, "walker", walker);
            Set(modes, "interactor", interactor);
            Set(modes, "avatar", arka);
            Set(modes, "boat", boat.Controller);
            Set(modes, "fishing", fishing);
            Set(modes, "oars", boat.Rowing);
            Set(modes, "cameraRig", rig);
            Set(modes, "seat", boat.Seat);
            Set(boat.Root.AddComponent<BoatDockSensor>(), "modes", modes);
            player.AddComponent<CharacterSelector>().EditorSetup(arka, laras, modes, rodHolder);

            var board = new GameObject("BoardBoatPoint", typeof(BoardBoatPoint)).transform;
            board.position = dockT.TransformPoint(new Vector3(-0.75f, 0.58f, 4.4f));
            board.rotation = Quaternion.LookRotation(-dockT.forward, Vector3.up);
            Set(board.GetComponent<BoardBoatPoint>(), "modes", modes);
            Set(board.GetComponent<BoardBoatPoint>(), "boat", boat.Root.transform);
            Set(modes, "dockStandPoint", board);
            Set(rig, "target", player.transform);

            // --- river only: Grandpa's cabin with its interior ---------------------------------------------
            if (cabinT != null) BuildCabin(cabinT, level, outdoor, tod, lanterns);
            SetList(tod, "lanterns", lanterns);
            if (stall != null) PlaceMira(stall);

            // --- fishing spots & fish ---------------------------------------------------------------------
            var spots = new GameObject("Fishing Spots").transform;
            foreach (var s in layout.spots)
            {
                var go = new GameObject($"Spot - {s.name}", typeof(SphereCollider));
                go.transform.SetParent(spots, false);
                go.transform.position = new Vector3(s.x, 0f, s.y);
                var spot = go.AddComponent<FishingSpot>();
                Set(spot, "spotName", s.name);
                Set(spot, "spotNameID", s.nameID);
                SetInt(spot, "habitat", (int)s.habitat.Split('|').Aggregate(Habitat.None, (h, n) => h | (Habitat)Enum.Parse(typeof(Habitat), n)));
                Set(spot, "rippleMaterial", ripple);
                Set(spot, "bubbleTemplate", ShaderMaterial("Bubble", "Universal Render Pipeline/Particles/Unlit"));
            }
            var schools = new GameObject("Fish Schools").transform;
            foreach (var s in layout.schools)
            {
                var go = new GameObject($"School - {s.fish}");
                go.transform.SetParent(schools, false);
                go.transform.position = new Vector3(s.x, 0f, s.y);
                var school = go.AddComponent<FishSchool>();
                Set(school, "fishPrefab", AssetDatabase.LoadAssetAtPath<GameObject>($"{Models}/Fish/{s.fish}.fbx"));
                Set(school, "threat", boat.Root.transform);
                SetInt(school, "count", s.count);
                SetFloat(school, "areaRadius", s.radius);
                SetFloat(school, "depth", s.depth);
            }

            // The dock fishing stand needs the spots to exist (it fishes the one in front of the dock).
            BuildDockFishing(dockT, boat, modes, rig, tod);

            // --- animals and villagers -------------------------------------------------------------------------
            AmbientBuilder.Build(id, look.extent, dockT, cabinT, schools.Cast<Transform>().ToArray());
            // The edge of the play area: a fence on land, floats on the water (no invisible walls).
            BoundaryBuilder.Build(look.extent, level);

            // --- grass, UI ----------------------------------------------------------------------------------
            BuildGrass(terrain, props, look, player.transform, boat.Root.transform);
            new GameObject("TouchControls").AddComponent<TouchControls>();
            new GameObject("InteractionPrompt").AddComponent<InteractionPrompt>();
            var hud = new GameObject("HUD").AddComponent<HUD>();
            Set(hud, "fishing", fishing);
            Set(hud, "timeOfDay", tod);
            Set(hud, "modes", modes);

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{look.scene}.unity");
            Debug.Log($"[LocationSceneBuilder] {look.scene} built");
        }

        // ------------------------------------------------------------------------------------------ world
        static Material TerrainMaterial(string id, Look look)
        {
            var m = ShaderMaterial(id == "river" ? "Terrain_Stylized" : $"Terrain_{look.scene}", "CozyFishing/StylizedTerrain");
            if (look.grass != null)
            {
                m.SetColor("_GrassA", look.grass[0]); m.SetColor("_GrassB", look.grass[1]);
                m.SetColor("_GrassC", look.grass[2]); m.SetColor("_GrassDry", look.grass[3]);
            }
            m.SetFloat("_SlopeRock", look.slopeRock);
            m.SetFloat("_SlopeDirt", look.slopeDirt);
            m.SetFloat("_ShoreHeight", look.shore);
            return m;
        }

        static Material WaterMaterial(string name, Color shallow, Color deep)
        {
            var m = ShaderMaterial(name, "CozyFishing/StylizedWater");
            m.SetColor("_ShallowColor", shallow);
            m.SetColor("_DeepColor", deep);
            m.SetFloat("_FoamDistance", 0.22f);
            return m;
        }

        static void BuildWater(string id, Look look, Transform level)
        {
            if (id == "river")
            {
                var main = WaterMaterial("Water_River", look.shallow, look.deep);
                var upper = WaterMaterial("Water_Upper", new Color(0.4f, 0.8f, 0.78f), new Color(0.1f, 0.35f, 0.4f));
                var fall = ShaderMaterial("Waterfall", "CozyFishing/Waterfall");
                foreach (var (name, mat) in new[] { ("Water_Main", main), ("Water_Upper", upper), ("Waterfall", fall) })
                    foreach (var r in Spawn($"{Models}/Level/{name}.fbx", level).GetComponentsInChildren<MeshRenderer>())
                    {
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                return;
            }
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(level, false);
            var e = look.extent;
            float m = look.seaMargin, pad = 6f;
            // The estuary's sea runs out past the east edge to the horizon.
            var rect = Rect.MinMaxRect(e.xMin - pad, e.yMin - pad, e.xMax + pad + m, e.yMax + pad);
            water.transform.position = new Vector3(rect.center.x, 0f, rect.center.y);
            water.transform.localScale = new Vector3(rect.width / 10f, 1f, rect.height / 10f);
            var wr = water.GetComponent<MeshRenderer>();
            wr.sharedMaterial = WaterMaterial($"Water_{look.scene}", look.shallow, look.deep);
            wr.shadowCastingMode = ShadowCastingMode.Off;
        }

        static Transform PlaceDock(Item item, Transform props, List<Light> lanterns)
        {
            var go = Spawn($"{Models}/Dock.fbx", props);
            go.transform.SetPositionAndRotation(new Vector3(item.x, 0f, item.y), Quaternion.Euler(0f, -item.rot, 0f));
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>();
            SnapDockToShore(go.transform);
            BuildDockSteps(go.transform);
            // Invisible rails along both sides and the far end: the deck is 2.2 m wide, 6 m long (local +Z over the water).
            var rails = new GameObject("DockRails").transform;
            rails.SetParent(go.transform, false);
            Blocker(rails, new Vector3(-1.16f, 1.0f, 3.25f), new Vector3(0.1f, 1.4f, 5.6f));
            Blocker(rails, new Vector3(1.19f, 1.0f, 3.25f), new Vector3(0.1f, 1.4f, 5.6f));
            Blocker(rails, new Vector3(0f, 1.0f, 6.1f), new Vector3(2.4f, 1.4f, 0.1f));
            MarkStatic(go);
            lanterns.Add(PointLight(go.transform, new Vector3(-0.61f, 1.95f, 5.85f), 6f));
            var zone = new GameObject("DockZone", typeof(SphereCollider), typeof(DockZone));
            zone.transform.SetParent(go.transform, false);
            zone.transform.localPosition = new Vector3(0f, 0f, 4.8f);
            var sc = zone.GetComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = 3.6f;
            Physics.SyncTransforms();
            return go.transform;
        }

        static GameObject PlaceItem(Item item, Transform props, Transform dock, Vector3 spawn, List<Light> lanterns)
        {
            var pos = new Vector3(item.x, item.z, item.y);
            var rot = Quaternion.Euler(0f, -item.rot, 0f);
            if (item.asset == "Waterfall") return BuildWaterfall(pos, item.size, props);
            if (item.asset.StartsWith("Prop_")) return PlaceMeshyProp(item, props, spawn, lanterns);

            var go = Spawn($"{Models}/{item.asset}.fbx", props);
            float scale = item.scale <= 0f ? 1f : item.scale;
            if (item.asset == "Reeds") pos.y += 0.3f;         // clump base sits 0.3 m below its origin
            if (item.asset == "LilyPads") pos.y = 0f;
            go.transform.SetPositionAndRotation(pos, rot);
            go.transform.localScale = Vector3.one * scale;
            if (item.asset.StartsWith("Pine"))
            {
                var trunk = go.AddComponent<CapsuleCollider>();
                trunk.center = new Vector3(0f, 1.5f, 0f);
                trunk.radius = 0.35f;
                trunk.height = 3f;
            }
            else if (item.asset.StartsWith("Rock") || item.asset == "Cabin")
            {
                foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                    if (!mf.name.Contains("Roof")) mf.gameObject.AddComponent<MeshCollider>();
            }
            MarkStatic(go);
            return go;
        }

        static GameObject PlaceMeshyProp(Item item, Transform props, Vector3 spawn, List<Light> lanterns)
        {
            var go = SpawnMeshyProp(item.asset, props);
            if (go == null) return null;
            go.name = item.asset;
            if (FitByHeight.Contains(item.asset)) FitHeight(go, item.size); else Fit(go, item.size);
            var pos = new Vector3(item.x, 0f, item.y);
            float yaw;
            if (item.asset == "Prop_Willow" || item.asset == "Prop_Lighthouse") yaw = -item.rot;
            else
            {
                // Buildings, stalls and the map board turn their fronts (Meshy +Z) toward where the player arrives.
                var to = spawn - pos; to.y = 0f;
                if (to.sqrMagnitude < 0.01f) to = Vector3.forward;
                yaw = Quaternion.LookRotation(to).eulerAngles.y;
            }
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var b = Bounds(go);
            float ground = new[] { b.min, b.max, new Vector3(b.min.x, 0f, b.max.z), new Vector3(b.max.x, 0f, b.min.z), b.center }
                .Min(c => TerrainHeight(new Vector3(c.x, 0f, c.z)));
            SitOn(go, Mathf.Max(ground, TerrainHeight(pos) - 0.25f), 0.06f);

            b = Bounds(go);
            if (item.asset == "Prop_Willow")
            {
                var trunk = go.AddComponent<CapsuleCollider>();
                float s = go.transform.lossyScale.y;
                trunk.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + 1.5f, b.center.z));
                trunk.radius = 0.45f / s;
                trunk.height = 3f / s;
            }
            else
            {
                var box = go.AddComponent<BoxCollider>();
                var ls = go.transform.lossyScale;
                box.center = go.transform.InverseTransformPoint(b.center);
                // Bounds are axis aligned: shrink a little so rotated props don't block the path around them.
                box.size = new Vector3(b.size.x / ls.x, b.size.y / ls.y, b.size.z / ls.z) * 0.8f;
            }
            MarkStatic(go);

            switch (item.asset)
            {
                case "Prop_MarketStall":
                    Interaction<MarketStall>(go.transform, b, 2.4f);
                    break;
                case "Prop_MapBoard":
                    Interaction<MapBoard>(go.transform, b, 2.0f);
                    break;
                case "Prop_Lighthouse":
                    var beam = PointLight(go.transform, Vector3.zero, 18f);
                    beam.transform.position = new Vector3(b.center.x, b.max.y - b.size.y * 0.12f, b.center.z);
                    lanterns.Add(beam);
                    break;
                case "Prop_Boathouse":
                    lanterns.Add(PointLight(go.transform, go.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y + 2.2f, b.center.z) + go.transform.forward * (b.extents.z + 0.3f)), 6f));
                    break;
            }
            return go;
        }

        static void Interaction<T>(Transform prop, Bounds b, float radius) where T : Interactable
        {
            var go = new GameObject(typeof(T).Name, typeof(T));
            go.transform.SetParent(prop, false);
            go.transform.position = new Vector3(b.center.x, b.min.y, b.center.z) + prop.forward * (Mathf.Max(b.extents.x, b.extents.z) * 0.6f + 0.6f);
            SetFloat(go.GetComponent<T>(), "radius", radius);
        }

        static GameObject BuildWaterfall(Vector3 lip, float width, Transform parent)
        {
            // A sheet of falling water down the north wall into the pool (camera looks north, so it faces south).
            var fall = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fall.name = "Waterfall";
            Object.DestroyImmediate(fall.GetComponent<Collider>());
            fall.transform.SetParent(parent, false);
            float h = lip.y + 0.4f;
            fall.transform.position = new Vector3(lip.x, h / 2f - 0.3f, lip.z - 1.3f);
            fall.transform.rotation = Quaternion.Euler(-8f, 0f, 0f);
            fall.transform.localScale = new Vector3(width, h, 1f);
            var r = fall.GetComponent<MeshRenderer>();
            r.sharedMaterial = ShaderMaterial("Waterfall", "CozyFishing/Waterfall");
            r.shadowCastingMode = ShadowCastingMode.Off;
            return fall;
        }

        static void PlaceMira(Transform stall)
        {
            if (!File.Exists(MiraImporter.ModelPath)) return;
            var b = Bounds(stall.gameObject);
            var go = Spawn(MiraImporter.ModelPath, null);
            go.name = "NPC_Mira";
            // Beside the stall on whichever side is dry land (some stalls stand right at the water's edge).
            var c = new Vector3(b.center.x, 0f, b.center.z);
            float side = Mathf.Max(b.extents.x, b.extents.z) * 0.7f + 0.7f;
            var pos = new[] { -stall.right, stall.right, stall.forward - stall.right, stall.forward + stall.right, stall.forward }
                .Select(d => c + d.normalized * side)
                .OrderByDescending(p => Mathf.Min(TerrainHeight(p), 0.6f) - Vector3.Distance(p, c) * 0.01f)
                .First();
            go.transform.SetPositionAndRotation(Ground(pos, 0f), stall.rotation);
            FitHeight(go, 1.62f);
            SitOn(go, TerrainHeight(go.transform.position));
            if (!go.TryGetComponent<Animator>(out var anim)) anim = go.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(MiraImporter.ControllerPath);
            anim.applyRootMotion = false;
            go.AddComponent<NpcMira>().EditorSetup(anim);
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.8f / go.transform.lossyScale.y, 0f);
            col.radius = 0.3f / go.transform.lossyScale.x;
            col.height = 1.6f / go.transform.lossyScale.y;
        }

        static void BuildCabin(Transform cabinT, Transform level, CameraZone outdoorZone, TimeOfDay tod, List<Light> lanterns)
        {
            BuildPorchSafety(cabinT);
            var interior = InteriorBuilder.Build(level, null);
            lanterns.AddRange(interior.Lamps);
            DecorBuilder.Build(interior.Root, lanterns);
            var porchArrive = new GameObject("PorchArrive").transform;
            porchArrive.SetParent(cabinT, false);
            porchArrive.localPosition = new Vector3(0.6f, 0.47f, -2.25f);
            porchArrive.localRotation = Quaternion.Euler(0f, 180f, 0f);
            var roofT = cabinT.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name.Contains("Roof"));
            if (roofT != null)
            {
                roofT.gameObject.isStatic = false;
                Set(cabinT.gameObject.AddComponent<RoofFader>(), "roof", roofT);
            }
            var enter = new GameObject("CabinDoor", typeof(DoorPortal));
            enter.transform.SetParent(cabinT, false);
            enter.transform.localPosition = new Vector3(0.6f, 0.47f, -1.75f);
            Set(enter.GetComponent<DoorPortal>(), "destination", interior.InsideSpawn);
            Set(enter.GetComponent<DoorPortal>(), "destinationZone", interior.Zone);
            SetFloat(enter.GetComponent<DoorPortal>(), "radius", 2.4f);
            var exitDoor = interior.ExitPortal.gameObject.AddComponent<DoorPortal>();
            Set(exitDoor, "destination", porchArrive);
            Set(exitDoor, "destinationZone", outdoorZone);
            SetBool(exitDoor, "isExit", true);
            var bedT = interior.Root.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Prop_Bed"));
            if (bedT != null) Set(bedT.gameObject.AddComponent<Bed>(), "timeOfDay", tod);
            var journal = interior.Root.Find("Journal");
            if (journal != null) journal.gameObject.AddComponent<JournalBook>();
        }

        /// <summary>
        /// "Fish here" at the end of the dock: a second fishing controller (no boat) that shares the hero's rod and casts
        /// straight out over the water, with its own float and line.
        /// </summary>
        static void BuildDockFishing(Transform dock, BoatRig.Result boat, PlayerModes modes, CameraRig rig, TimeOfDay tod)
        {
            var stand = new GameObject("DockFishing").transform;
            // Left of centre and short of the end: the crates stand on the right, the lamp post at the far left corner.
            stand.SetPositionAndRotation(dock.TransformPoint(new Vector3(-0.35f, 0.6f, 5.0f)), dock.rotation);
            stand.gameObject.AddComponent<Creel>();
            var f = stand.gameObject.AddComponent<FishingController>();
            Set(f, "creel", stand.GetComponent<Creel>());
            Set(f, "rodPivot", boat.RodRoot);
            Set(f, "rodTip", boat.RodTip);
            Set(f, "bobber", BoatRig.BuildBobber());
            var lineGo = new GameObject("DockFishingLine", typeof(LineRenderer));
            lineGo.transform.SetParent(stand, false);
            var lr = lineGo.GetComponent<LineRenderer>();
            lr.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{Mats}/FishingLine.mat");
            lr.widthMultiplier = 0.012f;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.enabled = false;
            Set(f, "line", lr);
            Set(f, "cameraRig", rig);
            Set(f, "timeOfDay", tod);
            SetBool(f, "castAhead", true);
            f.enabled = false;
            var spot = Object.FindObjectsByType<FishingSpot>(FindObjectsSortMode.None)
                .OrderBy(s => (s.transform.position - (stand.position + stand.forward * 6f)).sqrMagnitude).FirstOrDefault();
            var point = stand.gameObject.AddComponent<ShoreFishingPoint>();
            point.EditorSetup(stand, f, spot, modes);
            SetFloat(point, "radius", 1.8f);
        }

        /// <summary>An invisible solid box (keeps the player on decks and porches).</summary>
        static void Blocker(Transform parent, Vector3 localCenter, Vector3 size)
        {
            var b = new GameObject("Blocker", typeof(BoxCollider)).transform;
            b.SetParent(parent, false);
            b.localPosition = localCenter;
            b.GetComponent<BoxCollider>().size = size;
        }

        /// <summary>
        /// The cabin porch is raised on posts (floor 0.45 m, x -1.95..1.95, z -2.89..-1.55 in cabin space) with open-tread
        /// stairs at x 0.4..1.4. A smooth ramp over the stairs stops the player catching on step edges, and invisible
        /// rails on the open porch edges stop them dropping into the gap under the floor.
        /// </summary>
        static void BuildPorchSafety(Transform cabin)
        {
            var root = new GameObject("PorchSafety").transform;
            root.SetParent(cabin, false);
            const float porch = 0.45f, front = -2.89f;
            Blocker(root, new Vector3(-0.78f, 0.75f, front - 0.05f), new Vector3(2.4f, 1.6f, 0.1f));   // front, left of the stairs
            Blocker(root, new Vector3(1.7f, 0.75f, front - 0.05f), new Vector3(0.6f, 1.6f, 0.1f));     // front, right of the stairs
            Blocker(root, new Vector3(-2.0f, 0.75f, -2.22f), new Vector3(0.1f, 1.6f, 1.45f));          // left side
            Blocker(root, new Vector3(2.0f, 0.75f, -2.22f), new Vector3(0.1f, 1.6f, 1.45f));           // right side
            // Ramp from the ground at the stair foot up to the porch edge (overlapping the floor so there is no gap).
            Physics.SyncTransforms();
            Vector3 footWorld = cabin.TransformPoint(new Vector3(0.9f, 0f, -3.95f));
            float ground = cabin.InverseTransformPoint(new Vector3(footWorld.x, TerrainHeight(footWorld), footWorld.z)).y;
            float run = 1.15f, rise = porch - ground;
            float angle = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
            var ramp = new GameObject("StairRamp", typeof(BoxCollider)).transform;
            ramp.SetParent(root, false);
            ramp.localPosition = new Vector3(0.9f, (porch + ground) / 2f - 0.05f, front - run / 2f + 0.1f);
            ramp.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.GetComponent<BoxCollider>().size = new Vector3(1.1f, 0.1f, run / Mathf.Cos(angle * Mathf.Deg2Rad) + 0.2f);
            Blocker(root, new Vector3(0.32f, 0.5f, front - 0.5f), new Vector3(0.08f, 1.2f, 1.0f));     // stair side rails
            Blocker(root, new Vector3(1.48f, 0.5f, front - 0.5f), new Vector3(0.08f, 1.2f, 1.0f));
        }

        static PlayerAvatar Hero(Transform player, string character, string name, params (string id, string outfit)[] outfits)
        {
            var model = Spawn(CharacterImporter.ModelPath(character), player);
            model.name = name;
            if (!model.TryGetComponent<Animator>(out var anim)) anim = model.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(CharacterImporter.ControllerPath(character));
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var avatar = model.AddComponent<PlayerAvatar>();
            model.AddComponent<RowerBody>();
            // Outfits: Meshy retextures on the hero's own UVs, swapped at runtime.
            var ids = new List<string>(); var textures = new List<Texture2D>();
            foreach (var (id, outfit) in outfits)
            {
                string src = $"ArtSource/Meshy/{character}/Outfits/{character}_{outfit}_BaseColor.png";
                string dst = $"Assets/Art/Characters/{character}/Outfits/{character}_{outfit}.png";
                if (!File.Exists(src)) continue;
                if (!File.Exists(dst))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst);
                    AssetDatabase.ImportAsset(dst);
                }
                ids.Add(id);
                textures.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(dst));
            }
            model.AddComponent<OutfitSwitcher>().EditorSetup(model.GetComponentsInChildren<SkinnedMeshRenderer>(), ids.ToArray(), textures.ToArray());
            return avatar;
        }

        static void BuildGrass(GameObject terrain, Transform props, Look look, Transform player, Transform boat)
        {
            var grassMat = ShaderMaterial("Grass_Blades", "CozyFishing/StylizedGrass");
            grassMat.enableInstancing = true;
            Material Flower(string name, Color petal, Color edge, Color centre, float petals)
            {
                var fm = ShaderMaterial(name, "CozyFishing/StylizedFlower");
                fm.enableInstancing = true;
                fm.SetColor("_Petal", petal); fm.SetColor("_PetalEdge", edge); fm.SetColor("_Centre", centre); fm.SetFloat("_Petals", petals);
                return fm;
            }
            var flowers = new[]
            {
                Flower("Flower_Daisy", new Color(0.99f, 0.97f, 0.93f), new Color(0.95f, 0.9f, 0.96f), new Color(0.98f, 0.76f, 0.22f), 8),
                Flower("Flower_Pink", new Color(0.96f, 0.66f, 0.74f), new Color(0.86f, 0.45f, 0.62f), new Color(0.99f, 0.9f, 0.6f), 5),
                Flower("Flower_Butter", new Color(1f, 0.86f, 0.36f), new Color(0.98f, 0.7f, 0.25f), new Color(0.8f, 0.45f, 0.15f), 5),
            };
            var grass = new GameObject("Grass").AddComponent<GrassField>();
            Set(grass, "terrain", terrain.GetComponentInChildren<MeshFilter>());
            Set(grass, "grassMaterial", grassMat);
            SetList(grass, "flowerMaterials", flowers);
            SetFloat(grass, "bladesPerSquareMetre", look.grassDensity);
            SetList(grass, "exclusions", props.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToList());
            SetList(grass, "pushers", new[] { player, boat });
        }

        // ------------------------------------------------------------------------------------------ dock helpers
        /// <summary>Slides the dock inland along its length until its shore end rests on dry ground (deck walkable from the bank).</summary>
        static void SnapDockToShore(Transform dock)
        {
            Physics.SyncTransforms();
            Vector3 back = -dock.forward;
            for (float s = -3f; s < 8f; s += 0.1f)
            {
                Vector3 shoreEnd = dock.position + back * s;
                if (TerrainHeight(shoreEnd + back * 0.3f) > 0.35f)
                {
                    dock.position += back * (s + 0.2f);
                    return;
                }
            }
            Debug.LogWarning($"[LocationSceneBuilder] dock at {dock.position} found no shore");
        }

        /// <summary>Two plank steps plus a hidden ramp so the player walks smoothly from the bank onto the deck.</summary>
        static void BuildDockSteps(Transform dock)
        {
            const float deck = 0.58f;
            float ground = TerrainHeight(dock.position - dock.forward * 1.1f);
            var wood = Palette("Pal_WoodMid");
            var root = new GameObject("DockSteps").transform;
            root.SetParent(dock, false);
            for (int i = 0; i < 2; i++)
            {
                float top = Mathf.Lerp(ground, deck, (i + 1) / 3f);
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = $"Step{i}";
                Object.DestroyImmediate(step.GetComponent<Collider>());
                step.transform.SetParent(root, false);
                float h = top - ground + 0.15f;
                step.transform.localPosition = new Vector3(0f, top - h / 2f, -0.55f + i * 0.34f);
                step.transform.localScale = new Vector3(1.6f, h, 0.36f);
                step.GetComponent<MeshRenderer>().sharedMaterial = wood;
            }
            var ramp = new GameObject("Ramp", typeof(BoxCollider)).transform;
            ramp.SetParent(root, false);
            // The ramp overlaps the first deck plank so there is no gap to fall through.
            float len = 1.3f;
            float angle = Mathf.Atan2(deck - ground, len) * Mathf.Rad2Deg;
            ramp.localPosition = new Vector3(0f, (deck + ground) / 2f - 0.05f, -len / 2f + 0.3f);
            ramp.localRotation = Quaternion.Euler(-angle, 0f, 0f);
            ramp.GetComponent<BoxCollider>().size = new Vector3(1.8f, 0.1f, len / Mathf.Cos(angle * Mathf.Deg2Rad) + 0.1f);
        }

        // ------------------------------------------------------------------------------------------ rendering setup
        public static void ConfigurePipeline()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                asset.supportsCameraDepthTexture = true;
                asset.supportsCameraOpaqueTexture = true;
                asset.shadowDistance = 70f;
                asset.shadowCascadeCount = 2;
                asset.msaaSampleCount = 1;
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
        }

        static VolumeProfile PostProfile()
        {
            const string path = "Assets/Art/PostProcess_River.asset";
            var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (existing != null) return existing;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.9f, 0.78f));
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.35f);
            color.contrast.Override(14f);
            color.saturation.Override(6f);
            profile.Add<WhiteBalance>(true).temperature.Override(6f);
            var smh = profile.Add<ShadowsMidtonesHighlights>(true);
            smh.shadows.Override(new Vector4(0.92f, 0.98f, 1.1f, 0f));
            smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.92f, 0f));
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.12f, 0.1f, 0.14f));
            var dof = profile.Add<DepthOfField>(true);
            // Gaussian: only the far background softens (no bokeh discs, cheap on phones).
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(22f);
            dof.gaussianEnd.Override(60f);
            dof.gaussianMaxRadius.Override(1.2f);
            dof.highQualitySampling.Override(false);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }
    }
}
