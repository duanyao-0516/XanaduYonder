using System;
using System.IO;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Feihualing;

internal static class NotebookChecks
{
    private const string FileName = "feihualing-notebook-v1.json";
    private static readonly PoetryWork First = new PoetryWork("song-ci:1", "苏轼", "水调歌头",
        "明月几时有，把酒问青天。\n但愿人长久，千里共婵娟。");
    private static readonly PoetryWork Second = new PoetryWork("song-ci:2", "苏轼", "水调歌头",
        "昵昵儿女语，灯火夜微明。\n恩怨尔汝来去，弹指泪和声。");
    private static readonly PoetryWork Third = new PoetryWork("poem:3", "王维", "山居秋暝",
        "空山新雨后，天气晚来秋。\n明月松间照，清泉石上流。");

    public static void Run()
    {
        InTemporaryDirectory(CheckRoundTrip);
        InTemporaryDirectory(CheckFailedWrite);
        InTemporaryDirectory(CheckCorruptFiles);
        InTemporaryDirectory(CheckFutureFile);
        Console.WriteLine("Feihualing notebook checks passed.");
    }

    private static void CheckRoundTrip(string directory)
    {
        var matchPath = Path.Combine(directory, "feihualing-v3.json");
        var idiomPath = Path.Combine(directory, "idiom-notebook-v1.json");
        File.WriteAllText(matchPath, "unrelated match bytes");
        File.WriteAllText(idiomPath, "unrelated idiom notebook bytes");
        var firstStore = new FeihualingNotebookStore(directory);
        var secondStore = new FeihualingNotebookStore(directory);
        Require(firstStore.LoadNotebook(out var warning) && warning == null && firstStore.Entries.Count == 0, "Missing notebook did not open empty.");
        Require(secondStore.LoadNotebook(out _), "Second reader could not load notebook.");
        Require(firstStore.Add(First, out var added) == FeihualingNotebookAddResult.Added && !string.IsNullOrEmpty(added), "Initial collection failed.");
        Require(secondStore.Add(Second, out _) == FeihualingNotebookAddResult.Added, "Second reader could not append another work.");
        Require(secondStore.Entries.Count == 2, "A stale second instance discarded the first collection.");
        var changedEdition = new PoetryWork(First.Id, "另一版本", "另一篇名", "不同文句");
        Require(firstStore.Add(changedEdition, out var duplicate) == FeihualingNotebookAddResult.AlreadySaved && duplicate != added,
            "Same stable work ID did not report an idempotent duplicate.");
        Require(firstStore.Contains(First.Id) && firstStore.Contains(Second.Id) && !firstStore.Contains("unknown"), "Saved IDs are not discoverable.");

        var restarted = new FeihualingNotebookStore(directory);
        Require(restarted.LoadNotebook(out warning) && warning == null && restarted.Entries.Count == 2, "Restart did not recover the same notebook.");
        Require(restarted.Entries[0].WorkId == Second.Id && restarted.Entries[1].WorkId == First.Id, "Ordering did not preserve newest first.");
        Require(restarted.Entries[1].Title == First.Title && restarted.Entries[1].Author == First.Author &&
            restarted.Entries[1].Body == First.Body, "Complete original or metadata was altered during round trip.");
        Require(File.ReadAllText(matchPath) == "unrelated match bytes" && File.ReadAllText(idiomPath) == "unrelated idiom notebook bytes",
            "Notebook work changed an unrelated file.");
    }

