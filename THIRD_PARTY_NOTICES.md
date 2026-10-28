# Third-party notices

The project's original code is licensed under MIT. Third-party software and assets retain their own licenses.

## Chromium Dino runtime and design reference assets

- Source: [Chromium](https://chromium.googlesource.com/chromium/src/+/64574fa0f2daa1b48f2703a072687272000178ce/), revision `64574fa0f2daa1b48f2703a072687272000178ce`, retrieved on 2026-10-27.
- The two original sprite sheets and three sound files under `docs/superpowers/specs/assets/chrome-dino/` remain the source references. The layout illustration uses excerpts from the unmodified sprite sheet.
- Bundled runtime assets: `Assets/Resources/Dino/Sprites.png` is the unchanged 2× sprite sheet; `Audio/Jump.ogg`, `Audio/Hit.ogg` and `Audio/Score.ogg` are unchanged copies of `button-press.mp3`, `hit.mp3` and `score-reached.mp3`. The original audio files contain Ogg/Vorbis data despite their upstream `.mp3` extension; only names and extensions changed, with no re-encoding. The 1× sprite sheet is retained as a design reference and is not bundled as a Dino runtime resource.
- License: Chromium BSD-style license, Copyright 2015 The Chromium Authors. Its complete notice, conditions and disclaimer are reproduced in [LICENSE.chromium](docs/superpowers/specs/assets/chrome-dino/LICENSE.chromium) and bundled unchanged in [Assets/Resources/Dino/LICENSE.txt](Assets/Resources/Dino/LICENSE.txt). The game exposes the complete text through Dino → 玩法说明 → 素材与许可.
- Exact upstream paths and hashes are recorded in [source-manifest.json](docs/superpowers/specs/assets/chrome-dino/source-manifest.json); runtime mappings, hashes and import settings are documented in [the Dino asset record](docs/dino-assets.md).
- These assets retain their upstream terms and are not relicensed by the project's MIT license. This project does not claim Google endorsement.

## Chromium Dino source reference archive

- Source: [Chromium revision 3df8d372a3f9d5c38a89d0bc496cc44c85af1feb](https://chromium.googlesource.com/chromium/src/+/3df8d372a3f9d5c38a89d0bc496cc44c85af1feb/), retrieved on 2026-10-28.
- The original TypeScript game files and `neterror.css` are archived under `docs/chromium-dino-reference/source/` for configuration and behavior research. They are documentation references and are not compiled into the Unity player.
- Original copyright headers and the complete BSD-style [LICENSE](docs/chromium-dino-reference/source/LICENSE) are retained unchanged. These files retain their upstream terms and are not relicensed under MIT.
- Exact upstream paths, decoded byte counts and SHA-256 hashes are recorded in [manifest.json](docs/chromium-dino-reference/manifest.json). Extracted parameters and unit conversions are documented in [the configuration reference](docs/dino-chromium-config.md).

## Chromium Dino rule adaptations

- Dino's C# speed, jump, obstacle-generation and collision-box logic under `Assets/Scripts/Core/Dino/` adapts the pinned Chromium normal-mode rules listed above. The original Chromium copyright notices and BSD-style terms apply to the adapted portions; the full unchanged license is bundled in `Assets/Resources/Dino/LICENSE.txt` and displayed through Dino → 玩法说明 → 素材与许可.
- Unity integration, touch ownership, responsive layout, persistence, continuous collision stopping and holiday decorations are project code. Fixed 60 Hz simulation deliberately makes acceleration independent of the display refresh rate; the source reference archive records the original callback-based behavior.
- These adapted portions are not relicensed under the project's MIT license.

## Offline idiom dictionary

- `hefengbao/jingmo-data`, commit `dc3ed0bc8877512d8ac0b2b93cf6b2420237ec72`, files `api/chinese_idiom_v5_1.json` through `_5.json`: MIT, Copyright 2024 贺丰宝. The independent data repository's license applies; no GPL-licensed jingmo app code is imported.
- `pwxcoo/chinese-xinhua`, commit `fe6d6c2e8baa82187f4c96bbe042e43f96c05666`, file `data/idiom.json`: MIT, Copyright 2018 PWXCOO. Its README describes the data as collected from the web; this notice does not assert independent verification of every upstream text's rights.
- Exact source URLs, SHA256 values and original license URLs are pinned in `Data/Idioms/source-manifest.json`. The importer preserves both license texts in its generated `licenses/` directory and keeps field-level source references.
- The player bundles 42,851 eligible four-character idioms in `Assets/Resources/Idioms/`, with a compressed catalog and 168 detail chunks. Both original MIT license texts are retained in `Assets/Resources/Idioms/licenses/`; resource hashes and the dictionary version are recorded in `Assets/Resources/Idioms/manifest.json`. Full cleaning audit artifacts remain local and are not bundled.

## Noto Sans CJK SC

- Source: https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese
- File: `Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf`
- License: SIL Open Font License 1.1, reproduced in `Assets/Resources/Fonts/OFL.txt`.
- Copyright notices and the reserved font names are preserved in that license file. The font is bundled unchanged.

## Unity packages

Unity Editor, player runtime, UGUI, IDE integration and testing packages retain the terms and notices distributed with Unity and each package. Resolved versions are recorded in `Packages/packages-lock.json` after Unity imports the project.

`com.unity.nuget.newtonsoft-json` 3.2.2 packages Newtonsoft.Json 13.0.2 (MIT, James Newton-King). The package's included LICENSE/Third Party Notices apply. The standalone persistence tests reference the matching NuGet version.

The 2048 gameplay, UI layout and synthesized sound generation are original. No implementation or artwork from another 2048 game is included. Other games and source reference archives retain the specific provenance, authorship and licenses recorded in this document.

## User-supplied jump assets

- Source archive: `跳一跳素材.zip`, supplied by the project owner on 2026-10-26.
- The project owner explicitly confirmed that free redistribution permission has already been obtained for these materials.
- Included unchanged: `Assets/Resources/Jump/Audio/{scale_intro,scale_loop,start,success,pop,fall}.mp3` and `Assets/Resources/Jump/Preview/{platform,pawn}.png` (original archive paths `sound/` and `img/boxs/box/box0.png`, `img/tyt/juese.png`).
- These assets retain their original rights; the repository's MIT license applies to original code and does not relicense these supplied assets. The archive did not include a separate license text or author attribution.
- The 3D meshes, rendering shader, gameplay and integration are original. The reference repository `dee1024/jump-jump-game` was inspected locally, and no code or assets from that repository are bundled.

## User-supplied tile assets (旸了个旸)

- Source archive: `yang_le_ge_yang_all_in_one.zip`, supplied by the project owner on 2026-10-26.
- Included unchanged: `Assets/Resources/Sheep/Tiles/block_1.png` through `block_16.png` and `block_bg.png`, from the archive's `all_in_one/block/theme0/` directory. Only these 17 required tile images are bundled.
- The enclosed `IP免费授权.pdf` excludes game uses and its term has expired. It is **not** the authorization basis for this use. After that restriction was specifically explained, the project owner separately confirmed authorization covering game development and distribution with the installation package: “已明确获得游戏开发的授权，进行完整开发工作” (2026-10-26).
- These images retain their original rights and are not relicensed under the repository's MIT license. The separate authorization confirmation is specific to this project; this notice does not grant independent reuse rights to others. No email template, personal information, or unrelated asset collections from the archive are bundled.
- The displayed game name is 旸了个旸. Game rules, level generator, interface layout, storage, and synthesized audio are original code; no third-party game implementation is copied.

## Project owner app logo

`Assets/Branding/XY_logo.png` is the unchanged `XY_logo.png` supplied by the project owner on 2026-10-26 for the app icon and launch branding. It is used as supplied; Unity produces the platform-specific icon sizes. The repository's code license does not independently grant rights to the project's visual identity.

## Zuma-Deluxe-HD code and original Zuma content

- Fixed upstream: [GalaxyShad/Zuma-Deluxe-HD at 28001e0e5061341d502116586626150027242cce](https://github.com/GalaxyShad/Zuma-Deluxe-HD/tree/28001e0e5061341d502116586626150027242cce). The HD presentation credits GalaxyShad and s4lat; the original game and content credit PopCap.
- The C# rules, source layout/cut rectangles and offline importer are derived from the MIT-licensed source. Its complete copyright and permission notice is reproduced below.
- Upstream expressly excludes `content` assets from the code license: original Zuma images, fonts, sound effects and module music belong to **PopCap Games / Electronic Arts**. These are **not MIT assets**. The project owner authorized this local implementation using the original assets. A separate public redistribution grant has not been supplied or established; this notice is not one. Public distribution of a package containing these resources requires the corresponding rights.
- Included image categories: the four original PNG atlases (`menu`, `frog`, `gameobjects`, `gameinterface`), 18 JPG map backgrounds, the eight referenced PNG overlays, 18 original map thumbnails decoded to RGB PNG and nine PNG bitmap font atlases. The original decoded pixel regions and top-left cut coordinates are retained exactly, without resampling or replacement artwork. Twenty-nine images retain their source file bytes. Twenty-eight non-block-aligned images receive only right/bottom padding to multiples of four so Unity actually serializes ETC2 rather than falling back to RGB/RGBA: ten alpha textures use transparent padding; eighteen JPEG thumbnails become lossless RGB PNG with replicated edge rows. This avoids a second lossy JPEG encode. Per-file manifest fields record original dimensions, pixel hashes, alpha mode and padding extents; source file hashes remain recorded separately. Font metrics are converted to JSON from the matching TXT files. The unused `cancun10` outline blocks are omitted; `cancun12` has 154 kerning keys but 153 values, so the unpaired last key is discarded instead of performing the upstream out-of-bounds read.
- Included audio categories: all 38 original sound effects and eight musical segments decoded from `content/music/zuma.mo3`. Twenty-seven effect OGG files are unchanged. The eleven effects encoded by Xiphophorus libVorbis I 20010615 (Vorbis 1.0 RC1) cannot be decoded by Unity 6000.3.27f1 FSBTool; the importer decodes those exact originals to 16-bit PCM WAV using oggdec/libvorbis, preserving channel count, sample rate and sample count. Their original OGG hashes and decoded PCM hashes are recorded in `audioConversions`; Unity performs the subsequent Vorbis compression, with no replacement sound or additional lossy intermediary. Original order IDs `0,24,28,32,34,36,38,39` select subsongs, not time offsets. Load-screen and main-menu segments split into their actual intro and repeat portions (restart orders 26 and 30); music metadata records source hash, decoder version and sample-accurate loop extent.
- `Assets/Resources/Zuma/manifest.json` records every shipped file hash, source-relative path/hash, source commit, stable map IDs, original source rectangles and the two source rectangles whose unavailable bottom pixels need clipping. `scripts/import-zuma-assets.py --source <checkout>` reproduces the whitelist; `--validate` checks the delivered resources. No desktop executable, BASS, SDL, Expat, MO3 runtime decoder, unused maps, reference screenshots or generated concept images are bundled.
- Offline conversion uses libopenmpt (OpenMPT Project Developers and Contributors, BSD-3-Clause) and oggenc/oggdec/libvorbis (Xiph.Org, BSD-style licenses). These are developer tools only and are not distributed in the application. Converted audio retains the original music's rights.

### Upstream source license

```text
MIT License

Copyright (c) 2023 GalaxyShad

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Authorized watermelon game assets (06 · 合成大西瓜)

- Source: [idootop/watermelon](https://github.com/idootop/watermelon), commit `8af7d8892c0218206a579d4b5717facab2d26d6e`, imported on 2026-10-27.
- The project owner explicitly confirmed authorization to use the materials for this game's development and distribution. This confirmation is the authorization basis for the bundled assets; it does not grant independent reuse rights to other parties.
- Included unchanged: `Assets/Resources/Watermelon/Fruit/1.png` through `11.png`, `Emoji/1.png` through `11.png`, and `Campus/1.png` through `10.png`. Fruit and emoji keep their source numbering under `assets/images/friuts/` and `qq/`; campus slots 1–10 now contain source `assets/images/985/2.png` through `11.png`, respectively. The Ocean University of China image is no longer bundled. The original misspelling `friuts` is preserved in the provenance record.
- Included audio: `Audio/Drop.mp3`, `Merge.mp3`, `Win.mp3`, and `Lose.mp3`, copied without re-encoding from source `assets/audio/fall.mp3`, `boom2.mp3`, `cheer.mp3`, and `boom1.mp3`, respectively. File hashes, dimensions and theme order are recorded in [the asset manifest](docs/watermelon-assets.md).
- The source repository's code is licensed under AGPL-3.0. Its README attributes some images and audio to 微伞 (Wesane) and other Internet sources. No Dart/Flutter implementation code is copied or bundled; the C# rules, Unity Physics2D integration, interface and persistence implementation are original.
- These images and audio retain their original rights. The project's MIT code license does not relicense them. Only 32 images and 4 audio files from this source are bundled; the source app, QR code, unrelated themes and other assets are excluded.

## Sun Yat-sen University emblem

- Source: [Sun Yat-sen University's official introduction page, university emblem section](https://www.sysu.edu.cn/xxg/zdjj1.htm), [original PNG](https://www.sysu.edu.cn/__local/C/8E/79/6236A91AEAFBB9F7F2C23A9F836_97F1F3B5_178FF.png?e=.png), retrieved on 2026-10-27 at the project owner's request.
- Bundled unchanged as `Assets/Resources/Watermelon/Campus/11.png`: 229×229 pixels, 96,511 bytes, SHA-256 `61b24c100d11ada4738623ceb9855a027dee199223b959c506f2f5d2be003f4f`. No cropping, resizing, recoloring or re-encoding was performed; Unity uses the existing circular mask.
- The emblem retains its original rights and is not relicensed under the project's MIT code license. The earlier authorization for assets from `idootop/watermelon` does not cover this separately sourced image; this record documents provenance, not an independent redistribution license.

## Sudoku Exchange puzzle data

- Source: https://github.com/grantm/sudoku-exchange-puzzle-bank at commit `d8c8ebaee0c08c412cfba96af1923dfa61c83317`.
- License: Unlicense, reproduced in `ThirdParty/Sudoku/LICENSE.txt`.
- Bundled file: `Assets/Resources/Sudoku/puzzles.txt`, a selection of 2,000 standard 9×9 puzzles with locally computed answers and difficulty labels. Import provenance and verification are documented in `docs/sudoku-content.md`.
- Only puzzle data is reused. The Sudoku rules, solver, hint explanations, interface and persistence code are original. No Sudoku Exchange web application code, QQWing or HoDoKu code is bundled.
