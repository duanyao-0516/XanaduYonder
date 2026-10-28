using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace XanaduYonder.EditorTools
{
#pragma warning disable 0649 // Fields are populated from the versioned manifest.
    [Serializable] public sealed class GoldMinerAssetManifest
    {
        public int schema_version;
        public GoldMinerArchive source_archive;
        public GoldMinerAsset[] assets;
        public GoldMinerProfiles import_profiles;
    }
    [Serializable] public sealed class GoldMinerAdditionalAssetManifest
    {
        public int schema_version;
        public GoldMinerAsset[] assets;
        public GoldMinerProfile import_profile;
    }
    [Serializable] public sealed class GoldMinerArchive { public string path; public string sha256; }
    [Serializable] public sealed class GoldMinerProfiles
    {
        public GoldMinerProfile standalone, sheet, background, illustration, ui, effect;
        public GoldMinerProfile Get(string key)
        {
            switch (key)
            {
                case "standalone": return standalone; case "sheet": return sheet; case "background": return background;
                case "illustration": return illustration; case "ui": return ui; case "effect": return effect;
                default: throw new InvalidDataException("Unknown Gold Miner import profile: " + key);
            }
        }
    }
    [Serializable] public sealed class GoldMinerProfile { public int max_texture_size; public int android_compression_quality; }
    [Serializable] public sealed class GoldMinerAsset
    {
        public string id, group, source_path, source_archive_member, source_sha256, target_path, resource_key, import_profile, sprite_mode, android_format, alpha_source, source_mode;
        public int source_bytes;
        public int[] source_size_px, source_alpha_range;
        public bool source_has_transparency, alpha_is_transparency;
        public double[] planned_single_pivot_normalized_bottom_left;
        public double[] suspension_ring_center_xy_top_left;
        public double suspension_ring_outer_diameter_px;
        public string generation_tool, generation_source_path;
        public string[] generation_prompts;
        public int registered_alpha_top_in_frame_source_px;
        public int[] measured_alpha_top_range_in_frame_source_px;
        public double registration_tolerance_source_px;
        public GoldMinerSlice[] slices;
    }
    [Serializable] public sealed class GoldMinerSlice
    {
        public string name;
        public int[] rect_xywh_top_left, unity_rect_xywh_bottom_left;
        public double[] planned_pivot_normalized_bottom_left;
        public double[] foot_anchor_xy_top_left_source_px, rope_exit_xy_top_left_source_px, axle_xy_top_left_source_px;
        public int[] planned_border_left_bottom_right_top_source_px;
    }
#pragma warning restore 0649

    // The same pure preflight/copy code is executed by the CLI checks and the Unity editor.
    public static class GoldMinerAssetPlan
    {
        private const string Prefix = "Assets/Resources/GoldMiner/";
        private static readonly HashSet<string> Groups = new HashSet<string> { "characters", "objects", "backgrounds", "ui", "effects" };
        public static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        public static string StableGuid(string path) { return Hash(Encoding.UTF8.GetBytes("xanadu-gold-miner-v1/" + path)).Substring(0, 32); }
        private static long SliceId(string path, string name) { return long.Parse(StableGuid(path + "#" + name).Substring(0, 15), NumberStyles.HexNumber, CultureInfo.InvariantCulture); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        public static void Validate(GoldMinerAssetManifest manifest)
        {
            Require(manifest != null && manifest.schema_version == 1 && manifest.assets != null && manifest.assets.Length == 44, "Expected Gold Miner v1 with 44 assets");
            var paths = new HashSet<string>(StringComparer.Ordinal); var sliceCount = 0; var alphaCount = 0;
            foreach (var a in manifest.assets)
            {
                Require(a != null && Groups.Contains(a.group) && !string.IsNullOrEmpty(a.id) && Regex.IsMatch(a.id, @"^[a-z0-9_-]+$"), "Invalid Gold Miner asset identity");
                var relative = a.group + "/" + a.id + ".png";
                Require(a.target_path == Prefix + relative && a.resource_key == "GoldMiner/" + a.group + "/" + a.id && paths.Add(a.target_path), "Invalid/duplicate Gold Miner target mapping: " + a.id);
                Require(a.source_path == "artifacts/gold-miner-ui-v1/" + relative && a.source_archive_member == "Xanadu-GoldMiner-UI-v1/" + relative, "Invalid source mapping: " + a.id);
                Require(a.source_sha256 != null && Regex.IsMatch(a.source_sha256, "^[a-f0-9]{64}$") && a.source_bytes > 0, "Invalid source hash/length: " + a.id);
                Require(a.source_size_px != null && a.source_size_px.Length == 2 && a.source_size_px[0] > 0 && a.source_size_px[1] > 0, "Invalid source dimensions: " + a.id);
                Require(a.slices != null && a.sprite_mode == (a.slices.Length > 0 ? "Multiple" : "Single"), "Invalid sprite mode: " + a.id);
                Require(a.android_format == (a.source_has_transparency ? "ETC2_RGBA8" : "ETC2_RGB4") && a.alpha_source == (a.source_has_transparency ? "FromInput" : "None") && a.alpha_is_transparency == a.source_has_transparency, "Invalid alpha/compression contract: " + a.id);
                if (a.source_has_transparency) alphaCount++;
                var profile = manifest.import_profiles.Get(a.import_profile);
                Require(profile != null && (profile.max_texture_size == 512 || profile.max_texture_size == 1024 || profile.max_texture_size == 2048) && profile.android_compression_quality == 100, "Invalid profile: " + a.id);
                ValidatePivot(a.planned_single_pivot_normalized_bottom_left, a.id);
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var s in a.slices)
                {
                    Require(s != null && s.name != null && Regex.IsMatch(s.name, "^[a-zA-Z0-9_-]+$") && names.Add(s.name), "Duplicate/invalid slice: " + a.id);
                    var top = s.rect_xywh_top_left; var bottom = s.unity_rect_xywh_bottom_left;
                    Require(top != null && top.Length == 4 && bottom != null && bottom.Length == 4, "Invalid rect: " + a.id);
                    Require(top[0] >= 0 && top[1] >= 0 && top[2] > 0 && top[3] > 0 && top[0] + top[2] <= a.source_size_px[0] && top[1] + top[3] <= a.source_size_px[1], "Slice outside source: " + a.id);
                    Require(bottom[0] == top[0] && bottom[1] == a.source_size_px[1] - top[1] - top[3] && bottom[2] == top[2] && bottom[3] == top[3], "Incorrect top-left to Unity coordinate conversion: " + a.id);
                    ValidatePivot(s.planned_pivot_normalized_bottom_left, a.id);
                    var b = s.planned_border_left_bottom_right_top_source_px;
                    Require(b != null && b.Length == 4 && b[0] >= 0 && b[1] >= 0 && b[2] >= 0 && b[3] >= 0 && b[0] + b[2] <= top[2] && b[1] + b[3] <= top[3], "Invalid border: " + a.id);
                    sliceCount++;
                }
            }
            Require(sliceCount == 43 && alphaCount == 37, "Gold Miner v1 must have 43 slices and 37 transparent images");
        }
        private static void ValidatePivot(double[] pivot, string id)
        {
            Require(pivot != null && pivot.Length == 2 && pivot[0] >= 0 && pivot[0] <= 1 && pivot[1] >= 0 && pivot[1] <= 1, "Invalid normalized pivot: " + id);
        }
        public static void PrepareFiles(string destinationRoot, GoldMinerAssetManifest manifest, string sourceRoot = null)
        {
            Validate(manifest); sourceRoot = sourceRoot ?? destinationRoot;
            // Preflight every existing target before any writes, including targets found after the first one.
            foreach (var a in manifest.assets)
            {
                var target = Path.Combine(destinationRoot, a.target_path);
                if (File.Exists(target)) VerifyBytes(a, File.ReadAllBytes(target));
            }
            var pending = new Dictionary<string, byte[]>();
            var archivePath = Path.Combine(sourceRoot, manifest.source_archive.path);
            if (File.Exists(archivePath)) Require(Hash(File.ReadAllBytes(archivePath)) == manifest.source_archive.sha256, "Gold Miner delivery ZIP hash mismatch");
            using (var archive = File.Exists(archivePath) ? ZipFile.OpenRead(archivePath) : null)
            {
                foreach (var a in manifest.assets)
                {
                    var target = Path.Combine(destinationRoot, a.target_path);
                    if (File.Exists(target)) continue;
                    var source = Path.Combine(sourceRoot, a.source_path);
                    byte[] bytes;
                    if (File.Exists(source)) bytes = File.ReadAllBytes(source);
                    else
                    {
                        var entry = archive == null ? null : archive.GetEntry(a.source_archive_member);
                        Require(entry != null, "Missing Gold Miner source and archive entry: " + a.source_path);
                        using (var input = entry.Open()) using (var output = new MemoryStream()) { input.CopyTo(output); bytes = output.ToArray(); }
                    }
                    VerifyBytes(a, bytes); pending.Add(a.target_path, bytes);
                }
            }
            // Source hashes are verified in full; a conflict never partially replaces the production pack.
            foreach (var a in manifest.assets)
            {
                var target = Path.Combine(destinationRoot, a.target_path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                EnsureFolderMeta(destinationRoot, "Assets/Resources/GoldMiner");
                EnsureFolderMeta(destinationRoot, "Assets/Resources/GoldMiner/" + a.group);
                byte[] bytes; if (pending.TryGetValue(a.target_path, out bytes)) File.WriteAllBytes(target, bytes);
                var metaPath = target + ".meta";
                var guid = StableGuid(a.target_path);
                if (File.Exists(metaPath))
                {
                    var match = Regex.Match(File.ReadAllText(metaPath), @"(?m)^guid: ([a-f0-9]{32})$");
                    Require(match.Success, "Invalid existing asset GUID: " + metaPath); guid = match.Groups[1].Value;
                }
                var metadata = TextureMeta(a, manifest.import_profiles.Get(a.import_profile), guid);
                if (!File.Exists(metaPath) || File.ReadAllText(metaPath) != metadata) File.WriteAllText(metaPath, metadata, new UTF8Encoding(false));
            }
        }
        internal static void VerifyBytes(GoldMinerAsset a, byte[] bytes)
        {
            Require(bytes.Length == a.source_bytes && Hash(bytes) == a.source_sha256, "Gold Miner PNG hash/length conflict; existing file preserved: " + a.target_path);
            Require(bytes.Length >= 24 && bytes[0] == 137 && bytes[1] == 80 && ReadBigEndian(bytes, 16) == a.source_size_px[0] && ReadBigEndian(bytes, 20) == a.source_size_px[1], "PNG dimensions differ: " + a.id);
        }
        internal static void VerifyRgbaBytes(GoldMinerAsset asset, byte[] bytes)
        {
            VerifyBytes(asset, bytes);
            Require(bytes.Length >= 33 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 && bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10 && bytes[12] == 73 && bytes[13] == 72 && bytes[14] == 68 && bytes[15] == 82 && bytes[24] == 8 && bytes[25] == 6, "Source must be the original 8-bit RGBA PNG: " + asset.id);
        }
        private static int ReadBigEndian(byte[] data, int offset) { return data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]; }
        internal static void EnsureFolderMeta(string root, string relative)
        {
            var path = Path.Combine(root, relative) + ".meta";
            if (!File.Exists(path)) File.WriteAllText(path, "fileFormatVersion: 2\nguid: " + StableGuid(relative) + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", new UTF8Encoding(false));
        }
        private static string N(double v) { return v.ToString("0.########", CultureInfo.InvariantCulture); }
        private static string V(double[] v) { return "{x: " + N(v[0]) + ", y: " + N(v[1]) + "}"; }
        public static string TextureMeta(GoldMinerAsset a, GoldMinerProfile p, string guid, string provenance = "GoldMiner-production-v1")
        {
            var b = new StringBuilder();
            b.Append("fileFormatVersion: 2\nguid: ").Append(guid).Append("\nTextureImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    mipMapMode: 0\n    enableMipMap: 0\n    sRGBTexture: 1\n    linearTexture: 0\n    fadeOut: 0\n    borderMipMap: 0\n    mipMapsPreserveCoverage: 0\n    alphaTestReferenceValue: 0.5\n    mipMapFadeDistanceStart: 1\n    mipMapFadeDistanceEnd: 3\n  isReadable: 0\n  streamingMipmaps: 0\n  streamingMipmapsPriority: 0\n  vTOnly: 0\n  ignoreMipmapLimit: 0\n  grayScaleToAlpha: 0\n  generateCubemap: 6\n  textureFormat: 1\n  maxTextureSize: ").Append(p.max_texture_size);
            b.Append("\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n  nPOTScale: 0\n  lightmap: 0\n  compressionQuality: 100\n  spriteMode: ").Append(a.slices.Length == 0 ? 1 : 2);
            b.Append("\n  spriteExtrude: 1\n  spriteMeshType: 0\n  alignment: 9\n  spritePivot: ").Append(V(a.planned_single_pivot_normalized_bottom_left));
            b.Append("\n  spritePixelsToUnits: 100\n  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 0\n  alphaUsage: ").Append(a.source_has_transparency ? 1 : 0);
            b.Append("\n  alphaIsTransparency: ").Append(a.source_has_transparency ? 1 : 0).Append("\n  spriteTessellationDetail: -1\n  textureType: 8\n  textureShape: 1\n  singleChannelComponent: 0\n  flipbookRows: 1\n  flipbookColumns: 1\n  swizzle: 50462976\n  platformSettings:\n");
            foreach (var platform in new[] { "DefaultTexturePlatform", "Android" })
            {
                b.Append("  - serializedVersion: 4\n    buildTarget: ").Append(platform).Append("\n    maxTextureSize: ").Append(p.max_texture_size).Append("\n    resizeAlgorithm: 0\n    textureFormat: ").Append(platform == "Android" ? (a.source_has_transparency ? 47 : 45) : -1);
                b.Append("\n    textureCompression: 2\n    compressionQuality: 100\n    crunchedCompression: 0\n    allowsAlphaSplitting: 0\n    overridden: ").Append(platform == "Android" ? 1 : 0).Append("\n    ignorePlatformSupport: 0\n    androidETC2FallbackOverride: 0\n    forceMaximumCompressionQuality_BC6H_BC7: 0\n");
            }
            b.Append("  spriteSheet:\n    serializedVersion: 2\n    sprites:");
            if (a.slices.Length == 0) b.Append(" []");
            b.Append('\n');
            foreach (var s in a.slices)
            {
                var r = s.unity_rect_xywh_bottom_left; var border = s.planned_border_left_bottom_right_top_source_px;
                b.Append("    - serializedVersion: 2\n      name: ").Append(s.name).Append("\n      rect:\n        serializedVersion: 2\n        x: ").Append(r[0]).Append("\n        y: ").Append(r[1]).Append("\n        width: ").Append(r[2]).Append("\n        height: ").Append(r[3]);
                b.Append("\n      alignment: 9\n      pivot: ").Append(V(s.planned_pivot_normalized_bottom_left)).Append("\n      border: {x: ").Append(border[0]).Append(", y: ").Append(border[1]).Append(", z: ").Append(border[2]).Append(", w: ").Append(border[3]).Append("}\n      customData: \n      outline: []\n      physicsShape: []\n      tessellationDetail: -1\n      bones: []\n      spriteID: ").Append(StableGuid(a.target_path + "#" + s.name)).Append("\n      internalID: ").Append(SliceId(a.target_path, s.name)).Append("\n      vertices: []\n      indices: \n      edges: []\n      weights: []\n");
            }
            b.Append("    outline: []\n    customData: \n    physicsShape: []\n    bones: []\n    spriteID: ").Append(StableGuid(a.target_path + "#single")).Append("\n    internalID: 0\n    vertices: []\n    indices: \n    edges: []\n    weights: []\n    secondaryTextures: []\n    spriteCustomMetadata:\n      entries: []\n    nameFileIdTable:");
            if (a.slices.Length == 0) b.Append(" {}"); b.Append('\n');
            foreach (var s in a.slices) b.Append("      ").Append(s.name).Append(": ").Append(SliceId(a.target_path, s.name)).Append('\n');
            b.Append("  mipmapLimitGroupName: \n  pSDRemoveMatte: 0\n  userData: ").Append(provenance).Append("\n  assetBundleName: \n  assetBundleVariant: \n");
            return b.ToString();
        }
    }

    // A separate, closed supplement keeps the original 44-image/43-slice contract unchanged.
    public static class GoldMinerAdditionalAssetPlan
    {
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        public static void Validate(GoldMinerAdditionalAssetManifest manifest)
        {
            Require(manifest != null && manifest.schema_version == 2 && manifest.assets != null && manifest.assets.Length == 3, "Expected Gold Miner v2 supplement with exactly three assets");
            var profile = manifest.import_profile;
            Require(profile != null && (profile.max_texture_size == 512 || profile.max_texture_size == 1024) && profile.android_compression_quality == 100, "Invalid Gold Miner supplemental import profile");
            var remaining = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "ready-thumbsup-miner-v2", "characters" },
                { "result-failed-miner-sign-v2", "characters" },
                { "gold-miner-wordmark-v2", "ui" }
            };
            foreach (var a in manifest.assets)
            {
                string group;
                Require(a != null && a.id != null && remaining.TryGetValue(a.id, out group) && a.group == group && remaining.Remove(a.id), "Unknown/duplicate Gold Miner supplemental asset or group");
                var relative = a.group + "/" + a.id + ".png";
                Require(a.target_path == "Assets/Resources/GoldMiner/" + relative && a.resource_key == "GoldMiner/" + a.group + "/" + a.id, "Invalid Gold Miner supplemental target: " + a.id);
                Require(a.source_path == "artifacts/gold-miner-ui-v2/" + relative && string.IsNullOrEmpty(a.source_archive_member), "Invalid Gold Miner supplemental source: " + a.id);
                Require(a.source_sha256 != null && Regex.IsMatch(a.source_sha256, "^[a-f0-9]{64}$") && a.source_bytes > 0, "Invalid supplemental source hash/length: " + a.id);
                Require(a.source_size_px != null && a.source_size_px.Length == 2 && a.source_size_px[0] > 0 && a.source_size_px[1] > 0, "Invalid supplemental source dimensions: " + a.id);
                Require(a.import_profile == "standalone" && a.sprite_mode == "Single" && a.slices != null && a.slices.Length == 0, "Supplemental artwork must be a standalone Single sprite: " + a.id);
                Require(a.source_has_transparency && a.alpha_is_transparency && a.alpha_source == "FromInput" && a.android_format == "ETC2_RGBA8", "Supplemental artwork requires ETC2 RGBA alpha: " + a.id);
                var pivot = a.planned_single_pivot_normalized_bottom_left;
                Require(pivot != null && pivot.Length == 2 && pivot[0] >= 0 && pivot[0] <= 1 && pivot[1] >= 0 && pivot[1] <= 1, "Invalid supplemental pivot: " + a.id);
            }
        }
        public static void PrepareFiles(string destinationRoot, GoldMinerAdditionalAssetManifest manifest, string sourceRoot = null)
        {
            Validate(manifest);
            PrepareValidatedFiles(destinationRoot, manifest, sourceRoot, "GoldMiner-production-v2-additions", GoldMinerAssetPlan.VerifyBytes);
        }
        internal static void PrepareValidatedFiles(string destinationRoot, GoldMinerAdditionalAssetManifest manifest, string sourceRoot, string provenance, Action<GoldMinerAsset, byte[]> verifyBytes)
        {
            sourceRoot = sourceRoot ?? destinationRoot;
            var pending = new Dictionary<string, byte[]>();
            var metadata = new Dictionary<string, string>();
            // Resolve every byte and existing GUID before writing anything. Checked-in targets
            // suffice for a fresh checkout; ignored artwork artifacts are needed only for a missing PNG.
            foreach (var a in manifest.assets)
            {
                var target = Path.Combine(destinationRoot, a.target_path);
                if (File.Exists(target)) verifyBytes(a, File.ReadAllBytes(target));
                else
                {
                    var source = Path.Combine(sourceRoot, a.source_path);
                    Require(File.Exists(source), "Missing Gold Miner supplemental source and target: " + a.source_path);
                    var bytes = File.ReadAllBytes(source); verifyBytes(a, bytes);
                    pending.Add(a.target_path, bytes);
                }
                var metaPath = target + ".meta"; var guid = GoldMinerAssetPlan.StableGuid(a.target_path);
                if (File.Exists(metaPath))
                {
                    var matches = Regex.Matches(File.ReadAllText(metaPath), @"(?m)^guid: ([a-f0-9]{32})\r?$");
                    Require(matches.Count == 1, "Invalid existing supplemental asset GUID: " + metaPath);
                    guid = matches[0].Groups[1].Value;
                }
                metadata.Add(a.target_path, GoldMinerAssetPlan.TextureMeta(a, manifest.import_profile, guid, provenance));
            }
            foreach (var a in manifest.assets)
            {
                var target = Path.Combine(destinationRoot, a.target_path);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                GoldMinerAssetPlan.EnsureFolderMeta(destinationRoot, "Assets/Resources/GoldMiner");
                GoldMinerAssetPlan.EnsureFolderMeta(destinationRoot, "Assets/Resources/GoldMiner/" + a.group);
                byte[] bytes; if (pending.TryGetValue(a.target_path, out bytes)) File.WriteAllBytes(target, bytes);
                var metaPath = target + ".meta";
                if (!File.Exists(metaPath) || File.ReadAllText(metaPath) != metadata[a.target_path]) File.WriteAllText(metaPath, metadata[a.target_path], new UTF8Encoding(false));
            }
        }
    }

    // Whole captured-load sprites have a closed v3 contract; v1/v2 retain their original whitelists.
    public static class GoldMinerCaughtAssetPlan
    {
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        public static void Validate(GoldMinerAdditionalAssetManifest manifest)
        {
            Require(manifest != null && manifest.schema_version == 3 && manifest.assets != null && manifest.assets.Length == 10, "Expected Gold Miner v3 with exactly ten captured/running sprites");
            Require(manifest.import_profile != null && manifest.import_profile.max_texture_size == 512 && manifest.import_profile.android_compression_quality == 100, "Caught artwork requires the 512 texture profile");
            var remaining = new HashSet<string>(StringComparer.Ordinal)
            {
                "caught-gold-small", "caught-gold-medium", "caught-gold-large", "caught-rock-small", "caught-rock-large",
                "caught-diamond", "caught-mystery-bag", "caught-mole", "caught-diamond-mole", "mole-carrying-diamond-run"
            };
            foreach (var a in manifest.assets)
            {
                Require(a != null && a.id != null && remaining.Remove(a.id) && a.group == "characters", "Unknown/duplicate captured sprite or group");
                Require(a.target_path == "Assets/Resources/GoldMiner/characters/" + a.id + ".png" && a.resource_key == "GoldMiner/characters/" + a.id, "Invalid captured target mapping: " + a.id);
                Require(a.source_path == "artifacts/gold-miner-followup-20261028/caught-art/" + a.id + ".png" && string.IsNullOrEmpty(a.source_archive_member), "Invalid captured source mapping: " + a.id);
                Require(a.source_sha256 != null && Regex.IsMatch(a.source_sha256, "^[a-f0-9]{64}$") && a.source_bytes > 0, "Invalid captured source hash/length: " + a.id);
                Require(a.source_size_px != null && a.source_size_px.Length == 2 && a.source_size_px[0] > 0 && a.source_size_px[1] > 0, "Invalid captured dimensions: " + a.id);
                Require(a.source_mode == "RGBA" && a.source_alpha_range != null && a.source_alpha_range.Length == 2 && a.source_alpha_range[0] >= 0 && a.source_alpha_range[0] < a.source_alpha_range[1] && a.source_alpha_range[1] <= 255, "Captured source must retain RGBA transparency: " + a.id);
                Require(a.source_has_transparency && a.alpha_is_transparency && a.alpha_source == "FromInput" && a.android_format == "ETC2_RGBA8", "Captured artwork requires ETC2 RGBA alpha: " + a.id);
                Require(a.import_profile == "standalone" && a.sprite_mode == "Single" && a.slices != null && a.slices.Length == 0, "Captured artwork must be a whole Single sprite: " + a.id);
                var pivot = a.planned_single_pivot_normalized_bottom_left;
                Require(pivot != null && pivot.Length == 2 && pivot[0] >= 0 && pivot[0] <= 1 && pivot[1] >= 0 && pivot[1] <= 1, "Invalid captured pivot: " + a.id);
                var ring = a.suspension_ring_center_xy_top_left;
                if (a.id == "mole-carrying-diamond-run")
                    Require((ring == null || ring.Length == 0) && a.suspension_ring_outer_diameter_px == 0 && pivot[0] == .5 && pivot[1] == .5, "Running diamond mole has no suspension ring");
                else
                {
                    Require(ring != null && ring.Length == 2 && ring[0] >= 0 && ring[0] <= a.source_size_px[0] && ring[1] >= 0 && ring[1] <= a.source_size_px[1], "Invalid captured ring coordinates: " + a.id);
                    Require(a.suspension_ring_outer_diameter_px > 0 && a.suspension_ring_outer_diameter_px <= Math.Min(a.source_size_px[0], a.source_size_px[1]), "Invalid captured ring diameter: " + a.id);
                    Require(Math.Abs(pivot[0] - ring[0] / a.source_size_px[0]) < .000001 && Math.Abs(pivot[1] - (1 - ring[1] / a.source_size_px[1])) < .000001, "Captured pivot must match its measured suspension ring: " + a.id);
                }
            }
        }
        public static void PrepareFiles(string destinationRoot, GoldMinerAdditionalAssetManifest manifest, string sourceRoot = null)
        {
            Validate(manifest);
            GoldMinerAdditionalAssetPlan.PrepareValidatedFiles(destinationRoot, manifest, sourceRoot, "GoldMiner-production-v3-caught", GoldMinerAssetPlan.VerifyRgbaBytes);
        }
    }

    // Each frame registers the complete miner, connected arm, handle and winch as one sprite.
    public static class GoldMinerReelAssetPlan
    {
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        public static void Validate(GoldMinerAdditionalAssetManifest manifest)
        {
            Require(manifest != null && manifest.schema_version == 4 && manifest.assets != null && manifest.assets.Length == 2, "Expected Gold Miner v4 with exactly two miner reel atlases");
            Require(manifest.import_profile != null && manifest.import_profile.max_texture_size == 1024 && manifest.import_profile.android_compression_quality == 100, "Miner reel atlases require the 1024 texture profile");
            var remaining = new HashSet<string>(StringComparer.Ordinal) { "miner-p1-reel-v4", "miner-p2-reel-v4" };
            foreach (var a in manifest.assets)
            {
                Require(a != null && a.id != null && remaining.Remove(a.id) && a.group == "characters", "Unknown/duplicate miner reel atlas or group");
                Require(a.target_path == "Assets/Resources/GoldMiner/characters/" + a.id + ".png" && a.resource_key == "GoldMiner/characters/" + a.id, "Invalid miner reel target mapping: " + a.id);
                Require(a.source_path == "artifacts/gold-miner-followup-20261028/reel-art/" + a.id + ".png" && string.IsNullOrEmpty(a.source_archive_member), "Invalid miner reel source mapping: " + a.id);
                Require(a.source_sha256 != null && Regex.IsMatch(a.source_sha256, "^[a-f0-9]{64}$") && a.source_bytes > 0, "Invalid miner reel source hash/length: " + a.id);
                Require(a.source_size_px != null && a.source_size_px.Length == 2 && a.source_size_px[0] > 0 && a.source_size_px[1] > 0, "Invalid miner reel dimensions: " + a.id);
                Require(a.source_mode == "RGBA" && a.source_alpha_range != null && a.source_alpha_range.Length == 2 && a.source_alpha_range[0] >= 0 && a.source_alpha_range[0] < a.source_alpha_range[1] && a.source_alpha_range[1] <= 255, "Miner reel source must retain RGBA transparency: " + a.id);
                Require(a.source_has_transparency && a.alpha_is_transparency && a.alpha_source == "FromInput" && a.android_format == "ETC2_RGBA8", "Miner reel atlas requires ETC2 RGBA alpha: " + a.id);
                Require(a.import_profile == "sheet" && a.sprite_mode == "Multiple" && a.slices != null && a.slices.Length == 6, "Miner reel atlas requires six Multiple sprite frames: " + a.id);
                var pivot = a.planned_single_pivot_normalized_bottom_left;
                Require(pivot != null && pivot.Length == 2 && pivot[0] == .5 && pivot[1] == .5, "Miner reel atlas has a neutral unused single pivot: " + a.id);
                Require(a.generation_tool == "built-in image_gen" && !string.IsNullOrWhiteSpace(a.generation_source_path) && a.generation_source_path.EndsWith(".png", StringComparison.Ordinal) && a.generation_prompts != null && a.generation_prompts.Length > 0, "Miner reel original generated PNG provenance is required: " + a.id);
                foreach (var prompt in a.generation_prompts) Require(!string.IsNullOrWhiteSpace(prompt), "Miner reel generation prompt cannot be empty: " + a.id);
                var top = a.measured_alpha_top_range_in_frame_source_px;
                Require(a.registration_tolerance_source_px == 2 && a.registered_alpha_top_in_frame_source_px >= 0 && top != null && top.Length == 2 && top[0] >= a.registered_alpha_top_in_frame_source_px && top[1] >= top[0] && top[1] - a.registered_alpha_top_in_frame_source_px <= a.registration_tolerance_source_px, "Invalid measured miner reel hat-top registration tolerance: " + a.id);
                for (var index = 0; index < a.slices.Length; index++) ValidateFrame(a, index);
                ValidateRegistration(a, frame => frame.foot_anchor_xy_top_left_source_px, "foot");
                ValidateRegistration(a, frame => frame.axle_xy_top_left_source_px, "axle");
            }
        }
        private static void ValidateFrame(GoldMinerAsset atlas, int index)
        {
            var frame = atlas.slices[index];
            Require(frame != null && frame.name == "frame_" + index, "Miner reel frames must be ordered frame_0 through frame_5: " + atlas.id);
            var r = frame.rect_xywh_top_left; var unity = frame.unity_rect_xywh_bottom_left;
            Require(r != null && r.Length == 4 && unity != null && unity.Length == 4, "Invalid miner reel frame rectangle: " + atlas.id + "/" + frame.name);
            Require(r[0] >= 0 && r[1] >= 0 && r[2] > 0 && r[3] > 0 && (long)r[0] + r[2] <= atlas.source_size_px[0] && (long)r[1] + r[3] <= atlas.source_size_px[1], "Miner reel frame leaves source bounds: " + frame.name);
            // The runtime keeps one registered display size while switching poses; origins may vary.
            Require(index == 0 || (r[2] == atlas.slices[0].rect_xywh_top_left[2] && r[3] == atlas.slices[0].rect_xywh_top_left[3]), "Miner reel frames must have equal source width and height: " + atlas.id);
            Require(unity[0] == r[0] && unity[1] == atlas.source_size_px[1] - r[1] - r[3] && unity[2] == r[2] && unity[3] == r[3], "Incorrect miner reel source-to-Unity rectangle: " + frame.name);
            var border = frame.planned_border_left_bottom_right_top_source_px;
            Require(border != null && border.Length == 4 && border[0] == 0 && border[1] == 0 && border[2] == 0 && border[3] == 0, "Miner reel frames cannot use nine-slice borders: " + frame.name);
            ValidateAnchor(frame.foot_anchor_xy_top_left_source_px, r, "foot", frame.name);
            ValidateAnchor(frame.rope_exit_xy_top_left_source_px, r, "rope exit", frame.name);
            ValidateAnchor(frame.axle_xy_top_left_source_px, r, "axle", frame.name);
            Require(atlas.measured_alpha_top_range_in_frame_source_px[1] < r[3], "Miner reel hat top must lie inside its frame: " + frame.name);
            var rope = frame.rope_exit_xy_top_left_source_px; var pivot = frame.planned_pivot_normalized_bottom_left;
            Require(pivot != null && pivot.Length == 2 && Math.Abs(pivot[0] - (rope[0] - r[0]) / r[2]) < .000001 && Math.Abs(pivot[1] - (1 - (rope[1] - r[1]) / r[3])) < .000001, "Miner reel pivot must match rope endpoint: " + frame.name);
            for (var prior = 0; prior < index; prior++)
            {
                var other = atlas.slices[prior].rect_xywh_top_left;
                Require(r[0] + r[2] <= other[0] || other[0] + other[2] <= r[0] || r[1] + r[3] <= other[1] || other[1] + other[3] <= r[1], "Miner reel frames overlap: " + frame.name);
            }
        }
        private static void ValidateAnchor(double[] point, int[] rect, string kind, string frame)
        {
            Require(point != null && point.Length == 2 && point[0] >= rect[0] && point[0] <= rect[0] + rect[2] && point[1] >= rect[1] && point[1] <= rect[1] + rect[3], "Invalid miner reel " + kind + " anchor: " + frame);
        }
        private static void ValidateRegistration(GoldMinerAsset atlas, Func<GoldMinerSlice, double[]> anchor, string kind)
        {
            // Check supplied reference anchors; this does not measure sprite pixels or prove the motion.
            for (var axis = 0; axis < 2; axis++)
            {
                var min = double.PositiveInfinity; var max = double.NegativeInfinity;
                foreach (var frame in atlas.slices)
                {
                    var offset = anchor(frame)[axis] - frame.rope_exit_xy_top_left_source_px[axis];
                    min = Math.Min(min, offset); max = Math.Max(max, offset);
                }
                Require(max - min <= atlas.registration_tolerance_source_px + .000001, "Miner reel " + kind + " registration drift exceeds drawing tolerance: " + atlas.id);
            }
        }
        public static void PrepareFiles(string destinationRoot, GoldMinerAdditionalAssetManifest manifest, string sourceRoot = null)
        {
            Validate(manifest);
            GoldMinerAdditionalAssetPlan.PrepareValidatedFiles(destinationRoot, manifest, sourceRoot, "GoldMiner-production-v4-reel", GoldMinerAssetPlan.VerifyRgbaBytes);
        }
    }

