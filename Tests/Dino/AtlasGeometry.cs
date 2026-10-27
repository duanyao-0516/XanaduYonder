using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using XanaduYonder.Core.Dino;

// Independent, read-only check against the shipped indexed PNG. No Unity or third-party decoder.
internal static class AtlasGeometry
{
    internal static void Check()
    {
        string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Assets/Resources/Dino/Sprites.png"));
        byte[] file = File.ReadAllBytes(path), alpha = null;
        int width = 0, height = 0;
        var compressed = new MemoryStream();
        for (int offset = 8; offset + 12 <= file.Length;) {
            int count = Big(file, offset); string tag = System.Text.Encoding.ASCII.GetString(file, offset + 4, 4); int begin = offset + 8;
            if (tag == "IHDR") { width = Big(file, begin); height = Big(file, begin + 4); if (file[begin + 8] != 8 || file[begin + 9] != 3) throw new Exception("Expected indexed 8-bit atlas"); }
            if (tag == "tRNS") { alpha = new byte[count]; Array.Copy(file, begin, alpha, 0, count); }
            if (tag == "IDAT") compressed.Write(file, begin, count);
            offset += count + 12;
        }
        if (width != 2446 || height != 194 || alpha == null) throw new Exception("Unexpected atlas contract");
        compressed.Position = 0;
        byte[] raw;
        using (var output = new MemoryStream()) { using (var z = new ZLibStream(compressed, CompressionMode.Decompress)) z.CopyTo(output); raw = output.ToArray(); }
        byte[] pixels = new byte[width * height];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
            int a = x == 0 ? 0 : pixels[y * width + x - 1], b = y == 0 ? 0 : pixels[(y - 1) * width + x], c = x == 0 || y == 0 ? 0 : pixels[(y - 1) * width + x - 1];
            int filter = raw[y * (width + 1)], delta;
            switch (filter) { case 0: delta = 0; break; case 1: delta = a; break; case 2: delta = b; break; case 3: delta = (a + b) / 2; break;
                case 4: int p = a + b - c, da = Math.Abs(p - a), db = Math.Abs(p - b), dc = Math.Abs(p - c); delta = da <= db && da <= dc ? a : db <= dc ? b : c; break;
                default: throw new Exception("Unsupported PNG filter"); }
            pixels[y * width + x] = (byte)(raw[y * (width + 1) + x + 1] + delta);
        }
        CheckParts("Standing", new[] { 1678, 1854, 1942 }, 47, width, pixels, alpha);
        CheckParts("Duck", new[] { 2206, 2324 }, 47, width, pixels, alpha);
        CheckParts("Small", new[] { 446 }, 35, width, pixels, alpha);
        CheckParts("Large", new[] { 652 }, 50, width, pixels, alpha);
        CheckParts("Bird", new[] { 260, 352 }, 40, width, pixels, alpha);
        foreach (DinoObstacleKind kind in new[] { DinoObstacleKind.SmallCactus, DinoObstacleKind.LargeCactus }) {
            int stride = kind == DinoObstacleKind.SmallCactus ? 34 : 50, origin = kind == DinoObstacleKind.SmallCactus ? 446 : 652;
            int spriteHeight = kind == DinoObstacleKind.SmallCactus ? 35 : 50;
            for (int group = 2; group <= 3; group++) for (int plant = 0; plant < group; plant++)
                CheckParts(kind + " group " + group + " plant " + plant, DinoCollision.ObstacleParts(kind, group, plant),
                    new[] { origin + group * (group - 1) / 2 * stride + plant * stride }, spriteHeight, width, pixels, alpha);
        }
    }

    private static void CheckParts(string name, int[] origins, int spriteHeight, int width, byte[] pixels, byte[] alpha)
    {
        var boxes = (DinoRect[])typeof(DinoCollision).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        CheckParts(name, boxes, origins, spriteHeight, width, pixels, alpha);
    }

    private static void CheckParts(string name, DinoRect[] boxes, int[] origins, int spriteHeight, int width, byte[] pixels, byte[] alpha)
    {
        for (int i = 0; i < boxes.Length; i++) {
            DinoRect box = boxes[i]; double scale = DinoRules.SpriteScale;
            int left = (int)Math.Round(box.X / scale * 2), right = (int)Math.Round((box.X + box.Width) / scale * 2);
            int top = (int)Math.Round((spriteHeight - (box.Y + box.Height) / scale) * 2), bottom = (int)Math.Round((spriteHeight - box.Y / scale) * 2);
            int opaque = 0, area = (right - left) * (bottom - top);
            for (int y = top; y < bottom; y++) for (int x = left; x < right; x++) {
                foreach (int origin in origins) { int value = pixels[(2 + y) * width + origin + x]; if (value >= alpha.Length || alpha[value] != 0) { opaque++; break; } }
            }
            if (opaque < area * .70) throw new Exception(name + " part " + i + " occupies only " + (100.0 * opaque / area).ToString("F1") + "% opaque pixels across animation frames");
        }
    }
    private static int Big(byte[] bytes, int offset) { return bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]; }
}
