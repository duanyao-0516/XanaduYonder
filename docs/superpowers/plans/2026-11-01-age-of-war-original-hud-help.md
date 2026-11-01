# Age of War original HUD help implementation plan

> **For agentic workers:** Use subagent-driven-development for the independent HUD and help-content tasks. User requested direct implementation, no more mockups, and no tests.

**Goal:** Keep the original game UI, move only the second edition's top-right controls slightly inward, and add a paused icon guide to both games.

**Architecture:** `AgeOfWarView` owns the original HUD placement and help hotspot. `AgeOfWarPage` owns pausing, dialog lifetime, and restoration. The existing `AgeOfWarMenuDialog` presents source-backed current-era content without executing battle commands.

**Tech Stack:** Unity C#, original sprite atlases and tooltip data, existing UGUI dialog.

- [x] Preserve a temporary copy outside the repository and remove the rejected touch HUD implementation and mockup proposal documents.
- [x] Merge latest `origin/master` into the existing isolated branch (fast-forward to `1daf099`).
- [x] Restore original single-click controls, scene viewport, icon sizes, and bottom HUD.
- [x] Add `mobile-battle-help`; move AoW2 original top-right images, hitboxes and dynamic overlays inward together, while preserving bottom artwork.
- [x] Pause before opening the guide; suppress battlefield input while reading, then restore the prior pause state without changing speed or spending resources.
- [x] Finish current-era original icons, bilingual names, real tooltip values, upgrade status, and source review; include the original second-edition Options and Game speed controls.
- [x] Build the current release package and replace-install it on the connected OPPO, preserving app data.

Delivery: `scripts/build-release.sh` exited successfully; `adb -P 5037 -s 3B6F4NE8A66ZXTRL install --no-streaming -r` returned `Success`. APK: `artifacts/builds/XanaduYonder-0.9.4-android-arm64.apk`, 406,191,342 bytes, SHA-256 `a72c0e3b051f4a75417771c0c01111065c6c23dbd0d0b582ac1061dfd377087b`. The latest master also includes Cut the Rope content. No game was launched or played for validation.

No unit tests, automated gameplay, or agent-operated phone gameplay verification were performed. Android compilation and installation above were delivery steps. The user accepted the installed result on 2026-11-01 and requested committing and merging it to remote master without further tests or compilation.
