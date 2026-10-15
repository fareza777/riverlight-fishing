using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>
    /// Felix and his wagon visit every third day (from chapter 1). While he is in town the cart and Felix are shown
    /// and the player hears that he has arrived.
    /// </summary>
    public sealed class TravelingMerchant : MonoBehaviour
    {
        public const int VisitEvery = 3;
        const string AnnouncedKey = "merchant_announced_day";

        [SerializeField] GameObject visit;   // the cart, Felix and the trade point
        float check;

        public static bool InTown
        {
            get
            {
                var d = GameState.Instance.Data;
                return d.chapter >= 1 && d.day % VisitEvery == 0;
            }
        }

        void Start() => Refresh(true);

        void Update()
        {
            check -= Time.deltaTime;
            if (check > 0f) return;
            check = 2f;
            Refresh(false);
        }

        void Refresh(bool first)
        {
            bool here = InTown;
            if (visit.activeSelf != here) visit.SetActive(here);
            int day = GameState.Instance.Data.day;
            if (here && PlayerPrefs.GetInt(AnnouncedKey, -1) != day && !Story.CutscenePlayer.Playing)
            {
                PlayerPrefs.SetInt(AnnouncedKey, day);
                Toast.Show(Loc.T("felix_arrive"), 6f);
                VoicePlayer.Say("felix_arrive");
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject v) => visit = v;
#endif
    }
}
