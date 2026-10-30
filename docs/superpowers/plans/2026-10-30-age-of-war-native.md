# Age of War Native Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement isolated tasks with independent review. User authorizes continuous execution without intermediate confirmations.

**Goal:** Integrate both fixed Age of War originals in Unity C#, matching original presentation and behavior.

**Architecture:** Separate source-faithful simulation ports for AVM1 first game and AS3 second game, sharing presentation contracts only. A reproducible offline Flash timeline/asset conversion retains original art, animation origins and frame events. Unity owns mobile input, horizontal presentation and host lifecycle.

**Tech Stack:** Unity 6000.3.27f1, C# 7.3, Android ARM64, .NET 10 tests, FFDec 26.4.0 offline exporter, Python atlas tooling.

## Task 1: Baseline
- [x] Fetch origin/master and merge in existing isolated branch before implementation.
- [x] Run existing Core/Zuma persistence baseline tests and environment checks.

## Task 2: Shared boundary
- [x] Add `Assets/Scripts/Core/AgeOfWar/Shared/AgeOfWarContracts.cs`: versioned game identity, command kinds, HUD snapshot, visual objects, catalog, frame events. Original simulations remain separate.
- [x] Lock adapter method `bool Execute(AgeOfWarCommand command)`, `void Tick()`, `AgeOfWarFrame Capture()`, `int Edition`, `int TickRate`. No Unity dependency.

## Task 3: First game source port
- [x] Under `Core/AgeOfWar/One`, map actual EN/TU/TS/ES/EV and original AS frame scripts to data and simulation. Use original 40Hz timing, AI, projectile variants, skill effects, targeting, base hit rules and in-game PAUSED semantics.
- [x] In `Tests/AgeOfWarOne`, test fixed source values, legal/illegal purchase and refund, train queue, attack frame event, rewards/XP/evolution, turrets, every special, pause differences and win/loss. Run failing source-backed assertions before implementing paths; verify `scripts/test-age-of-war-one.sh`.

## Task 4: Second game source port
- [x] Under `Core/AgeOfWar/Two`, port actual XML/AI data, Unit/UnitsManager, Turret, Projectiles/DynamicObjects/skills, HUD transaction handlers and GameScreen order. Preserve source animation event callbacks and millisecond timing.
- [x] In `Tests/AgeOfWarTwo`, cover all source content, upgrades/prerequisites, populations/queue, type multipliers, XP use, specials, AI events, rewards, pause/speed and end state; run `scripts/test-age-of-war-two.sh`.

## Task 5: Source assets and animation
- [x] Implement reproducible converter in `scripts/age-of-war/`, verify both SWF hashes, embedded package set and generated output hashes.
- [x] Export actual unit/turret/base/background/UI/projectile/skill clips, retaining bounds, frame labels/events, looping and rendering offsets; pack transparent PNG into bounded atlases, retain original audio.
- [x] Generate `Assets/Resources/AgeOfWar/{One,Two}/manifest.json` and resources. Validate symbols, every catalog ID, all frame rects, alpha and source linkage; no silent missing-animation fallback.
- [ ] Add `Runtime/AgeOfWar/AgeOfWarAssets.cs` as resource owner; actual Unity texture/audio import verification before build.

## Task 6: Unity presentation and shell
- [x] Add `Runtime/AgeOfWar/AgeOfWarPage.cs`, `AgeOfWarView.cs`, `AgeOfWarAudio.cs`, `AgeOfWarViewport.cs` and edition-specific original UI. Rendering consumes simulation snapshots; commands enter original transaction paths.
- [x] Use latest Zuma entry/rotation/exit lifecycle pattern; add two AppController entries and narrow routing. Preserve all seventeen existing games and old storage contracts.
- [ ] Render original menu/battle/paused/win/loss screens, original stage aspect, resource HUD, training, turret placement, upgrades, tooltips and mobile camera interaction.

## Task 7: Real verification and delivery
- [ ] Add reproducible Unity visual/interaction checks and isolated `scripts/build-age-of-war-validation.sh`; use dedicated application ID, preserve release version/signing.
- [ ] Run original-source contract tests and relevant existing host regressions; Unity jobs serial within worktree.
- [ ] Capture side-by-side original/Unity evidence for both editions; exercise all content, combat/skills/evolution/termination and actual Android input/lifecycle.
- [ ] Independent spec review then implementation review; fix grounded findings and rerun affected checks.
- [ ] Save verification/source notices and commit isolated branch; report exact completion or remaining mismatch, never label approximate/partial port as original-equivalent.

## Verification progress (not acceptance)

- Baseline master, Core/Zuma/persistence/environment checks complete. Native source-contract suites have passed, but original runtime comparison is actively identifying callback/coordinate differences.
- Source conversion inventory and hash/alpha/frame/font validation pass. Fourteen Two MP3 streams fail Unity FMOD import; PCM WAV derivatives preserve independently decoded sample hashes and retain source provenance. Full Unity playback inventory and Android build remain required.
- Unity has rendered One menus and 12-second combat, and Two menus and battle entry. Expanded UI checks now include all audio, build/sell controls, GPU text filters and real transition snapshots. No final visual parity claim yet.
- Real original One SWF instrumentation is running under Ruffle 0.7.1, with original action bytes preserved. Runtime trace validation is distinct from source-derived unit tests.
