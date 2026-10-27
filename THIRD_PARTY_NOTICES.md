# Third-party notices

The project's original code is licensed under MIT. Third-party software and assets retain their own licenses.

## Noto Sans CJK SC

- Source: https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese
- File: `Assets/Resources/Fonts/NotoSansCJKsc-Regular.otf`
- License: SIL Open Font License 1.1, reproduced in `Assets/Resources/Fonts/OFL.txt`.
- Copyright notices and the reserved font names are preserved in that license file. The font is bundled unchanged.

## Unity packages

Unity Editor, player runtime, UGUI, IDE integration and testing packages retain the terms and notices distributed with Unity and each package. Resolved versions are recorded in `Packages/packages-lock.json` after Unity imports the project.

`com.unity.nuget.newtonsoft-json` 3.2.2 packages Newtonsoft.Json 13.0.2 (MIT, James Newton-King). The package's included LICENSE/Third Party Notices apply. The standalone persistence tests reference the matching NuGet version.

The 2048 gameplay, UI layout and synthesized sound generation are original. No implementation or artwork from another 2048 game is included. Other games have the specific provenance recorded below.

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