#if UNITY_EDITOR
    public static class GoldMinerAssetImporter
    {
        [MenuItem("Xanadu/Gold Miner/Prepare Production Assets")]
        public static void Prepare()
        {
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var manifest = JsonUtility.FromJson<GoldMinerAssetManifest>(File.ReadAllText(Path.Combine(root, "docs/gold-miner/production-assets-v1.json")));
            var additions = JsonUtility.FromJson<GoldMinerAdditionalAssetManifest>(File.ReadAllText(Path.Combine(root, "docs/gold-miner/production-assets-v2-additions.json")));
            var caught = JsonUtility.FromJson<GoldMinerAdditionalAssetManifest>(File.ReadAllText(Path.Combine(root, "docs/gold-miner/production-assets-v3-caught.json")));
            var reel = JsonUtility.FromJson<GoldMinerAdditionalAssetManifest>(File.ReadAllText(Path.Combine(root, "docs/gold-miner/production-assets-v4-reel.json")));
            GoldMinerAssetPlan.Validate(manifest);
            GoldMinerAdditionalAssetPlan.Validate(additions);
            GoldMinerCaughtAssetPlan.Validate(caught);
            GoldMinerReelAssetPlan.Validate(reel);
            GoldMinerAssetPlan.PrepareFiles(root, manifest);
            GoldMinerAdditionalAssetPlan.PrepareFiles(root, additions);
            GoldMinerCaughtAssetPlan.PrepareFiles(root, caught);
            GoldMinerReelAssetPlan.PrepareFiles(root, reel);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImports(manifest.assets, asset => manifest.import_profiles.Get(asset.import_profile));
            ConfigureImports(additions.assets, asset => additions.import_profile);
            ConfigureImports(caught.assets, asset => caught.import_profile);
            ConfigureImports(reel.assets, asset => reel.import_profile);
            ValidateImported(manifest.assets, asset => manifest.import_profiles.Get(asset.import_profile));
            ValidateImported(additions.assets, asset => additions.import_profile);
            ValidateImported(caught.assets, asset => caught.import_profile);
            ValidateImported(reel.assets, asset => reel.import_profile);
            Debug.Log("GOLD_MINER_ASSET_IMPORT_PASS: 44 original PNG; 43 source-pixel rectangles; 3 v2 supplemental sprites; 10 v3 captured/running sprites; 2 v4 miner reel atlases with 12 frames; ETC2 Android overrides; stable GUIDs");
        }
        private static void ConfigureImports(GoldMinerAsset[] assets, Func<GoldMinerAsset, GoldMinerProfile> getProfile)
        {
            foreach (var asset in assets)
            {
                var importer = AssetImporter.GetAtPath(asset.target_path) as TextureImporter;
                if (importer == null) throw new InvalidDataException("Missing TextureImporter: " + asset.target_path);
                var profile = getProfile(asset);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = asset.slices.Length > 0 ? SpriteImportMode.Multiple : SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 1;
                importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.isReadable = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = profile.max_texture_size; importer.compressionQuality = 100;
                importer.crunchedCompression = false;
                importer.alphaSource = asset.source_has_transparency ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = asset.source_has_transparency;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; settings.spriteExtrude = 1;
                settings.spriteGenerateFallbackPhysicsShape = false; importer.SetTextureSettings(settings);
                importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
                {
                    name = "Android", overridden = true, maxTextureSize = profile.max_texture_size,
                    format = asset.source_has_transparency ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC2_RGB4,
                    compressionQuality = 100, crunchedCompression = false, textureCompression = TextureImporterCompression.CompressedHQ
                });
                importer.SaveAndReimport();
            }
        }
        private static void ValidateImported(GoldMinerAsset[] assets, Func<GoldMinerAsset, GoldMinerProfile> getProfile)
        {
            foreach (var a in assets)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(a.target_path);
                var sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var item in AssetDatabase.LoadAllAssetsAtPath(a.target_path)) if (item is Sprite) sprites.Add(item.name, (Sprite)item);
                if (sprites.Count != Math.Max(1, a.slices.Length)) throw new InvalidDataException("Imported sprite count mismatch: " + a.id);
                var serialized = new SerializedObject(importer);
                var rects = serialized.FindProperty("m_SpriteSheet.m_Sprites");
                // Unity 6 materializes a full-source internal rectangle even for Single mode.
                // Authored atlas rectangles remain exact; a generated Single rectangle is not an extra slice.
                if (rects == null || rects.arraySize != Math.Max(1, a.slices.Length))
                {
                    throw new InvalidDataException("Serialized source rect count mismatch: " + a.id);
                }
                if (a.slices.Length == 0)
                {
                    var sourceRect = rects.GetArrayElementAtIndex(0).FindPropertyRelative("m_Rect").rectValue;
                    if (importer.spriteImportMode != SpriteImportMode.Single || sourceRect != new Rect(0, 0, a.source_size_px[0], a.source_size_px[1]))
                        throw new InvalidDataException("Imported single source bounds mismatch: " + a.id);
                    foreach (var sprite in sprites.Values)
                        if (Math.Abs(sprite.pivot.x / sprite.rect.width - a.planned_single_pivot_normalized_bottom_left[0]) > .002 ||
                            Math.Abs(sprite.pivot.y / sprite.rect.height - a.planned_single_pivot_normalized_bottom_left[1]) > .002)
                            throw new InvalidDataException("Imported single pivot mismatch: " + a.id);
                }
                for (var i = 0; i < a.slices.Length; i++)
                {
                    var s = a.slices[i]; var data = rects.GetArrayElementAtIndex(i); var r = s.unity_rect_xywh_bottom_left;
                    if (data.FindPropertyRelative("m_Name").stringValue != s.name || data.FindPropertyRelative("m_Rect").rectValue != new Rect(r[0], r[1], r[2], r[3]) || !sprites.ContainsKey(s.name)) throw new InvalidDataException("Imported source rect/name mismatch: " + a.id + "/" + s.name);
                    var sprite = sprites[s.name]; var expected = s.planned_pivot_normalized_bottom_left;
                    if (Math.Abs(sprite.pivot.x / sprite.rect.width - expected[0]) > .002 || Math.Abs(sprite.pivot.y / sprite.rect.height - expected[1]) > .002) throw new InvalidDataException("Imported pivot mismatch: " + a.id + "/" + s.name);
                    var border = s.planned_border_left_bottom_right_top_source_px;
                    var expectedBorder = new Vector4(border[0], border[1], border[2], border[3]);
                    if (data.FindPropertyRelative("m_Border").vector4Value != expectedBorder) throw new InvalidDataException("Imported source border mismatch: " + a.id + "/" + s.name);
                }
                var platform = importer.GetPlatformTextureSettings("Android");
                if (!platform.overridden || platform.maxTextureSize != getProfile(a).max_texture_size || platform.format != (a.source_has_transparency ? TextureImporterFormat.ETC2_RGBA8 : TextureImporterFormat.ETC2_RGB4)) throw new InvalidDataException("Android importer mismatch: " + a.id);
                if (importer.mipmapEnabled || importer.isReadable || platform.crunchedCompression || platform.compressionQuality != 100)
                    throw new InvalidDataException("Sprite memory/compression profile mismatch: " + a.id);
            }
        }
    }
#endif
}
