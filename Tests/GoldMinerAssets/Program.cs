using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using XanaduYonder.EditorTools;
using XanaduYonder.Runtime.GoldMiner;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    private static void Reject(Action action, string message)
    {
        try { action(); } catch (InvalidDataException) { checks++; return; }
        throw new Exception(message);
    }
    public static void Main(string[] args)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var manifest = JsonSerializer.Deserialize<GoldMinerAssetManifest>(File.ReadAllText(Path.Combine(root, "docs/gold-miner/production-assets-v1.json")), new JsonSerializerOptions { IncludeFields = true });
        GoldMinerAssetPlan.Validate(manifest);
        Check(manifest.assets.Length == 44, "44 production textures");
        Check(manifest.assets.Sum(a => a.slices.Length) == 43, "43 exact rectangles");
        var hook = manifest.assets[0];
        var validTarget = hook.target_path;
        hook.target_path = "Assets/Resources/GoldMiner/../Unrelated.png";
        Reject(() => GoldMinerAssetPlan.Validate(manifest), "Reject path traversal");
        hook.target_path = validTarget;
        hook.slices[0].unity_rect_xywh_bottom_left[1]++;
        Reject(() => GoldMinerAssetPlan.Validate(manifest), "Reject wrong coordinate conversion");
        hook.slices[0].unity_rect_xywh_bottom_left[1]--;
        SupplementalAssetsTests.Run(Check, Reject);
        SupplementalAssetsTests.ValidateDelivery(root, args, Check);
        CaughtAssetsTests.ValidateDelivery(root, args, Check);
        CaughtAssetsTests.Run(Check, Reject);
        ReelAssetsTests.Run(root, Check, Reject);
        ReelAssetsTests.ValidateDelivery(root, args, Check);
        var temp = Path.Combine(Path.GetTempPath(), "GoldMinerAssets-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(temp, hook.target_path)));
            File.WriteAllText(Path.Combine(temp, hook.target_path), "unrelated-existing-bytes");
            Reject(() => GoldMinerAssetPlan.PrepareFiles(temp, manifest, root), "Reject a target hash conflict before copying");
            Check(File.ReadAllText(Path.Combine(temp, hook.target_path)) == "unrelated-existing-bytes", "Conflict bytes preserved");
            Check(Directory.GetFiles(temp, "*.png", SearchOption.AllDirectories).Length == 1, "No partial imports on conflict");
        }
        finally { Directory.Delete(temp, true); }
        if (args.Contains("--prepare")) GoldMinerAssetPlan.PrepareFiles(root, manifest, root);
        foreach (var asset in manifest.assets)
        {
            var path = Path.Combine(root, asset.target_path);
            Check(File.Exists(path), "Missing production asset: " + asset.id);
            Check(GoldMinerAssetPlan.Hash(File.ReadAllBytes(path)) == asset.source_sha256, "Original PNG changed: " + asset.id);
            var meta = File.ReadAllText(path + ".meta");
            Check(meta.Contains("textureType: 8") && meta.Contains("spriteGenerateFallbackPhysicsShape: 0"), "Sprite import and physics: " + asset.id);
            Check(meta.Contains("guid: ") && meta.Contains("buildTarget: Android") && meta.Contains("crunchedCompression: 0"), "Versioned importer metadata: " + asset.id);
            foreach (var slice in asset.slices)
            {
                Check(meta.Contains("name: " + slice.name), "Slice missing: " + slice.name);
                Check(meta.Contains("        y: " + slice.unity_rect_xywh_bottom_left[1] + "\n"), "Bottom-left source pixel coordinates: " + slice.name);
            }
        }
        using (var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs/gold-miner/audio-cues-v1.json"))))
        {
            foreach (var cue in document.RootElement.GetProperty("cues").EnumerateArray())
            {
                var id = cue.GetProperty("id").GetString();
                var spec = GoldMinerSoundSynthesis.GetCue(id);
                var samples = GoldMinerSoundSynthesis.Generate(spec);
                Check(samples.Length == (int)Math.Ceiling(cue.GetProperty("duration_seconds").GetDouble() * 22050), id + " duration");
                Check(Math.Abs(spec.Gain - cue.GetProperty("gain").GetDouble()) < .000001, id + " gain");
                Check(samples[0] == 0 && samples[samples.Length - 1] == 0, id + " click-free endpoints");
                Check(samples.All(s => !float.IsNaN(s) && !float.IsInfinity(s) && Math.Abs(s) <= .500001), id + " finite amplitude bound");
                Check(samples.Any(s => Math.Abs(s) > .05), id + " non-silent");
                Check(samples.SequenceEqual(GoldMinerSoundSynthesis.Generate(spec)), id + " deterministic noise");
                var frequencies = cue.GetProperty("frequencies_hz").EnumerateArray().Select(f => f.GetDouble()).ToArray();
                Check(spec.Frequencies.SequenceEqual(frequencies), id + " exact cue frequencies");
            }
        }
        var pool = new GoldMinerVoicePool();
        for (var i=0;i<4;i++) Check(pool.Acquire(3, 0, 1) == i, "Free voice " + i);
        Check(pool.Acquire(1, .1, 1) == -1, "Low priority cannot steal protected sound");
        Check(pool.Acquire(4, .1, 1) == 0, "Oldest lowest-priority voice replaced");
        Check(pool.Acquire(3, .1, 1) == 1, "Equal priority replaces oldest");
        pool.Clear();
        Check(pool.Acquire(1, .1, 1) == 0, "Stop clears all voices");
        Console.WriteLine("GOLD_MINER_ASSET_CHECKS_PASS: " + checks + " checks; 44 original PNG, 43 slices, 3 supplemental v2 sprites, 10 captured/running v3 sprites, 2 reel v4 atlases/12 frames, 10 audio cues, four-voice priority policy");
    }
}
