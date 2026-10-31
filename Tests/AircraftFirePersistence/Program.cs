using System;
using System.IO;
using System.Text;
using XanaduYonder.Runtime.AircraftFire;

internal static class Program
{
    private static int assertions;

    private static void Main()
    {
        CodecPreservesProfilesAndInt64();
        CodecRejectsMalformedContracts();
        RecordOnlyUpdatesPlayedProfile();
        DiskRecoveryMergesProfilesWithoutWrites();
        CorruptRecordsRequireNewGameplay();
        Console.WriteLine("Aircraft Fire persistence: " + assertions + " assertions passed.");
    }

    private static void CodecPreservesProfilesAndInt64()
    {
        var source = new AircraftFireSaveData { ClassicBestScore = long.MaxValue, CrazyBestScore = 12000, LevelBestScore = 7000, HighestStage = 7 };
        var encoded = AircraftFireSaveCodec.Encode(source);
        Check(AircraftFireSaveCodec.TryDecode(encoded, out var decoded, out var reason), reason);
        Check(decoded.ClassicBestScore == long.MaxValue && decoded.CrazyBestScore == 12000 && decoded.LevelBestScore == 7000 && decoded.HighestStage == 7, "all profiles round-trip");
        Check(decoded.BestScore == long.MaxValue, "overall score is derived");
        Check(AircraftFireSaveCodec.TryDecode(AircraftFireSaveCodec.Encode(new AircraftFireSaveData()), out decoded, out reason) && decoded.HighestStage == 0, "empty valid record");
    }

