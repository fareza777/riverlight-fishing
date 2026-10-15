using System.Collections.Generic;
using CozyFishing.World;
using UnityEngine;
using static CozyFishing.EditorTools.SceneKit;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Furniture the player can buy for Grandpa's cabin. Every piece is placed in its own slot inside the interior
    /// (hidden) and <see cref="CabinDecor"/> shows the ones the player owns.
    /// </summary>
    public static class DecorBuilder
    {
        enum Mount { Floor, Wall, Hang, OnTop }

        // item id, Meshy prop, size (largest dimension), interior-local position, yaw, mount
        static readonly (string id, string prop, float size, Vector3 pos, float yaw, Mount mount)[] Slots =
        {
            ("decor_rug", "Decor_Rug", 1.6f, new Vector3(0.25f, 0f, -0.75f), 0f, Mount.Floor),
            ("decor_fern", "Decor_PottedFern", 0.8f, new Vector3(2.05f, 0f, -1.55f), 225f, Mount.Floor),
            ("decor_lamp", "Decor_HangingLamp", 0.45f, new Vector3(0.95f, 1.75f, 0.05f), 0f, Mount.Hang),
            ("decor_painting", "Decor_Painting", 0.75f, new Vector3(-2.34f, 1.35f, -0.3f), 90f, Mount.Wall),
            ("decor_clock", "Decor_Clock", 1.85f, new Vector3(2.12f, 0f, -0.55f), -90f, Mount.Floor),
            ("decor_aquarium", "Decor_Aquarium", 1.0f, new Vector3(-0.3f, 0f, 1.82f), 180f, Mount.Floor),
            ("decor_gramophone", "Decor_Gramophone", 0.55f, new Vector3(-1.72f, 0f, -0.25f), 90f, Mount.OnTop),
            ("decor_shipbottle", "Decor_ShipBottle", 0.4f, new Vector3(1.25f, 0f, 0.25f), 30f, Mount.OnTop),
        };

        public static void Build(Transform interior, List<Light> lamps)
        {
            var root = new GameObject("CabinDecor").transform;
            root.SetParent(interior, false);
            var ids = new List<string>(); var pieces = new List<GameObject>();
            Physics.SyncTransforms();
            foreach (var (id, prop, size, pos, yaw, mount) in Slots)
            {
                var go = SpawnMeshyProp(prop, root);
                if (go == null) continue;
                go.name = id;
                Fit(go, size);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                go.transform.localPosition = pos;
                Vector3 world = interior.TransformPoint(pos);
                float baseY = mount switch
                {
                    // Furniture surface below (chest, table): the highest collider under the slot.
                    Mount.OnTop => Physics.Raycast(world + Vector3.up * 3f, Vector3.down, out var hit, 4f) ? hit.point.y : world.y,
                    _ => world.y,
                };
                if (mount == Mount.Wall)
                {
                    // Wall pieces hang flat: centre the bounds at the slot height.
                    var b = Bounds(go);
                    go.transform.position += Vector3.up * (world.y - b.center.y);
                }
                else SitOn(go, baseY);
                if (mount == Mount.Hang)
                {
                    var glow = PointLight(go.transform, Vector3.zero, 3f);
                    glow.transform.position = Bounds(go).center;
                    lamps.Add(glow);
                }
                go.SetActive(false);
                ids.Add(id); pieces.Add(go);
            }
            root.gameObject.AddComponent<CabinDecor>().EditorSetup(ids.ToArray(), pieces.ToArray());
        }
    }
}
