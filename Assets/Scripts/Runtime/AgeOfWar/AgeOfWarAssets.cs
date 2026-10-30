using System;
using System.Collections.Generic;
using UnityEngine;

namespace XanaduYonder.Runtime.AgeOfWar
{
    [Serializable] public sealed class AgeOfWarAssetManifest
    {
        public int schemaVersion, edition;
        public AgeOfWarStage stage;
        public AgeOfWarQuality quality;
        public AgeOfWarAtlas[] atlases;
        public AgeOfWarClip[] clips;
        public AgeOfWarSound[] sounds;
        public AgeOfWarFont[] fonts;
        public AgeOfWarScene[] scenes;
    }
    [Serializable] public sealed class AgeOfWarQuality { public string name; public int coverageGrid; public bool bitmapSmoothingAllowed; public float stagePixelScale = 1; }
    [Serializable] public sealed class AgeOfWarStage { public int width, height; public float fps, scale = 1; }
    [Serializable] public sealed class AgeOfWarAtlas { public string resource; public int width, height; }
    [Serializable] public sealed class AgeOfWarClip
    {
        public string key, action;
        public bool loop;
        public AgeOfWarAssetFrame[] frames;
        public AgeOfWarClipEvent[] events;
        public int DurationTicks { get { int count = 0; foreach (var frame in frames) count += frame.ticks; return count; } }
    }
    [Serializable] public sealed class AgeOfWarAssetFrame
    {
        public int atlas, x, y, w, h, sourceFrame, ticks = 1;
        public float originX, originY, scale = 1;
        public bool empty;
    }
    [Serializable] public sealed class AgeOfWarClipEvent { public int frame; public string type, value; }
    [Serializable] public sealed class AgeOfWarFont { public string key, resource, family, portableFallback; public bool device, bold, italic, hasLayout; public float ascent, descent, leading, unitsPerEm = 1024; }
    [Serializable] public sealed class AgeOfWarFilter { public string type, glowColor, shadowColor, dropShadowColor; public float[] matrix; public float blurX, blurY, strength, angle, distance; public int passes; public bool innerGlow, innerShadow, knockout, compositeSource; }
    [Serializable] public sealed class AgeOfWarTextField { public string name, variable, initial, font, color; public float x, y, w, h, size, letterSpacing, leftMargin, rightMargin, indent, leading; public int align, characterId; public bool outlined, bold, italic, wordWrap, multiline; public AgeOfWarFilter[] filters; }
    [Serializable] public sealed class AgeOfWarSound { public string key, resource; }
    [Serializable] public sealed class AgeOfWarScene
    {
        public string key;
        public int width, height;
        public AgeOfWarSceneLayer[] layers;
        public AgeOfWarHotspot[] hotspots;
        public AgeOfWarTextField[] texts;
    }
    [Serializable] public sealed class AgeOfWarSceneLayer
    {
        public string key, action = "idle", name, clock;
        public float x, y, scaleX = 1, scaleY = 1, skewX, skewY, rotation, alpha = 1;
        public int depth, frame, birthFrame;
        public int clipDepth = -1;
    }
    [Serializable] public sealed class AgeOfWarHotspot
    {
        public string action, name;
        public float x, y, w, h;
        public int id;
    }
    public struct AgeOfWarSpriteFrame
    {
        public Sprite Sprite;
        // Original Flash coordinates: origin is measured from cropped bitmap top left.
        public Vector2 Origin, Size;
        public int SourceFrame, DurationTicks;
        public bool Empty;
    }

