using System;
using System.Collections.Generic;

namespace XanaduYonder.Core.Idiom
{
    public sealed class IdiomAppExposure
    {
        public string EntryId { get; private set; }
        public long LastShownUtc { get; private set; }
        public IdiomAppExposure(string entryId, long lastShownUtc) { EntryId = entryId; LastShownUtc = lastShownUtc; }
    }
    public sealed class IdiomUseCount
    {
        public string EntryId { get; private set; }
        public int IndependentUses { get; private set; }
        public int AssistedUses { get; private set; }
        public IdiomUseCount(string entryId, int independentUses, int assistedUses)
        { EntryId = entryId; IndependentUses = independentUses; AssistedUses = assistedUses; }
    }
    public sealed class IdiomNotebookAdded
    {
        public string EntryId { get; private set; }
        public long AddedAtUtc { get; private set; }
        public IdiomNotebookAdded(string entryId, long addedAtUtc) { EntryId = entryId; AddedAtUtc = addedAtUtc; }
    }
    public sealed class IdiomLearningSnapshot
    {
        public IReadOnlyList<string> FamiliarIds { get; private set; }
        public IReadOnlyList<string> NotebookIds { get; private set; }
        public IReadOnlyList<string> RecentAppIds { get; private set; }
        public IReadOnlyList<IdiomAppExposure> AppExposures { get; private set; }
        public IReadOnlyList<IdiomUseCount> Uses { get; private set; }
        public IReadOnlyList<IdiomNotebookAdded> NotebookAdded { get; private set; }
        public IdiomLearningSnapshot(IEnumerable<string> familiarIds, IEnumerable<string> notebookIds, IEnumerable<string> recentAppIds,
            IEnumerable<IdiomAppExposure> appExposures, IEnumerable<IdiomUseCount> uses, IEnumerable<IdiomNotebookAdded> notebookAdded)
        {
            FamiliarIds = Copy(familiarIds); NotebookIds = Copy(notebookIds); RecentAppIds = Copy(recentAppIds);
            AppExposures = Copy(appExposures); Uses = Copy(uses); NotebookAdded = Copy(notebookAdded);
        }
        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values)
        { if (values == null) throw new ArgumentNullException("values"); return new List<T>(values).AsReadOnly(); }
    }
    // Frozen eligibility at a policy ending. Later profile edits must not rewrite its reason.
    public sealed class IdiomSelectionContext
    {
        public IReadOnlyList<string> OrdinaryIds { get; private set; }
        public IReadOnlyList<string> ReviewIds { get; private set; }
        public IdiomSelectionContext(IEnumerable<string> ordinaryIds, IEnumerable<string> reviewIds)
        {
            if (ordinaryIds == null || reviewIds == null) throw new ArgumentNullException("ids");
            OrdinaryIds = new List<string>(ordinaryIds).AsReadOnly(); ReviewIds = new List<string>(reviewIds).AsReadOnly();
        }
    }

    public sealed class IdiomLearningProfile
    {
        public const long MaximumUtc = 253402300799L;
        private readonly IdiomLexicon lexicon;
        private HashSet<string> familiar = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> notebook = new HashSet<string>(StringComparer.Ordinal);
        private List<string> recent = new List<string>();
        private Dictionary<string, long> shown = new Dictionary<string, long>(StringComparer.Ordinal);
        private Dictionary<string, IdiomUseCount> uses = new Dictionary<string, IdiomUseCount>(StringComparer.Ordinal);
        private Dictionary<string, long> added = new Dictionary<string, long>(StringComparer.Ordinal);

        public IdiomLearningProfile(IdiomLexicon lexicon)
        { this.lexicon = lexicon ?? throw new ArgumentNullException("lexicon"); }
        public IdiomLearningProfile(IdiomLexicon lexicon, IdiomLearningSnapshot snapshot) : this(lexicon)
        { ReadSnapshot(snapshot); }
        public bool IsFamiliar(string id) { return id != null && familiar.Contains(id); }
        public bool IsNotebook(string id) { return id != null && notebook.Contains(id); }
        public void AddNotebook(string id, long addedAtUtc)
        {
            RequireId(id); RequireTime(addedAtUtc);
            if (notebook.Add(id)) added.Add(id, addedAtUtc);
        }
        public void RemoveNotebook(string id) { RequireId(id); notebook.Remove(id); added.Remove(id); }
        public void Remember(string id) { RequireId(id); familiar.Add(id); notebook.Remove(id); added.Remove(id); }
        public void ReplaceSnapshot(IdiomLearningSnapshot snapshot)
        {
            // Validate everything before replacing any collection observed by the four games.
            var replacement = new IdiomLearningProfile(lexicon, snapshot);
            familiar = replacement.familiar; notebook = replacement.notebook; recent = replacement.recent;
            shown = replacement.shown; uses = replacement.uses; added = replacement.added;
        }
        public IdiomLearningSnapshot GetSnapshot()
        {
            var exposures = new List<IdiomAppExposure>();
            foreach (string id in Sorted(shown.Keys)) exposures.Add(new IdiomAppExposure(id, shown[id]));
            var counts = new List<IdiomUseCount>(); foreach (string id in Sorted(uses.Keys)) counts.Add(uses[id]);
            var dates = new List<IdiomNotebookAdded>(); foreach (string id in Sorted(added.Keys)) dates.Add(new IdiomNotebookAdded(id, added[id]));
            return new IdiomLearningSnapshot(Sorted(familiar), Sorted(notebook), recent, exposures, counts, dates);
        }
        internal void RecordUse(string id, bool assisted)
        {
            IdiomUseCount prior; uses.TryGetValue(id, out prior);
            int independent = prior == null ? 0 : prior.IndependentUses, aided = prior == null ? 0 : prior.AssistedUses;
            if (assisted) aided = Increment(aided); else independent = Increment(independent);
            uses[id] = new IdiomUseCount(id, independent, aided);
            if (!assisted && !notebook.Contains(id)) familiar.Add(id);
        }
        internal void RecordApp(string id, long now)
        {
            shown[id] = now; recent.Add(id); if (recent.Count > 10) recent.RemoveAt(0);
        }
        internal bool Ordinary(IdiomEntry entry) { return !notebook.Contains(entry.Id) && (entry.Common || familiar.Contains(entry.Id)); }
        internal bool ReviewDue(string id, long now)
        { long last; return notebook.Contains(id) && (!shown.TryGetValue(id, out last) || now - last >= 86400); }
        internal int Weight(IdiomEntry entry, int continuations, long now)
        {
            IdiomUseCount count; uses.TryGetValue(entry.Id, out count);
            int independent = count == null ? 0 : count.IndependentUses;
            long baseWeight;
            if (notebook.Contains(entry.Id))
            {
                long origin; if (!shown.TryGetValue(entry.Id, out origin)) origin = added[entry.Id];
                long days = Math.Max(0, Math.Min(7, (now - origin) / 86400));
                baseWeight = 100 + days * 3 + Math.Max(0, 6 - Math.Min(independent, 6)) * 2;
            }
            else baseWeight = familiar.Contains(entry.Id) ? 120 + Math.Min(independent, 6) * 5 : 100;
            long weight = baseWeight * (100 + 10 * Math.Min(continuations, 3));
            if (recent.Contains(entry.Id)) weight /= 4;
            return (int)weight;
        }
        internal IdiomSelectionContext SelectionContext(long now, bool automaticReview)
        {
            var ordinary = new List<string>(); var review = new List<string>();
            foreach (var entry in lexicon.Entries)
            {
                if (Ordinary(entry)) ordinary.Add(entry.Id);
                else if (notebook.Contains(entry.Id) && (!automaticReview || ReviewDue(entry.Id, now))) review.Add(entry.Id);
            }
            return new IdiomSelectionContext(ordinary, review);
        }
        private void ReadSnapshot(IdiomLearningSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException("snapshot");
            familiar = ReadIds(snapshot.FamiliarIds); notebook = ReadIds(snapshot.NotebookIds);
            if (snapshot.RecentAppIds.Count > 10) Invalid();
            foreach (string id in snapshot.RecentAppIds) { RequireId(id); recent.Add(id); }
            foreach (var item in snapshot.AppExposures)
            {
                if (item == null) Invalid(); RequireId(item.EntryId); RequireTime(item.LastShownUtc);
                if (shown.ContainsKey(item.EntryId)) Invalid(); shown.Add(item.EntryId, item.LastShownUtc);
            }
            foreach (var item in snapshot.Uses)
            {
                if (item == null) Invalid(); RequireId(item.EntryId);
                if (item.IndependentUses < 0 || item.AssistedUses < 0 || (item.IndependentUses == 0 && item.AssistedUses == 0) || uses.ContainsKey(item.EntryId)) Invalid();
                uses.Add(item.EntryId, item);
            }
            foreach (var item in snapshot.NotebookAdded)
            {
                if (item == null) Invalid(); RequireId(item.EntryId); RequireTime(item.AddedAtUtc);
                if (!notebook.Contains(item.EntryId) || added.ContainsKey(item.EntryId)) Invalid(); added.Add(item.EntryId, item.AddedAtUtc);
            }
            if (added.Count != notebook.Count) Invalid();
            foreach (string id in recent) if (!shown.ContainsKey(id)) Invalid();
        }
        private HashSet<string> ReadIds(IEnumerable<string> ids)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids) { RequireId(id); if (!result.Add(id)) Invalid(); }
            return result;
        }
        private void RequireId(string id) { IdiomEntry entry; if (!lexicon.TryGetById(id, out entry)) Invalid(); }
        internal static void RequireTime(long value) { if (value < 0 || value > MaximumUtc) throw new ArgumentOutOfRangeException("utc"); }
        private static int Increment(int value) { return value == int.MaxValue ? value : value + 1; }
        private static List<string> Sorted(IEnumerable<string> ids) { var list = new List<string>(ids); list.Sort(StringComparer.Ordinal); return list; }
        private static void Invalid() { throw new ArgumentException("Invalid idiom learning snapshot.", "snapshot"); }
    }
}
