# iOS Native Games Implementation Plan

> **For agentic workers:** Use subagent-driven-development for independent source changes, then review and verify the integrated build.

**Goal:** Build the existing Unity-native games for iPhone/iPad and install on the connected iPhone when the Apple toolchain and personal signing account are available.

**Architecture:** Compile out six Android-only lobby entries. Export an iOS Xcode project from an isolated, filtered project copy, keeping the source checkout and Android build intact.

**Tech Stack:** Unity 6000.3.27f1, C#, Python 3, Bash, Xcode, IL2CPP/Metal.

---

- [x] Detect USB iPhone and obtain model / OS / pairing state; install the official Unity iOS module and obtain Xcode.
- [x] In `Assets/Scripts/Runtime/AppController.cs`, wrap the three groups containing the six Android-only entries with `#if !UNITY_IOS`. Validate the actual catalog contains 19 entries, the excluded IDs are absent, and `gold-miner` / `age-of-war` remain.
- [x] Guard `Assets/Editor/KingdomRushAndroidBuild.cs`, `LevelDevilAndroidBuild.cs`, and the Android SDK reference in `ProjectBuild.cs` so the Editor assembly can compile with the iOS target.
- [x] Add `Assets/Editor/ProjectBuildIOS.cs` for iOS preparation, existing scene export, strict build errors, and packed-resource verification. Reuse only platform-neutral input / scene preparation helpers.
- [x] Add a staging/build wrapper under `scripts/` to copy Assets, Packages and ProjectSettings into `artifacts/ios/project`, remove only the declared excluded content in that copy, and run Unity using `-buildTarget iOS`.
- [x] Export Xcode and inspect BuildReport plus generated data for excluded assets and Android native payloads. Validate shell syntax and meaningful staging failure paths before the full export.
- [ ] Build with Xcode, choose the user's Personal Team without collecting credentials, sign, install, and launch on the detected iPhone. If account or system setup requires user interaction, report the exact pending action while completing independent work.
- [x] Review the diff independently and record evidence in `docs/verification-ios-native.md`; retain local branch without merging or pushing.
