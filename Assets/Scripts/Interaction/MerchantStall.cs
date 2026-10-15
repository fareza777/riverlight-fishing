using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>Felix's wagon: opens his stock (rare bait, special decor, outfits) with one of his voiced lines.</summary>
    public sealed class MerchantStall : Interactable
    {
        [SerializeField] Animator felix;
        int line;

        public override string Prompt => Loc.T("trade");

        public override void Interact(PlayerInteractor who)
        {
            ShopUI.Open(true);
            string key = "felix_" + line;
            line = (line + 1) % 3;
            Toast.Show("<b>Felix:</b> " + Loc.T(key), 5f);
            VoicePlayer.Say(key);
            if (felix != null) felix.SetTrigger("Wave");
        }

#if UNITY_EDITOR
        public void EditorSetup(Animator a) => felix = a;
#endif
    }
}
