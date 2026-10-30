# Age of War original asset conversion

The Unity editions load native transparent atlases, original audio, fonts and a JSON manifest. They do not include a Flash player, WebView, executable SWF, or a screenshot substituted for a battlefield.

## Fixed originals and reproduction

The converter accepts only these SHA-256 hashes:

| Original | SHA-256 |
| --- | --- |
| `age-of-war-1-7k7k.swf` | `5f88517846ff0eb75eee3bd6dfebbf9fad75edbb4dbe20ed8ea4c0afb3dfd3a1` |
| `age-of-war-2-7k7k.swf` | `b7dc9f9654add56e87234381e5272695592e3837bbd8489f836ed7225b81960c` |

Requirements: Java 17, JPEXS FFDec 26.4.0, Python 3 and Pillow, and FFmpeg/FFprobe 9.0.2 for the verified MP3 decode workaround. The default paths refer to the research copies on the development machine; all are overridable. Source extraction is performed automatically when the analysis directories are absent, including the 30 embedded SWFs and the sponsor's streamed audio.

```sh
python3 scripts/age-of-war/convert.py \
  --source /Users/bytedance/Downloads/age-of-war-research-20261030 \
  --ffdec /tmp/aow-tools/ffdec-26.4.0/ffdec.jar \
  --java /opt/homebrew/opt/openjdk@17/bin/java \
  --cache /tmp/aow-native
python3 -m unittest discover -s Tests/AgeOfWarAssets -v
python3 scripts/age-of-war/validate.py --report /tmp/aow-native/asset-validation.json
```

Use `--output /tmp/aow-native/release-staging` to generate a complete resource tree without modifying Unity's imported files. `--skip-render` reuses raw frames only when the original SWF, complete render job and Java exporter fingerprints match. PNG packing is deterministic. FFDec TTF export timestamps in `head` are normalized and the corresponding checksums are rebuilt; glyphs, character maps and metrics are retained byte for byte, making fresh exports reproducible. The importer retains the original 1:1 stage-pixel scale, disables mipmaps and texture resizing, and uses ETC2 RGBA8 on Android.

`source-art.zip` retains the original exported vectors, morphs, raster images, audio, fonts, text and ActionScript source outside Resources. `source-metadata.zip` retains every source timeline's frame labels, draw order, matrices, color transforms, filters, instance names, collision/bullet-point geometry and the precise rasterization recipes. `one-actions.json` is the source-backed AVM1 action/animation event map. The original SWFs remain external inputs; do not change their hashes to accept another version.

## Manifest and runtime contract

`Assets/Resources/AgeOfWar/{One,Two}/manifest.json` has schema version 1. Resource paths omit file extensions and are relative to Unity Resources.

- `sources`: fixed original and embedded-package names, byte counts and SHA-256 hashes.
- `atlases`: bounded 2048-square RGBA PNGs, resource paths, dimensions and file hashes.
- `clips`: unique `(key, action)` pairs, original source character/frame interval, optional composition transforms, frame events and exact durations.
- `frames`: atlas rectangle measured from the PNG top left, original Flash registration origin measured from the trimmed frame top left, source frame number, exported pixels per original stage pixel, empty-frame flag, duration and a hash of cropped RGBA pixels. Identical consecutive frames share one record with a duration; identical pixels across clips share an atlas rectangle.
- `scenes`: independently renderable original layers, original transforms/depth/alpha, named controls, original button hit regions and dynamic text bounds/style/character identity.
- `sounds`: original numeric IDs, source linkage names and the short names used by the simulation, with distinct source and release-file hashes. Verified FMOD-incompatible MP3 sources additionally record decode method, original rate/channels, sample count and matching independent PCM hashes.
- `fonts`: original embedded TTFs, plus explicitly identified device-font records when the original contains no glyphs.

`AgeOfWarAssets.Load(int edition)` owns an edition's resources. `GetClip`, `GetScene`, `TryGetFrame`, `LoadSound`, `LoadFont` and `GetFontDefinition` use explicit keys. Missing clips/actions return no result; they never substitute another animation or edition. `TryGetFrame` receives a **zero-based frame relative to the active action label**. `AgeOfWarSpriteFrame.SourceFrame` is the original **one-based absolute source frame**, including the offset inside a compressed static run. Its `Origin` and `Size` are original stage pixels, independent of atlas coordinates.

For a unit visual at `(X,Y)`, place the bitmap's top left at `(X-Origin.x,Y-Origin.y)` before applying the visual's scale/rotation. Sprite pixels are premultiplied/composited by FFDec during offline rendering; Unity receives ordinary RGBA PNGs. The original Flash Y axis points down.

## Edition-specific original structure

