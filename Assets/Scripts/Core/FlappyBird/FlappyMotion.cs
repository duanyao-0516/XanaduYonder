using System;
namespace XanaduYonder.Core.FlappyBird
{
    // Fixed-step semi-implicit Euler endpoints; the collision path is the segment between endpoints.
    // Rendering and certificates use these exact rules rather than a separate continuous approximation.
    internal static class FlappyMotion
    {
        internal static void Predict(double y, double velocity, bool flap, out double nextY, out double nextVelocity)
        {
            nextVelocity = Math.Max(-FlappyRules.TerminalVelocity,
                (flap ? FlappyRules.FlapVelocity : velocity) - FlappyRules.Gravity * FlappyRules.FixedStep);
            nextY = y + nextVelocity * FlappyRules.FixedStep;
            if (nextY >= FlappyRules.PlayHeight - FlappyRules.BirdRadius)
            { nextY = FlappyRules.PlayHeight - FlappyRules.BirdRadius; nextVelocity = 0; }
        }
    }
}
