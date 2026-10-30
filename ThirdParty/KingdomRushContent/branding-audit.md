# First-generation presentation audit

The displayed game name is **王国保卫战 · 初代**. The Unity host remains **Xanadu塔防**. `scripts/kingdom-rush/content-branding.py` changes presentation after `content-patches.py`; it does not change level logic, balance, storage keys, original license files, or the immutable source APK.

## Source and attribution

This adaptation comes from the supplied FL8.0.5 Android APK, SHA256 `ac4abdd13e162a13fad5d07234324b2e3953978b53c513b20c61a6b9de3bb0e1`. The source title identifies the modification as FluidLight / FL8.0, by 流辉一枪349 (`liuhui349`). This documentation preserves that provenance while removing edition advertising from product titles. The original selected level 76 history includes “本关来自KRDove版，在此表示感谢。”; that credit is retained here while its suffix is removed from the visible mission narrative. The story preceding that suffix is unchanged. These adaptations do not imply authorship of the original game, artwork, runtime, or modification.

IRONHIDE GAME STUDIO is the original studio attribution, not FL branding. The inspected startup splash remains visible. The original credits content and original source notice/license files remain intact. The `dove_modules/notice/author_words.lua` and `must_read.lua` source notices are not rewritten; the active main/director/slot/map/profile entry chain contains no references to these optional modules or the author/mod-store views.

## Presentation changes

| Surface | Source | Adaptation |
| --- | --- | --- |
| Initial and recreated window | `conf.lua`, `main.lua`, `all/director.lua` | Use the canonical title directly. |
| Active locale title data | `zh-Hans_3.lua`, `en_3.lua` | Replace the FL/author title with the canonical title. |
| Settings footer | `all-desktop/screen_settings.lua` | Replace the FL version/author line. |
| Credits version labels | `all/screen_credits.lua` | Display canonical title with the existing Chinese-capable `sans` font. Credits entries are retained. |
| Version presentation metadata | `version.lua` | Change only `version.title`. |
| Error page header | `main.lua` | Canonical title plus neutral instructions to preserve a screenshot and reproduction steps. Remove author-publication redirect and insulting wording. |
| Selected level 76 history | `zh-Hans_5sp.lua` | Move the KRDove source-credit suffix to this document. |
| Reachable upgrade help | `all-desktop/screen_map.lua` | Replace foreign-generation examples with the first-generation six technology groups and star spending. Preserve the help key, seen state, and purchase behavior. |
| Slot menu | slot template and controller | Reuse `map_background1`, add a dark overlay and canonical title; remove the foreign-edition subtitle node and branch. |

The slot menu keeps `bg_view` as the original `KImageView`, with its original 1920×1080 logical size, anchor, position, and interactive descendants. Its image becomes the already-packaged initial-generation `map_background1` (also 1920×1080). The background overlay and title precede the unchanged menu children and propagate click/down/up events. The title is centered in the background (x=410, width=1100), remaining inside a 1440-wide 4:3 viewport when the background is cropped. `screen_slots.required_textures` explicitly includes `screen_map_123` so the replacement frame is loaded before constructing the menu.

The content profile already removes the map-generation switch, extra spell/tower/hero menus, developer controls, and wave-editor construction. This module does not rename the dormant class names or mutate gameplay routing. The content pipeline separately prunes proven foreign atlas pixels and frame records; merely removing a UI reference does not satisfy physical resource removal.

## Bitmap evidence and physical-removal contract

The ASTC images were decoded locally for visual inspection with the existing `astcenc` tool, without changing source images or running the game. In the inspected 75% texture candidate, logical atlas dimensions remain 2048×2048 while physical dimensions are 1536×1536, ASTC block size 8×8. Therefore logical frame coordinates must be scaled by 0.75 before identifying physical compression blocks; the content pruner also protects blocks sampled by retained frames.

| Frame | Index and texture | Logical `f_quad` | Decision and evidence |
| --- | --- | --- | --- |
| `main_menu_bg` | `screen_slots.lua`, `screen_slots-1.astc` | 6,6,1920,1080 | Remove frame and physical foreign scene. Decoded image visibly contains the ORIGINS logo and third-generation floating-island scene. No alias and no other frame rectangle overlaps it. |
| `main_menu_subtitle_cn` | same index and texture | 6,1687,371,60 | Remove frame and physical subtitle. Decoded image visibly says 王国保卫战：前线. No alias. The template node and its KR2-only display branch are removed. |
| `map_background1` | `screen_map_123.lua`, `screen_map-0.astc` | 7,7,1920,1080 | Retain and reuse for the menu. Existing first-generation map branch selects this frame. |
| `map_background2` | `screen_map_123.lua`, `screen_map-1.astc` | 7,7,1920,1080 | Independent source review proves this full background is selected only by the KR2 branch; content owner removes its frame/pixels. |
| `map_background_0001` | `screen_map_123.lua`, `screen_map-2.astc` | 7,7,1920,1080 | Independent source review proves this full background is selected only by the KR3 branch; content owner removes its frame/pixels. |
| Startup studio logo | `screen_splash-1.astc` | See source index | Retain: decoded pixels show IRONHIDE GAME STUDIO, without FL branding. |
| `credits_new_bg` | credits atlas | See source index | Retain: independent decoded-image review finds generic parchment/tree-root framing, with no proven foreign title, character, or scene. |

