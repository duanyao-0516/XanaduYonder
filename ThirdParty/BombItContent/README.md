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
The offline mobile bridge changes seven additional, individually hash-pinned
root tags: append actions to root frames 1/3/8/9 and sprites 519/554, and replace
the two pause actions of button 533. The original six action blocks are preserved byte for byte apart from their terminal
ActionEnd; their functions are not recompiled. All other root tags, resources,
AI, collision, bombs, maps and sound data are unchanged. Every build verifies
these exact mutations and rejects any additional change.

The original full forbidden-token scan, including ExternalInterface, still runs
against every retained original byte. Only the separately locked root bridge
may introduce ExternalInterface, with six fixed inbound callbacks. Outbound notification uses only the fixed
`FSCommand:xanadu-state` with an empty argument. The bridge files are checked against source and binary
SHA-256 values in `sanitizer/bridge/lock.json`. No runtime, network URL or loader
is added. The report explicitly sets `gameplayTagsByteIdentical: false`.

The two original `FSCommand:allowscale` / `FSCommand:showmenu` operations are
display settings, not external navigation. They remain, as do local
`SharedObject` accesses. Native request allowlisting and the adapter CSP provide
additional runtime boundaries independently of the structural edit.

## Independent source audit

The optional `sanitizer/AuditBridge.java` uses JPEXS FFDec **26.4.0**. It is a
developer tool, not a build or runtime dependency. The upstream ZIP is
`https://github.com/jindrapetrik/jpexs-decompiler/releases/download/version26.4.0/ffdec_26.4.0.zip`,
SHA-256 `56ce960aeafc7caf034e2052ba49f878a883f7c7ca690da6eeffca9587b5e4b9`.
The extracted jar is also pinned by `sanitizer/regenerate-bridge.py`.

```sh
mkdir -p artifacts/bomb-it/bridge-classes
javac -cp artifacts/bomb-it/tools/ffdec-26.4.0/ffdec.jar \
  -d artifacts/bomb-it/bridge-classes ThirdParty/BombItContent/sanitizer/AuditBridge.java
java -cp 'artifacts/bomb-it/bridge-classes:artifacts/bomb-it/tools/ffdec-26.4.0/ffdec.jar' \
  AuditBridge ThirdParty/BombItContent/original/game.swf \
  artifacts/bomb-it/content/game.swf ThirdParty/BombItContent/sanitizer/bridge
```

The independent parser checks 53 unchanged scripts, six exact append-only
patches and the two exact pause-button replacements. It also checks the two
removed SDK scripts, missing character references and actual AVM1 URL operations.
The previous `tools/AuditSwf.java` describes the initial SDK-only integration; its
all-gameplay-identical assertion intentionally does not accept the new bridge.

To regenerate the reviewed bridge after editing its AS2 sources:

```sh
python3 ThirdParty/BombItContent/sanitizer/regenerate-bridge.py \
  --ffdec-jar artifacts/bomb-it/tools/ffdec-26.4.0/ffdec.jar
```

Review the `.as` changes and regenerated `.bin`/`lock.json` together. Ordinary
content builds need only Python and do not invoke Java. Diagnostic SWFs, parser
outputs and browser evidence stay under ignored `artifacts/`; they are never
served by the production content manifest.

## Game bridge and real pause

Load the player with `parameters: {xanaduSession: String(generation)}` and, only
for a confirmed mode restart, `xanaduMode: "1"` or `"2"`. The bridge registers
after original root-frame-1 initialization. Wait for `booted: true`, which is
published after original root-frame-3 menu initialization, before declaring the
host ready. A mode request then sets the actual `totalPlayer` and enters original
`menu3` (difficulty), followed by original `select` (characters) and `game`.
Never change totalPlayer in an existing round; the host destroys/recreates the
player for confirmed mode changes and rejects stale session callbacks.

Ruffle 0.7.1 only constructs ExternalInterface when both `allowScriptAccess:
true` and `allowNetworking: 'all'` are set (`web/src/builder.rs:638` in the
pinned upstream version). The latter enables the local bridge in Ruffle; it is
not a grant of Android network permission. The package still adds no INTERNET
permission, retains `openUrlMode: 'deny'`, has CSP `connect-src 'self'`, and uses
the native host's fixed-origin/resource allowlist. The original networking/ad
code remains structurally removed and hash-audited as above.

Ruffle's `player.ruffle().callExternalInterface(name, ...args)` exposes only:

- `xanaduState()`: `{version:1, session, booted, phase, mode, manualPaused,
  hostPaused, paused, remainingTime, rootFrame, stage, continueInfo, saveOk,
  saveError, restoring, soundEnabled}`.
- `xanaduSetHostPaused(boolean)`: idempotently sets the combined host reason;
  returns the same state shape.
- `xanaduSetManualPaused(boolean)`: idempotently sets the shared manual pause;
  returns the same state shape.
- `xanaduContinue()`: from an original menu, enters the saved campaign through
  the original selection/game initialization. Ignore when no valid checkpoint.
- `xanaduFlushSave()`: explicitly flushes local data and returns state; the host
  must check `saveOk` before claiming save/exit success.
- `xanaduSetSoundEnabled(boolean)`: synchronizes the real original sound flag
  and the active menu/game music track; returns state.

