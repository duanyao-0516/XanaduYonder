using System;
using System.IO;
using System.Text;

namespace XanaduYonder.Runtime.Sudoku
{
    public sealed class SudokuProfileStore
    {
        private readonly string directory;
        public string SavePath { get; private set; }

        public SudokuProfileStore(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("Save directory is missing.", nameof(directory));
            this.directory = directory;
            SavePath = Path.Combine(directory, "sudoku-v2.json");
        }

        public SudokuProfile Load(out string warning)
        {
            warning = null;
            // Presence, even corruption, establishes v2 ownership. Never resurrect stale v1 progress.
            if (!Exists(SavePath) && !Exists(SavePath + ".bak")) return MigrateLegacy(out warning);
            if (TryRead(SavePath, out var profile, out var mainError)) return profile;
            if (TryRead(SavePath + ".bak", out profile, out var backupError))
            {
                warning = "数独存档异常，已恢复最近一次备份。";
                return profile;
            }
            warning = "数独存档无法读取，请重新选择关卡。";
            return null;
        }

        public bool TrySave(SudokuProfile profile, out string error)
        {
            error = null;
            var temporary = SavePath + ".tmp";
            try
            {
                // Fully validate and encode before changing any committed or temporary bytes.
                var bytes = new UTF8Encoding(false).GetBytes(SudokuProfileCodec.Encode(profile));
                Directory.CreateDirectory(directory);
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(SavePath))
                {
                    var previousIsValid = TryRead(SavePath, out var previous, out var readError);
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

        private SudokuProfile MigrateLegacy(out string warning)
        {
            var legacy = new SudokuSaveStore(directory).Load(out warning);
            if (legacy == null) return null;
            var profile = new SudokuProfile { SelectedDifficulty = legacy.Game.Puzzle.Difficulty };
            profile.Pages[profile.SelectedDifficulty] = (legacy.Game.Puzzle.Level - 1) / 50;
            profile.Capture(legacy);
            // The old page eagerly saved its initial board before a player chose Start.
            if (UntouchedLegacy(legacy)) profile.Slots[profile.SelectedDifficulty] = null;
            if (!TrySave(profile, out var error))
                warning = "旧数独进度已读取，但新版存档暂未保存。";
            return profile;
        }

        private static bool UntouchedLegacy(SudokuSaveData data)
        {
            // Only the old eager d0-l001 is ambiguous: deliberate entry into that exact
            // untouched board at t=0 has the same bytes. Every other selected puzzle is real.
            var game = data.Game;
            if (!string.Equals(game.Puzzle.Id, "se-v1-d0-l001", StringComparison.Ordinal) ||
                game.Revision != 0 || game.Mistakes != 0 || game.HintsUsed != 0 || game.History.Length != 0 ||
                data.ElapsedSeconds != 0 || data.Pencil || data.Quick || data.SelectedCell != -1 ||
                data.QuickDigit != 0 || data.Hint != null) return false;
            for (var cell = 0; cell < 81; cell++)
                if (game.Values[cell] != game.Puzzle.Givens[cell] || game.Notes[cell] != 0) return false;
            return true;
        }

        private static bool TryRead(string path, out SudokuProfile profile, out string error)
        {
            profile = null;
            error = null;
            if (!Exists(path)) return false;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var bytes = new MemoryStream())
                {
                    if (stream.Length > SudokuProfileCodec.MaximumLength)
                    {
                        error = "Sudoku profile file is too large.";
                        return false;
                    }
                    var buffer = new byte[4096];
                    while (true)
                    {
                        var allowance = SudokuProfileCodec.MaximumLength - (int)bytes.Length + 1;
                        var read = stream.Read(buffer, 0, Math.Min(buffer.Length, allowance));
                        if (read == 0) break;
                        if (bytes.Length + read > SudokuProfileCodec.MaximumLength)
                        {
                            error = "Sudoku profile file is too large.";
                            return false;
                        }
                        bytes.Write(buffer, 0, read);
                    }
                    return SudokuProfileCodec.TryDecode(new UTF8Encoding(false, true).GetString(bytes.GetBuffer(), 0, (int)bytes.Length), out profile, out error);
                }
            }
            catch (Exception exception) when (IsStorageFailure(exception) || exception is DecoderFallbackException)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool Exists(string path) { return File.Exists(path) || Directory.Exists(path); }

        private static bool IsStorageFailure(Exception exception)
        {
            return exception is IOException || exception is UnauthorizedAccessException ||
                   exception is System.Security.SecurityException || exception is NotSupportedException;
        }
    }
}