    private static void CheckFailedWrite(string directory)
    {
        var store = new FeihualingNotebookStore(directory);
        Require(store.LoadNotebook(out _) && store.Add(First, out _) == FeihualingNotebookAddResult.Added, "Could not seed write failure fixture.");
        var path = Path.Combine(directory, FileName);
        var before = File.ReadAllText(path);
        Directory.CreateDirectory(path + ".tmp");
        Require(store.Add(Second, out var message) == FeihualingNotebookAddResult.Failed && !string.IsNullOrEmpty(message), "Write failure did not report an error.");
        Require(store.Entries.Count == 1 && !store.Contains(Second.Id) && File.ReadAllText(path) == before,
            "Failed write changed the durable or in-memory notebook.");
        Directory.Delete(path + ".tmp");
        Require(store.Add(Second, out _) == FeihualingNotebookAddResult.Added && store.Entries.Count == 2, "Retry could not persist the collection.");
    }

    private static void CheckCorruptFiles(string directory)
    {
        var ready = new FeihualingNotebookStore(directory);
        Require(ready.LoadNotebook(out _) && ready.Add(First, out _) == FeihualingNotebookAddResult.Added &&
            ready.Add(Second, out _) == FeihualingNotebookAddResult.Added, "Could not seed backup recovery.");
        var path = Path.Combine(directory, FileName);
        var backup = path + ".bak";
        const string corruptMain = "{broken notebook";
        File.WriteAllText(path, corruptMain);
        var recovered = new FeihualingNotebookStore(directory);
        Require(recovered.LoadNotebook(out var notice) && !string.IsNullOrEmpty(notice) && recovered.Entries.Count == 1 &&
            recovered.Contains(First.Id), "Valid backup did not recover the notebook.");
        Require(File.ReadAllText(path) == corruptMain, "Reading the backup changed the corrupt primary.");
        Require(recovered.Add(Third, out _) == FeihualingNotebookAddResult.Added &&
            recovered.Contains(First.Id) && recovered.Contains(Third.Id), "Writing after backup recovery lost the recoverable collection.");
        var archived = Directory.GetFiles(directory, FileName, SearchOption.AllDirectories);
        Require(Array.Exists(archived, candidate => candidate != path && File.ReadAllText(candidate) == corruptMain),
            "Recovered write did not preserve the corrupt primary.");
        const string corruptBackup = "invalid backup";
        File.WriteAllText(path, corruptMain);
        File.WriteAllText(backup, corruptBackup);
        var protectedStore = new FeihualingNotebookStore(directory);
        Require(!protectedStore.LoadNotebook(out notice) && !string.IsNullOrEmpty(notice), "Two corrupt files looked like a fresh notebook.");
        Require(protectedStore.Add(Second, out _) == FeihualingNotebookAddResult.Failed && protectedStore.Entries.Count == 0,
            "A collection action allowed writes over unrecoverable files.");
        Require(File.ReadAllText(path) == corruptMain && File.ReadAllText(backup) == corruptBackup, "Unrecoverable notebook bytes were replaced.");
    }

    private static void CheckFutureFile(string directory)
    {
        var ready = new FeihualingNotebookStore(directory);
        Require(ready.LoadNotebook(out _) && ready.Add(First, out _) == FeihualingNotebookAddResult.Added &&
            ready.Add(Second, out _) == FeihualingNotebookAddResult.Added, "Could not seed future schema safeguard.");
        var path = Path.Combine(directory, FileName);
        const string future = "{\"schemaVersion\":2,\"gameId\":\"feihualing-notebook\",\"futureShape\":true}";
        var backup = File.ReadAllText(path + ".bak");
        File.WriteAllText(path, future);
        var reader = new FeihualingNotebookStore(directory);
        Require(!reader.LoadNotebook(out var warning) && !string.IsNullOrEmpty(warning), "Future schema was mistaken for corrupt data recoverable by regression.");
        Require(reader.Add(Third, out _) == FeihualingNotebookAddResult.Failed, "Future schema was overwritten by a collection action.");
        Require(File.ReadAllText(path) == future && File.ReadAllText(path + ".bak") == backup, "Future notebook or backup changed.");
    }

    private static void InTemporaryDirectory(Action<string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "feihualing-notebook-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { check(directory); }
        finally { Directory.Delete(directory, true); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
