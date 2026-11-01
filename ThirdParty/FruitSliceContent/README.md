# Fruit Slice offline content adapter

This directory contains first-party host and transformation source. The fixed, versioned input is `ThirdParty/FruitSliceSource/original/` with its sibling `download-manifest.json`; the complete adapted APK content is versioned under `Assets/StreamingAssets/fruit-slice/`. Rebuilding does not depend on ignored `artifacts/` downloads. The manifest records provenance only: the public source and copied files grant no additional license or redistribution rights.

Run from any directory:

```sh
python3 scripts/fruit-slice/build-content.py
python3 scripts/fruit-slice/verify-content.py
```

Both commands accept `--root`. Build also accepts `--output`; verify accepts `--content`. Python 3.9+ and Node (class inspection and syntax verification) are required; no package download or service is used. The builder verifies all 418 original files against `download-manifest.json`, pins the game bundle SHA, rejects changed replacement sites, computes the local dependency closure, and records generated and adapter hashes. It deliberately fails on missing source or content instead of producing a partial game. Two builds from the same inputs produce identical bytes, including the manifest.

## Runtime ownership

The Android host owns safe insets and the lifecycle. `index.html` has a fixed local origin at `https://appassets.androidplatform.net/fruit-slice/index.html`. The game keeps its 720×1280 logical stage using `showall`; the safe WebView viewport letterboxes without changing 3D ray coordinate scaling. This preserves all 50 levels, source models, slice physics, blade effects and sound effects.

`window.XanaduFruitHost` is created before the engine scripts. Its `setPaused(bool)`, `setSoundEnabled(bool)`, `cancelInput()` and `shutdown()` accept calls before game readiness. Pause stops the game/physics/update timers, cancels touch, and mutes audio. All game `Date.getTime()` elapsed-time reads use the host logical clock, excluding background duration. Resume resets each paused Laya timer's last-tick timestamp so suspended WebView time is not applied as a giant physics/timer step. Document visibility is an additional pause owner. Shutdown is irreversible for that page.

Only `XanaduFruitNative.ready()` and `reportError(string)` are used. Ready is emitted once from the first enabled main scene after model and level preload. Uncaught errors, rejected promises and engine resource-load failures report errors; normal game warnings and save failures do not invoke the native fatal screen. Save failures display an in-game message. Writes use the browser storage API directly because this pinned Laya version swallows storage exceptions.

The pinned Laya engine ignores additional fingers when multi-touch is disabled. Gameplay additionally owns one touch id and stores its coordinates; Down immediately ray-casts and Move immediately slices. Cancel clears pending TouchManager click records before releasing UI mouse-up listeners, clears hover/capture and then drops the DOM touch owner, preventing a cancelled system gesture from activating a shop item. The real pinned TouchManager release behavior was exercised in a temporary Node VM check. The missed-fruit and missed-banana paths retain their red-X prefab and original life penalties; the transform rejects constructor references to deleted classes.

All 27 blades are available from the first launch. Tapping either a blade icon or its equip button immediately equips it. The shop has no purchase confirmation, prices, ownership locks, video labels or acquisition countdown. The home-screen timed-blade entry and its global unlock timer are removed.

There is no currency system: balances, diamond reward displays, reward calculations, delayed payouts, triple-reward buttons, charges and ownership writes are removed. The original 3D spawn list contains only fruit, bombs and bananas, with no currency-drop objects. Result screens retain gameplay scores, level stars, level progression and personal records. Revive and replay remain local one-shot actions. The 60-second gameplay clock and bomb time penalties remain intact; the removed acquisition timer is a separate business path. Level arrays are copied before banana-wave insertion so one round does not alter cached future rounds.

The `gongfuqieshuiguo` save retains levels, stars, records, equipped blade and sound/progress settings. A valid legacy equipped blade (0–26) survives even if the old ownership array marks it locked. Removed currency, ownership and acquisition-time fields are ignored when reading and are absent from subsequent saves. The boundary rejects invalid JSON, null/non-object roots, malformed arrays, invalid numbers and impossible equipped ids. It restores defaults for corrupt/missing fields and preserves valid progress. Active flying objects and an in-progress round are not serialized.

## Removed surface and verification limits

The build removes complete game-specific platform SDK/dispatcher/telemetry classes, ad/privacy UI classes, external navigation, promotion/share/privacy/contact UI, fabricated platform leaderboards and their preloads, and platform startup overlays. Revive call sites directly execute local behavior; no SDK success callbacks are faked. Unused ad atlases and other unneeded resources are excluded. Shared original UI atlases can still contain unused image pixels; no removed UI references those frames. Laya's generic same-origin asset loader remains necessary for bundled assets; CSP and the native handler prevent external requests.

`verify-content.py` validates exact output hashes, current adapter hashes, atlas frames and image pages, all local references, all levels, source-level removal of platform/network call sites, the portrait/clock/input contract and JS syntax. It also evaluates the actual game registrations without bootstrapping a browser, validates every scene and retained inline prefab runtime/type through the engine registration rules, and checks inline prefab assets. Removing a required prefab registration fails the build, including declarations referenced only through JSON strings. The original unused treasure-box hand prefab and egg atlas are explicitly excluded after checking constructor, registration-string, scene and inline-view references; the challenge-screen donghua prefab is retained. Runtime/device interaction evidence is recorded outside this directory; static and VM checks do not prove Android rendering, audio or gesture parity.

## Source relocation bookkeeping (2026-11-01)

The fixed source path was changed for repository delivery. Existing generated gameplay files were retained; only the `adapter_files` digest for `scripts/fruit-slice/build-content.py` in `content-manifest.json` was refreshed to match that path-only source edit. This was a metadata update alongside editing, not a content regeneration or a new test, build or verification run.
