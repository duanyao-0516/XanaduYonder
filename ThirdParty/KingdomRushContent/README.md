# Kingdom Rush playable baseline

The active build is restored to the complete game from the previously playable **192.14 MB APK**, whose tower-defense increment was **77.30 MB**. Further code and resource reduction is deferred. The selected gameplay remains the FL first-generation campaign: 32 levels, three modes and 13 heroes, with the original integrated resources retained.

## Exact source and resource baseline

The restored game archive is **70,585,835 bytes**, SHA256 `212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641`. All **2,424** Lua, image, sound, font and configuration files are pinned by `profile/stable-content-manifest.json`. The previous APK is preserved locally in `artifacts/kingdom-rush-1/final-prune/baseline/previous-apk.apk`, SHA256 `247e952d9cf3b6f4732fae13bc2ddf1f289de3f7b2a8643b740a7ffb29d13a6b`.

The original builder assembles this source and media from the user-supplied FL8.0.5 APK and the measured compression inputs. It no longer invokes the experimental final pruning pipeline. The generated `staging/` directory is the full editable source/resource tree. Before replacing `game.love`, the builder checks the exact approved archive hash and every file against the stable manifest; an unexpected change fails the build.

```sh
python3 scripts/kingdom-rush/build-content.py \
  --source-apk '/Users/bytedance/Downloads/v1.0-250209/FL8.0.5手机Lite版-一键安装直玩，更新自动接档.apk' \
  --compression-source '/tmp/krfl805-compression-20261029/build-firstgen' \
  --astcenc '/tmp/krfl-feasibility-20261029/size-audit/tools/bin/astcenc'

python3 scripts/kingdom-rush/content-validate.py \
  --content artifacts/kingdom-rush-1/game.love \
  --lua-pythonlib /tmp/krfl805-compression-20261029/build-firstgen/textures/tools/pythonlib \
  --luajit artifacts/kingdom-rush-1/host-luajit/libs/LuaJIT/src/luajit \
  --report artifacts/kingdom-rush-1/static-validation.json

python3 scripts/kingdom-rush/build-runtime.py \
  --game artifacts/kingdom-rush-1/game.love
scripts/build-kingdom-rush.sh
```

The content validator compiles all 1,294 Lua files and checks the original atlas, audio, direct-file, native profile and mode data. Both Android build entry points require this passing certificate, the exact stable content, and matching content/runtime AAR hashes. They reject experimental pruned archives even if those archives have an older passing report. APK checks cover the embedded archive, ARM64 native libraries, offline permissions, CRC, signature and absence of diagnostic probes.

## Preserved reduction experiment

The pre-rollback work is preserved on local Git branch `p/duanyao.0516/codex/kr1-pruning-snapshot-20261030-193034`, commit `10ce12f905c2112ec5c440b6700903ecc0a46bbe`. A complete filesystem snapshot, including ignored artifacts, native libraries and Unity caches, is stored outside the worktree at `/Users/bytedance/Desktop/code/XanaduYonder-backups/kingdom-rush-before-stable-rollback-20261030-193034/`. Its file manifest and Git bundle support independent recovery.

Experimental pruning tools and evidence are not part of the active build. Continue any later reduction work from the saved snapshot after the playable baseline has been accepted. Do not relax the stable hash gate to silently package a new reduction experiment.

## Device and delivery boundary

The preview remains a separate offline Android package, `com.duanyao.xanaduyonder.kingdomrushtest`. Its save identity stays `xanadu_kingdom_rush_1`; replacement installation must not clear user data. The previously requested lobby logo is retained. The current request is to build and install for user acceptance, without publishing a GitHub Release or launching the game automatically.

The historical stable APK passed a limited OPPO smoke test through early first-level combat and return/re-entry. This is not a claim that every level or mechanic was played to completion. Current build, artifact and installation results are recorded separately from those historical device observations.
