# Flappy Bird Implementation Plan

> **For agentic workers:** Use subagent-driven-development to implement this plan with independent ownership, specification review, and code review. User approved the design and UI example on 2026-10-28 and requested full implementation in this worktree.

**Goal:** Deliver the approved Flappy Bird card and Unity game, matching the saved UI prototype, with deterministic tap-to-fly rules, lifecycle handling, record persistence, and verified Android output.

**Architecture:** A pure C# rules/session module owns simulation and phase transitions. A dedicated Runtime page owns input, presentation, audio and its record store; AppController provides navigation, shared settings and foreground lifecycle. Keep other games' rules and saves unchanged.

**Tech Stack:** Unity 6000.3.27f1, C# 7.3, UGUI, Newtonsoft.Json 13.0.2, .NET 10 console checks, Android validation package.

## Contracts and ownership

- Core owner: `Assets/Scripts/Core/FlappyBird/`, `Tests/FlappyBird/`, `Tests/FlappyBirdSession/`, corresponding scripts and metadata.
- Persistence owner: `Assets/Scripts/Runtime/FlappyBird/FlappySaveData.cs`, `FlappySaveCodec.cs`, `FlappySaveStore.cs`, `Tests/FlappyBirdPersistence/`, corresponding script and metadata.
- Page owner: other files in `Assets/Scripts/Runtime/FlappyBird/`; implement the approved HTML dimensions, colors and states. Own scene art/audio loading within that module; no shell edits.
- Integrator: AppController, assets, editor checks/build entry, README/architecture/notices, verification and final review. Serial Unity invocations in this worktree only.

Public rules API to keep independent work compatible:

```csharp
// namespace XanaduYonder.Core.FlappyBird
public sealed class FlappyGame {
    public FlappyGame(uint seed);
    public double BirdY { get; } // ground-relative, positive upward
    public double BirdVelocity { get; }
    public long Score { get; }
    public bool GameOver { get; }
    public double ElapsedTime { get; }
    public IReadOnlyList<FlappyPipe> Pipes { get; }
    public FlappyStepResult Step(bool flap); // exactly one FixedStep
}
public struct FlappyStepResult { public bool Flapped, Died; public int Points; }
// FlappyPipe: readonly X (left edge), GapCenter, Passed, Id.
// FlappyRules: Width=360, Height=600, GroundHeight=32.4, PlayHeight=567.6,
// BirdX=.28*Width, BirdRadius=21, FixedStep=1.0/60, PipeWidth=.16*Width,
// GapHeight=.29*PlayHeight, Speed=.38*Width; other values follow spec.
public enum FlappyPhase { Ready, Running, Dying, Result, Paused, Countdown }
public sealed class FlappySession {
    public FlappySession(uint seed);
    public FlappyGame Game { get; }
    public FlappyPhase Phase { get; }
    public int CountdownNumber { get; }
    public void Press();
    public FlappyStepResult Advance(double elapsed);
    public void Suspend();
    public void Resume();
    public void Restart(uint seed);
    public void ClearInput();
}
// namespace XanaduYonder.Runtime.FlappyBird; plain classes testable outside Unity.
public sealed class FlappySaveData { public long BestScore; public FlappySaveData Copy(); }
public sealed class FlappySaveStore {
    public FlappySaveStore(string directory);
    public string SavePath { get; }
    public FlappySaveData Load(out string warning);
    public bool TrySave(FlappySaveData data, out string error);
}
```

Page shell contract:

```csharp
internal sealed partial class FlappyBirdPage : MonoBehaviour {
    public void Initialize(RectTransform root, Action lobby, Action<Action> settings,
        Func<bool> acceptsInput, Func<bool> sound, Func<bool> haptics,
        GameFeedback sharedFeedback, Action<string> status,
        FlappySaveStore initialStore = null, FlappySaveData initialData = null);
    public long BestScore { get; }
    public string SaveError { get; }
    public event Action Changed;
    public void Enter(); public void Leave(); public void Suspend();
    public void SetForeground(bool foreground); public void Back(); public void Save();
    internal void SetHostSaveWarning(string message);
    internal void Advance(float elapsed); // production loop and editor checks
}
```

Explicit test data opts out of production disk reads unless an explicit temporary store is supplied. Page keeps unsaved high scores in memory across leaving and re-entering; active runs are not persisted. UI fixtures must never access normal player storage.

## Tasks

### 1. Freeze approved design and baseline

