using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace XanaduYonder.EditorTools
{
    internal static class RemovedGamesBuildGuard
    {
        internal static void VerifySource()
        {
            var start = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "\"" + Path.GetFullPath("scripts/verify-removed-games.py") + "\" --source-root \"" + Path.GetFullPath(".") + "\"",
                UseShellExecute = false,
                RedirectStandardError = true
            };
            using (var process = Process.Start(start))
            {
                if (process == null) throw new BuildFailedException("Could not start source payload verification.");
                var errors = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Source payload verification failed: " + errors);
            }
        }

        internal static void WritePackedAssets(BuildReport report, string path)
        {
            var assets = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var archive in report.packedAssets)
                foreach (var item in archive.contents)
                    if (!string.IsNullOrEmpty(item.sourceAssetPath)) assets.Add(item.sourceAssetPath);
            if (assets.Count == 0) throw new BuildFailedException("The detailed packed-asset inventory is empty.");
            File.WriteAllLines(path, assets);
        }
    }
}
