using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.BoardGames;
using XanaduYonder.Runtime.BoardGames;

internal static class Program
{
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "board-persistence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            RoundTripEachGame(Path.Combine(root, "roundtrip"));
            PreserveRoutesAndModeSlots(Path.Combine(root, "slots"));
            RejectMalformedAndIllegalHistory(Path.Combine(root, "malformed"));
            RecoverAndPreservePreviousBackup(Path.Combine(root, "backup"));
            ProtectBlockedSavePath(Path.Combine(root, "paths"));
            UndoRestoresCorrectActor();
            Console.WriteLine("6 board persistence and undo scenarios passed.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void RoundTripEachGame(string directory)
    {
        foreach (BoardGameId game in Enum.GetValues(typeof(BoardGameId)))
        {
            var store = new BoardSaveStore(directory, game);
            var profile = store.Load(out string warning);
            Check(warning == null && !store.ProtectDamaged && profile.Matches.Count == 0 && profile.ConfirmArmyMove, "first launch defaults");
            profile.ConfirmArmyMove = false;
            profile.TutorialSeen = true;
            var options = new BoardMatchOptions(false, BoardDifficulty.Hard, game == BoardGameId.Checkers ? 6 : 2, 1);
            var match = game == BoardGameId.Army
                ? new BoardMatch(game, options, ArmyGame.CreateFormation(201), ArmyGame.CreateFormation(315))
                : new BoardMatch(game, options);
            profile.Options = options;
            profile.Matches.Add(options.Slot, match);
            for (int ply = 0; ply < 3; ply++) PlayFirst(match);
            Check(store.Save(profile), "roundtrip actual write for " + game);
            Check(File.Exists(SavePath(directory, game)), "per-game save path for " + game);
            var reloaded = new BoardSaveStore(directory, game).Load(out warning);
            var replay = reloaded.Matches[options.Slot];
            Check(warning == null && reloaded.Options.Human == 1 && !reloaded.ConfirmArmyMove && reloaded.TutorialSeen, "preferences round trip");
            Check(replay.History.SequenceEqual(match.History) && replay.Position.CurrentSide == match.Position.CurrentSide, "history and current seat round trip");
            Check(replay.ArmyFirst.SequenceEqual(match.ArmyFirst) && replay.ArmySecond.SequenceEqual(match.ArmySecond), "formations round trip");
            for (int point = 0; point < match.Position.PointCount; point++)
                for (int viewer = 0; viewer < options.Seats; viewer++)
                {
                    var expected = match.Position.PieceAt(point, viewer);
                    var actual = replay.Position.PieceAt(point, viewer);
                    Check(expected.Side == actual.Side && expected.Kind == actual.Kind, "every replayed projection matches");
                }
        }
    }

    private static void PreserveRoutesAndModeSlots(string directory)
    {
        const BoardGameId game = BoardGameId.Checkers;
        var profile = new BoardProfile(game);
        for (int id = 0; id < 5; id++)
        {
            var options = new BoardMatchOptions(id == 0, BoardDifficulty.Normal, id == 0 ? 2 : BoardCatalog.CheckersSeats[id - 1]);
            var match = new BoardMatch(game, options);
            var hop = match.Position.LegalMoves().First(m => m.PathLength > 0);
            Check(match.Play(hop), "a routed opening hop is legal");
            profile.Matches.Add(id, match);
        }
        var store = new BoardSaveStore(directory, game);
        Check(store.Save(profile), "all five checkers slots save together");
        var restored = store.Load(out string warning);
        Check(warning == null && restored.Matches.Count == 5, "all five slots remain independently resumable");
        foreach (var entry in profile.Matches)
        {
            var replay = restored.Matches[entry.Key];
            Check(replay.Options.Seats == entry.Value.Options.Seats && replay.Options.Local == entry.Value.Options.Local, "slot mode and seats stay distinct");
            Check(replay.LastMove.Path.SequenceEqual(entry.Value.LastMove.Path), "route remains in the restored move");
        }
    }

    private static void RejectMalformedAndIllegalHistory(string directory)
    {
        const BoardGameId game = BoardGameId.Chess;
        var store = new BoardSaveStore(directory, game);
        var profile = ProfileWithMatch(game);
        Check(store.Save(profile), "write canonical save");
        string path = SavePath(directory, game);
        string valid = File.ReadAllText(path);
        var mutations = new Action<JObject>[]
        {
            root => root["version"] = 2,
            root => root["confirmMove"] = "true",
            root => root.Remove("tutorialSeen"),
            root => root["matches"][0]["options"]["human"] = JValue.CreateNull(),
            root => root["matches"][0]["moves"][0] = new JArray(63, 63, 0, new JArray()),
            root => root["matches"][0]["moves"][0][3] = new JArray(2),
            root => ((JArray)root["matches"]).Add(root["matches"][0].DeepClone()),
            root => root["matches"][0]["slot"] = 0,
        };
        foreach (var change in mutations)
        {
            var corrupted = JObject.Parse(valid);
            change(corrupted);
            File.WriteAllText(path, corrupted.ToString(Formatting.None));
            AssertDamaged(directory, game, profile);
        }
        File.WriteAllText(path, valid.Replace("\"version\":1", "\"version\":1,\"version\":1"));
        AssertDamaged(directory, game, profile);
        File.WriteAllBytes(path, new byte[] { 0x7b, 0xfe, 0x7d });
        AssertDamaged(directory, game, profile);
    }

    private static void AssertDamaged(string directory, BoardGameId game, BoardProfile replacement)
    {
        var store = new BoardSaveStore(directory, game);
        var profile = store.Load(out string warning);
        Check(warning != null && store.ProtectDamaged && profile.Matches.Count == 0, "bad file is protected instead of being accepted");
        Check(!store.Save(replacement), "ordinary save cannot overwrite damaged original");
    }

    private static void RecoverAndPreservePreviousBackup(string directory)
    {
        const BoardGameId game = BoardGameId.Xiangqi;
        var store = new BoardSaveStore(directory, game);
        var profile = ProfileWithMatch(game);
        var match = profile.Matches[profile.Options.Slot];
        Check(store.Save(profile), "first real write");
        string path = SavePath(directory, game);
        var first = File.ReadAllBytes(path);
        PlayFirst(match);
        Check(store.Save(profile), "next write replaces main atomically");
        Check(first.SequenceEqual(File.ReadAllBytes(path + ".bak")), "backup is the last complete previous snapshot");
        File.WriteAllText(path, "{truncated");
        var restored = store.Load(out string warning);
        Check(warning != null && !store.ProtectDamaged && restored.Matches[restored.Options.Slot].MoveCount == 1, "damaged main recovers backup");
        Check(store.Save(restored), "a saved backup recovery repairs main");
        Check(first.SequenceEqual(File.ReadAllBytes(path + ".bak")), "repair preserves last known-good backup");
        File.WriteAllText(path, "{");
        File.WriteAllText(path + ".bak", "{");
        store.Load(out warning);
        Check(store.ProtectDamaged, "both corrupt files require confirmation");
        store.AllowReplacement();
        Check(store.Save(profile), "explicit replacement permits valid new save");
        Check(store.Load(out warning).Matches.Count == 1 && warning == null, "new save is readable");
    }

    private static void ProtectBlockedSavePath(string path)
    {
        const BoardGameId game = BoardGameId.Chess;
        File.WriteAllText(path, "not a directory");
        var directory = Path.Combine(path, "nested", "save");
        var store = new BoardSaveStore(directory, game);
        store.Load(out string warning);
        Check(warning != null && store.ProtectDamaged, "a file blocking a save-directory ancestor must report a storage failure");
        Check(!store.Save(new BoardProfile(game)), "cannot write below a file");
        Check(File.ReadAllText(path) == "not a directory", "blocked save does not alter its regular ancestor file");
        File.Delete(path);
        var healthy = new BoardSaveStore(directory, game);
        Check(healthy.Save(ProfileWithMatch(game)), "save creates previously absent nested directory");
        Check(store.Load(out warning).Matches.Count == 1 && warning == null && !store.ProtectDamaged,
            "successful reload clears stale damage protection");
    }

    private static void UndoRestoresCorrectActor()
    {
        var local = new BoardMatch(BoardGameId.Xiangqi, new BoardMatchOptions(true, BoardDifficulty.Normal, 2, flip: true));
        PlayFirst(local);
        BoardMove first = local.LastMove;
        PlayFirst(local);
        int revision = local.Revision;
        Check(local.CanUndo && local.Undo() && local.MoveCount == 1 && local.LastMove == first &&
            local.Position.CurrentSide == 1 && local.Revision == revision + 1, "local undo replays exactly one ply");

        var aiFirst = new BoardMatch(BoardGameId.Chess, new BoardMatchOptions(false, BoardDifficulty.Normal, 2, 1));
        PlayFirst(aiFirst);
        Check(!aiFirst.CanUndo && !aiFirst.Undo(), "opening computer move is not a human undo point");
        PlayFirst(aiFirst);
        PlayFirst(aiFirst);
        Check(aiFirst.Undo() && aiFirst.MoveCount == 1 && !aiFirst.Computer, "human and reply undo preserves AI opening");

        var six = new BoardMatch(BoardGameId.Checkers, new BoardMatchOptions(false, BoardDifficulty.Normal, 6));
        for (int ply = 0; ply < 12; ply++) PlayFirst(six);
        Check(six.Undo() && six.MoveCount == 6 && !six.Computer, "multiplayer undo removes the latest human turn and all five computer replies");
        PlayFirst(six);
        Check(six.Computer && six.Undo() && six.MoveCount == 6 && !six.Computer, "undo can invalidate a pending computer reply");

        var formation = ArmyGame.CreateFormation(31);
        var army = new BoardMatch(BoardGameId.Army, new BoardMatchOptions(true, BoardDifficulty.Normal, 2), formation, formation);
        PlayFirst(army);
        Check(!army.CanUndo && !army.Undo(), "concealed battle cannot undo after submitting a move");
    }

    private static BoardProfile ProfileWithMatch(BoardGameId game)
    {
        var profile = new BoardProfile(game);
        var match = new BoardMatch(game, profile.Options);
        PlayFirst(match);
        profile.Matches.Add(profile.Options.Slot, match);
        return profile;
    }

    private static void PlayFirst(BoardMatch match)
    {
        Check(match.Play(match.Position.LegalMoves().First()), "execute first legal move");
    }

    private static string SavePath(string directory, BoardGameId game) => Path.Combine(directory, BoardCatalog.Id(game) + "-v1.json");

    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
    }
}
