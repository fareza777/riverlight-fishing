using System.Collections.Generic;
using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Fishing
{
    public readonly struct CaughtFish
    {
        public CaughtFish(FishSpecies species, float sizeCm)
        {
            Species = species;
            SizeCm = sizeCm;
        }

        public FishSpecies Species { get; }
        public float SizeCm { get; }
    }

    /// <summary>The wicker creel in the boat. Contents live in <see cref="GameState"/> so they persist across scenes.</summary>
    public sealed class Creel : MonoBehaviour
    {
        public int Capacity => GameState.Instance.CreelCapacity;
        public int Count => GameState.Instance.Data.creel.Count;
        public bool IsFull => Count >= Capacity;
        public IEnumerable<CreelFish> Slots => GameState.Instance.Data.creel;

        /// <returns>true when this species was new to the journal.</returns>
        public bool Add(CaughtFish fish) => GameState.Instance.RecordCatch(fish.Species, fish.SizeCm);
    }
}