- [x] Record approved prototype and spec in Git; use current feature branch based on `0f6e4fd`.
- [x] Confirm applicable repository guidance, actual Unity/.NET availability and current device inventory without modifying other worktrees or installing over the user's app.
- [x] Reproduce the Core test project's missing Newtonsoft dependency, fix the test-project dependency boundary only, and rerun its existing production-source tests.

### 2. Rules and session with regression checks

- [x] Write failing tests for first tap, one-tap impulse, gravity, ceiling, collision before score, one score per pipe, terminal freeze and deterministic seeds.
- [x] Implement the contracts above in `Core/FlappyBird`; use fixed stepping and swept contact, bound obstacle storage and integer score growth.
- [x] Verify the allowed discrete gap transitions and continuous playable paths with production simulation; record the generation contract, not just random sampling.
- [x] Write and pass phase/clock tests for Ready, Running, Dying, Result, Paused, Countdown, repeated interruptions and 30/60/90/120Hz render partitions.
- [x] Run `scripts/test-flappy-bird.sh` and `scripts/test-flappy-bird-session.sh` to zero failures; independently review requirements then correctness.

### 3. Independent record persistence

- [x] Write rejection/roundtrip/file-transaction tests using real Newtonsoft before implementing.
- [x] Implement `flappy-bird-v1.json` with schema 1, gameId `flappy-bird`, nonnegative decimal-string bestScore; strict required types, duplicate keys, depth and size limits.
- [x] Implement tmp flush, valid-backup preservation, broken-primary recovery and clear errors. No game snapshot or duplicated global settings.
- [x] Run `scripts/test-flappy-bird-persistence.sh`, including filesystem failures and byte-identical unrelated-game files; independently review.

### 4. Assets and production page

- [x] Preserve original atlas and source manifest; import only used bird/pipe/ground sprite regions or an optimized atlas plus exact slicing metadata. Do not alter approved art styles.
- [x] Implement sky gradient, clouds, hills, smooth scrolling ground, bird flap/tilt and pipe rendering from the approved example; gameplay and visible boundaries share model coordinates.
- [x] Implement the 388×784 reference layout using width-relative scale and a 3:5 contained field; shared Home/Book/Settings, score row and pause, no bottom flap button.
- [x] Implement pointer-down ownership and single submit path; overlays, keyboard, cancellation, background and resize cannot leak input.
- [x] Implement ready/flight/pause/countdown/death/result/help/settings/exit/unsaved UI, including retry and correct prior-state restoration. Shared settings affect synthesized short flap/point/hit sounds and haptics.
- [x] Compile in Unity and compare the same states and usable dimensions against prototype screenshots; fix differences before delivery.

### 5. Shell integration and UI validation

- [x] Add the next lobby card without renumbering existing games; lazy page initialization, sound/haptics delegates and input predicate.
- [x] Add all affected enter/leave, foreground, Back, global-save, status and highest-score refresh paths.
- [x] Implement actual UGUI checks in `Assets/Editor/FlappyBirdUiChecks.cs`; exercise real graphics/raycast events with temporary stores. Cover sizes, input gates, page flow, overlays, max scores and failure retries.
- [x] Add `scripts/test-flappy-bird-ui.sh`; run serially with other Unity operations and inspect output PNGs.

### 6. Build, regression, review and documentation

- [x] Add isolated `com.duanyao.xanaduyonder.flappybirdtest` build entry and `scripts/build-flappy-bird-validation.sh`, restoring project settings via finally.
- [x] Run relevant existing Core, Dino, persistence and UI regression checks; don't claim unrelated suites were run.
- [x] Build fresh Android APK, verify success log/output SHA-256, install/launch only the dedicated test package on an explicitly selected available test device; preserve existing app data.
- [x] Perform reachable device checks for tap/hold/retry/pause/foreground and save reload; report actual device versus emulator evidence and remaining physical-device limits.
- [x] Independent spec and code review, fix concrete findings and rerun affected checks.
- [x] Update README, architecture, notices, approved spec status and verification record; commit only this task's changes and retain this worktree. No merge or push requested.


## Execution notes

Completed production implementation, TDD rules/session/persistence, 176 real UGUI assertions, relevant Core/Dino/Sokoban regressions, independent reviews and isolated Android build. OPPO installation was explicitly requested and completed; subsequent physical-device feedback led to slower gravity, speed and wider/more separated pipes. Final parameters and fresh validation evidence are recorded in `docs/verification-flappy-bird.md`. The user completed OPPO hand-feel validation, accepted the result, explicitly stopped additional tests and requested merge/push to remote master.
