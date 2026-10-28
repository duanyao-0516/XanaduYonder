# Whole miner reel animation implementation plan

**Goal:** Replace detached arm pieces with six whole illustrated miner, arm, handle and winch frames, as explicitly approved by the user on 2026-10-28.

**Architecture:** Keep all changes in the Gold Miner presentation layer. Each player's view owns an independent six-frame loop. Actual decreasing rope length gates playback; paused or stationary hooks cannot animate. The painted machine has a fixed rope outlet registered to the existing hook origin by measured source-pixel pivots. The existing hemp rope and caught-object sprites remain in use.

**Tech Stack:** Unity 6000.3.27f1 UGUI, C#, built-in imagegen, strict asset manifests, Android validation package.

- [x] Generate and visually inspect one coherent 3×2 frame sheet for each player using the existing orange/teal miner style. Preserve the original generated PNG bytes. Measure slice rectangles, feet, axle and rope outlet; save prompts and SHA metadata in `docs/gold-miner/production-assets-v4-reel.json`.
- [x] Extend `GoldMinerAssetImporter.cs` and `Tests/GoldMinerAssets` with a strict two-file/twelve-sprite contract without changing earlier manifest requirements. Verify source bounds, pivots, alpha, identity, ETC2 RGBA8, no mipmaps and no CPU readability.
- [x] Replace `GoldMinerWinchView.cs` split rig with a whole sprite. Cache frames once; advance independently only during actual retraction. Heavy motion cycles roughly 0.8–1.0 s, light motion roughly 0.5–0.7 s; preserve fixed origin and rope continuity.
- [x] Update `GoldMinerMechanicsChecks.cs` to exercise six-frame playback, stationary/paused behavior, player independence, stable rope anchors and HUD clearance. Preserve caught-load and rope regressions. Inspect actual engine screenshots of every frame in single and cooperative layouts.
- [x] Run asset, mechanics and UI checks; independently review source and visual output. Build a clean Android validation APK, verify package identity/signature/hash and size delta.
- [x] Install the validation package on OPPO with current save hashes preserved, without interrupting unrelated app work. Record precisely which device/runtime checks ran and deliver a reviewable preview plus APK. No commit/push was requested.

用户已确认体验无问题并要求停止检验；Android 包安装与哈希核对完成，后续模拟器交互停止。转入 master 合并与 v0.6.0 发布。
