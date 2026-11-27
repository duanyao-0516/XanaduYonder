using System;
using System.IO;

namespace XanaduYonder.Runtime
{
    internal static class AtomicSaveFile
    {
        // FileShare.None covers the write. Keep it and the following rename in one commit across store instances.
        private static readonly object CommitLock = new object();

        public static void Write(string path, byte[] encoded, Func<string, bool> canBackUp)
        {
            lock (CommitLock)
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temporary = path + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(encoded, 0, encoded.Length);
                    stream.Flush(true);
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, canBackUp(path) ? path + ".bak" : null);
                else
                    File.Move(temporary, path);
            }
        }
    }
}
