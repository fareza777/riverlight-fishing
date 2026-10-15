using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CozyFishing.EditorTools
{
    /// <summary>Imports Mira (Meshy rig + Idle/Wave/Talk clips) as a generic rig with a small animator controller.</summary>
    public static class MiraImporter
    {
        const string Src = "ArtSource/Meshy/NPC_Mira";
        const string Dst = "Assets/Art/Characters/NPC_Mira";
        public const string ModelPath = Dst + "/NPC_Mira.fbx";
        public const string ControllerPath = Dst + "/NPC_Mira.controller";

        public static bool Import()
        {
            if (!File.Exists($"{Src}/NPC_Mira_Rigged.fbx")) { Debug.LogWarning("[Mira] rig missing"); return false; }
            Directory.CreateDirectory($"{Dst}/Animations");
            if (!File.Exists(ModelPath))
            {
                File.Copy($"{Src}/NPC_Mira_Rigged.fbx", ModelPath, true);
                foreach (var f in Directory.GetFiles($"{Src}/Animations", "*.fbx"))
                    File.Copy(f, $"{Dst}/Animations/{Path.GetFileName(f)}", true);
                AssetDatabase.Refresh();
            }
            var model = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            model.animationType = ModelImporterAnimationType.Generic;
            model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.importAnimation = false;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.SaveAndReimport();
            model.ExtractTextures(Dst);
            AssetDatabase.Refresh();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();

            AnimationClip Clip(string name, bool loop)
            {
                string path = $"{Dst}/Animations/NPC_Mira@{name}.fbx";
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                if (imp == null) return null;
                imp.animationType = ModelImporterAnimationType.Generic;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                var ca = imp.defaultClipAnimations[0];
                ca.name = name;
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
            var wave = Clip("Wave", false);
            var talk = Clip("Talk", true);
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Talking", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sWave = sm.AddState("Wave"); sWave.motion = wave;
            var sTalk = sm.AddState("Talk"); sTalk.motion = talk;
            sm.defaultState = sIdle;
            var t = sIdle.AddTransition(sWave); t.hasExitTime = false; t.duration = 0.2f; t.AddCondition(AnimatorConditionMode.If, 0, "Wave");
            t = sWave.AddTransition(sIdle); t.hasExitTime = true; t.exitTime = 0.9f; t.duration = 0.25f;
            t = sIdle.AddTransition(sTalk); t.hasExitTime = false; t.duration = 0.25f; t.AddCondition(AnimatorConditionMode.If, 0, "Talking");
            t = sWave.AddTransition(sTalk); t.hasExitTime = false; t.duration = 0.25f; t.AddCondition(AnimatorConditionMode.If, 0, "Talking");
            t = sTalk.AddTransition(sIdle); t.hasExitTime = false; t.duration = 0.3f; t.AddCondition(AnimatorConditionMode.IfNot, 0, "Talking");
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
