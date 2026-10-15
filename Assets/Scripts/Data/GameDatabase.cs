using System.Collections.Generic;
using System.Linq;
using CozyFishing.Fishing;
using CozyFishing.Story;
using UnityEngine;

namespace CozyFishing.Data
{
    /// <summary>All static game content. Lives at Resources/GameDatabase so any scene can reach it.</summary>
    [CreateAssetMenu(menuName = "CozyFishing/Game Database", fileName = "GameDatabase")]
    public sealed class GameDatabase : ScriptableObject
    {
        public List<FishSpecies> fish = new();
        public List<ItemDef> items = new();
        public List<LocationDef> locations = new();
        public List<CutsceneDef> cutscenes = new();
        public Sprite portraitArka, portraitLaras;

        static GameDatabase instance;
        public static GameDatabase Instance => instance != null ? instance : instance = Resources.Load<GameDatabase>("GameDatabase");

        public FishSpecies Fish(string id) => fish.FirstOrDefault(f => f != null && f.Id == id);
        public ItemDef Item(string id) => items.FirstOrDefault(i => i != null && i.id == id);
        public LocationDef Location(string id) => locations.FirstOrDefault(l => l != null && l.id == id);
        public LocationDef LocationForScene(string scene) => locations.FirstOrDefault(l => l != null && l.sceneName == scene);
        public CutsceneDef Cutscene(string id) => cutscenes.FirstOrDefault(c => c != null && c.id == id);
        public IEnumerable<FishSpecies> FishAt(string location) => fish.Where(f => f != null && f.Location == location);
    }
}
