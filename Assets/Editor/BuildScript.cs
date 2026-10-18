using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CozyFishing.EditorTools
{
    /// <summary>Command-line builds: unity build ... --execute-method CozyFishing.EditorTools.BuildScript.BuildWindows</summary>
    public static class BuildScript
    {
        static readonly string[] Scenes = { "Assets/Scenes/Title.unity", "Assets/Scenes/River.unity", "Assets/Scenes/Lake.unity", "Assets/Scenes/Gorge.unity", "Assets/Scenes/Estuary.unity" };

        /// <summary>Regenerates all content and scenes, then builds the Windows player.</summary>
        public static void RebuildSceneAndWindows()
        {
            LocationSceneBuilder.BuildEverything();
            BuildWindows();
        }

        [MenuItem("CozyFishing/Build Windows Player")]
        public static void BuildWindows()
        {
            PlayerSettings.productName = "Riverlight";
            PlayerSettings.companyName = "CozyFishing";
            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = "Builds/Windows/CozyFishing.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            });
            Debug.Log($"[BuildScript] {report.summary.result} size={report.summary.totalSize} errors={report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>
        /// Play Store bundle: portrait, IL2CPP ARM64, app icon. Signs with the owner's upload keystore when
        /// PlayerSettings.Android.keystoreName is set (passwords entered in the editor, never stored here);
        /// otherwise the bundle is debug-signed and only good for local testing.
        /// </summary>
        [MenuItem("CozyFishing/Build Android App Bundle")]
        public static void BuildAndroid() => BuildAndroid(true);

        /// <summary>Installable APK for testing on a phone (sideload).</summary>
        [MenuItem("CozyFishing/Build Android APK")]
        public static void BuildAndroidApk() => BuildAndroid(false);

        static void BuildAndroid(bool bundle)
        {
            PlayerSettings.productName = "Riverlight";
            PlayerSettings.companyName = "CozyFishing";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.cozyfishing.riverlight");
            ConfigureUploadSigning();
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.runInBackground = false;
            PlayerSettings.enableFrameTimingStats = true;   // CPU/GPU frame times for the on-device perf probe
            const string iconPath = "Assets/Art/UI/Icon/app_icon_full.png";
            if (AssetImporter.GetAtPath(iconPath) is TextureImporter iconImporter && iconImporter.textureCompression != TextureImporterCompression.Uncompressed)
            {
                iconImporter.textureCompression = TextureImporterCompression.Uncompressed;   // launcher icons stay crisp
                iconImporter.SaveAndReimport();
            }
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
            if (icon != null) PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Props" }))
            {
                // Props are small on a phone screen: 1K textures are plenty.
                if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is TextureImporter ti && ti.maxTextureSize > 1024)
                {
                    ti.maxTextureSize = 1024;
                    ti.SaveAndReimport();
                }
            }
            EditorUserBuildSettings.buildAppBundle = bundle;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = bundle ? "Builds/Android/Riverlight.aab" : "Builds/Android/Riverlight.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            Debug.Log($"[BuildScript] Android {report.summary.result} size={report.summary.totalSize} errors={report.summary.totalErrors}");
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Reads local-only signing values from the build environment; secrets never enter the project.</summary>
        static void ConfigureUploadSigning()
        {
            string keystore = Environment.GetEnvironmentVariable("RIVERLIGHT_KEYSTORE");
            string password = Environment.GetEnvironmentVariable("RIVERLIGHT_KEYSTORE_PASSWORD");
            string alias = Environment.GetEnvironmentVariable("RIVERLIGHT_KEY_ALIAS");
            if (string.IsNullOrWhiteSpace(keystore) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(alias))
                return;
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = password;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = password;
        }
    }
}
