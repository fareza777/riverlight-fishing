using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CozyFishing.Data;
using CozyFishing.Fishing;
using CozyFishing.Story;
using UnityEditor;
using UnityEngine;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// CozyFishing/Build Content: creates every data asset from the ArtSource files and the generated art/audio,
    /// and the GameDatabase in Resources that the runtime reads.
    ///   fish      ArtSource/fish_catalog.json  + Assets/Art/Models/Fish/&lt;id&gt;.fbx + Assets/Art/UI/Fish/&lt;id&gt;.png
    ///   story     ArtSource/story.json         + Assets/Art/Story/&lt;cutscene&gt;_&lt;n&gt;.png + Resources/Audio/Voice
    ///   items     defined below                + Assets/Art/UI/Items/&lt;id&gt;.png
    ///   locations defined below                + Assets/Art/UI/Locations/&lt;id&gt;.png
    /// </summary>
    public static class ContentBuilder
    {
        const string Data = "Assets/Data";
        const string DbPath = "Assets/Resources/GameDatabase.asset";

        [Serializable] class CatalogFish
        {
            public string id, name, nameID, location, rarity, desc, descID, hint, hintID;
            public string[] habitats;
            public float activeFrom, activeTo, sizeMin, sizeMax, fight;
            public int price, chapter, rodTier;
        }
        [Serializable] class Catalog { public CatalogFish[] fish; }
        [Serializable] class StorySlide { public string en, id; }
        [Serializable] class StoryCutscene { public string id, titleEN, titleID; public bool memory; public StorySlide[] slides; }
        [Serializable] class StoryFile { public StoryCutscene[] cutscenes; }

        public static readonly (string id, string scene, string en, string idn, string descEN, string descID, int chapter)[] LocationList =
        {
            ("river", "River", "Riverlight River", "Sungai Riverlight", "Grandpa's home water: gentle bends, the old dock and the waterfall pool.", "Perairan rumah Kakek: tikungan tenang, dermaga tua, dan kolam air terjun.", 0),
            ("lake", "Lake", "Willow Lake", "Danau Willow", "A mirror-still lake ringed by willows, lilies and a little island.", "Danau setenang cermin, dikelilingi dedalu, teratai, dan sebuah pulau kecil.", 2),
            ("gorge", "Gorge", "Pinefall Gorge", "Ngarai Pinefall", "Cold rapids between stone cliffs, and a deep pool under the great falls.", "Jeram dingin di antara tebing batu, dan kolam dalam di bawah air terjun besar.", 3),
            ("estuary", "Estuary", "Saltreed Estuary", "Muara Saltreed", "Where the river meets the sea: reed mazes, sand flats and the old lighthouse.", "Tempat sungai bertemu laut: labirin ilalang, dataran pasir, dan mercusuar tua.", 4),
        };

        [MenuItem("CozyFishing/Build Content")]
        public static void Build()
        {
            PaletteMaterialBuilder.Build();
            ConfigureAudio();
            Directory.CreateDirectory("Assets/Resources");
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DbPath);
            if (db == null)
            {
                db = ScriptableObject.CreateInstance<GameDatabase>();
                AssetDatabase.CreateAsset(db, DbPath);
            }
            db.fish = BuildFish();
            db.items = BuildItems();
            db.locations = BuildLocations();
            db.cutscenes = BuildCutscenes();
            db.portraitArka = Portrait("FisherM", "arka");
            db.portraitLaras = Portrait("FisherF", "laras");
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Content] {db.fish.Count} fish, {db.items.Count} items, {db.locations.Count} locations, {db.cutscenes.Count} cutscenes");
        }

        static T Asset<T>(string path) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a != null) return a;
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        // ------------------------------------------------------------------ fish
        static List<FishSpecies> BuildFish()
        {
            // The six prototype species are replaced by the catalogue.
            foreach (var old in new[] { "SilverBream", "BrookTrout", "Perch", "Pike", "GoldenCarp", "MoonEel" })
                AssetDatabase.DeleteAsset($"{Data}/Fish/Fish_{old}.asset");
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText("ArtSource/fish_catalog.json"));
            var list = new List<FishSpecies>();
            foreach (var f in catalog.fish)
            {
                var so = Asset<FishSpecies>($"{Data}/Fish/{f.id}.asset");
                var habitat = f.habitats.Aggregate(Habitat.None, (h, s) => h | (Habitat)Enum.Parse(typeof(Habitat), s));
                var spec = new FishSpecies.Spec
                {
                    id = f.id, name = f.name, nameID = f.nameID, location = f.location, desc = f.desc, descID = f.descID,
                    hintEN = f.hint, hintID = f.hintID, rarity = (FishRarity)Enum.Parse(typeof(FishRarity), f.rarity), habitats = habitat,
                    from = f.activeFrom, to = f.activeTo, fight = f.fight, size = new Vector2(f.sizeMin, f.sizeMax),
                    price = f.price, rodTier = f.rodTier, chapter = f.chapter,
                };
                var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{SceneKit.Models}/Fish/{f.id}.fbx");
                if (model == null) Debug.LogWarning($"[Content] no model for {f.id}");
                so.EditorSetup(spec, model, SceneKit.Sprite($"Assets/Art/UI/Fish/{f.id}.png", 512));
                EditorUtility.SetDirty(so);
                list.Add(so);
            }
            return list;
        }

        // ------------------------------------------------------------------ items
        static List<ItemDef> BuildItems()
        {
            var defs = new (string id, ItemType type, string en, string idn, string dEN, string dID, int price, int tier, float band, float reel, float rare, int cap)[]
            {
                ("rod_willow", ItemType.Rod, "Willow Rod", "Joran Dedalu", "Grandpa's spare rod. Light and forgiving.", "Joran cadangan Kakek. Ringan dan mudah dipakai.", 0, 0, 0f, 0f, 0f, 0),
                ("rod_ash", ItemType.Rod, "Ash Rod", "Joran Abu", "Springy ash wood. A wider safe zone when reeling.", "Kayu abu yang lentur. Zona aman lebih lebar saat menggulung.", 200, 1, 0.05f, 0.1f, 0f, 0),
                ("rod_oak", ItemType.Rod, "Oak Rod", "Joran Ek", "Stiff dark oak with an iron reel. Holds the gorge giants.", "Kayu ek gelap yang kaku dengan gulungan besi. Kuat untuk ikan raksasa ngarai.", 700, 2, 0.08f, 0.2f, 0f, 0),
                ("rod_sea", ItemType.Rod, "Sea Rod", "Joran Laut", "Heavy sea rod with a chrome reel, built for salt water.", "Joran laut berat dengan gulungan krom, untuk air asin.", 1600, 3, 0.11f, 0.32f, 0f, 0),
                ("rod_grandpa", ItemType.Rod, "Grandpa's Bamboo Rod", "Joran Bambu Kakek", "Wrapped in red thread by Grandpa's own hands. Nothing escapes it.", "Dililit benang merah oleh tangan Kakek sendiri. Tak ada yang lolos darinya.", 0, 4, 0.14f, 0.4f, 0f, 0),
                ("bait_worms", ItemType.Bait, "Worms (x5)", "Cacing (x5)", "Fresh earthworms. Uncommon fish bite more often.", "Cacing tanah segar. Ikan langka lebih sering makan.", 20, 0, 0f, 0f, 0.5f, 0),
                ("bait_glowworm", ItemType.Bait, "Glowworms (x5)", "Ulat Cahaya (x5)", "They shine in dark water. Rare fish can't resist.", "Bercahaya di air gelap. Ikan langka tak bisa menolak.", 60, 0, 0f, 0f, 1.0f, 0),
                ("bait_shrimp", ItemType.Bait, "Shrimp (x5)", "Udang (x5)", "Plump shrimp. The best bait for rare and legendary fish.", "Udang gemuk. Umpan terbaik untuk ikan langka dan legendaris.", 120, 0, 0f, 0f, 1.5f, 0),
                ("creel_small", ItemType.Creel, "Small Creel", "Keranjang Kecil", "Holds 8 fish.", "Muat 8 ikan.", 0, 0, 0f, 0f, 0f, 8),
                ("creel_medium", ItemType.Creel, "Medium Creel", "Keranjang Sedang", "Holds 12 fish.", "Muat 12 ikan.", 300, 0, 0f, 0f, 0f, 12),
                ("creel_large", ItemType.Creel, "Large Creel", "Keranjang Besar", "Holds 16 fish.", "Muat 16 ikan.", 900, 0, 0f, 0f, 0f, 16),
                ("creel_grand", ItemType.Creel, "Grand Creel", "Keranjang Agung", "Holds 24 fish.", "Muat 24 ikan.", 2000, 0, 0f, 0f, 0f, 24),
            };
            var list = new List<ItemDef>();
            foreach (var d in defs)
            {
                var it = Asset<ItemDef>($"{Data}/Items/{d.id}.asset");
                it.id = d.id; it.type = d.type; it.nameEN = d.en; it.nameID = d.idn; it.descEN = d.dEN; it.descID = d.dID;
                it.price = d.price; it.tier = d.tier; it.safeBandBonus = d.band; it.reelBonus = d.reel; it.rareBonus = d.rare; it.capacity = d.cap;
                it.icon = SceneKit.Sprite($"Assets/Art/UI/Items/{d.id}.png", 256);
                EditorUtility.SetDirty(it);
                list.Add(it);
            }
            return list;
        }

        // ------------------------------------------------------------------ locations
        static List<LocationDef> BuildLocations()
        {
            var list = new List<LocationDef>();
            foreach (var l in LocationList)
            {
                var loc = Asset<LocationDef>($"{Data}/Locations/{l.id}.asset");
                loc.id = l.id; loc.sceneName = l.scene; loc.nameEN = l.en; loc.nameID = l.idn; loc.descEN = l.descEN; loc.descID = l.descID;
                loc.unlockChapter = l.chapter;
                loc.postcard = SceneKit.Sprite($"Assets/Art/UI/Locations/{l.id}.png", 1024);
                EditorUtility.SetDirty(loc);
                list.Add(loc);
            }
            return list;
        }

        // ------------------------------------------------------------------ cutscenes
        static List<CutsceneDef> BuildCutscenes()
        {
            var story = JsonUtility.FromJson<StoryFile>(File.ReadAllText("ArtSource/story.json"));
            var list = new List<CutsceneDef>();
            var pans = new[] { (new Vector2(0f, 0f), new Vector2(0.02f, -0.02f)), (new Vector2(-0.02f, 0.01f), new Vector2(0.02f, 0f)), (new Vector2(0.02f, -0.01f), new Vector2(-0.01f, 0.02f)) };
            foreach (var c in story.cutscenes)
            {
                var def = Asset<CutsceneDef>($"{Data}/Cutscenes/{c.id}.asset");
                def.id = c.id; def.titleEN = c.titleEN; def.titleID = c.titleID; def.memory = c.memory;
                def.music = Clip("Music", c.memory ? "memory" : "title");
                def.slides = new List<CutsceneSlide>();
                for (int i = 0; i < c.slides.Length; i++)
                {
                    var (from, to) = pans[i % pans.Length];
                    def.slides.Add(new CutsceneSlide
                    {
                        image = SceneKit.Sprite($"Assets/Art/Story/{c.id}_{i}.png", 1536),
                        textEN = c.slides[i].en, textID = c.slides[i].id,
                        voice = Clip("Voice", $"{c.id}_{i}"), voiceID = Clip("Voice", $"{c.id}_{i}_id"),
                        voiceM = Clip("Voice", $"{c.id}_{i}_m"), voiceMID = Clip("Voice", $"{c.id}_{i}_m_id"),
                        seconds = 5.5f, zoomFrom = 1.04f, zoomTo = 1.14f, panFrom = from, panTo = to,
                    });
                }
                EditorUtility.SetDirty(def);
                list.Add(def);
            }
            return list;
        }

        static AudioClip Clip(string folder, string name)
        {
            foreach (var ext in new[] { ".mp3", ".wav", ".ogg" })
            {
                var c = AssetDatabase.LoadAssetAtPath<AudioClip>($"Assets/Resources/Audio/{folder}/{name}{ext}");
                if (c != null) return c;
            }
            return null;
        }

        static Sprite Portrait(string meshyName, string id)
        {
            string dst = $"Assets/Art/UI/Portraits/{id}.png";
            if (!File.Exists(dst))
            {
                var src = Directory.GetFiles($"ArtSource/Meshy/{meshyName}", "concept_*.png").FirstOrDefault();
                if (src == null) return null;
                Directory.CreateDirectory(Path.GetDirectoryName(dst));
                File.Copy(src, dst);
                AssetDatabase.ImportAsset(dst);
            }
            return SceneKit.Sprite(dst, 1024);
        }

        // ------------------------------------------------------------------ audio import
        static void ConfigureAudio()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Resources/Audio" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not AudioImporter imp) continue;
                var s = imp.defaultSampleSettings;
                bool longClip = path.Contains("/Music/") || path.Contains("/Ambience/");
                s.loadType = longClip ? AudioClipLoadType.Streaming : path.Contains("/Voice/") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = longClip ? 0.55f : 0.7f;
                imp.defaultSampleSettings = s;
                imp.forceToMono = path.Contains("/Voice/") || path.Contains("/Sfx/");
                imp.loadInBackground = longClip;
                imp.SaveAndReimport();
            }
        }
    }
}
