using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.AgeOfWar;
using XanaduYonder.Core.AgeOfWar.One;
using XanaduYonder.Core.AgeOfWar.Two;

namespace XanaduYonder.Runtime.AgeOfWar
{
    internal sealed class AgeOfWarSaveStore
    {
        internal const int MaximumBattleBytes = 16 * 1024 * 1024;
        private const int MaximumBase64Length = ((MaximumBattleBytes + 2) / 3) * 4;
        private const int MaximumJsonBytes = MaximumBase64Length + 4096;
        private const int MaximumDepth = 4;
        private static readonly object Gate = new object();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly string[] Fields = { "schemaVersion", "edition", "battleState", "cameraX", "cameraY",
            "menu", "quality", "musicVolume", "soundVolume", "showDamage", "frameRate" };
        private readonly string directory;
        private readonly int edition;
        public string SavePath { get; private set; }

        public AgeOfWarSaveStore(string directory, int edition)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            if (edition != 1 && edition != 2) throw new ArgumentOutOfRangeException(nameof(edition));
            this.directory = directory;
            this.edition = edition;
            SavePath = Path.Combine(directory, edition == 1 ? "age-of-war-one-v1.json" : "age-of-war-two-v1.json");
        }

        public AgeOfWarLoadResult Load()
        {
            lock (Gate) return Combine(Read(SavePath), Read(SavePath + ".bak"));
        }

        public bool TrySave(AgeOfWarSaveData data, out string error)
        {
            lock (Gate)
            {
                error = null;
                try
                {
                    if (data == null) throw new FormatException("Missing Age of War save data.");
                    Validate(data);
                    var copy = Copy(data);
                    Validate(copy);
                    // A write must not rotate a previously good backup behind an invalid payload.
                    Restore(copy);
                    return Write(copy, out error);
                }
                catch (Exception exception) when (IsDataFailure(exception) || exception is NotSupportedException)
                { error = "战争进化史未保存：" + exception.Message; return false; }
            }
        }

        public bool TryClear(out string error)
        {
            lock (Gate)
            {
                var loaded = Combine(Read(SavePath), Read(SavePath + ".bak"));
                if (!loaded.CanWrite) { error = loaded.Warning; return false; }
                var empty = loaded.Data == null ? new AgeOfWarSaveData { Edition = edition, FrameRate = edition == 1 ? 40 : 150 } : Copy(loaded.Data);
                empty.BattleState = null;
                empty.CameraX = empty.CameraY = 0;
                empty.Menu = "main";
                return Write(empty, out error);
            }
        }

        private bool Write(AgeOfWarSaveData data, out string error)
        {
            error = null;
            string temporary = SavePath + ".tmp";
            string backupTemporary = SavePath + ".bak.tmp";
            try
            {
                var main = Read(SavePath);
                var backup = Read(SavePath + ".bak");
                var permission = Combine(main, backup);
                if (!permission.CanWrite) { error = permission.Warning; return false; }
                byte[] bytes = Encode(data);
                Directory.CreateDirectory(directory);
                FlushFile(temporary, bytes);
                if (data.BattleState == null) FlushFile(backupTemporary, bytes);

                // Preserve a future-version or unreadable slot even if it appeared while writing.
                main = Read(SavePath); backup = Read(SavePath + ".bak");
                permission = Combine(main, backup);
                if (!permission.CanWrite) { error = permission.Warning; return false; }
                if (data.BattleState == null)
                {
                    // Replace the backup first. Once a new primary tombstone exists, its backup
                    // must already be a tombstone too; a damaged primary cannot revive an old run.
                    Replace(backupTemporary, SavePath + ".bak", backup.Status, null);
                    Replace(temporary, SavePath, main.Status, null);
                }
                else
                {
                    // A corrupt primary must never overwrite the legal backup used for recovery.
                    Replace(temporary, SavePath, main.Status,
                        main.Status == AgeOfWarLoadStatus.Loaded ? SavePath + ".bak" : null);
                }
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || IsDataFailure(exception))
            { error = "战争进化史未保存：" + exception.Message; return false; }
            finally
            {
                DeleteTemporary(temporary);
                DeleteTemporary(backupTemporary);
            }
        }

        private static void FlushFile(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }

        private static void Replace(string temporary, string target, AgeOfWarLoadStatus previous, string backup)
        {
            if (previous == AgeOfWarLoadStatus.Missing) File.Move(temporary, target);
            else File.Replace(temporary, target, backup);
        }

