using System;
using UnityEngine;

namespace CozyFishing.Fishing
{
    /// <summary>
    /// A ripple-marked area where fish bite. Detected by the boat entering its trigger.
    /// Spawns its own ring ripples and bubble particles so it reads from the top-down camera.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public sealed class FishingSpot : MonoBehaviour
    {
        [SerializeField] string spotName = "Quiet Bend";
        [SerializeField] string spotNameID;
        [SerializeField] Habitat habitat = Habitat.River;
        [SerializeField] float radius = 4.5f;
        [SerializeField] Material rippleMaterial;
        [Tooltip("Template using URP Particles/Unlit; referenced so the shader is included in builds.")]
        [SerializeField] Material bubbleTemplate;
        [SerializeField] int rippleCount = 3;

        public static event Action<FishingSpot> Discovered;

        public string SpotName => Core.Loc.Current == Core.Language.Indonesian && !string.IsNullOrEmpty(spotNameID) ? spotNameID : spotName;
        public Habitat Habitat => habitat;
        public float Radius => radius;
        public bool IsDiscovered { get; private set; }

        Transform[] ripples;

        void Awake()
        {
            var trigger = GetComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = radius;
            BuildRipples();
            BuildBubbles();
        }

        void BuildRipples()
        {
            if (rippleMaterial == null) return;
            ripples = new Transform[rippleCount];
            for (int i = 0; i < rippleCount; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.name = "Ripple";
                quad.transform.SetParent(transform, false);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = rippleMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Phase", i / (float)rippleCount);
                r.SetPropertyBlock(block);
                float s = radius * 1.15f;
                quad.transform.localScale = new Vector3(s, s, 1f);
                quad.transform.localPosition = new Vector3((i - 1) * 0.6f, 0.06f + i * 0.004f, (i % 2) * 0.5f);
                ripples[i] = quad.transform;
            }
        }

        void BuildBubbles()
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
            main.startColor = new Color(1f, 1f, 1f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var emission = ps.emission;
            emission.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.45f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0f)));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = BubbleMaterial(bubbleTemplate);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
        }

        static Material bubbleMat;
        static Material BubbleMaterial(Material template)
        {
            if (bubbleMat != null) return bubbleMat;
            if (template == null)
            {
                Debug.LogWarning("[FishingSpot] No bubble material template assigned; bubbles disabled.");
                return null;
            }
            bubbleMat = new Material(template) { name = "Bubble (runtime)" };
            bubbleMat.SetColor("_BaseColor", new Color(0.95f, 1f, 1f, 0.9f));
            bubbleMat.SetTexture("_BaseMap", BubbleTexture());
            // Transparent alpha blending so the round texture's edges fade.
            bubbleMat.SetFloat("_Surface", 1f);
            bubbleMat.SetFloat("_Blend", 0f);
            bubbleMat.SetOverrideTag("RenderType", "Transparent");
            bubbleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            bubbleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            bubbleMat.SetInt("_ZWrite", 0);
            bubbleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            bubbleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 20;
            return bubbleMat;
        }

        static Texture2D BubbleTexture()
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.75f) * 6f);
                float fill = Mathf.Clamp01(1f - d) * 0.35f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Max(ring, fill) * Mathf.Clamp01((1f - d) * 8f)));
            }
            tex.Apply();
            return tex;
        }

        void OnTriggerEnter(Collider other)
        {
            if (IsDiscovered || other.attachedRigidbody == null || other.attachedRigidbody.GetComponent<FishingController>() == null) return;
            IsDiscovered = true;
            Discovered?.Invoke(this);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
