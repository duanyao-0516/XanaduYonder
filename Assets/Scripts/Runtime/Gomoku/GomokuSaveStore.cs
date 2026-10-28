using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Gomoku
{
    public enum GomokuLoadStatus { Missing, Loaded, Recovered, Invalid }

    public sealed class GomokuSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }
        public GomokuLoadStatus LastLoadStatus { get; private set; }

        public GomokuSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "gomoku-v1.json");
        }

        public GomokuSaveData Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var data, out var mainError))
            { LastLoadStatus = GomokuLoadStatus.Loaded; return data; }
            if (TryRead(SavePath + ".bak", out data, out var backupError))
            {
                LastLoadStatus = GomokuLoadStatus.Recovered;
                warning = "五子棋存档异常，已恢复最近一次完整备份。";
                return data;
            }
            LastLoadStatus = mainError == null && backupError == null ? GomokuLoadStatus.Missing : GomokuLoadStatus.Invalid;
            if (LastLoadStatus == GomokuLoadStatus.Invalid) warning = "五子棋进度无法恢复，请确认开始新局。其他游戏不受影响。";
            return null;
        }

        public bool TrySave(GomokuSaveData data, out string error)
        {
            error = null;
            try
            {
                // Validate before opening files. Only committed, valid main data may replace the backup.
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(GomokuSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                string temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    bool previousIsValid = TryRead(SavePath, out var previous, out var readError);
                    File.Replace(temporary, SavePath, previousIsValid ? SavePath + ".bak" : null);
                }
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is ArgumentException)
            { error = exception.Message; return false; }
        }

        private static bool TryRead(string path, out GomokuSaveData data, out string error)
        {
            data = null; error = null;
            try
            {
                var buffer = new byte[GomokuSaveCodec.MaximumLength + 1];
                int count = 0;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    while (count < buffer.Length)
                    {
                        int read = stream.Read(buffer, count, buffer.Length - count);
                        if (read == 0) break;
                        count += read;
                    }
                }
                if (count > GomokuSaveCodec.MaximumLength) { error = "Gomoku save file is too large."; return false; }
                return GomokuSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
            }
            // File.Exists hides access failures. Only a genuinely absent path means first use.
            catch (FileNotFoundException) { error = MissingPathError(path); return false; }
            catch (DirectoryNotFoundException) { error = MissingPathError(path); return false; }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is DecoderFallbackException || exception is ArgumentException)
            { error = exception.Message; return false; }
        }

        private static string MissingPathError(string path)
        {
            // Some runtimes report an ancestor regular file as "directory not found".
            // Distinguish this configuration/access failure from a truly absent save folder.
            try
            {
                string parent = Path.GetDirectoryName(Path.GetFullPath(path));
                while (!string.IsNullOrEmpty(parent))
                {
                    try
                    {
                        var attributes = File.GetAttributes(parent);
                        return (attributes & FileAttributes.Directory) != 0 ? null : "Save path has a non-directory ancestor.";
                    }
                    catch (FileNotFoundException) { }
                    catch (DirectoryNotFoundException) { }
                    parent = Path.GetDirectoryName(parent);
                }
                return null;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is ArgumentException)
            { return exception.Message; }
        }

        private static bool IsStorageFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException || exception is NotSupportedException;
        }
    }
}
