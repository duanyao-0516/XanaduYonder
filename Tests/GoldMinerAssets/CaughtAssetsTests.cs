using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.EditorTools;

internal static class CaughtAssetsTests
{
    public static void ValidateDelivery(string root, string[] args, Action<bool, string> check)
    {
        var path = Path.Combine(root, "docs/gold-miner/production-assets-v3-caught.json");
        check(File.Exists(path), "Caught artwork requires an independent v3 production manifest");
        var manifest = JsonSerializer.Deserialize<GoldMinerAdditionalAssetManifest>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
        GoldMinerCaughtAssetPlan.Validate(manifest);
        if (args.Contains("--prepare") || args.Contains("--prepare-caught")) GoldMinerCaughtAssetPlan.PrepareFiles(root, manifest);
        check(manifest.schema_version == 3 && manifest.assets.Length == 10, "Ten exact caught/running sprites in v3");
        check(manifest.import_profile.max_texture_size == 512 && manifest.import_profile.android_compression_quality == 100, "Caught imports bounded to 512");
        foreach (var a in manifest.assets)
        {
            var target = Path.Combine(root, a.target_path);
            check(File.Exists(target), "Missing caught production artwork: " + a.id);
            var bytes = File.ReadAllBytes(target);
            check(bytes.Length == a.source_bytes && GoldMinerAssetPlan.Hash(bytes) == a.source_sha256, "Caught original PNG preserved: " + a.id);
            GoldMinerAssetPlan.VerifyBytes(a, bytes);
            var meta = File.ReadAllText(target + ".meta");
            check(meta.Contains("spriteMode: 1") && meta.Contains("textureType: 8") && meta.Contains("alphaUsage: 1") && meta.Contains("alphaIsTransparency: 1"), "Caught Single RGBA import: " + a.id);
            check(meta.Contains("buildTarget: Android") && meta.Contains("textureFormat: 47") && meta.Contains("maxTextureSize: 512") && meta.Contains("enableMipMap: 0"), "Caught Android ETC2 RGBA8 no mips profile: " + a.id);
            check(meta.Contains("userData: GoldMiner-production-v3-caught"), "Caught versioned provenance: " + a.id);
        }
    }

    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
        var names = new[] { "caught-gold-small", "caught-gold-medium", "caught-gold-large", "caught-rock-small", "caught-rock-large", "caught-diamond", "caught-mystery-bag", "caught-mole", "caught-diamond-mole", "mole-carrying-diamond-run" };
        var manifest = new GoldMinerAdditionalAssetManifest
        {
            schema_version = 3,
            import_profile = new GoldMinerProfile { max_texture_size = 512, android_compression_quality = 100 },
            assets = names.Select(id => Asset(id, png)).ToArray()
        };
        GoldMinerCaughtAssetPlan.Validate(manifest);
        check(manifest.assets.Length == 10, "Ten whitelisted captured/running sprite identities");
        var first = manifest.assets[0]; var last = manifest.assets[9];
        last.suspension_ring_center_xy_top_left = new double[0];
        GoldMinerCaughtAssetPlan.Validate(manifest);
        check(true, "Empty serialized ring array also means the running animal has no ring");
        last.suspension_ring_center_xy_top_left = null;
        manifest.schema_version = 2;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught manifest cannot pretend to be v2"); manifest.schema_version = 3;
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "v2 validator remains closed to v3");
        manifest.assets[9] = first;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Duplicate caught identities rejected"); manifest.assets[9] = last;
        first.id = "caught-unknown";
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Unknown caught identity rejected"); first.id = names[0];
        first.group = "objects";
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught sprite only belongs to characters"); first.group = "characters";
        first.source_path = "artifacts/gold-miner-followup-20261028/caught-art/../unrelated.png";
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught source traversal rejected"); first.source_path = Asset(names[0], png).source_path;
        first.target_path = "Assets/Resources/GoldMiner/characters/../unexpected.png";
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught target traversal rejected"); first.target_path = Asset(names[0], png).target_path;
        manifest.import_profile.max_texture_size = 1024;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught size cap cannot exceed 512"); manifest.import_profile.max_texture_size = 512;
        first.android_format = "ETC2_RGB4";
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught alpha cannot be discarded"); first.android_format = "ETC2_RGBA8";
        first.source_mode = "RGB";
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught source must be original RGBA"); first.source_mode = "RGBA";
        first.slices = new[] { new GoldMinerSlice() };
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught sheet slicing rejected"); first.slices = new GoldMinerSlice[0];
        first.planned_single_pivot_normalized_bottom_left[1] = double.NaN;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Nonfinite caught pivot rejected"); first.planned_single_pivot_normalized_bottom_left[1] = .75;
        first.suspension_ring_center_xy_top_left[1] = .5;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Caught pivot must agree with ring anchor"); first.suspension_ring_center_xy_top_left[1] = .25;
        first.suspension_ring_outer_diameter_px = double.NaN;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Nonfinite ring scale rejected"); first.suspension_ring_outer_diameter_px = .2;
        last.suspension_ring_center_xy_top_left = new[] { .5, .5 };
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Running diamond mole has no suspension ring"); last.suspension_ring_center_xy_top_left = null;
        manifest.assets[9] = null;
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "Null caught sprite rejected"); manifest.assets[9] = last;
        var temp = Path.Combine(Path.GetTempPath(), "GoldMinerCaught-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source"); var destination = Path.Combine(temp, "destination");
        try
        {
            foreach (var a in manifest.assets) Write(Path.Combine(source, a.source_path), png);
            var lastSource = Path.Combine(source, last.source_path);
            Write(lastSource, new byte[] { 1, 2, 3 });
            reject(() => GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source), "Tenth source conflict prevents partial imports");
            check(!Directory.Exists(destination), "No partial v3 files on final source conflict");
            var rgb = (byte[])png.Clone(); rgb[25] = 2; Write(lastSource, rgb); last.source_sha256 = GoldMinerAssetPlan.Hash(rgb);
            reject(() => GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source), "PNG RGB header rejected even with matching digest");
            check(!Directory.Exists(destination), "No writes on mismatched RGBA header");
            last.source_sha256 = GoldMinerAssetPlan.Hash(png); Write(lastSource, png);
            var lastTarget = Path.Combine(destination, last.target_path); Write(lastTarget, new byte[] { 9, 8, 7 });
            reject(() => GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source), "Tenth target conflict prevents partial imports");
            check(File.ReadAllBytes(lastTarget).SequenceEqual(new byte[] { 9, 8, 7 }) && !File.Exists(Path.Combine(destination, first.target_path)), "Conflicting caught target preserved");
            File.Delete(lastTarget); File.WriteAllText(lastTarget + ".meta", "unrelated-meta");
            reject(() => GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source), "Invalid caught GUID rejected before writes");
            check(!File.Exists(Path.Combine(destination, first.target_path)), "No first captured PNG when final metadata is invalid");
            const string guid = "0123456789abcdef0123456789abcdef";
            File.WriteAllText(lastTarget + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
            GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source);
            foreach (var a in manifest.assets)
            {
                var target = Path.Combine(destination, a.target_path); var meta = File.ReadAllText(target + ".meta");
                check(File.ReadAllBytes(target).SequenceEqual(png), "Caught source bytes preserved: " + a.id);
                check(meta.Contains("maxTextureSize: 512") && meta.Contains("textureFormat: 47") && meta.Contains("enableMipMap: 0") && meta.Contains("userData: GoldMiner-production-v3-caught"), "Caught ETC2 RGBA8 no-mip metadata: " + a.id);
            }
            check(File.ReadAllText(lastTarget + ".meta").Contains("guid: " + guid), "Existing caught GUID preserved");
            Directory.Delete(source, true);
            var before = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            foreach (var path in before.Keys) File.SetLastWriteTimeUtc(path, timestamp);
            GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source);
            check(before.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "Caught fresh-checkout preparation needs no ignored sources");
            check(before.Keys.All(path => File.GetLastWriteTimeUtc(path) == timestamp), "Caught repeated import does not rewrite identical bytes");
            File.Delete(Path.Combine(destination, first.target_path));
            reject(() => GoldMinerCaughtAssetPlan.PrepareFiles(destination, manifest, source), "Missing caught source and target fails explicitly");
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    private static void Write(string path, byte[] bytes) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes); }
    private static GoldMinerAsset Asset(string id, byte[] png)
    {
        var held = id.StartsWith("caught-", StringComparison.Ordinal);
        return new GoldMinerAsset
        {
            id = id, group = "characters", source_path = "artifacts/gold-miner-followup-20261028/caught-art/" + id + ".png",
            source_sha256 = GoldMinerAssetPlan.Hash(png), source_bytes = png.Length, source_size_px = new[] { 1, 1 }, source_mode = "RGBA", source_alpha_range = new[] { 0, 255 },
            target_path = "Assets/Resources/GoldMiner/characters/" + id + ".png", resource_key = "GoldMiner/characters/" + id,
            import_profile = "standalone", sprite_mode = "Single", android_format = "ETC2_RGBA8", alpha_source = "FromInput",
            source_has_transparency = true, alpha_is_transparency = true, slices = new GoldMinerSlice[0],
            suspension_ring_center_xy_top_left = held ? new[] { .5, .25 } : null, suspension_ring_outer_diameter_px = held ? .2 : 0,
            planned_single_pivot_normalized_bottom_left = held ? new[] { .5, .75 } : new[] { .5, .5 }
        };
    }
}
