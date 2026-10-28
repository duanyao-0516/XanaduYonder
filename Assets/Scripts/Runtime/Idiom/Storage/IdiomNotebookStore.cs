namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomNotebookStore : IdiomFileStore<IdiomNotebookData>
    {
        public IdiomNotebookStore(string directory) : base(directory, "idiom-notebook-v1.json", IdiomNotebookCodec.MaximumBytes,
            IdiomNotebookCodec.Decode, IdiomNotebookCodec.Encode, "生词本") { }
    }
}