`REMOVED_ATLAS_FRAMES` exported by the branding module is the exact two-frame slot-menu reference contract. A separate content-pruning manifest records all actual removals and resulting hashes. This audit does not claim that a `kr3` filename alone establishes foreign content: shared UI framing and effects used by the selected initial-generation levels remain necessary.

## Deliberately stable internal fields

- `slot.liuhui`, `liuhui_hero`, and related saved selections remain unchanged.
- `KR_GAME=kr3`, `KR_TARGET=desktop`, module/resource directory names, and the existing first-generation ID mapping remain unchanged because they select the original runtime and shared asset paths.
- `version.identity`, `version.string`, `version.string_short`, `version.vc`, `version.bundle_id`, and `version.bundle_keywords` remain unchanged. In particular, `all/storage.lua` writes `version.string` into data version/import markers. The visible readers are patched instead of changing persisted values.
- `KR_FL_VERSION`, original diagnostic log filenames, source comments, and author/license notices remain source/diagnostic metadata. They are not menu titles. Diagnostic exports may still contain these original identifiers.
- Unused foreign-generation localization keys remain data; active window-title call sites use the canonical title. This is not a blanket replacement of words inside original stories or license text.

## Verification and integration

Import the module using the same mechanism as `content-patches.py`, then call `patch_tree(stage)`. Run it after the first-generation source adaptations and before foreign frame pruning. It returns before/after SHA256 for changed files. Every reviewed anchor must occur exactly once in original or already-patched form; all files are validated before writes. UTF-8 BOM and existing line endings are preserved where source anchors are replaced.

Verify the actual generated content, then the final APK payload (the explicit expected content path prevents a stale AAR from passing):

```sh
python3 scripts/kingdom-rush/content-branding.py \
  --verify artifacts/kingdom-rush-1/game.love \
  --physical-report artifacts/kingdom-rush-1/physical-media-removal/physical-removal-report.json \
  --report artifacts/kingdom-rush-1/content-branding-verification.json

python3 scripts/kingdom-rush/content-branding.py \
  --verify artifacts/builds/XanaduYonder-kingdom-rush-1-android-arm64.apk \
  --expected-game artifacts/kingdom-rush-1/game.love \
  --physical-report artifacts/kingdom-rush-1/physical-media-removal/physical-removal-report.json \
  --report artifacts/kingdom-rush-1/apk-branding-verification.json
```

Use the actual final APK filename from the build output in the second command. Validation reads nested `assets/game.love`, checks its SHA256 against the explicit expected archive, verifies every presentation anchor including both window-title calls, rejects the old slot-menu frame records, and binds physical-removal report texture hashes to packaged bytes. The physical report also records preserved shared edge blocks: removing the foreign full scenes must not modify compression blocks required by adjacent retained UI frames. Thus the report does not claim zero residual non-sampled padding pixels.

Verification performed against the current staging snapshot:

- All 12 changed Lua sources compiled with the matching host LuaJIT 2.1.1762386122, without executing game initialization.
- A second patch application produced no modifications.
- Corrupting a late anchor caused failure without partial writes to earlier files.
- Pure menu-table comparison proved all existing menu descendants and geometry unchanged after excluding the intentionally replaced subtitle and added presentation layers.
- Static title bounds checked at logical viewport widths 1440, 1920, and 2560; independent review caught and corrected the initial left-aligned title crop risk.
- The existing `sans_bold` mapping resolves to packaged HarmonyOS Sans SC Bold; credits labels switch from numeric-only styling to `sans`.
- Standalone content and nested-APK verification fixtures pass; APK verification rejects missing expected-content input and mismatched content hashes. The final generated content passes all presentation checks and binds all ten masked texture hashes to the physical-removal report; the exact content/report SHA256 values are in `content-branding-verification.json`.

These are source/compile/data checks. Final composite appearance, interaction, and lifecycle need device verification by the parent/user workflow. This module does not launch a device or game and does not itself rebuild `game.love`, AARs, or the APK.
