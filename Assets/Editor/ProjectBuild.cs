using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using XanaduYonder.Runtime;

namespace XanaduYonder.EditorTools
{
    public static class ProjectBuild
    {
        public const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        public const string PackageName = "com.duanyao.xanaduyonder";
        public const string Version = "0.3.0";

        [MenuItem("XanaduYonder/Prepare project")]
        public static void PrepareProject()
        {
            RunOrFail(Prepare);
        }

        [MenuItem("XanaduYonder/Build Android APK")]
        public static void BuildAndroid()
        {
            RunOrFail(() =>
            {
                Prepare();
                BuildApk(".apk", BuildOptions.Development | BuildOptions.StrictMode);
            });
        }

        // User preview stays side-by-side with the installed app, without the Development runtime.
        // BuildAndroid remains the explicit diagnostic build; no runtime errors are hidden here.
        public static void BuildZumaPreview()
        {
            RunOrFail(() =>
            {
                var identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
                var product = PlayerSettings.productName;
                var development = EditorUserBuildSettings.development;
                var allowDebugging = EditorUserBuildSettings.allowDebugging;
                var connectProfiler = EditorUserBuildSettings.connectProfiler;
                var buildAppBundle = EditorUserBuildSettings.buildAppBundle;
                var exportGradleProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
                var customKeystore = PlayerSettings.Android.useCustomKeystore;
                try
                {
                    Prepare();
                    EditorUserBuildSettings.development = false;
                    EditorUserBuildSettings.allowDebugging = false;
                    EditorUserBuildSettings.connectProfiler = false;
                    // A non-Development build can still use the local debug signing identity.
                    PlayerSettings.Android.useCustomKeystore = false;
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName + ".zumapreview");
                    PlayerSettings.productName = "Xanadu Zuma Preview";
                    BuildApk("-zuma-preview.apk", BuildOptions.StrictMode);
                }
                finally
                {
                    PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, identifier);
                    PlayerSettings.productName = product;
                    EditorUserBuildSettings.development = development;
                    EditorUserBuildSettings.allowDebugging = allowDebugging;
                    EditorUserBuildSettings.connectProfiler = connectProfiler;
                    EditorUserBuildSettings.buildAppBundle = buildAppBundle;
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = exportGradleProject;
                    PlayerSettings.Android.useCustomKeystore = customKeystore;
                    AssetDatabase.SaveAssets();
                }
            });
        }

        // Called by build-release.sh. This intermediate is not a distributable release:
        // the script replaces its debug signature with the private release identity.
        public static void BuildAndroidRelease()
        {
            RunOrFail(() =>
            {
                var development = EditorUserBuildSettings.development;
                var allowDebugging = EditorUserBuildSettings.allowDebugging;
                var connectProfiler = EditorUserBuildSettings.connectProfiler;
                var customKeystore = PlayerSettings.Android.useCustomKeystore;
                try
                {
                    Prepare();
                    EditorUserBuildSettings.development = false;
                    EditorUserBuildSettings.allowDebugging = false;
                    EditorUserBuildSettings.connectProfiler = false;
                    // Private signing credentials never enter Unity or ProjectSettings.
                    PlayerSettings.Android.useCustomKeystore = false;
                    BuildApk("-release-intermediate.apk", BuildOptions.StrictMode);
                }
                finally
                {
                    EditorUserBuildSettings.development = development;
                    EditorUserBuildSettings.allowDebugging = allowDebugging;
                    EditorUserBuildSettings.connectProfiler = connectProfiler;
                    PlayerSettings.Android.useCustomKeystore = customKeystore;
                    AssetDatabase.SaveAssets();
                }
            });
        }

        private static void BuildApk(string suffix, BuildOptions options)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Install this Editor's Android Build Support, SDK/NDK and OpenJDK modules.");
            var output = Path.GetFullPath("artifacts/builds/XanaduYonder-" + Version + suffix);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android build {report.summary.result}: {report.summary.totalErrors} errors.");
            if (!File.Exists(output))
                throw new IOException("Unity reported success but the APK does not exist: " + output);
            Debug.Log($"Android APK: {output} ({new FileInfo(output).Length} bytes)");
        }

        public static void ValidatePersistence()
        {
            PersistenceChecks.Run();
        }

        [MenuItem("XanaduYonder/Configure VS Code")]
        public static void ConfigureCodeEditor()
        {
            RunOrFail(() =>
            {
                var editor = Environment.GetEnvironmentVariable("UNITY_SCRIPT_EDITOR");
                if (string.IsNullOrEmpty(editor)) editor = "/Applications/Visual Studio Code.app";
                if (!Directory.Exists(editor) && !File.Exists(editor))
                    throw new FileNotFoundException("Set UNITY_SCRIPT_EDITOR to the installed code editor.", editor);
                Unity.CodeEditor.CodeEditor.SetExternalScriptEditor(editor);
                Unity.CodeEditor.CodeEditor.CurrentEditor.SyncAll();
                Debug.Log("Configured external C# editor: " + editor);
            });
        }

        private static void Prepare()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            PlayerSettings.companyName = "DuanYao";
            PlayerSettings.productName = "XanaduYonder";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Minimal);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 480;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            // Startup remains portrait; the Zuma page requests fixed landscape only while active.
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.bundleVersionCode = 3;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel36;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
            PlayerSettings.Android.splitApplicationBinary = false;
            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.Android.startInFullscreen = true;
            PlayerSettings.Android.renderOutsideSafeArea = true;
            PlayerSettings.Android.optimizedFramePacing = true;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            AppBranding.Configure();
            SetLegacyInput();
            EnsureBootstrapScene();
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("Prepared portrait Android ARM64 IL2CPP project: " + PackageName + " " + Version);
        }

        private static void SetLegacyInput()
        {
            // Unity exposes this setting through serialized PlayerSettings rather than a public setter.
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0)
                throw new InvalidOperationException("Unity PlayerSettings asset was not created.");
            using (var settings = new SerializedObject(assets[0]))
            {
                var input = settings.FindProperty("activeInputHandler");
                if (input == null)
                    throw new InvalidOperationException("Pinned Unity Editor lacks the activeInputHandler setting.");
                input.intValue = 0; // Input Manager (Old).
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void EnsureBootstrapScene()
        {
            if (File.Exists(ScenePath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                foreach (var root in existing.GetRootGameObjects())
                {
                    var app = root.GetComponentInChildren<AppController>(true);
                    if (app != null)
                    {
                        ConfigureLaunchArtwork(app);
                        if (!EditorSceneManager.SaveScene(existing, ScenePath))
                            throw new IOException("Failed to save bootstrap launch artwork: " + ScenePath);
                        return;
                    }
                }
                throw new InvalidOperationException(ScenePath + " is missing AppController. Restore the bootstrap scene before building.");
            }

            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraObject = new GameObject("Background Camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(247, 244, 236, 255);
            camera.orthographic = true;
            camera.cullingMask = 0;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            cameraObject.transform.position = new Vector3(0, 0, -10);
            var controller = new GameObject("XanaduYonder App", typeof(AppController)).GetComponent<AppController>();
            ConfigureLaunchArtwork(controller);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save bootstrap scene: " + ScenePath);
        }

        private static void ConfigureLaunchArtwork(AppController app)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Branding/XY_logo.png");
            if (texture == null || texture.width != texture.height)
                throw new InvalidOperationException("The complete square launch artwork is missing.");
            var splash = app.GetComponent<BrandSplash>();
            if (splash == null) splash = app.gameObject.AddComponent<BrandSplash>();
            splash.Logo = texture;
            EditorUtility.SetDirty(splash);
        }

        private static void RunOrFail(Action action)
        {
            try { action(); }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                    EditorApplication.Exit(1);
                else
                    throw;
            }
        }
    }
}
