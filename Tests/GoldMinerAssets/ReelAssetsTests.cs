using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.EditorTools;

internal static class ReelAssetsTests
{
    public static void ValidateDelivery(string root, string[] args, Action<bool, string> check)
    {
        var path = Path.Combine(root, "docs/gold-miner/production-assets-v4-reel.json");
        check(File.Exists(path), "Whole miner reel animation requires an independent v4 production manifest");
        var manifest = JsonSerializer.Deserialize<GoldMinerAdditionalAssetManifest>(File.ReadAllText(path), new JsonSerializerOptions { IncludeFields = true });
        GoldMinerReelAssetPlan.Validate(manifest);
        if (args.Contains("--prepare") || args.Contains("--prepare-reel")) GoldMinerReelAssetPlan.PrepareFiles(root, manifest);
        check(manifest.assets.Length == 2 && manifest.assets.Sum(a => a.slices.Length) == 12, "Two whole-miner atlases and twelve registered frames");
        foreach (var a in manifest.assets)
        {
            var target = Path.Combine(root, a.target_path);
            check(File.Exists(target), "Missing miner reel atlas: " + a.id);
            var bytes = File.ReadAllBytes(target); GoldMinerAssetPlan.VerifyBytes(a, bytes);
            check(bytes.Length == a.source_bytes && GoldMinerAssetPlan.Hash(bytes) == a.source_sha256, "Original reel atlas bytes preserved: " + a.id);
            var meta = File.ReadAllText(target + ".meta");
            check(meta.Contains("spriteMode: 2") && meta.Contains("textureType: 8") && meta.Contains("alphaUsage: 1") && meta.Contains("alphaIsTransparency: 1"), "Reel Multiple sprite RGBA import: " + a.id);
            check(meta.Contains("buildTarget: Android") && meta.Contains("textureFormat: 47") && meta.Contains("maxTextureSize: 1024") && meta.Contains("enableMipMap: 0"), "Reel ETC2 RGBA8 1024 no-mip profile: " + a.id);
            check(meta.Contains("userData: GoldMiner-production-v4-reel"), "Reel versioned provenance: " + a.id);
            foreach (var frame in a.slices)
            {
                check(meta.Contains("name: " + frame.name) && meta.Contains("        y: " + frame.unity_rect_xywh_bottom_left[1] + "\n"), "Registered reel source slice: " + a.id + "/" + frame.name);
                check(meta.Contains("spriteID: " + GoldMinerAssetPlan.StableGuid(a.target_path + "#" + frame.name)), "Stable reel frame identity: " + a.id + "/" + frame.name);
            }
        }
    }

