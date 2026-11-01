using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    public sealed class ZhaoyunAdouAndroidBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1010;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != UnityEditor.BuildTarget.Android) return;
            var root = Path.GetFullPath(".");
            var gate = Path.Combine(root, "scripts/zhaoyun-adou/verify-build-input.py");
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "\"" + gate + "\" --root \"" + root + "\"",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not start the Zhaoyun Adou offline validation gate.");
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new BuildFailedException("Zhaoyun Adou offline content validation failed: " + errors +
                        " Build scripts/zhaoyun-adou/build-content.py and build-runtime.py first.");
            }
        }
    }
}
