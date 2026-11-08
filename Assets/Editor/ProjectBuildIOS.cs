using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace XanaduYonder.EditorTools
{
    // Run through scripts/build.sh ios: only the filtered project copy is changed.
    public static class ProjectBuildIOS
    {
        public const string BundleIdentifier = "com.duanyao.xanaduyonder.iosnative";

        [Serializable]
        private sealed class StagingManifest
        {
            public string staging_root;
            public string[] excluded_paths;
        }

        public static void Export()
        {
            try
            {
#if UNITY_IOS
                ExportProject();
#else
                throw new BuildFailedException("Run the iOS export with -buildTarget iOS.");
#endif
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

#if UNITY_IOS
        private static void ExportProject()
        {
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var artifactDirectory = Path.GetDirectoryName(project);
            var manifestPath = Path.Combine(artifactDirectory, "staging-manifest.json");
            if (!File.Exists(manifestPath))
                throw new BuildFailedException("Missing filtered staging manifest. Run scripts/build.sh ios.");
            var manifest = JsonUtility.FromJson<StagingManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || string.IsNullOrEmpty(manifest.staging_root) ||
                Path.GetFullPath(manifest.staging_root) != project ||
                manifest.excluded_paths == null || manifest.excluded_paths.Length == 0)
                throw new BuildFailedException("The iOS staging manifest does not describe this project.");
            foreach (var path in manifest.excluded_paths)
                if (File.Exists(Path.Combine(project, path)) || Directory.Exists(Path.Combine(project, path)))
                    throw new BuildFailedException("Excluded content remains in iOS staging: " + path);

            var output = Environment.GetEnvironmentVariable("XANADU_IOS_XCODE_PATH");
            if (string.IsNullOrEmpty(output) || Path.GetFullPath(output) != Path.Combine(artifactDirectory, "xcode"))
                throw new BuildFailedException("XANADU_IOS_XCODE_PATH must point to the staging sibling xcode directory.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
                throw new BuildFailedException("Install iOS Build Support for the pinned Unity Editor.");

            Configure();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectBuild.ScenePath },
                locationPathName = output,
                target = BuildTarget.iOS,
                options = BuildOptions.StrictMode | BuildOptions.DetailedBuildReport
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("iOS export failed: " + report.summary.result +
                    " (" + report.summary.totalErrors + " errors).");

            var packed = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var archive in report.packedAssets)
                foreach (var item in archive.contents)
                    if (!string.IsNullOrEmpty(item.sourceAssetPath)) packed.Add(item.sourceAssetPath);
            File.WriteAllLines(Path.Combine(artifactDirectory, "packed-assets.txt"), packed);
            foreach (var path in packed)
                if (manifest.excluded_paths.Any(excluded => path == excluded ||
                    path.StartsWith(excluded.TrimEnd('/') + "/", StringComparison.Ordinal)))
                    throw new BuildFailedException("Excluded content was packed: " + path);
            foreach (var path in Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories))
                if (path.EndsWith(".aar", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                    path.Replace('\\', '/').Contains("/fruit-slice/"))
                    throw new BuildFailedException("Android-only payload was exported: " + path);
            if (!packed.Any(path => path.StartsWith("Assets/Resources/GoldMiner/", StringComparison.Ordinal)))
                throw new BuildFailedException("The native Unity GoldMiner resources are missing.");
            File.WriteAllText(Path.Combine(artifactDirectory, "export-summary.txt"),
                "Unity=" + Application.unityVersion + "\nBundleIdentifier=" + BundleIdentifier +
                "\nVersion=" + ProjectBuild.Version + "\nXcodeProject=" + output +
                "\nPackedAssetCount=" + packed.Count +
                "\nExcludedPayloadCheck=PASS\nNativeCompilation=NOT_RUN\nSigning=NOT_RUN\nDeviceInstall=NOT_RUN\n");
            Debug.Log("IOS_EXPORT_PASS: " + output + " (excluded payloads absent; native compilation and signing are separate steps)");
        }

        private static void Configure()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "DuanYao";
            PlayerSettings.productName = "XanaduYonder";
            PlayerSettings.bundleVersion = ProjectBuild.Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
            PlayerSettings.iOS.requiresFullScreen = true;
            PlayerSettings.iOS.buildNumber = ProjectBuild.VersionCode.ToString();
            PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            PlayerSettings.iOS.appleDeveloperTeamID = Environment.GetEnvironmentVariable("XANADU_IOS_TEAM_ID") ?? "";
            PlayerSettings.iOS.iOSManualProvisioningProfileID = "";
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            AppBranding.Configure();
            ProjectBuild.SetLegacyInput();
            ProjectBuild.EnsureBootstrapScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ProjectBuild.ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