`phase` is loading/menu/difficulty/select/game/gameover. `remainingTime` is a
read-only number during an initialized battle, otherwise null; it includes the
active fraction since the last 500ms timer tick. Changes of phase, mode or pause
state send only `fscommand("xanadu-state", "")`. Before loading, the host registers
`player.ruffle().addFSCommandHandler(handler)` for this fixed command. Its closure
captures that player's generation; a microtask checks the current generation,
then calls `xanaduState` to read current state and validate its session. Notifications
carry no state snapshots. Original allowscale/showmenu commands are ignored by
this handler. This avoids reentering Ruffle inside a callback and rejects late
notifications from a destroyed player.

Ruffle's outbound ExternalInterface.call uses JavaScript new Function, which the
strict CSP blocks. It is deliberately not used. The FSCommand handler calls a
registered function directly; no unsafe-eval permission, runtime fork or
continuous JavaScript polling is needed. Settings/help/mode confirmation/background/exit are host
reasons; closing one cannot clear a manual pause or another active reason.

Both original pause-button conditions (click/lowercase p and uppercase P) call
the same manual function. The original game pause implementation and its
two-frame pause/resume label remain in use. Gameover is distinguished from a
manual pause using the actual timer lifecycle; host resume never clears the
original gameover state.

The original countdown uses getTimer, which advances while Ruffle is suspended.
On the effective running-to-paused transition, the bridge records that clock;
on the final resume it shifts `timer.t0` by exactly the paused duration before
the host resumes Ruffle. It preserves the active sub-500ms fraction and does not
give extra time on repeated pauses. While paused, timer callbacks cannot overwrite
the saved baseline. The host must set the pause reason before suspend, and clear
it while still suspended before resume. It resumes only when the returned state
has no manual or host pause. The root bridge does not change Ruffle's clock.

Static byte/parser checks do not replace real countdown, touch, audio, mode and
lifecycle verification. In particular, a frozen screenshot alone is not evidence
that an elapsed-time-based game timer is correctly paused.

## Continue campaign checkpoints

The original SWF only saved names and high scores. The integration adds one
local automatic campaign slot at `SharedObject.getLocal("bomb_it").data.xanaduCheckpoint`;
original `data.myObj` remains intact. It stores schema/source identity, mode,
difficulty, stage, and all four characters' IDs, remaining lives and accumulated
scores. `continueInfo` is null or a summary with mode/difficulty/stage,
heroId1/heroId2, life1/life2, totalScore1/totalScore2 and totalStages.

Continuing restarts that stage with a freshly generated original map and 180
seconds. It does not restore positions, placed bombs, active effects, pickups,
per-round scores or temporary upgrades. The existing original rules reset those
on a new round. CPU lives and accumulated scores are preserved too.

Checkpoint lifecycle:

- First round, next round and retry: save the actual round-start campaign state.
- Stable victory result: save the next stage after original bonuses/life changes.
- Nonfinal loss with remaining lives: save the same stage after settlement.
- Final victory or no remaining human lives: clear and flush the slot.
- Selecting a new mode/character does not discard the old slot; actually starting
  the new game's first round replaces it.

Restore first runs original frame 8 to build characters and key maps. After
original frame 9 resets stage/lives/scores, the bridge restores the campaign and
sets stage to savedStage-1; original game setup then advances to savedStage.
`restoring` only blocks input while these actions execute; it must not itself
suspend the interpreter. Original gameover is wrapped after its full body has
executed, so checkpoint results include final bonuses and teammate-life changes.

Every stable write/clear explicitly flushes and checks the Ruffle storage result.
`saveOk: false` and `saveError` mean the host must retain the player and offer a
retry instead of acknowledging a successful exit. A capacity error may recover
through `xanaduFlushSave()`. An invalid current checkpoint cannot be reported as
saved just because flushing an older checkpoint succeeds; that validation error
remains until a valid round checkpoint is formed or the slot is deliberately
cleared. Read validation rejects incompatible schemas/source identity, invalid
modes/difficulties/stages, duplicate characters, and invalid life/score values.

## Original sound setting

`state.soundEnabled` reads the original root `soundon`. The original music button
updates that same flag and therefore publishes a state change through the fixed
FSCommand. `xanaduSetSoundEnabled` stops both possible music instances when
muting and only restarts the track for the current stage when enabling. Repeating
the same setting does not restart a track. Host lifecycle muting/suspension is
separate from the user's persistent sound choice.

Pass load parameter `xanaduSoundEnabled: "0"` or `"1"` to preserve the host choice
across player recreation. The bridge reapplies it after original menu music
initialization, which otherwise sets soundon=true. No new network request or
permission is used for saving or audio.

## Original controls and persistence

- Single player: arrows and Space. Two player: 1P WASD + Space, 2P arrows + Enter.
- Character selection uses direction keys; releasing the bomb key confirms.
  Both players confirm in two-player mode. The original P pause action remains.
- The original `SharedObject.getLocal("bomb_it")` saves player names and the
  local high-score table. It does not serialize a complete active battle.
  The integration now adds the explicitly described campaign checkpoint above;
  it still must not advertise exact mid-battle continuation.

`adapter/` owns mobile controls and host lifecycle integration. It does not
replace original collision, bombs, opponent AI, maps, score or sound rules.

See `NOTICE.txt` for source attribution and retained rights.
