# Third-party notices

The project's original code is licensed under MIT. Third-party software and assets retain their own licenses.

## Sokoban design and content preparation (not yet bundled in the player)

- Kenney Sokoban: [official page](https://kenney.nl/assets/sokoban), [author's OpenGameArt upload](https://opengameart.org/content/sokoban-100-tiles). CC0, Kenney Vleugels. Original PNG sizes, spritesheets, tilesheets, vector files and license are retained in `ThirdParty/Sokoban/kenney-sokoban/`. The UI prototype references these local images.
- Kenney Interface Sounds: [official page](https://kenney.nl/assets/interface-sounds), [author's upload](https://opengameart.org/content/interface-sounds). 100 OGG files, CC0, Kenney Vleugels; retained with the original license in `ThirdParty/Sokoban/kenney-interface-sounds/`. Not played by the UI prototype.
- Both asset folders contain `source-manifest.json` with download URLs, archive SHA-256 and hashes of retained files. The original archives contain promotional links that are not imported; retained resource bytes are unchanged.
- [Google DeepMind Boxoban levels](https://github.com/google-deepmind/boxoban-levels), commit `78011d8c86ad423bfb2e1005012393b4c3c24409`, Apache-2.0: `unfiltered/test/000.txt` is archived unchanged in `Data/Sokoban/boxoban/`, with both upstream license files and README including the requested author citation. No upstream game implementation is copied. Derived candidate lists and solution witnesses are explicitly marked as local verification outputs in `Data/Sokoban/verification/`.
- These resources currently support design and content preparation outside `Assets/`; they are not part of the Unity player. The two maps used in the prototype are original demonstration layouts. Microban is researched but not imported.

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

All gameplay code, UI layout and synthesized sound generation in this repository are original. No implementation or artwork from another 2048 game is included.

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

## Sokoban graphics and feedback audio (08 · 推箱子)

- Graphics: [Kenney Sokoban](https://kenney.nl/assets/sokoban), by Kenney Vleugels, CC0. The archived source and its unchanged license are in `ThirdParty/Sokoban/kenney-sokoban/`. Archive SHA-256: `d8cc8af649a5e21d87a9f30a8a616fe2c0e94e68c80cdfe0df8ca72ec0f208ac`.
- Included unchanged in `Assets/Resources/Sokoban/Sprites/`: wall `block_06`, normal crate `crate_02`, occupied-goal crate `crate_05`, and four outlined player directions `player_05`, `player_08`, `player_17`, `player_20`, all from the source's `PNG/Default size/` directory. Only these seven PNGs are bundled.
- Audio: [Kenney Interface Sounds](https://kenney.nl/assets/interface-sounds), by Kenney, CC0. The archived source and its unchanged license are in `ThirdParty/Sokoban/kenney-interface-sounds/`. Archive SHA-256: `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232`.
- Included unchanged in `Assets/Resources/Sokoban/Audio/`: `tick_001.ogg` as `move.ogg`, `click_001.ogg` as `push.ogg`, `confirmation_001.ogg` as `goal.ogg`, and `confirmation_002.ogg` as `complete.ogg`. These four clips are 0.023–0.539 seconds long. Both original Kenney licenses are included in the released `Licenses/` directory.

## Google DeepMind Boxoban level data

- Source: [google-deepmind/boxoban-levels](https://github.com/google-deepmind/boxoban-levels), fixed commit `78011d8c86ad423bfb2e1005012393b4c3c24409`, source file `unfiltered/test/000.txt`. The complete original 1,000-level file and both original license files are archived in `Data/Sokoban/boxoban/`.
- License: Apache-2.0. The selected 100 maps in `Assets/Resources/Sokoban/campaign.json` retain their exact source rows and source IDs. Their derived solution witnesses and the original eight tutorial maps are identified separately in the release manifest. The source dataset labels are not presented as a human difficulty grade.
- Both original Apache license texts and the unchanged upstream README with its complete author/BibTeX citation are included in `Assets/Resources/Sokoban/Licenses/`. The canonical attribution is “An investigation of Model-free planning: boxoban levels,” Arthur Guez and coauthors, 2018; the upstream citation includes all authors.
- Only level data is reused. The game rules, UI, persistence and verification tooling are original. Third-party media and Boxoban data retain their own licenses and are not relicensed by the repository's MIT license. Per-file hashes, map hashes, exact selected-source mappings and production-rule replay evidence are in `Assets/Resources/Sokoban/manifest.json`, `verification-report.json` and `Data/Sokoban/campaign/README.md`.
