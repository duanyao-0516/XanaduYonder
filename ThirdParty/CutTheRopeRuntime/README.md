# Cut the Rope Android host

This independent Activity embeds the pinned offline game in a platform WebView. It has no AndroidX/Gradle/download dependency. Unity calls `CutTheRopeBridge.open(Activity, soundEnabled)` and polls `isRunning()` / `getLastError()`.

The host serves only `https://appassets.androidplatform.net/assets/cut-the-rope/` from the corresponding APK assets, using explicit MIME types and a restrictive CSP. URLs with another origin, explicit ports/credentials, escaped filenames, dot segments, or path separators are denied without network fallback. Only GET is served. Page navigation and additional windows are blocked. The manifest requests no permissions and does not export the Activity. The merged APK must also omit INTERNET permission.

API 33+ uses `OnBackInvokedCallback`; cancellation never invokes the completed event. API 26–32 uses `Activity.onBackPressed`. Two completed Back events within an inclusive 2000ms window return to the hall. Focus loss, pause, configuration change, and destroy reset the first event. The second event pauses the game and awaits synchronous durable `flush()` before finishing; a 3-second timeout becomes a visible host error.

`CutTheRopeNative` stores the game's exact serialized strings in a dedicated SharedPreferences file. `writeSave` and `removeSave` call `commit()` on the WebView bridge thread and return its actual boolean. Failed read/write/remove/flush and WebView load/renderer failures set the Unity error and close the Activity. The JS adapter must write each progress transition, rather than waiting for an Activity lifecycle callback. Lifecycle flushing additionally protects ordinary exits; process termination is not a callback guarantee.

Native window state, audio focus, and Activity pause update `CutTheRopeOffline.setHostPaused` / `setSoundEnabled`. Touch cancellation is sent on focus/pause/configuration changes. WebView pause is deferred until the JS lifecycle update completes, with a lifecycle revision preventing a stale completion from pausing a resumed game. The WebView fills the fullscreen window, preserving the original game's proportional scene layout. Cutout mode is `ALWAYS` on API 30+ and `SHORT_EDGES` on API 28–29; the window and loading surfaces use a black background. System gesture regions retain Android's input priority without adding margins around the canvas. No custom touch-edge Back recognition or gesture exclusions are installed.

Build and tests:

```sh
python3 Tests/CutTheRopeRuntime/run-tests.py
python3 scripts/cut-the-rope/build-runtime.py --runtime-only
# Once artifacts/cut-the-rope/offline is generated:
python3 scripts/cut-the-rope/build-runtime.py
```

The build uses Unity 6000.3.27f1's JDK and Android API 36. It emits deterministic AARs and `artifacts/cut-the-rope/runtime-build.json`, including every packaged content asset's size and SHA-256 for the final APK audit. Host policy tests cover Back time boundaries/reset and routing/traversal; Java compilation covers Android signatures. Android lifecycle, hardware gesture cancellation, audio focus, rotation, renderer recovery, and real device interaction require separate emulator/device verification.

Platform API references: [predictive Back](https://developer.android.com/guide/navigation/custom-back/predictive-back-gesture), [WebSettings](https://developer.android.com/reference/android/webkit/WebSettings), and [WebViewClient](https://developer.android.com/reference/android/webkit/WebViewClient).
