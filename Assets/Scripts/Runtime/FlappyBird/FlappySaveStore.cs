using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.FlappyBird
{
    public sealed class FlappySaveStore
    {
        private readonly string directory;
        private long knownBestScore;
        private bool requiresNewRecord;
        public string SavePath { get; private set; }

        public FlappySaveStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "flappy-bird-v1.json");
        }

        public FlappySaveData Load(out string warning)
        {
            warning = null;
            var primaryValid = TryRead(SavePath, out var primary, out var primaryError);
            var backupValid = TryRead(SavePath + ".bak", out var backup, out var backupError);
            // A highest-score record is monotonic. An older but readable primary
            // must not hide a better valid backup that TrySave already protects.
            if (primaryValid && (!backupValid || primary.BestScore >= backup.BestScore))
            {
                knownBestScore = Math.Max(knownBestScore, primary.BestScore);
                return primary;
            }
            if (backupValid)
            {
                knownBestScore = Math.Max(knownBestScore, backup.BestScore);
                warning = primaryValid ? "Flappy Bird 主纪录低于备份，已恢复备份中的最高纪录。" : "Flappy Bird 纪录文件异常，已读取最近一次备份。";
                return backup;
            }
            requiresNewRecord |= primaryError != null || backupError != null;
            if (requiresNewRecord)
                warning = "Flappy Bird 纪录无法读取，暂以 0 分显示；获得新纪录后才能重新保存。";
            return new FlappySaveData();
        }

        public bool TrySave(FlappySaveData data, out string error)
        {
            error = null;
            var temporary = SavePath + ".tmp";
            var ownsTemporary = false;
            try
            {
                // Validate and snapshot before touching the directory or any existing file.
                var snapshot = data == null ? null : data.Copy();
                var bytes = new UTF8Encoding(false).GetBytes(FlappySaveCodec.Encode(snapshot));
                var primaryValid = TryRead(SavePath, out var primary, out var primaryError);
                var backupValid = TryRead(SavePath + ".bak", out var backup, out var backupError);
                requiresNewRecord |= !primaryValid && !backupValid && (primaryError != null || backupError != null);
                var minimumScore = Math.Max(knownBestScore, Math.Max(primaryValid ? primary.BestScore : 0, backupValid ? backup.BestScore : 0));
                if (snapshot.BestScore < minimumScore)
                    throw new ArgumentException("不能用较低分数覆盖已知的 Flappy Bird 最高纪录。", nameof(data));
                // A failed load is not permission to replace an unknown record with a UI default.
                // With no trusted record, the first positive gameplay record starts a new history.
                if (!primaryValid && !backupValid && requiresNewRecord && snapshot.BestScore == 0)
                    throw new ArgumentException("原纪录无法读取，获得新的正分纪录后才能重新保存。", nameof(data));
                if (primaryValid && primary.BestScore == snapshot.BestScore)
                {
                    knownBestScore = Math.Max(knownBestScore, snapshot.BestScore);
                    requiresNewRecord = false;
                    return true;
                }

                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    ownsTemporary = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    // Only a valid primary may replace a backup. Never downgrade a better backup.
                    var backupPath = primaryValid && (!backupValid || primary.BestScore >= backup.BestScore) ? SavePath + ".bak" : null;
                    File.Replace(temporary, SavePath, backupPath);
                }
                else File.Move(temporary, SavePath);
                knownBestScore = Math.Max(knownBestScore, snapshot.BestScore);
                requiresNewRecord = false;
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

        private static bool TryRead(string path, out FlappySaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                // Read at most one byte beyond the cap, including if a file grows during the read.
                var buffer = new byte[FlappySaveCodec.MaximumLength + 1];
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
                if (count > FlappySaveCodec.MaximumLength)
                {
                    error = "Flappy Bird save file is too large.";
                    return false;
                }
                return FlappySaveCodec.TryDecode(new UTF8Encoding(false, true).GetString(buffer, 0, count), out data, out error);
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
