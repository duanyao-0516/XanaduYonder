using System;

namespace XanaduYonder.Core.Watermelon
{
    [Serializable]
    public sealed class WatermelonBallState
    {
        public int Id, Level;
        public float X, Y, VelocityX, VelocityY, Angle, AngularVelocity, Age;
        public bool Contacted;
        public WatermelonBallState Copy()
        {
            return new WatermelonBallState {
                Id = Id, Level = Level, X = X, Y = Y, VelocityX = VelocityX, VelocityY = VelocityY,
                Angle = Angle, AngularVelocity = AngularVelocity, Age = Age, Contacted = Contacted
            };
        }
    }

    public sealed class WatermelonMergeResult
    {
        public int FirstId, SecondId, AddedScore;
        public WatermelonBallState NewBall;
        public bool ReachedWatermelon;
    }
}
