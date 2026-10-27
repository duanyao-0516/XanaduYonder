using System;
using System.Collections.Generic;
using System.IO;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    // Page-owned plain data, independent of visual asset lifetime. Store validation can revisit
    // the disk primary/backup map after a different map has become the current dirty session.
    public sealed class ZumaLevelCache
    {
        private readonly Func<string, ZumaLevelDefinition> loader;
        private readonly Dictionary<string, ZumaLevelDefinition> levels =
            new Dictionary<string, ZumaLevelDefinition>(StringComparer.Ordinal);

        public ZumaLevelCache(Func<string, ZumaLevelDefinition> loader)
        {
            if (loader == null) throw new ArgumentNullException(nameof(loader));
            this.loader = loader;
        }

        // The returned definition is shared read-only content. ZumaGame makes its own copy.
        // Keeping it here avoids cloning thousands of path vertices on each save validation.
        public ZumaLevelDefinition Get(string mapId)
        {
            if (Array.IndexOf(ZumaSaveData.MapIds, mapId) < 0)
                throw new ArgumentException("Unknown Zuma map ID.", nameof(mapId));
            if (levels.TryGetValue(mapId, out var cached)) return cached;

            ZumaLevelDefinition definition;
            try
            {
                var loaded = loader(mapId);
                if (loaded == null || loaded.Id != mapId)
                    throw new InvalidOperationException("Zuma level content has the wrong identity.");
                if (!ZumaGame.ValidateLevel(loaded, out var reason))
                    throw new InvalidOperationException("Invalid Zuma level content: " + reason);
                // Detach from the asset provider; disposing/reusing it cannot invalidate saved maps.
                definition = loaded.Copy();
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                // Always wrap even FileNotFoundException: that refers to missing level content,
                // never to an absent save file, and therefore must not enable automatic overwrite.
                throw new IOException("Zuma level content is unavailable: " + mapId, exception);
            }
            levels.Add(mapId, definition);
            return definition;
        }
    }
}
