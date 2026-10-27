using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomSaveStore : IdiomFileStore<IdiomSaveData>
    {
        private readonly IdiomLexicon lexicon;
        public IdiomSaveStore(string directory, IdiomLexicon lexicon) : base(directory, "idiom-learning-v1.json", IdiomSaveCodec.MaximumBytes,
            json => IdiomSaveCodec.Decode(json, lexicon), data => IdiomSaveCodec.Encode(data, lexicon), "成语学习记录")
        { this.lexicon = lexicon; }
        protected override bool IsReady { get { return lexicon != null; } }
    }
}
