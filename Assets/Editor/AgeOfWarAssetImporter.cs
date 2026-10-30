using UnityEditor;
using UnityEngine;

namespace XanaduYonder.EditorTools
{
    /// <summary>Keep original stage pixels/alpha intact and make bounded atlases usable on Android.</summary>
    public sealed class AgeOfWarAssetImporter : AssetPostprocessor
    {
        private bool IsOriginalAsset { get { return assetPath.StartsWith("Assets/Resources/AgeOfWar/"); } }
        private void OnPreprocessTexture()
        {
            if (!IsOriginalAsset) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            var android = importer.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 4096;
            android.format = TextureImporterFormat.ETC2_RGBA8;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
        }
        private void OnPreprocessAudio()
        {
            if (!IsOriginalAsset) return;
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            var sample = importer.defaultSampleSettings;
            sample.preloadAudioData = false;
            sample.loadType = AudioClipLoadType.CompressedInMemory;
            sample.compressionFormat = AudioCompressionFormat.Vorbis;
            // WAVs include verified MP3 decodes and exact SWF latency/sample windows.
            // Keep their PCM samples intact instead of adding another lossy encode.
            if (assetPath.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
            {
                sample.loadType = AudioClipLoadType.DecompressOnLoad;
                sample.compressionFormat = AudioCompressionFormat.PCM;
            }
            sample.quality = 1f;
            sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = sample;
        }
    }
}
