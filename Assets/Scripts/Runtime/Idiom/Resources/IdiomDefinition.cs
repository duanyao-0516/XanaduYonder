namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomCitation
    {
        public string Text { get; set; }
        public string Reference { get; set; }
    }

    // These DTOs retain each dictionary record exactly as supplied. An empty
    // source field means the source supplied no content; it is never inferred.
    public sealed class IdiomDefinitionSource
    {
        public string SourceKey { get; set; }
        public string SourceLabel { get; set; }
        public string SourceRecordId { get; set; }
        public string Pinyin { get; set; }
        public string Meaning { get; set; }
        public string[] Stories { get; set; }
        public IdiomCitation[] Origins { get; set; }
        public IdiomCitation[] Examples { get; set; }
    }

    public sealed class IdiomDefinition
    {
        public string Id { get; set; }
        public string SenseId { get; set; }
        public string Word { get; set; }
        public string Pinyin { get; set; }
        public string Meaning { get; set; }
        public string[] Stories { get; set; }
        public IdiomCitation[] Origins { get; set; }
        public IdiomCitation[] Examples { get; set; }
        public string SourceLabel { get; set; }
        public IdiomDefinitionSource[] Sources { get; set; }
    }
}
