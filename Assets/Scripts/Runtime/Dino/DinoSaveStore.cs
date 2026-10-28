using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Dino
{
    public sealed class DinoSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }

        public DinoSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "dino-v1.json");
        }

        public DinoSaveData Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var data, out var primaryError)) return data;
            if (TryRead(SavePath + ".bak", out data, out var backupError))
            {
                warning = "Dino 纪录文件异常，已读取最近一次备份。";
                return data;
            }
            if (primaryError != null || backupError != null) warning = "Dino 纪录无法读取，已使用默认纪录与方向。";
            return new DinoSaveData();
        }

        public bool TrySave(DinoSaveData data, out string error)
        {
            error = null;
            var temporary = SavePath + ".tmp";
            var ownsTemporary = false;
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(DinoSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    ownsTemporary = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    var previousIsValid = TryRead(SavePath, out var previous, out var readError);
                    // Keep a known-good backup when replacing a corrupt primary.
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
            finally
            {
                if (ownsTemporary)
                {
                    try { File.Delete(temporary); }
                    catch (Exception exception) when (IsStorageFailure(exception)) { }
                }
            }
        }

        private static bool TryRead(string path, out DinoSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                // Read at most one byte beyond the limit, even if another process grows the file.
                var buffer = new byte[DinoSaveCodec.MaximumLength + 1];
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
                if (count > DinoSaveCodec.MaximumLength)
                {
                    error = "Dino save file is too large.";
                    return false;
                }
                return DinoSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
            }
            catch (Exception exception) when (exception is FileNotFoundException || exception is DirectoryNotFoundException)
            {
                return false;
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
