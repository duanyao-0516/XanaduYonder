using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime
{
    public sealed class LocalSaveStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }

        public LocalSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "xanaduyonder-v1.json");
        }

        public SaveData Load(out string warning)
        {
            warning = null;
            SaveData data;
            string mainError;
            if (TryRead(SavePath, out data, out mainError)) return data;
            string backupError;
            if (TryRead(SavePath + ".bak", out data, out backupError))
            {
                warning = "上次存档异常，已恢复最近一次备份。";
                return data;
            }
            if (mainError != null || backupError != null) warning = "存档无法读取，已为你开启新的一局。";
            return null;
        }

        public bool TrySave(SaveData data, out string error)
        {
            error = null;
            var temporary = SavePath + ".tmp";
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(JsonSaveCodec.Encode(data));
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    SaveData previous;
                    string readError;
                    var previousIsValid = TryRead(SavePath, out previous, out readError);
                    // Never replace the last good backup with a corrupt primary file.
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

        private static bool TryRead(string path, out SaveData data, out string error)
        {
            data = null;
            error = null;
            if (!File.Exists(path)) return false;
            try
            {
                if (new FileInfo(path).Length > JsonSaveCodec.MaximumLength)
                {
                    error = "Save file is too large.";
                    return false;
                }
                return JsonSaveCodec.TryDecode(File.ReadAllText(path, Encoding.UTF8), out data, out error);
            }
            catch (Exception exception) when (IsStorageFailure(exception))
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
