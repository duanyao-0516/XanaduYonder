using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace XanaduYonder.EditorTools
{
    // Unity's normal sprite import/packing pipeline preserves the approved pixels
    // while shipping only four selected rectangles. The original is not a Resource.
    public static class FlappyAssetPreparation
    {
        private const string Source = "Assets/ArtSources/FlappyBird/Peleila.png";
        private const string Destination = "Assets/Resources/FlappyBird/Atlas.spriteatlas";
        public static void Ensure()
        {
            EditorSettings.spritePackerMode = SpritePackerMode.AlwaysOnAtlas;
            var importer = (TextureImporter)AssetImporter.GetAtPath(Source);
            if (importer == null) throw new InvalidOperationException("Flappy source atlas is missing.");
            var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(Destination);
            if (atlas == null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.mipmapEnabled = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
#pragma warning disable 618
                importer.spritesheet = new[] {
                    Region("BirdRest", 0, 0, 124, 112), Region("BirdWing", 128, 0, 124, 112),
                    Region("PipeCap", 528, 51, 97, 36), Region("Ground", 514, 594, 124, 47)
                };
#pragma warning restore 618
                importer.SaveAndReimport();
                var sprites = AssetDatabase.LoadAllAssetsAtPath(Source).OfType<Sprite>().ToArray();
                if (sprites.Length != 4) throw new InvalidOperationException("Flappy sprite slicing did not produce four views.");
                atlas = new SpriteAtlas();
                atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 });

                atlas.SetPlatformSettings(new TextureImporterPlatformSettings { name = "DefaultTexturePlatform", maxTextureSize = 512, textureCompression = TextureImporterCompression.Uncompressed });
                atlas.Add(sprites);
                atlas.SetIncludeInBuild(true);
                AssetDatabase.CreateAsset(atlas, Destination);
                AssetDatabase.SaveAssets();
            }
            // Start from native defaults; a zero-initialized settings struct also
            // zeros readonly texture-format/max-size fields used by the packer.
            var defaults = new SpriteAtlas();
            var textures = defaults.GetTextureSettings();
            UnityEngine.Object.DestroyImmediate(defaults);
            textures.readable = false; textures.generateMipMaps = false;
            textures.sRGB = true; textures.filterMode = FilterMode.Bilinear;
            atlas.SetTextureSettings(textures);
            var platform = atlas.GetPlatformSettings("DefaultTexturePlatform");
            platform.maxTextureSize = 512; platform.format = TextureImporterFormat.RGBA32;
            platform.textureCompression = TextureImporterCompression.Uncompressed;
            atlas.SetPlatformSettings(platform);
            EditorUtility.SetDirty(atlas); AssetDatabase.SaveAssets();
            SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        }
        private static SpriteMetaData Region(string name, int x, int y, int width, int height)
        {
            return new SpriteMetaData { name = name, rect = new Rect(x, 1123 - y - height, width, height), alignment = (int)SpriteAlignment.Center, pivot = new Vector2(.5f, .5f) };
        }
    }
}
