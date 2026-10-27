using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomSaveData
    {
        public IdiomMode ActiveMode { get; set; }
        public IdiomMatchKind ActiveMatchKind { get; set; }
        public IdiomModeSave Character { get; set; }
        public IdiomModeSave Sound { get; set; }
        public IdiomModeSave LocalCharacter { get; set; }
        public IdiomModeSave LocalSound { get; set; }
        public IdiomLearningSnapshot Profile { get; set; }
        public IdiomNotebookData Notebook { get; set; }
    }
    public sealed class IdiomModeSave
    {
        public IdiomSnapshot Game { get; set; }
        public string Draft { get; set; } = "";
        public int Best { get; set; }
        public bool NewBestInRun { get; set; }
    }
}