        private static void DeleteTemporary(string path)
        {
            try { File.Delete(path); }
            catch (Exception exception) when (IsStorageFailure(exception)) { /* Main and backup remain authoritative. */ }
        }

        private AgeOfWarLoadResult Read(string path)
        {
            string json;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaximumJsonBytes)
                        return Result(AgeOfWarLoadStatus.Corrupt, null, null, "存档超过大小上限。", false);
                    var bytes = new byte[(int)stream.Length];
                    int count = 0;
                    while (count < bytes.Length)
                    {
                        int read = stream.Read(bytes, count, bytes.Length - count);
                        if (read == 0) throw new EndOfStreamException("Save file changed while reading.");
                        count += read;
                    }
                    if (stream.ReadByte() != -1) throw new IOException("Save file changed while reading.");
                    json = Utf8.GetString(bytes);
                }
            }
            catch (FileNotFoundException) { return Result(AgeOfWarLoadStatus.Missing, null, null, null, true); }
            catch (DirectoryNotFoundException) { return Result(AgeOfWarLoadStatus.Missing, null, null, null, true); }
            catch (DecoderFallbackException exception) { return Result(AgeOfWarLoadStatus.Corrupt, null, null, exception.Message, false); }
            catch (Exception exception) when (IsStorageFailure(exception))
            { return Result(AgeOfWarLoadStatus.IoError, null, null, exception.Message, false); }

            try
            {
                var data = Decode(json);
                var battle = Restore(data);
                return Result(AgeOfWarLoadStatus.Loaded, data, battle, null, true);
            }
            catch (NotSupportedException exception)
            { return Result(AgeOfWarLoadStatus.UnsupportedVersion, null, null, exception.Message, false); }
            catch (Exception exception) when (IsDataFailure(exception))
            { return Result(AgeOfWarLoadStatus.Corrupt, null, null, exception.Message, false); }
        }

        private static AgeOfWarLoadResult Combine(AgeOfWarLoadResult main, AgeOfWarLoadResult backup)
        {
            if (main.Status == AgeOfWarLoadStatus.UnsupportedVersion || backup.Status == AgeOfWarLoadStatus.UnsupportedVersion)
                return Result(AgeOfWarLoadStatus.UnsupportedVersion, null, null, "战争进化史存档版本不受支持，原文件已保留，当前不会覆盖存档。", false);
            if (main.Status == AgeOfWarLoadStatus.IoError || backup.Status == AgeOfWarLoadStatus.IoError)
                return Result(AgeOfWarLoadStatus.IoError, null, null, "战争进化史存档读取失败，原文件已保留，请重试。", false);
            if (main.Status == AgeOfWarLoadStatus.Loaded) return main;
            if (backup.Status == AgeOfWarLoadStatus.Loaded)
                return Result(AgeOfWarLoadStatus.Recovered, backup.Data, backup.Battle, "战争进化史主存档异常，已恢复最近一次合法备份。", true);
            if (main.Status == AgeOfWarLoadStatus.Missing && backup.Status == AgeOfWarLoadStatus.Missing)
                return Result(AgeOfWarLoadStatus.Missing, null, null, null, true);
            return Result(AgeOfWarLoadStatus.Corrupt, null, null, "战争进化史存档及备份不可用，原文件已保留，当前不会覆盖存档。", false);
        }

        private AgeOfWarSaveData Decode(string json)
        {
            new StrictJsonSyntax(json).Validate();
            JObject root;
            using (var text = new StringReader(json))
            using (var reader = new JsonTextReader(text) { MaxDepth = MaximumDepth, DateParseHandling = DateParseHandling.None })
            {
                root = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new FormatException("Trailing JSON data.");
            }
            var version = root["schemaVersion"];
            if (version != null && version.Type == JTokenType.Integer && version.ToString(Formatting.None) != "1")
                throw new NotSupportedException("Unsupported Age of War save schema.");
            var identity = root["edition"];
            if (identity != null && identity.Type == JTokenType.Integer && identity.ToString(Formatting.None) != edition.ToString(CultureInfo.InvariantCulture))
                throw new NotSupportedException("Age of War save belongs to another edition.");
            if (root.Count != Fields.Length) throw new FormatException("Unexpected or missing save fields.");
            foreach (string field in Fields) if (root.Property(field) == null) throw new FormatException("Missing save field: " + field);
            var data = new AgeOfWarSaveData {
                SchemaVersion = Integer(root["schemaVersion"]), Edition = Integer(root["edition"]),
                BattleState = Binary(root["battleState"]), CameraX = Number(root["cameraX"]), CameraY = Number(root["cameraY"]),
                Menu = Text(root["menu"]), Quality = Text(root["quality"]),
                MusicVolume = Volume(root["musicVolume"]), SoundVolume = Volume(root["soundVolume"]),
                ShowDamage = Boolean(root["showDamage"]), FrameRate = Integer(root["frameRate"])
            };
            Validate(data);
            return data;
        }

        private static byte[] Encode(AgeOfWarSaveData data)
        {
            var root = new JObject {
                ["schemaVersion"] = data.SchemaVersion, ["edition"] = data.Edition,
                ["battleState"] = data.BattleState == null ? JValue.CreateNull() : new JValue(Convert.ToBase64String(data.BattleState)),
                ["cameraX"] = data.CameraX, ["cameraY"] = data.CameraY, ["menu"] = data.Menu, ["quality"] = data.Quality,
                ["musicVolume"] = data.MusicVolume, ["soundVolume"] = data.SoundVolume,
                ["showDamage"] = data.ShowDamage, ["frameRate"] = data.FrameRate
            };
            byte[] bytes = Utf8.GetBytes(root.ToString(Formatting.None));
            if (bytes.Length > MaximumJsonBytes) throw new FormatException("Save exceeds the size limit.");
            return bytes;
        }

        private void Validate(AgeOfWarSaveData data)
        {
            if (data.SchemaVersion != 1 || data.Edition != edition) throw new NotSupportedException("Unsupported save identity or schema.");
            if (data.BattleState != null && (data.BattleState.Length == 0 || data.BattleState.Length > MaximumBattleBytes))
                throw new FormatException("Battle payload must contain between 1 byte and 16 MiB.");
            if (!Finite(data.CameraX) || !Finite(data.CameraY)) throw new FormatException("Camera coordinates must be finite.");
            if (data.Menu != "main" && data.Menu != "units" && data.Menu != "turrets" && data.Menu != "build" && data.Menu != "sell")
                throw new FormatException("Unknown battle menu.");
            if (data.Quality != "LOW" && data.Quality != "MEDIUM" && data.Quality != "HIGH") throw new FormatException("Unknown graphics quality.");
            if (!Finite(data.MusicVolume) || !Finite(data.SoundVolume) || data.MusicVolume < 0 || data.MusicVolume > 1 || data.SoundVolume < 0 || data.SoundVolume > 1)
                throw new FormatException("Audio volume must be between zero and one.");
            if (edition == 1 ? data.FrameRate != 40 : data.FrameRate < 25 || data.FrameRate > 300)
                throw new FormatException("Invalid frame rate for this edition.");
        }

        private static IAgeOfWarBattle Restore(AgeOfWarSaveData data)
        {
            if (data.BattleState == null) return null;
            IAgeOfWarBattle battle = data.Edition == 1 ? (IAgeOfWarBattle)AgeOfWarOneBattle.RestoreState(data.BattleState) : AgeOfWarTwoBattle.RestoreState(data.BattleState);
            if (battle == null || battle.TickRate != data.FrameRate) throw new FormatException("Battle frame rate differs from the save envelope.");
            return battle;
        }

        private static AgeOfWarSaveData Copy(AgeOfWarSaveData data)
        {
            return new AgeOfWarSaveData {
                SchemaVersion = data.SchemaVersion, Edition = data.Edition,
                BattleState = data.BattleState == null ? null : (byte[])data.BattleState.Clone(),
                CameraX = data.CameraX, CameraY = data.CameraY, Menu = data.Menu, Quality = data.Quality,
                MusicVolume = data.MusicVolume, SoundVolume = data.SoundVolume, ShowDamage = data.ShowDamage, FrameRate = data.FrameRate
            };
        }

        private static byte[] Binary(JToken token)
        {
            if (token.Type == JTokenType.Null) return null;
            string value = Text(token);
            if (value.Length == 0 || value.Length > MaximumBase64Length || value.Length % 4 != 0) throw new FormatException("Invalid battle base64 length.");
            byte[] bytes = Convert.FromBase64String(value);
            if (bytes.Length > MaximumBattleBytes || Convert.ToBase64String(bytes) != value) throw new FormatException("Battle payload must use canonical base64 within 16 MiB.");
            return bytes;
        }

        private static string Text(JToken token)
        { if (token.Type != JTokenType.String) throw new FormatException("Expected a JSON string."); return (string)token; }
        private static bool Boolean(JToken token)
        { if (token.Type != JTokenType.Boolean) throw new FormatException("Expected a JSON boolean."); return (bool)token; }
        private static int Integer(JToken token)
        {
            if (token.Type != JTokenType.Integer || !int.TryParse(token.ToString(Formatting.None), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                throw new FormatException("Expected a 32-bit JSON integer.");
            return value;
        }
        private static double Number(JToken token)
        {
            if ((token.Type != JTokenType.Float && token.Type != JTokenType.Integer) ||
                !double.TryParse(token.ToString(Formatting.None), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !Finite(value))
                throw new FormatException("Expected a finite JSON number.");
            return value;
        }
        private static float Volume(JToken token)
        { double value = Number(token); if (value < 0 || value > 1) throw new FormatException("Volume is outside zero to one."); return (float)value; }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static AgeOfWarLoadResult Result(AgeOfWarLoadStatus status, AgeOfWarSaveData data, IAgeOfWarBattle battle, string warning, bool canWrite)
        { return new AgeOfWarLoadResult { Status = status, Data = data, Battle = battle, Warning = warning, CanWrite = canWrite }; }
        private static bool IsDataFailure(Exception exception)
        { return exception is JsonException || exception is FormatException || exception is OverflowException || exception is InvalidDataException || exception is EndOfStreamException || exception is ArgumentException; }
        private static bool IsStorageFailure(Exception exception)
        { return exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException; }

        // Json.NET accepts JavaScript extensions; reject them before reading any save fields.
        private sealed class StrictJsonSyntax
        {
            private readonly string text;
            private int position;
            internal StrictJsonSyntax(string text) { this.text = text; }
            internal void Validate() { Value(0); Space(); if (position != text.Length) Invalid(); }
            private void Value(int depth)
            {
                Space(); if (position >= text.Length) Invalid(); char c = text[position];
                if (c == '{' || c == '[')
                {
                    if (++depth > MaximumDepth) Invalid(); bool obj = c == '{'; position++; Space(); char close = obj ? '}' : ']';
                    if (Take(close)) return;
                    while (true)
                    {
                        if (obj) { String(); Space(); if (!Take(':')) Invalid(); }
                        Value(depth); Space(); if (Take(close)) return; if (!Take(',')) Invalid(); Space();
                    }
                }
                if (c == '"') { String(); return; }
                if (c == 't') { Literal("true"); return; }
                if (c == 'f') { Literal("false"); return; }
                if (c == 'n') { Literal("null"); return; }
                Number();
            }
            private void String()
            {
                if (!Take('"')) Invalid();
                while (position < text.Length)
                {
                    char c = text[position++]; if (c == '"') return; if (c < 32) Invalid();
                    if (c != '\\') continue;
                    if (position >= text.Length) Invalid(); c = text[position++];
                    if (c == 'u')
                    {
                        for (int i = 0; i < 4; i++)
                        { if (position >= text.Length) Invalid(); c = text[position++]; if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))) Invalid(); }
                    }
                    else if (c != '"' && c != '\\' && c != '/' && c != 'b' && c != 'f' && c != 'n' && c != 'r' && c != 't') Invalid();
                }
                Invalid();
            }
            private void Number()
            {
                Take('-');
                if (!Take('0')) { if (position >= text.Length || text[position] < '1' || text[position] > '9') Invalid(); Digits(); }
                if (Take('.')) { if (!Digit()) Invalid(); Digits(); }
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); if (!Digit()) Invalid(); Digits(); }
            }
            private bool Digit() { return position < text.Length && text[position] >= '0' && text[position] <= '9'; }
            private void Digits() { while (Digit()) position++; }
            private void Literal(string value) { foreach (char c in value) if (!Take(c)) Invalid(); }
            private bool Take(char c) { if (position >= text.Length || text[position] != c) return false; position++; return true; }
            private void Space() { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) position++; }
            private static void Invalid() { throw new FormatException("Save is not strict JSON or exceeds its depth limit."); }
        }
    }
}
