using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Imports a Meshy NPC (rig + library clips in ArtSource/Meshy/&lt;name&gt;) as a generic rig with a small controller:
    /// Idle / Walk ("Walking" bool) / Sit (default for sitters) / Talk ("Talking" bool) / Wave (trigger).
    /// </summary>
    public static class NpcImporter
    {
        public static string ModelPath(string name) => $"Assets/Art/Characters/{name}/{name}.fbx";
        public static string ControllerPath(string name) => $"Assets/Art/Characters/{name}/{name}.controller";

        public static bool Import(string name)
        {
            string src = $"ArtSource/Meshy/{name}";
            string dst = $"Assets/Art/Characters/{name}";
            if (!File.Exists($"{src}/{name}_Rigged.fbx")) { Debug.LogWarning($"[NPC] {name}: rig missing"); return false; }
            Directory.CreateDirectory($"{dst}/Animations");
            if (!File.Exists(ModelPath(name))) File.Copy($"{src}/{name}_Rigged.fbx", ModelPath(name), true);
            // New clips from later animation packs are picked up on every import.
            foreach (var f in Directory.GetFiles($"{src}/Animations", "*.fbx"))
            {
                string to = $"{dst}/Animations/{Path.GetFileName(f)}";
                if (!File.Exists(to)) File.Copy(f, to, true);
            }
            AssetDatabase.Refresh();
            var model = (ModelImporter)AssetImporter.GetAtPath(ModelPath(name));
            model.animationType = ModelImporterAnimationType.Generic;
            model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.importAnimation = false;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.SaveAndReimport();
            model.ExtractTextures(dst);
            AssetDatabase.Refresh();
            foreach (var tex in Directory.GetFiles(dst, "*.jpg").Concat(Directory.GetFiles(dst, "*.png")))
                if (AssetImporter.GetAtPath(tex.Replace('\\', '/')) is TextureImporter ti && ti.maxTextureSize > 1024)
                {
                    ti.maxTextureSize = 1024;   // NPCs are small on a phone screen
                    ti.SaveAndReimport();
                }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath(name)).OfType<Avatar>().FirstOrDefault();

            AnimationClip Clip(string clip, bool loop)
            {
                string path = $"{dst}/Animations/{name}@{clip}.fbx";
                if (AssetImporter.GetAtPath(path) is not ModelImporter imp) return null;
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                var ca = imp.defaultClipAnimations[0];
                ca.name = clip;
                ca.loopTime = loop;
                ca.loopPose = loop;
                ca.lockRootPositionXZ = true;
                ca.lockRootHeightY = true;
                ca.lockRootRotation = true;
                imp.clipAnimations = new[] { ca };
                imp.SaveAndReimport();
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            }

            var idle = Clip("Idle", true);
            // Leo runs everywhere.
            var walk = name == "NPC_Dimas" && File.Exists($"{dst}/Animations/{name}@Run.fbx") ? Clip("Run", true) : Clip("Walk", true);
            var sit = Clip("Sit", true);
            var wave = Clip("Wave", false);
            var talk = Clip("Talk", true);

            AssetDatabase.DeleteAsset(ControllerPath(name));
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath(name));
            ac.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Walking", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;
            // Sitters stay seated: their "rest" state is Sit and they wave/talk from the stool (upper body only reads fine).
            var rest = sm.AddState(sit != null ? "Sit" : "Idle");
            rest.motion = sit != null ? sit : idle;
            sm.defaultState = rest;
            if (sit == null && walk != null)
            {
                var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
                Link(rest, sWalk, 0.2f, ("Walking", true));
                Link(sWalk, rest, 0.25f, ("Walking", false));
            }
            if (wave != null && sit == null)
            {
                var sWave = sm.AddState("Wave"); sWave.motion = wave;
                var t = sm.AddAnyStateTransition(sWave); t.hasExitTime = false; t.duration = 0.2f; t.canTransitionToSelf = false;
                t.AddCondition(AnimatorConditionMode.If, 0, "Wave");
                var back = sWave.AddTransition(rest); back.hasExitTime = true; back.exitTime = 0.9f; back.duration = 0.25f;
            }
            if (talk != null && sit == null)
            {
                var sTalk = sm.AddState("Talk"); sTalk.motion = talk;
                Link(rest, sTalk, 0.25f, ("Talking", true));
                Link(sTalk, rest, 0.3f, ("Talking", false));
            }
            // Extra one-shot clips (animation pack): each is a trigger of the same name, played from the rest state.
            foreach (var path in Directory.GetFiles($"{dst}/Animations", $"{name}@*.fbx"))
            {
                string clipName = Path.GetFileNameWithoutExtension(path).Split('@')[1];
                if (clipName is "Idle" or "Walk" or "Run" or "Sit" or "Wave" or "Talk") continue;
                var clip = Clip(clipName, false);
                if (clip == null) continue;
                ac.AddParameter(clipName, AnimatorControllerParameterType.Trigger);
                var state = sm.AddState(clipName);
                state.motion = clip;
                var t = rest.AddTransition(state);
                t.hasExitTime = false; t.duration = 0.2f;
                t.AddCondition(AnimatorConditionMode.If, 0, clipName);
                var back = state.AddTransition(rest);
                back.hasExitTime = true; back.exitTime = 0.92f; back.duration = 0.3f;
            }
            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>The extra trigger names of an imported NPC (for NpcVillager).</summary>
        public static string[] Extras(string name)
        {
            var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath(name));
            return ac == null ? new string[0] : ac.parameters.Where(p => p.type == AnimatorControllerParameterType.Trigger && p.name != "Wave").Select(p => p.name).ToArray();
        }

        static void Link(AnimatorState from, AnimatorState to, float duration, (string param, bool value) cond)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.AddCondition(cond.value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, cond.param);
        }
    }
}
