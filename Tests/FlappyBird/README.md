# Flappy rules verification

Run `scripts/test-flappy-bird.sh`. The console project compiles the production
`Assets/Scripts/Core/FlappyBird/*.cs` files as C# 7.3 without Unity.

The physics contract is fixed 60 Hz semi-implicit Euler: apply a requested flap,
apply gravity and terminal speed, calculate the next position, then soft-clamp the
ceiling. Collision sweeps the circle continuously along that step's endpoint
segment against the moving full-width cap and inset stem rectangles and the ground.
Cap height, stem width, inset and overlap offsets are shared with presentation. It preserves rounded
corners. All geometry stops at the first contact; only strictly earlier full-body
pipe crossings score. This is an explicitly discrete simulation, not a claim to
solve continuous accelerated motion analytically.

## Generation certificate

Five gap centers are available: 0.30, 0.40, 0.50, 0.60, and 0.70 of playable height.
Only equal or adjacent centers are enabled. The design's maximum 0.20-height
change is a limit; two-level jumps are not in the proven whitelist.

`FlappyReachability` checks all 13 enabled directed transitions. At the instant a
bird's full body clears each pipe, the constructed witness has height within two
world units of the gap center. The next segment's first input resets velocity.
For each height delta, two fixed flap recipes cover that entire entrance interval
and return to the next interval after exactly 132 production ticks. Whole-segment
vertical enclosures certify clearance for every height in the admitted intervals,
including the full horizontal overlap period, with extra radius clearance. Exact
production circle sweeps at the interval extremes are also checked. The two
endpoint displacement intervals overlap and cover the full entrance interval.

A 192-tick startup recipe connects the real initial bird state to the first
interval. The first pipe's left edge reaches the bird center at approximately
2.472 seconds; the full body clears it at 3.2 seconds. These finite checks give a
compositional argument by induction for every generated sequence, not merely each
pair in isolation or a sampled set of random runs. A changed parameter that breaks
the certificate prevents game construction. Reachability means some legal input
path exists; it does not guarantee recovery from arbitrary player errors.

The suite additionally replays 1,001 pipes for each of four seeds through the real
game, checking all five levels occur, every crossing scores once, geometry stays
finite, and obstacle storage remains bounded. Those long runs are regression
checks supplementing the interval certificate, not its proof.

Session checks live in `Tests/FlappyBirdSession` and run with
`scripts/test-flappy-bird-session.sh`. They cover 30/60/90/120 Hz replay partitions,
exact-contact death timing, queued-input clearing, invalid-frame pauses,
interruption-state restoration, full restartable countdowns, and the 350ms guard
starting when Result becomes visible (after the separate 450ms death presentation).
Pointer ownership belongs to the runtime page and is verified separately there.


## Gentler tuning after OPPO feedback

The device user reported that descent and approaching obstacles left too little
reaction time. The production parameters now use gravity `2.2H/s²`, flap speed
`0.80H/s`, terminal downward speed `0.95H/s`, pipe speed `0.30W/s`, and gap height
`0.32H`. Pipe intervals are 132 ticks (2.2 seconds), giving 237.6 world units
between pipe left edges. This increases physical spacing as well as time; the
previous spacing was 218.88 units. The first pipe's earliest full-width horizontal
body overlap is approximately 2.278 seconds after starting.

With the actual fixed-step integration, an isolated first flap reaches its peak
at tick 21 (0.35 seconds), returns below its starting height at tick 43 (about
0.717 seconds), and reaches the ground at about 1.267 seconds. The continuous
formula estimates 0.364 seconds to the apex and 0.727 seconds to the same height;
those estimates are not substituted for production-physics verification.
The allowed bird-center half-height inside a gap grows from 61.302 to 69.816 units.
Slower pipe motion also lengthens each pipe's horizontal body-overlap window to
about 0.922 seconds, so automated reachability does not establish human comfort.

All 13 same/adjacent directed connections retain the original full-segment,
full-width enclosure proof and the +/-2-unit entry interval. The new flap recipes
(listed as tick intervals between impulses) are:

| Gap delta | Negative endpoint error recipe | Positive endpoint error recipe |
| --- | --- | --- |
| -0.10H | 53, 45, 34 | 56, 36, 40 |
| 0 | 55, 24, 45, 8 | 54, 24, 46, 8 |
| +0.10H | 56, 12, 27, 37 | 10, 54, 37, 31 |

Startup uses `11, 44, 52, 44, 41` ticks and ends 0.946 units below the first gap
center. The rules suite locks the requested reaction-time envelope and numeric
parameters and replays all 4,004 pipes; the session suite replays the updated
101-pipe input timeline at 30/60/90/120 Hz. The certificate is regenerated rather
than bypassed, and the enabled five height levels are unchanged.
