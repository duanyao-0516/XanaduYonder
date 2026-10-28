using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.HappyBall
{
    public sealed class HappyBallSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }
        public HappyBallSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "happy-ball-v1.json");
        }

        public HappyBallSaveData Load(out string warning)
        {
            warning = null;
            if (TryRead(SavePath, out var data, out var mainError)) return data;
            if (TryRead(SavePath + ".bak", out data, out var backupError))
            {
                warning = "欢乐球球存档异常，已恢复最近一次备份。";
                return data;
            }
            if (mainError != null || backupError != null) warning = "欢乐球球存档无法读取，已为你开启新的一局。";
            return null;
        }

        public bool TrySave(HappyBallSaveData data, out string error)
        {
            error = null;
            try
            {
                // Reject invalid state before touching the last committed primary or backup.
                var bytes = new UTF8Encoding(false).GetBytes(HappyBallSaveCodec.Encode(data));
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

        private static bool TryRead(string path, out HappyBallSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                var buffer = new byte[HappyBallSaveCodec.MaximumLength + 1];
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
                if (count > HappyBallSaveCodec.MaximumLength)
                {
                    error = "HappyBall save file is too large.";
                    return false;
                }
                return HappyBallSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
            }
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