    private static void CodecRejectsMalformedContracts()
    {
        var valid = AircraftFireSaveCodec.Encode(new AircraftFireSaveData());
        var cases = new[]
        {
            "", "null", "[]", valid + "{}", valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2"),
            valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":\"1\""),
            valid.Replace("aircraft-fire", "flappy-bird"),
            valid.Replace("\"classicBestScore\":\"0\"", "\"classicBestScore\":null"),
            valid.Replace("\"classicBestScore\":\"0\"", "\"classicBestScore\":0"),
            valid.Replace("\"classicBestScore\":\"0\"", "\"classicBestScore\":\"-1\""),
            valid.Replace("\"classicBestScore\":\"0\"", "\"classicBestScore\":\"9223372036854775808\""),
            valid.Replace("\"classicBestScore\":\"0\"", "\"classicBestScore\":\"1\\u0000\""),
            valid.Replace("\"highestStage\":0", "\"highestStage\":8"),
            valid.Replace("\"highestStage\":0", "\"highestStage\":-1"),
            valid.Replace("\"highestStage\":0", "\"highestStage\":null"),
            valid.Replace("\"highestStage\":0", "\"highestStage\":0,\"highestStage\":1"),
            valid.Replace("\"highestStage\":0", "\"highestStage\":0,\"unknown\":0"),
            valid.Replace(",\"highestStage\":0", ""),
            new string(' ', AircraftFireSaveCodec.MaximumLength + 1)
        };
        foreach (var json in cases)
            Check(!AircraftFireSaveCodec.TryDecode(json, out var data, out var reason) && data == null && !string.IsNullOrEmpty(reason), "invalid input rejected: " + json.Substring(0, Math.Min(json.Length, 100)));
    }

    private static void RecordOnlyUpdatesPlayedProfile()
    {
        var data = new AircraftFireSaveData();
        Check(data.Record(false, false, 1000, 0), "classic new record");
        Check(!data.Record(false, false, 999, 0), "lower score unchanged");
        Check(data.Record(false, true, 6000, 0), "crazy separate record");
        Check(data.Record(true, false, 3000, 3), "level separate record");
        Check(data.ClassicBestScore == 1000 && data.CrazyBestScore == 6000 && data.LevelBestScore == 3000 && data.HighestStage == 3, "profile values remain independent");
        var copy = data.Copy();
        copy.Record(true, false, 1, 4);
        Check(data.HighestStage == 3 && copy.HighestStage == 4, "copy isolates mutation");
        Throws(() => data.Record(true, false, 1, 8), "unreached stage rejected");
        Throws(() => data.Record(false, false, -1, 0), "negative score rejected");
    }

    private static void DiskRecoveryMergesProfilesWithoutWrites()
    {
        WithDirectory(directory =>
        {
            var store = new AircraftFireSaveStore(directory);
            var empty = store.Load(out var warning);
            Check(empty.BestScore == 0 && warning == null && !Directory.Exists(directory), "load new game is read-only");
            Check(store.TrySave(new AircraftFireSaveData { ClassicBestScore = 1000 }, out var error), error);
            File.WriteAllText(store.SavePath + ".bak", AircraftFireSaveCodec.Encode(new AircraftFireSaveData { CrazyBestScore = 6000, HighestStage = 3 }));
            var primaryBytes = File.ReadAllBytes(store.SavePath);
            var backupBytes = File.ReadAllBytes(store.SavePath + ".bak");
            var merged = store.Load(out warning);
            Check(merged.ClassicBestScore == 1000 && merged.CrazyBestScore == 6000 && merged.HighestStage == 3 && warning != null, "merge better fields from both files");
            Check(Convert.ToBase64String(primaryBytes) == Convert.ToBase64String(File.ReadAllBytes(store.SavePath)) && Convert.ToBase64String(backupBytes) == Convert.ToBase64String(File.ReadAllBytes(store.SavePath + ".bak")), "load never repairs disk implicitly");
            Check(!store.TrySave(new AircraftFireSaveData { ClassicBestScore = 2000 }, out error), "reject cross-profile downgrade");
            merged.Record(true, false, 3000, 4);
            Check(store.TrySave(merged, out error), error);
            Check(AircraftFireSaveCodec.TryDecode(File.ReadAllText(store.SavePath + ".bak"), out var backup, out error), error);
            Check(backup.ClassicBestScore == 1000 && backup.CrazyBestScore == 6000 && backup.HighestStage == 3, "backup preserves component-wise prior maxima");
            File.WriteAllText(store.SavePath, "broken");
            var recovered = new AircraftFireSaveStore(directory).Load(out warning);
            Check(recovered.ClassicBestScore == 1000 && recovered.CrazyBestScore == 6000 && warning != null, "corrupt primary recovers backup");
            Check(Directory.GetFiles(directory, "*.tmp*").Length == 0, "atomic writes clean temporary files");
        });
    }

    private static void CorruptRecordsRequireNewGameplay()
    {
        WithDirectory(directory =>
        {
            Directory.CreateDirectory(directory);
            var store = new AircraftFireSaveStore(directory);
            File.WriteAllBytes(store.SavePath, new byte[] { 0xff, 0xfe, 0xff });
            var defaults = store.Load(out var warning);
            Check(warning != null && defaults.BestScore == 0, "invalid UTF8 degrades safely");
            Check(!store.TrySave(defaults, out var error), "UI defaults do not overwrite unreadable records");
            Check(File.ReadAllBytes(store.SavePath)[0] == 0xff, "unreadable file preserved");
            defaults.Record(false, false, 1000, 0);
            Check(store.TrySave(defaults, out error), error);
            Check(store.Load(out warning).BestScore == 1000, "new positive record starts recovery history");
            Check(!File.Exists(store.SavePath + ".bak"), "corrupt primary is never copied into backup");
            File.WriteAllText(store.SavePath, new string('x', AircraftFireSaveCodec.MaximumLength + 1), new UTF8Encoding(false));
            Check(new AircraftFireSaveStore(directory).Load(out warning).BestScore == 0 && warning != null, "oversized disk record rejected");
        });
    }

    private static void WithDirectory(Action<string> action)
    {
        var path = Path.Combine(Path.GetTempPath(), "aircraft-fire-tests-" + Guid.NewGuid().ToString("N"));
        try { action(path); }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static void Throws(Action action, string message)
    {
        try { action(); }
        catch (ArgumentException) { assertions++; return; }
        throw new InvalidOperationException(message);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
}
