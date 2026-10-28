namespace XanaduYonder.Core.FlappyBird
{
    public struct FlappyPipe
    {
        public readonly double X, GapCenter;
        public readonly bool Passed;
        public readonly int Id;
        public FlappyPipe(int id, double x, double gapCenter, bool passed = false)
        { Id = id; X = x; GapCenter = gapCenter; Passed = passed; }
        internal FlappyPipe At(double x, bool passed) { return new FlappyPipe(Id, x, GapCenter, passed); }
    }
}
