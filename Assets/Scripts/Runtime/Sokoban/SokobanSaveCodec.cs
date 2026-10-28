using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Sokoban;

namespace XanaduYonder.Runtime.Sokoban
{
    public static class SokobanSaveCodec
    {
        public const int MaximumLength = 2 * 1024 * 1024;
        public const int MaximumDepth = 12;
        public static string Encode(SokobanSaveData data, SokobanCatalog catalog)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            // Live exports originate in the rule owner. Full trajectory replay is reserved for loading.
            if (!data.TryValidate(catalog, out var reason, false)) throw new ArgumentException(reason, nameof(data));
            var records = new JArray(); foreach (var record in data.Records) records.Add(new JObject { ["levelId"] = record.LevelId, ["bestMoves"] = Score(record.BestMoves), ["bestPushes"] = Score(record.BestPushes) });
            string json = new JObject { ["schemaVersion"] = 1, ["gameId"] = "sokoban", ["contentVersion"] = data.ContentVersion, ["selectedLevelId"] = data.SelectedLevelId, ["tutorialSeen"] = data.TutorialSeen,
                ["activeAttempt"] = data.ActiveAttempt == null ? (JToken)JValue.CreateNull() : Attempt(data.ActiveAttempt), ["completedLevelIds"] = new JArray(data.CompletedLevelIds), ["records"] = records }.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumLength) throw new ArgumentException("Save exceeds 2 MiB.", nameof(data)); return json;
        }
        private static JObject Score(SokobanScore score) { return new JObject { ["moves"] = score.Moves, ["pushes"] = score.Pushes }; }
        private static JObject Attempt(SokobanSessionData data)
        { return new JObject { ["attemptId"] = data.AttemptId, ["levelId"] = data.LevelId, ["actions"] = data.Actions, ["player"] = data.Player, ["boxes"] = new JArray(data.Boxes), ["moves"] = data.Moves, ["pushes"] = data.Pushes, ["won"] = data.Won, ["resultRecorded"] = data.ResultRecorded, ["viewport"] = new JObject { ["zoom"] = data.Zoom, ["centerX"] = data.CenterX, ["centerY"] = data.CenterY } }; }
        public static bool TryDecode(string json, SokobanCatalog catalog, out SokobanSaveData data, out string reason)
        {
            data = null; reason = null;
            try
            {
                if (catalog == null) throw new ArgumentNullException(nameof(catalog));
                var root = SokobanJson.Parse(json); SokobanJson.Fields(root, "schemaVersion", "gameId", "contentVersion", "selectedLevelId", "tutorialSeen", "activeAttempt", "completedLevelIds", "records");
                if (SokobanJson.Integer(root["schemaVersion"]) != 1 || SokobanJson.Text(root["gameId"]) != "sokoban") throw new FormatException("Unknown Sokoban save schema or game.");
                var decoded = new SokobanSaveData { ContentVersion = SokobanJson.Nonempty(root["contentVersion"]), SelectedLevelId = SokobanJson.Nonempty(root["selectedLevelId"]), TutorialSeen = SokobanJson.Boolean(root["tutorialSeen"]), ActiveAttempt = root["activeAttempt"].Type == JTokenType.Null ? null : ReadAttempt(root["activeAttempt"]) };
                var completed = SokobanJson.Array(root["completedLevelIds"]); var records = SokobanJson.Array(root["records"]);
                if (completed.Count > 1000 || records.Count != completed.Count) throw new FormatException("Invalid record count.");
                decoded.CompletedLevelIds = new string[completed.Count]; var unique = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < completed.Count; i++) { string id = SokobanJson.Nonempty(completed[i]); if (!unique.Add(id)) throw new FormatException("Duplicate completion."); decoded.CompletedLevelIds[i] = id; }
                decoded.Records = new SokobanRecord[records.Count]; var recordIds = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < records.Count; i++)
                {
                    var item = SokobanJson.Object(records[i]); SokobanJson.Fields(item, "levelId", "bestMoves", "bestPushes");
                    var record = new SokobanRecord { LevelId = SokobanJson.Nonempty(item["levelId"]), BestMoves = ReadScore(item["bestMoves"]), BestPushes = ReadScore(item["bestPushes"]) };
                    if (!record.Valid || !unique.Contains(record.LevelId) || !recordIds.Add(record.LevelId)) throw new FormatException("Invalid or duplicate result record."); decoded.Records[i] = record;
                }
                if (decoded.ActiveAttempt != null && !decoded.ActiveAttempt.ValidateShape(out reason)) return false;
                if (decoded.ContentVersion != catalog.ContentVersion)
                {
                    // Old coordinates/history have no meaning on changed terrain. Only stable known record IDs survive.
                    var retained = new List<SokobanRecord>(); var ids = new List<string>(); foreach (var record in decoded.Records) if (catalog.TryGet(record.LevelId, out var level)) { retained.Add(record); ids.Add(record.LevelId); }
                    decoded.Records = retained.ToArray(); decoded.CompletedLevelIds = ids.ToArray(); decoded.ActiveAttempt = null; decoded.ContentVersion = catalog.ContentVersion;
                    if (!catalog.TryGet(decoded.SelectedLevelId, out var selected)) decoded.SelectedLevelId = catalog.At(0).LevelId;
                    decoded.RestoreWarning = "关卡内容已更新，已保留完成纪录，请重新开始本关。";
                }
                if (!decoded.TryValidate(catalog, out reason)) return false;
                data = decoded; return true;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is ArgumentException || exception is OverflowException)
            { reason = exception.Message; data = null; return false; }
        }
        private static SokobanScore ReadScore(JToken token)
        { var item = SokobanJson.Object(token); SokobanJson.Fields(item, "moves", "pushes"); return new SokobanScore { Moves = SokobanJson.Integer(item["moves"]), Pushes = SokobanJson.Integer(item["pushes"]) }; }
        private static SokobanSessionData ReadAttempt(JToken token)
        {
            var item = SokobanJson.Object(token); SokobanJson.Fields(item, "attemptId", "levelId", "actions", "player", "boxes", "moves", "pushes", "won", "resultRecorded", "viewport");
            var viewport = SokobanJson.Object(item["viewport"]); SokobanJson.Fields(viewport, "zoom", "centerX", "centerY");
            var boxes = SokobanJson.Array(item["boxes"]); if (boxes.Count > 140) throw new FormatException("Too many boxes."); var positions = new int[boxes.Count]; for (int i = 0; i < boxes.Count; i++) positions[i] = SokobanJson.Integer(boxes[i]);
            var data = new SokobanSessionData { AttemptId = SokobanJson.Nonempty(item["attemptId"]), LevelId = SokobanJson.Nonempty(item["levelId"]), Actions = SokobanJson.Text(item["actions"]), Player = SokobanJson.Integer(item["player"]), Boxes = positions, Moves = SokobanJson.Integer(item["moves"]), Pushes = SokobanJson.Integer(item["pushes"]), Won = SokobanJson.Boolean(item["won"]), ResultRecorded = SokobanJson.Boolean(item["resultRecorded"]), Zoom = SokobanJson.Number(viewport["zoom"]), CenterX = SokobanJson.Number(viewport["centerX"]), CenterY = SokobanJson.Number(viewport["centerY"]) };
            data.NormalizeViewport(); return data;
        }
    }
}
