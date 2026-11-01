using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    public sealed class FruitSliceAndroidBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android) return;
            var root = Path.GetFullPath(".");
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "\"" + Path.Combine(root, "scripts/fruit-slice/verify-content.py") + "\"",
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not validate Fruit Slice content.");
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Fruit Slice content validation failed: " + error);
            }
            if (!File.Exists("Assets/Resources/Lobby/Icons/fruit-slice.png") ||
                !File.Exists("Assets/Resources/Lobby/Icons/fruit-slice-logo.jpg"))
                throw new BuildFailedException("Missing Fruit Slice lobby artwork. Run scripts/build-fruit-slice.sh first.");
        }
    }
}
