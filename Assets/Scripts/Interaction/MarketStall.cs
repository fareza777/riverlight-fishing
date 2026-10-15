using CozyFishing.Audio;
using CozyFishing.Core;
using CozyFishing.UI;
using UnityEngine;

namespace CozyFishing.Interaction
{
    /// <summary>The dockside market crate: sell fish, buy gear.</summary>
    public sealed class MarketStall : Interactable
    {
        public override string Prompt => Loc.T("market");
        int visits;

        public override void Interact(PlayerInteractor who)
        {
            bool newOrders = Progression.RefreshOrders();
            ShopUI.Open();
            // Mira greets the player: an introduction first, news of fresh orders, otherwise a random line.
            string key = visits++ == 0 ? "mira_0" : newOrders ? "mira_order" : "mira_" + Random.Range(1, 4);
            Toast.Show("<b>Mira:</b> " + Loc.T(key), 5f);
            VoicePlayer.Say(key);
        }
    }
}
