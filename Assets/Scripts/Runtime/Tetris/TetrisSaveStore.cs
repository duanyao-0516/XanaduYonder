using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Tetris
{
    public enum TetrisLoadStatus { Missing, Loaded, Recovered, Corrupt, UnsupportedVersion, IoError }

    public sealed class TetrisLoadResult
    {
        public TetrisLoadStatus Status;
        public TetrisSaveData Data;
        public string Message;
        public bool CanWrite;
    }

    public sealed class TetrisSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }

        public TetrisSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "tetris-v1.json");
        }

        public TetrisLoadResult Load()
        {
            var main = Read(SavePath);
            var backup = Read(SavePath + ".bak");
            return Combine(main, backup);
        }

        public bool TrySave(TetrisSaveData data, out string error)
        {
            error = null;
            if (!TetrisSaveCodec.TryEncode(data, out var json, out error)) return false;
            try
            {
                // Inspect both slots on every write, even when the primary remains valid.
                var main = Read(SavePath); var backup = Read(SavePath + ".bak");
                var permission = Combine(main, backup);
                if (!permission.CanWrite) { error = permission.Message; return false; }
                Directory.CreateDirectory(directory);
                string temporary = SavePath + ".tmp";
                byte[] bytes = new UTF8Encoding(false, true).GetBytes(json);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                // Recheck after flushing so a newly discovered future backup cannot be rotated away.
                main = Read(SavePath); backup = Read(SavePath + ".bak"); permission = Combine(main, backup);
                if (!permission.CanWrite) { error = permission.Message; return false; }
                if (main.Status == TetrisLoadStatus.Missing) File.Move(temporary, SavePath);
                else
                {
                    // Recovery retains the last legal backup; a bad primary must never replace it.
                    string rotateTo = main.Status == TetrisLoadStatus.Loaded ? SavePath + ".bak" : null;
                    File.Replace(temporary, SavePath, rotateTo);
                }
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            { error = "俄罗斯方块未保存：" + exception.Message; return false; }
        }

        public bool TryResetCorrupt(out string error)
        {
            error = null;
            try
            {
                var main = Read(SavePath); var backup = Read(SavePath + ".bak");
                var state = Combine(main, backup);
                if (state.Status != TetrisLoadStatus.Corrupt)
                { error = "只能重置已确认损坏且没有合法备份的俄罗斯方块存档。"; return false; }
                // Unique same-directory archives preserve the actual bytes for diagnosis.
                string suffix = ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
                if (main.Status == TetrisLoadStatus.Corrupt) File.Move(SavePath, SavePath + suffix);
                if (backup.Status == TetrisLoadStatus.Corrupt) File.Move(SavePath + ".bak", SavePath + ".bak" + suffix);
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception))
            { error = "无法归档损坏的俄罗斯方块存档：" + exception.Message; return false; }
        }

        private static TetrisLoadResult Combine(TetrisLoadResult main, TetrisLoadResult backup)
        {
            if (main.Status == TetrisLoadStatus.UnsupportedVersion || backup.Status == TetrisLoadStatus.UnsupportedVersion)
                return Result(TetrisLoadStatus.UnsupportedVersion, null, "俄罗斯方块存档版本不受支持，原文件已保留。请返回或使用支持该版本的应用。", false);
            if (main.Status == TetrisLoadStatus.IoError || backup.Status == TetrisLoadStatus.IoError)
                return Result(TetrisLoadStatus.IoError, null, "俄罗斯方块存档读取失败，原文件已保留。请重试。", false);
            if (main.Status == TetrisLoadStatus.Loaded)
                return Result(TetrisLoadStatus.Loaded, main.Data, null, true);
            if (backup.Status == TetrisLoadStatus.Loaded)
                return Result(TetrisLoadStatus.Recovered, backup.Data, "俄罗斯方块主存档异常，已恢复最近一次合法备份。", true);
            if (main.Status == TetrisLoadStatus.Missing && backup.Status == TetrisLoadStatus.Missing)
                return Result(TetrisLoadStatus.Missing, new TetrisSaveData(), null, true);
            return Result(TetrisLoadStatus.Corrupt, null, "俄罗斯方块存档及备份不可用。原文件已保留；确认重置后会先归档再开始。", false);
        }

        private static TetrisLoadResult Read(string path)
        {
            try
            {
                var buffer = new byte[TetrisSaveCodec.MaximumLength + 1]; int count = 0;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    while (count < buffer.Length)
                    {
                        int read = stream.Read(buffer, count, buffer.Length - count); if (read == 0) break; count += read;
                    }
                }
                if (count > TetrisSaveCodec.MaximumLength) return Result(TetrisLoadStatus.Corrupt, null, "Tetris save exceeds the size limit.", false);
                string json = new UTF8Encoding(false, true).GetString(buffer, 0, count);
                if (TetrisSaveCodec.TryDecode(json, out var data, out var status, out var error)) return Result(TetrisLoadStatus.Loaded, data, null, true);
                return Result(status, null, error, false);
            }
            catch (FileNotFoundException) { return Result(TetrisLoadStatus.Missing, null, null, true); }
            catch (DirectoryNotFoundException) { return Result(TetrisLoadStatus.Missing, null, null, true); }
            catch (DecoderFallbackException exception) { return Result(TetrisLoadStatus.Corrupt, null, exception.Message, false); }
            catch (Exception exception) when (IsStorageFailure(exception))
            { return Result(TetrisLoadStatus.IoError, null, exception.Message, false); }
        }
        private static TetrisLoadResult Result(TetrisLoadStatus status, TetrisSaveData data, string message, bool canWrite)
        { return new TetrisLoadResult { Status = status, Data = data, Message = message, CanWrite = canWrite }; }
        private static bool IsStorageFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is System.Security.SecurityException || exception is NotSupportedException;
        }
    }
}
