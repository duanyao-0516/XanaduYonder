using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    // Fail before Gradle if the generated plugin does not match tracked Java/H5 inputs.
    public sealed class ClassicGoldMinerAndroidBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1010;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != UnityEditor.BuildTarget.Android) return;
            var root = Path.GetFullPath(".");
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "\"" + Path.Combine(root, "scripts/classic-gold-miner/verify-content.py") + "\" --verify-aar",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not start classic Gold Miner input validation.");
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new BuildFailedException("Classic Gold Miner inputs changed or are missing. Run scripts/classic-gold-miner/build-runtime.py. " + errors);
            }
        }
    }
}
