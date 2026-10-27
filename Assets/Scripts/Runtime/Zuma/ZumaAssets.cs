using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    [Serializable] public sealed class ZumaMapAsset
    {
        public string id, displayName, level, background, overlay, thumbnail;
        public int pointCount, hiddenSegments, prioritySegments;
    }
    [Serializable] public sealed class ZumaMusicAsset
    {
        public string id, resource, introResource, decoder, source, sourceSha256;
        public int order, subsong, restartOrder, restartRow, sampleRate, channels, introSamples, loopStartSample, loopEndSample;
        public bool loop;
    }
    [Serializable] public sealed class ZumaSourceSlice
    {
        public string id, texture, axis, sourceBoundsCorrection;
        public int x, y, width, height, clippedHeight, frames;
        public float pivotX, pivotY;
    }
    [Serializable] public sealed class ZumaAssetManifest
    {
        public int contentVersion;
        public string sourceUrl, sourceCommit, coordinateSystem, rights;
        public ZumaMapAsset[] maps;
        public string[] difficulties, fonts, sounds;
        public ZumaMusicAsset[] music;
        public ZumaSourceSlice[] slices;
    }
    [Serializable] public sealed class ZumaGlyph
    {
        public string character;
        public int x, y, width, height, advance, offsetX, offsetY;
    }
    [Serializable] public sealed class ZumaKerning { public string pair; public int amount; }
    [Serializable] public sealed class ZumaBitmapFont
    {
        public string id, texture;
        public int lineHeight, spaceAdvance;
        public ZumaGlyph[] glyphs;
        public ZumaKerning[] kerning;
    }

    /// <summary>Owns exactly the textures/clips acquired by one Zuma page. JSON is
    /// released immediately; map textures have an explicit shorter lifetime.</summary>
    public sealed class ZumaAssets : IDisposable
    {
        public const string SourceCommit = "28001e0e5061341d502116586626150027242cce";
        readonly Dictionary<string, UnityEngine.Object> loaded = new Dictionary<string, UnityEngine.Object>();
        readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        readonly Dictionary<string, ZumaBitmapFont> fonts = new Dictionary<string, ZumaBitmapFont>();
        bool disposed;
        public ZumaAssetManifest Manifest { get; private set; }

        public ZumaAssets()
        {
            Manifest = ReadJson<ZumaAssetManifest>("manifest");
            if (Manifest == null || Manifest.contentVersion != 1 || Manifest.sourceCommit != SourceCommit ||
                Manifest.maps == null || Manifest.maps.Length != 18 || Manifest.difficulties == null || Manifest.difficulties.Length != 4)
                throw new InvalidOperationException("Zuma resource manifest is missing or incompatible.");
        }

        T ReadJson<T>(string path)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ZumaAssets));
            var text = Resources.Load<TextAsset>("Zuma/" + path);
            if (text == null) throw new InvalidOperationException("Missing Zuma data: " + path);
            try { return JsonConvert.DeserializeObject<T>(text.text); }
            finally { Resources.UnloadAsset(text); }
        }

        T Acquire<T>(string path) where T : UnityEngine.Object
        {
            if (disposed) throw new ObjectDisposedException(nameof(ZumaAssets));
            UnityEngine.Object cached;
            var key = typeof(T).Name + ":" + path;
            if (loaded.TryGetValue(key, out cached)) return (T)cached;
            var asset = Resources.Load<T>("Zuma/" + path);
            if (asset == null) throw new InvalidOperationException("Missing Zuma resource: " + path);
            loaded.Add(key, asset);
            return asset;
        }

        public ZumaMapAsset Map(string id)
        {
            foreach (var map in Manifest.maps) if (map.id == id) return map;
            throw new ArgumentException("Unknown Zuma map: " + id, nameof(id));
        }
        public ZumaLevelDefinition LoadLevel(string id)
        {
            var map = Map(id);
            var level = ReadJson<ZumaLevelDefinition>(map.level);
            if (level == null || level.Id != id || level.Points == null || level.Points.Length != map.pointCount || level.Coins == null || level.Coins.Length == 0)
                throw new InvalidOperationException("Invalid Zuma level resource: " + id);
            return level;
        }
        public Texture2D Texture(string path) { return Acquire<Texture2D>(path); }
        public AudioClip LoadSound(string id)
        {
            if (Array.IndexOf(Manifest.sounds, id) < 0) throw new ArgumentException("Unknown Zuma sound: " + id);
            return Acquire<AudioClip>("Audio/Sounds/" + id);
        }
        public ZumaMusicAsset Music(string id)
        {
            foreach (var music in Manifest.music) if (music.id == id) return music;
            throw new ArgumentException("Unknown Zuma music: " + id);
        }
        public AudioClip LoadMusic(string id) { return Acquire<AudioClip>(Music(id).resource); }
        public AudioClip LoadMusicIntro(string id)
        {
            var path = Music(id).introResource;
            return string.IsNullOrEmpty(path) ? null : Acquire<AudioClip>(path);
        }
        public ZumaBitmapFont LoadFont(string id)
        {
            ZumaBitmapFont font;
            if (fonts.TryGetValue(id, out font)) return font;
            if (Array.IndexOf(Manifest.fonts,id) < 0) throw new ArgumentException("Unknown Zuma font: " + id);
            font = ReadJson<ZumaBitmapFont>("Fonts/"+id);
            if (font == null || font.id != id || font.glyphs == null || font.kerning == null)
                throw new InvalidOperationException("Invalid Zuma font: " + id);
            fonts.Add(id,font);
            return font;
        }
        public Sprite Sprite(string path, Rect topLeftSourceRect, Vector2? pivot = null)
        {
            var texture = Texture(path);
            var rect = topLeftSourceRect;
            rect.xMin = Mathf.Max(0,rect.xMin); rect.yMin = Mathf.Max(0,rect.yMin);
            rect.xMax = Mathf.Min(texture.width,rect.xMax); rect.yMax = Mathf.Min(texture.height,rect.yMax);
            if (rect.width <= 0 || rect.height <= 0) throw new ArgumentException("Zuma sprite rectangle is outside its atlas.");
            var center = pivot ?? new Vector2(.5f,.5f);
            var key = path+":"+rect+":"+center;
            Sprite sprite;
            if (sprites.TryGetValue(key,out sprite)) return sprite;
            var unityRect = new Rect(rect.x, texture.height-rect.yMax, rect.width, rect.height);
            sprite = UnityEngine.Sprite.Create(texture,unityRect,center,100,0,SpriteMeshType.FullRect);
            sprite.name = key; sprites.Add(key,sprite);
            return sprite;
        }
        public void ReleaseLevelResources(string id)
        {
            var map = Map(id);
            ReleaseTexture(map.background);
            if (!string.IsNullOrEmpty(map.overlay)) ReleaseTexture(map.overlay);
        }
        void ReleaseTexture(string path)
        {
            var remove = new List<string>();
            foreach (var pair in sprites) if (pair.Key.StartsWith(path+":",StringComparison.Ordinal)) { UnityEngine.Object.Destroy(pair.Value); remove.Add(pair.Key); }
            foreach (var key in remove) sprites.Remove(key);
            UnityEngine.Object asset;
            var resourceKey = nameof(Texture2D)+":"+path;
            if (loaded.TryGetValue(resourceKey,out asset)) { Resources.UnloadAsset(asset); loaded.Remove(resourceKey); }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var sprite in sprites.Values) UnityEngine.Object.Destroy(sprite);
            sprites.Clear(); fonts.Clear();
            foreach (var asset in loaded.Values) Resources.UnloadAsset(asset);
            loaded.Clear();
        }
    }
}
