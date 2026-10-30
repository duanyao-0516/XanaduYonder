# Kingdom Rush playable content

The FL8.0.5 first-generation integration contains 32 levels, three challenge modes and 13 heroes. The user accepted the restored playable version on OPPO and authorized merging and pushing it to `master`. Further code and resource pruning remains deferred. No new GitHub Release has been published.

## Accepted baseline and allowed changes

The accepted game archive contains **2,424 files**, **70,585,835 bytes**, SHA-256 `212cb79ede90d140159d66663bf3d23eeb68d984f862b4091dbe121122a18641`. Every baseline file is pinned in [profile/stable-content-manifest.json](profile/stable-content-manifest.json). The historical reference APK was **192.14 MB**, with a **77.30 MB** increase over the old host APK; those figures are not the size or increment of the current master integration.

This integration retains every baseline file. Its only existing-content change is the footer hook in `all-desktop/screen_slots.lua`; removing that hook must reproduce the original file exactly. The other **2,423 files must retain their original SHA-256**. The only addition is `all-desktop/xanadu_start_menu.lua`, sourced from [menu/xanadu_start_menu.lua](menu/xanadu_start_menu.lua). The resulting content has **2,425 files**, including **1,295 Lua files**. Existing fonts, gameplay, levels, textures and audio remain unchanged.

The new module adds top-right help and settings buttons to the start screen. Help explains map dragging, building and upgrading towers, hero selection and movement, wave-path preview and early-call rewards, skills and challenges. Settings opens the game's existing music/sound-effects panel and retains its save behavior; it does not synchronize settings back to the Unity hall. Help uses the baseline's existing Chinese font.

The builder first verifies the exact baseline, then permits only this menu change. Content validation and Android build checks must bind their reports to the resulting archive and AAR hashes. Unexpected deletions, resource changes or extra files must fail validation. Results for the current integration are recorded separately in [the verification record](../../docs/verification-kingdom-rush.md).

## Reproduce

The source APK and measured compression inputs are local build inputs. The original source/resource generation remains active; the experimental pruning pipeline is excluded.

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

The standalone preview uses `com.duanyao.xanaduyonder.kingdomrushtest`; its save identity remains `xanadu_kingdom_rush_1`. Replacement installation must preserve user data. The hall now includes Kingdom Rush as entry 19 in all three views, uses the existing castle icon for compact views, and displays the newly supplied logo unchanged in Single mode with Zuma's geometry.

## Preserved experiment and verification boundary

The previous pruning experiment is saved on local branch `p/duanyao.0516/codex/kr1-pruning-snapshot-20261030-193034`, commit `10ce12f905c2112ec5c440b6700903ecc0a46bbe`. Its complete filesystem snapshot is at `/Users/bytedance/Desktop/code/XanaduYonder-backups/kingdom-rush-before-stable-rollback-20261030-193034/`. It is not the active build.

The user accepted the restored baseline and subsequently accepted the new menu, logo and lobby integration on OPPO, authorizing v0.9.0. This does not establish complete coverage of every level. Content verification, UI checks, preview installation and formal release results are tracked separately in the linked record and `docs/releases/v0.9.0.md`.
