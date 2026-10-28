using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.LinkMatch
{
    /// <summary>Only a fully validated primary may replace the backup; write failure leaves the caller's live profile intact.</summary>
    public sealed class LinkMatchSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }
        public LinkMatchSaveStore(string directory)
        { if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing save directory.", nameof(directory)); this.directory = directory; SavePath = Path.Combine(directory, "link-match-v1.json"); }
        public LinkMatchSaveData Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var data, out var mainError)) return data;
            if (mainError != null) TryArchive(SavePath);
            if (TryRead(SavePath + ".bak", out data, out var backupError)) { warning = "连连看存档异常，已恢复最近一次完整备份。"; return data; }
            if (backupError != null) TryArchive(SavePath + ".bak");
            if (mainError != null || backupError != null) warning = "连连看进度无法恢复，已尝试保留异常文件，请重新开始。其他游戏不受影响。";
            return null;
        }
        public bool TrySave(LinkMatchSaveData data, out string error)
        {
            error = null;
            try
            {
                var bytes = new UTF8Encoding(false, true).GetBytes(LinkMatchSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                // Saves are small (six boards of at most 48 cells). Revalidate the actual bytes every
                // time, including after external edits, instead of trusting a cached valid flag.
                bool priorValid = TryRead(SavePath, out var prior, out var priorError);
                if (priorError != null) Archive(SavePath);
                string temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, priorValid ? SavePath + ".bak" : null);
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception exception) when (StorageFailure(exception) || exception is ArgumentException)
            { error = exception.Message; return false; }
        }
        private static bool TryRead(string path, out LinkMatchSaveData data, out string error)
        {
            data = null; error = null;
            try
            {
                var bytes = new byte[LinkMatchSaveCodec.MaximumLength + 1]; int count = 0;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                { while (count < bytes.Length) { int read = stream.Read(bytes, count, bytes.Length - count); if (read == 0) break; count += read; } }
                if (count > LinkMatchSaveCodec.MaximumLength) { error = "Link Match save exceeds 128 KiB."; return false; }
                return LinkMatchSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(bytes, 0, count), out data, out error);
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception exception) when (StorageFailure(exception) || exception is DecoderFallbackException) { error = exception.Message; return false; }
        }
        private static void TryArchive(string path) { try { Archive(path); } catch (Exception exception) when (StorageFailure(exception)) { } }
        private static void Archive(string path) { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N"), false); }
        private static bool StorageFailure(Exception exception) { return exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException; }
    }
}