    /// <summary>Owns the original edition's release textures/sprites. Missing actions stay missing.</summary>
    public sealed class AgeOfWarAssets : IDisposable
    {
        private readonly Dictionary<string, AgeOfWarClip> clips = new Dictionary<string, AgeOfWarClip>();
        private readonly Dictionary<string, AgeOfWarScene> scenes = new Dictionary<string, AgeOfWarScene>();
        private readonly Dictionary<string, string> soundPaths = new Dictionary<string, string>();
        private readonly Dictionary<AgeOfWarAssetFrame, Sprite> sprites = new Dictionary<AgeOfWarAssetFrame, Sprite>();
        private readonly Dictionary<int, Texture2D> textures = new Dictionary<int, Texture2D>();
        private readonly Dictionary<string, AgeOfWarFont> fontPaths = new Dictionary<string, AgeOfWarFont>();
        private readonly HashSet<Font> createdFonts = new HashSet<Font>();
        private readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        private readonly Dictionary<string, AudioClip> sounds = new Dictionary<string, AudioClip>();
        private readonly List<Sprite> retiredSprites = new List<Sprite>();
        private readonly List<Texture2D> retiredTextures = new List<Texture2D>();
        public int Revision { get; private set; }
        public string Quality { get { return Manifest.quality == null ? "High" : Manifest.quality.name; } }
        public AgeOfWarAssetManifest Manifest { get; private set; }
        public int Edition { get { return Manifest.edition; } }
        public AgeOfWarStage Stage { get { return Manifest.stage; } }
        private AgeOfWarAssets(AgeOfWarAssetManifest manifest)
        {
            Manifest = manifest;
            if (manifest == null || manifest.schemaVersion != 1 || manifest.clips == null || manifest.atlases == null)
                throw new InvalidOperationException("Invalid Age of War asset manifest.");
            foreach (var clip in manifest.clips) clips.Add(clip.key + "\n" + clip.action, clip);
            foreach (var scene in manifest.scenes ?? new AgeOfWarScene[0]) scenes.Add(scene.key, scene);
            foreach (var sound in manifest.sounds ?? new AgeOfWarSound[0]) soundPaths.Add(sound.key, sound.resource);
            foreach (var font in manifest.fonts ?? new AgeOfWarFont[0]) fontPaths.Add(font.key, font);
        }
        public static AgeOfWarAssets Load(int edition)
        {
            if (edition != 1 && edition != 2) throw new ArgumentOutOfRangeException("edition");
            var path = "AgeOfWar/" + (edition == 1 ? "One" : "Two") + "/manifest";
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing original asset manifest: " + path);
            var manifest = JsonUtility.FromJson<AgeOfWarAssetManifest>(asset.text);
            if (manifest.edition != edition) throw new InvalidOperationException("Original asset edition mismatch.");
            return new AgeOfWarAssets(manifest);
        }
        /// <summary>Rebind every visible Image synchronously before old textures are released.</summary>
        public bool SetQuality(string quality, Action rebind)
        {
            if (Edition != 2) throw new InvalidOperationException("Quality variants belong to Age of War Two.");
            if (rebind == null) throw new ArgumentNullException("rebind");
            string canonical;
            switch ((quality ?? "").ToLowerInvariant())
            {
                case "low": canonical = "Low"; break;
                case "medium": canonical = "Medium"; break;
                case "high": canonical = "High"; break;
                default: throw new ArgumentOutOfRangeException("quality");
            }
            if (Quality == canonical) return false;
            string path = "AgeOfWar/Two/" + (canonical == "High" ? "manifest" : "Quality/" + canonical + "/manifest");
            var asset = Resources.Load<TextAsset>(path);
            if (asset == null) throw new InvalidOperationException("Missing original quality manifest: " + path);
            var manifest = JsonUtility.FromJson<AgeOfWarAssetManifest>(asset.text);
            if (manifest == null || manifest.edition != 2 || manifest.schemaVersion != 1 || manifest.quality == null || manifest.quality.name != canonical || manifest.clips == null || manifest.atlases == null)
                throw new InvalidOperationException("Invalid original quality manifest: " + path);
            var nextClips = new Dictionary<string, AgeOfWarClip>();
            foreach (var clip in manifest.clips)
            {
                string identity = clip.key + "\n" + clip.action;
                AgeOfWarClip previous;
                if (!clips.TryGetValue(identity, out previous) || previous.DurationTicks != clip.DurationTicks)
                    throw new InvalidOperationException("Original quality changes animation identity: " + identity);
                nextClips.Add(identity, clip);
            }
            if (nextClips.Count != clips.Count) throw new InvalidOperationException("Original quality omits animations.");
            // Keep the previous images alive across the synchronous presenter rebind.
            retiredSprites.AddRange(sprites.Values); retiredTextures.AddRange(textures.Values);
            sprites.Clear(); textures.Clear(); clips.Clear();
            foreach (var entry in nextClips) clips.Add(entry.Key, entry.Value);
            Manifest = manifest; Revision++;
            // If the callback fails, retain both generations until Dispose; never invalidate
            // an Image whose rebinding did not finish.
            rebind();
            ReleaseRetired();
            return true;
        }
        private void ReleaseRetired()
        {
            foreach (var sprite in retiredSprites) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in retiredTextures) if (texture != null) Resources.UnloadAsset(texture);
            retiredSprites.Clear(); retiredTextures.Clear();
        }
        public AgeOfWarClip GetClip(string key, string action)
        {
            AgeOfWarClip clip;
            return clips.TryGetValue(key + "\n" + action, out clip) ? clip : null;
        }
        public AgeOfWarScene GetScene(string key)
        {
            AgeOfWarScene scene;
            return scenes.TryGetValue(key, out scene) ? scene : null;
        }
        public AgeOfWarFont GetFontDefinition(string key)
        {
            AgeOfWarFont font;
            return key != null && fontPaths.TryGetValue(key, out font) ? font : null;
        }
        public bool TryGetFrame(string key, string action, int tick, out AgeOfWarSpriteFrame result)
        {
            result = default(AgeOfWarSpriteFrame);
            var clip = GetClip(key, action);
            if (clip == null || clip.frames.Length == 0) return false;
            int duration = clip.DurationTicks;
            int time = clip.loop ? Math.Max(0, tick) % Math.Max(1, duration) : Math.Min(Math.Max(0, tick), duration - 1);
            AgeOfWarAssetFrame frame = clip.frames[clip.frames.Length - 1];
            foreach (var candidate in clip.frames)
            {
                if (time < candidate.ticks) { frame = candidate; break; }
                time -= candidate.ticks;
            }
            Sprite sprite;
            if (!sprites.TryGetValue(frame, out sprite))
            {
                Texture2D texture;
                var atlas = Manifest.atlases[frame.atlas];
                if (!textures.TryGetValue(frame.atlas, out texture))
                {
                    texture = Resources.Load<Texture2D>(atlas.resource);
                    if (texture == null) throw new InvalidOperationException("Missing original atlas: " + atlas.resource);
                    if (texture.width != atlas.width || texture.height != atlas.height)
                        throw new InvalidOperationException("Atlas import resized original pixels: " + atlas.resource);
                    textures.Add(frame.atlas, texture);
                }
                // Explicit Origin allows a Flash registration point outside a tightly cropped Sprite.
                sprite = Sprite.Create(texture, new Rect(frame.x, atlas.height - frame.y - frame.h, frame.w, frame.h), new Vector2(0, 1), frame.scale, 0, SpriteMeshType.FullRect);
                sprite.name = key + "/" + action + "/" + frame.sourceFrame;
                sprites.Add(frame, sprite);
            }
            result = new AgeOfWarSpriteFrame { Sprite = sprite, Origin = new Vector2(frame.originX, frame.originY), Size = new Vector2(frame.w / frame.scale, frame.h / frame.scale), SourceFrame = frame.sourceFrame + Math.Max(0, time), DurationTicks = frame.ticks, Empty = frame.empty };
            return true;
        }
        public Font LoadFont(string key)
        {
            Font font;
            if (fonts.TryGetValue(key, out font)) return font;
            AgeOfWarFont descriptor;
            if (!fontPaths.TryGetValue(key, out descriptor)) return null;
            if (descriptor.device)
            {
                string family = descriptor.family;
                string styled = family + (descriptor.bold ? " Bold" : "") + (descriptor.italic ? " Italic" : "");
                foreach (var installed in Font.GetOSInstalledFontNames())
                {
                    if (string.Equals(installed, family, StringComparison.OrdinalIgnoreCase) || string.Equals(installed, styled, StringComparison.OrdinalIgnoreCase))
                    { font = Font.CreateDynamicFontFromOSFont(installed, 16); createdFonts.Add(font); break; }
                }
                if (font == null && !string.IsNullOrEmpty(descriptor.portableFallback)) font = LoadFont(descriptor.portableFallback);
                // Device fonts have no glyphs in the original. The caller may provide its explicit host fallback.
                if (font == null) return null;
            }
            else
            {
                font = Resources.Load<Font>(descriptor.resource);
                if (font == null) throw new InvalidOperationException("Missing original font: " + descriptor.resource);
            }
            fonts.Add(key, font); return font;
        }
        public AudioClip LoadSound(string key)
        {
            AudioClip sound;
            if (sounds.TryGetValue(key, out sound)) return sound;
            string path;
            if (!soundPaths.TryGetValue(key, out path)) return null;
            sound = Resources.Load<AudioClip>(path);
            if (sound == null) throw new InvalidOperationException("Missing original audio: " + path);
            sounds.Add(key, sound); return sound;
        }
        public void Dispose()
        {
            ReleaseRetired();
            foreach (var sprite in sprites.Values) if (sprite != null) UnityEngine.Object.Destroy(sprite);
            foreach (var texture in textures.Values) if (texture != null) Resources.UnloadAsset(texture);
            foreach (var sound in sounds.Values) if (sound != null) Resources.UnloadAsset(sound);
            foreach (var font in new HashSet<Font>(fonts.Values))
            { if (font == null) continue; if (createdFonts.Contains(font)) UnityEngine.Object.Destroy(font); else Resources.UnloadAsset(font); }
            createdFonts.Clear();
            sprites.Clear(); textures.Clear(); sounds.Clear(); fonts.Clear();
        }
    }
}
