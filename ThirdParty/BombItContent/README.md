# Bomb It / Q版泡泡堂初代

This integration uses the original AVM1 Flash game, not the unrelated Godot
remake. The fixed source is `original/game.swf`; its exact download URL, referer,
length and SHA-256 are in `source-manifest.json`. It has a 550 × 400 stage,
35 fps and 12 root frames. The site wrapper and AS3 score loader are not inputs.

## Reproduce the production payload

```sh
python3 scripts/bomb-it/build-content.py
python3 scripts/bomb-it/build-content.py --verify
```

The production builder needs only Python's standard library and pinned local
inputs. `build()` returns `(files, aar_bytes, patch_report)` for iOS staging
without writing the output tree or AAR. The command writes
`artifacts/bomb-it/content`, `artifacts/bomb-it/patch-report.json` and
`Assets/Plugins/Android/bomb-it-content.aar`. Android assets are under
`assets/bomb-it/`; iOS uses the same files under its existing web-games root.
The manifest lists every served file with its path, size, SHA-256 and MIME.
Both Ruffle WASM variants and both upstream licenses are included. Source maps,
the original SWF, diagnostic builds and audit tools are not production assets.

## Structural removal of the site SDK

`sanitizer/sanitize.py` verifies the complete original hash, decompresses CWS,
then removes precisely six reviewed top-level tags:

- Empty SDK symbols 1 and 603.
- Their `FWAd_AS2` and `__Packages.FWAd_AS2` exports.
- The complete SDK class definition and `Object.registerClass` initialization.

This removes the ad framework, News.swf loader, LocalConnection bridge and
remote load methods from the distributed game. It does not just block their
requests at runtime. The SDK is not placed on the original stage and the game
scripts do not call it. The `FileAttributes.UseNetwork` flag is also cleared.
All other 1,040 root tags, including nested gameplay actions, visuals, audio and
timelines, remain byte-identical. The sanitizer checks this invariant on every
build and rejects surviving SDK names, network URLs and remote-loader strings.

The two original `FSCommand:allowscale` / `FSCommand:showmenu` operations are
display settings, not external navigation. They remain, as do local
`SharedObject` accesses. Native request allowlisting and the adapter CSP provide
additional runtime boundaries independently of the structural edit.

## Independent source audit

The optional `tools/AuditSwf.java` uses JPEXS FFDec **26.4.0**. It is a developer
tool, not a build or runtime dependency. The upstream ZIP is
`https://github.com/jindrapetrik/jpexs-decompiler/releases/download/version26.4.0/ffdec_26.4.0.zip`,
SHA-256 `56ce960aeafc7caf034e2052ba49f878a883f7c7ca690da6eeffca9587b5e4b9`.
With that release unpacked into `artifacts/bomb-it/tools/ffdec-26.4.0`:

```sh
mkdir -p artifacts/bomb-it/tools/audit-classes
javac -cp artifacts/bomb-it/tools/ffdec-26.4.0/ffdec.jar \
  -d artifacts/bomb-it/tools/audit-classes \
  ThirdParty/BombItContent/tools/AuditSwf.java
java -cp 'artifacts/bomb-it/tools/audit-classes:artifacts/bomb-it/tools/ffdec-26.4.0/ffdec.jar' \
  AuditSwf ThirdParty/BombItContent/original/game.swf artifacts/bomb-it/content/game.swf
```

The independent parser checks unchanged gameplay action bytes, the exact two
removed scripts, missing character references and actual AVM1 URL operations.
Re-exporting AS2 source also showed that all 61 retained scripts were identical;
only the two SDK scripts disappeared. Static evidence does not replace actual
gameplay, touch, sound or lifecycle checks.

## Original controls and persistence

- Single player: arrows and Space. Two player: 1P WASD + Space, 2P arrows + Enter.
- Character selection uses direction keys; releasing the bomb key confirms.
  Both players confirm in two-player mode. The original P pause action remains.
- The original `SharedObject.getLocal("bomb_it")` saves player names and the
  local high-score table. It does not serialize a complete active battle.
  Suspending a live Ruffle instance and restoring a terminated app are different
  operations; the adapter must not advertise full battle continuation.

`adapter/` owns mobile controls and host lifecycle integration. It does not
replace original collision, bombs, opponent AI, maps, score or sound rules.

See `NOTICE.txt` for source attribution and retained rights.
