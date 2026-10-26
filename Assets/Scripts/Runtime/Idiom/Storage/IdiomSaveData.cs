using XanaduYonder.Core.Idiom;

namespace XanaduYonder.Runtime.Idiom
{
    public sealed class IdiomSaveData
    {
        public IdiomMode ActiveMode { get; set; }
        public IdiomModeSave Character { get; set; }
        public IdiomModeSave Sound { get; set; }
    }
    public sealed class IdiomModeSave
    {
        public IdiomSnapshot Game { get; set; }
        public string Draft { get; set; } = "";
        public int Best { get; set; }
        public bool NewBestInRun { get; set; }
    }
}
