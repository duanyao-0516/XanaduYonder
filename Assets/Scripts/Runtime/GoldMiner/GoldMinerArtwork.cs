using System;
using System.Collections.Generic;
using UnityEngine;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>
    /// Page-owned, lazy cache. Destroy/clear consuming Images and SpriteRenderers before Dispose.
    /// Dispose releases references and requests Unity's safe unused-asset collection at the page
    /// boundary. It never forcibly unloads a texture still used by another page or transition.
    /// </summary>
    internal sealed class GoldMinerArtwork : IDisposable
    {
        private readonly Dictionary<string, Dictionary<string, Sprite>> cache = new Dictionary<string, Dictionary<string, Sprite>>(StringComparer.Ordinal);
        private bool disposed;
        public Sprite Get(string relativeResourcePathWithoutExtension, string sliceName = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(GoldMinerArtwork));
            var key = Normalize(relativeResourcePathWithoutExtension);
            Dictionary<string, Sprite> sprites;
            if (!cache.TryGetValue(key, out sprites))
            {
                // LoadAll is limited to this one texture, never GoldMiner/ or the whole Resources tree.
                var loaded = Resources.LoadAll<Sprite>(key);
                if (loaded.Length == 0) throw new InvalidOperationException("Gold Miner artwork is missing: " + key);
                sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
                foreach (var sprite in loaded) sprites.Add(sprite.name, sprite);
                cache.Add(key, sprites);
            }
            if (sliceName == null && sprites.Count == 1)
                foreach (var sprite in sprites.Values) return sprite;
            Sprite result;
            if (sliceName != null && sprites.TryGetValue(sliceName, out result)) return result;
            throw new ArgumentException("A named slice is required for " + key + ": " + (sliceName ?? "<none>"), nameof(sliceName));
        }
        public Sprite Sprite(string relativeResourcePathWithoutExtension, string sliceName = null) { return Get(relativeResourcePathWithoutExtension, sliceName); }
        private static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A production artwork path is required", nameof(path));
            var key = path.StartsWith("GoldMiner/", StringComparison.Ordinal) ? path : "GoldMiner/" + path;
            if (key.Contains("..") || key.Contains("\\") || key.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Use a relative Resources key without extension", nameof(path));
            var segments = key.Split('/');
            if (segments.Length != 3 || !(segments[1] == "characters" || segments[1] == "objects" || segments[1] == "backgrounds" || segments[1] == "ui" || segments[1] == "effects")) throw new ArgumentException("Not a production Gold Miner artwork key", nameof(path));
            return key;
        }
        /// <summary>
        /// Source alpha bounds in normalized bottom-left coordinates for unsliced textures.
        /// Source slices are already cropped to authored rectangles, so they use the full rect.
        /// Useful for aligning visible object size without enabling TextureImporter Read/Write.
        /// This is a presentation bound; gameplay collision sizes always come from the model.
        /// </summary>
        public static Rect VisibleBoundsNormalized(string relativeResourcePathWithoutExtension)
        {
            var key = Normalize(relativeResourcePathWithoutExtension);
            Rect result;
            return VisibleBounds.TryGetValue(key, out result) ? result : new Rect(0, 0, 1, 1);
        }
        public void ReleaseView()
        {
            if (disposed) throw new ObjectDisposedException(nameof(GoldMinerArtwork));
            cache.Clear();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; cache.Clear();
            Resources.UnloadUnusedAssets();
        }
        private static readonly Dictionary<string, Rect> VisibleBounds = new Dictionary<string, Rect>(StringComparer.Ordinal)
        {
            {"GoldMiner/objects/item_diamond_polish", new Rect(0.159489633f, 0.0877192982f, 0.680223285f, 0.799043062f)},
            {"GoldMiner/objects/item_dynamite", new Rect(0.210526316f, 0.133971292f, 0.603668262f, 0.698564593f)},
            {"GoldMiner/objects/item_lucky_clover", new Rect(0.159489633f, 0.0948963317f, 0.715311005f, 0.776714514f)},
            {"GoldMiner/objects/item_rock_book", new Rect(0.0574162679f, 0.0255183413f, 0.903508772f, 0.885964912f)},
            {"GoldMiner/objects/item_strength", new Rect(0.140350877f, 0.0582137161f, 0.770334928f, 0.880382775f)},
            {"GoldMiner/objects/ore_diamond", new Rect(0.128389155f, 0.161881978f, 0.744019139f, 0.649920255f)},
            {"GoldMiner/objects/ore_gold_large", new Rect(0.0470494418f, 0.0893141946f, 0.909888357f, 0.804625199f)},
            {"GoldMiner/objects/ore_gold_medium", new Rect(0.128389155f, 0.0653907496f, 0.747208931f, 0.860446571f)},
            {"GoldMiner/objects/ore_gold_small", new Rect(0.124401914f, 0.14354067f, 0.755980861f, 0.702551834f)},
            {"GoldMiner/objects/ore_mystery_bag", new Rect(0.0789473684f, 0.0805422648f, 0.841307815f, 0.833333333f)},
            {"GoldMiner/objects/ore_rock_large", new Rect(0.0661881978f, 0.169856459f, 0.87400319f, 0.648325359f)},
            {"GoldMiner/objects/ore_rock_small", new Rect(0.133173844f, 0.203349282f, 0.738437002f, 0.550239234f)},
            {"GoldMiner/objects/ui_gold_coin", new Rect(0.0909090909f, 0.0733652313f, 0.822169059f, 0.832535885f)},
            {"GoldMiner/backgrounds/bg-coop", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/bg-mode-select", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/bg-shop-landscape", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/bg-shop-portrait", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/bg-solo", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/layer-ground-edge", new Rect(0.00920810313f, 0.284530387f, 0.982044199f, 0.337016575f)},
            {"GoldMiner/backgrounds/layer-sky", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/lobby-cover", new Rect(0f, 0f, 1f, 1f)},
            {"GoldMiner/backgrounds/mode-coop-illustration", new Rect(0.0924464487f, 0.0541149944f, 0.8156708f, 0.852311161f)},
            {"GoldMiner/backgrounds/mode-solo-illustration", new Rect(0.0797448166f, 0.0494417863f, 0.838118022f, 0.86523126f)},
            {"GoldMiner/effects/fx_collect_sparkle", new Rect(0.203349282f, 0.196172249f, 0.621212121f, 0.605263158f)},
            {"GoldMiner/effects/fx_dust_puff", new Rect(0.0462519936f, 0.312599681f, 0.913875598f, 0.3261563f)},
            {"GoldMiner/effects/fx_explosion", new Rect(0.0622009569f, 0.0996810207f, 0.876395534f, 0.800637959f)},
        };
    }
}
