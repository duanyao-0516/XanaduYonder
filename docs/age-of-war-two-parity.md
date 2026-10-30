# Age of War 2 original-runtime differential

The checked-in fixture was recorded from the original `age-of-war-2-7k7k.swf` running in local Ruffle 0.7.1, in Chrome, on 2026-10-30 at 08:29:28 UTC. Replaying its actual per-stage elapsed seconds against the production C# sources passes **597 frames, 4,788 comparisons, zero mismatches** over 4.005 seconds.

## Exact scope

The fixture starts after the original GameScreen finishes loading. Its first render dispatches the original tutorial click and `btnU2` click, then calls the unmodified `GameScreen.render(dt)`. Every sample is taken after the complete original render returns.

Compared fields are player gold, XP, population, unit count, unit x, health, speed, absolute source animation frame (one-based), and current source label. The sample covers the purchase, HUD training queue/timer, creation on the following timer branch, the 0.2-second birth wait, idle, walkStart completion callback, repeated walkCycle, and per-write signed-twip position quantization. Recorded zero-length stage deltas are replayed too.

This is a bounded training and movement differential. It does **not** establish random AI, damage RNG, every projectile, all seven skills, all ages, victory/defeat, audio, or full-game visual equivalence. Source-backed core tests cover additional paths separately. The native RNG seed is arbitrary because the compared player does not fight or meet another unit during this fixture.

## Original-code preservation and instrumentation

`scripts/age-of-war-parity/two/InjectProbe.java` inserts one standalone ABC **after the original ABC definitions**. It does not recompile or edit an original method. The added internal `ProbeGame` subclass overrides `render`, adds setup/trace observation around `super.render(dt)`, and a timer loads it through the original ScreenManager once Preloader is ready. The test deliberately bypasses the opening/menu flow; it does not validate that flow.

The injector serializes and hashes every original ABC before insertion, reloads the generated SWF, and verifies that all original ABC hashes are present with the same multiplicities. The source SWF, generated SWF, injector, probe, and raw fixture hashes are recorded in `Tests/AgeOfWarParity/Two/metadata.json`. The generated copyrighted SWF is not checked in.

The raw CSV retains a final additional paused frame emitted before the browser's asynchronous suspend completes. Replay stops at the **first** DONE marker. It rejects missing/repeated frame numbers and incomplete or empty captures. The fixture is not regenerated from C#.

## Replay

From the repository root, with .NET 10 installed:

```sh
scripts/age-of-war-parity/two/replay.sh
```

The test project compiles `Assets/Scripts/Core/AgeOfWar/Shared/**/*.cs` and `Two/**/*.cs` directly. A replacement independently captured fixture can be passed as the sole argument.

## Re-record in the original runtime

Obtain the original SWF matching the metadata hash, FFDec 26.4.0 `ffdec_lib.jar`, and the recorded Ruffle runtime. The helper creates only the selected temporary output directory:

```sh
scripts/age-of-war-parity/two/prepare.sh \
  /path/to/age-of-war-2-7k7k.swf \
  /path/to/ffdec_lib.jar \
  /path/to/ruffle \
  /tmp/aow-two-recapture
python3 scripts/age-of-war-parity/one/serve.py \
  --directory /tmp/aow-two-recapture/site \
  --trace /tmp/aow-two-recapture/reference.csv \
  --port 8781
```

Open `http://127.0.0.1:8781/` in Chrome. The added probe automatically enters the original game and prints `AOW|ROW` records. At DONE, the harness posts the trace only to that loopback server and suspends Ruffle. Replay the new file:

```sh
scripts/age-of-war-parity/two/replay.sh /tmp/aow-two-recapture/reference.csv
```

The actual dt sequence and frame count depend on browser scheduling; compare against that newly recorded sequence, not a fixed nominal 150-Hz assumption. Record the new hashes/time separately instead of silently replacing the reference evidence.

## Separate original text evidence

`Tests/AgeOfWarParity/Two/TextMetrics` records the independently executed original TextField probe and bounded bitmap readbacks. It confirms source8px window/HUD baseline pitch15px and damage14px pitch15.95px (ascent10.75, descent3.2, leading2). Identity-transform normal-font ROIs match across three quality settings; the damage ROI changes. This does not establish fractional-position or whole-stage raster equivalence. See that fixture's README for clipping and artificial-input limits.
