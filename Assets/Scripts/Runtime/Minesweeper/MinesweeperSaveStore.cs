using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Minesweeper
{
    public sealed class MinesweeperSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }
        public MinesweeperSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "minesweeper-v1.json");
        }

        public MinesweeperSaveData Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var data, out var mainError)) return data;
            if (TryRead(SavePath + ".bak", out data, out var backupError))
            {
                warning = "扫雷存档异常，已恢复最近一次完整备份。";
                return data;
            }
            if (mainError != null || backupError != null) warning = "扫雷进度无法恢复，请开始新局。其他游戏不受影响。";
            return null;
        }

        public bool TrySave(MinesweeperSaveData data, out string error)
        {
            error = null;
            try
            {
                // Reject invalid state before touching the last committed primary or backup.
                var bytes = new UTF8Encoding(false).GetBytes(MinesweeperSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                var temporary = SavePath + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    var previousIsValid = TryRead(SavePath, out var previous, out var readError);
                    // After backup recovery, never promote a corrupt primary into the valid backup.
                    File.Replace(temporary, SavePath, previousIsValid ? SavePath + ".bak" : null);
                }
                else File.Move(temporary, SavePath);
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryRead(string path, out MinesweeperSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                var buffer = new byte[MinesweeperSaveCodec.MaximumLength + 1];
                var count = 0;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    while (count < buffer.Length)
                    {
                        var read = stream.Read(buffer, count, buffer.Length - count);
                        if (read == 0) break;
                        count += read;
                    }
                }
                if (count > MinesweeperSaveCodec.MaximumLength)
                {
                    error = "Minesweeper save file is too large.";
                    return false;
                }
                return MinesweeperSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
            }
            // File.Exists suppresses access failures; only an actual missing-path exception is first use.
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is DecoderFallbackException)
            {
                error = exception.Message;
                return false;
            }
        }
        private static bool IsStorageFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is System.Security.SecurityException || exception is NotSupportedException;
        }
    }
}
