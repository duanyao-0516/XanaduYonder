using System.IO;
using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    public sealed class CutTheRopeAndroidBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1001;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            foreach (var name in new[] { "CutTheRopeRuntime.aar", "CutTheRopeContent.aar" })
            {
                var file = Path.Combine("Assets/Plugins/Android", name);
                if (!File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new BuildFailedException("Missing " + name + ". Generate the Cut the Rope offline content and run scripts/cut-the-rope/build-runtime.py first.");
            }
            var root = Path.GetFullPath(".");
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "\"" + Path.Combine(root, "scripts/verify-cut-the-rope-input.py") + "\" --root \"" + root + "\"",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not start Cut the Rope content verification.");
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Cut the Rope content validation failed: " + errors);
            }
        }
    }
}
