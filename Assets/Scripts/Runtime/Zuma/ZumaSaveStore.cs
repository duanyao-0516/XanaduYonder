using System;
using System.IO;
using System.Text;
using XanaduYonder.Core.Zuma;

namespace XanaduYonder.Runtime.Zuma
{
    public enum ZumaLoadStatus { Missing, Loaded, RecoveredFromBackup, Corrupt, UnsupportedVersion, IoError }

    public sealed class ZumaLoadResult
    {
        public ZumaLoadStatus Status;
        public ZumaSaveData Data;
        public string Warning;
    }

    public sealed class ZumaSaveResult
    {
        public bool Success;
        public string Error;
    }

    // One synchronous writer, owned by the Zuma page. Every commit rechecks both disk versions.
    public sealed class ZumaSaveStore
    {
        private readonly string directory;
        private readonly Func<string, ZumaLevelDefinition> levelResolver;
        private readonly object gate = new object();
        private ZumaLoadStatus status;
        public string SavePath { get; private set; }
        public bool IsWriteProtected { get { return Protected(status); } }

        public ZumaSaveStore(string directory, Func<string, ZumaLevelDefinition> levelResolver = null)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing Zuma save directory.", nameof(directory));
            this.directory = directory;
            this.levelResolver = levelResolver;
            SavePath = Path.Combine(directory, "zuma-v1.json");
        }

        public ZumaLoadResult Load()
        {
            lock (gate) return LoadInternal();
        }

        private ZumaLoadResult LoadInternal()
        {
            var main = Read(SavePath);
            if (main.Status == ZumaLoadStatus.Loaded || main.Status == ZumaLoadStatus.UnsupportedVersion || main.Status == ZumaLoadStatus.IoError)
                return FinishLoad(main);
            var backup = Read(SavePath + ".bak");
            if (backup.Status == ZumaLoadStatus.Loaded)
            {
                backup.Status = ZumaLoadStatus.RecoveredFromBackup;
                backup.Warning = "Zuma主存档异常，已恢复最近一次备份。";
                return FinishLoad(backup);
            }
            if (backup.Status == ZumaLoadStatus.UnsupportedVersion || backup.Status == ZumaLoadStatus.IoError)
                return FinishLoad(backup);
            if (main.Status == ZumaLoadStatus.Missing && backup.Status == ZumaLoadStatus.Missing)
                return FinishLoad(new ZumaLoadResult { Status = ZumaLoadStatus.Missing, Data = ZumaSaveData.CreateNew() });
            return FinishLoad(new ZumaLoadResult { Status = ZumaLoadStatus.Corrupt, Warning = "Zuma存档已损坏。可退出或明确确认重置；原文件尚未改动。" });
        }

        private ZumaLoadResult FinishLoad(ZumaLoadResult result)
        {
            status = result.Status;
            return result;
        }

        public ZumaSaveResult Save(ZumaSaveData data)
        {
            lock (gate)
            {
                try
                {
                    // Encode a detached, fully validated snapshot before touching any files.
                    var bytes = new UTF8Encoding(false, true).GetBytes(ZumaSaveCodec.Encode(data, levelResolver));
                    var loaded = LoadInternal();
                    if (Protected(loaded.Status)) return Failure(loaded.Warning);
                    // A valid main wins when loading, but may not rotate over a future or unreadable backup.
                    var backup = Read(SavePath + ".bak");
                    if (backup.Status == ZumaLoadStatus.UnsupportedVersion || backup.Status == ZumaLoadStatus.IoError)
                    {
                        status = backup.Status;
                        return Failure(backup.Warning);
                    }
                    Write(bytes, loaded.Status == ZumaLoadStatus.Loaded);
                    status = ZumaLoadStatus.Loaded;
                    return new ZumaSaveResult { Success = true };
                }
                catch (Exception exception) when (StorageFailure(exception) || exception is ArgumentException || exception is FormatException)
                {
                    return Failure("Zuma进度未保存：" + exception.Message);
                }
            }
        }

        public bool TrySave(ZumaSaveData data, out string error)
        {
            var result = Save(data);
            error = result.Error;
            return result.Success;
        }

