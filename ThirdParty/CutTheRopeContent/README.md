# Offline Cut the Rope content

The source is the immutable 218-file snapshot restored under `artifacts/cut-the-rope-4399-142014/game`. The tracked [source archive](source/cut-the-rope-4399-142014.zip) contains only those original files and the original `evidence/download-manifest.json`, without research screenshots or logs. Its SHA-256 is `e8e5e8295dbcf12b99e513beb909fbd1411f8deae722bbccc5f1afdb192083c8` (19,188,246 bytes). The restoration script checks that hash, every path, the complete 219-entry inventory and all individual file hashes before writing. Existing matching files are retained; conflicting files or symlinks fail explicitly without replacing user data. No network is required.

The main script and download manifest also have pinned SHA-256 values in `scripts/cut-the-rope/build-content.py`; every downloaded file is checked before generation. The original files are never edited. Source provenance and the snapshot's missing optional media are documented in `docs/research/2026-11-01-cut-the-rope-download.md`. These third-party game materials retain the attribution and rights boundaries in [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md); the repository's code license does not relicense them.

The generated package retains the original 17 boxes and 425 ordinary levels, physics, touch input, stars, unlocks, options and 20 available picture rewards. Intro/outro video is omitted; the original final result continuation still sets and saves `gameWon`. Christmas, advertising, rewarded powerups, purchases, remote SDKs and portal navigation are disabled. The menu starts normally, without a test level or unlocked-save fixture.

The game uses the original English bitmap font and hides the Language option through its existing `force_english` feature. Old saves in Dutch or another language become English without changing progress or settings. Current-schema saves are normalized once through the durable storage path; older schemas use their existing upgrade write. The source includes Chinese translations, but its downloaded bitmap fonts cannot render them: even their combined glyph sets lack 216 of the 293 distinct printable characters in the Simplified Chinese strings. The renderer replaces missing glyphs with spaces and does not fall back to an Android font.

## Build

Requirements: Python 3.9 or later with Pillow 11.3 or later (AVIF and DDS support), and Node.js. Browser checks additionally use Playwright and Google Chrome; `PLAYWRIGHT_MODULE` can override the local module path in the verification scripts. No package installation or network request is performed by the content builder.

From the repository root:

```sh
python3 scripts/cut-the-rope/restore-source.py
python3 scripts/cut-the-rope/build-content.py
python3 scripts/cut-the-rope/build-runtime.py
```

The restore command creates the original snapshot in `artifacts/cut-the-rope-4399-142014`; `--output /path/to/snapshot` restores it elsewhere for verification. In that case, pass `--source /path/to/snapshot/game` to the content builder. The content builder produces `artifacts/cut-the-rope/offline`. The runtime builder packages that directory as `Assets/Plugins/Android/CutTheRopeContent.aar`. The content manifest records the source hash, build-input hashes, all packaged file hashes, conversion dimensions and decoded pixel hashes. Do not package a stale output after editing the templates or generator.

The Hx extractor evaluates the hash-verified original decoder in a Node VM, without starting the game. Its output is standard DXT1/DXT5 DDS. Pillow decodes all 89 textures, preserves their complete 4-pixel-aligned dimensions and atlas coordinates, converts premultiplied color channels to straight PNG alpha, and flips rows to match the original PNG upload convention. The remaining 25 images (English font, loader and picture rewards) are decoded from AVIF. The original AAC sound sprite containers and timing tables are copied byte for byte. Only `font-en.png` and `font-en.dat` ship; the English atlas's 11 internal subfonts and the original `kf` language ordering are retained because its main text faces occupy indices 2 and 3. No AVIF, DDS, video or SDK script is shipped.

## Storage and host contract

`window.GameInterface` implements only the explicit methods referenced by the pinned game. Its storage values are the game's serialized strings. In Android, reads and writes use `CutTheRopeNative`; writes and removals must return `true` after durable completion. Browser verification uses namespaced localStorage. A failed write throws, reports its error and remains visible to `CutTheRopeOffline.flush()` until that same key is saved successfully. A successful unrelated tutorial write cannot hide a progress failure.

The host uses `setHostPaused(boolean)`, `setSoundEnabled(boolean)` and synchronous `flush(): boolean`. Pausing cancels active pointers before notifying the game, and also mutes audio. Pauses received while initial assets load are replayed when the game installs its listener. The original menu's `gameReady()` signals native readiness after the adapter and error handlers exist.

The generator also applies small, anchor-checked fixes required by actual offline completion:

- Remove the menu's unconditional intro preload, even though the intro feature is disabled.
- Hold the existing paused gameplay state while the completion Promise resolves, so multiple physics ticks in a slow frame cannot dereference an uncreated transition animation.
- Reconcile the orphan 21st picture with the 20 downloaded images, including gallery layout, reward eligibility and durable normalization of existing saves containing that unavailable reward. Stars, level progress and unlocks are preserved.
- Release the bouncer atlas by its own resource ID, preserving the hook atlas subsequently used by level 12-20.
- Restore the original v3 telekinesis field's intended serializer/read pairing.
- Preserve short pointer presses and their original down/up coordinates until consumed; recycle bounded touch slots, and cancel both input and gameplay drag ownership without executing release actions.
- Keep schema-upgrade writes outside the parser's reset fallback, so a failed durable write cannot erase an existing save. Malformed JSON and unsupported future save versions retain their original reset behavior.

## Verification

```sh
node --test Tests/CutTheRopeContent/*.test.cjs
python3 Tests/CutTheRopeContent/test_content.py
python3 -m http.server 8767 --bind 127.0.0.1 --directory artifacts/cut-the-rope
```

In another shell:

```sh
node scripts/cut-the-rope/verify-browser.cjs --probe
node scripts/cut-the-rope/verify-scenes.cjs
node scripts/cut-the-rope/verify-touch.cjs
node scripts/cut-the-rope/verify-language.cjs
```

The browser verifier performs actual menu and cut gestures, collects the first picture, and enters level 2. The scene verifier initializes and draws all 425 original maps at desktop and phone viewports, then invokes the real last-level result/outro continuation and checks its durable save. Test-only private references are inserted into an intercepted HTTP response in an isolated browser; they are never written into the production package. The touch verifier additionally exercises short taps, canceled taps, backgrounding, repeated secondary fingers and an actual touch swipe win. All verifiers reject external requests and record script/resource errors. Screenshots and JSON reports are stored under `artifacts/cut-the-rope/evidence`.

The language verifier seeds a Dutch save, checks exact progress preservation and one durable normalization write, then clicks Settings, sound, music and Reset/No at desktop and phone viewports. It verifies that Language is hidden, Reset is moved up, only the English font is requested, and reloading does not repeat the migration. Its screenshots, glyph analysis and before/after size evidence are under `artifacts/cut-the-rope/language`. Language changes are covered by this targeted verifier and unchanged map hashes; they do not require solving or redrawing every level again.

These checks distinguish resource closure, original scene initialization and a real first-level completion from solving every puzzle. Android lifecycle, Back, audio focus, native persistence and actual WebView rendering are validated separately by the runtime harness and integrated APK checks; browser success alone is not an Android device claim.
