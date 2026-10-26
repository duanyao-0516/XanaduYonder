using System.Collections.Generic;

namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomNotebookData
    {
        public List<IdiomNotebookItem> Entries { get; set; } = new List<IdiomNotebookItem>();
    }

    public sealed class IdiomNotebookItem
    {
        public string Id { get; set; }
        public string SenseId { get; set; }
        public string Word { get; set; }
        public string Pinyin { get; set; }
        public string Meaning { get; set; }
        public long AddedAt { get; set; }
        public string DictionaryVersion { get; set; }
    }
}
