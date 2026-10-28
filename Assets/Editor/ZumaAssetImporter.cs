using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.EditorTools
{
    /// <summary>The editor entry point delegates source XML/DAT/MO3 conversion to
    /// the versioned offline importer. Builds validate every shipped resource.</summary>
    public sealed class ZumaAssetImporter : AssetPostprocessor, IPreprocessBuildWithReport
    {
        const string AssetRoot = "Assets/Resources/Zuma/";
        const string SourceCommit = "28001e0e5061341d502116586626150027242cce";
        static readonly string[] MapIds = { "spiral", "claw", "riverbed", "targetglyph", "turnaround", "longrange", "tiltspiral", "underover", "warshak", "loopy", "groovefest", "spaceinvaders", "triangle", "coaster", "squaresville", "tunnellevel", "overunder", "inversespiral" };
        public int callbackOrder { get { return -100; } }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AssetRoot,StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            // Padded JPEG thumbnails are RGB PNG files; extension is not alpha evidence.
            importer.alphaIsTransparency = importer.DoesSourceTextureHaveAlpha();
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            importer.maxTextureSize = Math.Max(width,height)>2048 ? 4096 : 2048;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = importer.maxTextureSize;
            android.format = importer.alphaIsTransparency ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC2_RGB4;
            android.textureCompression = TextureImporterCompression.CompressedHQ;
            android.compressionQuality = 100;
            android.crunchedCompression = false;
            importer.SetPlatformTextureSettings(android);
        }

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(AssetRoot,StringComparison.Ordinal)) return;
            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.loadType = assetPath.Contains("/Music/") ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = false;
        }

        [MenuItem("Xanadu/Zuma/Import fixed upstream content")]
        public static void ImportContent()
        {
            var source = EditorUtility.OpenFolderPanel("Zuma-Deluxe-HD at " + SourceCommit, "", "");
            if (!string.IsNullOrEmpty(source)) RunImport(source);
        }

        // Batch entry point: set ZUMA_UPSTREAM_SOURCE to the read-only upstream checkout.
        public static void ImportFromEnvironment()
        {
            var source = Environment.GetEnvironmentVariable("ZUMA_UPSTREAM_SOURCE");
            if (string.IsNullOrWhiteSpace(source)) throw new BuildFailedException("ZUMA_UPSTREAM_SOURCE is required.");
            RunImport(source);
        }
        static void RunImport(string source)
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var info = new ProcessStartInfo {
                FileName = "python3", Arguments = Quote(Path.Combine(root,"scripts/import-zuma-assets.py")) + " --source " + Quote(source),
                WorkingDirectory = root, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true
            };
            using (var process = Process.Start(info))
            {
                // Consume both pipes concurrently: decoder diagnostics cannot deadlock import.
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new BuildFailedException("Zuma import failed: " + error.Result + output.Result);
                UnityEngine.Debug.Log(output.Result);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateContent();
        }
        static string Quote(string value) { return "\"" + value.Replace("\\","\\\\").Replace("\"","\\\"") + "\""; }

        [MenuItem("Xanadu/Zuma/Validate imported content")]
        public static void ValidateContent()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(root,AssetRoot,"manifest.json");
            if (!File.Exists(path)) throw new BuildFailedException("Zuma manifest is missing. Run scripts/import-zuma-assets.py.");
            var manifest = JObject.Parse(File.ReadAllText(path));
            Require((int?)manifest["contentVersion"] == 1 && (string)manifest["sourceCommit"] == SourceCommit, "Wrong content/source version.");
            var maps = (JArray)manifest["maps"];
            Require(maps != null && maps.Count == 18 && maps.Select(m=>(string)m["id"]).SequenceEqual(MapIds), "The 18-map whitelist changed.");
            var difficulties=(JArray)manifest["difficulties"];
            Require(difficulties != null && difficulties.Values<string>().SequenceEqual(new[]{"rabbit","eagle","jaguar","sun_god"}), "The four difficulty IDs changed.");
            var files = (JArray)manifest["files"];
            Require(files != null && files.Count > 100,"Incomplete resource list.");
            var expected=files.Select(f=>(string)f["path"]).Append("manifest.json").OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            var actual=Directory.GetFiles(Path.Combine(root,AssetRoot),"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta",StringComparison.Ordinal)).Select(p=>p.Substring(Path.Combine(root,AssetRoot).Length).Replace('\\','/')).OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            Require(expected.SequenceEqual(actual),"Unmanifested or missing Zuma resource files.");
            var audioResources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var relative=(string)file["path"];
                Require(!string.IsNullOrEmpty(relative) && !relative.Contains("..") && !Path.IsPathRooted(relative),"Unsafe asset path.");
                var full=Path.Combine(root,AssetRoot,relative);
                Require(File.Exists(full),"Missing asset "+relative);
                using (var stream=File.OpenRead(full))
                using (var hash=SHA256.Create())
                {
                    var digest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                    Require(digest==(string)file["sha256"],"Asset hash mismatch "+relative);
                }
                if (relative.StartsWith("Audio/",StringComparison.Ordinal))
                {
                    var resourceId="Zuma/"+relative.Substring(0,relative.Length-Path.GetExtension(relative).Length);
                    Require(audioResources.Add(resourceId),"Duplicate audio resource ID "+resourceId);
                    // A source file/hash can exist even when FSBTool rejected its
                    // codec and Unity produced no AudioClip. Validate imported
                    // objects and the exact Resources lookup before packaging.
                    var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(AssetRoot+relative);
                    Require(clip!=null,"AudioClip import failed "+relative+"; inspect FSBTool import diagnostics.");
                    Require(clip.samples>0 && clip.frequency>0 && clip.channels>0,"Invalid imported AudioClip "+relative);
                    Require(Resources.Load<AudioClip>(resourceId)==clip,"Resources.Load<AudioClip> failed or resolved another asset: "+resourceId);
                    Require(clip.LoadAudioData() && clip.loadState==AudioDataLoadState.Loaded,"AudioClip data failed to load: "+relative);
                }
                if (file["width"] != null)
                {
                    var importer=AssetImporter.GetAtPath(AssetRoot+relative) as TextureImporter;
                    Require(importer!=null,"Missing texture importer "+relative);
                    importer.GetSourceTextureWidthAndHeight(out var width,out var height);
                    Require(width==(int)file["width"] && height==(int)file["height"],"Source texture dimensions changed "+relative);
                    var android=importer.GetPlatformTextureSettings("Android");
                    Require(!importer.mipmapEnabled && !importer.isReadable && importer.npotScale==TextureImporterNPOTScale.None,"Unsafe texture settings "+relative);
                    Require(importer.maxTextureSize>=Math.Max(width,height) && android.maxTextureSize>=Math.Max(width,height),"Atlas would be downsampled "+relative);
                    var hasAlpha=(bool)file["hasAlpha"];
                    var expectedImport=hasAlpha ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC2_RGB4;
                    Require(width%4==0 && height%4==0,"ETC2 block dimensions are not multiples of four: "+relative);
                    Require(importer.DoesSourceTextureHaveAlpha()==hasAlpha && importer.alphaIsTransparency==hasAlpha,"Texture alpha contract changed: "+relative);
                    Require(android.overridden && android.format==expectedImport,"ETC2 override missing or wrong alpha format "+relative);
                    var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot+relative);
                    Require(texture!=null && texture.width==width && texture.height==height,"Texture import failed or resized source: "+relative);
                    if (EditorUserBuildSettings.activeBuildTarget==BuildTarget.Android)
                    {
                        var expectedActual=hasAlpha ? TextureFormat.ETC2_RGBA8 : TextureFormat.ETC2_RGB;
                        // Import settings are requests; incompatible dimensions previously
                        // silently serialized 28 textures as uncompressed RGB/RGBA.
                        Require(texture.format==expectedActual,"Actual Android texture format is "+texture.format+" instead of "+expectedActual+": "+relative);
                    }
                }
            }
            Require(audioResources.Count==48,"Expected 38 effects and 10 music clips, all successfully loaded.");
            foreach (var map in maps)
            {
                var level=JObject.Parse(File.ReadAllText(Path.Combine(root,AssetRoot,(string)map["level"]+".json"))).ToObject<ZumaLevelDefinition>();
                Require(level != null && level.Id==(string)map["id"] && level.Points != null && level.Points.Length==(int)map["pointCount"] && level.Points.Length>1,"Invalid level definition.");
                Require(level.Coins!=null && level.Coins.Length>0 && Finite(level.FrogX) && Finite(level.FrogY),"Invalid frog/coin data.");
                foreach(var point in level.Points) Require(Finite(point.X)&&Finite(point.Y),"Non-finite path point.");
                foreach(var difficulty in difficulties) Require(difficulty.Type==JTokenType.String,"Invalid map/difficulty reference.");
            }
            var music=(JArray)manifest["music"];
            Require(music!=null && music.Count==8 && music.Select(m=>(int)m["order"]).SequenceEqual(new[]{0,24,28,32,34,36,38,39}),"Music order mapping changed.");
            Require(((JArray)manifest["sounds"]).Count==38,"Missing original sound effects.");
            UnityEngine.Debug.Log("Zuma content verified: 18 maps x 4 difficulties, original atlases/fonts, 38 effects, eight decoded module segments; all 48 AudioClips loaded through AssetDatabase and Resources, SHA-256, block-aligned source dimensions and actual Android ETC2 format checks passed.");
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static void Require(bool condition,string message) { if (!condition) throw new BuildFailedException("Zuma: "+message); }
        public void OnPreprocessBuild(BuildReport report) { ValidateContent(); }
    }
}
