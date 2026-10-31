using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.AircraftFire
{
    public sealed class AircraftFireSaveStore
    {
        private readonly string directory;
        private readonly AircraftFireSaveData known = new AircraftFireSaveData();
        private bool requiresNewRecord;
        public string SavePath { get; private set; }

        public AircraftFireSaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "aircraft-fire-v1.json");
        }

        public AircraftFireSaveData Load(out string warning)
        {
            warning = null;
            var primaryValid = TryRead(SavePath, out var primary, out var primaryError);
            var backupValid = TryRead(SavePath + ".bak", out var backup, out var backupError);
            if (primaryValid || backupValid)
            {
                known.Merge(primary);
                known.Merge(backup);
                if (!primaryValid || !primary.Contains(known))
                    warning = "飞机大战纪录已从备份恢复，各模式保留最高成绩。";
                return known.Copy();
            }
            requiresNewRecord |= primaryError != null || backupError != null;
            if (requiresNewRecord)
                warning = "飞机大战纪录无法读取，暂显示已知成绩；获得新的正分纪录后才能保存。";
            return known.Copy();
        }

        public bool TrySave(AircraftFireSaveData data, out string error)
        {
            error = null;
            try
            {
                // Validate and snapshot before any filesystem mutation.
                var snapshot = data == null ? null : data.Copy();
                var encoded = AircraftFireSaveCodec.Encode(snapshot);
                var primaryValid = TryRead(SavePath, out var primary, out var primaryError);
                var backupValid = TryRead(SavePath + ".bak", out var backup, out var backupError);
                requiresNewRecord |= !primaryValid && !backupValid && (primaryError != null || backupError != null);
                var previous = known.Copy();
                previous.Merge(primary);
                previous.Merge(backup);
                if (!snapshot.Contains(previous))
                    throw new ArgumentException("不能用较低成绩或关卡覆盖已知的飞机大战纪录。", nameof(data));
                if (!primaryValid && !backupValid && requiresNewRecord && snapshot.BestScore == 0)
                    throw new ArgumentException("原纪录无法读取，获得新的正分纪录后才能重新保存。", nameof(data));
                if (primaryValid && primary.Contains(snapshot))
                {
                    known.Merge(snapshot);
                    requiresNewRecord = false;
                    return true;
                }

                Directory.CreateDirectory(directory);
                // The two files can contain better scores for different modes.
                // Preserve their combined maxima before atomically replacing primary.
                // If interrupted here, either existing primary or backup remains valid.
                if (primaryValid || backupValid || known.BestScore > 0 || known.HighestStage > 0)
                    WriteAtomic(SavePath + ".bak", AircraftFireSaveCodec.Encode(previous));
                WriteAtomic(SavePath, encoded);
                known.Merge(snapshot);
                requiresNewRecord = false;
                return true;
            }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is ArgumentException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void WriteAtomic(string path, string content)
        {
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            var ownsTemporary = false;
            try
            {
                var bytes = new UTF8Encoding(false).GetBytes(content);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsTemporary = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
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

        private static bool TryRead(string path, out AircraftFireSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                var buffer = new byte[AircraftFireSaveCodec.MaximumLength + 1];
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
                if (count > AircraftFireSaveCodec.MaximumLength)
                {
                    error = "Aircraft Fire save file is too large.";
                    return false;
                }
                return AircraftFireSaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
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
