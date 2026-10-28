// Portions derived from Chromium, Copyright 2024 The Chromium Authors.
// Use of this source code is governed by the BSD-style license in docs/chromium-dino-reference/source/LICENSE.
namespace XanaduYonder.Core.Dino
{
    /// <summary>Trex state in original top-left canvas units; conversion happens only at the model boundary.</summary>
    internal struct DinoMotionState
    {
        internal double Y, Velocity;
        internal bool Jumping, ReachedMinHeight, SpeedDrop;
        internal double WorldY { get { return (93 - Y) * DinoRules.SpriteScale; } }
        internal double WorldVelocity { get { return -Velocity * 60 * DinoRules.SpriteScale; } }
        internal static DinoMotionState Grounded { get { return new DinoMotionState { Y = 93 }; } }
    }

    internal static class DinoMotion
    {
        internal static void Start(ref DinoMotionState state, double originalSpeed)
        {
            state.Velocity = -10 - originalSpeed / 10;
            state.Jumping = true; state.ReachedMinHeight = false; state.SpeedDrop = false;
        }

        internal static void End(ref DinoMotionState state)
        {
            if (state.ReachedMinHeight && state.Velocity < -5) state.Velocity = -5;
        }

        internal static void SetDrop(ref DinoMotionState state)
        {
            if (!state.Jumping) return;
            state.SpeedDrop = true; state.Velocity = 1;
        }

        internal static void Advance(ref DinoMotionState state)
        {
            if (!state.Jumping) return;
            state.Y += DinoRules.Round(state.Velocity * (state.SpeedDrop ? 3 : 1));
            state.Velocity += .6;
            if (state.Y < 63 || state.SpeedDrop) state.ReachedMinHeight = true;
            if (state.Y < 30 || state.SpeedDrop) End(ref state);
            // Equality deliberately retains the airborne flag, matching Trex.updateJump.
            if (state.Y > 93) state = DinoMotionState.Grounded;
        }
    }
}
