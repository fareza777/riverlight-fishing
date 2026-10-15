using CozyFishing.Core;
using CozyFishing.UI;

namespace CozyFishing.Interaction
{
    /// <summary>Grandpa's journal on the cabin table.</summary>
    public sealed class JournalBook : Interactable
    {
        public override string Prompt => Loc.T("read");
        public override void Interact(PlayerInteractor who) => PauseMenu.Open(PauseMenu.Tab.Journal);
    }
}
