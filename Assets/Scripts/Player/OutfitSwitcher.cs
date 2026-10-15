using CozyFishing.Core;
using UnityEngine;

namespace CozyFishing.Player
{
    /// <summary>Swaps the hero's clothing texture for the equipped outfit (Meshy retextures share the model's UVs).</summary>
    public sealed class OutfitSwitcher : MonoBehaviour
    {
        static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        [SerializeField] Renderer[] renderers = new Renderer[0];
        [SerializeField] string[] outfitIds = new string[0];
        [SerializeField] Texture2D[] textures = new Texture2D[0];

        Texture original;
        Material[] materials;

        void Start()
        {
            materials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) materials[i] = renderers[i].material;   // per-hero instance
            if (materials.Length > 0 && materials[0].HasProperty(BaseMap)) original = materials[0].GetTexture(BaseMap);
            Apply();
            GameState.Instance.Changed += Apply;
        }

        void OnDestroy()
        {
            if (GameState.Instance != null) GameState.Instance.Changed -= Apply;
        }

        void Apply()
        {
            if (materials == null) return;
            Texture tex = original;
            string id = GameState.Instance.Data.outfit;
            for (int i = 0; i < outfitIds.Length; i++)
                if (outfitIds[i] == id && textures[i] != null) tex = textures[i];
            foreach (var m in materials)
            {
                if (m.HasProperty(BaseMap)) m.SetTexture(BaseMap, tex);
                if (m.HasProperty(MainTex)) m.SetTexture(MainTex, tex);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(Renderer[] r, string[] ids, Texture2D[] tex) { renderers = r; outfitIds = ids; textures = tex; }
#endif
    }
}
