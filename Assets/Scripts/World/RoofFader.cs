using UnityEngine;
using UnityEngine.Rendering;

namespace CozyFishing.World
{
    /// <summary>
    /// Hides a building's roof (it keeps casting its shadow) while the player is close, so doors and porches
    /// under it are visible from the top-down camera. The roof lifts away and drops back with a short ease.
    /// </summary>
    public sealed class RoofFader : MonoBehaviour
    {
        [SerializeField] Renderer roof;
        [SerializeField] Transform player;
        [SerializeField] float radius = 5.5f;
        [SerializeField] float liftHeight = 1.2f;

        Vector3 restLocal;
        float open;

        void Start()
        {
            if (roof != null) restLocal = roof.transform.localPosition;
            if (player == null)
            {
                var modes = FindAnyObjectByType<Player.PlayerModes>();
                if (modes != null) player = modes.transform;
            }
        }

        void LateUpdate()
        {
            if (roof == null || player == null) return;
            Vector3 d = player.position - transform.position;
            d.y = 0f;
            bool near = d.magnitude < radius;
            open = Mathf.MoveTowards(open, near ? 1f : 0f, Time.deltaTime * 3.5f);
            float e = open * open * (3f - 2f * open);
            roof.transform.localPosition = restLocal + Vector3.up * (liftHeight * e);
            roof.shadowCastingMode = e > 0.35f ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }
    }
}
