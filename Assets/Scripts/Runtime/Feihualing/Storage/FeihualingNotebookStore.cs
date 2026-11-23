using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using XanaduYonder.Core.Feihualing;
using XanaduYonder.Runtime.Idiom;

namespace XanaduYonder.Runtime.Feihualing
{
    public enum FeihualingNotebookAddResult
    {
        Added,
        AlreadySaved,
        Failed
    }

    public sealed class FeihualingNotebookEntry
    {
        internal FeihualingNotebookEntry(string workId, string title, string author, string body)
        {
            WorkId = workId;
            Title = title;
            Author = author;
            Body = body;
        }

        public string WorkId { get; }
        public string Title { get; }
        public string Author { get; }
        public string Body { get; }
    }

    public sealed class FeihualingNotebookStore
    {
        private const string FileName = "feihualing-notebook-v1.json";
        private const string GameId = "feihualing-notebook";
        private const int MaximumBytes = 32 * 1024 * 1024;
        private const int MaximumEntries = 50_000;
        private const int MaximumIdLength = 96;
        private const int MaximumTitleLength = 300;
        private const int MaximumAuthorLength = 100;
        private const int MaximumBodyLength = 16_000;

        private static readonly object FileGate = new object();
        private readonly NotebookFile file;
        private IReadOnlyList<FeihualingNotebookEntry> entries = Array.Empty<FeihualingNotebookEntry>();

        public FeihualingNotebookStore(string directory)
        {
            file = new NotebookFile(directory);
        }

        public IReadOnlyList<FeihualingNotebookEntry> Entries
        {
            get
            {
                lock (FileGate)
                    return entries;
            }
        }

        public bool LoadNotebook(out string warning)
        {
            lock (FileGate)
            {
                if (!TryRead(out var saved, out warning))
                    return false;
                entries = saved;
                return true;
            }
        }

        public bool Contains(string workId)
        {
            return Contains(Entries, workId);
        }

        public FeihualingNotebookAddResult Add(PoetryWork work, out string message)
        {
            lock (FileGate)
            {
                if (work == null)
                {
                    message = "这首诗词暂不可收藏。";
                    return FeihualingNotebookAddResult.Failed;
                }

                // Reload the durable entries first so that separate views cannot overwrite one another.
                if (!TryRead(out var saved, out message))
                    return FeihualingNotebookAddResult.Failed;
                if (Contains(saved, work.Id))
                {
                    entries = saved;
                    message = "这首诗词已收藏。";
                    return FeihualingNotebookAddResult.AlreadySaved;
                }

                var updated = new FeihualingNotebookEntry[saved.Count + 1];
                updated[0] = new FeihualingNotebookEntry(work.Id, work.Title, work.Author, work.Body);
                for (var i = 0; i < saved.Count; i++)
                    updated[i + 1] = saved[i];
                var candidate = Array.AsReadOnly(updated);
                if (!file.TrySave(candidate, out message))
                {
                    if (file.NeedsRecovery)
                        message = RecoveryWarning();
                    return FeihualingNotebookAddResult.Failed;
                }

                entries = candidate;
                message = "已收藏这首诗词。";
                return FeihualingNotebookAddResult.Added;
            }
        }

        private bool TryRead(out IReadOnlyList<FeihualingNotebookEntry> saved, out string warning)
        {
            saved = file.Load(out warning);
            if (!file.CanWrite)
            {
                if (file.NeedsRecovery)
                    warning = RecoveryWarning();
                return false;
            }
            if (saved == null)
                saved = Array.Empty<FeihualingNotebookEntry>();
            return true;
        }

        private string RecoveryWarning()
        {
            return file.LoadState == IdiomLoadState.Incompatible
                ? "收藏记录版本不兼容，原文件已保留。"
                : "收藏记录暂无法读取，原文件已保留。";
        }

        private static bool Contains(IReadOnlyList<FeihualingNotebookEntry> entries, string workId)
        {
            if (workId == null)
                return false;
            foreach (var entry in entries)
                if (string.Equals(entry.WorkId, workId, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static IReadOnlyList<FeihualingNotebookEntry> Decode(string json)
        {
            var root = IdiomStrictJson.Read(json, MaximumBytes);
            IdiomStrictJson.Identity(root, GameId);
            IdiomStrictJson.Object(root, "schemaVersion", "gameId", "entries");

            var rows = IdiomStrictJson.Array(root["entries"], MaximumEntries);
            var saved = new FeihualingNotebookEntry[rows.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < rows.Count; i++)
            {
                var row = IdiomStrictJson.Object(rows[i], "workId", "title", "author", "body");
                var id = IdiomStrictJson.Text(row["workId"], MaximumIdLength);
                if (!ids.Add(id))
                    throw new FormatException("Duplicate notebook work ID.");
                saved[i] = new FeihualingNotebookEntry(id, IdiomStrictJson.Text(row["title"], MaximumTitleLength),
                    IdiomStrictJson.Text(row["author"], MaximumAuthorLength), IdiomStrictJson.Text(row["body"], MaximumBodyLength));
            }
            return Array.AsReadOnly(saved);
        }

        private static string Encode(IReadOnlyList<FeihualingNotebookEntry> saved)
        {
            if (saved.Count > MaximumEntries)
                throw new IdiomLimitException();
            var rows = new JArray();
            foreach (var entry in saved)
            {
                IdiomStrictJson.ValidateText(entry.WorkId, MaximumIdLength);
                IdiomStrictJson.ValidateText(entry.Title, MaximumTitleLength);
                IdiomStrictJson.ValidateText(entry.Author, MaximumAuthorLength);
                IdiomStrictJson.ValidateText(entry.Body, MaximumBodyLength);
                rows.Add(new JObject { ["workId"] = entry.WorkId, ["title"] = entry.Title,
                    ["author"] = entry.Author, ["body"] = entry.Body });
            }
            return IdiomStrictJson.Write(new JObject { ["schemaVersion"] = 1, ["gameId"] = GameId, ["entries"] = rows }, MaximumBytes);
        }

        private sealed class NotebookFile : IdiomFileStore<IReadOnlyList<FeihualingNotebookEntry>>
        {
            public NotebookFile(string directory)
                : base(directory, FileName, MaximumBytes, Decode, Encode, "诗词收藏") { }

            protected override string RecoveryDirectoryName => "feihualing-notebook-recovery";
            protected override bool PreserveCorruptPrimaryAfterBackup => true;
        }
    }
}
