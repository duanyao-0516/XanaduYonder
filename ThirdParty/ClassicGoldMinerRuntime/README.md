# Classic Gold Miner Android host

This source-only Android library uses the installed **system `android.webkit.WebView`**. It contains no browser engine, downloaded Java dependency, JNI library, network permission, exported Activity or launcher entry. Unity starts the dedicated landscape Activity and keeps its lobby below it.

Build from a clean checkout with Unity 6000.3.27f1 Android Build Support installed:

```sh
python3 scripts/classic-gold-miner/build-runtime.py
python3 scripts/classic-gold-miner/verify-content.py --verify-aar
```

`UNITY_ANDROID_PLAYER` or `--android-player` can point to a matching Android Player toolchain. Java 8 bytecode compiles against API 36; minSdk is 26 and targetSdk is 36. The output is `Assets/Plugins/Android/ClassicGoldMiner.aar`. Assets come exclusively from tracked `ThirdParty/ClassicGoldMinerContent/web/`. The sorted archive uses fixed ZIP timestamps/permissions; input hashes, toolchain version, bytecode hash and the full asset inventory are embedded in provenance. Reports are written under ignored `artifacts/classic-gold-miner/`. No Unity build, installation or device access occurs in these scripts.

## Boundary and bridge

`ClassicGoldMinerBridge.open(Activity, boolean sound, boolean haptics)` accepts a Unity Activity and returns whether launch was accepted. `isRunning()` covers both pending launch and the native Activity lifetime; launch failure/timeout clears it. `getLastError()` supplies a launch/recovery diagnostic. The haptics flag is accepted for the lobby contract; the original H5 games do not issue vibration actions.

Only `https://classic-gold-miner.local/` is recognized. Every GET is intercepted and resolved under `assets/classic-gold-miner/`; all other URLs/methods receive a local denial. Only the root, solo and duo index documents can navigate. File/content access, new windows, permissions, downloads and mixed content are blocked. Network loads are disabled. CSP permits local script/style/image/audio/font/XHR use plus generated image data/blob URLs; remote sources, workers, frames, objects, forms and eval are disallowed. Script files use external scripts; `.mjs` has JavaScript MIME. The two vendor libraries contain inert attribution/namespace strings and a generic `file://` loader comparison; the content auditor distinguishes these from concrete remote dependencies.

JavaScript receives `ClassicGoldMinerNative.readSave(mode)`, `writeSave(mode,json)`, `getLastSaveError()` and `isSoundEnabled()`. Modes are exactly `solo` and `duo`; JSON must be a bounded single object with numeric `schema:1` and matching `mode`. Mode-specific scene/entity validation belongs to each game adapter. Atomic files are named `classic-gold-miner-solo-v1.json` and `classic-gold-miner-duo-v1.json` in app-private files. The old Unity `gold-miner-v1.json` is never opened. Reads preserve corrupt data and report it; writes acknowledge only after sync, atomic commit and committed-byte verification.

## Lifecycle and failure behavior

API 33+ uses the committed `OnBackInvokedCallback`; older devices use `onBackPressed`. First Back prompts. A second committed Back within 2000 ms pauses and saves through `ClassicHost.pauseAndSave('exit')`; only a `true` callback finishes. Failure/timeout offers retry or cancel. No predictive gesture start/progress/cancel event is counted. Backgrounding clears Back state and invalidates outstanding exit callbacks.

Background first evaluates synchronous JS pause/save, then pauses this WebView after its callback (bounded fallback at 1500 ms). It never calls the process-global `WebView.pauseTimers()`. Foreground resumes the WebView, reasserts JS pause/save and displays the shared continuation overlay; adapters reset their clocks when the user resumes. Lifecycle and page generations reject stale callbacks. Cutout and system-gesture insets pad the game surface.

Renderer termination destroys the failed WebView and offers reload from the **latest successful save**, or return to the lobby without overwriting that save; the dialog explicitly warns that later progress may be lost. This exceptional return is user-selected and does not claim the interrupted frame was saved. Load/start failures likewise offer an explicit recovery/return path so Unity cannot remain permanently busy.

The content auditor checks entry documents, static dependencies, forbidden URLs/elements, paths, JSON/font references, permission-free manifest and packaged hashes. Dynamic loader paths, Android lifecycle scheduling, predictive Back gestures and real touch/gameplay require device validation; a passing Java build/static report does not prove them.
