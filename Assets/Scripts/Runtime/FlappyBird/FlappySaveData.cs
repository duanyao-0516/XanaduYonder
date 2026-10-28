namespace XanaduYonder.Runtime.FlappyBird
{
    public sealed class FlappySaveData
    {
        public long BestScore { get; set; }

        public FlappySaveData Copy()
        {
            return new FlappySaveData { BestScore = BestScore };
        }
    }
}
