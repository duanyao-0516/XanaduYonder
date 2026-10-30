# Kingdom Rush 1 Android runtime

This plugin runs the selected FL8.0.5 first-generation Lua game inside an internal
landscape Activity in the Unity APK. The Unity hall remains underneath it. The
game package is supplied separately as `assets/game.love`.

## Source and binary boundary

`provenance.json` pins the user's original APK and each of its eight retained
ARM64 native libraries. They are the original SDL3 / LÖVE 12 / LuaJIT runtime,
not the earlier SDL2 / LÖVE 11.5 feasibility runtime. Exact native build inputs
for the author's custom LÖVE binary are not available; these libraries are
imported binaries and are not described as rebuilt from source.
The local `prebuilt/` directory is ignored by Git. A fresh checkout imports these
eight libraries from the user's original APK after checking its complete SHA256,
then checks every library's exact size and SHA256. No author's runtime binaries
are committed by default.
The original `libluajit.so` is byte-identical to the same megasource revision's
ARM64 prebuilt (`LuaJIT 2.1.1762386122`); its hash and source path are recorded.

The SDL3 binary reports `SDL3-3.3.0-11.5-141-g9618942e`. Its corresponding Java
sources come from [love2d/megasource 9618942e](https://github.com/love2d/megasource/tree/9618942ec0fdeea41c0835c2104bcd6d0686effc/libs/SDL3).
`GameActivity` comes from [love2d/love-android 4c65fff4](https://github.com/love2d/love-android/tree/4c65fff4f8b38693aca5d91bc06f254f86a97adf).
Its framework permission calls, null-safe insets/audio-property reads, and host
hooks are locally adapted. The author's extra high-refresh-rate preference is
not imported. `LuaHTTPS` is retained for native Java class resolution, without
adding INTERNET permission. Its source revision is recorded in provenance.

`jni-contract.json` records all 62 Java native method names and descriptors from
the original APK. The build compares the compiled Java declarations against it.
The original native libraries keep their names. They do not collide with Unity's
`libunity`, `libil2cpp`, `libmain` or `libswappywrapper` libraries.

The plugin deliberately omits `libc++_shared.so`. Unity NDK r27c supplies the only
C++ runtime; the builder checks that it exports all 241 symbols used by the
original libraries and supplied by the original APK's C++ runtime. No `pickFirst`
packaging rule selects a version by ordering. Symbol coverage is a static ABI
check, not a complete runtime compatibility test.

## Public launch contract

```java
KingdomRushBridge.open(Activity host, boolean soundEnabled, boolean hapticsEnabled);
KingdomRushBridge.isRunning();
KingdomRushBridge.getLastError();
```

`open` returns whether launch was queued. It rejects duplicate launches while
the Activity or its native teardown is active. An asynchronous launch or startup
failure stores a readable error and returns to the existing hall. `isRunning`
remains true until native teardown has completed. There is no call to terminate
the shared process.

The launcher trampoline finishes immediately when Android restores an existing
task. On a cold task it explicitly starts the Unity Activity named by manifest
metadata. This prevents Unity's `singleTask` entry from clearing a running game
when the user reopens the app icon. `AndroidManifest.xml` in Assets is based on
the installed Unity 6000.3.27f1 Activity-entry template.

Back is translated to Escape, letting the Lua game show its existing pause UI.
The game's explicit return control saves, then calls `love.event.quit()`.
SDL's completion callback finishes the internal Activity. If Android destroys
it first, SDL receives its normal quit event. A timed join is followed by a
background join when needed; native mutexes and AssetManager are destroyed only
after the game thread really stops. New launches remain blocked during that wait.
Android-driven recreation is also checked inside SDLActivity itself; a rejected
new instance finishes without touching the previous instance's static runtime.
Activity pause/resume and input remain owned by the matching SDL3 implementation.

## Data and JNI

`getFilesDir()` exposes only the dedicated `files/kingdom-rush-1` directory to
LÖVE and creates its `save` parent before SDL starts. The Lua profile additionally
uses identity `xanadu_kingdom_rush_1`. Other Unity games' persistence is untouched.
The standalone test package has its own Android application ID and sandbox.

The compiled `libjni_android.so` module supplies the exact required properties
using Android APIs. `jni_android.lua` is only `return require("libjni_android")`.
Unknown properties raise errors; there is no wildcard success/no-op bridge.
Sound and haptics reflect the launch arguments. Lua applies sound to its actual
audio settings; Activity vibration obeys haptics. The legacy
`crashlytics_log_and_crash` name records its real message to Android logs and the
host error channel, without uploading telemetry or killing Unity.

## Reproduce

On a fresh checkout, import the original native libraries and compile the runtime:

```sh
python3 scripts/kingdom-rush/build-runtime.py \
  --source-apk "/path/to/FL8.0.5.apk" --runtime-only
```

The source APK must match the recorded SHA256
`ac4abdd13e162a13fad5d07234324b2e3953978b53c513b20c61a6b9de3bb0e1`.
Only the eight pinned `lib/arm64-v8a/` entries are imported; assets and the
original `libc++_shared.so` are not imported. Missing libraries without
`--source-apk`, a different APK, or modified local binaries fail explicitly.

After building the content package, package both Android plugins:

```sh
python3 scripts/kingdom-rush/build-runtime.py
```

Use `--android-player` to point at another installed Unity AndroidPlayer toolchain,
or `--runtime-only` to compile/check the runtime before content is ready. The
default requires Unity 6000.3.27f1's JDK, SDK API35 and NDK r27c. No `/tmp` reference
or downloaded decompiler is required by the build.

Generated, ignored outputs are `Assets/Plugins/Android/KingdomRushRuntime.aar`,
`KingdomRushContent.aar`, and `artifacts/kingdom-rush-1/runtime-build.json`.
The first AAR holds Java and native libraries; the second stores `assets/game.love`
without an extra ZIP deflate layer. Source hashes, JNI declarations, Java/native
compilation and C++ symbol coverage are checked on every build.
The Unity Gradle postprocessor keeps `.love` uncompressed in the final APK and
uses extracted native libraries, matching the real Lua module search path.
Both AAR manifests explicitly declare minSdk 26 and targetSdk 36. This prevents
Android manifest merging from treating the asset-only AAR as a targetSdk 1
library and adding legacy external-storage or phone-state permissions.

Validation is limited to source/static checks and compilation in this development
step. The target OPPO uses 4 KB memory pages and supports ASTC. Full game startup,
touch, audio, background/foreground, repeat entry, save restoration and all 32
levels are user device tests. This integration makes no 16 KB page compatibility
claim for the original native binaries.
