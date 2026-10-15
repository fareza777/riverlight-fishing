using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.World
{
    /// <summary>Shows the furniture the player has bought for Grandpa's cabin (each piece waits in its slot, hidden).</summary>
    public sealed class CabinDecor : MonoBehaviour
    {
        [SerializeField] string[] itemIds = new string[0];
        [SerializeField] GameObject[] pieces = new GameObject[0];

        void Start()
        {
            Refresh();
            GameState.Instance.Changed += Refresh;
        }

        void OnDestroy()
        {
            if (GameState.Instance != null) GameState.Instance.Changed -= Refresh;
        }

        void Refresh()
        {
            for (int i = 0; i < pieces.Length; i++)
                if (pieces[i] != null) pieces[i].SetActive(GameState.Instance.Owns(itemIds[i]));
        }

#if UNITY_EDITOR
        public void EditorSetup(string[] ids, GameObject[] objects) { itemIds = ids; pieces = objects; }
#endif
    }
}
