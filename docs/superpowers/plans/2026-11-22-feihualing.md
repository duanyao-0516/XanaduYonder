# 飞花令 Implementation Plan

> **For agentic workers:** Use subagent-driven-development. Data, pure rules, and Unity presentation have separate owners and share the data contract below.

**Goal:** Implement offline play in all 16 combinations on the isolated feature worktree with a desktop-built 20,000–30,000-work corpus, approximately 12 MB APK growth, and a five-petal header icon.

**Architecture:** The desktop importer builds a deterministic compressed catalog, including all playable cards, aliases, positions and audited theme mappings. Pure C# owns match state and answers. Unity owns display, composition and lifecycle. The feature owns its versioned save file.

**Tech Stack:** Unity UGUI, C#, Newtonsoft.Json, Python desktop importer and .NET independent checks.

- [x] **Data:** Export the pinned corpus with `scripts/import-feihualing.py`. Document source and review status and verify deterministic output, duplicate identity, possible schedules and theme evidence.
- [x] **Rules:** Implement `Assets/Scripts/Core/Feihualing/` and verify the 16 combinations, answer validation, deadlines, ties, shared hint exposure and restore.
- [x] **Presentation:** Verify the production UGUI pages, hint accordion, flower, composition callbacks, handoff and Android emulator background behavior. Device acceptance of a Chinese IME remains open.
- [x] **Host/storage:** Integrate `AppController`, independent atomic saves and background catalog parsing. Compare matching ARM64 test APKs and verify the Android manifest.

Asset contract is a UTF-8 JSON object in `Assets/Resources/Feihualing/catalog.bytes`, gzip level 9. Numeric IDs in that version are zero-based row indices. `version` is integer 1 and `corpus` is a nonempty version/digest; `works` rows are `[stableId, author, titleOrTune, originalBody]`; `lines` rows are `[workIndex, startUtf16, lengthUtf16, repeatedGroupId, common01, aliases]`, where aliases is a JSON array of accepted alternate strings and offset/length refer to the stored display body. `cards` rows are `[id, kind, title, key, secondKey, width, candidateSets, reasonRows]`; kind 0 = single character, 1 = character position, 2 = two characters, 3 = topic. Empty metadata fields are empty strings or zero as appropriate. `candidateSets` is an array of nonempty arrays of line IDs, one for each position in position mode, one otherwise. Theme evidence rows `[lineId, reason]` describe each admitted line and are empty for other rules. Duplicate groups prevent counted aliases or same text from scoring twice. Normalization keys keep editorial internal breaks.
