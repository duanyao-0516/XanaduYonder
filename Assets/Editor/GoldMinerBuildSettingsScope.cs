using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace XanaduYonder.EditorTools
{
    // Owns only the settings temporarily changed by the Gold Miner build entry point.
    // Capture Unity's loaded objects, not their files: restoring files alone leaves the
    // Editor's cached settings changed and a later SaveAssets can overwrite the files again.
    public sealed class GoldMinerBuildSettingsScope : IDisposable
    {
        private readonly SettingsAsset[] _assets =
        {
            new SettingsAsset("ProjectSettings/ProjectSettings.asset"),
            new SettingsAsset("ProjectSettings/QualitySettings.asset"),
            new SettingsAsset("ProjectSettings/EditorSettings.asset")
        };
        private readonly BuildFlags _flags = new BuildFlags();
        private readonly EditorBuildSettingsScene[] _scenes = EditorBuildSettings.scenes;
        private readonly int _qualityLevel = QualitySettings.GetQualityLevel();
        private readonly int _vSyncCount = QualitySettings.vSyncCount;
        private readonly int _antiAliasing = QualitySettings.antiAliasing;
        private readonly ShadowQuality _shadows = QualitySettings.shadows;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            var errors = new List<Exception>();
            // A failure restoring one object must not prevent the other settings restoring.
            foreach (var asset in _assets) Attempt(asset.Restore, errors);
            Attempt(() =>
            {
                if (QualitySettings.GetQualityLevel() != _qualityLevel)
                    QualitySettings.SetQualityLevel(_qualityLevel, false);
                // Refresh the active quality values as well as the serialized quality levels.
                QualitySettings.vSyncCount = _vSyncCount;
                QualitySettings.antiAliasing = _antiAliasing;
                QualitySettings.shadows = _shadows;
            }, errors);
            Attempt(_flags.Restore, errors);
            Attempt(() => EditorBuildSettings.scenes = _scenes, errors);
            Attempt(AssetDatabase.SaveAssets, errors);
            Attempt(AssertRestored, errors);
            if (errors.Count != 0)
                throw new AggregateException("Gold Miner build settings could not be fully restored.", errors);
            _disposed = true;
        }

        private static void Attempt(Action action, List<Exception> errors)
        {
            try { action(); }
            catch (Exception exception) { errors.Add(exception); }
        }

        private void AssertRestored()
        {
            foreach (var asset in _assets) asset.AssertRestored();
            Require(_flags.EqualsCurrent(), "EditorUserBuildSettings flags were not restored.");
            var scenes = EditorBuildSettings.scenes;
            Require(scenes.Length == _scenes.Length, "Build scene count was not restored.");
            for (var i = 0; i < scenes.Length; i++)
                Require(scenes[i].path == _scenes[i].path && scenes[i].guid == _scenes[i].guid &&
                    scenes[i].enabled == _scenes[i].enabled, "Build scene was not restored at index " + i + ".");
            Require(QualitySettings.GetQualityLevel() == _qualityLevel && QualitySettings.vSyncCount == _vSyncCount &&
                QualitySettings.antiAliasing == _antiAliasing && QualitySettings.shadows == _shadows,
                "Active quality settings were not restored.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class SettingsAsset
        {
            private readonly string _path;
            private readonly Object _target;
            private readonly string _json;

            public SettingsAsset(string path)
            {
                _path = path;
                var objects = AssetDatabase.LoadAllAssetsAtPath(path);
                Require(objects.Length == 1 && objects[0] != null, "Cannot snapshot settings: " + path);
                _target = objects[0];
                // Includes platform maps, activeInputHandler, icons and splash settings changed by Prepare.
                _json = EditorJsonUtility.ToJson(_target);
                Require(!string.IsNullOrEmpty(_json) && _json != "{}", "Empty settings snapshot: " + path);
            }

            public bool EqualsCurrent() { return _json == EditorJsonUtility.ToJson(_target); }

            public void Restore()
            {
                EditorJsonUtility.FromJsonOverwrite(_json, _target);
                EditorUtility.SetDirty(_target);
            }

            public void AssertRestored()
            {
                // Do not log serialized settings: signing or service credentials may be present.
                Require(EqualsCurrent(), "Serialized settings were not restored: " + _path);
            }
        }

        private sealed class BuildFlags
        {
            private readonly bool _appBundle = EditorUserBuildSettings.buildAppBundle;
            private readonly bool _export = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            private readonly bool _development = EditorUserBuildSettings.development;
            private readonly bool _debugging = EditorUserBuildSettings.allowDebugging;
            private readonly bool _profiler = EditorUserBuildSettings.connectProfiler;

            public void Restore()
            {
                EditorUserBuildSettings.buildAppBundle = _appBundle;
                EditorUserBuildSettings.exportAsGoogleAndroidProject = _export;
                EditorUserBuildSettings.development = _development;
                EditorUserBuildSettings.allowDebugging = _debugging;
                EditorUserBuildSettings.connectProfiler = _profiler;
            }

            public bool EqualsCurrent()
            {
                return EditorUserBuildSettings.buildAppBundle == _appBundle &&
                    EditorUserBuildSettings.exportAsGoogleAndroidProject == _export &&
                    EditorUserBuildSettings.development == _development &&
                    EditorUserBuildSettings.allowDebugging == _debugging &&
                    EditorUserBuildSettings.connectProfiler == _profiler;
            }
        }

        // Runs without Prepare, importing assets or BuildPlayer. The outer scope protects the
        // original project settings even if a mutation or assertion in either test path fails.
        public static void RunValidation()
        {
            try
            {
                using (var original = new GoldMinerBuildSettingsScope())
                {
                    ValidateExit(original, false);
                    ValidateExit(original, true);
                }
                Debug.Log("Gold Miner build settings scope: normal and failed exits restored all settings.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }

        private static void ValidateExit(GoldMinerBuildSettingsScope original, bool simulateFailure)
        {
            var failure = new InvalidOperationException("Expected simulated Gold Miner build failure.");
            var caughtFailure = false;
            try
            {
                using (new GoldMinerBuildSettingsScope())
                {
                    ChangeRepresentativeSettings();
                    foreach (var asset in original._assets)
                        Require(!asset.EqualsCurrent(), "Validation did not change a settings object.");
                    Require(!original._flags.EqualsCurrent(), "Validation did not change build flags.");
                    if (simulateFailure) throw failure;
                }
            }
            catch (InvalidOperationException exception) when (ReferenceEquals(exception, failure))
            {
                caughtFailure = true;
            }
            Require(caughtFailure == simulateFailure, "Validation did not exercise the requested exit path.");
            original.AssertRestored();
        }

        private static void ChangeRepresentativeSettings()
        {
            PlayerSettings.companyName += " GoldMinerScopeCheck";
            PlayerSettings.productName += " GoldMinerScopeCheck";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.duanyao.goldminerscopecheck");
            PlayerSettings.Android.useCustomKeystore = !PlayerSettings.Android.useCustomKeystore;
            PlayerSettings.defaultInterfaceOrientation = PlayerSettings.defaultInterfaceOrientation == UIOrientation.Portrait
                ? UIOrientation.LandscapeLeft : UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeRight = !PlayerSettings.allowedAutorotateToLandscapeRight;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,
                !PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android));
            var graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[]
            {
                graphics.Length == 1 && graphics[0] == GraphicsDeviceType.OpenGLES3
                    ? GraphicsDeviceType.Vulkan : GraphicsDeviceType.OpenGLES3
            });
            PlayerSettings.SplashScreen.backgroundColor = PlayerSettings.SplashScreen.backgroundColor == Color.white
                ? Color.black : Color.white;
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            using (var serialized = new SerializedObject(settings[0]))
            {
                var input = serialized.FindProperty("activeInputHandler");
                Require(input != null, "Pinned Unity Editor lacks activeInputHandler.");
                input.intValue = input.intValue == 0 ? 2 : 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSettings.serializationMode = EditorSettings.serializationMode == SerializationMode.ForceText
                ? SerializationMode.Mixed : SerializationMode.ForceText;
            QualitySettings.vSyncCount = QualitySettings.vSyncCount == 0 ? 1 : 0;
            QualitySettings.antiAliasing = QualitySettings.antiAliasing == 0 ? 2 : 0;
            QualitySettings.shadows = QualitySettings.shadows == ShadowQuality.Disable ? ShadowQuality.All : ShadowQuality.Disable;
            EditorUserBuildSettings.buildAppBundle = !EditorUserBuildSettings.buildAppBundle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = !EditorUserBuildSettings.exportAsGoogleAndroidProject;
            EditorUserBuildSettings.development = !EditorUserBuildSettings.development;
            EditorUserBuildSettings.allowDebugging = !EditorUserBuildSettings.allowDebugging;
            EditorUserBuildSettings.connectProfiler = !EditorUserBuildSettings.connectProfiler;
            var scenes = EditorBuildSettings.scenes;
            EditorBuildSettings.scenes = scenes.Length == 0
                ? new[] { new EditorBuildSettingsScene(ProjectBuild.ScenePath, false) }
                : Array.Empty<EditorBuildSettingsScene>();
            AssetDatabase.SaveAssets();
        }
    }
}
