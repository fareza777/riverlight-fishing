using System.Collections.Generic;
using CozyFishing.Boat;
using CozyFishing.Fishing;
using UnityEngine;
using UnityEngine.Rendering;
using static CozyFishing.EditorTools.SceneKit;
using Object = UnityEngine.Object;

namespace CozyFishing.EditorTools
{
    /// <summary>The player's rowboat with oars, stowed rod, bobber, line, creel and fishing controller, moored beside a dock.</summary>
    public static class BoatRig
    {
        public sealed class Result
        {
            public GameObject Root;
            public BoatController Controller;
            public RowingAnimator Rowing;
            public FishingController Fishing;
            public Transform Seat, RodRoot;
        }

        public static Result Build(Transform dock, List<Light> lanterns)
        {
            var boatRoot = new GameObject("PlayerBoat");
            // Moor alongside the outer half of the dock (dock local +Z runs out over the water, deck is 2 m wide).
            var moor = dock.TransformPoint(new Vector3(-2.2f, 0f, 4.4f));
            boatRoot.transform.SetPositionAndRotation(new Vector3(moor.x, 0f, moor.z), dock.rotation);
            var body = boatRoot.AddComponent<Rigidbody>();
            body.mass = 220f;
            var box = boatRoot.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.34f, 0f);
            box.size = new Vector3(1.35f, 0.5f, 3.1f);
            box.sharedMaterial = new PhysicsMaterial("BoatHull") { bounciness = 0.05f, dynamicFriction = 0.1f, staticFriction = 0.1f, frictionCombine = PhysicsMaterialCombine.Minimum };

            var visual = new GameObject("Visual").transform;
            visual.SetParent(boatRoot.transform, false);
            var boatModel = Spawn($"{Models}/Boat.fbx", visual);
            foreach (var c in boatModel.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var mask = Spawn($"{Models}/Boat_WaterMask.obj", visual);
            var maskMat = ShaderMaterial("WaterMask", "CozyFishing/WaterMask");
            foreach (var r in mask.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = maskMat;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            lanterns.Add(PointLight(boatModel.transform, new Vector3(0.22f, 0.6f, 0.75f), 4f));
            var seat = new GameObject("RowerSeat").transform;
            seat.SetParent(visual, false);
            seat.localPosition = new Vector3(0f, 0f, -0.12f);
            seat.localRotation = Quaternion.Euler(0f, 180f, 0f);

            var controller = boatRoot.AddComponent<BoatController>();
            Set(controller, "visual", visual);
            var rowing = boatRoot.AddComponent<RowingAnimator>();
            Set(rowing, "boat", controller);
            var oarL = Find(boatModel.transform, "Oar_L");
            var oarR = Find(boatModel.transform, "Oar_R");
            Set(rowing, "oarLeft", oarL);
            Set(rowing, "oarRight", oarR);
            // Bake handle/blade points now: mesh vertices are not readable in player builds.
            RowingAnimator.BakeOarPoints(oarL, boatRoot.transform);
            RowingAnimator.BakeOarPoints(oarR, boatRoot.transform);
            Set(controller, "rowing", rowing);

            var creel = boatRoot.AddComponent<Creel>();
            var (rodRoot, rodTip) = BuildRod(null);
            var stowed = BuildRod(visual).pivot;
            stowed.name = "StowedRod";
            // Lies on the rower's right-hand side, grip just in front of the seat (within reach), tip toward the bow.
            stowed.localPosition = new Vector3(-0.36f, 0.43f, -0.42f);
            stowed.localRotation = Quaternion.Euler(90f, 8f, 0f);
            var bobber = BuildBobber();
            var lineGo = new GameObject("FishingLine", typeof(LineRenderer));
            lineGo.transform.SetParent(boatRoot.transform, false);
            var lr = lineGo.GetComponent<LineRenderer>();
            var lineMat = ShaderMaterial("FishingLine", "Universal Render Pipeline/Unlit");
            lineMat.SetColor("_BaseColor", new Color(0.95f, 0.95f, 0.9f));
            lr.sharedMaterial = lineMat;
            lr.widthMultiplier = 0.012f;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.enabled = false;

            var fishing = boatRoot.AddComponent<FishingController>();
            Set(fishing, "creel", creel);
            Set(fishing, "rowing", rowing);
            Set(fishing, "rodPivot", rodRoot);
            Set(fishing, "rodTip", rodTip);
            Set(fishing, "stowedRod", stowed.gameObject);
            Set(fishing, "bobber", bobber);
            Set(fishing, "line", lr);
            return new Result { Root = boatRoot, Controller = controller, Rowing = rowing, Fishing = fishing, Seat = seat, RodRoot = rodRoot };
        }

        static (Transform pivot, Transform tip) BuildRod(Transform parent)
        {
            var pivot = new GameObject("Rod").transform;
            if (parent != null) pivot.SetParent(parent, false);
            // Grip sits at the origin (in the hand); the blank runs up the local Y axis.
            Part(pivot, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.04f, 0.16f, 0.04f), Palette("Pal_WoodLight"));
            Part(pivot, PrimitiveType.Cylinder, new Vector3(0f, 1.0f, 0f), new Vector3(0.018f, 0.85f, 0.018f), Palette("Pal_WoodDark"));
            Part(pivot, PrimitiveType.Sphere, new Vector3(0.045f, 0.16f, 0f), new Vector3(0.08f, 0.08f, 0.05f), Palette("Pal_Metal"));
            var tip = new GameObject("RodTip").transform;
            tip.SetParent(pivot, false);
            tip.localPosition = new Vector3(0f, 1.85f, 0f);
            return (pivot, tip);
        }

        static Transform BuildBobber()
        {
            var root = new GameObject("Bobber").transform;
            Part(root, PrimitiveType.Sphere, new Vector3(0f, 0.04f, 0f), new Vector3(0.14f, 0.1f, 0.14f), Palette("Pal_Red"));
            Part(root, PrimitiveType.Sphere, new Vector3(0f, -0.01f, 0f), new Vector3(0.13f, 0.09f, 0.13f), Palette("Pal_Cream"));
            Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.11f, 0f), new Vector3(0.015f, 0.05f, 0.015f), Palette("Pal_Red"));
            root.gameObject.SetActive(false);
            return root;
        }
    }
}