One uses `unit/1..16` with the original `idle`, `walk`, `attack`, `die`, `shoot` and `shootwalk` labels when present. Unit images include the `699.units` offset, `687.anims` transform, action-layer transform and the separate ground shadow. Unit 12 keeps the original negative X scale. Unit 16 uses the exact original ColorMatrix filter from sprite 687, frame 16, rather than a guessed tint. `turret/1..15` retains the original complete turret animation as action `idle`.

One's `base/{age}` is raw sprite 8; `base-extension/{slot}/{age}` is raw sprite 14/20/26. Their parent placement belongs to the simulation. The healing aura is `aura`. Original randomized particle shapes are `part/2` and `part/8`, actions `variant0..variant11`. Independently advancing random fire sprite 589 is removed from its baked parents and exported under `nested/{parentSymbol}/{depth}`; the simulation owns its random starting frame and original 31-to-3 loop. Sound-only sprite 244's misleading editor artwork is excluded.

Two uses all `unit/1..29` and `turret/1..17`. **Every original label is a separate action**, including `attackMeleeHit`, `attackRangedHit`, `walkAttackHit1` and `shootLaunch`. The core owns label-end callbacks. A turret's action `base` is its static mounting, while `idle`/`shoot`/`shootLaunch` contain only the cannon; aiming must rotate the cannon without rotating the mounting. Some originals have an intentionally empty base symbol, recorded explicitly in source provenance. `base/{age}` is sprite 59's body with tower extensions removed; `base-extension/{slot}/{age}` retains each raw tower. The helper markers `bp`, `bp2` and `hitzone` are recorded as metadata and excluded from artwork.

UI scenes retain the original names. One includes `one-units/{age}`, `one-turrets/{age}`, `one-main/{frame}`, `one-special/{age}`, `one-base-enemy`, `build-slots`, `one-cursor`, and `one-reward`. `special-charge/idle` retains all 1000 charge frames and its original inverted color/alpha transform. The main menu's button UP states are intentionally transparent: visible menu wording is a different source layer. Their clickable HIT geometry is retained and clipped to the stage.

Two's `buttons/1` contains all original age-specific buttons at their authored positions; visibility is selected by the original catalog and UI state. It is placed at the original HUD offset `(10,19)`. `ui/hud/74` provides all 301 authored special-delay frames (the final two are empty), separately from the buttons. Status/overlay scenes include `status-progress`, `status-queue`, `error-popup`, `window/*Info`, `window/sellTurretInfo/onT`, `damage-normal` and `damage-critical`. Dynamic text includes `characterId`, which lets the presenter hide the source sample text and draw current values in the same location. Original `letterSpacing`, wrapping/multiline flags, margins, indentation and leading are retained; dimensions are stage pixels. Dynamic fields also retain their placed-instance filters, including the five HUD DropShadow instances. Font layout metrics are copied from the original layout tags, normalized to 1024 units per em; a font without source layout is explicitly marked `hasLayout=false`. The build and turret sell controls expose `/out`, `/over` and `/down` scenes with their original instance filters and independently advancing cross animation.

One has 27 defined sounds and one real streamed intro track. The general FFDec sound export also produces a zero-sample `-1.wav` from an empty stream header. That empty file stays in the source archive and `excludedSources`; it is excluded from playable resources. The nonempty stream inside sprite 1008 is exported independently as `intro-stream`.

The Unity 6000.3 audio importer rejected Two source IDs 3, 4, 6, 9, 13, 17, 20, 24, 32, 34, 35, 47, 48 and 49 with an FSBTool/FMOD internal error. These fourteen release files use PCM S16LE WAV decoded from the retained original MP3 bytes. Every PCM sample is checked against an independent decode; rate and channels are unchanged. The other forty MP3s and the sponsor WAV are byte-identical source exports. Unity imports the derived WAVs as PCM to avoid a further lossy encode. A successful minimal Unity import of sound 6 established this workaround; the host validates all release clips after synchronization.

Seventeen One sounds declare positive MP3 `InitialLatency`. Their playable WAVs contain exactly the source interval `[InitialLatency, InitialLatency + SoundSampleCount)`, verified byte for byte against the corresponding slice of an independent full PCM decode. The manifest retains the original file hash, complete decoded hash, trim boundaries and trimmed PCM/file hashes. The importer keeps all WAV samples as PCM. The seven zero-latency One MP3s and Two music sources whose FFmpeg gapless decode differs from their declared count remain original MP3s; Unity's observed sample counts match their source declarations. Two sound 16 has a source/decoded duration discrepancy that remains unresolved and is not silently trimmed.

Original opening material includes One `preloader`, `preloader-progress`, `loaded`, `intro-lead`, `intro-background`, and 160 `intro/{frame}` scenes, plus Two `preloader`, `sponsor-initial`, `sponsor` and 66 `author/{frame}` scenes. `opening-timelines.json` records the original controls, completion transitions, sound events and lack of a skip action. One's 26 fire instances remain separate native-clock layers; `clock` and `birthFrame` identify their timing contract. The native opening helper must follow original instance lifetime semantics. These assets do not fabricate a network-loading duration. Stream seek metadata is retained separately from full decoded stream audio; exact Flash seek/continuous-play alignment remains part of runtime reference comparison.

