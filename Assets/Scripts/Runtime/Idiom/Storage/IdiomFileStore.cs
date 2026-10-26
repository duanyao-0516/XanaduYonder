using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace XanaduYonder.Runtime.Idiom
{
    internal static class IdiomStoreErrors
    {
        internal static bool IsDataError(Exception ex)
        {
            return ex is FormatException || ex is ArgumentException || ex is JsonException || ex is OverflowException;
        }
        internal static bool IsExpected(Exception ex)
        {
            return IsDataError(ex) || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is System.Security.SecurityException;
        }
    }

    public enum IdiomLoadState { NotLoaded, Missing, Loaded, RecoveredBackup, Corrupt, Incompatible, Unavailable }

    public abstract class IdiomFileStore<T> where T : class
    {
        private readonly string directory;
        private readonly Func<string, T> decode;
        private readonly Func<T, string> encode;
        private readonly int maximumBytes;
        private readonly string label;
        private bool loaded;
        protected IdiomFileStore(string directory, string filename, int maximumBytes, Func<string, T> decode, Func<T, string> encode, string label)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("存档目录不可用。");
            this.directory = Path.GetFullPath(directory); this.decode = decode; this.encode = encode; this.maximumBytes = maximumBytes; this.label = label;
            SavePath = Path.Combine(this.directory, filename);
        }
        public string SavePath { get; private set; }
        public bool CanWrite { get; private set; }
        public bool NeedsRecovery { get; private set; }
        public IdiomLoadState LoadState { get; private set; }
        protected virtual bool IsReady { get { return true; } }

        public T Load(out string warning)
        {
            loaded = true; CanWrite = false; NeedsRecovery = false; warning = null;
            if (!IsReady) { LoadState = IdiomLoadState.Unavailable; warning = "词库尚未就绪，暂时无法读取或保存对局。"; return null; }
            bool primaryExists, backupExists;
            try
            {
                CheckDirectory();
                primaryExists = Exists(SavePath); backupExists = Exists(SavePath + ".bak");
            }
            catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex))
            { LoadState = IdiomLoadState.Unavailable; warning = "存档目录暂不可用，请重试。"; return null; }
            if (!primaryExists && !backupExists)
            { LoadState = IdiomLoadState.Missing; CanWrite = true; return null; }
            if (primaryExists)
            {
                try { T data = Read(SavePath); CanWrite = true; LoadState = IdiomLoadState.Loaded; return data; }
                catch (IdiomVersionException) { Protect(true, out warning); return null; }
                catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex)) { }
            }
            if (backupExists)
            {
                try { T data = Read(SavePath + ".bak"); CanWrite = true; LoadState = IdiomLoadState.RecoveredBackup; warning = "已从备份恢复" + label + "。"; return data; }
                catch (IdiomVersionException) { Protect(true, out warning); return null; }
                catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex)) { }
            }
            Protect(false, out warning); return null;
        }

        private void Protect(bool incompatible, out string error)
        {
            CanWrite = false; NeedsRecovery = true; LoadState = incompatible ? IdiomLoadState.Incompatible : IdiomLoadState.Corrupt;
            error = incompatible ? label + "版本已变化，原文件已保留，请确认重新开始。" : label + "无法读取，原文件已保留，请确认重建后再保存。";
        }
        private static bool Exists(string path)
        {
            try { File.GetAttributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }
        private void CheckDirectory()
        {
            // File.Exists hides access failures; those must not look like a new game.
            for (string current = directory; current != null; current = Path.GetDirectoryName(current))
            {
                if (!Exists(current)) continue;
                if ((File.GetAttributes(current) & FileAttributes.Directory) == 0) throw new IOException();
                using (var entries = Directory.EnumerateFileSystemEntries(current).GetEnumerator()) entries.MoveNext();
                return;
            }
            throw new IOException();
        }
        private T Read(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > maximumBytes) throw new FormatException();
                var bytes = new byte[(int)stream.Length]; int total = 0;
                while (total < bytes.Length) { int count = stream.Read(bytes, total, bytes.Length - total); if (count == 0) throw new IOException(); total += count; }
                return decode(IdiomStrictJson.Utf8.GetString(bytes));
            }
        }

        public bool TrySave(T data, out string error)
        {
            error = null;
            if (!loaded || !CanWrite || !IsReady) { error = label + "暂未保存，请先读取或确认重建原记录。"; return false; }
            string temporary = SavePath + ".tmp";
            try
            {
                byte[] bytes = IdiomStrictJson.Utf8.GetBytes(encode(data));
                if (bytes.Length > maximumBytes) throw new FormatException();
                bool primaryExists = Exists(SavePath), previousValid = false;
                if (primaryExists)
                {
                    try { Read(SavePath); previousValid = true; }
                    catch (IdiomVersionException) { Protect(true, out error); return false; }
                    catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex))
                    {
                        if (LoadState != IdiomLoadState.RecoveredBackup) { Protect(false, out error); return false; }
                    }
                }
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (primaryExists) File.Replace(temporary, SavePath, previousValid ? SavePath + ".bak" : null);
                else File.Move(temporary, SavePath);
                LoadState = IdiomLoadState.Loaded;
                return true;
            }
            catch (IdiomLimitException ex) { error = ex.Message; return false; }
            catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex))
            { error = label == "生词本" ? "生词本暂未保存，请重试。" : "当前进度未保存，请重试。"; return false; }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex)) { }
            }
        }

        public bool TryResetProtected(out string error)
        {
            error = null;
            if (!loaded || !IsReady) { error = "请先读取记录，待词库就绪后再重建。"; return false; }
            if (!NeedsRecovery)
            {
                if (!CanWrite) error = "存档目录暂不可用，请先重试读取记录。";
                return CanWrite;
            }
            try
            {
                var paths = new[] { SavePath, SavePath + ".bak", SavePath + ".tmp" };
                foreach (string path in paths) if (Directory.Exists(path)) throw new IOException();
                string archive = Path.Combine(directory, "idiom-recovery", Path.GetFileName(SavePath) + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(archive);
                // Copy every original durably before removing any active filename.
                foreach (string path in paths)
                {
                    if (!File.Exists(path)) continue;
                    using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(Path.Combine(archive, Path.GetFileName(path)), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { input.CopyTo(output); output.Flush(true); }
                }
                foreach (string path in paths) if (File.Exists(path)) File.Delete(path);
                CanWrite = true; NeedsRecovery = false; LoadState = IdiomLoadState.Missing;
                return true;
            }
            catch (Exception ex) when (IdiomStoreErrors.IsExpected(ex))
            { error = "原记录尚未安全归档，暂时无法重建，请重试。"; return false; }
        }
    }
}
