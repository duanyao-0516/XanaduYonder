using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Snake
{
    public sealed class SnakeSaveStore
    {
        private readonly string directory;
        private readonly SnakeCatalog catalog;
        public string SavePath { get; private set; }
        public SnakeSaveStore(string directory, SnakeCatalog catalog)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing save directory.", nameof(directory));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.directory = directory; this.catalog = catalog; SavePath = Path.Combine(directory, "snake-v1.json");
        }
        public SnakeProfile Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var json, out var mainError))
            {
                if (SnakeSaveCodec.TryDecode(json, catalog, out var current, out mainError)) return current;
                if (SnakeSaveCodec.IsIncompatible(json, catalog))
                {
                    SnakeSaveCodec.TryRecoverRecords(json, catalog, out var records);
                    warning = "贪吃蛇存档版本不兼容，旧文件已保留；已恢复可验证的纪录，请开始新局。";
                    return records ?? new SnakeProfile(catalog);
                }
            }
            if (TryRead(SavePath + ".bak", out var backupJson, out var backupError))
            {
                if (SnakeSaveCodec.TryDecode(backupJson, catalog, out var backup, out backupError))
                { warning = "贪吃蛇存档异常，已恢复最近一次完整备份。"; return backup; }
                if (SnakeSaveCodec.IsIncompatible(backupJson, catalog))
                {
                    SnakeSaveCodec.TryRecoverRecords(backupJson, catalog, out var records);
                    warning = "贪吃蛇进度无法恢复，旧版备份已保留；已恢复可验证的纪录，请开始新局。";
                    return records ?? new SnakeProfile(catalog);
                }
            }
            if (mainError != null || backupError != null) warning = "贪吃蛇进度无法恢复，请开始新局。其他游戏不受影响。";
            return null;
        }
        public bool TrySave(SnakeProfile profile, out string error)
        {
            error = null; string temporary = SavePath + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(SnakeSaveCodec.Encode(profile));
                Directory.CreateDirectory(directory);
                bool previousIsValid = false;
                if (TryRead(SavePath, out var previous, out var readError))
                {
                    previousIsValid = SnakeSaveCodec.TryDecode(previous, catalog, out var ignored, out var ignoredReason);
                    if (!previousIsValid && SnakeSaveCodec.IsIncompatible(previous, catalog))
                    {
                        // Keep original bytes before the user starts a game under a new content version.
                        Archive(SavePath);
                    }
                }
                if (previousIsValid && TryRead(SavePath + ".bak", out var backup, out var backupError) && SnakeSaveCodec.IsIncompatible(backup, catalog))
                    Archive(SavePath + ".bak");
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, previousIsValid ? SavePath + ".bak" : null);
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || SnakeJson.IsInvalid(exception) || exception is InvalidOperationException)
            { error = "贪吃蛇保存失败：" + exception.Message; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) when (IsStorageFailure(exception)) { }
            }
        }
        private static void Archive(string path)
        {
            string archive = path + ".incompatible-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N");
            File.Copy(path, archive, false);
        }
        private static bool TryRead(string path, out string json, out string error)
        {
            json = null; error = null;
            try
            {
                var buffer = new byte[SnakeSaveCodec.MaximumLength + 1]; int count = 0;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    while (count < buffer.Length)
                    { int read = stream.Read(buffer, count, buffer.Length - count); if (read == 0) break; count += read; }
                }
                if (count > SnakeSaveCodec.MaximumLength) { error = "Snake save is too large."; return false; }
                json = new UTF8Encoding(false, true).GetString(buffer, 0, count); return true;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is DecoderFallbackException)
            { error = exception.Message; return false; }
        }
        private static bool IsStorageFailure(Exception exception)
        { return exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException; }
    }
}
