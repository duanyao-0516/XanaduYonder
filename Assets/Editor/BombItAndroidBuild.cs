#if UNITY_ANDROID
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    public sealed class BombItAndroidBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder => 1120;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android)
                Run("scripts/bomb-it/build-content.py", "--verify");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android && report.summary.outputPath.EndsWith(".apk"))
                Run("scripts/bomb-it/verify-package.py", Quote(report.summary.outputPath));
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static void Run(string script, string args)
        {
            var start = new ProcessStartInfo("python3", Quote(Path.GetFullPath(script)) + " " + args)
            {
                WorkingDirectory = Path.GetFullPath("."),
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not verify offline Bomb It content.");
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Bomb It content validation failed: " + errors);
            }
        }
    }
}
#endif
