using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CozyFishing.EditorTools
{
    /// <summary>
    /// Imports the Meshy characters (ArtSource/Meshy/&lt;Name&gt;) as Humanoids and builds an Animator Controller per character.
    /// Menu: CozyFishing/Import Characters.
    /// </summary>
    public static class CharacterImporter
    {
        public static readonly string[] Characters = { "FisherM", "FisherF" };
        const string Root = "Assets/Art/Characters";

        static readonly HashSet<string> Looping = new() { "Idle", "Walk", "Run", "SitIdle", "Row", "SitReel", "StandReel", "StandWait" };

        // Parameters used by PlayerAvatar.
        public const string Speed = "Speed", Seated = "Seated", Rowing = "Rowing", RowSpeed = "RowSpeed",
            Fishing = "Fishing", ReelSpeed = "ReelSpeed", CastTime = "CastTime", Cast = "Cast", OpenDoor = "OpenDoor", Cheer = "Cheer", Wave = "Wave",
            Celebrate = "Celebrate";

        [MenuItem("CozyFishing/Import Characters")]
        public static void ImportAll()
        {
            foreach (var c in Characters) Import(c);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterImporter] done");
        }

        public static string ModelPath(string name) => $"{Root}/{name}/{name}.fbx";
        public static string ControllerPath(string name) => $"{Root}/{name}/{name}.controller";

        static void Import(string name)
        {
            string src = $"ArtSource/Meshy/{name}";
            string dst = $"{Root}/{name}";
            Directory.CreateDirectory(dst);
            Directory.CreateDirectory($"{dst}/Animations");
            File.Copy($"{src}/{name}_Rigged.fbx", ModelPath(name), true);
            foreach (var f in Directory.GetFiles($"{src}/Animations", "*.fbx"))
                File.Copy(f, $"{dst}/Animations/{Path.GetFileName(f)}", true);
            AssetDatabase.Refresh();

            // Model: humanoid avatar, embedded textures extracted next to it.
            var model = (ModelImporter)AssetImporter.GetAtPath(ModelPath(name));
            model.animationType = ModelImporterAnimationType.Human;
            model.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            model.importAnimation = false;
            model.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            model.materialLocation = ModelImporterMaterialLocation.InPrefab;
            model.SaveAndReimport();
            model.ExtractTextures(dst);
            AssetDatabase.Refresh();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath(name)).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman) Debug.LogError($"[CharacterImporter] {name}: humanoid avatar failed");

            // Animations: humanoid, reuse the model's avatar, in-place (root baked).
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var path in Directory.GetFiles($"{dst}/Animations", "*.fbx").Select(p => p.Replace('\\', '/')))
            {
                string clipName = Path.GetFileNameWithoutExtension(path).Split('@')[1];
                var imp = (ModelImporter)AssetImporter.GetAtPath(path);
                imp.animationType = ModelImporterAnimationType.Human;
                // Each clip gets its own avatar: text-to-motion clips use a different unit scale, and humanoid
                // retargeting maps them onto the character regardless.
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.importAnimation = true;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                var defaults = imp.defaultClipAnimations;
                if (defaults.Length == 0) { Debug.LogWarning($"no clip in {path}"); continue; }
                var ca = defaults[0];
                ca.name = clipName;
                ca.loopTime = Looping.Contains(clipName);
                ca.loopPose = ca.loopTime;
                ca.lockRootRotation = true;
                ca.lockRootHeightY = true;
                ca.lockRootPositionXZ = true;
                ca.keepOriginalOrientation = true;
                ca.keepOriginalPositionY = true;
                ca.keepOriginalPositionXZ = true;
                imp.clipAnimations = new[] { ca };
                imp.SaveAndReimport();
                clips[clipName] = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            }
            BuildController(name, clips);
        }

        static void BuildController(string name, Dictionary<string, AnimationClip> clips)
        {
            string path = ControllerPath(name);
            AssetDatabase.DeleteAsset(path);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter(Speed, AnimatorControllerParameterType.Float);
            ac.AddParameter(Seated, AnimatorControllerParameterType.Bool);
            ac.AddParameter(Rowing, AnimatorControllerParameterType.Bool);
            ac.AddParameter(RowSpeed, AnimatorControllerParameterType.Float);
            ac.AddParameter(Fishing, AnimatorControllerParameterType.Bool);
            ac.AddParameter(ReelSpeed, AnimatorControllerParameterType.Float);
            ac.AddParameter(CastTime, AnimatorControllerParameterType.Float);
            foreach (var t in new[] { Cast, OpenDoor, Cheer, Wave }) ac.AddParameter(t, AnimatorControllerParameterType.Trigger);

            var sm = ac.layers[0].stateMachine;
            var loco = ac.CreateBlendTreeInController("Locomotion", out var tree, 0);
            tree.blendParameter = Speed;
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips["Idle"], 0f);
            tree.AddChild(clips["Walk"], 1f);
            tree.AddChild(clips["Run"], 2f);
            sm.defaultState = loco;

            var sit = State(sm, "Seated", clips["SitIdle"]);
            // Legs keep the seated pose; the rowing stroke plays on an upper-body layer (see below).
            var row = State(sm, "Rowing", clips["SitIdle"]);
            // The cast clip is scrubbed by gameplay: charge pulls the arm back, release plays the throw.
            var cast = State(sm, "Cast", clips["SitCast"]);
            cast.timeParameterActive = true;
            cast.timeParameter = CastTime;
            var hold = State(sm, "FishHold", clips["SitReel"]);
            hold.speedParameterActive = true;
            hold.speedParameter = ReelSpeed;
            var door = State(sm, "OpenDoor", clips["OpenDoor"]);
            var cheer = State(sm, "Cheer", clips["Cheer"]);
            var wave = State(sm, "Wave", clips["Wave"]);

            Link(loco, sit, 0.25f, (Seated, true));
            Link(sit, loco, 0.25f, (Seated, false));
            Link(sit, row, 0.2f, (Rowing, true));
            Link(row, sit, 0.3f, (Rowing, false));
            Link(row, loco, 0.25f, (Seated, false));
            Trigger(sit, cast, Cast, 0.15f);
            Trigger(row, cast, Cast, 0.15f);
            var toHold = cast.AddTransition(hold);
            toHold.hasExitTime = false;
            toHold.duration = 0.2f;
            toHold.AddCondition(AnimatorConditionMode.Greater, 0.97f, CastTime);
            Link(cast, sit, 0.25f, (Fishing, false));
            Link(hold, sit, 0.25f, (Fishing, false));
            Trigger(loco, door, OpenDoor, 0.1f);
            Exit(door, loco, 0.85f, 0.2f);
            Trigger(loco, cheer, Cheer, 0.1f);
            Exit(cheer, loco, 0.9f, 0.25f);
            Trigger(loco, wave, Wave, 0.1f);
            Exit(wave, loco, 0.9f, 0.25f);

            // Standing fishing from the dock: the cast is scrubbed by CastTime, the reel clip runs at ReelSpeed.
            if (clips.TryGetValue("StandCast", out var standCastClip) && clips.TryGetValue("StandReel", out var standReelClip))
            {
                var standCast = State(sm, "StandCast", standCastClip);
                standCast.timeParameterActive = true;
                standCast.timeParameter = CastTime;
                var standHold = State(sm, "StandHold", standReelClip);
                standHold.speedParameterActive = true;
                standHold.speedParameter = ReelSpeed;
                var toCast = loco.AddTransition(standCast);
                toCast.hasExitTime = false;
                toCast.duration = 0.15f;
                toCast.AddCondition(AnimatorConditionMode.If, 0, Cast);
                toCast.AddCondition(AnimatorConditionMode.IfNot, 0, Seated);
                var toStandHold = standCast.AddTransition(standHold);
                toStandHold.hasExitTime = false;
                toStandHold.duration = 0.2f;
                toStandHold.AddCondition(AnimatorConditionMode.Greater, 0.97f, CastTime);
                Link(standCast, loco, 0.25f, (Fishing, false));
                Link(standHold, loco, 0.25f, (Fishing, false));
            }
            // One-shot extras from the animation pack: a happy jump and two idle fidgets.
            foreach (var (clipName, trigger) in new[] { ("HappyJump", Celebrate), ("LookAround", "LookAround"), ("Idle3", "Idle3") })
            {
                if (!clips.TryGetValue(clipName, out var extraClip)) continue;
                ac.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var extra = State(sm, clipName, extraClip);
                Trigger(loco, extra, trigger, 0.15f);
                Exit(extra, loco, 0.92f, 0.25f);
            }

            // Upper body: rowing stroke over the seated legs. Weight is driven by PlayerAvatar.
            ac.AddLayer(new AnimatorControllerLayer
            {
                name = "UpperBody",
                defaultWeight = 0f,
                avatarMask = UpperBodyMask(),
                iKPass = true,
                stateMachine = new AnimatorStateMachine { name = "UpperBody", hideFlags = HideFlags.HideInHierarchy },
            });
            var layers = ac.layers;
            AssetDatabase.AddObjectToAsset(layers[1].stateMachine, ac);
            var upperRow = layers[1].stateMachine.AddState("RowUpper");
            upperRow.motion = clips["Row"];
            upperRow.speedParameterActive = true;
            upperRow.speedParameter = RowSpeed;
            layers[1].stateMachine.defaultState = upperRow;
            layers[0].iKPass = true;
            ac.layers = layers;
            EditorUtility.SetDirty(ac);
        }

        static AvatarMask UpperBodyMask()
        {
            const string path = Root + "/UpperBody.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }
            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                bool upper = part is AvatarMaskBodyPart.Body or AvatarMaskBodyPart.Head or AvatarMaskBodyPart.LeftArm or AvatarMaskBodyPart.RightArm
                    or AvatarMaskBodyPart.LeftFingers or AvatarMaskBodyPart.RightFingers or AvatarMaskBodyPart.LeftHandIK or AvatarMaskBodyPart.RightHandIK;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static AnimatorState State(AnimatorStateMachine sm, string name, AnimationClip clip)
        {
            var s = sm.AddState(name);
            s.motion = clip;
            return s;
        }

        static void Link(AnimatorState from, AnimatorState to, float duration, params (string p, bool v)[] conds)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            foreach (var (p, v) in conds) t.AddCondition(v ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, 0, p);
        }

        static void Trigger(AnimatorState from, AnimatorState to, string trigger, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = false;
            t.duration = duration;
            t.AddCondition(AnimatorConditionMode.If, 0, trigger);
        }

        static void Exit(AnimatorState from, AnimatorState to, float exitTime, float duration)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = duration;
        }
    }
}
