using CozyFishing.Core;
using CozyFishing.UI;

namespace CozyFishing.Interaction
{
    /// <summary>Signpost by the dock: opens the map to travel between locations.</summary>
    public sealed class MapBoard : Interactable
    {
        public override string Prompt => Loc.T("map");
        public override void Interact(PlayerInteractor who) => PauseMenu.Open(PauseMenu.Tab.Map);
    }
}
