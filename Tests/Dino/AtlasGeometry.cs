using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using XanaduYonder.Core.Dino;

// Read the shipped indexed PNG directly. Opaque near-white outlines are not body pixels.
internal static class AtlasGeometry
{
    private struct Cell
    {
        internal double X, Y, Width;
        internal Cell(double x, double y, double width) { X = x; Y = y; Width = width; }
    }
    private sealed class Atlas
    {
        internal int Width, Height;
        internal byte[] Pixels, Alpha, Palette;
        internal bool IsBody(int x, int y) {
            int value = Pixels[y * Width + x];
            return (value >= Alpha.Length || Alpha[value] > 200) && Palette[value * 3] < 150 && Palette[value * 3 + 1] < 150 && Palette[value * 3 + 2] < 150;
        }
        internal bool IsWhite(int x, int y) {
            int value = Pixels[y * Width + x];
            return (value >= Alpha.Length || Alpha[value] > 200) && Palette[value * 3] > 240 && Palette[value * 3 + 1] > 240 && Palette[value * 3 + 2] > 240;
        }
    }
    private static Atlas atlas;
    private static readonly Dictionary<string, List<Cell>> Bodies = new Dictionary<string, List<Cell>>();

    internal static void Check()
    {
        Atlas image = GetAtlas();
        CheckCounts(image, 1678, 88, 94, 2960, 1664, "standing");
        CheckCounts(image, 446, 34, 70, 996, 496, "small cactus");
        // A known opaque border pixel must never satisfy the dark body predicate.
        int whites = 0;
        for (int y = 2; y < 96; y++) for (int x = 1678; x < 1766; x++)
            if (image.IsWhite(x, y)) { whites++; if (image.IsBody(x, y)) throw new Exception("White outline counted as body"); }
        if (whites == 0) throw new Exception("Atlas border fixture disappeared");
    }

    private static void CheckCounts(Atlas image, int origin, int width, int height, int expectedDark, int expectedWhite, string name)
    {
        int dark = 0, white = 0;
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) {
            if (image.IsBody(origin + x, 2 + y)) dark++;
            if (image.IsWhite(origin + x, 2 + y)) white++;
        }
        if (dark != expectedDark || white != expectedWhite) throw new Exception(name + " body/border contract changed: " + dark + "/" + white);
    }

    internal static double DarkBodyGap(string player, double playerX, double playerY, DinoObstacleKind kind, int group, double obstacleX, double obstacleY)
    {
        int playerOrigin = player == "Standing" ? 1678 : player == "Run1" ? 1854 : player == "Run2" ? 1942 : player == "Dead" ? 2118 : 2206;
        List<Cell> a = Body(player, playerOrigin, player == "Duck" ? 118 : 88, 94);
        int stride = kind == DinoObstacleKind.SmallCactus ? 34 : kind == DinoObstacleKind.LargeCactus ? 50 : 92;
        int origin = kind == DinoObstacleKind.SmallCactus ? 446 : kind == DinoObstacleKind.LargeCactus ? 652 : 260;
        int height = kind == DinoObstacleKind.SmallCactus ? 70 : kind == DinoObstacleKind.LargeCactus ? 100 : 80;
        List<Cell> b = Body(kind + ":" + group, origin + group * (group - 1) / 2 * stride, stride * group, height);
        double result = double.PositiveInfinity, cellHeight = DinoRules.SpriteScale / 2;
        foreach (Cell p in a) foreach (Cell o in b) {
            double dx = Math.Max(0, Math.Max(playerX + p.X - obstacleX - o.X - o.Width, obstacleX + o.X - playerX - p.X - p.Width));
            double dy = Math.Max(0, Math.Max(playerY + p.Y - obstacleY - o.Y - cellHeight, obstacleY + o.Y - playerY - p.Y - cellHeight));
            result = Math.Min(result, Math.Sqrt(dx * dx + dy * dy));
        }
        return result;
    }

    private static List<Cell> Body(string key, int origin, int width, int height)
    {
        List<Cell> result;
        if (Bodies.TryGetValue(key, out result)) return result;
        result = new List<Cell>(); Atlas image = GetAtlas(); double unit = DinoRules.SpriteScale / 2;
        for (int y = 0; y < height; y++) {
            int begin = -1;
            for (int x = 0; x <= width; x++) {
                bool on = x < width && image.IsBody(origin + x, 2 + y);
                if (on && begin < 0) begin = x;
                if (!on && begin >= 0) { result.Add(new Cell(begin * unit, (height - 1 - y) * unit, (x - begin) * unit)); begin = -1; }
            }
        }
        Bodies.Add(key, result); return result;
    }

    private static Atlas GetAtlas()
    {
        if (atlas != null) return atlas;
        string root = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(root, "Assets/Resources/Dino/Sprites.png"))) {
            DirectoryInfo parent = Directory.GetParent(root);
            if (parent == null) throw new Exception("Cannot find shipped Dino atlas from test output");
            root = parent.FullName;
        }
        byte[] file = File.ReadAllBytes(Path.Combine(root, "Assets/Resources/Dino/Sprites.png"));
        var result = new Atlas(); var compressed = new MemoryStream();
        for (int offset = 8; offset + 12 <= file.Length;) {
            int count = Big(file, offset), begin = offset + 8; string tag = System.Text.Encoding.ASCII.GetString(file, offset + 4, 4);
            if (tag == "IHDR") { result.Width = Big(file, begin); result.Height = Big(file, begin + 4); if (file[begin + 8] != 8 || file[begin + 9] != 3) throw new Exception("Expected indexed 8-bit atlas"); }
            if (tag == "PLTE") { result.Palette = new byte[count]; Array.Copy(file, begin, result.Palette, 0, count); }
            if (tag == "tRNS") { result.Alpha = new byte[count]; Array.Copy(file, begin, result.Alpha, 0, count); }
            if (tag == "IDAT") compressed.Write(file, begin, count);
            offset += count + 12;
        }
        if (result.Width != 2446 || result.Height != 194 || result.Alpha == null || result.Palette == null) throw new Exception("Unexpected atlas contract");
        compressed.Position = 0; byte[] raw;
        using (var output = new MemoryStream()) { using (var z = new ZLibStream(compressed, CompressionMode.Decompress)) z.CopyTo(output); raw = output.ToArray(); }
        int width = result.Width; byte[] pixels = new byte[width * result.Height];
        for (int y = 0; y < result.Height; y++) for (int x = 0; x < width; x++) {
            int a = x == 0 ? 0 : pixels[y * width + x - 1], b = y == 0 ? 0 : pixels[(y - 1) * width + x], c = x == 0 || y == 0 ? 0 : pixels[(y - 1) * width + x - 1];
            int filter = raw[y * (width + 1)], delta;
            switch (filter) { case 0: delta = 0; break; case 1: delta = a; break; case 2: delta = b; break; case 3: delta = (a + b) / 2; break;
                case 4: int p = a + b - c, da = Math.Abs(p - a), db = Math.Abs(p - b), dc = Math.Abs(p - c); delta = da <= db && da <= dc ? a : db <= dc ? b : c; break;
                default: throw new Exception("Unsupported PNG filter"); }
            pixels[y * width + x] = (byte)(raw[y * (width + 1) + x + 1] + delta);
        }
        result.Pixels = pixels; atlas = result; return result;
    }
    private static int Big(byte[] bytes, int offset) { return bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3]; }
}
