using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Sudoku
{
    public sealed class SudokuSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }

        public SudokuSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "sudoku-v1.json");
        }

        public SudokuSaveData Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var data, out var mainError)) return data;
            if (TryRead(SavePath + ".bak", out data, out var backupError))
            {
                warning = "数独存档异常，已恢复最近一次备份。";
                return data;
            }
            if (mainError != null || backupError != null) warning = "数独存档无法读取，请重新选择关卡。";
            return null;
        }

        public bool TrySave(SudokuSaveData data, out string error)
        {
            error = null;
            var temporary = SavePath + ".tmp";
            try
            {
                // Validate before touching any file; failed model updates retain the last committed game.
                var bytes = new UTF8Encoding(false).GetBytes(SudokuSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    var previousIsValid = TryRead(SavePath, out var previous, out var readError);
                    // A corrupt primary must never overwrite the last good backup.
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

        private static bool TryRead(string path, out SudokuSaveData data, out string error)
        {
            data = null;
            error = null;
            if (!File.Exists(path)) return false;
            try
            {
                // Use a small buffer for ordinary games, while bounding bytes actually read even if a file grows.
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var bytes = new MemoryStream())
                {
                    if (stream.Length > SudokuSaveCodec.MaximumLength)
                    {
                        error = "Sudoku save file is too large.";
                        return false;
                    }
                    var buffer = new byte[4096];
                    while (true)
                    {
                        var allowance = SudokuSaveCodec.MaximumLength - (int)bytes.Length + 1;
                        var read = stream.Read(buffer, 0, Math.Min(buffer.Length, allowance));
                        if (read == 0) break;
                        if (bytes.Length + read > SudokuSaveCodec.MaximumLength)
                        {
                            error = "Sudoku save file is too large.";
                            return false;
                        }
                        bytes.Write(buffer, 0, read);
                    }
                    return SudokuSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(bytes.GetBuffer(), 0, (int)bytes.Length), out data, out error);
                }
            }
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
