using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>Activates the hero chosen on the title screen (Kai / Lena) and rewires the systems that use it.</summary>
    [DefaultExecutionOrder(-150)]
    public sealed class CharacterSelector : MonoBehaviour
    {
        [SerializeField] PlayerAvatar arka;
        [SerializeField] PlayerAvatar laras;
        [SerializeField] PlayerModes modes;
        [SerializeField] RodHolder rod;

        void Awake()
        {
            bool female = GameState.Instance.Data.character == "FisherF" && laras != null;
            var chosen = female ? laras : arka;
            if (arka != null) arka.gameObject.SetActive(!female);
            if (laras != null) laras.gameObject.SetActive(female);
            modes.SetAvatar(chosen);
            if (rod != null) rod.Bind(chosen.GetComponent<Animator>());
        }

#if UNITY_EDITOR
        public void EditorSetup(PlayerAvatar m, PlayerAvatar f, PlayerModes pm, RodHolder r) { arka = m; laras = f; modes = pm; rod = r; }
#endif
    }
}
