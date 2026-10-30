# Age of War autosave implementation plan

**Goal:** Both editions resume the last unfinished battlefield after leaving the game or restarting the app; the edition selector shows Continue when a valid save exists.

**Architecture:** Each core owns an explicit versioned binary snapshot, including its random stream and complete object graph. The runtime owns independent per-edition JSON envelopes, atomic replacement and backups. The page saves at one-second intervals, after accepted gameplay commands, and before lifecycle/exit teardown. Restored battles open paused; completed battles write a cleared-slot marker. No elapsed time is simulated while the app is closed.

**Original implementation constraints:** Continue in the current isolated worktree. The user explicitly requests no tests; perform source review and the APK build only, install to the existing OPPO test application, then hand device verification to the user. Merging was deferred until acceptance. On 2026-10-30 the user confirmed acceptance and authorized committing and merging into the latest master without rerunning tests.

- [x] One core: implement `SaveState()` and `RestoreState(byte[])` with bounded input validation and stable object references; preserve removed-but-referenced targets and all skill/projectile timelines.
- [x] Two core: preserve constructor-time unit statistics, AI cursors, animation registration order and callback semantics; restore without replaying damage, randomness or audio events.
- [x] Runtime store: add `AgeOfWarSaveData`, load results and `AgeOfWarSaveStore` with strict schema/edition checks, backups, safe writes and completed-slot markers. Invalid or future saves must not be overwritten silently.
- [x] Host: restore into the existing landscape handoff; save camera, menu and local presentation settings; show save warnings; keep memory state when writes fail and retry; save before clearing battle objects.
- [x] Selector: refresh per-edition labels from real save state, use Continue only for resumable saves, and enter the restored paused battlefield directly. New players keep the existing title/difficulty flow.
- [x] Delivery: inspect integration boundaries and compile the Android package; install with `adb -P 5039 -s 3B6F4NE8A66ZXTRL install -r`, launch only, do not run gameplay or test suites.

Delivery evidence: Android build exited 0, Unity reported `Build Finished, Result: Success.`; APK size 281429684 bytes. OPPO installation returned `Success` and launch returned `Status: ok` (COLD). No tests or gameplay verification ran.
