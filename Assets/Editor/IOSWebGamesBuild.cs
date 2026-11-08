#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace XanaduYonder.EditorTools
{
    internal static class IOSWebGamesBuild
    {
        [PostProcessBuild(110)]
        private static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            project.AddFrameworkToProject(project.GetUnityFrameworkTargetGuid(), "WebKit.framework", false);
            project.WriteToFile(projectPath);

            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            // Unity's portrait startup otherwise exports only Portrait, even when
            // individual native/web games switch to landscape while running.
            foreach (var key in new[] { "UISupportedInterfaceOrientations", "UISupportedInterfaceOrientations~ipad" })
            {
                var orientations = root.CreateArray(key);
                orientations.AddString("UIInterfaceOrientationPortrait");
                orientations.AddString("UIInterfaceOrientationLandscapeLeft");
                orientations.AddString("UIInterfaceOrientationLandscapeRight");
            }
            var ats = root.values.TryGetValue("NSAppTransportSecurity", out var existing)
                ? existing.AsDict() : root.CreateDict("NSAppTransportSecurity");
            // Only local resources use HTTP. Do not relax transport rules for web content generally.
            ats.SetBoolean("NSAllowsLocalNetworking", true);
            var exceptions = ats.values.TryGetValue("NSExceptionDomains", out var domains)
                ? domains.AsDict() : ats.CreateDict("NSExceptionDomains");
            var loopback = exceptions.values.TryGetValue("127.0.0.1", out var local)
                ? local.AsDict() : exceptions.CreateDict("127.0.0.1");
            // iOS 17 and later support explicit IP exceptions. The listener never binds to a LAN address.
            loopback.SetBoolean("NSExceptionAllowsInsecureHTTPLoads", true);
            loopback.SetBoolean("NSIncludesSubdomains", false);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
