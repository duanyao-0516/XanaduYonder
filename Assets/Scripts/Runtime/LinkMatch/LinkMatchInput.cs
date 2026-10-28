namespace XanaduYonder.Runtime.LinkMatch
{
    /// <summary>A press belongs to one tile and one pointer; leaving it permanently cancels that press.</summary>
    public sealed class LinkMatchInput
    {
        private int? owner;
        private int pressed=-1;
        private bool canceled;
        public bool HasOwner { get { return owner.HasValue; } }
        public void Begin(int pointer,int tile,bool allowed)
        {
            if(owner.HasValue) { canceled=true; return; }
            owner=pointer; pressed=tile; canceled=!allowed||tile<0;
        }
        public void Move(int pointer,int tile)
        { if(owner.HasValue&&(pointer!=owner.Value||tile!=pressed)) canceled=true; }
        public void Cancel() { owner=null; pressed=-1; canceled=true; }
        public int End(int pointer,int tile,bool allowed)
        {
            int result=allowed&&!canceled&&owner==pointer&&tile==pressed?pressed:-1;
            Cancel(); return result;
        }
    }
}
