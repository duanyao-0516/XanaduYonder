using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

namespace XanaduYonder.Runtime.SlidingPuzzle
{
    public sealed class SlidingPuzzleSaveStore
    {
        private readonly string directory;
        private bool primaryChecked, primaryValid, primaryArchived;
        private string primaryFingerprint;
        public string SavePath { get; private set; }
        public SlidingPuzzleSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing save directory.", nameof(directory));
            this.directory = directory; SavePath = Path.Combine(directory, "sliding-puzzle-v1.json");
        }
        public SlidingPuzzleSaveData Load(out string warning)
        {
            warning = null; primaryChecked = true; primaryArchived = false;
            primaryValid = TryRead(SavePath, out var data, out var mainError, out primaryFingerprint);
            if (primaryValid) { warning = null; return data; }
            if (mainError != null) primaryArchived = TryArchive(SavePath);
            if (TryRead(SavePath + ".bak", out data, out var backupError))
            { warning = "数字华容道存档异常，已恢复最近一次完整备份。"; return data; }
            if (backupError != null) TryArchive(SavePath + ".bak");
            if (mainError != null || backupError != null) warning = "数字华容道进度无法恢复，已保留异常文件，请开始新关卡。其他游戏不受影响。";
            return null;
        }
        public bool TrySave(SlidingPuzzleSaveData data, out string error)
        {
            error = null;
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(SlidingPuzzleSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                string currentFingerprint = FingerprintFile(SavePath);
                if (!primaryChecked || currentFingerprint != primaryFingerprint)
                {
                    primaryValid = TryRead(SavePath, out var previous, out var priorError, out primaryFingerprint);
                    primaryChecked = true; primaryArchived = false;
                }
                // Revalidate the old primary only after external changes, before choosing a backup source.
                if (File.Exists(SavePath) && !primaryValid && !primaryArchived) { Archive(SavePath); primaryArchived = true; }
                string temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (File.Exists(SavePath)) File.Replace(temporary, SavePath, primaryValid ? SavePath + ".bak" : null);
                else File.Move(temporary, SavePath);
                primaryChecked = primaryValid = true; primaryArchived = false; primaryFingerprint = Fingerprint(bytes, bytes.Length); return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is ArgumentException)
            { error = exception.Message; return false; }
        }
        private bool TryRead(string path, out SlidingPuzzleSaveData data, out string error)
        { return TryRead(path, out data, out error, out var fingerprint); }
        private bool TryRead(string path, out SlidingPuzzleSaveData data, out string error, out string fingerprint)
        {
            data = null; error = null; fingerprint = null;
            try
            {
                var buffer = new byte[SlidingPuzzleSaveCodec.MaximumLength + 1]; int count = 0;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                { while (count < buffer.Length) { int read = stream.Read(buffer, count, buffer.Length - count); if (read == 0) break; count += read; } }
                fingerprint = Fingerprint(buffer, count);
                if (count > SlidingPuzzleSaveCodec.MaximumLength) { error = "SlidingPuzzle save exceeds 2 MiB."; return false; }
                return SlidingPuzzleSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is DecoderFallbackException) { error = exception.Message; return false; }
        }
        private static string FingerprintFile(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var hash = SHA256.Create())
                {
                    // An oversized file is always revalidated; ordinary saves hash with a small streaming buffer.
                    if (stream.Length > SlidingPuzzleSaveCodec.MaximumLength) return "oversized";
                    return Convert.ToBase64String(hash.ComputeHash(stream));
                }
            }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }
        private static string Fingerprint(byte[] bytes, int count)
        { using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(bytes, 0, count)); }
        private static bool TryArchive(string path)
        { try { Archive(path); return true; } catch (Exception exception) when (IsStorageFailure(exception)) { return false; } }
        private static void Archive(string path)
        { File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N"), false); }
        private static bool IsStorageFailure(Exception exception)
        { return exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException; }
    }
}
