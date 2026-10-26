using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    public static class IdiomSaveCodec
    {
        public const int MaximumBytes = 4 * 1024 * 1024;
        public const int MaximumDraftLength = 128;

        public static string Encode(IdiomSaveData data, IdiomLexicon lexicon)
        {
            if (data == null || lexicon == null || data.Character == null || data.Sound == null)
                throw new ArgumentException("对局记录不完整，当前进度未保存。");
            var root = new JObject { ["schemaVersion"] = 1, ["gameId"] = "idiom", ["dictionaryVersion"] = lexicon.Version,
                ["activeMode"] = (int)data.ActiveMode, ["character"] = WriteMode(data.Character), ["sound"] = WriteMode(data.Sound) };
            string json = IdiomStrictJson.Write(root, MaximumBytes);
            Decode(json, lexicon);
            return json;
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
            return new JObject { ["draft"] = mode.Draft, ["best"] = mode.Best, ["newBestInRun"] = mode.NewBestInRun,
                ["game"] = new JObject { ["mode"] = (int)game.Mode, ["firstActor"] = (int)game.FirstActor,
                    ["endReason"] = (int)game.EndReason, ["randomState"] = game.RandomState,
                    ["history"] = new JArray(game.History), ["hints"] = hints } };
        }

        public static bool TryDecode(string json, IdiomLexicon lexicon, out IdiomSaveData data, out string error)
        {
            try { data = Decode(json, lexicon); error = null; return true; }
            catch (Exception ex) when (IdiomStoreErrors.IsDataError(ex))
            { data = null; error = "对局记录无法读取，原文件已保留。"; return false; }
        }

        internal static IdiomSaveData Decode(string json, IdiomLexicon lexicon)
        {
            if (lexicon == null) throw new ArgumentException("词库尚未就绪。");
            var root = IdiomStrictJson.Read(json, MaximumBytes);
            IdiomStrictJson.Identity(root, "idiom");
            if (IdiomStrictJson.Text(root["dictionaryVersion"], 128) != lexicon.Version) throw new IdiomVersionException();
            IdiomStrictJson.Object(root, "schemaVersion", "gameId", "dictionaryVersion", "activeMode", "character", "sound");
            return new IdiomSaveData { ActiveMode = (IdiomMode)IdiomStrictJson.Integer(root["activeMode"], 0, 1),
                Character = ReadMode(root["character"], lexicon, IdiomMode.SameCharacter),
                Sound = ReadMode(root["sound"], lexicon, IdiomMode.SameSound) };
        }
        private static IdiomModeSave ReadMode(JToken token, IdiomLexicon lexicon, IdiomMode expectedMode)
        {
            var saved = IdiomStrictJson.Object(token, "game", "draft", "best", "newBestInRun");
            var game = IdiomStrictJson.Object(saved["game"], "mode", "firstActor", "endReason", "randomState", "history", "hints");
            var mode = (IdiomMode)IdiomStrictJson.Integer(game["mode"], 0, 1);
            if (mode != expectedMode) throw new FormatException();
            var history = new List<string>();
            foreach (var id in IdiomStrictJson.Array(game["history"], lexicon.Count)) history.Add(IdiomStrictJson.Text(id, 128));
            var hints = new List<IdiomHint>();
            foreach (var hintToken in IdiomStrictJson.Array(game["hints"], 3))
            {
                var hint = IdiomStrictJson.Object(hintToken, "turnIndex", "entryIds");
                var ids = new List<string>();
                foreach (var id in IdiomStrictJson.Array(hint["entryIds"], 3)) ids.Add(IdiomStrictJson.Text(id, 128));
                hints.Add(new IdiomHint((int)IdiomStrictJson.Integer(hint["turnIndex"], 0, history.Count), ids));
            }
            var snapshot = new IdiomSnapshot(mode, (IdiomActor)IdiomStrictJson.Integer(game["firstActor"], 0, 2),
                (IdiomEndReason)IdiomStrictJson.Integer(game["endReason"], 0, 5),
                (uint)IdiomStrictJson.Integer(game["randomState"], 1, uint.MaxValue), history, hints);
            // Validate through the actual rule engine, including historical hint legality.
            var restored = new IdiomGame(lexicon, snapshot);
            int best = (int)IdiomStrictJson.Integer(saved["best"], restored.PlayerCount, lexicon.Count);
            bool newBest = IdiomStrictJson.Boolean(saved["newBestInRun"]);
            if (newBest && (restored.PlayerCount == 0 || best != restored.PlayerCount)) throw new FormatException();
            return new IdiomModeSave { Game = restored.GetSnapshot(), Draft = IdiomStrictJson.Text(saved["draft"], MaximumDraftLength, true),
                Best = best, NewBestInRun = newBest };
        }
    }
}
