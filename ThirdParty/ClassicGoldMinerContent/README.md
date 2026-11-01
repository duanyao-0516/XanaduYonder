# Classic Gold Miner

The separate Unity lobby item `classic-gold-miner` runs these local H5 adaptations in Android's installed `android.webkit.WebView`. The APK contains the game scripts, images, fonts and audio; it does not contain a Chromium/Chrome engine. The existing Unity `gold-miner` entry, implementation and `gold-miner-v1.json` remain independent.

## Sources

Downloaded on 2026-11-01 from the two user-selected pages:

| Mode | Page | Original archive SHA-256 |
| --- | --- | --- |
| Solo | https://www.4399.com/flash/230816.htm | `736b28140a9f60a33b223c6d7a45cb15fb1be06148e58536affb79ed0ca0f45f` |
| Duo | https://www.4399.com/flash/232532.htm | `7a9d44a86514ac949c4505ba25e3167fe2beda6d430d1e5aeec90384820c922f` |

The untouched downloads and manifests are retained under ignored `artifacts/gold-miner-single-230816/` and `artifacts/gold-miner-duo-232532/`. The tracked `web/` subtree is the actual build input. Solo's missing MP3 alternatives and sponsor assets are not runtime dependencies: the adaptation uses the available local OGG effects and removes the sponsor loading chain. Original gameplay value tables remain in each mode; the two source games intentionally have different level, price and bonus systems. The fullscreen adaptation expands mine geometry and projects the aiming fan for wide screens, so distances and practical difficulty are not claimed to match the original narrow layout exactly.

The solo adapter removes analytics, the sponsor/advertising startup sequence, external font loading and external links. The duo package excludes the website's outer shell. Engine attribution is retained. See `web/solo/PROVENANCE.md` and `THIRD_PARTY_NOTICES.md` for source ownership; the repository's MIT license does not relicense the original games.

## Storage and lifecycle

`shared/host.js` owns the common synchronous persistence/lifecycle contract. Each mode validates and reconstructs its own complete gameplay snapshot. Native files in the app's private files directory are `classic-gold-miner-solo-v1.json` and `classic-gold-miner-duo-v1.json`, written through `AtomicFile`. Browser previews use separate `classic-gold-miner-{solo,duo}-v1` localStorage keys. Invalid data is preserved and surfaced; selecting New Game explicitly permits replacing that mode's saved progress.

Snapshots include the current scene, selected map, entity state, hook position/motion/caught object, remaining timers and delayed rewards, money, inventory and shop prices/purchases. Cosmetic shimmer and merchant blinking may restart. Autosave runs every second and at significant actions; a completed exit waits for synchronous persistence acknowledgement. Backgrounding pauses input, clocks and sound; foregrounding requires tapping Continue.

Gameplay fills the landscape viewport. Solo drops on the first mine press and uses the bottom-right bomb; duo assigns mine presses by left/right half and uses corresponding bottom-left/right bombs. Backgrounds extend behind system cutouts/gesture edges while essential HUD and floating controls honor native CSS safe insets. Characters and ores keep their proportions. Snapshots record bounded layout width for cross-size restoration; earlier preview saves default to the source game width.

Touch during gameplay acts on the first press. Shopping first selects a product and displays its information; a second press on that same product purchases it. The original solo store allows debt, which is retained. The duo store retains its original funds check. Two-player actions use independent pointers; explosives are reserved synchronously before their original delayed effects complete.

Only completed system Back events count: first prompts, second within two seconds saves and returns to Unity. Timeout/background clears the count. The game Activity owns landscape fullscreen and gesture/cutout insets; Unity's lobby stays beneath it.

## Build

```sh
python3 scripts/classic-gold-miner/build-runtime.py
python3 scripts/classic-gold-miner/verify-content.py --verify-aar
scripts/build-classic-gold-miner.sh
```

The generated `Assets/Plugins/Android/ClassicGoldMiner.aar` is ignored and rebuilt from tracked sources. Java compilation uses the pinned Unity Android toolchain, min SDK 26 / target SDK 36. The content audit and Unity prebuild gate reject missing assets, nonlocal dependencies and a stale AAR. The host serves a synthetic HTTPS origin from APK assets, refuses all other URLs/navigation/permissions/downloads and blocks network loads; no INTERNET permission is requested by this feature.

The preview build uses `com.duanyao.xanaduyonder.classicgoldminertest`, separate from the formal application. Existing Kingdom Rush content/build inputs must also pass that game's own gate. Build reports are under `artifacts/classic-gold-miner/`; gameplay/device verification evidence is documented separately in `docs/verification-classic-gold-miner.md`.
