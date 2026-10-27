# Third-party notices

The project's original code is licensed under MIT. Third-party software and assets retain their own licenses.

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
- Included unchanged: `Assets/Resources/Watermelon/Fruit/1.png` through `11.png`, `Emoji/1.png` through `11.png`, and `Campus/1.png` through `11.png`, from source directories `assets/images/friuts/`, `qq/`, and `985/`. The original misspelling `friuts` is preserved in the provenance record.
- Included audio: `Audio/Drop.mp3`, `Merge.mp3`, `Win.mp3`, and `Lose.mp3`, copied without re-encoding from source `assets/audio/fall.mp3`, `boom2.mp3`, `cheer.mp3`, and `boom1.mp3`, respectively. File hashes, dimensions and theme order are recorded in [the asset manifest](docs/watermelon-assets.md).
- The source repository's code is licensed under AGPL-3.0. Its README attributes some images and audio to 微伞 (Wesane) and other Internet sources. No Dart/Flutter implementation code is copied or bundled; the C# rules, Unity Physics2D integration, interface and persistence implementation are original.
- These images and audio retain their original rights. The project's MIT code license does not relicense them. Only the 33 images and 4 audio files listed in the manifest are bundled; the source app, QR code, unrelated themes and other assets are excluded.