    public static void Run(string root, Action<bool, string> check, Action<Action, string> reject)
    {
        // Existing checked-in PNG bytes keep source/target conflict tests independent of new artwork.
        var png = File.ReadAllBytes(Path.Combine(root, "Assets/Resources/GoldMiner/characters/miner-p1-states.png"));
        var manifest = new GoldMinerAdditionalAssetManifest
        {
            schema_version = 4,
            import_profile = new GoldMinerProfile { max_texture_size = 1024, android_compression_quality = 100 },
            assets = new[] { Asset("miner-p1-reel-v4", png), Asset("miner-p2-reel-v4", png) }
        };
        GoldMinerReelAssetPlan.Validate(manifest);
        check(manifest.assets.Sum(a => a.slices.Length) == 12, "V4 accepts twelve equal-size source rectangles at independent non-grid positions");
        var first = manifest.assets[0]; var second = manifest.assets[1]; var frame = first.slices[0];
        first.slices[1].rect_xywh_top_left[2] = 110;
        first.slices[1].unity_rect_xywh_bottom_left[2] = 110;
        first.slices[1].planned_pivot_normalized_bottom_left[0] = 80.0 / 110;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Different reel frame widths must be rejected before runtime");
        first.slices[1] = Asset(first.id, png).slices[1];
        first.slices[1].rect_xywh_top_left[3] = 110;
        first.slices[1].unity_rect_xywh_bottom_left[1] = first.source_size_px[1] - 3 - 110;
        first.slices[1].unity_rect_xywh_bottom_left[3] = 110;
        first.slices[1].planned_pivot_normalized_bottom_left[1] = 1 - 50.0 / 110;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Different reel frame heights must be rejected before runtime");
        first.slices[1] = Asset(first.id, png).slices[1];
        manifest.schema_version = 3;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel contract rejects other schema versions"); manifest.schema_version = 4;
        reject(() => GoldMinerAdditionalAssetPlan.Validate(manifest), "V2 remains closed to reel artwork");
        reject(() => GoldMinerCaughtAssetPlan.Validate(manifest), "V3 remains closed to reel artwork");
        manifest.assets[1] = first;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Duplicate miner reel identity rejected"); manifest.assets[1] = second;
        first.id = "miner-p3-reel-v4";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Unknown reel atlas rejected"); first.id = "miner-p1-reel-v4";
        first.group = "objects";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel atlas belongs only to characters"); first.group = "characters";
        first.source_path = "artifacts/gold-miner-followup-20261028/reel-art/../unrelated.png";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel source traversal rejected"); first.source_path = Asset(first.id, png).source_path;
        first.target_path = second.target_path;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel target mapping is exact"); first.target_path = Asset(first.id, png).target_path;
        first.generation_source_path = "";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Original generated PNG provenance required"); first.generation_source_path = "/generated_images/test-original.png";
        first.generation_prompts = new[] { "" };
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Generation prompt cannot be empty"); first.generation_prompts = new[] { "Test fixture: use existing source PNG unchanged." };
        first.registration_tolerance_source_px = 3;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Miner registration tolerance is bounded to two source pixels"); first.registration_tolerance_source_px = 2;
        first.measured_alpha_top_range_in_frame_source_px[1] = 21;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Measured hat-top drift must stay within the registration tolerance"); first.measured_alpha_top_range_in_frame_source_px[1] = 20;
        manifest.import_profile.max_texture_size = 2048;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel max texture size stays 1024"); manifest.import_profile.max_texture_size = 1024;
        first.sprite_mode = "Single";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel atlas must use Multiple sprites"); first.sprite_mode = "Multiple";
        first.android_format = "ETC2_RGB4";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel source alpha retained"); first.android_format = "ETC2_RGBA8";
        first.slices = first.slices.Take(5).ToArray();
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Every reel atlas requires all six frames"); first.slices = Asset(first.id, png).slices; frame = first.slices[0];
        frame.name = "frame_1";
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel names and playback order are exact"); frame.name = "frame_0";
        frame.unity_rect_xywh_bottom_left[1]++;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Incorrect source-to-Unity rect conversion rejected"); frame.unity_rect_xywh_bottom_left[1]--;
        frame.rect_xywh_top_left[0] = -1;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel frame cannot leave source bounds"); frame.rect_xywh_top_left[0] = 0;
        first.slices[1] = CloneOverlapping(frame, "frame_1");
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Reel frames cannot overlap neighbouring poses"); first.slices[1] = Asset(first.id, png).slices[1];
        frame.planned_pivot_normalized_bottom_left[1] += .1;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Rope endpoint and Unity pivot must agree"); frame.planned_pivot_normalized_bottom_left[1] -= .1;
        frame.foot_anchor_xy_top_left_source_px[0] = double.NaN;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Nonfinite reel foot anchor rejected"); frame.foot_anchor_xy_top_left_source_px[0] = 50;
        frame.rope_exit_xy_top_left_source_px[0] = 101;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Rope exit belongs inside the registered frame"); frame.rope_exit_xy_top_left_source_px[0] = 80;
        frame.rope_exit_xy_top_left_source_px[1] = double.PositiveInfinity;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Nonfinite rope exit rejected"); frame.rope_exit_xy_top_left_source_px[1] = 50;
        frame.axle_xy_top_left_source_px[0] = -1;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Axle must remain inside the registered frame"); frame.axle_xy_top_left_source_px[0] = 70;
        first.slices[1].foot_anchor_xy_top_left_source_px[0] += 3;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Foot registration relative to rope cannot drift beyond two source pixels"); first.slices[1].foot_anchor_xy_top_left_source_px[0] -= 3;
        first.slices[2].axle_xy_top_left_source_px[1] += 3;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Winch axle relative to rope cannot drift beyond two source pixels"); first.slices[2].axle_xy_top_left_source_px[1] -= 3;
        frame.planned_border_left_bottom_right_top_source_px[0] = 1;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Whole miner frames cannot use nine-slice borders"); frame.planned_border_left_bottom_right_top_source_px[0] = 0;
        first.slices[5] = null;
        reject(() => GoldMinerReelAssetPlan.Validate(manifest), "Null reel frame rejected"); first.slices[5] = Asset(first.id, png).slices[5];
        var temp = Path.Combine(Path.GetTempPath(), "GoldMinerReel-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(temp, "source"); var destination = Path.Combine(temp, "destination");
        try
        {
            foreach (var a in manifest.assets) Write(Path.Combine(source, a.source_path), png);
            var secondSource = Path.Combine(source, second.source_path); Write(secondSource, new byte[] { 3, 2, 1 });
            reject(() => GoldMinerReelAssetPlan.PrepareFiles(destination, manifest, source), "Second reel source conflict rejected before writes");
            check(!Directory.Exists(destination), "No partial first reel atlas on second source conflict"); Write(secondSource, png);
            var secondTarget = Path.Combine(destination, second.target_path); Write(secondTarget, new byte[] { 7, 8, 9 });
            reject(() => GoldMinerReelAssetPlan.PrepareFiles(destination, manifest, source), "Second reel target conflict rejected before writes");
            check(File.ReadAllBytes(secondTarget).SequenceEqual(new byte[] { 7, 8, 9 }) && !File.Exists(Path.Combine(destination, first.target_path)), "Reel conflict bytes preserved without partial copy");
            File.Delete(secondTarget); File.WriteAllText(secondTarget + ".meta", "bad-meta");
            reject(() => GoldMinerReelAssetPlan.PrepareFiles(destination, manifest, source), "Invalid reel metadata rejected before writes");
            check(!File.Exists(Path.Combine(destination, first.target_path)), "No first reel write when second metadata is invalid");
            const string guid = "0123456789abcdef0123456789abcdef";
            File.WriteAllText(secondTarget + ".meta", "fileFormatVersion: 2\nguid: " + guid + "\n");
            GoldMinerReelAssetPlan.PrepareFiles(destination, manifest, source);
            foreach (var a in manifest.assets)
            {
                var target = Path.Combine(destination, a.target_path); var meta = File.ReadAllText(target + ".meta");
                check(File.ReadAllBytes(target).SequenceEqual(png), "Reel atlas copied without pixel edits: " + a.id);
                check(meta.Contains("spriteMode: 2") && meta.Contains("maxTextureSize: 1024") && meta.Contains("textureFormat: 47") && meta.Contains("enableMipMap: 0") && meta.Contains("userData: GoldMiner-production-v4-reel"), "Reel six-frame RGBA import metadata: " + a.id);
                foreach (var s in a.slices)
                    check(meta.Contains("name: " + s.name) && meta.Contains("spriteID: " + GoldMinerAssetPlan.StableGuid(a.target_path + "#" + s.name)), "Stable reel slice metadata: " + a.id + "/" + s.name);
            }
            check(File.ReadAllText(secondTarget + ".meta").Contains("guid: " + guid), "Existing reel atlas GUID retained");
            Directory.Delete(source, true);
            var before = Directory.GetFiles(destination, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
            var timestamp = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            foreach (var p in before.Keys) File.SetLastWriteTimeUtc(p, timestamp);
            GoldMinerReelAssetPlan.PrepareFiles(destination, manifest, source);
            check(before.All(pair => File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value)), "Reel fresh checkout needs no ignored source PNGs");
            check(before.Keys.All(p => File.GetLastWriteTimeUtc(p) == timestamp), "Reel repeated prepare preserves file timestamps");
            File.Delete(Path.Combine(destination, first.target_path));
            reject(() => GoldMinerReelAssetPlan.PrepareFiles(destination, manifest, source), "Missing reel source and target fails explicitly");
        }
        finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
    }

    private static GoldMinerSlice CloneOverlapping(GoldMinerSlice original, string name)
    {
        return new GoldMinerSlice { name = name, rect_xywh_top_left = (int[])original.rect_xywh_top_left.Clone(), unity_rect_xywh_bottom_left = (int[])original.unity_rect_xywh_bottom_left.Clone(), planned_pivot_normalized_bottom_left = (double[])original.planned_pivot_normalized_bottom_left.Clone(), foot_anchor_xy_top_left_source_px = (double[])original.foot_anchor_xy_top_left_source_px.Clone(), rope_exit_xy_top_left_source_px = (double[])original.rope_exit_xy_top_left_source_px.Clone(), axle_xy_top_left_source_px = (double[])original.axle_xy_top_left_source_px.Clone(), planned_border_left_bottom_right_top_source_px = new int[4] };
    }
    private static void Write(string path, byte[] bytes) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path, bytes); }
    private static int BigEndian(byte[] data, int offset) { return data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]; }
    private static GoldMinerAsset Asset(string id, byte[] png)
    {
        var width = BigEndian(png, 16); var height = BigEndian(png, 20);
        var rects = new[] { new[] { 0, 0, 100, 100 }, new[] { 130, 3, 100, 100 }, new[] { 270, 8, 100, 100 }, new[] { 2, 120, 100, 100 }, new[] { 140, 125, 100, 100 }, new[] { 280, 125, 100, 100 } };
        return new GoldMinerAsset
        {
            id = id, group = "characters", source_path = "artifacts/gold-miner-followup-20261028/reel-art/" + id + ".png", source_sha256 = GoldMinerAssetPlan.Hash(png), source_bytes = png.Length, source_size_px = new[] { width, height }, source_mode = "RGBA", source_alpha_range = new[] { 0, 255 },
            target_path = "Assets/Resources/GoldMiner/characters/" + id + ".png", resource_key = "GoldMiner/characters/" + id, import_profile = "sheet", sprite_mode = "Multiple", android_format = "ETC2_RGBA8", alpha_source = "FromInput", source_has_transparency = true, alpha_is_transparency = true, planned_single_pivot_normalized_bottom_left = new[] { .5, .5 },
            generation_tool = "built-in image_gen", generation_source_path = "/generated_images/test-original.png", generation_prompts = new[] { "Test fixture: use existing source PNG unchanged." },
            registration_tolerance_source_px = 2, registered_alpha_top_in_frame_source_px = 18, measured_alpha_top_range_in_frame_source_px = new[] { 18, 20 },
            slices = rects.Select((r, index) => new GoldMinerSlice { name = "frame_" + index, rect_xywh_top_left = r, unity_rect_xywh_bottom_left = new[] { r[0], height-r[1]-r[3], r[2], r[3] }, planned_pivot_normalized_bottom_left = new[] { .8, .5 }, planned_border_left_bottom_right_top_source_px = new int[4], foot_anchor_xy_top_left_source_px = new[] { r[0]+r[2]*.8-30, r[1]+r[3]*.5+20 }, rope_exit_xy_top_left_source_px = new[] { r[0]+r[2]*.8, r[1]+r[3]*.5 }, axle_xy_top_left_source_px = new[] { r[0]+r[2]*.8-10, r[1]+r[3]*.5-15 } }).ToArray()
        };
    }
}
