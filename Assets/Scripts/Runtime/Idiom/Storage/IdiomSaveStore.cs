using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomSaveStore : IdiomFileStore<IdiomSaveData>
    {
        private readonly IdiomLexicon lexicon;
        public IdiomSaveStore(string directory, IdiomLexicon lexicon) : base(directory, "idiom-v1.json", IdiomSaveCodec.MaximumBytes,
            json => IdiomSaveCodec.Decode(json, lexicon), data => IdiomSaveCodec.Encode(data, lexicon), "对局存档")
        { this.lexicon = lexicon; }
        protected override bool IsReady { get { return lexicon != null; } }
    }
}
