using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using XanaduYonder.Core.GoldMiner;

namespace XanaduYonder.Runtime.GoldMiner
{
    /// <summary>Only the filesystem boundary is injectable; all validation and recovery run in the store.</summary>
    public interface IGoldMinerSaveFileSystem
    {
        bool Exists(string path);
        byte[] ReadBounded(string path, int maximumLength);
        void CreateDirectory(string path);
        void WriteAndFlush(string path, byte[] bytes);
        void Replace(string temporary, string destination, string backup);
        void Move(string source, string destination);
    }

    public sealed class GoldMinerSaveFileSystem : IGoldMinerSaveFileSystem
    {
        public bool Exists(string path)
        {
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }
        public byte[] ReadBounded(string path, int maximumLength)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var length = stream.Length;
                if (length > maximumLength) throw new InvalidDataException("Gold miner file exceeds 1 MiB.");
                // Size from this open handle, not a pre-open path stat. The extra byte detects
                // growth while reading without reserving 1 MiB for every small save file.
                var bytes = new byte[checked((int)length + 1)]; var count = 0;
                while (count < bytes.Length)
                {
                    var read = stream.Read(bytes, count, bytes.Length - count);
                    if (read == 0) break;
                    count += read;
                }
                if (count > length || stream.Length > length)
                    throw new InvalidDataException("Gold miner file grew while being read.");
                Array.Resize(ref bytes, count); return bytes;
            }
        }
        public void CreateDirectory(string path) { Directory.CreateDirectory(path); }
        public void WriteAndFlush(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }
        public void Replace(string temporary, string destination, string backup) { File.Replace(temporary, destination, backup); }
        public void Move(string source, string destination) { File.Move(source, destination); }
    }

    public sealed class GoldMinerSaveStore
    {
        private readonly string directory;
        private readonly IGoldMinerSaveFileSystem files;
        private readonly object gate = new object();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private bool ownsTemporary;
        public string SavePath { get; private set; }
        public bool RequiresRecovery { get; private set; }

        public GoldMinerSaveStore(string directory) : this(directory, new GoldMinerSaveFileSystem()) { }
        public GoldMinerSaveStore(string directory, IGoldMinerSaveFileSystem files)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Missing save directory.", nameof(directory));
            if (files == null) throw new ArgumentNullException(nameof(files));
            this.directory = directory; this.files = files;
            SavePath = Path.Combine(directory, "gold-miner-v1.json");
        }

        public GoldMinerRootSnapshot Load(out string warning)
        {
            lock (gate)
            {
                warning = null; RequiresRecovery = false;
                GoldMinerRootSnapshot snapshot; string mainError, backupError;
                if (TryRead(SavePath, out snapshot, out mainError)) return snapshot;
                if (TryRead(SavePath + ".bak", out snapshot, out backupError))
                {
                    warning = "黄金矿工主存档无法读取，已恢复最近一次合法备份。";
                    return snapshot;
                }
                string temporaryError; var hasTemporary = Exists(SavePath + ".tmp", out temporaryError);
                RequiresRecovery = mainError != null || backupError != null || hasTemporary || temporaryError != null;
                if (RequiresRecovery) warning = "黄金矿工存档损坏、版本不受支持或写入未完成。原文件已保留；开始新挑战前需要确认归档。";
                return null;
            }
        }

        /// <summary>Called only after the player explicitly chooses to archive the unreadable save.</summary>
        public bool TryArchiveInvalid(out string error)
        {
            lock (gate)
            {
                error = null;
                try
                {
                    GoldMinerRootSnapshot valid; string ignored;
                    if (TryRead(SavePath, out valid, out ignored) || TryRead(SavePath + ".bak", out valid, out ignored))
                    {
                        RequiresRecovery = false;
                        error = "A valid gold miner save exists; recovery archival is not required.";
                        return false;
                    }
                    // Each move is non-overwriting. A failure can leave a partial archive, but never
                    // deletes bytes; RequiresRecovery remains set and the remaining files can be retried.
                    var suffix = ".invalid-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
                    foreach (var extension in new[] { "", ".bak", ".tmp" })
                    {
                        var source = SavePath + extension;
                        if (files.Exists(source)) files.Move(source, source + suffix);
                    }
                    ownsTemporary = false; RequiresRecovery = false;
                    return true;
                }
                catch (Exception exception) when (StorageFailure(exception))
                {
                    RequiresRecovery = true; error = exception.Message; return false;
                }
            }
        }

        public bool TrySave(GoldMinerRootSnapshot snapshot, out string error)
        {
            lock (gate)
            {
                error = null; byte[] bytes = null; long sequence = 0; bool replacing = false;
                try
                {
                    // Validate the complete candidate before touching even the temporary file.
                    bytes = Utf8.GetBytes(GoldMinerSaveCodec.Encode(snapshot)); sequence = snapshot.TxnSeq;
                    if (RequiresRecovery) throw new IOException("Archive the invalid gold miner files before starting a new challenge.");
                    GoldMinerRootSnapshot previous; string mainError, backupError;
                    var previousIsValid = TryRead(SavePath, out previous, out mainError);
                    var backupIsValid = TryRead(SavePath + ".bak", out previous, out backupError);
                    if (!previousIsValid && !backupIsValid &&
                        (mainError != null || backupError != null || (!ownsTemporary && files.Exists(SavePath + ".tmp"))))
                    {
                        RequiresRecovery = true;
                        throw new IOException("Unreadable gold miner files must be archived before saving a new challenge.");
                    }
                    files.CreateDirectory(directory);
                    ownsTemporary = true;
                    files.WriteAndFlush(SavePath + ".tmp", bytes);
                    replacing = true;
                    if (files.Exists(SavePath))
                        files.Replace(SavePath + ".tmp", SavePath, previousIsValid ? SavePath + ".bak" : null);
                    else files.Move(SavePath + ".tmp", SavePath);
                    ownsTemporary = false;
                    return true;
                }
                catch (Exception exception) when (StorageFailure(exception) || exception is ArgumentException || exception is OverflowException)
                {
                    // A platform can report an error after its rename/replace completed. Commit only
                    // if the exact candidate bytes and transaction sequence are present and valid.
                    if (replacing && MatchesCommitted(bytes, sequence)) { ownsTemporary = false; return true; }
                    error = exception.Message; return false;
                }
            }
        }

        private bool MatchesCommitted(byte[] expected, long sequence)
        {
            try
            {
                var actual = files.ReadBounded(SavePath, GoldMinerSaveCodec.MaximumLength);
                GoldMinerRootSnapshot root; string reason;
                if (!GoldMinerSaveCodec.TryDecode(Utf8.GetString(actual), out root, out reason) || root.TxnSeq != sequence) return false;
                using (var hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(actual)) == Convert.ToBase64String(hash.ComputeHash(expected));
            }
            catch (Exception exception) when (StorageFailure(exception) || exception is ArgumentException) { return false; }
        }
        private bool TryRead(string path, out GoldMinerRootSnapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            try
            {
                if (!files.Exists(path)) return false;
                var bytes = files.ReadBounded(path, GoldMinerSaveCodec.MaximumLength);
                return GoldMinerSaveCodec.TryDecode(Utf8.GetString(bytes), out snapshot, out error);
            }
            catch (Exception exception) when (StorageFailure(exception) || exception is ArgumentException)
            {
                error = exception.Message; return false;
            }
        }
        private bool Exists(string path, out string error)
        {
            error = null;
            try { return files.Exists(path); }
            catch (Exception exception) when (StorageFailure(exception)) { error = exception.Message; return false; }
        }
        private static bool StorageFailure(Exception exception)
        {
            return exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException || exception is NotSupportedException;
        }
    }
}
