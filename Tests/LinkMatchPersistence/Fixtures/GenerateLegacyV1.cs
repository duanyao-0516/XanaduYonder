// Fixture producer compiled only against git e59247c's unmodified production sources.
using System;
using System.IO;
using XanaduYonder.Core.LinkMatch;
using XanaduYonder.Runtime.LinkMatch;
internal static class GenerateLegacyV1
{
    private static void Main(string[] args)
    {
        for (int variant = 0; variant < 2; variant++)
        {
            var p = new LinkMatchProfile();
            for (int mode = 0; mode < 2; mode++) for (int number = 1; number <= 3; number++)
            {
                var level = LinkMatchLevel.Get(number);
                var s = p.Start((LinkMatchMode)mode, level.Id, (uint)(number + 50 * mode + variant * 100));
                s.Enter(); s.Tick(number + 0.125 + variant); s.TryHint(); s.TryShuffle(); Solve(s); p.Capture(s);
                if (mode == 0 && number <= 2) continue;
                s = p.Start((LinkMatchMode)mode, level.Id, (uint)(400 + number + 50 * mode + variant * 100), true);
                s.Enter(); s.Tick(4 * number + 0.375 + variant); s.TryHint(); s.TryShuffle();
                var pair = s.TryHint(); if (pair != null) s.TryMatch(pair.First, pair.Second, out var path);
                p.Capture(s);
            }
            p.SelectedMode = variant == 0 ? LinkMatchMode.Challenge : LinkMatchMode.Relaxed;
            p.SelectedLevelId = variant == 0 ? "level-03" : "level-02";
            string json = LinkMatchSaveCodec.Encode(p.Export());
            if (!LinkMatchSaveCodec.TryDecode(json, out var decoded, out var reason)) throw new Exception(reason);
            File.WriteAllText(Path.Combine(args[0], variant == 0 ? "legacy-v1-primary.json" : "legacy-v1-backup.json"), json);
            Console.WriteLine("Original v1 codec verified variant " + variant + ": " + decoded.Slots.Length + " slots");
        }
    }
    private static void Solve(LinkMatchSession session)
    { while (!session.IsTerminal) { session.EnsureMove(); var pair = session.Game.FindMatch(); if (pair == null || !session.TryMatch(pair.First, pair.Second, out var path)) throw new Exception("Cannot finish fixture"); } }
}
