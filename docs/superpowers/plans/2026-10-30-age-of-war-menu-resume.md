# Age of War menu resume and help plan

**Goal:** Both edition cards always open their title menu. Resume is selected through Continue inside that menu; One can return from pause to the title menu to select a new difficulty. Each title menu exposes its own help and shared settings.

**Architecture:** Keep the existing independent save files and battle snapshots. The page owns fresh save availability, explicit restore and lifecycle gates. The original scene controls own menu lettering and hit regions. A local landscape UGUI dialog owns edition-specific help and common-setting callbacks, with the page blocking underlying manual pointer input while it is open.

**Original implementation constraints:** Reuse the isolated worktree and preserve the previous autosave implementation. User authorization covers direct implementation, build and OPPO install; no further design approval, no tests or gameplay verification. Merging was deferred until acceptance. On 2026-10-30 the user confirmed acceptance and authorized committing and merging into the latest master without rerunning tests.

- [x] Scene controls: One Play / Continue / Instructions / Exit; One pause return to Main Menu before Exit (lowercase r matches the original embedded font subset); Two GAME / Continue / Credits / Exit; unavailable Continue stays gray and cannot activate. Right-aligned Book and Settings icons only on title menus.
- [x] Navigation: selector always says Enter; Enter always shows title menu. Reload save on Continue, restore paused, and keep the prior save until a new difficulty actually starts. Preserve clear/write failures and retry them before leaving or starting.
- [x] Help/settings: scrollable per-edition help with English-to-Chinese combat/UI terms. Use common sound/haptic callbacks. Modal, focus, Back, close and orientation changes cancel pending pointer input.
- [x] Source review, Android build, preserve-data OPPO install and launch only; record actual outputs and leave device acceptance to the user.

Delivery: build process exited 0 and Unity reported `Build Finished, Result: Success.` APK size 281438143 bytes. OPPO `install -r` returned `Success`; activity launch returned `Status: ok` (COLD). No tests or gameplay verification ran. Changes remain in the isolated feature worktree.
