#if UNITY_ANDROID
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    public sealed class LevelDevilAndroidBuild : IPreprocessBuildWithReport,
        IPostGenerateGradleAndroidProject, IPostprocessBuildWithReport
    {
        public int callbackOrder => 1100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            // Generate before entering Unity so the AAR is already imported. A build
            // must never silently accept stale content from a previous patch version.
            RunPython("scripts/level-devil/build-content.py", "--verify");
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var library = Path.Combine(path, "build.gradle");
            if (!File.Exists(library) || !File.ReadAllText(library).Contains("com.android.library"))
                throw new BuildFailedException("Generated Unity library Gradle module was not found.");
            ReplaceBlock(library, "LEVEL DEVIL DEPENDENCIES", @"
dependencies {
    implementation 'androidx.webkit:webkit:1.18.0'
}");

            var properties = Path.GetFullPath(Path.Combine(path, "../gradle.properties"));
            if (!File.Exists(properties))
                throw new BuildFailedException("Generated root gradle.properties was not found.");
            var lines = File.ReadAllLines(properties);
            for (var i = 0; i < lines.Length; i++)
                if (lines[i].TrimStart().StartsWith("android.useAndroidX=", StringComparison.Ordinal))
                    lines[i] = "";
            File.WriteAllText(properties, string.Join("\n", lines).TrimEnd() + "\nandroid.useAndroidX=true\n");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android ||
                (report.summary.options & BuildOptions.Development) != 0 ||
                !report.summary.outputPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) return;
            var sdk = AndroidExternalToolsSettings.sdkRootPath;
            if (string.IsNullOrEmpty(sdk))
                sdk = Path.Combine(BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None), "SDK");
            var aapt = Path.Combine(sdk, "build-tools/36.0.0/aapt");
            if (!File.Exists(aapt)) throw new BuildFailedException("Android build-tools 36.0.0 aapt is required for offline APK verification.");
            RunPython("scripts/verify-level-devil-apk.py", Quote(report.summary.outputPath) + " --aapt " + Quote(aapt));
        }

        private static void ReplaceBlock(string file, string label, string block)
        {
            var begin = "// BEGIN XANADU " + label;
            var end = "// END XANADU " + label;
            var text = File.ReadAllText(file);
            var start = text.IndexOf(begin, StringComparison.Ordinal);
            if (start >= 0)
            {
                var finish = text.IndexOf(end, start, StringComparison.Ordinal);
                if (finish < 0 || text.IndexOf(begin, start + begin.Length, StringComparison.Ordinal) >= 0)
                    throw new BuildFailedException("Malformed Level Devil Gradle dependency block.");
                text = text.Remove(start, finish + end.Length - start);
            }
            File.WriteAllText(file, text.TrimEnd() + "\n\n" + begin + "\n" + block.Trim() + "\n" + end + "\n");
        }

        private static string Quote(string argument)
        {
            return "\"" + argument.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void RunPython(string script, string arguments)
        {
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = Quote(Path.GetFullPath(script)) + " " + arguments,
                WorkingDirectory = Path.GetFullPath("."),
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not start Level Devil build verification.");
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new BuildFailedException("Level Devil build verification failed: " + error);
            }
        }
    }
}
#endif
