using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    public static class IdiomSaveCodec
    {
        // One transaction contains the notebook (up to 32 MiB), four complete games,
        // the shared learning profile and bounded historical selection witnesses.
        public const int MaximumBytes = 64 * 1024 * 1024;
        public const int MaximumDraftLength = 128;
        private const long MaximumTimestamp = 253402300799L;

        public static string Encode(IdiomSaveData data, IdiomLexicon lexicon)
        {
            if (data == null || lexicon == null || data.Character == null || data.Sound == null ||
                data.LocalCharacter == null || data.LocalSound == null || data.Profile == null || data.Notebook == null)
                throw new ArgumentException("成语学习记录不完整，当前进度未保存。");
            var root = new JObject { ["schemaVersion"] = 1, ["gameId"] = "idiom-learning", ["dictionaryVersion"] = lexicon.Version,
                ["activeMode"] = (int)data.ActiveMode, ["activeMatchKind"] = (int)data.ActiveMatchKind,
                ["closeKeyboardAfterSend"] = data.CloseKeyboardAfterSend, ["showPinyin"] = data.ShowPinyin,
                ["character"] = WriteMode(data.Character), ["sound"] = WriteMode(data.Sound),
                ["localCharacter"] = WriteMode(data.LocalCharacter), ["localSound"] = WriteMode(data.LocalSound),
                ["profile"] = WriteProfile(data.Profile), ["notebook"] = JObject.Parse(IdiomNotebookCodec.Encode(data.Notebook)) };
            string json = IdiomStrictJson.Write(root, MaximumBytes);
            Decode(json, lexicon);
            return json;
        }

        private static JObject WriteProfile(IdiomLearningSnapshot profile)
        {
            var exposures = new JArray();
            foreach (var exposure in profile.AppExposures)
            {
                if (exposure == null) throw new ArgumentException("学习记录不完整。");
                exposures.Add(new JObject { ["entryId"] = exposure.EntryId, ["lastShownUtc"] = exposure.LastShownUtc });
            }
            var uses = new JArray();
            foreach (var use in profile.Uses)
            {
                if (use == null) throw new ArgumentException("学习记录不完整。");
                uses.Add(new JObject { ["entryId"] = use.EntryId, ["independentUses"] = use.IndependentUses, ["assistedUses"] = use.AssistedUses });
            }
            var added = new JArray();
            foreach (var item in profile.NotebookAdded)
            {
                if (item == null) throw new ArgumentException("学习记录不完整。");
                added.Add(new JObject { ["entryId"] = item.EntryId, ["addedAtUtc"] = item.AddedAtUtc });
            }
            return new JObject { ["familiarIds"] = new JArray(profile.FamiliarIds), ["notebookIds"] = new JArray(profile.NotebookIds),
                ["recentAppIds"] = new JArray(profile.RecentAppIds), ["appExposures"] = exposures, ["uses"] = uses, ["notebookAdded"] = added };
        }

        private static JObject WriteMode(IdiomModeSave mode)
        {
            var game = mode.Game;
            if (game == null) throw new ArgumentException("对局记录不完整，当前进度未保存。");
            var hints = new JArray();
            foreach (var hint in game.Hints)
            {
                if (hint == null) throw new ArgumentException("提示记录不完整，当前进度未保存。");
                hints.Add(new JObject { ["turnIndex"] = hint.TurnIndex, ["entryIds"] = new JArray(hint.EntryIds) });
            }
            var context = game.EndSelectionContext;
            JToken selection = context == null ? (JToken)JValue.CreateNull() : new JObject {
                ["ordinaryIds"] = new JArray(context.OrdinaryIds), ["reviewIds"] = new JArray(context.ReviewIds) };
            return new JObject { ["draft"] = mode.Draft, ["best"] = mode.Best, ["newBestInRun"] = mode.NewBestInRun,
                ["game"] = new JObject { ["mode"] = (int)game.Mode, ["matchKind"] = (int)game.MatchKind, ["firstActor"] = (int)game.FirstActor,
                    ["endReason"] = (int)game.EndReason, ["randomState"] = game.RandomState, ["hintRandomState"] = game.HintRandomState,
                    ["history"] = new JArray(game.History), ["hints"] = hints,
                    ["assistedEntryIds"] = new JArray(game.AssistedEntryIds), ["endSelectionContext"] = selection } };
        }

        public static bool TryDecode(string json, IdiomLexicon lexicon, out IdiomSaveData data, out string error)
        {
            try { data = Decode(json, lexicon); error = null; return true; }
            catch (Exception ex) when (IdiomStoreErrors.IsDataError(ex))
            { data = null; error = "成语学习记录无法读取，原文件已保留。"; return false; }
        }

        internal static IdiomSaveData Decode(string json, IdiomLexicon lexicon)
        {
            if (lexicon == null) throw new ArgumentException("词库尚未就绪。");
            var root = IdiomStrictJson.Read(json, MaximumBytes);
            IdiomStrictJson.Identity(root, "idiom-learning");
            if (IdiomStrictJson.Text(root["dictionaryVersion"], 128) != lexicon.Version) throw new IdiomVersionException();
            // Existing aggregates have neither preference. Only missing keys receive
            // defaults; explicit nulls and non-booleans must retain corruption protection.
            if (root.Property("closeKeyboardAfterSend") == null) root["closeKeyboardAfterSend"] = true;
            if (root.Property("showPinyin") == null) root["showPinyin"] = false;
            IdiomStrictJson.Object(root, "schemaVersion", "gameId", "dictionaryVersion", "activeMode", "activeMatchKind",
                "closeKeyboardAfterSend", "showPinyin", "character", "sound", "localCharacter", "localSound", "profile", "notebook");
            var profile = ReadProfile(root["profile"], lexicon);
            var notebook = IdiomNotebookCodec.Decode(root["notebook"].ToString(Formatting.None));
            ValidateNotebook(notebook, profile.GetSnapshot(), lexicon);
            return new IdiomSaveData { ActiveMode = (IdiomMode)IdiomStrictJson.Integer(root["activeMode"], 0, 1),
                ActiveMatchKind = (IdiomMatchKind)IdiomStrictJson.Integer(root["activeMatchKind"], 0, 1),
                CloseKeyboardAfterSend = IdiomStrictJson.Boolean(root["closeKeyboardAfterSend"]),
                ShowPinyin = IdiomStrictJson.Boolean(root["showPinyin"]),
                Character = ReadMode(root["character"], lexicon, profile, IdiomMode.SameCharacter, IdiomMatchKind.Computer),
                Sound = ReadMode(root["sound"], lexicon, profile, IdiomMode.SameSound, IdiomMatchKind.Computer),
                LocalCharacter = ReadMode(root["localCharacter"], lexicon, profile, IdiomMode.SameCharacter, IdiomMatchKind.Local),
                LocalSound = ReadMode(root["localSound"], lexicon, profile, IdiomMode.SameSound, IdiomMatchKind.Local),
                Profile = profile.GetSnapshot(), Notebook = notebook };
        }

        private static IdiomLearningProfile ReadProfile(JToken token, IdiomLexicon lexicon)
        {
            var profile = IdiomStrictJson.Object(token, "familiarIds", "notebookIds", "recentAppIds", "appExposures", "uses", "notebookAdded");
            var exposures = new List<IdiomAppExposure>();
            foreach (var item in IdiomStrictJson.Array(profile["appExposures"], lexicon.Count))
            {
                var row = IdiomStrictJson.Object(item, "entryId", "lastShownUtc");
                exposures.Add(new IdiomAppExposure(IdiomStrictJson.Text(row["entryId"], 128),
                    IdiomStrictJson.Integer(row["lastShownUtc"], 0, MaximumTimestamp)));
            }
            var uses = new List<IdiomUseCount>();
            foreach (var item in IdiomStrictJson.Array(profile["uses"], lexicon.Count))
            {
                var row = IdiomStrictJson.Object(item, "entryId", "independentUses", "assistedUses");
                uses.Add(new IdiomUseCount(IdiomStrictJson.Text(row["entryId"], 128),
                    (int)IdiomStrictJson.Integer(row["independentUses"], 0, int.MaxValue),
                    (int)IdiomStrictJson.Integer(row["assistedUses"], 0, int.MaxValue)));
            }
            var added = new List<IdiomNotebookAdded>();
            foreach (var item in IdiomStrictJson.Array(profile["notebookAdded"], lexicon.Count))
            {
                var row = IdiomStrictJson.Object(item, "entryId", "addedAtUtc");
                added.Add(new IdiomNotebookAdded(IdiomStrictJson.Text(row["entryId"], 128),
                    IdiomStrictJson.Integer(row["addedAtUtc"], 0, MaximumTimestamp)));
            }
            return new IdiomLearningProfile(lexicon, new IdiomLearningSnapshot(
                ReadIds(profile["familiarIds"], lexicon.Count), ReadIds(profile["notebookIds"], lexicon.Count),
                ReadIds(profile["recentAppIds"], 10), exposures, uses, added));
        }

        private static void ValidateNotebook(IdiomNotebookData notebook, IdiomLearningSnapshot profile, IdiomLexicon lexicon)
        {
            var ids = new HashSet<string>(profile.NotebookIds, StringComparer.Ordinal);
            var added = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var item in profile.NotebookAdded) added.Add(item.EntryId, item.AddedAtUtc);
            if (ids.Count != notebook.Entries.Count || added.Count != ids.Count) throw new FormatException();
            foreach (var item in notebook.Entries)
            {
                IdiomEntry entry; long addedAt;
                if (!ids.Contains(item.Id) || !added.TryGetValue(item.Id, out addedAt) || item.AddedAt != addedAt ||
                    !lexicon.TryGetById(item.Id, out entry) || entry.Word != item.Word || entry.SenseId != item.SenseId || item.DictionaryVersion != lexicon.Version)
                    throw new FormatException();
            }
        }

        private static List<string> ReadIds(JToken token, int maximum)
        {
            var ids = new List<string>();
            foreach (var id in IdiomStrictJson.Array(token, maximum)) ids.Add(IdiomStrictJson.Text(id, 128));
            return ids;
        }

        private static IdiomModeSave ReadMode(JToken token, IdiomLexicon lexicon, IdiomLearningProfile profile,
                                             IdiomMode expectedMode, IdiomMatchKind expectedKind)
        {
            var saved = IdiomStrictJson.Object(token, "game", "draft", "best", "newBestInRun");
            var game = IdiomStrictJson.Object(saved["game"], "mode", "matchKind", "firstActor", "endReason", "randomState",
                "hintRandomState", "history", "hints", "assistedEntryIds", "endSelectionContext");
            var mode = (IdiomMode)IdiomStrictJson.Integer(game["mode"], 0, 1);
            var kind = (IdiomMatchKind)IdiomStrictJson.Integer(game["matchKind"], 0, 1);
            if (mode != expectedMode || kind != expectedKind) throw new FormatException();
            var history = ReadIds(game["history"], lexicon.Count);
            var hints = new List<IdiomHint>();
            foreach (var hintToken in IdiomStrictJson.Array(game["hints"], kind == IdiomMatchKind.Local ? 6 : 3))
            {
                var hint = IdiomStrictJson.Object(hintToken, "turnIndex", "entryIds");
                hints.Add(new IdiomHint((int)IdiomStrictJson.Integer(hint["turnIndex"], 0, history.Count), ReadIds(hint["entryIds"], 3)));
            }
            IdiomSelectionContext context = null;
            if (game["endSelectionContext"].Type != JTokenType.Null)
            {
                var selection = IdiomStrictJson.Object(game["endSelectionContext"], "ordinaryIds", "reviewIds");
                context = new IdiomSelectionContext(ReadIds(selection["ordinaryIds"], lexicon.Count), ReadIds(selection["reviewIds"], lexicon.Count));
            }
            var snapshot = new IdiomSnapshot(mode, (IdiomActor)IdiomStrictJson.Integer(game["firstActor"], 0, 2),
                (IdiomEndReason)IdiomStrictJson.Integer(game["endReason"], 0, 7),
                (uint)IdiomStrictJson.Integer(game["randomState"], 1, uint.MaxValue), history, hints, kind,
                (uint)IdiomStrictJson.Integer(game["hintRandomState"], 1, uint.MaxValue), ReadIds(game["assistedEntryIds"], lexicon.Count), context);
            // Historical legality and frozen terminal evidence are validated by Core,
            // without regenerating results from the currently shared learning profile.
            var restored = new IdiomGame(lexicon, snapshot, profile, () => 0L);
            int count = kind == IdiomMatchKind.Local ? restored.History.Count : restored.PlayerCount;
            int best = (int)IdiomStrictJson.Integer(saved["best"], count, lexicon.Count);
            bool newBest = IdiomStrictJson.Boolean(saved["newBestInRun"]);
            if (newBest && (count == 0 || best != count)) throw new FormatException();
            return new IdiomModeSave { Game = restored.GetSnapshot(), Draft = IdiomStrictJson.Text(saved["draft"], MaximumDraftLength, true),
                Best = best, NewBestInRun = newBest };
        }
    }
}
