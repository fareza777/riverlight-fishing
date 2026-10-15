using CozyFishing.Core;
using CozyFishing.World;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>Sleep until the next morning. Only offered in the evening/night so the day isn't skipped by accident.</summary>
    public sealed class Bed : Interactable
    {
        [SerializeField] TimeOfDay timeOfDay;
        [SerializeField, Range(0f, 24f)] float wakeHour = 6.5f;
        [SerializeField, Range(0f, 24f)] float sleepFrom = 17.5f;

        public static event System.Action Slept;

        public override string Prompt => Loc.T("sleep");

        public override bool CanInteract(PlayerInteractor who)
        {
            if (timeOfDay == null || ScreenFader.Busy) return false;
            float h = timeOfDay.Hour;
            return h >= sleepFrom || h < wakeHour - 0.5f;
        }

        public override void Interact(PlayerInteractor who)
        {
            ScreenFader.Transition(() =>
            {
                // Sleeping before midnight skips into the next day; after midnight the day already turned.
                if (timeOfDay.Hour >= 12f) GameState.Instance.Data.day++;
                timeOfDay.SetHour(wakeHour);
                GameState.Instance.Save();
                Slept?.Invoke();
            }, 0.8f, 1.2f);
        }
    }
}
