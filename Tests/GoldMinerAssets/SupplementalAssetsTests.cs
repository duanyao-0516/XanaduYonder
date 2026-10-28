using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.EditorTools;

internal static class SupplementalAssetsTests
{
    public static void ValidateDelivery(string root, string[] args, Action<bool, string> check)
    {
        var manifest = JsonSerializer.Deserialize<GoldMinerAdditionalAssetManifest>(File.ReadAllText(Path.Combine(root, "docs/gold-miner/production-assets-v2-additions.json")), new JsonSerializerOptions { IncludeFields = true });
        GoldMinerAdditionalAssetPlan.Validate(manifest);
        if (args.Contains("--prepare") || args.Contains("--prepare-additions")) GoldMinerAdditionalAssetPlan.PrepareFiles(root, manifest);
        foreach (var a in manifest.assets)
        {
            var path = Path.Combine(root, a.target_path);
            check(File.Exists(path), "Missing supplemental production asset: " + a.id);
            var bytes = File.ReadAllBytes(path);
            check(bytes.Length == a.source_bytes && GoldMinerAssetPlan.Hash(bytes) == a.source_sha256, "Supplemental original PNG changed: " + a.id);
            GoldMinerAssetPlan.VerifyBytes(a, bytes);
            var meta = File.ReadAllText(path + ".meta");
            check(meta.Contains("spriteMode: 1") && meta.Contains("textureType: 8") && meta.Contains("alphaUsage: 1") && meta.Contains("alphaIsTransparency: 1"), "Supplemental Single sprite alpha metadata: " + a.id);
            check(meta.Contains("buildTarget: Android") && meta.Contains("textureFormat: 47") && meta.Contains("maxTextureSize: 1024") && meta.Contains("crunchedCompression: 0"), "Supplemental ETC2 RGBA import profile: " + a.id);
        }
    }

    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        // A valid one-pixel RGBA PNG keeps these boundary tests independent of the delivery artwork.
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
        var manifest = new GoldMinerAdditionalAssetManifest
        {
            schema_version = 2,
            import_profile = new GoldMinerProfile { max_texture_size = 1024, android_compression_quality = 100 },
            assets = new[] { Asset("ready-thumbsup-miner-v2", png), Asset("result-failed-miner-sign-v2", png), Asset("gold-miner-wordmark-v2", png, "ui") }
        };
        GoldMinerAdditionalAssetPlan.Validate(manifest);
        check(manifest.assets.Length == 3, "Three named supplemental sprites");
        var first = manifest.assets[0]; var second = manifest.assets[1]; var wordmark = manifest.assets[2];
        manifest.assets[2] = Asset("gold-miner-wordmark-v2", png, "characters");
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Wordmark only belongs to the ui group"); manifest.assets[2] = wordmark;
        manifest.assets[2] = Asset("unexpected-wordmark-v2", png, "ui");
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Unknown ui supplement rejected"); manifest.assets[2] = wordmark;
        manifest.assets = new[] { first, second };
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Incomplete supplemental pack rejected"); manifest.assets = new[] { first, second, wordmark };
        var id = first.id; first.id = "unexpected-character-v2";
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Unknown supplement rejected"); first.id = id;
        var target = first.target_path; first.target_path = second.target_path;
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Duplicate/mismatched supplemental mapping rejected"); first.target_path = target;
        first.source_path = "artifacts/gold-miner-ui-v2/../unrelated.png";
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Supplemental source traversal rejected"); first.source_path = "artifacts/gold-miner-ui-v2/characters/" + id + ".png";
        first.android_format = "ETC2_RGB4";
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Supplemental alpha must use ETC2 RGBA"); first.android_format = "ETC2_RGBA8";
        first.slices = new[] { new GoldMinerSlice() };
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Supplemental atlas slices rejected"); first.slices = new GoldMinerSlice[0];
        manifest.import_profile.max_texture_size = 2048;
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Supplemental import size bounded to 512 or 1024"); manifest.import_profile.max_texture_size = 1024;
        manifest.assets[1] = null;
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Null supplemental entry rejected"); manifest.assets[1] = second;
        first.planned_single_pivot_normalized_bottom_left[0] = double.NaN;
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "Nonfinite supplemental pivot rejected"); first.planned_single_pivot_normalized_bottom_left[0] = .5;
        var temp = Path.Combine(Path.GetTempPath(), "GoldMinerAdditions-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source"); var destination = Path.Combine(temp, "destination");
        try
        {
            foreach (var a in manifest.assets) Write(Path.Combine(source, a.source_path), png);
            Write(Path.Combine(source, wordmark.source_path), new byte[] { 7, 8, 9 });
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Bad third source rejected before writes");
            check(!Directory.Exists(destination), "No partial supplemental files after wordmark source conflict");
            Write(Path.Combine(source, wordmark.source_path), png);
            var wordmarkTarget = Path.Combine(destination, wordmark.target_path);
            Write(wordmarkTarget, new byte[] { 9, 8, 7 });
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Bad existing wordmark target rejected before writes");
            check(File.ReadAllBytes(wordmarkTarget).SequenceEqual(new byte[] { 9, 8, 7 }) && !File.Exists(Path.Combine(destination, first.target_path)) && !File.Exists(Path.Combine(destination, second.target_path)), "Wordmark conflict preserved without importing characters");
            Directory.Delete(destination, true);
            Write(Path.Combine(source, second.source_path), new byte[] { 1, 2, 3 });
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Bad second source rejected before writes");
            check(!Directory.Exists(destination), "No partial supplemental files after source conflict");
            Write(Path.Combine(source, second.source_path), png);
            first.source_size_px[0] = 2;
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Supplemental PNG dimensions checked independently of hash"); first.source_size_px[0] = 1;
            check(!Directory.Exists(destination), "No partial supplemental files after dimension mismatch");
            var secondTarget = Path.Combine(destination, second.target_path);
            Write(secondTarget, new byte[] { 4, 5, 6 });
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Bad existing second target rejected before writes");
            check(File.ReadAllBytes(secondTarget).SequenceEqual(new byte[] { 4, 5, 6 }), "Conflicting supplemental target preserved");
            check(!File.Exists(Path.Combine(destination, first.target_path)), "No first supplemental import when second target conflicts");
            File.Delete(secondTarget);
            File.WriteAllText(secondTarget + ".meta", "unrelated-metadata");
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Invalid existing meta rejected before writes");
            check(!File.Exists(Path.Combine(destination, first.target_path)) && File.ReadAllText(secondTarget + ".meta") == "unrelated-metadata", "Invalid meta preserved without partial writes");
            const string guid = "0123456789abcdef0123456789abcdef";
            File.WriteAllText(secondTarget + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
            GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source);
            check(File.ReadAllText(Path.Combine(destination, "Assets/Resources/GoldMiner/ui.meta")).Contains("guid: " + GoldMinerAssetPlan.StableGuid("Assets/Resources/GoldMiner/ui")), "Supplemental ui folder receives stable metadata");
            foreach (var a in manifest.assets)
            {
                var path = Path.Combine(destination, a.target_path);
                check(File.ReadAllBytes(path).SequenceEqual(png), "Supplemental original bytes preserved: " + a.id);
                var meta = File.ReadAllText(path + ".meta");
                check(meta.Contains("spriteMode: 1") && meta.Contains("textureFormat: 47") && meta.Contains("alphaIsTransparency: 1") && meta.Contains("maxTextureSize: 1024"), "Single ETC2 RGBA supplemental metadata: " + a.id);
                check(meta.Contains("userData: GoldMiner-production-v2-additions"), "Supplemental provenance marker: " + a.id);
            }
            check(File.ReadAllText(secondTarget + ".meta").Contains("guid: " + guid), "Existing supplemental GUID preserved");
            Directory.Delete(source, true);
            var before = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).ToDictionary(p => p, p => File.ReadAllBytes(p));
            var savedTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            foreach (var path in before.Keys) File.SetLastWriteTimeUtc(path, savedTime);
            GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source);
            check(before.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "Repeated supplemental preparation without source artifacts preserves bytes");
            check(before.Keys.All(path => File.GetLastWriteTimeUtc(path) == savedTime), "Repeated supplemental preparation does not rewrite identical files");
            File.Delete(Path.Combine(destination, first.target_path));
            reject(() => GoldMinerAdditionalAssetPlan.PrepareFiles(destination, manifest, source), "Missing source and target rejected explicitly");
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    private static void Write(string path, byte[] bytes) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes); }
    private static GoldMinerAsset Asset(string id, byte[] bytes, string group = "characters")
    {
        return new GoldMinerAsset
        {
            id = id, group = group, source_path = "artifacts/gold-miner-ui-v2/" + group + "/" + id + ".png",
            source_sha256 = GoldMinerAssetPlan.Hash(bytes), source_bytes = bytes.Length, source_size_px = new[] { 1, 1 },
            target_path = "Assets/Resources/GoldMiner/" + group + "/" + id + ".png", resource_key = "GoldMiner/" + group + "/" + id,
            import_profile = "standalone", sprite_mode = "Single", android_format = "ETC2_RGBA8", alpha_source = "FromInput",
            source_has_transparency = true, alpha_is_transparency = true,
            planned_single_pivot_normalized_bottom_left = new[] { .5, .5 }, slices = new GoldMinerSlice[0]
        };
    }
}