        // Call only after the user explicitly confirms resetting a Corrupt result. Future versions
        // and unknown I/O state have no reset shortcut. Archives are created before replacing data.
        public ZumaSaveResult ResetCorrupt()
        {
            lock (gate)
            {
                try
                {
                    var loaded = LoadInternal();
                    if (loaded.Status != ZumaLoadStatus.Corrupt)
                        return Failure("仅明确损坏的Zuma存档可重置。");
                    var bytes = new UTF8Encoding(false, true).GetBytes(ZumaSaveCodec.Encode(ZumaSaveData.CreateNew(), levelResolver));
                    var suffix = ".recovery-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
                    Archive(SavePath, SavePath + suffix);
                    Archive(SavePath + ".bak", SavePath + ".bak" + suffix);
                    // Backups remain in place until the new primary is committed. The archives preserve
                    // both originals; a future normal save may rotate the corrupt backup safely.
                    Write(bytes, false);
                    status = ZumaLoadStatus.Loaded;
                    return new ZumaSaveResult { Success = true };
                }
                catch (Exception exception) when (StorageFailure(exception) || exception is ArgumentException)
                {
                    return Failure("Zuma重置失败，原存档已保留：" + exception.Message);
                }
            }
        }

        private static void Archive(string source, string destination)
        {
            try
            {
                using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    input.CopyTo(output);
                    output.Flush(true);
                }
            }
            catch (FileNotFoundException) { }
        }

        private void Write(byte[] bytes, bool rotateValidMain)
        {
            Directory.CreateDirectory(directory);
            var temporary = SavePath + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            // Existence is checked only after reads have classified errors. A directory target must
            // reach File.Replace and fail, rather than being mistaken for a missing save.
            if (File.Exists(SavePath) || Directory.Exists(SavePath))
                File.Replace(temporary, SavePath, rotateValidMain ? SavePath + ".bak" : null);
            else File.Move(temporary, SavePath);
        }

        private ZumaLoadResult Read(string path)
        {
            try
            {
                var buffer = new byte[ZumaSaveCodec.MaximumLength + 1];
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
                // The version may be beyond our bounded read, including inside a future session.
                // An unverifiable oversized file must never enable fallback, overwrite or reset.
                if (count > ZumaSaveCodec.MaximumLength) return ZumaSaveCodec.UnverifiableSize();
                var json = new UTF8Encoding(false, true).GetString(buffer, 0, count);
                var decoded = ZumaSaveCodec.Decode(json, levelResolver);
                return new ZumaLoadResult { Status = decoded.Status, Data = decoded.Data, Warning = decoded.Warning };
            }
            catch (FileNotFoundException) { return new ZumaLoadResult { Status = ZumaLoadStatus.Missing }; }
            catch (DirectoryNotFoundException exception)
            {
                // A missing hierarchy is missing; a file standing in for a parent is an I/O failure.
                var parent = Path.GetDirectoryName(path);
                while (!string.IsNullOrEmpty(parent))
                {
                    if (File.Exists(parent)) return new ZumaLoadResult { Status = ZumaLoadStatus.IoError, Warning = exception.Message };
                    parent = Path.GetDirectoryName(parent);
                }
                return new ZumaLoadResult { Status = ZumaLoadStatus.Missing };
            }
            catch (DecoderFallbackException exception)
            {
                return new ZumaLoadResult { Status = ZumaLoadStatus.Corrupt, Warning = exception.Message };
            }
            catch (Exception exception) when (StorageFailure(exception))
            {
                return new ZumaLoadResult { Status = ZumaLoadStatus.IoError, Warning = "Zuma存档暂时无法读取，请重试：" + exception.Message };
            }
        }

        private static bool Protected(ZumaLoadStatus value)
        {
            return value == ZumaLoadStatus.Corrupt || value == ZumaLoadStatus.UnsupportedVersion || value == ZumaLoadStatus.IoError;
        }
        private static ZumaSaveResult Failure(string error) { return new ZumaSaveResult { Error = error ?? "Zuma进度未保存。" }; }
        private static bool StorageFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException || exception is System.Security.SecurityException || exception is NotSupportedException;
        }
    }
}
