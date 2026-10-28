using System;
namespace XanaduYonder.Core.FlappyBird
{
    /// <summary>
    /// Constructive generation certificate, not a random-sample claim. At each full-body
    /// pipe crossing the witness lies in center +/- 2 units. Its next segment starts with
    /// a flap, so incoming velocity is immaterial. Two fixed recipes per enabled delta
    /// cover that entire interval and return it to the next interval in exactly 132 ticks.
    /// Consequently every sequence from the finite transition graph has a continuous
    /// legal input path by induction. This does not rescue arbitrary player mistakes.
    /// </summary>
    internal static class FlappyReachability
    {
        internal const double EntryTolerance = 2;
        internal static readonly int[] Startup = { 11, 44, 52, 44, 41 };
        private static readonly int[][] Negative = { new[] { 53, 45, 34 }, new[] { 55, 24, 45, 8 }, new[] { 56, 12, 27, 37 } };
        private static readonly int[][] Positive = { new[] { 56, 36, 40 }, new[] { 54, 24, 46, 8 }, new[] { 10, 54, 37, 31 } };
        private static readonly bool Verified = VerifyContract();
        internal static double Center(int index) { return (.3 + index * .1) * FlappyRules.PlayHeight; }
        internal static void EnsureVerified()
        {
            if (!Verified) throw new InvalidOperationException("Flappy physics changed: the generation certificate no longer holds.");
        }
        internal static int[] Recipe(int source, int target, double entryY)
        {
            if (source < 0 || source > 4 || target < 0 || target > 4 || Math.Abs(source-target) > 1)
                throw new ArgumentOutOfRangeException(nameof(target));
            double error = entryY - Center(source);
            if (Math.Abs(error) > EntryTolerance + 1e-7) throw new ArgumentOutOfRangeException(nameof(entryY));
            int index = target - source + 1;
            double positiveError = Displacement(Positive[index]) - (Center(target) - Center(source));
            return error + positiveError <= EntryTolerance ? Positive[index] : Negative[index];
        }
        internal static bool VerifyContract()
        {
            double startupError = .55 * FlappyRules.PlayHeight + Displacement(Startup) - Center(2);
            if (Math.Abs(startupError) > EntryTolerance || !SafePlan(.55 * FlappyRules.PlayHeight, Center(2), Startup, 0, FlappyRules.FirstPassTick)) return false;
            for (int source=0; source<5; source++) for (int target=Math.Max(0,source-1); target<=Math.Min(4,source+1); target++) {
                int index=target-source+1;
                double delta=Center(target)-Center(source);
                double lower=Displacement(Negative[index])-delta, upper=Displacement(Positive[index])-delta;
                // The two admitted entry intervals overlap and jointly cover [-E, E].
                if (lower>0 || upper<0 || upper-lower>2*EntryTolerance) return false;
                if (!SafePlan(Center(source),Center(target),Negative[index],EntryTolerance,FlappyRules.PipeIntervalTicks)
                    || !SafePlan(Center(source),Center(target),Positive[index],EntryTolerance,FlappyRules.PipeIntervalTicks)) return false;
            }
            return true;
        }
        private static double Displacement(int[] intervals)
        {
            double y=FlappyRules.PlayHeight*.5, start=y, velocity=0;
            // Translation-invariant free motion; these recipes never reach either world boundary.
            for(int i=0;i<intervals.Length;i++) for(int j=0;j<intervals[i];j++) {
                double nextY,nextVelocity;
                FlappyMotion.Predict(y,velocity,j==0,out nextY,out nextVelocity);
                y=nextY;velocity=nextVelocity;
            }
            return y-start;
        }
        private static bool SafePlan(double start, double target, int[] intervals, double uncertainty, int ticks)
        {
            double y=start, velocity=0;
            int elapsed=0;
            double travel=FlappyRules.Speed*FlappyRules.FixedStep;
            double pipeX=FlappyRules.BirdX-FlappyRules.BirdRadius-FlappyRules.PipeWidth+travel*ticks;
            for(int i=0;i<intervals.Length;i++) for(int j=0;j<intervals[i];j++) {
                double nextY,nextVelocity;
                FlappyMotion.Predict(y,velocity,j==0,out nextY,out nextVelocity);
                double minimum=Math.Min(y,nextY)-uncertainty, maximum=Math.Max(y,nextY)+uncertainty;
                if(minimum<=FlappyRules.BirdRadius || maximum>=FlappyRules.PlayHeight-FlappyRules.BirdRadius) return false;
                // Whole-segment vertical enclosure for all admitted entry heights. This is
                // stronger than corner-circle clearance and proves the complete continuum.
                if(pipeX-travel <= FlappyRules.BirdX+FlappyRules.BirdRadius && pipeX+FlappyRules.PipeWidth>=FlappyRules.BirdX-FlappyRules.BirdRadius) {
                    if(minimum <= target-FlappyRules.GapHeight/2+FlappyRules.BirdRadius
                        || maximum >= target+FlappyRules.GapHeight/2-FlappyRules.BirdRadius) return false;
                }
                double contact;
                for(int sign=-1;sign<=1;sign+=2) {
                    double offset=sign*uncertainty;
                    if(FlappyCollision.SweepPipe(FlappyRules.BirdX,y+offset,FlappyRules.BirdX+travel,nextY+offset,
                        new FlappyPipe(0,pipeX,target),out contact)) return false;
                }
                y=nextY;velocity=nextVelocity;pipeX-=travel;elapsed++;
            }
            return elapsed==ticks;
        }
    }
}
