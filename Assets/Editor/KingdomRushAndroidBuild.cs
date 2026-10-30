using System;
using System.IO;
using System.Diagnostics;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    // The application module owns native extraction; a library AAR cannot select it.
    public sealed class KingdomRushAndroidBuild : IPreprocessBuildWithReport, IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != UnityEditor.BuildTarget.Android) return;
            foreach (var name in new[] { "KingdomRushRuntime.aar", "KingdomRushContent.aar" })
            {
                var file = Path.Combine("Assets/Plugins/Android", name);
                if (!File.Exists(file) || new FileInfo(file).Length == 0)
                    throw new BuildFailedException("Missing " + name + ". Generate the KR1 content and run scripts/kingdom-rush/build-runtime.py first.");
            }
            var root = Path.GetFullPath(".");
            var gate = Path.Combine(root, "scripts/kingdom-rush/verify-build-input.py");
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
                if (process == null) throw new BuildFailedException("Could not start the Kingdom Rush content validation gate.");
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Kingdom Rush content validation failed: " + errors);
            }
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string launcher = null;
            foreach (var candidate in new[]
            {
                Path.GetFullPath(Path.Combine(path, "../launcher/build.gradle")),
                Path.GetFullPath(Path.Combine(path, "launcher/build.gradle"))
            })
            {
                if (File.Exists(candidate) && File.ReadAllText(candidate).Contains("com.android.application"))
                {
                    if (launcher != null) throw new BuildFailedException("Ambiguous generated Android application module.");
                    launcher = candidate;
                }
            }
            if (launcher == null) throw new BuildFailedException("Generated Android launcher/build.gradle was not found.");
            const string begin = "// BEGIN XANADU KR1 PACKAGING";
            const string end = "// END XANADU KR1 PACKAGING";
            var source = File.ReadAllText(launcher);
            var start = source.IndexOf(begin, StringComparison.Ordinal);
            if (start >= 0)
            {
                var finish = source.IndexOf(end, start, StringComparison.Ordinal);
                if (finish < 0) throw new BuildFailedException("Incomplete KR1 packaging block in generated Gradle source.");
                source = source.Remove(start, finish + end.Length - start);
            }
            File.WriteAllText(launcher, source.TrimEnd() + "\n\n" + begin + @"
android {
    packaging {
        jniLibs {
            useLegacyPackaging = true
        }
    }
    androidResources {
        noCompress += ['love']
    }
}
" + end + "\n");
        }
    }
}