## Validation and limits

The tests verify catalog completeness, visible actor art, original menu hit regions, unique action identity, frame accounting and runtime audio names. `validate.py` additionally checks every source hash, atlas hash, rectangle, registration origin, RGBA crop hash, alpha classification, referenced scene clip, original sound file and embedded font file. Run it again after copying staged resources into Unity. The generated report records the actual counts for each edition.

These checks prove conversion integrity. Unity compilation, screenshots, original-versus-native interaction comparison and Android import/runtime behavior are separate validation performed by the host task.

The original explicitly requests device fonts with zero embedded glyphs: One's font 52 is Arial Bold and font 1011 is Castellar Bold. The loader prefers the installed requested family. For Arial on a device without it, the manifest explicitly identifies the original embedded Arial 197 as a portable fallback. Castellar has no embedded substitute and is reported as unavailable to the presenter. This is a font availability limit of the fixed SWF, not an invented original font. Do not describe an Android fallback as pixel-identical to a desktop's device font.

Original random nested animation phase, parent/child draw ordering and dynamic text/filter rendering must remain represented by the native simulation and presenter. War-machine death sprite 685 is split into `die-back` (depths through 11), independently animated fire at depth 12, and `die-front` (depths from 13); all retain the original frame indexing. The native presenter must maintain this ordering, and final occlusion remains part of real original/native comparison. Retaining all source images does not by itself prove those runtime interactions are identical.

The current release has 584 clips and 196 scenes for One, and 912 clips and 184 scenes for Two. A fresh extraction from only the two fixed SWFs, followed by a cached recipe-verified repeat, produced 163 byte-identical release files. The 23 Python tests and complete pixel/source/audio validator pass. Native opening flow, exact stream alignment, and Flash quality-mode comparison remain separate host/runtime work.


## Original quality candidates

`quality_build.py` produces nominal LOW (no vector antialiasing and no bitmap smoothing), MEDIUM (2×2 vector coverage) and HIGH (4×4 vector coverage) at **one output pixel per original stage pixel**. MEDIUM/HIGH retain source bitmap-smoothing settings. The pinned FFDec 26.4.0 `SerializableImage` source is patched only in the offline tool cache so LOW applies to filtered intermediate graphics contexts as well as outer images. No patched Java library ships in the game.

HIGH replaces the default Two atlas set; it is not duplicated. LOW and MEDIUM are under `Two/Quality/{Low,Medium}` and reuse the original sounds/fonts. All three preserve the same 912 clip/action identities, source-frame intervals, events, scenes and pivots. Run the quality pass after the ordinary conversion, with the ordinary output as both `--base` and `--output`, to reproduce the combined resource tree:

```sh
python3 scripts/age-of-war/quality_build.py --base /tmp/aow-native/release-staging --output /tmp/aow-native/release-staging
python3 scripts/age-of-war/quality_validate.py --root /tmp/aow-native/release-staging
```

The three 1× variants each occupy 32 square 2048 atlases. PNG file sizes are LOW 27,564,231 bytes, MEDIUM 50,004,951 bytes and HIGH 70,472,625 bytes. The two additional PNG sets therefore add 77,569,182 bytes (about 74.0 MiB), plus their manifests. Each full set represents 128 MiB in ETC2 RGBA8 or 512 MiB in uncompressed RGBA; those are format-size bounds, not observed scene residency or APK growth. The loader loads only referenced atlases and releases the preceding generation after the synchronous presenter rebind. Real APK compression and device memory are measured by the host build.

`SetQuality(string, Action rebind)` updates `Quality` and `Revision` before invoking the mandatory synchronous rebind callback. The presenter must refresh every visible sprite using its existing key/action/tick. Old sprites and textures remain alive until that callback returns. If it fails, old resources remain owned until disposal so partially rebound images are not invalidated. Ten C# lifecycle assertions with Unity API test doubles cover this contract; actual Unity rendering is a separate check.

These are actual original-vector raster variants, not a texture-filter switch. They do not establish pixel equality to Flash rasterizing vectors after arbitrary world transforms. Native dynamic text antialiasing and original-player pixel comparison remain separate verification. `quality-validation.json` records crop hashes, bounds, catalog equivalence and a visible unit sample where LOW has 0 partially transparent edge pixels, MEDIUM has 270 and HIGH has 414.

A recipe-verified repeat of the quality pass reproduced all 99 atlas/manifest files byte for byte. Source text settings are audited separately in `text-quality-audit.json`: Two has 40 normal embedded fields and two advanced CSM fields. Its native dynamic text is still rendered from the real embedded TTF through Unity; the three atlas modes must not be described as completing that text rasterization contract.
